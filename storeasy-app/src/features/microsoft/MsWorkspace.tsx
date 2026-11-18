import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Eye } from 'lucide-react'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState, StateView } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Field, Input } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { plansApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { permissionLabel } from '@/features/common/labels'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { msApi } from './api'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import { blockText, statusLabel, targetLabel } from './labels'
import { canPlan, lockState, type MsLockState } from './rules'
import {
  MsChangeKind,
  MsProductKind,
  MsSubmissionTarget,
  type MsChangeInputDto,
  type MsChangePlanDto,
  type MsProductDto,
  type MsSubmissionDto,
} from './types'

// The frame of the Microsoft Store screens: the app workspace with the Microsoft record selected, the product (flags,
// active submission), the sub screens of its product type and the Partner Center rules every submission screen shows
// (design 7.4, 8.20: one active submission; a draft changed in Partner Center is locked, only discarding is left).

export type MsSub = 'product' | 'listings' | 'images' | 'pricing' | 'packages' | 'installers' | 'flights' | 'addons' | 'submissions'

const SUB_PATHS: Record<MsSub, string> = {
  product: 'ms/product',
  listings: 'ms/listings',
  images: 'ms/images',
  pricing: 'ms/pricing',
  packages: 'ms/packages',
  installers: 'ms/installers',
  flights: 'ms/flights',
  addons: 'ms/addons',
  submissions: 'ms/submissions',
}

// MSIX apps have packages, flights, images and add-ons through the API; MSI/EXE products have installer URLs.
const SUBS_OF: Record<MsProductKind, MsSub[]> = {
  [MsProductKind.Msix]: ['product', 'listings', 'images', 'pricing', 'packages', 'flights', 'addons', 'submissions'],
  [MsProductKind.MsiExe]: ['product', 'listings', 'pricing', 'installers', 'submissions'],
}

export interface MsContext extends WorkspaceContext {
  product: MsProductDto
  lock: MsLockState
  // Editor and not locked by a Partner Center rule.
  editable: boolean
}

// ---- Change plans ------------------------------------------------------------------------------------------------------
/** The blocked answer of a dry run: why, and the assisted steps link when Partner Center owns the change. */
export function BlockedCallout({ plan, appId }: { plan: MsChangePlanDto; appId: number }) {
  const t = useMs()
  return (
    <Callout tone="warning" title={t.plan.blockedTitle}>
      <span className="block">{blockText(plan.BlockKey, t)}</span>
      {plan.AssistedReason ? (
        <span className="mt-1 block">
          {t.plan.assisted}{' '}
          <Link className="lnk" to={`/apps/${appId}/assisted?store=microsoft`}>
            {t.plan.openAssisted}
          </Link>
        </span>
      ) : null}
    </Callout>
  )
}

/**
 * Plans a Microsoft Store change (ms/change, always a dry run) and shows the saved plan in the shared plan dialog, where
 * it is approved through plans/approve. A blocked answer is kept and shown by `element`.
 */
export function useMsPlan(
  context: Pick<WorkspaceContext, 'organizationId' | 'record' | 'app'>,
  onDecided?: (plan: ChangePlanDto | null) => void,
) {
  const queryClient = useQueryClient()
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const [blocked, setBlocked] = useState<MsChangePlanDto | null>(null)
  const { organizationId, record, app } = context

  const make = useMutation({
    mutationFn: async (change: MsChangeInputDto) => {
      const made = await msApi.change(organizationId, record.StoreAppId, change)
      const saved = !made.Blocked && made.PlanId ? await plansApi.detail(organizationId, made.PlanId) : null
      return { made, saved }
    },
    onSuccess: ({ made, saved }) => {
      setBlocked(made.Blocked ? made : null)
      setPlan(saved)
      if (made.Blocked) {
        // A Partner Center task became an assisted step of the product.
        void queryClient.invalidateQueries({ queryKey: msKeys.product(organizationId, record.StoreAppId) })
      }
    },
  })

  const element = (
    <>
      {blocked ? <BlockedCallout plan={blocked} appId={app.Id} /> : null}
      <PlanDialog
        plan={plan}
        onClose={() => setPlan(null)}
        onDecided={(decided) => {
          void queryClient.invalidateQueries({ queryKey: msKeys.all })
          onDecided?.(decided)
        }}
      />
    </>
  )

  return {
    make: (change: MsChangeInputDto) => {
      setBlocked(null)
      make.mutate(change)
    },
    pending: make.isPending,
    blocked,
    element,
  }
}

// ---- Submitting --------------------------------------------------------------------------------------------------------
export interface SubmitTarget {
  Target: MsSubmissionTarget
  FlightId?: string | null
  AddonStoreId?: string | null
  label: string
}

/** The submission target of a pending submission row. */
export function submitTargetOf(submission: MsSubmissionDto, label: string): SubmitTarget {
  return { Target: submission.Target, FlightId: submission.FlightId ?? null, AddonStoreId: submission.AddonStoreId ?? null, label }
}

/**
 * Sending a draft to certification (SubmitForReview): a summary, the product's Store id typed as confirmation, the button
 * disabled until it matches (case does not matter, as on the API). Answers a job, followed on the live stream.
 */
export function MsSubmitDialog({
  target,
  onClose,
  organizationId,
  product,
}: {
  target: SubmitTarget | null
  onClose: () => void
  organizationId: number
  product: MsProductDto
}) {
  const t = useMs()
  const queryClient = useQueryClient()
  const { watchJobs } = useActivity()
  const [typed, setTyped] = useState('')
  const expected = product.ProductId ?? ''
  const matches = !!expected && typed.trim().toLowerCase() === expected.toLowerCase()

  const close = () => {
    setTyped('')
    onClose()
  }

  const submit = useMutation({
    mutationFn: () =>
      msApi.submit(organizationId, product.StoreAppId, {
        Target: target?.Target ?? MsSubmissionTarget.App,
        FlightId: target?.FlightId ?? null,
        AddonStoreId: target?.AddonStoreId ?? null,
        Confirm: typed.trim(),
      }),
    onSuccess: (jobId) => {
      watchJobs([jobId])
      showToast(t.submit.sent, 'success', target?.label)
      void queryClient.invalidateQueries({ queryKey: msKeys.all })
      close()
    },
  })

  return (
    <Modal
      open={!!target}
      onClose={close}
      role="alertdialog"
      title={interpolate(t.submit.title, { name: product.Name ?? expected })}
      description={t.submit.body}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {t.submit.close}
          </Button>
          <Button variant="primary" disabled={!matches} loading={submit.isPending} onClick={() => submit.mutate()}>
            {t.submit.send}
          </Button>
        </>
      }
    >
      <dl className="kv mb-3">
        <dt>{t.submit.target}</dt>
        <dd>{target ? `${targetLabel(target.Target, t)} · ${target.label}` : '-'}</dd>
      </dl>
      <Field
        label={interpolate(t.submit.typeLabel, { id: expected })}
        error={typed.trim() && !matches ? t.submit.mismatch : null}
        hint={matches ? t.submit.match : undefined}
      >
        <Input mono value={typed} autoComplete="off" spellCheck={false} onChange={(event) => setTyped(event.target.value)} />
      </Field>
    </Modal>
  )
}

