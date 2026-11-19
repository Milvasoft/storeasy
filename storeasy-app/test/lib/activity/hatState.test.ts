import { describe, expect, it } from 'vitest'
import { ActivityEventType, type ActivityEvent } from '@/lib/activity/events'
import { InboundEventType } from '@/lib/activity/events'
import {
  activeIntakes,
  activeJobCount,
  DONE_LINGER_MS,
  hatReducer,
  MAX_TOKENS,
  nextExpiry,
  PULSE_MS,
  pulseLabel,
  readIntakeMessage,
  tokenPosition,
  tracksOf,
  initialHatState,
  jobsOf,
  lanesOf,
  MAX_FEED,
  type HatAction,
  type HatState,
} from '@/lib/activity/hatState'
import { ConnectionStatus, StepDirection, StoreJobKind, StoreJobStatus, StoreJobStepStatus, StoreKind } from '@/lib/api/types'
import { activityEvent, job } from '../../support/factories'

const sync = { JobId: 48213, JobKind: StoreJobKind.SyncApps, Store: StoreKind.Apple, Attempt: 1 }

function run(...actions: HatAction[]): HatState {
  return actions.reduce(hatReducer, initialHatState)
}

function step(overrides: Partial<ActivityEvent> = {}): ActivityEvent {
  return activityEvent({
    ...sync,
    Type: ActivityEventType.JobStep,
    JobStatus: StoreJobStatus.Running,
    StepOrder: 1,
    StepName: 'apps.list',
    Direction: StepDirection.Outbound,
    StepStatus: StoreJobStepStatus.Running,
    ...overrides,
  })
}

const queued = activityEvent({ ...sync, Type: ActivityEventType.JobQueued, JobStatus: StoreJobStatus.Queued })

