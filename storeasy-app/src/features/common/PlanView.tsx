import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Badge, StoreTag, Tag } from '@/components/ui/Badge'
import { DiffView } from '@/components/ui/Code'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Button, Field, Textarea } from '@/components/ui/primitives'
import { apiErrorMessage, apiStatus } from '@/lib/api/http'
import { OrgPermission } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { plansApi } from './api'
import { useP2 } from './i18n/useP2'
import { contentKeys } from './keys'
import { changeActionLabel, channelLabel, fieldLabel, permissionLabel, planKindLabel, planStatusView } from './labels'
import { ChangeAction, ChangePlanStatus, type ChangePlanDto } from './types'

// One change plan (design 7.6, 11.9): the diff, the warnings, the store calls and files, and the decision. The server
// decides again on every call; the buttons only follow the caller's permission (design 11.11: a plan the caller cannot
// approve keeps its button, disabled, with the reason under it).

export const MIN_REJECT_REASON = 10

export function PlanBody({ plan }: { plan: ChangePlanDto }) {
  const t = useP2()
  const format = useFormat()
  const status = planStatusView(plan.Status, t)
  const items = plan.Items ?? []
  const warnings = plan.Warnings ?? []
  const calls = plan.Calls ?? []
  const files = plan.Files ?? []
  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center gap-2 text-[13px] text-fg2">
        <Badge tone={status.tone} dot>
          {status.label}
        </Badge>
        <StoreTag store={plan.Store} small />
        <Tag>{planKindLabel(plan.Kind, t)}</Tag>
        {plan.VersionString ? <span className="mono">{plan.VersionString}</span> : null}
        <span>{interpolate(t.plan.createdBy, { name: `${plan.CreatedByUserName ?? '-'} (${channelLabel(plan.Source, t)})` })}</span>
        {plan.Status === ChangePlanStatus.Pending ? (
          <span>{interpolate(t.plan.expires, { date: format.dateTime(plan.ExpiryDate) })}</span>
        ) : null}
      </div>
      {items.length === 0 ? <p className="text-[13px] text-fg3">{t.plan.noChanges}</p> : null}
      {items.map((item, index) => (
        <DiffView
          key={`${item.Group}-${item.Locale}-${item.Field}-${index}`}
          title={
            <span className="flex flex-wrap items-center gap-2">
              <b>{fieldLabel(item.Field ?? '-', t)}</b>
              {item.Locale ? <span className="mono">{item.Locale}</span> : null}
              <Badge
                small
                tone={item.Action === ChangeAction.Delete ? 'danger' : item.Action === ChangeAction.Create ? 'success' : 'neutral'}
              >
                {changeActionLabel(item.Action, t)}
              </Badge>
            </span>
          }
          lines={[
            ...(item.Action !== ChangeAction.Create ? [{ kind: 'del' as const, text: item.Before || t.plan.empty }] : []),
            ...(item.Action !== ChangeAction.Delete ? [{ kind: 'add' as const, text: item.After || t.plan.empty }] : []),
          ]}
        />
      ))}
      {files.length > 0 ? (
        <section aria-label={t.plan.files}>
          <h3 className="h3 mb-2">{t.plan.files}</h3>
          <ul className="m-0 grid list-none gap-1 p-0 text-[13px]">
            {files.map((file) => (
              <li key={file.AssetId} className="flex flex-wrap items-center gap-2">
                <span className="mono">{file.FileName}</span>
                {file.Width && file.Height ? <span className="text-fg3">{`${file.Width}x${file.Height}`}</span> : null}
                {file.Result ? (
                  <Badge small tone={file.Result === 'Rejected' ? 'danger' : file.Result === 'Skipped' ? 'neutral' : 'success'}>
                    {file.Result}
                  </Badge>
                ) : null}
                {file.Reason ? <span className="text-fg2">{file.Reason}</span> : null}
                {file.SuggestedClassKey ? (
                  <span className="text-fg3">{interpolate(t.plan.suggested, { key: file.SuggestedClassKey })}</span>
                ) : null}
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      {warnings.length > 0 ? (
        <Callout tone="warning" title={t.plan.warnings}>
          <ul className="m-0 grid list-disc gap-1 pl-4">
            {warnings.map((warning, index) => (
              <li key={`${warning.Key}-${index}`}>
                {warning.Locale ? <span className="mono">{warning.Locale} · </span> : null}
                {warning.Message ?? warning.Key}
              </li>
            ))}
          </ul>
        </Callout>
      ) : null}
      {calls.length > 0 ? (
        <details>
          <summary className="cursor-pointer text-[13px] font-semibold">{t.plan.calls}</summary>
          <ul className="mono m-0 mt-2 grid list-none gap-1 p-0 text-xs text-fg2">
            {calls.map((call, index) => (
              <li key={index}>
                {call.Method} {call.Path} {call.Target ? `· ${call.Target}` : ''}
              </li>
            ))}
          </ul>
        </details>
      ) : null}
      {plan.DecidedByUserName ? (
        <p className="text-[13px] text-fg2">
          {interpolate(t.plan.decided, { name: plan.DecidedByUserName, date: format.dateTime(plan.DecisionDate) })}
          {plan.DecisionNote ? ` · ${plan.DecisionNote}` : ''}
        </p>
      ) : null}
      {plan.JobError ? (
        <Callout tone="danger" title={t.plan.jobError}>
          {plan.JobError}
        </Callout>
      ) : null}
    </div>
  )
}

/** The decision buttons and their dialogs; `onDone` runs after any decision. */
export function PlanActions({ plan, onDone }: { plan: ChangePlanDto; onDone: (plan: ChangePlanDto | null) => void }) {
  const t = useP2()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const [reasonError, setReasonError] = useState<string | null>(null)

  const refresh = () => void queryClient.invalidateQueries({ queryKey: contentKeys.all })

  const approve = useMutation({
    mutationFn: (typed: string | null) => plansApi.approve(organization.Id, plan.PlanId, typed),
    meta: { silentError: true },
    onSuccess: (updated) => {
      showToast(t.plan.approved, 'success', updated.Title ?? undefined)
      refresh()
      onDone(updated)
    },
    onError: (failure) => {
      const status = apiStatus(failure)
      setError(status === 409 ? t.plan.conflict : status === 410 ? t.plan.expired : apiErrorMessage(failure, t.plan.conflict))
    },
  })
  const reject = useMutation({
    mutationFn: (text: string) => plansApi.reject(organization.Id, plan.PlanId, text),
    onSuccess: (updated) => {
      showToast(t.plan.rejected, 'success')
      setRejecting(false)
      refresh()
      onDone(updated)
    },
  })
  const discard = useMutation({
    mutationFn: () => plansApi.discard(organization.Id, plan.PlanId),
    onSuccess: (updated) => {
      showToast(t.plan.discarded, 'success')
      refresh()
      onDone(updated)
    },
  })

  if (plan.Status !== ChangePlanStatus.Pending || !can(OrgPermission.EditContent)) {
    return null
  }

  const allowed = can(plan.RequiredPermission)
  const askApprove = async () => {
    setError(null)
    if (plan.ConfirmRequired) {
      const text = plan.ConfirmText ?? ''
      const ok = await confirm({
        title: interpolate(t.plan.confirmTitle, { title: plan.Title ?? '' }),
        message: t.plan.confirmBody,
        confirmLabel: t.plan.approve,
        typeToConfirm: text,
      })
      if (ok) {
        approve.mutate(text)
      }
      return
    }
    approve.mutate(null)
  }

  const submitReject = () => {
    if (reason.trim().length < MIN_REJECT_REASON) {
      setReasonError(t.plan.reasonShort)
      return
    }
    reject.mutate(reason.trim())
  }

  return (
    <div className="grid w-full gap-2">
      {error ? (
        <Callout tone="danger" className="w-full">
          {error}
        </Callout>
      ) : null}
      <div className="flex flex-wrap items-center justify-end gap-2">
        <Button variant="ghost" loading={discard.isPending} onClick={() => discard.mutate()}>
          {t.plan.discard}
        </Button>
        <Button variant="danger-outline" onClick={() => setRejecting(true)}>
          {t.plan.reject}
        </Button>
        <Button variant="primary" disabled={!allowed} loading={approve.isPending} onClick={() => void askApprove()}>
          {t.plan.approve}
        </Button>
      </div>
      {!allowed ? (
        <p className="text-right text-xs text-fg3">
          {interpolate(t.plan.needs, { permission: permissionLabel(plan.RequiredPermission, t) })}
        </p>
      ) : null}
      <Modal
        open={rejecting}
        onClose={() => setRejecting(false)}
        title={t.plan.rejectTitle}
        size="sm"
        footer={
          <>
            <Button variant="ghost" onClick={() => setRejecting(false)}>
              {t.plan.close}
            </Button>
            <Button variant="danger" loading={reject.isPending} onClick={submitReject}>
              {t.plan.reject}
            </Button>
          </>
        }
      >
        <p className="mb-3 text-[13.5px] text-fg2">{t.plan.rejectBody}</p>
        <Field label={t.plan.reason} error={reasonError}>
          <Textarea
            value={reason}
            rows={3}
            onChange={(event) => {
              setReason(event.target.value)
              setReasonError(null)
            }}
          />
        </Field>
      </Modal>
    </div>
  )
}

/** A freshly made plan in a dialog (the dry run answer of a Plan* call). */
export function PlanDialog({
  plan,
  onClose,
  onDecided,
}: {
  plan: ChangePlanDto | null
  onClose: () => void
  // After approve, reject or discard, with the plan as the API answered it.
  onDecided?: (plan: ChangePlanDto | null) => void
}) {
  const t = useP2()
  return (
    <Modal
      open={!!plan}
      onClose={onClose}
      size="xl"
      title={plan?.Title ?? t.plan.title}
      description={plan ? interpolate(t.plan.changes, { count: plan.ItemCount }) : undefined}
      footer={
        plan ? (
          <PlanActions
            plan={plan}
            onDone={(decided) => {
              onDecided?.(decided)
              onClose()
            }}
          />
        ) : undefined
      }
    >
      {plan ? <PlanBody plan={plan} /> : null}
    </Modal>
  )
}
