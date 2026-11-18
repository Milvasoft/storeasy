import {
  StepDirection,
  StoreJobStatus,
  StoreJobStepStatus,
  StoreKind,
  type ActionChannel,
  type ConnectionStatus,
  type IsoDateTime,
  type StoreJobKind,
  type StoreJobListDto,
} from '@/lib/api/types'
import { ActivityEventType, type ActivityEvent } from './events'

// The live view's state, built from the hub's events (pure, framework free, unit tested) and seeded with the jobs that
// were already active when the page opened. The Hat scene and the status list read the same state, so the reduced
// motion view never drifts from the animated one.

export const MAX_FEED = 50

export interface JobView {
  id: number
  kind: StoreJobKind | null
  appId: number | null
  store: StoreKind | null
  status: StoreJobStatus
  // Known from the job list only (the events do not carry them).
  channel: ActionChannel | null
  requestedBy: string | null
  maxAttempts: number | null
  attempt: number | null
  // A failed attempt waits for its next try (job.progress back to Queued with the reason).
  retrying: boolean
  // The latest step: what the lane draws.
  stepOrder: number | null
  stepName: string | null
  direction: StepDirection | null
  stepStatus: StoreJobStepStatus | null
  progress: number | null
  total: number | null
  message: string | null
  updatedAt: IsoDateTime
}

// out: to the store; in: back from it; event: storeasy's own news (queued, finished); error: a failure.
export type FeedKind = 'out' | 'in' | 'event' | 'error'

export interface FeedItem {
  id: string
  date: IsoDateTime
  kind: FeedKind
  // The step name, or the event type for job and organization events.
  name: string
  jobId: number | null
  jobKind: StoreJobKind | null
  appId: number | null
  store: StoreKind | null
  connectionStatus: ConnectionStatus | null
  detail: string | null
}

export interface HatState {
  jobs: Record<number, JobView>
  feed: FeedItem[]
  lastEventAt: IsoDateTime | null
  sequence: number
}

export const initialHatState: HatState = { jobs: {}, feed: [], lastEventAt: null, sequence: 0 }

export interface SeedAction {
  Type: 'seed'
  Jobs: StoreJobListDto[]
}

export type HatAction = ActivityEvent | SeedAction

function isSeed(action: HatAction): action is SeedAction {
  return action.Type === 'seed'
}

function pushFeed(state: HatState, event: ActivityEvent, kind: FeedKind, name: string): FeedItem[] {
  const item: FeedItem = {
    id: `${state.sequence}`,
    date: event.Date,
    kind,
    name,
    jobId: event.JobId ?? null,
    jobKind: event.JobKind ?? null,
    appId: event.AppId ?? null,
    store: event.Store ?? null,
    connectionStatus: event.ConnectionStatus ?? null,
    detail: event.Message ?? null,
  }
  return [item, ...state.feed].slice(0, MAX_FEED)
}

function emptyJob(id: number, date: IsoDateTime): JobView {
  return {
    id,
    kind: null,
    appId: null,
    store: null,
    status: StoreJobStatus.Queued,
    channel: null,
    requestedBy: null,
    maxAttempts: null,
    attempt: null,
    retrying: false,
    stepOrder: null,
    stepName: null,
    direction: null,
    stepStatus: null,
    progress: null,
    total: null,
    message: null,
    updatedAt: date,
  }
}

// The job an event belongs to, with what every job event carries (kind, app, store, attempt) folded in.
function jobOf(state: HatState, event: ActivityEvent, jobId: number): JobView {
  const known = state.jobs[jobId] ?? emptyJob(jobId, event.Date)
  return {
    ...known,
    kind: event.JobKind ?? known.kind,
    appId: event.AppId ?? known.appId,
    store: event.Store ?? known.store,
    attempt: event.Attempt ?? known.attempt,
    updatedAt: event.Date,
  }
}

function stepFeedKind(event: ActivityEvent): FeedKind {
  if (event.StepStatus === StoreJobStepStatus.Failed) {
    return 'error'
  }
  if (event.Direction === StepDirection.Outbound) {
    return 'out'
  }
  return event.Direction === StepDirection.Inbound ? 'in' : 'event'
}

function seed(state: HatState, jobs: StoreJobListDto[]): HatState {
  const next = { ...state.jobs }
  for (const dto of jobs) {
    const known = next[dto.Id]
    const base = known ?? emptyJob(dto.Id, dto.StartDate ?? dto.CreationDate ?? new Date(0).toISOString())
    // An event that arrived while the list loaded is newer than the list: it wins, the list fills the gaps.
    next[dto.Id] = {
      ...base,
      kind: base.kind ?? dto.Kind,
      appId: base.appId ?? dto.AppId ?? null,
      store: base.store ?? dto.Store ?? null,
      status: known ? base.status : dto.Status,
      channel: dto.Channel,
      requestedBy: dto.RequestedByUserName ?? null,
      maxAttempts: dto.MaxAttempts,
      attempt: base.attempt ?? dto.Attempt,
      message: base.message ?? dto.Error ?? null,
    }
  }
  return { ...state, jobs: next }
}

