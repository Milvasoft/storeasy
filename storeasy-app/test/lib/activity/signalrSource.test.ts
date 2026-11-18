import { HubConnectionState, type HubConnection } from '@microsoft/signalr'
import { describe, expect, it, vi } from 'vitest'
import { ACTIVITY_CLIENT_METHOD, JOIN_ORGANIZATION_METHOD, type ActivityEvent, type ConnectionInfo } from '@/lib/activity/events'
import { activityRetryPolicy, createSignalRSource, readActivityEvent } from '@/lib/activity/signalrSource'
import { ensureRefresh } from '@/lib/api/http'
import { StoreJobKind, StoreJobStatus, StoreKind } from '@/lib/api/types'

vi.mock(import('@/lib/api/http'), async (original) => ({ ...(await original()), ensureRefresh: vi.fn() }))

// A hub connection without a network: the test drives its callbacks and answers JoinOrganization.
function fakeHub({ joined = true, startError }: { joined?: boolean; startError?: Error } = {}) {
  const handlers = new Map<string, (payload: unknown) => void>()
  let reconnected: (() => void) | null = null
  const hub = {
    state: HubConnectionState.Disconnected,
    on: vi.fn((name: string, handler: (payload: unknown) => void) => handlers.set(name, handler)),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn((handler: () => void) => {
      reconnected = handler
    }),
    onclose: vi.fn(),
    start: vi.fn(async () => {
      if (startError) {
        throw startError
      }
      hub.state = HubConnectionState.Connected
    }),
    stop: vi.fn(async () => {
      hub.state = HubConnectionState.Disconnected
    }),
    invoke: vi.fn(async () => joined),
  }
  return {
    hub,
    connection: hub as unknown as HubConnection,
    send: (payload: unknown) => handlers.get(ACTIVITY_CLIENT_METHOD)?.(payload),
    reconnect: () => reconnected?.(),
  }
}

// The hub's wire format: SignalR's default JSON protocol, camelCase keys and enums as numbers.
const wireEvent = {
  type: 'job.step',
  organizationId: 7,
  jobId: 48213,
  jobKind: 1,
  jobStatus: 2,
  attempt: 1,
  store: 1,
  appId: null,
  stepOrder: 3,
  stepName: 'builds.list',
  stepStatus: 1,
  direction: 1,
  progress: 2,
  total: 5,
  message: null,
  date: '2026-11-18T09:12:00Z',
}

function start(fake: ReturnType<typeof fakeHub>, organizationId = 7) {
  const source = createSignalRSource(organizationId, { build: () => fake.connection })
  const events: ActivityEvent[] = []
  const states: ConnectionInfo[] = []
  source.subscribe((event) => events.push(event))
  source.onConnection((info) => states.push(info))
  return { source, events, states }
}

describe('signalr activity source', () => {
  it('joins the organization after connecting and reports connected', async () => {
    const fake = fakeHub()
    const { source, states } = start(fake)

    await source.start()

    expect(fake.hub.invoke).toHaveBeenCalledWith(JOIN_ORGANIZATION_METHOD, 7)
    expect(fake.hub.on).toHaveBeenCalledWith(ACTIVITY_CLIENT_METHOD, expect.any(Function))
    expect(states.map((info) => info.state)).toEqual(['connecting', 'connected'])
  })

  it('reports a refused join when the server says the caller is not a member', async () => {
    const fake = fakeHub({ joined: false })
    const { source, states } = start(fake)

    await source.start()

    expect(states.at(-1)).toEqual({ state: 'disconnected', refused: true })
  })

  it('reads camelCase payloads with numeric enums and drops events of another organization', async () => {
    const fake = fakeHub()
    const { source, events } = start(fake)
    await source.start()

    fake.send(wireEvent)
    fake.send({ ...wireEvent, organizationId: 8 })
    fake.send('not an event')

    expect(events).toHaveLength(1)
    expect(events[0]).toMatchObject({
      Type: 'job.step',
      JobKind: StoreJobKind.SyncApps,
      JobStatus: StoreJobStatus.Running,
      Store: StoreKind.Apple,
      StepName: 'builds.list',
      Progress: 2,
      Total: 5,
    })
  })

  it('joins again after an automatic reconnect, because groups do not survive it', async () => {
    const fake = fakeHub()
    const { source } = start(fake)
    await source.start()

    fake.reconnect()

    await vi.waitFor(() => expect(fake.hub.invoke).toHaveBeenCalledTimes(2))
  })

  it('renews the token once and connects again after a 401', async () => {
    vi.mocked(ensureRefresh).mockResolvedValueOnce(true)
    const failing = fakeHub({
      startError: new Error('Failed to complete negotiation with the server: Error: Unauthorized: Status code 401'),
    })
    const working = fakeHub()
    const hubs = [failing.connection, working.connection]
    const source = createSignalRSource(7, { build: () => hubs.shift()! })

    await source.start()

    expect(ensureRefresh).toHaveBeenCalledTimes(1)
    expect(working.hub.invoke).toHaveBeenCalledWith(JOIN_ORGANIZATION_METHOD, 7)
  })

  it('gives up with disconnected when the hub cannot be reached', async () => {
    const fake = fakeHub({ startError: new Error('Failed to fetch') })
    const { source, states } = start(fake)

    await expect(source.start()).rejects.toThrow('Failed to fetch')
    expect(states.at(-1)).toEqual({ state: 'disconnected' })
  })

  it('stops a running connection', async () => {
    const fake = fakeHub()
    const { source } = start(fake)
    await source.start()

    await source.stop()

    expect(fake.hub.stop).toHaveBeenCalled()
  })
})

describe('activity retry policy', () => {
  it('retries after 0, 2, 10 and 30 s, then every 30 s, and gives up after 5 minutes', () => {
    const delay = (previousRetryCount: number, elapsedMilliseconds = 0) =>
      activityRetryPolicy.nextRetryDelayInMilliseconds({ previousRetryCount, elapsedMilliseconds, retryReason: new Error('x') })

    expect([0, 1, 2, 3, 4].map((count) => delay(count))).toEqual([0, 2_000, 10_000, 30_000, 30_000])
    expect(delay(9, 5 * 60_000)).toBeNull()
  })
})

describe('readActivityEvent', () => {
  it('answers null for a payload without a type', () => {
    expect(readActivityEvent({ organizationId: 7 })).toBeNull()
    expect(readActivityEvent(null)).toBeNull()
  })
})
