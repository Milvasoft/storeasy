import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ClipboardCheck, Pause, Play } from 'lucide-react'
import { HatLine, jobTitle, progressText, type StoreRow } from '@/components/activity/Hat'
import { AttentionBar, UpcomingBoard } from '@/components/activity/HatBoard'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { Banner } from '@/components/ui/Feedback'
import { Tabs } from '@/components/ui/Tabs'
import { Button, Chip, EmptyState, PageHeader } from '@/components/ui/primitives'
import { complianceApi } from '@/features/common/api'
import { contentKeys } from '@/features/common/keys'
import { rejectionKeys, rejectionsApi } from '@/features/rejections/api'
import { RejectionState } from '@/features/rejections/types'
import { useActivity } from '@/lib/activity/ActivityContext'
import { attentionOf, gateItemsOf, hourStrip, mutedAppIdsOf, upcomingOf, withoutMutedApps, type BoardInputs } from '@/lib/activity/board'
import { ActivityEventType, InboundEventType } from '@/lib/activity/events'
import {
  isActive,
  jobsOf,
  lanesOf,
  LANE_STORES,
  nextExpiry,
  tracksOf,
  type FeedItem,
  type HatState,
  type JobView,
  type Track,
} from '@/lib/activity/hatState'
import { connectionsApi, jobsApi } from '@/lib/api/endpoints'
import { SortType, type ListRequest } from '@/lib/api/list'
import { StoreKind } from '@/lib/api/types'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { usePrefersReducedMotion } from '@/lib/hooks/usePrefersReducedMotion'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import type { Dictionary } from '@/lib/i18n/en'
import { connectionStatusLabel, jobKindLabel, jobStatusView } from '@/lib/jobs/labels'
import { channelLabel } from '@/lib/org/auditLabels'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { cn } from '@/lib/utils/cn'

const STORE_PARAM: Record<string, StoreKind> = { apple: StoreKind.Apple, google: StoreKind.Google, microsoft: StoreKind.Microsoft }

export function ActivityTabs() {
  const t = useT()
  return (
    <Tabs
      label={t.activity.title}
      items={[
        { value: 'hat', label: t.activity.tabHat, to: '/activity' },
        { value: 'jobs', label: t.activity.tabJobs, to: '/activity/jobs' },
        { value: 'plans', label: t.activity.tabPlans, to: '/activity/plans' },
      ]}
    />
  )
}

// Ticks once a second while `active` (a countdown or a running duration is on screen).
export function useNow(active: boolean): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!active) {
      return
    }
    setNow(Date.now())
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [active])
  return now
}

/** Design 11.14 and 18.12: the live connection's state above the scene, with a manual reconnect. */
function ConnectionBanner() {
  const t = useT()
  const format = useFormat()
  const { connection, reconnect, state } = useActivity()
  const now = useNow(connection.state === 'reconnecting')
  if (connection.state === 'reconnecting') {
    const seconds = connection.nextRetryAt ? Math.max(0, Math.ceil((connection.nextRetryAt - now) / 1000)) : null
    return (
      <Banner
        tone="info"
        role="status"
        action={
          <Button size="sm" onClick={reconnect}>
            {t.activity.reconnectNow}
          </Button>
        }
      >
        {seconds !== null
          ? interpolate(t.activity.reconnecting, { attempt: connection.attempt ?? 1, seconds })
          : t.activity.reconnectingNow}
        {state.lastEventAt ? (
          <span className="text-fg3"> · {interpolate(t.activity.lastEvent, { time: format.time(state.lastEventAt) })}</span>
        ) : null}
      </Banner>
    )
  }
  if (connection.state === 'disconnected' && connection.refused) {
    return (
      <Banner tone="danger" role="alert">
        {t.activity.refused}
      </Banner>
    )
  }
  if (connection.state === 'disconnected') {
    return (
      <Banner
        tone="danger"
        role="alert"
        action={
          <Button size="sm" variant="inverse" onClick={reconnect}>
            {t.activity.reconnect}
          </Button>
        }
      >
        {t.activity.disconnected}
      </Banner>
    )
  }
  return null
}