/** The send button: shown to editors, enabled with SubmitForReview, the missing permission named under it otherwise. */
export function SubmitButton({ context, onClick, label }: { context: MsContext; onClick: () => void; label?: string }) {
  const t = useMs()
  const p2 = useP2()
  if (!context.editor) {
    return null
  }
  const allowed = context.can(OrgPermission.SubmitForReview)
  return (
    <span className="inline-grid justify-items-end gap-1">
      <Button size="sm" variant="primary" disabled={!allowed || !context.editable} onClick={onClick}>
        {label ?? t.submissions.submit}
      </Button>
      {!allowed ? (
        <span className="text-xs text-fg3">
          {interpolate(t.submit.needs, { permission: permissionLabel(OrgPermission.SubmitForReview, p2) })}
        </span>
      ) : null}
    </span>
  )
}

// ---- Rules -------------------------------------------------------------------------------------------------------------
function RulesPanel({ context }: { context: MsContext }) {
  const t = useMs()
  const { product, lock } = context
  const plan = useMsPlan(context)
  const active = product.ActiveSubmission
  const activeName = active?.FriendlyName ?? active?.PackageVersion ?? active?.SubmissionId ?? '-'
  const notices = product.Notices ?? []
  return (
    <div className="mb-4 grid gap-3">
      {lock.editedInPartnerCenter && active ? (
        <Callout tone="danger" title={t.rules.editedTitle}>
          <span className="block">{t.rules.edited}</span>
          {context.editor ? (
            <span className="mt-2 block">
              <Button
                size="sm"
                variant="danger-outline"
                loading={plan.pending}
                onClick={() =>
                  plan.make({
                    Kind: MsChangeKind.DiscardSubmission,
                    FlightId: active.FlightId ?? null,
                    AddonStoreId: active.AddonStoreId ?? null,
                  })
                }
              >
                {t.rules.discard}
              </Button>
            </span>
          ) : null}
        </Callout>
      ) : null}
      {lock.firstSubmissionMissing ? <Callout tone="warning">{t.rules.firstSubmission}</Callout> : null}
      {lock.inProgress ? (
        <Callout tone="warning">
          {interpolate(t.rules.inProgress, { name: activeName, status: statusLabel(lock.inProgress.Status, t) })}
        </Callout>
      ) : null}
      {notices.length > 0 ? (
        <Callout tone="info" title={t.rules.title}>
          <ul className="m-0 grid list-disc gap-1 pl-4">
            {notices.map((notice) => (
              <li key={notice}>{notice}</li>
            ))}
          </ul>
        </Callout>
      ) : null}
      {plan.element}
    </div>
  )
}

