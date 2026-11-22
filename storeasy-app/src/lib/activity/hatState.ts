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
import { ActivityEventType, InboundEventType, type ActivityEvent } from './events'
import { ALL_STORES } from '@/lib/stores/channels'

// The live view's state, built from the hub's events (pure, framework free, unit tested) and seeded with the jobs that
// were already active when the page opened. The line, its tokens and pulses, the gates and the reduced motion view all
// read the same state, so the calm view never drifts from the animated one.

export const MAX_FEED = 50
export const MAX_PULSES = 30
export const MAX_INTAKES = 20
// The steps a token keeps as its stations; older ones fold away.
export const MAX_STEPS = 8

/** One step a job went through: a station on its token. */
export interface StepPip {
  order: number
  name: string
  direction: StepDirection | null
  status: StoreJobStepStatus | null
}

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
  // The latest step: where the token stands.
  stepOrder: number | null
  stepName: string | null
  direction: StepDirection | null
  stepStatus: StoreJobStepStatus | null
  progress: number | null
  total: number | null
  message: string | null
  updatedAt: IsoDateTime
  // Every step seen live, in order (the token's stations); empty for a job only known from the list.
  steps: StepPip[]
  // When this browser saw the job end (ms); null while it runs or for a job that ended before the page opened.
  endedAt: number | null
}

// out: to the store; in: back from it or from the store on its own; event: storeasy's own news; error: a failure.
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

// What a store said on its own: a webhook event, an App Review state, a rejection, new reviews.
export type PulseKind = 'webhook' | 'version' | 'rejection' | 'review'

/** An inbound event riding from a store's gate into storeasy. */
export interface Pulse {
  id: string
  kind: PulseKind
  store: StoreKind | null
  appId: number | null
  // A short state or event name ("IN_REVIEW", "SUBSCRIPTION_RENEWED"); null when the event carried none.
  label: string | null
  detail: string | null
  date: IsoDateTime
  receivedAt: number
}

// ArtifactSessionStatus names (storeasy-api): the session's state as artifact.progress reports it.
export const INTAKE_ACTIVE = ['Uploading', 'Parsing', 'Checking', 'Delivering'] as const
export const INTAKE_FAILED = ['Rejected', 'Failed', 'Held'] as const

/** A package upload session (CI, EAS, a person's upload) on its way into storeasy. */
export interface IntakeView {
  sessionId: number
  appId: number | null
  store: StoreKind | null
  jobId: number | null
  status: string
  date: IsoDateTime
  receivedAt: number
}

export interface HatState {
  jobs: Record<number, JobView>
  feed: FeedItem[]
  pulses: Pulse[]
  intakes: Record<number, IntakeView>
  lastEventAt: IsoDateTime | null
  sequence: number
}

export const initialHatState: HatState = { jobs: {}, feed: [], pulses: [], intakes: {}, lastEventAt: null, sequence: 0 }

export interface SeedAction {
  Type: 'seed'
  Jobs: StoreJobListDto[]
}

export type HatAction = ActivityEvent | SeedAction

function isSeed(action: HatAction): action is SeedAction {
  return action.Type === 'seed'
}

function receivedAt(event: ActivityEvent): number {
  return event.ReceivedAt ?? Date.parse(event.Date)
}

