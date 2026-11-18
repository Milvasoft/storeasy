import { describe, expect, it } from 'vitest'
import { ActivityEventName, JobStatus, StepDirection, StepStatus, type ActivityEvent } from '@/lib/activity/events'
import { activeJobCount, hatReducer, initialHatState, lanesOf, MAX_FEED, WEBHOOK_PULSE_MS, type HatState } from '@/lib/activity/hatState'
import { StoreKind } from '@/lib/api/types'

const DATE = '2026-11-18T14:02:41Z'

const created: ActivityEvent = {
  type: ActivityEventName.JobCreated,
  data: {
    JobId: 1,
    Kind: 'screenshots.apply',
    Title: 'iPad 13" · tr',
    AppName: 'Pusula',
    Store: StoreKind.Apple,
    StepCount: 7,
    Date: DATE,
  },
}

function step(
  status: StepStatus,
  direction: StepDirection = StepDirection.Outbound,
  extra: Partial<Extract<ActivityEvent, { type: 'job.step' }>['data']> = {},
): ActivityEvent {
  return {
    type: ActivityEventName.JobStep,
    data: {
      JobId: 1,
      Order: 4,
      Kind: 'asset.upload',
      Direction: direction,
      Status: status,
      Message: '04-takvim.png · parça 2/3',
      Date: DATE,
      ...extra,
    },
  }
}

function play(...events: ActivityEvent[]): HatState {
  return events.reduce((state, event) => hatReducer(state, event, 1_000), initialHatState)
}

describe('hat state', () => {
  it('puts a created job on its store lane, queued and not moving', () => {
    const lanes = lanesOf(play(created), 1_000)

    const apple = lanes.find((lane) => lane.store === StoreKind.Apple)!
    expect(apple.jobs).toHaveLength(1)
    expect(apple.moving).toBe(false)
    expect(lanes.find((lane) => lane.store === StoreKind.Google)!.jobs).toHaveLength(0)
  })

  it('moves the lane only while an outbound step is running', () => {
    const state = play(created, step(StepStatus.Running))

    const apple = lanesOf(state, 1_000).find((lane) => lane.store === StoreKind.Apple)!
    expect(apple.moving).toBe(true)
    expect(apple.jobs[0].message).toBe('04-takvim.png · parça 2/3')
    expect(state.feed[0]).toMatchObject({ kind: 'out', name: 'asset.upload', subject: 'Pusula · iPad 13" · tr' })
  })

  it('turns the lane red and stops it while a step waits for its retry', () => {
    const state = play(created, step(StepStatus.Retrying, StepDirection.Outbound, { Attempt: 2, MaxAttempt: 3 }))

    const apple = lanesOf(state, 1_000).find((lane) => lane.store === StoreKind.Apple)!
    expect(apple.failed).toBe(true)
    expect(apple.moving).toBe(false)
    expect(state.feed[0].kind).toBe('error')
  })

  it('takes a finished job off the active lane and keeps it as the last one', () => {
    const state = play(created, step(StepStatus.Running), {
      type: ActivityEventName.JobFinished,
      data: { JobId: 1, Status: JobStatus.Succeeded, Title: 'iPad 13" · tr', AppName: 'Pusula', Store: StoreKind.Apple, Date: DATE },
    })

    const apple = lanesOf(state, 1_000).find((lane) => lane.store === StoreKind.Apple)!
    expect(apple.jobs).toHaveLength(0)
    expect(apple.last?.status).toBe(JobStatus.Succeeded)
    expect(activeJobCount(state)).toBe(0)
  })

  it('pulses a webhook on its store for a few seconds only', () => {
    const state = play({
      type: ActivityEventName.WebhookReceived,
      data: {
        Store: StoreKind.Apple,
        EventType: 'X',
        AppName: 'Pusula 2.3',
        OldValue: 'WAITING_FOR_REVIEW',
        NewValue: 'IN_REVIEW',
        SignatureValid: true,
        Date: DATE,
      },
    })

    expect(lanesOf(state, 1_000)[0].webhookAt).toBe(1_000)
    expect(lanesOf(state, 1_000 + WEBHOOK_PULSE_MS + 1)[0].webhookAt).toBeNull()
    expect(state.feed[0].detail).toBe('WAITING_FOR_REVIEW → IN_REVIEW')
  })

  it('counts a CI upload until its last byte arrives', () => {
    const progress = (bytes: number): ActivityEvent => ({
      type: ActivityEventName.ArtifactProgress,
      data: { SessionId: 's', AppName: 'Halka', Source: 'GitHub Actions', FileName: 'halka.ipa', Bytes: bytes, Total: 10, Date: DATE },
    })

    expect(activeJobCount(play(progress(3)))).toBe(1)
    expect(activeJobCount(play(progress(3), progress(10)))).toBe(0)
  })

  it('keeps the newest events first and at most 50 of them', () => {
    const events = Array.from({ length: MAX_FEED + 5 }, (_, index) => step(StepStatus.Running, StepDirection.Outbound, { Order: index }))

    const state = play(created, ...events)

    expect(state.feed).toHaveLength(MAX_FEED)
  })

  it('shows a step of a job it never saw created in the feed, without a lane', () => {
    const state = play(step(StepStatus.Running))

    expect(state.feed).toHaveLength(1)
    expect(lanesOf(state, 1_000).every((lane) => lane.jobs.length === 0)).toBe(true)
  })
})
