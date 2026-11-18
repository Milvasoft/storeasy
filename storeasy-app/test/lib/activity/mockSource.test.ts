import { afterEach, describe, expect, it, vi } from 'vitest'
import { ActivityEventType, type ActivityEvent, type ConnectionInfo } from '@/lib/activity/events'
import { hatReducer, initialHatState } from '@/lib/activity/hatState'
import { createMockActivitySource } from '@/lib/activity/mockSource'
import { ApiError } from '@/lib/api/http'
import { shouldRetry } from '@/lib/query/queryClient'

afterEach(() => {
  vi.useRealTimers()
})

describe('demo activity source', () => {
  it('says it is the demo and plays the real event types in a loop, with fresh job ids each round', async () => {
    vi.useFakeTimers()
    const source = createMockActivitySource({ intervalMs: 10 })
    const events: ActivityEvent[] = []
    const states: ConnectionInfo[] = []
    source.subscribe((event) => events.push(event))
    source.onConnection((info) => states.push(info))

    await source.start()
    vi.advanceTimersByTime(10 * 20)
    await source.stop()
    const count = events.length
    vi.advanceTimersByTime(100)

    expect(states).toEqual([{ state: 'connected', demo: true }])
    expect(events.length).toBe(count)
    const types = new Set(events.map((event) => event.Type))
    for (const type of Object.values(ActivityEventType).filter((value) => value !== ActivityEventType.JobCancelled)) {
      expect(types).toContain(type)
    }
    expect(new Set(events.filter((event) => event.Type === ActivityEventType.JobQueued).map((event) => event.JobId)).size).toBeGreaterThan(
      2,
    )
    // Every scripted event is one the live view understands.
    const state = events.reduce(hatReducer, initialHatState)
    expect(state.feed.length).toBeGreaterThan(10)
  })
})

describe('shouldRetry', () => {
  it('retries a network or server failure once and never a refusal', () => {
    expect(shouldRetry(0, new Error('Failed to fetch'))).toBe(true)
    expect(shouldRetry(1, new Error('Failed to fetch'))).toBe(false)
    expect(shouldRetry(0, new ApiError('x', 500))).toBe(true)
    expect(shouldRetry(0, new ApiError('x', 409))).toBe(false)
    expect(shouldRetry(0, new ApiError('x', 503, [], ['FeatureDisabled']))).toBe(false)
  })
})
