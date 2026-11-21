import { Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { FlaskConical, Plus, RefreshCw } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, EmptyState, PageHeader } from '@/components/ui/primitives'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { apiErrorMessage } from '@/lib/api/http'
import { StoreKind } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { experimentsApi, useExperimentOverview } from './api'
import { useExperimentsText, type ExperimentsDictionary } from './i18n'
import { kindLabel, statusView, supportLabel } from './labels'
import { supportedKinds } from './rules'
import type { AsoExperimentSummaryDto, AsoStoreCapabilityDto } from './types'

// "Deneyler": the store listing A/B tests and custom pages of an app in the selected store, what the store supports and
// how (API, assisted step or not at all), and the way to a new one.

export function capabilityText(store: StoreKind, t: ExperimentsDictionary): string {
  switch (store) {
    case StoreKind.Apple:
      return t.capability.apple
    case StoreKind.Google:
      return t.capability.google
    default:
      return t.capability.microsoft
  }
}

export function ExperimentStatusChip({ status }: { status: AsoExperimentSummaryDto['Status'] }) {
  const t = useExperimentsText()
  const view = statusView(status, t)
  return (
    <Badge tone={view.tone} dot>
      {view.label}
    </Badge>
  )
}

function CapabilityCard({ capability, store }: { capability: AsoStoreCapabilityDto | undefined; store: StoreKind }) {
  const t = useExperimentsText()
  const kinds = capability?.Kinds ?? []
  return (
    <Callout tone={kinds.length === 0 ? 'warning' : 'info'}>
      <p className="m-0">{capabilityText(store, t)}</p>
      {kinds.length > 0 ? (
        <ul className="m-0 mt-2 flex list-none flex-wrap gap-2 p-0" aria-label={t.column.kind}>
          {kinds.map((item) => (
            <li key={item.Kind}>
              <Badge small>{`${kindLabel(item.Kind, t)}: ${supportLabel(item.Support, t)}`}</Badge>
            </li>
          ))}
        </ul>
      ) : null}
    </Callout>
  )
}

function Experiments({ context }: { context: WorkspaceContext }) {
  const t = useExperimentsText()
  const base = useT()
  const format = useFormat()
  const { app, record, organizationId, editor, storeQuery } = context
  const overview = useExperimentOverview(organizationId, app.Id)
  const sync = useMutation({
    mutationFn: () => experimentsApi.sync(organizationId, record.StoreAppId),
    onSuccess: () => showToast(t.syncStarted, 'success'),
    onError: (error) => showToast(apiErrorMessage(error, base.common.error), 'error'),
  })

  if (overview.isLoading) {
    return <CardSkeleton />
  }
  if (overview.isError || !overview.data) {
    return <ScreenState error={overview.error} onRetry={() => void overview.refetch()} />
  }

  const capability = overview.data.Capabilities.find((item) => item.StoreAppId === record.StoreAppId)
  const kinds = supportedKinds(capability)
  const rows = overview.data.Experiments.filter((item) => item.StoreAppId === record.StoreAppId)
  const canCreate = editor && kinds.length > 0

  const columns: Column<AsoExperimentSummaryDto>[] = [
    {
      key: 'name',
      header: t.column.name,
      role: 'main',
      cell: (row) => (
        <Link className="lnk font-semibold" to={`/apps/${app.Id}/experiments/${row.Id}${storeQuery}`}>
          {row.Name}
        </Link>
      ),
    },
    { key: 'kind', header: t.column.kind, cell: (row) => kindLabel(row.Kind, t) },
    { key: 'status', header: t.column.status, cell: (row) => <ExperimentStatusChip status={row.Status} /> },
    {
      key: 'traffic',
      header: t.column.traffic,
      cell: (row) => (row.TrafficProportion != null ? interpolate(t.trafficValue, { value: row.TrafficProportion }) : '-'),
    },
    {
      key: 'dates',
      header: t.column.dates,
      cell: (row) =>
        row.StartDate
          ? interpolate(t.dateRange, { start: format.date(row.StartDate), end: row.EndDate ? format.date(row.EndDate) : t.ongoing })
          : t.notStarted,
    },
    {
      key: 'winner',
      header: t.column.winner,
      cell: (row) => (row.WinnerVariantKey ? interpolate(t.variantLabel, { key: row.WinnerVariantKey }) : t.noWinner),
    },
  ]

  return (
    <div className="grid gap-4">
      <PageHeader
        title={t.title}
        subtitle={t.subtitle}
        actions={
          <>
            {record.Store === StoreKind.Apple && editor ? (
              <Button variant="outline" loading={sync.isPending} onClick={() => sync.mutate()}>
                <RefreshCw className="h-4 w-4" aria-hidden />
                {t.sync}
              </Button>
            ) : null}
            {canCreate ? (
              <Link className="btn pri" to={`/apps/${app.Id}/experiments/new${storeQuery}`}>
                <Plus className="h-4 w-4" aria-hidden />
                {t.newExperiment}
              </Link>
            ) : null}
          </>
        }
      />
      <CapabilityCard capability={capability} store={record.Store} />
      {rows.length === 0 ? (
        <EmptyState icon={<FlaskConical aria-hidden />} title={kinds.length > 0 ? t.empty : t.wizard.notSupported} />
      ) : (
        <DataTable caption={t.title} columns={columns} rows={rows} rowKey={(row) => row.Id} />
      )}
    </div>
  )
}

export function ExperimentsPage() {
  return <AppWorkspace section="experiments">{(context) => <Experiments context={context} />}</AppWorkspace>
}