function MsFrame({ context, sub, children }: { context: WorkspaceContext; sub: MsSub; children: (context: MsContext) => ReactNode }) {
  const t = useMs()
  const { organizationId, record, app, storeQuery } = context
  const product = useQuery({
    queryKey: msKeys.product(organizationId, record.StoreAppId),
    queryFn: () => msApi.product(organizationId, record.StoreAppId),
  })

  if (product.isLoading) {
    return <CardSkeleton />
  }
  if (product.isError || !product.data) {
    return <ScreenState error={product.error} onRetry={() => void product.refetch()} />
  }

  const data = product.data
  const lock = lockState(data)
  const subs = SUBS_OF[data.Kind] ?? SUBS_OF[MsProductKind.Msix]
  const ms: MsContext = { ...context, product: data, lock, editable: context.editor && canPlan(lock) }
  return (
    <>
      <nav className="toolbar mb-3" aria-label={t.section}>
        {subs.map((item) => (
          <Link
            key={item}
            to={`/apps/${app.Id}/${SUB_PATHS[item]}${storeQuery}`}
            className={cn('chip', item === sub && 'on')}
            aria-current={item === sub ? 'page' : undefined}
          >
            {t.sub[item]}
          </Link>
        ))}
      </nav>
      <RulesPanel context={ms} />
      {subs.includes(sub) ? (
        children(ms)
      ) : (
        <StateView
          icon={<Eye aria-hidden />}
          title={interpolate(t.notForKind, { kind: data.Kind === MsProductKind.MsiExe ? t.kind.msiExe : t.kind.msix })}
        />
      )}
    </>
  )
}

/** A Microsoft Store screen: the app workspace on its Microsoft record, then the product frame. */
export function MsWorkspace({ sub, children }: { sub: MsSub; children: (context: MsContext) => ReactNode }) {
  return (
    <AppWorkspace section="microsoft" stores={[StoreKind.Microsoft]}>
      {(context) => (
        <MsFrame context={context} sub={sub}>
          {children}
        </MsFrame>
      )}
    </AppWorkspace>
  )
}