function pushFeed(state: HatState, event: ActivityEvent, kind: FeedKind, name: string, detail?: string | null): FeedItem[] {
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
    detail: detail === undefined ? (event.Message ?? null) : detail,
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
    steps: [],
    endedAt: null,
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

function withStep(steps: StepPip[], pip: StepPip): StepPip[] {
  const index = steps.findIndex((step) => step.order === pip.order)
  const next = index >= 0 ? steps.map((step, at) => (at === index ? pip : step)) : [...steps, pip].sort((a, b) => a.order - b.order)
  return next.slice(-MAX_STEPS)
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

/**
 * The state a store message names: the new value of "OLD -> NEW", else the trailing STATE_NAME of a sentence, else the
 * message itself when it is one short word. Null when there is nothing short to show.
 */
export function pulseLabel(message: string | null | undefined): string | null {
  if (!message) {
    return null
  }
  const arrow = message.split('->')
  if (arrow.length > 1) {
    return arrow[arrow.length - 1].trim() || null
  }
  const state = /([A-Z][A-Z0-9_]{3,})\W*$/.exec(message)
  if (state) {
    return state[1]
  }
  return /^\S{1,32}$/.test(message.trim()) ? message.trim() : null
}

const PULSE_KIND: Record<string, PulseKind> = {
  [InboundEventType.WebhookReceived]: 'webhook',
  [InboundEventType.VersionState]: 'version',
  [InboundEventType.RejectionOpened]: 'rejection',
  [InboundEventType.ReviewReceived]: 'review',
}

function inbound(state: HatState, base: HatState, event: ActivityEvent, kind: PulseKind): HatState {
  // App Review states and rejection cases come from App Store Connect only (today); the event does not name the store.
  const store = event.Store ?? (kind === 'version' || kind === 'rejection' ? StoreKind.Apple : null)
  const pulse: Pulse = {
    id: `p${base.sequence}`,
    kind,
    store,
    appId: event.AppId ?? null,
    label: kind === 'review' ? null : pulseLabel(event.Message),
    detail: event.Message ?? null,
    date: event.Date,
    receivedAt: receivedAt(event),
  }
  return {
    ...base,
    pulses: [pulse, ...state.pulses].slice(0, MAX_PULSES),
    feed: pushFeed(base, { ...event, Store: store }, kind === 'rejection' ? 'error' : 'in', event.Type),
  }
}

/** "Session 12: Uploading" from artifact.progress, read into its parts; null for anything else. */
export function readIntakeMessage(message: string | null | undefined): { sessionId: number; status: string } | null {
  const match = /Session\s+(\d+):\s*(.+)$/.exec(message ?? '')
  return match ? { sessionId: Number(match[1]), status: match[2].trim() } : null
}

function intake(state: HatState, base: HatState, event: ActivityEvent): HatState {
  const parsed = readIntakeMessage(event.Message)
  if (!parsed) {
    return state
  }
  const known = state.intakes[parsed.sessionId]
  const view: IntakeView = {
    sessionId: parsed.sessionId,
    appId: event.AppId ?? known?.appId ?? null,
    store: event.Store ?? known?.store ?? null,
    jobId: event.JobId ?? known?.jobId ?? null,
    status: parsed.status,
    date: event.Date,
    receivedAt: receivedAt(event),
  }
  const intakes = Object.fromEntries(
    Object.values({ ...state.intakes, [parsed.sessionId]: view })
      .sort((a, b) => b.receivedAt - a.receivedAt || b.sessionId - a.sessionId)
      .slice(0, MAX_INTAKES)
      .map((item) => [item.sessionId, item]),
  )
  const failed = (INTAKE_FAILED as readonly string[]).includes(parsed.status)
  // The feed gets the start and the end of a session, not every state in between.
  const notable = !known || failed || !(INTAKE_ACTIVE as readonly string[]).includes(parsed.status)
  return {
    ...base,
    intakes,
    feed: notable ? pushFeed(base, { ...event, Store: view.store }, failed ? 'error' : 'in', event.Type, parsed.status) : state.feed,
  }
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
      const job: JobView = { ...jobOf(state, event, jobId), status: StoreJobStatus.Queued, endedAt: null }
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
      const current = jobOf(state, event, jobId)
      const job: JobView = {
        ...current,
        status: StoreJobStatus.Running,
        retrying: false,
        stepOrder: event.StepOrder ?? null,
        stepName: event.StepName ?? null,
        direction: event.Direction ?? null,
        stepStatus: event.StepStatus ?? null,
        progress: event.Progress ?? null,
        total: event.Total ?? null,
        message: event.Message ?? null,
        steps:
          event.StepOrder != null
            ? withStep(current.steps, {
                order: event.StepOrder,
                name: event.StepName ?? '',
                direction: event.Direction ?? null,
                status: event.StepStatus ?? null,
              })
            : current.steps,
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
        endedAt: receivedAt(event),
      }
      const kind: FeedKind = status === StoreJobStatus.Failed ? 'error' : 'event'
      return { ...base, jobs: { ...state.jobs, [jobId]: job }, feed: pushFeed(base, event, kind, event.Type) }
    }
    case ActivityEventType.ConnectionVerified:
    case ActivityEventType.AppSynced:
      return { ...base, feed: pushFeed(base, event, 'in', event.Type) }
    case InboundEventType.WebhookReceived:
    case InboundEventType.VersionState:
    case InboundEventType.RejectionOpened:
    case InboundEventType.ReviewReceived:
      return inbound(state, base, event, PULSE_KIND[event.Type])
    case InboundEventType.ArtifactProgress:
      return intake(state, base, event)
    default:
      // A type this client does not know yet (a newer API): ignored, never a crash.
      return state
  }
}

