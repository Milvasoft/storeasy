import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Field, Input, PageHeader, Switch, TableSkeleton } from '@/components/ui/primitives'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { assistedView } from '@/features/common/labels'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { purchaseSettingsApi } from './api'
import { useMoneyT } from './i18n'
import { moneyKeys } from './keys'
import { RENEWAL_RATES, renewalRateLabel } from './labels'
import { APPLE_AND_GOOGLE, usePlanRequest } from './shared'
import { MonetizationChangeKind, type SandboxTesterDto, type ServerNotificationsDto } from './types'

// Purchase settings of an app: where the store sends purchase notifications (App Store Server Notifications, production
// and sandbox; Play's Real-time developer notifications are an assisted step) and the App Store sandbox testers.

const VERSIONS = ['V2', 'V1'] as const

export function PurchaseSettingsPage() {
  return (
    <AppWorkspace section="money" sub="purchaseSettings" stores={APPLE_AND_GOOGLE}>
      {(context) => <PurchaseSettings context={context} />}
    </AppWorkspace>
  )
}

function PurchaseSettings({ context }: { context: WorkspaceContext }) {
  const t = useMoneyT()
  const { organizationId, record } = context
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const notifications = useQuery({
    queryKey: moneyKeys.serverNotifications(organizationId, record.StoreAppId),
    queryFn: () => purchaseSettingsApi.serverNotifications(organizationId, record.StoreAppId),
  })
  return (
    <div className="grid max-w-4xl gap-4">
      <PageHeader title={t.settings.title} subtitle={t.settings.subtitle} />
      {notifications.isLoading ? (
        <CardSkeleton count={1} />
      ) : notifications.isError || !notifications.data ? (
        <ScreenState error={notifications.error} onRetry={() => void notifications.refetch()} />
      ) : notifications.data.Assisted || record.Store === StoreKind.Google ? (
        <AssistedNotifications context={context} data={notifications.data} />
      ) : (
        <ServerNotifications
          key={`${notifications.data.Url}-${notifications.data.SandboxUrl}`}
          context={context}
          data={notifications.data}
          onPlanned={setPlan}
        />
      )}
      {record.Store === StoreKind.Apple ? <SandboxTesters context={context} onPlanned={setPlan} /> : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} />
    </div>
  )
}

function AssistedNotifications({ context, data }: { context: WorkspaceContext; data: ServerNotificationsDto }) {
  const t = useMoneyT()
  const p2 = useP2()
  const { app, storeQuery } = context
  const status = data.AssistedStepStatus ? assistedView(data.AssistedStepStatus, p2) : null
  return (
    <section className="card" aria-label={t.settings.notifications}>
      <div className="card-h">
        <h2 className="h3">{t.settings.playNotifications}</h2>
        {status ? (
          <Badge tone={status.tone} small dot>
            {status.label}
          </Badge>
        ) : null}
      </div>
      <div className="card-b grid gap-3 text-[13px]">
        <p className="m-0 text-fg2">{t.settings.playNotificationsBody}</p>
        {data.AssistedStepId ? (
          <div>
            <Link className="btn" to={`/apps/${app.Id}/assisted/${data.AssistedStepId}${storeQuery}`}>
              {t.settings.openStep}
            </Link>
          </div>
        ) : (
          <p className="m-0 text-fg3">{t.settings.noStep}</p>
        )}
      </div>
    </section>
  )
}

function isHttps(value: string): boolean {
  try {
    return new URL(value).protocol === 'https:'
  } catch {
    return false
  }
}