function LiveBadge() {
  const t = useT()
  const { connection, frozen } = useActivity()
  if (frozen) {
    return <Badge>{t.activity.frozen}</Badge>
  }
  if (connection.state === 'connected') {
    return (
      <Badge tone="success" live>
        {connection.demo ? t.activity.demo : t.activity.live}
      </Badge>
    )
  }
  if (connection.state === 'connecting' || connection.state === 'idle') {
    return <Badge>{t.activity.connecting}</Badge>
  }
  return <Badge tone={connection.state === 'reconnecting' ? 'info' : 'danger'}>{t.activity.offline}</Badge>
}

const FEED_DIR: Record<FeedItem['kind'], { className: string; key: keyof Dictionary['activity']['direction'] }> = {
  out: { className: 'out', key: 'out' },
  in: { className: 'in', key: 'in' },
  event: { className: '', key: 'event' },
  error: { className: 'err', key: 'error' },
}

// What a feed row is about: the app and the job kind for job events, the result for a connection check.
function feedSubject(item: FeedItem, t: Dictionary, appName: string | null): string | null {
  if (item.name === ActivityEventType.ConnectionVerified) {
    return item.connectionStatus
      ? interpolate(t.activity.connectionChecked, { status: connectionStatusLabel(item.connectionStatus, t) })
      : null
  }
  if (item.name === ActivityEventType.AppSynced || item.name === InboundEventType.ReviewReceived) {
    return appName
  }
  if (item.name === InboundEventType.WebhookReceived || item.name === InboundEventType.ArtifactProgress) {
    return appName
  }
  return [appName, item.jobKind ? jobKindLabel(item.jobKind, t) : null].filter(Boolean).join(' · ') || null
}

function Feed({ items }: { items: FeedItem[] }) {
  const t = useT()
  const format = useFormat()
  const { frozen, appName } = useActivity()
  return (
    <section className="feed" aria-label={t.activity.events}>
      <div className="card-h">
        <h2 className="h3">{t.activity.events}</h2>
        <span className="ml-auto">{frozen ? <Badge small>{t.activity.frozen}</Badge> : null}</span>
      </div>
      {items.length === 0 ? (
        <p className="m-0 p-4 text-[13px] text-fg3">{t.activity.noEvents}</p>
      ) : (
        <ol className="feed-b" aria-live="off">
          {items.map((item) => {
            const subject = feedSubject(item, t, appName(item.appId))
            return (
              <li key={item.id} className={cn('ev', item.kind === 'error' && 'x')}>
                <time dateTime={item.date}>{format.time(item.date)}</time>
                <div className="min-w-0">
                  <span className={cn('dir', FEED_DIR[item.kind].className)}>{t.activity.direction[FEED_DIR[item.kind].key]}</span>
                  <b className="mono text-[12px]">{item.name}</b> {subject ? <span className="text-fg2">{subject}</span> : null}
                  {item.detail ? <div className="m">{item.detail}</div> : null}
                </div>
              </li>
            )
          })}
        </ol>
      )}
      <div className="card-f text-xs text-fg3">{t.activity.feedFooter}</div>
    </section>
  )
}

/** One job as a row: store, title, the current step and its progress, status; the title opens the job (design 11.5). */
export function JobRow({ job }: { job: JobView }) {
  const t = useT()
  const format = useFormat()
  const { appName } = useActivity()
  const status = jobStatusView(job.status, t, { retrying: job.retrying })
  const step = job.stepName
    ? [job.stepOrder ? interpolate(t.activity.stepN, { order: job.stepOrder }) : null, job.stepName, progressText(job, format)]
        .filter(Boolean)
        .join(' · ')
    : null
  const meta = [step, job.channel ? channelLabel(job.channel, t) : null, job.requestedBy].filter(Boolean).join(' · ')
  return (
    <li className="jb">
      {job.store ? <StoreTag store={job.store} small codeOnly /> : <span />}
      <div className="min-w-0">
        <Link className="block truncate text-[13.5px] font-semibold hover:underline" to={`/activity/jobs/${job.id}`}>
          {jobTitle(job, t, appName(job.appId))}
        </Link>
        <span className="s">{job.retrying || (!isActive(job) && job.message) ? job.message : meta}</span>
      </div>
      <span className="max-md:hidden" />
      <span className="text-right">
        <Badge tone={status.tone} dot={status.dot} small>
          {status.label}
        </Badge>
      </span>
    </li>
  )
}