export function isActive(job: JobView): boolean {
  return job.status === StoreJobStatus.Queued || job.status === StoreJobStatus.Running
}

export const LANE_STORES: readonly StoreKind[] = ALL_STORES

export interface Lane {
  store: StoreKind
  // Active jobs on this store, newest first.
  jobs: JobView[]
  // The latest finished job, shown when nothing is moving.
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

/** Every job of the state, newest first (the "active jobs" list under the line). */
export function jobsOf(state: HatState): JobView[] {
  return Object.values(state.jobs).sort(newestFirst)
}

export function activeJobCount(state: HatState): number {
  return Object.values(state.jobs).filter(isActive).length
}

// ---- Tokens: where a job stands on its line ---------------------------------------------------------------------------

// queued: on storeasy's platform; internal: storeasy's own work; out: on the way to the store; in: on the way back;
// done: arrived (the job ended well); failed: stopped where its step failed.
export type TokenPhase = 'queued' | 'internal' | 'out' | 'in' | 'done' | 'failed'

export interface Token {
  job: JobView
  // 0 is storeasy's end of the line, 1 the store's gate.
  x: number
  phase: TokenPhase
  // The step reports no progress: the token shows motion in place instead of claiming a position.
  indeterminate: boolean
}

// A finished job's token stays this long: a success glides into the gate, a failure stays red where it stopped.
export const DONE_LINGER_MS = 6_000
export const FAILED_LINGER_MS = 90_000
// Tokens drawn per line; the rest are counted.
export const MAX_TOKENS = 3

function fraction(job: JobView): number | null {
  return job.progress !== null && job.total ? Math.min(1, Math.max(0, job.progress / job.total)) : null
}

/**
 * Where a job's token stands, from its real step: queued at storeasy (0.04), storeasy's own work near it (0.14), an
 * outbound step from 0.2 to 0.8 by its progress (0.85 once the store answered), an inbound step back from 0.8 to 0.2,
 * and a finished job at the gate (1). A failed step keeps the place where it failed.
 */
export function tokenPosition(job: JobView): number {
  if (job.status === StoreJobStatus.Succeeded) {
    return 1
  }
  if (job.status === StoreJobStatus.Queued && !job.retrying && job.stepOrder === null) {
    return 0.04
  }
  const part = fraction(job)
  switch (job.direction) {
    case StepDirection.Outbound:
      if (job.stepStatus === StoreJobStepStatus.Succeeded) {
        return 0.85
      }
      return 0.2 + 0.6 * (part ?? 0.4)
    case StepDirection.Inbound:
      if (job.stepStatus === StoreJobStepStatus.Succeeded) {
        return 0.2
      }
      return 0.8 - 0.6 * (part ?? 0.4)
    case StepDirection.Internal:
      return 0.14
    default:
      return 0.08
  }
}

export function tokenPhase(job: JobView): TokenPhase {
  if (job.retrying || job.status === StoreJobStatus.Failed || job.stepStatus === StoreJobStepStatus.Failed) {
    return 'failed'
  }
  if (job.status === StoreJobStatus.Succeeded) {
    return 'done'
  }
  if (job.status === StoreJobStatus.Queued) {
    return 'queued'
  }
  if (job.direction === StepDirection.Outbound) {
    return 'out'
  }
  if (job.direction === StepDirection.Inbound) {
    return 'in'
  }
  return 'internal'
}

function onLine(job: JobView, now: number): boolean {
  if (isActive(job)) {
    return true
  }
  if (job.endedAt === null) {
    return false
  }
  if (job.status === StoreJobStatus.Succeeded) {
    return now - job.endedAt < DONE_LINGER_MS
  }
  return job.status === StoreJobStatus.Failed && now - job.endedAt < FAILED_LINGER_MS
}

export interface Track {
  store: StoreKind
  // Drawn tokens, newest first (at most MAX_TOKENS).
  tokens: Token[]
  // Jobs on the line that are not drawn.
  hidden: number
  // Inbound pulses still riding (a burst folded into one).
  pulses: PulseGroup[]
  failed: boolean
}

export function tracksOf(state: HatState, now: number): Track[] {
  const all = Object.values(state.jobs).sort(newestFirst)
  return LANE_STORES.map((store) => {
    const jobs = all.filter((job) => job.store === store && onLine(job, now))
    const tokens = jobs.slice(0, MAX_TOKENS).map((job) => {
      const phase = tokenPhase(job)
      return {
        job,
        x: tokenPosition(job),
        phase,
        indeterminate: (phase === 'out' || phase === 'in') && job.stepStatus === StoreJobStepStatus.Running && fraction(job) === null,
      }
    })
    return {
      store,
      tokens,
      hidden: Math.max(0, jobs.length - tokens.length),
      pulses: pulsesOf(state, store, now),
      failed: tokens.some((token) => token.phase === 'failed'),
    }
  })
}

/** The soonest moment a lingering token or a pulse leaves the line (ms), so the page re-renders only when needed. */
export function nextExpiry(state: HatState, now: number): number | null {
  const times: number[] = []
  for (const job of Object.values(state.jobs)) {
    if (job.endedAt !== null && !isActive(job)) {
      const until = job.endedAt + (job.status === StoreJobStatus.Succeeded ? DONE_LINGER_MS : FAILED_LINGER_MS)
      if (until > now && job.status !== StoreJobStatus.Cancelled) {
        times.push(until)
      }
    }
  }
  for (const pulse of state.pulses) {
    if (pulse.receivedAt + PULSE_MS > now) {
      times.push(pulse.receivedAt + PULSE_MS)
    }
  }
  return times.length > 0 ? Math.min(...times) : null
}

// ---- Pulses: inbound store events riding into storeasy ---------------------------------------------------------------

// How long a pulse rides (the animation) and stays listed as "just arrived" (the calm view).
export const PULSE_MS = 9_000
// Pulses this close together ride as one, with a count.
export const PULSE_BURST_MS = 2_000
export const MAX_PULSE_GROUPS = 3

export interface PulseGroup {
  id: string
  store: StoreKind | null
  kind: PulseKind
  appId: number | null
  label: string | null
  count: number
  receivedAt: number
  pulses: Pulse[]
}

/** The store's pulses of the last PULSE_MS, a burst folded into one group, newest first. */
export function pulsesOf(state: HatState, store: StoreKind, now: number): PulseGroup[] {
  const recent = state.pulses.filter((pulse) => pulse.store === store && now - pulse.receivedAt < PULSE_MS)
  const groups: PulseGroup[] = []
  for (const pulse of recent) {
    const group = groups.find((item) => Math.abs(item.receivedAt - pulse.receivedAt) < PULSE_BURST_MS)
    if (group) {
      group.count += 1
      group.pulses.push(pulse)
      // A burst of one app keeps its name; a mixed one names no app.
      if (group.appId !== pulse.appId) {
        group.appId = null
      }
      if (group.label !== pulse.label) {
        group.label = null
      }
      if (pulse.kind === 'rejection') {
        group.kind = 'rejection'
      }
    } else {
      groups.push({
        id: pulse.id,
        store: pulse.store,
        kind: pulse.kind,
        appId: pulse.appId,
        label: pulse.label,
        count: 1,
        receivedAt: pulse.receivedAt,
        pulses: [pulse],
      })
    }
  }
  return groups.slice(0, MAX_PULSE_GROUPS)
}

// ---- Sources: what flows into storeasy -------------------------------------------------------------------------------

export function activeIntakes(state: HatState): IntakeView[] {
  return Object.values(state.intakes)
    .filter((item) => (INTAKE_ACTIVE as readonly string[]).includes(item.status))
    .sort((a, b) => b.receivedAt - a.receivedAt)
}

export function lastIntake(state: HatState): IntakeView | null {
  return Object.values(state.intakes).sort((a, b) => b.receivedAt - a.receivedAt)[0] ?? null
}