function ServerNotifications({
  context,
  data,
  onPlanned,
}: {
  context: WorkspaceContext
  data: ServerNotificationsDto
  onPlanned: (plan: ChangePlanDto) => void
}) {
  const t = useMoneyT()
  const { organizationId, record, can } = context
  const allowed = can(OrgPermission.ManageWebhooks)
  const [url, setUrl] = useState(data.Url ?? '')
  const [sandboxUrl, setSandboxUrl] = useState(data.SandboxUrl ?? '')
  const [version, setVersion] = useState<string>(data.Version ?? 'V2')
  const urlChanged = url.trim() !== (data.Url ?? '')
  const sandboxChanged = sandboxUrl.trim() !== (data.SandboxUrl ?? '')
  const versionChanged = version !== (data.Version ?? 'V2')
  const urlError = url.trim() && !isHttps(url.trim()) ? t.settings.httpsOnly : null
  const sandboxError = sandboxUrl.trim() && !isHttps(sandboxUrl.trim()) ? t.settings.httpsOnly : null
  const { mutation, errorBox } = usePlanRequest(
    () =>
      purchaseSettingsApi.planServerNotifications(organizationId, record.StoreAppId, {
        Url: urlChanged || versionChanged ? url.trim() || null : null,
        SandboxUrl: sandboxChanged || versionChanged ? sandboxUrl.trim() || null : null,
        Version: version,
      }),
    onPlanned,
  )
  const changed = urlChanged || sandboxChanged || versionChanged
  return (
    <section className="card" aria-label={t.settings.notifications}>
      <div className="card-h">
        <h2 className="h3">{t.settings.notifications}</h2>
      </div>
      <form
        className="card-b grid gap-4"
        noValidate
        onSubmit={(event) => {
          event.preventDefault()
          if (changed && !urlError && !sandboxError) {
            mutation.mutate()
          }
        }}
      >
        <p className="m-0 text-[13px] text-fg2">{t.settings.notificationsBody}</p>
        {errorBox}
        <Field label={t.settings.productionUrl} error={urlError} hint={t.settings.urlHint}>
          <Input mono type="url" value={url} disabled={!allowed} onChange={(event) => setUrl(event.target.value)} />
        </Field>
        <Field
          label={t.settings.sandboxUrl}
          error={sandboxError}
          hint={data.SandboxVersion ? interpolate(t.settings.version, { version: data.SandboxVersion }) : undefined}
        >
          <Input mono type="url" value={sandboxUrl} disabled={!allowed} onChange={(event) => setSandboxUrl(event.target.value)} />
        </Field>
        <Field label={t.settings.versionLabel}>
          <Select value={version} disabled={!allowed} options={VERSIONS.map((value) => ({ value, label: value }))} onChange={setVersion} />
        </Field>
        {allowed ? (
          <div>
            <Button type="submit" variant="primary" disabled={!changed || !!urlError || !!sandboxError} loading={mutation.isPending}>
              {t.plan.review}
            </Button>
          </div>
        ) : (
          <Callout tone="info">{t.settings.needsWebhooks}</Callout>
        )}
      </form>
    </section>
  )
}