function ActiveJobs({ jobs }: { jobs: JobView[] }) {
  const t = useT()
  const active = jobs.filter(isActive)
  const shown = active.length > 0 ? active : jobs.slice(0, 5)
  return (
    <section className="card mt-4">
      <div className="card-h">
        <h2 className="h3">{active.length > 0 ? t.activity.activeJobs : t.activity.recentJobs}</h2>
        <span className="mono text-xs text-fg3">{shown.length}</span>
        <Link className="lnk ml-auto text-[13px]" to="/activity/jobs">
          {t.app.allJobs}
        </Link>
      </div>
      {shown.length === 0 ? (
        <p className="m-0 p-[18px] text-[13px] text-fg3">{t.activity.firstJobHint}</p>
      ) : (
        <ul className="m-0 list-none p-0">
          {shown.map((job) => (
            <JobRow key={job.id} job={job} />
          ))}
        </ul>
      )}
    </section>
  )
}

// The live page reads its clock from here: it moves on every event, every half minute for the elapsed times, and once
// more when a lingering token or a pulse is due to leave. A frozen screen keeps its clock too.
function useLineClock(state: HatState, frozen: boolean): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (frozen) {
      return
    }
    setNow(Date.now())
    const timer = window.setInterval(() => setNow(Date.now()), 30_000)
    return () => window.clearInterval(timer)
  }, [state, frozen])
  useEffect(() => {
    if (frozen) {
      return
    }
    const next = nextExpiry(state, Date.now())
    if (next === null) {
      return
    }
    const timer = window.setTimeout(() => setNow(Date.now()), Math.max(50, next - Date.now() + 50))
    return () => window.clearTimeout(timer)
  }, [state, now, frozen])
  return now
}

const OPEN_REJECTIONS: ListRequest = { PageNumber: 1, RowCount: 20, Sorting: { SortBy: 'Id', Type: SortType.Desc } }

// The calm data beside the stream: the board (reviews, builds, 24 hours), open rejection cases, open compliance
// deadlines and the connections. Each one that fails (a role without the right, an older API) only leaves its part out.
// Apps whose alerts are muted are left out of all of it (the attention bar says how many).
function useBoardInputs(jobs: JobView[]): BoardInputs {
  const { current } = useOrganization()
  const organizationId = current?.Id ?? 0
  const enabled = organizationId > 0
  const quiet = { enabled, retry: false, staleTime: 30_000 } as const
  const board = useQuery({
    queryKey: queryKeys.jobs.board(organizationId),
    queryFn: () => jobsApi.board(organizationId),
    refetchInterval: 120_000,
    ...quiet,
  })
  const rejections = useQuery({
    queryKey: [...rejectionKeys.all, 'open', organizationId] as const,
    queryFn: () => rejectionsApi.list(organizationId, { State: RejectionState.Open }, OPEN_REJECTIONS),
    ...quiet,
  })
  const compliance = useQuery({
    queryKey: contentKeys.orgCompliance(organizationId, true),
    queryFn: () => complianceApi.organization(organizationId, { OpenOnly: true }),
    ...quiet,
    staleTime: 300_000,
  })
  const connections = useQuery({
    queryKey: queryKeys.connections.list(organizationId),
    queryFn: () => connectionsApi.list(organizationId),
    ...quiet,
  })
  return useMemo(
    () =>
      withoutMutedApps({
        board: board.data ?? null,
        rejections: rejections.data?.Data ?? [],
        compliance: compliance.data ?? [],
        connections: connections.data ?? [],
        jobs,
      }),
    [board.data, rejections.data, compliance.data, connections.data, jobs],
  )
}

/**
 * The live view: what needs attention, the line (storeasy, its sources, one line per store with its tokens, pulses, 24
 * hours and gate), what is coming up, the event feed and the jobs. Reduced motion keeps the same line without the motion.
 * Store chips filter on the client; the filter lives in the address (?store=apple).
 */