describe('hatReducer', () => {
  it('opens a job on job.queued and lists it in the feed', () => {
    const state = run(queued)

    expect(state.jobs[48213]).toMatchObject({ status: StoreJobStatus.Queued, store: StoreKind.Apple, kind: StoreJobKind.SyncApps })
    expect(state.feed[0]).toMatchObject({ kind: 'event', name: 'job.queued', jobKind: StoreJobKind.SyncApps })
    expect(activeJobCount(state)).toBe(1)
  })

  it('moves a lane while an outbound step runs, with its progress', () => {
    const state = run(queued, step({ StepName: 'builds.list', StepOrder: 3, Progress: 2, Total: 5 }))
    const apple = lanesOf(state).find((lane) => lane.store === StoreKind.Apple)

    expect(apple).toMatchObject({ moving: true, failed: false })
    expect(apple?.jobs[0]).toMatchObject({ stepName: 'builds.list', progress: 2, total: 5, status: StoreJobStatus.Running })
    expect(state.feed[0]).toMatchObject({ kind: 'out', name: 'builds.list' })
  })

  it('adds a step to the feed when it starts and when it ends, not on every progress tick', () => {
    const state = run(
      step({ StepOrder: 3, Progress: 1, Total: 5 }),
      step({ StepOrder: 3, Progress: 2, Total: 5 }),
      step({ StepOrder: 3, Progress: 5, Total: 5, StepStatus: StoreJobStepStatus.Succeeded }),
    )

    expect(state.feed).toHaveLength(2)
    expect(state.jobs[48213].progress).toBe(5)
  })

  it('marks an internal step as an event and a failed one as an error', () => {
    const internal = run(step({ StepName: 'apps.save', Direction: StepDirection.Internal, StepStatus: StoreJobStepStatus.Succeeded }))
    const failed = run(step({ StepStatus: StoreJobStepStatus.Failed, Message: '503' }))

    expect(internal.feed[0].kind).toBe('event')
    expect(failed.feed[0]).toMatchObject({ kind: 'error', detail: '503' })
  })

  it('reads job.progress back to Queued with a reason as a retry the lane shows in red', () => {
    const state = run(
      queued,
      step(),
      activityEvent({ ...sync, Type: ActivityEventType.JobProgress, JobStatus: StoreJobStatus.Queued, Message: '503 Service Unavailable' }),
    )
    const apple = lanesOf(state).find((lane) => lane.store === StoreKind.Apple)

    expect(state.jobs[48213]).toMatchObject({ retrying: true, message: '503 Service Unavailable', status: StoreJobStatus.Queued })
    expect(apple).toMatchObject({ failed: true, moving: false })
    expect(state.feed[0].kind).toBe('error')
  })

  it('clears the retry when the next attempt starts', () => {
    const state = run(
      activityEvent({ ...sync, Type: ActivityEventType.JobProgress, JobStatus: StoreJobStatus.Queued, Message: '503' }),
      activityEvent({ ...sync, Attempt: 2, Type: ActivityEventType.JobProgress, JobStatus: StoreJobStatus.Running }),
    )

    expect(state.jobs[48213]).toMatchObject({ retrying: false, attempt: 2, status: StoreJobStatus.Running, message: null })
  })

  it.each([
    [ActivityEventType.JobSucceeded, StoreJobStatus.Succeeded, 'event'],
    [ActivityEventType.JobFailed, StoreJobStatus.Failed, 'error'],
    [ActivityEventType.JobCancelled, StoreJobStatus.Cancelled, 'event'],
  ])('ends the job on %s', (type, status, kind) => {
    const state = run(queued, step(), activityEvent({ ...sync, Type: type, JobStatus: status, Message: 'done' }))
    const apple = lanesOf(state).find((lane) => lane.store === StoreKind.Apple)

    expect(state.jobs[48213].status).toBe(status)
    expect(state.feed[0]).toMatchObject({ kind, name: type, detail: 'done' })
    expect(apple?.jobs).toHaveLength(0)
    expect(apple?.last?.id).toBe(48213)
    expect(activeJobCount(state)).toBe(0)
  })

  it('takes the final state from the type when the event has no status', () => {
    const state = run(activityEvent({ ...sync, Type: ActivityEventType.JobCancelled }))

    expect(state.jobs[48213].status).toBe(StoreJobStatus.Cancelled)
  })

  it('lists connection checks and app syncs as incoming events', () => {
    const state = run(
      activityEvent({
        Type: ActivityEventType.ConnectionVerified,
        ConnectionId: 12,
        Store: StoreKind.Google,
        ConnectionStatus: ConnectionStatus.Rejected,
      }),
      activityEvent({ Type: ActivityEventType.AppSynced, AppId: 101, Store: StoreKind.Apple }),
    )

    expect(state.feed.map((item) => [item.name, item.kind])).toEqual([
      ['app.synced', 'in'],
      ['connection.verified', 'in'],
    ])
    expect(state.feed[1].connectionStatus).toBe(ConnectionStatus.Rejected)
  })

  it('ignores a type it does not know and job events without a job id', () => {
    const before = run(queued)

    expect(hatReducer(before, activityEvent({ Type: 'plan.created' }))).toBe(before)
    expect(hatReducer(before, activityEvent({ Type: ActivityEventType.JobStep }))).toBe(before)
  })

  it('keeps the feed to the last 50 events', () => {
    const events = Array.from({ length: MAX_FEED + 5 }, (_, index) =>
      activityEvent({ ...sync, JobId: index + 1, Type: ActivityEventType.JobQueued }),
    )

    expect(run(...events).feed).toHaveLength(MAX_FEED)
  })

  it('seeds lanes from the job list, and an event that came first wins over the snapshot', () => {
    const state = run(step({ JobId: 2 }), {
      Type: 'seed',
      Jobs: [
        job({ Id: 1, Status: StoreJobStatus.Running, Store: StoreKind.Google, RequestedByUserName: 'ece@kuzeylab.dev' }),
        job({ Id: 2, Status: StoreJobStatus.Queued, MaxAttempts: 5 }),
      ],
    })

    expect(state.jobs[1]).toMatchObject({ status: StoreJobStatus.Running, store: StoreKind.Google, requestedBy: 'ece@kuzeylab.dev' })
    expect(state.jobs[2]).toMatchObject({ status: StoreJobStatus.Running, maxAttempts: 5, stepName: 'apps.list' })
    expect(jobsOf(state).map((item) => item.id)).toContain(1)
  })
})