function SandboxTesters({ context, onPlanned }: { context: WorkspaceContext; onPlanned: (plan: ChangePlanDto) => void }) {
  const t = useMoneyT()
  const confirm = useConfirm()
  const { organizationId, record, editor } = context
  const [editing, setEditing] = useState<SandboxTesterDto | null>(null)
  const testers = useQuery({
    queryKey: moneyKeys.sandboxTesters(organizationId, record.StoreAppId),
    queryFn: () => purchaseSettingsApi.sandboxTesters(organizationId, record.StoreAppId),
  })
  const clear = usePlanRequest(
    (tester: SandboxTesterDto) =>
      purchaseSettingsApi.planSandboxTesters(organizationId, record.StoreAppId, [
        { Kind: MonetizationChangeKind.SandboxTester, TesterId: tester.Id, ClearPurchaseHistory: true },
      ]),
    onPlanned,
  )
  const askClear = async (tester: SandboxTesterDto) => {
    const ok = await confirm({
      title: interpolate(t.settings.clearTitle, { name: tester.AccountName ?? tester.Id ?? '' }),
      message: t.settings.clearBody,
      confirmLabel: t.settings.clear,
      danger: true,
    })
    if (ok) {
      clear.mutation.mutate(tester)
    }
  }
  const columns: Column<SandboxTesterDto>[] = [
    {
      key: 'tester',
      header: t.settings.tester,
      role: 'main',
      cell: (row) => (
        <span className="grid gap-0.5">
          <b>{[row.FirstName, row.LastName].filter(Boolean).join(' ') || '-'}</b>
          <span className="mono text-xs text-fg3">{row.AccountName}</span>
        </span>
      ),
    },
    { key: 'territory', header: t.settings.territory, cell: (row) => <span className="mono">{row.Territory ?? '-'}</span> },
    {
      key: 'interrupt',
      header: t.settings.interrupt,
      cell: (row) => (row.InterruptPurchases ? t.common.yes : t.common.no),
    },
    { key: 'rate', header: t.settings.renewalRate, role: 'hidePhone', cell: (row) => renewalRateLabel(row.SubscriptionRenewalRate, t) },
    {
      key: 'actions',
      header: t.price.actions,
      role: 'action',
      cell: (row) =>
        editor ? (
          <span className="flex gap-1">
            <Button size="sm" variant="ghost" onClick={() => setEditing(row)}>
              {interpolate(t.settings.edit, { name: row.AccountName ?? '' })}
            </Button>
            <Button size="sm" variant="ghost" onClick={() => void askClear(row)}>
              {interpolate(t.settings.clearFor, { name: row.AccountName ?? '' })}
            </Button>
          </span>
        ) : null,
    },
  ]
  const rows = testers.data ?? []
  return (
    <section className="card" aria-label={t.settings.sandbox}>
      <div className="card-h">
        <h2 className="h3">{t.settings.sandbox}</h2>
        <Badge small>{rows.length}</Badge>
      </div>
      <div className="card-b grid gap-3">
        <p className="m-0 text-[13px] text-fg2">{t.settings.sandboxBody}</p>
        {clear.errorBox}
        {testers.isLoading ? (
          <TableSkeleton columns={[t.settings.tester, t.settings.territory]} rows={3} label={t.settings.sandbox} />
        ) : testers.isError ? (
          <ScreenState error={testers.error} onRetry={() => void testers.refetch()} />
        ) : rows.length === 0 ? (
          <p className="m-0 text-[13px] text-fg3">{t.settings.noTesters}</p>
        ) : (
          <DataTable columns={columns} rows={rows} rowKey={(row) => row.Id ?? ''} caption={t.settings.sandbox} />
        )}
      </div>
      {editing ? <TesterModal context={context} tester={editing} onClose={() => setEditing(null)} onPlanned={onPlanned} /> : null}
    </section>
  )
}

function TesterModal({
  context,
  tester,
  onClose,
  onPlanned,
}: {
  context: WorkspaceContext
  tester: SandboxTesterDto
  onClose: () => void
  onPlanned: (plan: ChangePlanDto) => void
}) {
  const t = useMoneyT()
  const { organizationId, record } = context
  const [territory, setTerritory] = useState(tester.Territory ?? '')
  const [interrupt, setInterrupt] = useState(!!tester.InterruptPurchases)
  const [rate, setRate] = useState<string | null>(tester.SubscriptionRenewalRate ?? null)
  const { mutation, errorBox } = usePlanRequest(
    () =>
      purchaseSettingsApi.planSandboxTesters(organizationId, record.StoreAppId, [
        {
          Kind: MonetizationChangeKind.SandboxTester,
          TesterId: tester.Id,
          Territory: territory.trim() && territory.trim() !== (tester.Territory ?? '') ? territory.trim().toUpperCase() : null,
          InterruptPurchases: interrupt !== !!tester.InterruptPurchases ? interrupt : null,
          RenewalRate: rate && rate !== tester.SubscriptionRenewalRate ? rate : null,
        },
      ]),
    (made) => {
      onPlanned(made)
      onClose()
    },
  )
  return (
    <Modal
      open
      onClose={onClose}
      title={interpolate(t.settings.editTitle, { name: tester.AccountName ?? '' })}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button variant="primary" loading={mutation.isPending} onClick={() => mutation.mutate()}>
            {t.plan.add}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        <Field label={t.settings.territory} hint={t.settings.territoryHint}>
          <Input mono maxLength={3} value={territory} onChange={(event) => setTerritory(event.target.value.toUpperCase())} />
        </Field>
        <div className="flex items-center gap-3 text-[13px]">
          <Switch checked={interrupt} label={t.settings.interrupt} onChange={setInterrupt} />
          {t.settings.interrupt}
        </div>
        <Field label={t.settings.renewalRate}>
          <Select value={rate} options={RENEWAL_RATES.map((value) => ({ value, label: renewalRateLabel(value, t) }))} onChange={setRate} />
        </Field>
      </div>
    </Modal>
  )
}
