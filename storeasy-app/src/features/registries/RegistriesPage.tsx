import { useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ExternalLink, Package, Plus, PowerOff, RefreshCw, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Callout } from '@/components/ui/Feedback'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { ScreenState, StateView } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, EmptyState, PageHeader, TableSkeleton } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission } from '@/lib/api/types'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { AddPackageDialog } from './AddPackageDialog'
import { TokenReminders } from './TokenReminders'
import { TrustedPublishingSection } from './TrustedPublishingSection'
import { DownloadsTab, ListingTab } from './PackageInsights'
import { Tabs } from '@/components/ui/Tabs'
import { Select } from '@/components/ui/Select'
import { registriesApi } from './api'
import { useRegistries } from './i18n/useRegistries'
import type { RegistriesDictionary } from './i18n/en'
import { registriesKeys } from './keys'
import { alertKindLabel, alertTone, registryLabel } from './rules'
import type { PackageAlertDto, TrackedPackageSummaryDto, TrackedPackageVersionDto } from './types'

// The package registries module (docs/package-registries.md): the summary of every tracked package and one package's
// versions and alerts. storeasy only reads the registries' public APIs; publishing stays in CI.

function ExternalAnchor({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a href={href} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 underline underline-offset-2">
      {children}
      <ExternalLink aria-hidden className="size-3.5" />
    </a>
  )
}

function ProvenanceBadge({ row, t }: { row: TrackedPackageSummaryDto; t: RegistriesDictionary }) {
  if (!row.ProvenanceSupported) {
    return (
      <span title={t.provenance.unsupportedHint}>
        <Badge small>{t.provenance.unsupported}</Badge>
      </span>
    )
  }
  return row.HasProvenance ? (
    <Badge small tone="success">
      {t.provenance.yes}
    </Badge>
  ) : (
    <Badge small tone="warning">
      {t.provenance.no}
    </Badge>
  )
}

function AlertBadge({ alert, t }: { alert: PackageAlertDto; t: RegistriesDictionary }) {
  return (
    <Badge small tone={alertTone(alert.Kind, alert.Severity)}>
      {alertKindLabel(alert.Kind, t)}
      {alert.Severity ? ` · ${alert.Severity}` : ''}
    </Badge>
  )
}

/** The module's switched-off state (the Registries feature flag). */
function RegistriesOff() {
  const t = useRegistries()
  return <StateView icon={<PowerOff aria-hidden />} title={t.off.title} description={t.off.body} />
}

