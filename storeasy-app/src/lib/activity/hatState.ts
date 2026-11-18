import { StoreKind, type IsoDateTime } from '@/lib/api/types'
import { ActivityEventName, JobStatus, StepDirection, StepStatus, type ActivityEvent, type JobChannel } from './events'

// The live view's state, built from the event stream alone (pure, framework free, unit tested). The Hat scene and the
// status list read the same state, so the reduced motion view never drifts from the animated one.

export const MAX_FEED = 50
// A webhook pulse shows on its store node for this long.
export const WEBHOOK_PULSE_MS = 4_000

export interface JobView {
  id: number
  kind: string
  title: string
  appName: string | null
  store: StoreKind
  channel: JobChannel | null
  requestedBy: string | null
  status: JobStatus
  stepCount: number | null
  doneSteps: number
  // The latest step: what the lane draws.
  stepKind: string | null
  direction: StepDirection | null
  stepStatus: StepStatus | null
  bytes: number | null
  total: number | null
  message: string | null
  attempt: number | null
  maxAttempt: number | null
  updatedAt: IsoDateTime
}

export interface ArtifactView {
  sessionId: string
  appName: string
  source: string
  fileName: string
  bytes: number
  total: number
  updatedAt: IsoDateTime
}

export type FeedKind = 'out' | 'in' | 'webhook' | 'error'

export interface FeedItem {
  id: string
  date: IsoDateTime
  kind: FeedKind
  name: string
  subject: string | null
  detail: string | null
  store: StoreKind | null
}

export interface HatState {
  jobs: Record<number, JobView>
  artifacts: Record<string, ArtifactView>
  feed: FeedItem[]
  webhooks: Partial<Record<StoreKind, number>>
  lastEventAt: IsoDateTime | null
  sequence: number
}

export const initialHatState: HatState = { jobs: {}, artifacts: {}, feed: [], webhooks: {}, lastEventAt: null, sequence: 0 }

function pushFeed(state: HatState, item: Omit<FeedItem, 'id'>): FeedItem[] {
  return [{ ...item, id: `${state.sequence}` }, ...state.feed].slice(0, MAX_FEED)
}