function finishedStatus(event: ActivityEvent): StoreJobStatus {
  if (event.JobStatus) {
    return event.JobStatus
  }
  if (event.Type === ActivityEventType.JobSucceeded) {
    return StoreJobStatus.Succeeded
  }
  return event.Type === ActivityEventType.JobCancelled ? StoreJobStatus.Cancelled : StoreJobStatus.Failed
}

export function hatReducer(state: HatState, action: HatAction): HatState {
  if (isSeed(action)) {
    return seed(state, action.Jobs)
  }
  const event = action
  const base: HatState = { ...state, sequence: state.sequence + 1, lastEventAt: event.Date }
  const jobId = event.JobId ?? null

  switch (event.Type) {
    case ActivityEventType.JobQueued: {
      if (jobId === null) {
        return state
      }
      const job: JobView = { ...jobOf(state, event, jobId), status: StoreJobStatus.Queued }
      return { ...base, jobs: { ...state.jobs, [jobId]: job }, feed: pushFeed(base, event, 'event', event.Type) }
    }
    case ActivityEventType.JobProgress: {
      if (jobId === null) {
        return state
      }
      const status = event.JobStatus ?? StoreJobStatus.Running
      // Back to Queued with a reason: the attempt failed and the job waits for its next try.
      const retrying = status === StoreJobStatus.Queued && !!event.Message
      const job: JobView = { ...jobOf(state, event, jobId), status, retrying, message: retrying ? (event.Message ?? null) : null }
      const feed = retrying ? pushFeed(base, event, 'error', event.Type) : state.feed
      return { ...base, jobs: { ...state.jobs, [jobId]: job }, feed }
    }
    case ActivityEventType.JobStep: {
      if (jobId === null) {
        return state
      }
      const known = state.jobs[jobId]
      const job: JobView = {
        ...jobOf(state, event, jobId),
        status: StoreJobStatus.Running,
        retrying: false,
        stepOrder: event.StepOrder ?? null,
        stepName: event.StepName ?? null,
        direction: event.Direction ?? null,
        stepStatus: event.StepStatus ?? null,
        progress: event.Progress ?? null,
        total: event.Total ?? null,
        message: event.Message ?? null,
      }
      // A step reports its start, its progress and its end; the feed gets the start and the end, not every tick.
      const newStep = !known || known.stepOrder !== job.stepOrder
      const ended = job.stepStatus !== StoreJobStepStatus.Running
      const feed = newStep || ended ? pushFeed(base, event, stepFeedKind(event), event.StepName ?? event.Type) : state.feed
      return { ...base, jobs: { ...state.jobs, [jobId]: job }, feed }
    }
    case ActivityEventType.JobSucceeded:
    case ActivityEventType.JobFailed:
    case ActivityEventType.JobCancelled: {
      if (jobId === null) {
        return state
      }
      const status = finishedStatus(event)
      const known = jobOf(state, event, jobId)
      const job: JobView = {
        ...known,
        status,
        retrying: false,
        stepStatus: status === StoreJobStatus.Failed ? StoreJobStepStatus.Failed : known.stepStatus,
        message: event.Message ?? known.message,
      }
      const kind: FeedKind = status === StoreJobStatus.Failed ? 'error' : 'event'
      return { ...base, jobs: { ...state.jobs, [jobId]: job }, feed: pushFeed(base, event, kind, event.Type) }
    }
    case ActivityEventType.ConnectionVerified:
    case ActivityEventType.AppSynced:
      return { ...base, feed: pushFeed(base, event, 'in', event.Type) }
    default:
      // A type this client does not know yet (a newer API): ignored, never a crash.
      return state
  }
}

export function isActive(job: JobView): boolean {
  return job.status === StoreJobStatus.Queued || job.status === StoreJobStatus.Running
}

export const LANE_STORES: readonly StoreKind[] = [StoreKind.Apple, StoreKind.Google, StoreKind.Microsoft]

export interface Lane {
  store: StoreKind
  // Active jobs on this store, newest first; the lane draws the first and counts the rest.
  jobs: JobView[]
  // The latest finished job, shown when nothing is moving (design 11.13).
  last: JobView | null
  // Something is really on the way: only then does the lane animate.
  moving: boolean
  failed: boolean
}

function newestFirst(a: JobView, b: JobView): number {
  if (a.updatedAt !== b.updatedAt) {
    return a.updatedAt < b.updatedAt ? 1 : -1
  }
  return b.id - a.id
}

export function lanesOf(state: HatState): Lane[] {
  const all = Object.values(state.jobs).sort(newestFirst)
  return LANE_STORES.map((store) => {
    const jobs = all.filter((job) => job.store === store && isActive(job))
    const current = jobs[0]
    return {
      store,
      jobs,
      last: all.find((job) => job.store === store && !isActive(job)) ?? null,
      moving: !!current && current.status === StoreJobStatus.Running && current.stepStatus === StoreJobStepStatus.Running,
      failed: !!current && (current.retrying || current.stepStatus === StoreJobStepStatus.Failed),
    }
  })
}

/** Every job of the state, newest first (the "active jobs" list under the scene). */
export function jobsOf(state: HatState): JobView[] {
  return Object.values(state.jobs).sort(newestFirst)
}

export function activeJobCount(state: HatState): number {
  return Object.values(state.jobs).filter(isActive).length
}
