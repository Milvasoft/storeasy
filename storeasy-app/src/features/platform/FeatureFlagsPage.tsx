import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Flag } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, EmptyState, Field, Input, PageHeader, Switch } from '@/components/ui/primitives'
import type { ListRequest } from '@/lib/api/list'
import { useGlobalPermissions } from '@/lib/auth/globalPermissions'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { platformApi, platformKeys } from './api'
import { MAX_ROWS, PlatformGate, PlatformPermission } from './PlatformCommon'
import { usePlatformText, type PlatformDictionary } from './i18n'
import { FLAG_REASON_MAX, updated, type FeatureFlagListDto, type UpdateFeatureFlagCommand } from './types'

// Design 17.22-17.23: the runtime kill switches. Each row keeps its own draft and is saved alone; turning a feature off
// needs a reason (the audit note, at most 200) and a confirmation, turning it on does not. Each row says what the feature
// is (the server's description) and who changed it last.

const REQUEST: ListRequest = { PageNumber: 1, RowCount: MAX_ROWS }

export function flagLabel(key: string, t: PlatformDictionary): string {
  return (t.flags.labels as Record<string, string>)[key] ?? key
}

/** What a row's save sends: only the changed fields; null when nothing changed. */
export function flagCommand(flag: FeatureFlagListDto, enabled: boolean, reason: string): UpdateFeatureFlagCommand | null {
  const command: UpdateFeatureFlagCommand = { Id: flag.Id }
  if (enabled !== flag.IsEnabled) {
    command.IsEnabled = updated(enabled)
  }
  const nextReason = reason.trim() || null
  if (nextReason !== (flag.Reason ?? null)) {
    command.Reason = updated(nextReason)
  }
  return Object.keys(command).length > 1 ? command : null
}

function FlagRow({ flag, canUpdate }: { flag: FeatureFlagListDto; canUpdate: boolean }) {
  const t = usePlatformText()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const [enabled, setEnabled] = useState(flag.IsEnabled)
  const [reason, setReason] = useState(flag.Reason ?? '')
  const [touched, setTouched] = useState(false)
  const label = flagLabel(flag.Key, t)
  const command = flagCommand(flag, enabled, reason)
  const reasonError = !enabled && !reason.trim() ? t.flags.reasonRequired : null
  const save = useMutation({
    mutationFn: (next: UpdateFeatureFlagCommand) => platformApi.updateFeatureFlag(next),
    onSuccess: () => {
      showToast(interpolate(t.flags.saved, { name: label }), 'success')
      setTouched(false)
      void queryClient.invalidateQueries({ queryKey: platformKeys.featureFlags() })
    },
  })

  const reset = () => {
    setEnabled(flag.IsEnabled)
    setReason(flag.Reason ?? '')
    setTouched(false)
  }

  const submit = async () => {
    setTouched(true)
    if (!command || reasonError) {
      return
    }
    if (command.IsEnabled && !command.IsEnabled.Value) {
      const ok = await confirm({
        title: interpolate(t.flags.confirm.title, { name: label }),
        message: (
          <div className="grid gap-2">
            <p>{interpolate(t.flags.confirm.body, { name: label })}</p>
            <p className="text-fg">{interpolate(t.flags.confirm.reason, { reason: reason.trim() })}</p>
          </div>
        ),
        confirmLabel: t.flags.confirm.confirm,
        danger: true,
      })
      if (!ok) {
        return
      }
    }
    save.mutate(command)
  }

  return (
    <li className="card grid gap-3 p-4 sm:grid-cols-[minmax(0,1fr)_auto]">
      <div className="grid content-start gap-1">
        <span className="flex flex-wrap items-center gap-2">
          <b className="text-[14px]">{label}</b>
          <span className="mono text-xs text-fg3">{flag.Key}</span>
          <Badge tone={flag.IsEnabled ? 'success' : 'danger'} small dot>
            {flag.IsEnabled ? t.flags.on : t.flags.off}
          </Badge>
          {command ? (
            <Badge tone="warning" small>
              {t.flags.unsaved}
            </Badge>
          ) : null}
        </span>
        {flag.Description ? <span className="text-[13px] text-fg2">{flag.Description}</span> : null}
        {flag.LastModifierUserName ? (
          <span className="text-xs text-fg3">
            {interpolate(t.flags.lastChange, { user: flag.LastModifierUserName, time: format.dateTime(flag.LastModificationDate) })}
          </span>
        ) : null}
        {canUpdate ? (
          <Field
            label={interpolate(t.flags.reasonLabel, { name: label })}
            optional={enabled}
            error={touched ? reasonError : null}
            counter={{ value: reason.length, max: FLAG_REASON_MAX }}
          >
            <Input
              value={reason}
              maxLength={FLAG_REASON_MAX}
              placeholder={t.flags.reasonPlaceholder}
              onChange={(event) => setReason(event.target.value)}
            />
          </Field>
        ) : (
          <span className="text-[13px] text-fg2">{flag.Reason || '-'}</span>
        )}
      </div>
      <div className="flex items-start gap-2 sm:flex-col sm:items-end">
        <span className="inline-flex items-center gap-2 text-[13px]">
          <Switch
            checked={enabled}
            disabled={!canUpdate || save.isPending}
            label={interpolate(t.flags.turnOn, { name: label })}
            onChange={(value) => {
              setEnabled(value)
              if (value) {
                setReason('')
              }
            }}
          />
          {enabled ? t.flags.on : t.flags.off}
        </span>
        {command ? (
          <span className="flex gap-2">
            <Button size="sm" variant="ghost" onClick={reset}>
              {t.flags.cancel}
            </Button>
            <Button size="sm" variant="primary" loading={save.isPending} disabled={touched && !!reasonError} onClick={() => void submit()}>
              {t.flags.save}
            </Button>
          </span>
        ) : null}
      </div>
    </li>
  )
}

function FeatureFlags() {
  const t = usePlatformText()
  const global = useGlobalPermissions()
  const flags = useQuery({ queryKey: platformKeys.featureFlags(), queryFn: () => platformApi.featureFlags(REQUEST) })
  const canUpdate = global.has(PlatformPermission.FeatureFlagUpdate)
  const rows = flags.data?.Data ?? []
  return (
    <>
      <PageHeader title={t.flags.title} subtitle={t.flags.subtitle} />
      {!canUpdate ? (
        <div className="mb-3">
          <Callout tone="info">{t.flags.readOnly}</Callout>
        </div>
      ) : null}
      {flags.isLoading ? (
        <CardSkeleton />
      ) : flags.isError ? (
        <ScreenState error={flags.error} onRetry={() => void flags.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={<Flag aria-hidden />} title={t.flags.empty} />
      ) : (
        <ul className="grid gap-2" aria-label={t.flags.title}>
          {rows.map((flag) => (
            // Keyed by the saved state too, so a refetch after saving resets the row's draft.
            <FlagRow key={`${flag.Id}-${String(flag.IsEnabled)}-${flag.Reason ?? ''}`} flag={flag} canUpdate={canUpdate} />
          ))}
        </ul>
      )}
      <p className="mt-3 text-xs text-fg3">{t.flags.keysNote}</p>
      <p className="mt-1 text-xs text-fg3">{t.flags.registrationNote}</p>
    </>
  )
}

/** /admin/feature-flags. */
export function FeatureFlagsPage() {
  return (
    <PlatformGate permission={PlatformPermission.FeatureFlagList}>
      <FeatureFlags />
    </PlatformGate>
  )
}
