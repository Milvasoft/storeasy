import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Boxes, Link2, MoreHorizontal, RefreshCw } from 'lucide-react'
import { LinkRecordDialog } from '@/components/apps/AppDialogs'
import { AppAvatar } from '@/components/apps/AppAvatar'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Badge, StoreTag, storeName } from '@/components/ui/Badge'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Menu } from '@/components/ui/Menu'
import { ScreenState } from '@/components/ui/ScreenState'
import { Tabs } from '@/components/ui/Tabs'
import { Button, Chip, EmptyState, IconButton, PageHeader, PageSkeleton } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { isActive } from '@/lib/activity/hatState'
import { appsApi, connectionsApi } from '@/lib/api/endpoints'
import {
  BuildProcessingState,
  OrgPermission,
  StoreKind,
  type AppDetailDto,
  type AppStoreRecordDto,
  type BuildDto,
  type StoreConnectionDto,
} from '@/lib/api/types'
import { connectionIdsOf, recordsOf } from '@/lib/apps/apps'
import { useSyncApps } from '@/lib/apps/useSyncApps'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { buildStateView } from '@/lib/jobs/labels'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { APP_NAMES_REQUEST } from '@/lib/activity/ActivityContext'
import { JobsPage } from '@/pages/activity/JobsPages'
import { JobRow } from '@/pages/ActivityPage'
import { workspaceTabs } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'

export type AppTab = 'overview' | 'builds' | 'jobs'

const STATES = Object.values(BuildProcessingState)

function BuildStateBadge({ state }: { state: BuildProcessingState }) {
  const t = useT()
  const view = buildStateView(state, t)
  return (
    <Badge tone={view.tone} dot={view.dot} small>
      {view.label}
    </Badge>
  )
}

/** The builds of all the app's records, newest first (design 8.1): a state filter, then the table. */
export function BuildsTable({ builds, compact }: { builds: BuildDto[]; compact?: boolean }) {
  const t = useT()
  const format = useFormat()
  const columns: Column<BuildDto>[] = [
    { key: 'store', header: t.builds.store, cell: (build) => <StoreTag store={build.Store} small codeOnly /> },
    {
      key: 'version',
      header: t.builds.version,
      role: 'main',
      cell: (build) => (
        <span className="mono">
          <b>{build.Version ?? '-'}</b> <span className="text-fg3">({build.BuildNumber ?? '-'})</span>
        </span>
      ),
    },
    { key: 'status', header: t.builds.status, cell: (build) => <BuildStateBadge state={build.ProcessingState} /> },
    {
      key: 'uploaded',
      header: t.builds.uploaded,
      cell: (build) => <span className="whitespace-nowrap tabular-nums">{format.dateTime(build.UploadedDate)}</span>,
    },
  ]
  if (!compact) {
    columns.push(
      {
        key: 'minOs',
        header: t.builds.minOs,
        role: 'hidePhone',
        cell: (build) => <span className="mono">{build.MinOsVersion ?? '-'}</span>,
      },
      {
        key: 'more',
        header: `${t.builds.expires} / ${t.builds.tracks}`,
        role: 'hidePhone',
        cell: (build) =>
          build.Store === StoreKind.Google ? (
            <span className="mono">{build.Tracks ?? '-'}</span>
          ) : (
            <span className="tabular-nums">{format.date(build.ExpiryDate)}</span>
          ),
      },
    )
  }
  return <DataTable caption={t.builds.title} columns={columns} rows={builds} rowKey={(build) => build.Id} />
}

function BuildsTab({ app }: { app: AppDetailDto }) {
  const t = useT()
  const [state, setState] = useState<BuildProcessingState | null>(null)
  const builds = app.Builds ?? []
  if (builds.length === 0) {
    return <EmptyState icon={<Boxes aria-hidden />} title={t.builds.emptyTitle} description={t.builds.emptyBody} />
  }
  const shown = state ? builds.filter((build) => build.ProcessingState === state) : builds
  return (
    <>
      <div className="toolbar" role="group" aria-label={t.builds.filter}>
        <Chip selected={state === null} onClick={() => setState(null)}>
          {interpolate(t.builds.all, { count: builds.length })}
        </Chip>
        {STATES.filter((value) => builds.some((build) => build.ProcessingState === value)).map((value) => (
          <Chip key={value} selected={state === value} onClick={() => setState(value)}>
            {buildStateView(value, t).label} · {builds.filter((build) => build.ProcessingState === value).length}
          </Chip>
        ))}
        <span className="ml-auto text-xs text-fg3">{t.builds.limitNote}</span>
      </div>
      {shown.length === 0 ? <p className="text-[13px] text-fg3">{t.builds.noMatch}</p> : <BuildsTable builds={shown} />}
    </>
  )
}