export function ActivityPage() {
  const t = useT()
  const reduced = usePrefersReducedMotion()
  const { state, frozen, setFrozen } = useActivity()
  const [params, setParams] = useSearchParams()
  const storeFilter = STORE_PARAM[params.get('store') ?? ''] ?? null
  const now = useLineClock(state, frozen)

  const allJobs = useMemo(() => jobsOf(state), [state])
  const inputs = useBoardInputs(allJobs)
  const stores = LANE_STORES.filter((store) => !storeFilter || store === storeFilter)
  const lanes = useMemo(() => lanesOf(state), [state])
  const tracks = useMemo(() => tracksOf(state, now), [state, now])
  const upcomingAll = upcomingOf(inputs, now)
  const rows: StoreRow[] = stores.map((store) => ({
    store,
    track: tracks.find((track) => track.store === store) as Track,
    lane: lanes.find((lane) => lane.store === store),
    items: gateItemsOf(store, inputs, now),
    bars: hourStrip(inputs.board, store, now),
    parked: upcomingAll.filter((item) => item.store === store),
  }))
  const inStores = <T extends { store: StoreKind | null }>(item: T) => !storeFilter || item.store === storeFilter
  const attention = attentionOf(inputs, now).filter(inStores)
  const upcoming = upcomingAll.filter(inStores)
  const jobs = allJobs.filter((job) => !storeFilter || job.store === storeFilter)
  const feed = storeFilter ? state.feed.filter((item) => item.store === storeFilter) : state.feed
  const moving = rows.some((row) => row.track.tokens.length > 0)
  const inboundToday = (inputs.board?.Hours ?? [])
    .filter((hour) => !storeFilter || hour.Store === storeFilter)
    .reduce((sum, hour) => sum + hour.Inbound, 0)

  const setStore = (key: string | null) => {
    const next = new URLSearchParams(params)
    if (key) {
      next.set('store', key)
    } else {
      next.delete('store')
    }
    setParams(next, { replace: true })
  }

  return (
    <>
      <PageHeader
        title={t.activity.title}
        subtitle={reduced ? t.activity.reducedSubtitle : t.activity.subtitle}
        actions={
          <>
            <LiveBadge />
            <Button onClick={() => setFrozen(!frozen)} aria-pressed={frozen}>
              {frozen ? <Play aria-hidden /> : <Pause aria-hidden />}
              {frozen ? t.activity.resume : t.activity.freeze}
            </Button>
          </>
        }
      />
      <ActivityTabs />
      <div className="mb-4 overflow-hidden rounded-md empty:hidden">
        <ConnectionBanner />
      </div>
      <div className="toolbar" role="group" aria-label={t.activity.storeFilter}>
        <Chip selected={!storeFilter} onClick={() => setStore(null)}>
          {t.activity.allStores}
        </Chip>
        {(['apple', 'google', 'microsoft'] as const).map((key) => (
          <Chip key={key} selected={storeFilter === STORE_PARAM[key]} onClick={() => setStore(key)}>
            <StoreTag store={STORE_PARAM[key]} small />
          </Chip>
        ))}
      </div>
      <div className="hat">
        <section className="stage" aria-label={t.activity.sceneLabel}>
          <div className="stage-h">
            <AttentionBar items={attention} mutedAppIds={mutedAppIdsOf(inputs)} />
            <ul className="legend">
              <li>
                <i className="lo" />
                {t.activity.legendOut}
              </li>
              <li>
                <i className="li" />
                {t.activity.legendIn}
              </li>
              <li>
                <i className="lp" />
                {t.activity.legendPulse}
              </li>
              <li>
                <i className="lx" />
                {t.activity.legendError}
              </li>
            </ul>
          </div>
          <div className={cn('stage-b', frozen && 'frozen')}>
            <HatLine state={state} rows={rows} inboundToday={inboundToday} now={now} calm={reduced} frozen={frozen} />
          </div>
        </section>
        <div className="hat-low">
          <UpcomingBoard items={upcoming} rows={moving ? 4 : 8} now={now} />
          <Feed items={feed} />
        </div>
      </div>
      <ActiveJobs jobs={jobs} />
    </>
  )
}

/** The approval queue (11.8) waits for its endpoint (PATCH plans). */
export function ActivityPlansPage() {
  const t = useT()
  const enabled = useFeature('Plans')
  return (
    <>
      <PageHeader title={t.activity.title} subtitle={t.activity.subtitle} />
      <ActivityTabs />
      <EmptyState
        icon={<ClipboardCheck aria-hidden />}
        title={enabled ? t.activity.plansEmpty : t.activity.plansComingTitle}
        description={t.activity.plansComingBody}
      />
    </>
  )
}
