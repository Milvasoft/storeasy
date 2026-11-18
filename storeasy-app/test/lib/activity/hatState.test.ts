import { describe, expect, it } from 'vitest'
import { ActivityEventType, type ActivityEvent } from '@/lib/activity/events'
import {
  activeJobCount,
  hatReducer,
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

    expect(hatReducer(before, activityEvent({ Type: 'review.received' }))).toBe(before)
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
