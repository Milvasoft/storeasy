import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ClipboardCheck, ListChecks, Pause, Play } from 'lucide-react'
import { HatScene, HatScenePhone, HatStatusList, laneDetail } from '@/components/activity/Hat'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { Banner } from '@/components/ui/Feedback'
import { StepBar, type StepState } from '@/components/ui/Progress'
import { Tabs } from '@/components/ui/Tabs'
import { Button, Chip, EmptyState, PageHeader } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { JobChannel, JobStatus } from '@/lib/activity/events'
import { lanesOf, type FeedItem, type JobView, type Lane } from '@/lib/activity/hatState'
import { StoreKind } from '@/lib/api/types'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { usePrefersReducedMotion } from '@/lib/hooks/usePrefersReducedMotion'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import type { Dictionary } from '@/lib/i18n/en'
import { cn } from '@/lib/utils/cn'

const STORE_PARAM: Record<string, StoreKind> = { apple: StoreKind.Apple, google: StoreKind.Google, microsoft: StoreKind.Microsoft }

function ActivityTabs() {
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

function channelText(channel: JobChannel | null, t: Dictionary): string | null {
  switch (channel) {
    case JobChannel.Web:
      return t.enums.channel.web
    case JobChannel.Mcp:
      return t.enums.channel.mcp
    case JobChannel.Api:
      return t.enums.channel.api
    case JobChannel.Ci:
      return t.enums.channel.ci
    default:
      return null
  }
}

// Ticks once a second while a retry countdown is on screen.
function useNow(active: boolean): number {
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

const FEED_DIR: Record<FeedItem['kind'], { className: string; key: 'out' | 'in' | 'webhook' | 'error' }> = {
  out: { className: 'out', key: 'out' },
  in: { className: '', key: 'in' },
  webhook: { className: 'wh', key: 'webhook' },
  error: { className: 'err', key: 'error' },
}

function Feed({ items }: { items: FeedItem[] }) {
  const t = useT()
  const format = useFormat()
  const { frozen } = useActivity()
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
          {items.map((item) => (
            <li key={item.id} className={cn('ev', item.kind === 'error' && 'x')}>
              <time dateTime={item.date}>{format.time(item.date)}</time>
              <div className="min-w-0">
                <span className={cn('dir', FEED_DIR[item.kind].className)}>{t.activity.direction[FEED_DIR[item.kind].key]}</span>
                <b className="mono text-[12px]">{item.name}</b> {item.subject ? <span className="text-fg2">{item.subject}</span> : null}
                {item.detail ? <div className="m">{item.detail}</div> : null}
              </div>
            </li>
          ))}
        </ol>
      )}
      <div className="card-f text-xs text-fg3">{t.activity.feedFooter}</div>
    </section>
  )
}

function stepStates(job: JobView): StepState[] {
  const count = job.stepCount ?? 0
  return Array.from({ length: count }, (_, index) => {
    if (index < job.doneSteps) {
      return 'done'
    }
    if (index === job.doneSteps) {
      return job.status === JobStatus.Failed ? 'failed' : 'now'
    }
    return 'pending'
  })
}