// `now` is passed in (the reducer stays pure); it stamps webhook pulses.
export function hatReducer(state: HatState, event: ActivityEvent, now: number = Date.now()): HatState {
  const base = { ...state, sequence: state.sequence + 1, lastEventAt: event.data.Date }
  switch (event.type) {
    case ActivityEventName.JobCreated: {
      const data = event.data
      const job: JobView = {
        id: data.JobId,
        kind: data.Kind,
        title: data.Title,
        appName: data.AppName ?? null,
        store: data.Store,
        channel: data.Channel ?? null,
        requestedBy: data.RequestedByUserName ?? null,
        status: JobStatus.Queued,
        stepCount: data.StepCount ?? null,
        doneSteps: 0,
        stepKind: null,
        direction: null,
        stepStatus: null,
        bytes: null,
        total: null,
        message: null,
        attempt: null,
        maxAttempt: null,
        updatedAt: data.Date,
      }
      return { ...base, jobs: { ...state.jobs, [job.id]: job } }
    }
    case ActivityEventName.JobStep: {
      const data = event.data
      const known = state.jobs[data.JobId]
      const failed = data.Status === StepStatus.Failed || data.Status === StepStatus.Retrying
      const feed = pushFeed(base, {
        date: data.Date,
        kind: failed ? 'error' : data.Direction === StepDirection.Outbound ? 'out' : 'in',
        name: data.Kind,
        subject: known ? [known.appName, known.title].filter(Boolean).join(' · ') : null,
        detail: data.Message ?? null,
        store: known?.store ?? null,
      })
      if (!known) {
        // A step of a job created before this page opened: it shows in the feed; the lane waits for the job list
        // (PATCH jobs, planned) to know its store.
        return { ...base, feed }
      }
      const job: JobView = {
        ...known,
        status: data.Status === StepStatus.Failed ? JobStatus.Failed : JobStatus.Running,
        doneSteps: data.Status === StepStatus.Succeeded ? Math.max(known.doneSteps, data.Order) : known.doneSteps,
        stepKind: data.Kind,
        direction: data.Direction,
        stepStatus: data.Status,
        bytes: data.Bytes ?? null,
        total: data.Total ?? null,
        message: data.Message ?? null,
        attempt: data.Attempt ?? null,
        maxAttempt: data.MaxAttempt ?? null,
        updatedAt: data.Date,
      }
      return { ...base, jobs: { ...state.jobs, [job.id]: job }, feed }
    }
    case ActivityEventName.JobFinished: {
      const data = event.data
      const known = state.jobs[data.JobId]
      const jobs = { ...state.jobs }
      if (known) {
        jobs[data.JobId] = {
          ...known,
          status: data.Status,
          stepStatus: data.Status === JobStatus.Failed ? StepStatus.Failed : known.stepStatus,
          message: data.Message ?? known.message,
          updatedAt: data.Date,
        }
      }
      const feed = pushFeed(base, {
        date: data.Date,
        kind: data.Status === JobStatus.Failed ? 'error' : 'in',
        name: ActivityEventName.JobFinished,
        subject: [data.AppName, data.Title].filter(Boolean).join(' · '),
        detail: data.Message ?? null,
        store: data.Store,
      })
      return { ...base, jobs, feed }
    }
    case ActivityEventName.WebhookReceived: {
      const data = event.data
      const change = data.OldValue && data.NewValue ? `${data.OldValue} → ${data.NewValue}` : (data.NewValue ?? data.EventType)
      const feed = pushFeed(base, {
        date: data.Date,
        kind: 'webhook',
        name: ActivityEventName.WebhookReceived,
        subject: data.AppName ?? null,
        detail: change,
        store: data.Store,
      })
      return { ...base, feed, webhooks: { ...state.webhooks, [data.Store]: now } }
    }
    case ActivityEventName.ReviewReceived: {
      const data = event.data
      const feed = pushFeed(base, {
        date: data.Date,
        kind: 'in',
        name: ActivityEventName.ReviewReceived,
        subject: data.AppName,
        detail: [`${data.Rating}/5`, data.Locale, data.Title ? `"${data.Title}"` : null].filter(Boolean).join(' · '),
        store: data.Store,
      })
      return { ...base, feed }
    }
    case ActivityEventName.ArtifactProgress: {
      const data = event.data
      const artifact: ArtifactView = {
        sessionId: data.SessionId,
        appName: data.AppName,
        source: data.Source,
        fileName: data.FileName,
        bytes: data.Bytes,
        total: data.Total,
        updatedAt: data.Date,
      }
      const done = data.Bytes >= data.Total
      const artifacts = { ...state.artifacts }
      if (done) {
        delete artifacts[data.SessionId]
      } else {
        artifacts[data.SessionId] = artifact
      }
      const feed = pushFeed(base, {
        date: data.Date,
        kind: 'in',
        name: 'artifact.upload',
        subject: data.FileName,
        detail: data.Source,
        store: null,
      })
      return { ...base, artifacts, feed }
    }
    default:
      return state
  }
}

export function isActive(job: JobView): boolean {
  return job.status === JobStatus.Queued || job.status === JobStatus.Running
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
  webhookAt: number | null
}

export function lanesOf(state: HatState, now: number = Date.now()): Lane[] {
  const all = Object.values(state.jobs).sort((a, b) => (a.updatedAt < b.updatedAt ? 1 : -1))
  return LANE_STORES.map((store) => {
    const jobs = all.filter((job) => job.store === store && isActive(job))
    const current = jobs[0]
    const failed = !!current && (current.stepStatus === StepStatus.Retrying || current.stepStatus === StepStatus.Failed)
    const pulse = state.webhooks[store]
    return {
      store,
      jobs,
      last: all.find((job) => job.store === store && !isActive(job)) ?? null,
      moving: !!current && current.stepStatus === StepStatus.Running,
      failed,
      webhookAt: pulse !== undefined && now - pulse < WEBHOOK_PULSE_MS ? pulse : null,
    }
  })
}

export function activeJobCount(state: HatState): number {
  return Object.values(state.jobs).filter(isActive).length + Object.keys(state.artifacts).length
}