function RecordCard({
  app,
  record,
  connection,
}: {
  app: AppDetailDto
  record: AppStoreRecordDto
  connection: StoreConnectionDto | undefined
}) {
  const t = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const { can } = useOrganization()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const only = (app.Stores ?? []).length < 2

  const unlink = useMutation({
    mutationFn: () => appsApi.unlink(organization.Id, record.StoreAppId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.apps.all })
      showToast(t.app.unlinkDone, 'success')
    },
  })

  const askUnlink = async () => {
    const ok = await confirm({
      title: interpolate(t.app.unlinkTitle, { store: storeName(record.Store, t.enums.store) }),
      message: t.app.unlinkBody,
      confirmLabel: t.app.unlink,
    })
    if (ok) {
      unlink.mutate()
    }
  }

  return (
    <section className="card">
      <div className="card-h">
        <StoreTag store={record.Store} small />
        <span className="ml-auto">
          {record.SyncedDate ? (
            <Badge tone="success" small>
              {t.apps.synced}
            </Badge>
          ) : (
            <Badge tone="info" small dot>
              {t.apps.awaitingSync}
            </Badge>
          )}
        </span>
        {can(OrgPermission.EditContent) ? (
          <Menu
            label={t.app.recordMenu}
            trigger={
              <IconButton label={t.app.recordMenu} ghost size="sm">
                <MoreHorizontal aria-hidden />
              </IconButton>
            }
            entries={[{ key: 'unlink', label: only ? t.app.onlyRecord : t.app.unlink, disabled: only, onSelect: () => void askUnlink() }]}
          />
        ) : null}
      </div>
      <dl className="kv card-b">
        <dt>{t.app.externalId}</dt>
        <dd className="mono text-[12.5px]">{record.ExternalId ?? '-'}</dd>
        <dt>{t.app.bundleId}</dt>
        <dd className="mono text-[12.5px]">{record.BundleId ?? '-'}</dd>
        {record.Name && record.Name !== app.Name ? (
          <>
            <dt>{t.apps.app}</dt>
            <dd>{record.Name}</dd>
          </>
        ) : null}
        <dt>{t.app.connection}</dt>
        <dd>{connection?.Name ?? '-'}</dd>
        <dt>{t.app.lastSync}</dt>
        <dd className="tabular-nums">{record.SyncedDate ? format.dateTime(record.SyncedDate) : t.app.notSynced}</dd>
      </dl>
    </section>
  )
}

function OnTheLine({ app, started }: { app: AppDetailDto; started: Set<number> }) {
  const t = useT()
  const { state } = useActivity()
  // The app's own jobs, and the syncs started from this page (a sync belongs to a connection, not to one app).
  const jobs = Object.values(state.jobs).filter((job) => isActive(job) && (job.appId === app.Id || started.has(job.id)))
  return (
    <section className="card">
      <div className="card-h">
        <h2 className="h3">{t.app.onLine}</h2>
        {jobs.length > 0 ? (
          <Badge tone="success" small live>
            {t.app.live}
          </Badge>
        ) : null}
      </div>
      {jobs.length === 0 ? (
        <p className="card-b m-0 text-[13px] text-fg3">{t.app.nothingRunning}</p>
      ) : (
        <ul className="m-0 list-none p-0">
          {jobs.map((job) => (
            <JobRow key={job.id} job={job} />
          ))}
        </ul>
      )}
    </section>
  )
}

function Overview({ app, connections, started }: { app: AppDetailDto; connections: StoreConnectionDto[]; started: Set<number> }) {
  const t = useT()
  const builds = (app.Builds ?? []).slice(0, 5)
  return (
    <div className="grid gap-5">
      <div className="g3">
        {recordsOf(app).map((record) => (
          <RecordCard
            key={record.StoreAppId}
            app={app}
            record={record}
            connection={connections.find((connection) => connection.Id === record.StoreConnectionId)}
          />
        ))}
      </div>
      <div className="split">
        <section className="card">
          <div className="card-h">
            <h2 className="h3">{t.app.latestBuilds}</h2>
            <Link className="lnk ml-auto text-[13px]" to={`/apps/${app.Id}/builds`}>
              {t.app.allBuilds}
            </Link>
          </div>
          {builds.length === 0 ? (
            <p className="card-b m-0 text-[13px] text-fg3">{t.app.noBuilds}</p>
          ) : (
            <BuildsTable builds={builds} compact />
          )}
        </section>
        <aside className="grid gap-4">
          <OnTheLine app={app} started={started} />
        </aside>
      </div>
    </div>
  )
}