function ActiveJobs({ lanes }: { lanes: Lane[] }) {
  const t = useT()
  const format = useFormat()
  const jobs = lanes.flatMap((lane) => lane.jobs.map((job) => ({ job, lane })))
  const finished = lanes.map((lane) => lane.last).filter((job): job is JobView => !!job)
  return (
    <section className="card mt-4">
      <div className="card-h">
        <h2 className="h3">{jobs.length > 0 ? t.activity.activeJobs : t.activity.recentJobs}</h2>
        <span className="mono text-xs text-fg3">{jobs.length > 0 ? jobs.length : finished.length}</span>
      </div>
      {jobs.length === 0 && finished.length === 0 ? (
        <p className="m-0 p-[18px] text-[13px] text-fg3">{t.activity.firstJobHint}</p>
      ) : (
        <ul className="m-0 list-none p-0">
          {(jobs.length > 0 ? jobs : finished.map((job) => ({ job, lane: null }))).map(({ job, lane }) => (
            <li key={job.id} className="jb">
              <StoreTag store={job.store} small codeOnly />
              <div className="min-w-0">
                <b className="block truncate text-[13.5px]">{[job.appName, job.title].filter(Boolean).join(' · ')}</b>
                <span className="s">{[job.stepKind, channelText(job.channel, t), job.requestedBy].filter(Boolean).join(' · ')}</span>
              </div>
              {job.stepCount ? (
                <div className="stpc">
                  <StepBar steps={stepStates(job)} label={interpolate(t.activity.stepOf, { done: job.doneSteps, total: job.stepCount })} />
                </div>
              ) : (
                <span className="max-md:hidden" />
              )}
              <span className={cn('mono text-right text-xs', lane?.failed ? 'text-danger' : 'text-fg2')}>
                {lane
                  ? laneDetail(lane, t, format)
                  : job.status === JobStatus.Succeeded
                    ? t.activity.succeeded
                    : job.status === JobStatus.Failed
                      ? t.activity.failed
                      : t.activity.canceled}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

/**
 * The live view (design 11.1-11.3, 11.13, 11.14): the Hat scene and its event feed, driven by the activity stream. With
 * reduced motion the scene is replaced by one status row per lane. Store chips filter on the client; the filter lives
 * in the address (?store=apple).
 */
export function ActivityPage() {
  const t = useT()
  const reduced = usePrefersReducedMotion()
  const { state, frozen, setFrozen } = useActivity()
  const [params, setParams] = useSearchParams()
  const storeFilter = STORE_PARAM[params.get('store') ?? ''] ?? null
  const now = useNow(Object.keys(state.webhooks).length > 0)

  const lanes = useMemo(() => lanesOf(state, now).filter((lane) => !storeFilter || lane.store === storeFilter), [state, now, storeFilter])
  const artifacts = storeFilter ? [] : Object.values(state.artifacts)
  const feed = storeFilter ? state.feed.filter((item) => item.store === storeFilter) : state.feed
  const quiet = lanes.every((lane) => lane.jobs.length === 0) && artifacts.length === 0

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
                <i className="lw" />
                {t.activity.legendWebhook}
              </li>
              <li>
                <i className="lx" />
                {t.activity.legendError}
              </li>
            </ul>
          </div>
          {reduced ? (
            <HatStatusList lanes={lanes} artifacts={artifacts} />
          ) : (
            <div className={cn('stage-b', frozen && 'frozen')}>
              <div className="w-full max-md:hidden">
                <HatScene lanes={lanes} artifacts={artifacts} frozen={frozen} />
              </div>
              <div className="w-full md:hidden">
                <HatScenePhone lanes={lanes} artifacts={artifacts} frozen={frozen} />
              </div>
              {quiet ? (
                <div className="pointer-events-none absolute inset-x-0 bottom-3 text-center">
                  <b className="block text-[13.5px]">{t.activity.quietTitle}</b>
                  <span className="text-xs text-fg3">{t.activity.quietBody}</span>
                </div>
              ) : null}
            </div>
          )}
        </section>
        <Feed items={feed} />
      </div>
      <ActiveJobs lanes={lanes} />
    </>
  )
}

/** Jobs (11.4) and the approval queue (11.8) wait for their endpoints (PATCH jobs, PATCH plans). */
export function ActivityJobsPage() {
  const t = useT()
  const enabled = useFeature('Jobs')
  return (
    <>
      <PageHeader title={t.activity.title} subtitle={t.activity.subtitle} />
      <ActivityTabs />
      <EmptyState
        icon={<ListChecks aria-hidden />}
        title={enabled ? t.activity.jobsEmpty : t.activity.jobsComingTitle}
        description={t.activity.jobsComingBody}
        action={
          <Link className="btn" to="/activity">
            {t.activity.backToHat}
          </Link>
        }
      />
    </>
  )
}

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