const T0 = Date.parse('2026-11-18T09:12:00Z')
const apple = (state: HatState, now = T0) => tracksOf(state, now).find((track) => track.store === StoreKind.Apple)!

describe('tokens', () => {
  it('stands a token where its real step puts it: queued at storeasy, out by progress, back on the return, done at the gate', () => {
    const queuedJob = run(queued).jobs[48213]
    const outbound = run(queued, step({ StepOrder: 3, Progress: 1, Total: 4 })).jobs[48213]
    const further = run(queued, step({ StepOrder: 3, Progress: 3, Total: 4 })).jobs[48213]
    const answered = run(step({ StepStatus: StoreJobStepStatus.Succeeded })).jobs[48213]
    const inbound = run(step({ Direction: StepDirection.Inbound, StepOrder: 4, Progress: 3, Total: 4 })).jobs[48213]
    const internal = run(step({ Direction: StepDirection.Internal, StepOrder: 2 })).jobs[48213]
    const done = run(step(), activityEvent({ ...sync, Type: ActivityEventType.JobSucceeded })).jobs[48213]

    expect(tokenPosition(queuedJob)).toBeCloseTo(0.04)
    expect(tokenPosition(outbound)).toBeCloseTo(0.35)
    expect(tokenPosition(further)).toBeCloseTo(0.65)
    expect(tokenPosition(answered)).toBeCloseTo(0.85)
    expect(tokenPosition(inbound)).toBeCloseTo(0.35)
    expect(tokenPosition(internal)).toBeCloseTo(0.14)
    expect(tokenPosition(done)).toBe(1)
  })

  it('keeps the steps a job went through as its stations, in order', () => {
    const state = run(
      step({ StepOrder: 1, StepStatus: StoreJobStepStatus.Succeeded }),
      step({ StepOrder: 2, StepName: 'apps.save', Direction: StepDirection.Internal, StepStatus: StoreJobStepStatus.Succeeded }),
      step({ StepOrder: 3, StepName: 'builds.list' }),
      step({ StepOrder: 3, StepName: 'builds.list', Progress: 2, Total: 4 }),
    )

    expect(state.jobs[48213].steps.map((pip) => [pip.order, pip.status])).toEqual([
      [1, StoreJobStepStatus.Succeeded],
      [2, StoreJobStepStatus.Succeeded],
      [3, StoreJobStepStatus.Running],
    ])
  })

  it('marks an outbound step without progress as in flight, and a failed step as stopped where it failed', () => {
    const flying = apple(run(queued, step()))
    const failed = apple(run(queued, step({ StepOrder: 3, Progress: 2, Total: 4, StepStatus: StoreJobStepStatus.Failed, Message: '429' })))

    expect(flying.tokens[0]).toMatchObject({ phase: 'out', indeterminate: true })
    expect(failed.tokens[0]).toMatchObject({ phase: 'failed', x: 0.5 })
    expect(failed.failed).toBe(true)
  })

  it('lets a finished job glide into the gate and leave, and keeps a failed one on the line longer', () => {
    const ok = run(queued, step(), activityEvent({ ...sync, Type: ActivityEventType.JobSucceeded, ReceivedAt: T0 }))
    const bad = run(queued, step(), activityEvent({ ...sync, Type: ActivityEventType.JobFailed, ReceivedAt: T0, Message: '403' }))

    expect(apple(ok, T0 + 1000).tokens[0]).toMatchObject({ phase: 'done', x: 1 })
    expect(apple(ok, T0 + DONE_LINGER_MS + 1).tokens).toHaveLength(0)
    expect(apple(bad, T0 + DONE_LINGER_MS + 1).tokens[0].phase).toBe('failed')
    expect(nextExpiry(ok, T0 + 1000)).toBe(T0 + DONE_LINGER_MS)
  })

  it('draws at most a few tokens per line and counts the rest', () => {
    const events = Array.from({ length: MAX_TOKENS + 2 }, (_, index) =>
      activityEvent({ ...sync, JobId: 100 + index, Type: ActivityEventType.JobQueued }),
    )
    const track = apple(run(...events))

    expect(track.tokens).toHaveLength(MAX_TOKENS)
    expect(track.hidden).toBe(2)
  })
})