/**
 * One app (design 6.7): its store records with pairing and splitting, the newest builds, what runs for it on the line
 * right now, and its jobs. "Sync" queues the sync of the connections its records are read with.
 */
export function AppDetailPage({ tab }: { tab: AppTab }) {
  const t = useT()
  const p2 = useP2()
  const format = useFormat()
  const { appId: rawId = '' } = useParams()
  const appId = Number(rawId)
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const [linking, setLinking] = useState(false)
  const [started, setStarted] = useState<Set<number>>(() => new Set())
  const sync = useSyncApps((jobIds) => setStarted((current) => new Set([...current, ...jobIds])))
  const editor = can(OrgPermission.EditContent)

  const app = useQuery({
    queryKey: queryKeys.apps.detail(organization.Id, appId),
    queryFn: () => appsApi.detail(organization.Id, appId),
    enabled: Number.isInteger(appId) && appId > 0,
  })
  const connections = useQuery({
    queryKey: queryKeys.connections.list(organization.Id),
    queryFn: () => connectionsApi.list(organization.Id),
  })
  // The other apps, for pairing (the same first page the live view reads for app names).
  const others = useQuery({
    queryKey: queryKeys.apps.list(organization.Id, APP_NAMES_REQUEST),
    queryFn: () => appsApi.list(organization.Id, APP_NAMES_REQUEST),
    enabled: linking,
  })
  const connectionIds = useMemo(() => (app.data ? connectionIdsOf(app.data) : []), [app.data])

  const back = (
    <Link className="btn inv" to="/apps">
      {t.app.back}
    </Link>
  )

  if (app.isLoading || (!app.data && !app.isError)) {
    return <PageSkeleton />
  }
  if (app.isError || !app.data) {
    return <ScreenState error={app.error} onRetry={() => void app.refetch()} back={back} />
  }
  const data = app.data

  return (
    <>
      <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to="/apps">
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
        {t.app.back}
      </Link>
      <div className="mb-1 flex items-start gap-4">
        <AppAvatar app={data} size="l" />
        <div className="min-w-0 flex-1">
          <PageHeader
            title={data.Name}
            subtitle={
              <span className="flex flex-wrap items-center gap-2">
                {recordsOf(data).map((record) => (
                  <StoreTag key={record.StoreAppId} store={record.Store} small codeOnly />
                ))}
                <span>{interpolate(t.app.addedOn, { date: format.date(data.CreationDate) })}</span>
              </span>
            }
            actions={
              editor ? (
                <>
                  <Button onClick={() => setLinking(true)}>
                    <Link2 aria-hidden />
                    {t.app.link}
                  </Button>
                  <Button
                    loading={sync.isPending}
                    onClick={() => (connectionIds.length > 0 ? sync.mutate(connectionIds) : showToast(t.app.syncNoConnection, 'info'))}
                  >
                    <RefreshCw aria-hidden />
                    {t.app.sync}
                  </Button>
                </>
              ) : undefined
            }
          />
        </div>
      </div>
      <Tabs
        label={t.app.sections}
        items={[
          { value: 'overview', label: t.app.overview, to: `/apps/${data.Id}` },
          { value: 'builds', label: t.app.builds, to: `/apps/${data.Id}/builds`, count: (data.Builds ?? []).length },
          { value: 'jobs', label: t.app.jobs, to: `/apps/${data.Id}/jobs` },
          ...workspaceTabs(
            data.Id,
            p2,
            (data.Stores ?? []).map((record) => record.Store),
          ),
        ]}
      />
      {tab === 'overview' ? <Overview app={data} connections={connections.data ?? []} started={started} /> : null}
      {tab === 'builds' ? <BuildsTab app={data} /> : null}
      {tab === 'jobs' ? <JobsPage appId={data.Id} /> : null}
      {editor ? <LinkRecordDialog app={data} others={others.data?.Data ?? []} open={linking} onClose={() => setLinking(false)} /> : null}
    </>
  )
}