function useSync() {
  const t = useRegistries()
  const base = useT()
  const organization = useCurrentOrganization()
  const { watchJobs } = useActivity()
  return useMutation({
    mutationFn: (trackedPackageId: number | null) => registriesApi.sync(organization.Id, trackedPackageId),
    meta: { silentError: true },
    onSuccess: (jobId) => {
      watchJobs([jobId])
      showToast(t.syncQueued, 'success')
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })
}

type PackageSort = 'alerts' | 'name' | 'downloads' | 'published'

const downloadsOf = (row: TrackedPackageSummaryDto) => row.TotalDownloads ?? row.RecentDownloads ?? -1

/** The summary in the chosen order (the API answers open alerts first, then by name). */
export function sortPackages(rows: TrackedPackageSummaryDto[], sort: PackageSort): TrackedPackageSummaryDto[] {
  const byName = (a: TrackedPackageSummaryDto, b: TrackedPackageSummaryDto) => a.Name.localeCompare(b.Name)
  switch (sort) {
    case 'name':
      return [...rows].sort(byName)
    case 'downloads':
      return [...rows].sort((a, b) => downloadsOf(b) - downloadsOf(a) || byName(a, b))
    case 'published':
      return [...rows].sort((a, b) => (b.LatestPublishedDate ?? '').localeCompare(a.LatestPublishedDate ?? '') || byName(a, b))
    default:
      return rows
  }
}

type DetailTab = 'overview' | 'versions' | 'downloads' | 'listing' | 'trusted'

function PackageList() {
  const t = useRegistries()
  const format = useFormat()
  const navigate = useNavigate()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const canEdit = can(OrgPermission.EditContent)
  const [adding, setAdding] = useState(false)
  const [sort, setSort] = useState<PackageSort>('alerts')
  const sync = useSync()

  const packages = useQuery({
    queryKey: registriesKeys.packages(organization.Id),
    queryFn: () => registriesApi.packages(organization.Id),
  })

  const columns: Column<TrackedPackageSummaryDto>[] = [
    {
      key: 'package',
      header: t.table.package,
      role: 'main',
      cell: (row) => (
        <Link to={`/registries/${row.Id}`} className="mono font-medium" onClick={(event) => event.stopPropagation()}>
          {row.Name}
        </Link>
      ),
    },
    { key: 'registry', header: t.table.registry, cell: (row) => registryLabel(row.Registry) },
    { key: 'latest', header: t.table.latest, cell: (row) => <span className="mono">{row.LatestVersion ?? '-'}</span> },
    { key: 'published', header: t.table.published, cell: (row) => (row.LatestPublishedDate ? format.date(row.LatestPublishedDate) : '-') },
    {
      key: 'ciRun',
      header: t.table.ciRun,
      role: 'hidePhone',
      cell: (row) => (row.LatestCiRunUrl ? <ExternalAnchor href={row.LatestCiRunUrl}>{t.ciRunLink}</ExternalAnchor> : '-'),
    },
    { key: 'provenance', header: t.table.provenance, cell: (row) => <ProvenanceBadge row={row} t={t} /> },
    {
      key: 'downloads',
      header: t.table.downloads,
      role: 'hidePhone',
      cell: (row) =>
        row.TotalDownloads != null
          ? format.number(row.TotalDownloads)
          : row.RecentDownloads != null
            ? `${format.number(row.RecentDownloads)} / ${row.RecentDownloadsDays ?? 30}d`
            : '-',
    },
    {
      key: 'alerts',
      header: t.table.alerts,
      role: 'action',
      cell: (row) =>
        row.OpenAlertCount > 0 ? (
          <Badge small tone="danger">
            {row.OpenAlertCount}
          </Badge>
        ) : (
          <span className="text-fg3">{t.table.none}</span>
        ),
    },
  ]

  const actions = canEdit ? (
    <>
      <Button variant="outline" onClick={() => sync.mutate(null)} loading={sync.isPending} disabled={!packages.data?.length}>
        <RefreshCw aria-hidden />
        {t.syncAll}
      </Button>
      <Button variant="primary" onClick={() => setAdding(true)}>
        <Plus aria-hidden />
        {t.add.open}
      </Button>
    </>
  ) : null

  let body: ReactNode
  if (packages.isPending) {
    body = <TableSkeleton columns={[t.table.package, t.table.registry, t.table.latest, t.table.published]} label={t.table.caption} />
  } else if (packages.isError) {
    body = <ScreenState error={packages.error} onRetry={() => void packages.refetch()} />
  } else if (packages.data.length === 0) {
    body = (
      <EmptyState
        icon={<Package aria-hidden />}
        title={t.empty.title}
        description={t.empty.body}
        action={
          canEdit ? (
            <Button variant="primary" onClick={() => setAdding(true)}>
              <Plus aria-hidden />
              {t.add.open}
            </Button>
          ) : null
        }
      />
    )
  } else {
    body = (
      <DataTable
        caption={t.table.caption}
        columns={columns}
        rows={sortPackages(packages.data, sort)}
        rowKey={(row) => row.Id}
        onRowClick={(row) => navigate(`/registries/${row.Id}`)}
        busy={packages.isFetching}
      />
    )
  }

  return (
    <>
      <PageHeader title={t.title} subtitle={t.subtitle} actions={actions} />
      <div className="flex flex-wrap items-center gap-2">
        <Link to="/registries/groups" className="btn">
          {t.groups.title}
        </Link>
        <div className="ml-auto w-56">
          <Select
            aria-label={t.sort.label}
            value={sort}
            options={[
              { value: 'alerts' as PackageSort, label: t.sort.alerts },
              { value: 'name' as PackageSort, label: t.sort.name },
              { value: 'downloads' as PackageSort, label: t.sort.downloads },
              { value: 'published' as PackageSort, label: t.sort.published },
            ]}
            onChange={setSort}
          />
        </div>
      </div>
      {!canEdit ? <Callout tone="info">{t.readOnly}</Callout> : null}
      <div className="mt-3">{body}</div>
      <TokenReminders canEdit={canEdit} />
      <AddPackageDialog open={adding} onClose={() => setAdding(false)} />
    </>
  )
}

function versionState(version: TrackedPackageVersionDto, t: RegistriesDictionary): ReactNode {
  if (version.IsYanked) {
    return (
      <Badge small tone="warning">
        {t.detail.yanked}
      </Badge>
    )
  }
  if (version.IsUnlisted) {
    return (
      <Badge small tone="warning">
        {t.detail.unlisted}
      </Badge>
    )
  }
  if (version.IsDeprecated) {
    return (
      <Badge small tone="warning">
        {t.detail.deprecatedState}
      </Badge>
    )
  }
  return <Badge small>{t.detail.active}</Badge>
}

function PackageDetail({ packageId }: { packageId: number }) {
  const t = useRegistries()
  const base = useT()
  const format = useFormat()
  const navigate = useNavigate()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const canEdit = can(OrgPermission.EditContent)
  const sync = useSync()
  const [tab, setTab] = useState<DetailTab>('overview')

  const data = useQuery({
    queryKey: registriesKeys.package(organization.Id, packageId),
    queryFn: () => registriesApi.package(organization.Id, packageId),
  })

  const remove = useMutation({
    mutationFn: () => registriesApi.remove(organization.Id, packageId),
    meta: { silentError: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
      showToast(t.remove.done, 'success')
      navigate('/registries')
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  const back = (
    <Link to="/registries" className="btn ghost">
      <ArrowLeft aria-hidden />
      {t.detail.back}
    </Link>
  )

  if (data.isPending) {
    return <CardSkeleton count={2} />
  }
  if (data.isError) {
    return <ScreenState error={data.error} onRetry={() => void data.refetch()} back={back} />
  }

  const item = data.data
  const versions = item.Versions ?? []
  const alerts = item.Alerts ?? []

  const askRemove = async () => {
    const ok = await confirm({
      title: interpolate(t.remove.title, { name: item.Name }),
      message: t.remove.body,
      confirmLabel: t.remove.action,
      danger: true,
    })
    if (ok) {
      remove.mutate()
    }
  }

  const versionColumns: Column<TrackedPackageVersionDto>[] = [
    {
      key: 'version',
      header: t.detail.version,
      role: 'main',
      cell: (row) => (
        <span className="mono">
          {row.Version}
          {row.Version === item.LatestVersion ? ` (${t.detail.latest})` : ''}
        </span>
      ),
    },
    { key: 'published', header: t.table.published, cell: (row) => (row.PublishedDate ? format.date(row.PublishedDate) : '-') },
    { key: 'state', header: t.detail.state, cell: (row) => versionState(row, t) },
    {
      key: 'provenance',
      header: t.table.provenance,
      cell: (row) => {
        if (!item.ProvenanceSupported) {
          return t.provenance.unsupported
        }
        if (row.HasProvenance == null) {
          return <span className="text-fg3">{t.provenance.unknown}</span>
        }
        return row.HasProvenance ? t.provenance.yes : t.provenance.no
      },
    },
    {
      key: 'ciRun',
      header: t.table.ciRun,
      role: 'hidePhone',
      cell: (row) => (row.CiRunUrl ? <ExternalAnchor href={row.CiRunUrl}>{t.ciRunLink}</ExternalAnchor> : '-'),
    },
    {
      key: 'downloads',
      header: t.table.downloads,
      role: 'hidePhone',
      cell: (row) => (row.Downloads != null ? format.number(row.Downloads) : '-'),
    },
  ]

  const alertColumns: Column<PackageAlertDto>[] = [
    { key: 'kind', header: t.detail.alerts, role: 'main', cell: (row) => <AlertBadge alert={row} t={t} /> },
    { key: 'version', header: t.detail.version, cell: (row) => <span className="mono">{row.Version ?? '-'}</span> },
    {
      key: 'summary',
      header: t.detail.details,
      cell: (row) =>
        row.Url ? (
          <ExternalAnchor href={row.Url}>{[row.AdvisoryId, row.Summary].filter(Boolean).join(': ') || row.Url}</ExternalAnchor>
        ) : (
          (row.Summary ?? '-')
        ),
    },
    { key: 'opened', header: t.detail.opened, cell: (row) => format.date(row.OpenedDate) },
    {
      key: 'resolved',
      header: t.detail.resolved,
      role: 'action',
      cell: (row) =>
        row.ResolvedDate ? (
          format.date(row.ResolvedDate)
        ) : (
          <Badge small tone="danger">
            {t.detail.open}
          </Badge>
        ),
    },
  ]

  return (
    <>
      <div className="mb-2">{back}</div>
      <PageHeader
        title={item.Name}
        subtitle={`${registryLabel(item.Registry)} · ${item.LatestVersion ?? '-'}`}
        badge={<ProvenanceBadge row={item} t={t} />}
        actions={
          canEdit ? (
            <>
              <Button variant="outline" onClick={() => sync.mutate(item.Id)} loading={sync.isPending}>
                <RefreshCw aria-hidden />
                {t.sync}
              </Button>
              <Button variant="danger-outline" onClick={() => void askRemove()} loading={remove.isPending}>
                <Trash2 aria-hidden />
                {t.remove.action}
              </Button>
            </>
          ) : null
        }
      />
      <div className="grid gap-4">
        {item.SyncError ? (
          <Callout tone="danger" title={t.detail.syncError}>
            {item.SyncError}
          </Callout>
        ) : null}
        {item.DeprecationMessage != null ? (
          <Callout tone="warning" title={t.detail.deprecated}>
            {item.DeprecationMessage}
          </Callout>
        ) : null}
        <Tabs<DetailTab>
          label={item.Name}
          value={tab}
          onChange={setTab}
          items={[
            { value: 'overview', label: t.tabs.overview },
            { value: 'versions', label: t.tabs.versions },
            { value: 'downloads', label: t.tabs.downloads },
            { value: 'listing', label: t.tabs.listing },
            { value: 'trusted', label: t.tabs.trusted },
          ]}
        />
        {tab === 'overview' ? (
          <article className="card" aria-label={t.detail.overview}>
            <div className="card-h">
              <h2 className="h3">{t.detail.overview}</h2>
            </div>
            <dl className="card-b grid gap-2 sm:grid-cols-2">
              <div>
                <dt className="text-fg3">{t.table.published}</dt>
                <dd>{item.LatestPublishedDate ? format.dateTime(item.LatestPublishedDate) : '-'}</dd>
              </div>
              <div>
                <dt className="text-fg3">{t.table.ciRun}</dt>
                <dd>{item.LatestCiRunUrl ? <ExternalAnchor href={item.LatestCiRunUrl}>{t.ciRunLink}</ExternalAnchor> : '-'}</dd>
              </div>
              <div>
                <dt className="text-fg3">{t.workflow}</dt>
                <dd className="mono">{item.LatestWorkflow ?? '-'}</dd>
              </div>
              <div>
                <dt className="text-fg3">{t.detail.repository}</dt>
                <dd>
                  {item.SourceRepository ? <ExternalAnchor href={item.SourceRepository}>{item.SourceRepository}</ExternalAnchor> : '-'}
                </dd>
              </div>
              <div>
                <dt className="text-fg3">{t.detail.app}</dt>
                <dd>{item.AppId ? <Link to={`/apps/${item.AppId}`}>{item.AppName}</Link> : '-'}</dd>
              </div>
              <div>
                <dt className="text-fg3">{t.detail.lastSynced}</dt>
                <dd>{item.SyncedDate ? format.relative(item.SyncedDate) : t.detail.neverSynced}</dd>
              </div>
              {item.RegistryUrl ? (
                <div>
                  <dt className="sr-only">{t.detail.registryPage}</dt>
                  <dd>
                    <ExternalAnchor href={item.RegistryUrl}>{t.detail.registryPage}</ExternalAnchor>
                  </dd>
                </div>
              ) : null}
            </dl>
          </article>
        ) : null}
        {tab === 'trusted' ? <TrustedPublishingSection packageId={item.Id} canEdit={canEdit} /> : null}
        {tab === 'downloads' ? <DownloadsTab packageId={item.Id} /> : null}
        {tab === 'listing' ? <ListingTab packageId={item.Id} /> : null}
        {tab === 'overview' ? (
          <section aria-label={t.detail.alerts} className="grid gap-2">
            <h2 className="text-[15px] font-semibold">{t.detail.alerts}</h2>
            {alerts.length === 0 ? (
              <p className="text-fg3">{t.detail.noAlerts}</p>
            ) : (
              <DataTable caption={t.detail.alerts} columns={alertColumns} rows={alerts} rowKey={(row) => row.Id} />
            )}
          </section>
        ) : null}
        {tab === 'versions' ? (
          <section aria-label={t.detail.versions} className="grid gap-2">
            <h2 className="text-[15px] font-semibold">{t.detail.versions}</h2>
            <DataTable caption={t.detail.versions} columns={versionColumns} rows={versions} rowKey={(row) => row.Version} />
          </section>
        ) : null}
      </div>
    </>
  )
}

/** The summary of every tracked package (route /registries). */
export function RegistriesPage() {
  return useFeature('Registries') ? <PackageList /> : <RegistriesOff />
}

/** One tracked package with its versions and alerts (route /registries/:packageId). */
export function RegistryPackagePage() {
  const enabled = useFeature('Registries')
  const { packageId } = useParams()
  if (!enabled) {
    return <RegistriesOff />
  }
  return <PackageDetail packageId={Number(packageId)} />
}