describe('inbound pulses and package intake', () => {
  it('turns a store event into a pulse on its store, labelled with the new state, and lists it as incoming', () => {
    const state = run(
      activityEvent({
        Type: InboundEventType.WebhookReceived,
        Store: StoreKind.Apple,
        Message: 'WAITING_FOR_REVIEW -> IN_REVIEW',
        ReceivedAt: T0,
      }),
    )

    expect(apple(state, T0 + 100).pulses[0]).toMatchObject({ kind: 'webhook', label: 'IN_REVIEW', count: 1 })
    expect(state.feed[0]).toMatchObject({ kind: 'in', name: 'webhook.received', store: StoreKind.Apple })
    expect(apple(state, T0 + PULSE_MS + 1).pulses).toHaveLength(0)
  })

  it('folds a burst into one pulse with a count, and puts a version state and a rejection on the App Store line', () => {
    const burst = run(
      ...[0, 300, 600].map((offset) =>
        activityEvent({
          Type: InboundEventType.WebhookReceived,
          Store: StoreKind.Google,
          Message: 'SUBSCRIPTION_RENEWED',
          ReceivedAt: T0 + offset,
        }),
      ),
    )
    const review = run(
      activityEvent({
        Type: InboundEventType.VersionState,
        AppId: 101,
        Message: 'Pusula 2.3.1: WAITING_FOR_REVIEW -> IN_REVIEW',
        ReceivedAt: T0,
      }),
      activityEvent({ Type: InboundEventType.RejectionOpened, AppId: 103, Message: 'Mola 1.4.0: REJECTED', ReceivedAt: T0 + 500 }),
    )
    const google = tracksOf(burst, T0 + 700).find((track) => track.store === StoreKind.Google)!

    expect(google.pulses).toHaveLength(1)
    expect(google.pulses[0]).toMatchObject({ count: 3, label: 'SUBSCRIPTION_RENEWED' })
    expect(apple(review, T0 + 600).pulses[0]).toMatchObject({ kind: 'rejection', count: 2, appId: null })
    expect(review.feed[0]).toMatchObject({ kind: 'error', name: 'rejection.opened', appId: 103 })
  })

  it('reads the state a store message names', () => {
    expect(pulseLabel('PROCESSING -> COMPLETE')).toBe('COMPLETE')
    expect(pulseLabel('Mola 1.4.0: REJECTED')).toBe('REJECTED')
    expect(pulseLabel('SUBSCRIPTION_PURCHASED')).toBe('SUBSCRIPTION_PURCHASED')
    expect(pulseLabel('Halka: 2 new reviews.')).toBeNull()
    expect(pulseLabel(null)).toBeNull()
  })

  it('follows a package upload session from its first state to its end', () => {
    const intake = (status: string) =>
      activityEvent({ Type: InboundEventType.ArtifactProgress, AppId: 101, Store: StoreKind.Apple, Message: `Session 77: ${status}` })
    const uploading = run(intake('Uploading'), intake('Parsing'))
    const delivered = run(intake('Uploading'), intake('Parsing'), intake('Delivered'))

    expect(readIntakeMessage('Session 12: Checking')).toEqual({ sessionId: 12, status: 'Checking' })
    expect(activeIntakes(uploading).map((item) => item.status)).toEqual(['Parsing'])
    expect(uploading.feed).toHaveLength(1)
    expect(activeIntakes(delivered)).toHaveLength(0)
    expect(delivered.feed[0]).toMatchObject({ kind: 'in', name: 'artifact.progress', detail: 'Delivered' })
  })
})
