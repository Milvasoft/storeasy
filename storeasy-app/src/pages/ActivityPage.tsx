import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ClipboardCheck, Pause, Play } from 'lucide-react'
import { HatScene, HatScenePhone, HatStatusList, jobTitle, progressText } from '@/components/activity/Hat'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { Banner } from '@/components/ui/Feedback'
import { Tabs } from '@/components/ui/Tabs'
import { Button, Chip, EmptyState, PageHeader } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { ActivityEventType } from '@/lib/activity/events'
import { isActive, jobsOf, lanesOf, type FeedItem, type JobView } from '@/lib/activity/hatState'
import { StoreKind } from '@/lib/api/types'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { usePrefersReducedMotion } from '@/lib/hooks/usePrefersReducedMotion'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import type { Dictionary } from '@/lib/i18n/en'
import { connectionStatusLabel, jobKindLabel, jobStatusView } from '@/lib/jobs/labels'
import { channelLabel } from '@/lib/org/auditLabels'
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
  in: { className: '', key: 'in' },
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
  if (item.name === ActivityEventType.AppSynced) {
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

/**
 * The live view (design 11.1-11.3, 11.13, 11.14): the Hat scene and its event feed, driven by the activity hub. With
 * reduced motion the scene is replaced by one status row per lane. Store chips filter on the client; the filter lives
 * in the address (?store=apple).
 */
export function ActivityPage() {
  const t = useT()
  const reduced = usePrefersReducedMotion()
  const { state, frozen, setFrozen } = useActivity()
  const [params, setParams] = useSearchParams()
  const storeFilter = STORE_PARAM[params.get('store') ?? ''] ?? null

  const lanes = useMemo(() => lanesOf(state).filter((lane) => !storeFilter || lane.store === storeFilter), [state, storeFilter])
  const jobs = useMemo(() => jobsOf(state).filter((job) => !storeFilter || job.store === storeFilter), [state, storeFilter])
  const feed = storeFilter ? state.feed.filter((item) => item.store === storeFilter) : state.feed
  const quiet = lanes.every((lane) => lane.jobs.length === 0)

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
                <i className="lx" />
                {t.activity.legendError}
              </li>
            </ul>
          </div>
          {reduced ? (
            <HatStatusList lanes={lanes} />
          ) : (
            <div className={cn('stage-b', frozen && 'frozen')}>
              <div className="w-full max-md:hidden">
                <HatScene lanes={lanes} frozen={frozen} />
              </div>
              <div className="w-full md:hidden">
                <HatScenePhone lanes={lanes} frozen={frozen} />
              </div>
              {quiet ? (
                <div className="pb-1 pt-2 text-center">
                  <b className="block text-[13.5px]">{t.activity.quietTitle}</b>
                  <span className="text-xs text-fg3">{t.activity.quietBody}</span>
                </div>
              ) : null}
            </div>
          )}
        </section>
        <Feed items={feed} />
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
