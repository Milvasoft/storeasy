import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
  type IRetryPolicy,
  type RetryContext,
} from '@microsoft/signalr'
import { ACTIVITY_HUB_URL } from '@/config'
import { ensureRefresh } from '@/lib/api/http'
import { pascalizeKeys } from '@/lib/api/pascalize'
import { tokenStore } from '@/lib/api/tokenStore'
import {
  ACTIVITY_CLIENT_METHOD,
  createEmitter,
  JOIN_ORGANIZATION_METHOD,
  RecordEventType,
  type ActivityEvent,
  type ActivitySource,
  type ConnectionInfo,
} from './events'

// Design 18.12: retry after 0, 2, 10 and 30 s, then every 30 s; give up after 5 minutes and offer "Reconnect".
const RETRY_DELAYS_MS = [0, 2_000, 10_000, 30_000]
const GIVE_UP_AFTER_MS = 5 * 60_000

export const activityRetryPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds(context: RetryContext): number | null {
    if (context.elapsedMilliseconds >= GIVE_UP_AFTER_MS) {
      return null
    }
    return RETRY_DELAYS_MS[context.previousRetryCount] ?? 30_000
  },
}

/** The hub's payload as the app reads it: PascalCase keys, numeric enums kept as they are. */
export function readActivityEvent(payload: unknown): ActivityEvent | null {
  const event = pascalizeKeys(payload) as ActivityEvent | null
  return event && typeof event === 'object' && typeof event.Type === 'string' ? event : null
}

export interface SignalRSourceOptions {
  // Tests build the connection themselves; the app uses the real builder.
  build?: (url: string, accessTokenFactory: () => string) => HubConnection
}

function buildHub(url: string, accessTokenFactory: () => string, retry: IRetryPolicy): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(url, { accessTokenFactory })
    .withAutomaticReconnect(retry)
    .configureLogging(LogLevel.None)
    .build()
}

/**
 * The live source: a SignalR connection to /hubs/activity with the current access token (sent as access_token by the
 * client for websockets). After every (re)connect the source joins the organization's group; the server checks the
 * membership and answers false for a stranger. A 401 while connecting renews the token through the same single-flight
 * refresh the HTTP layer uses, then tries once more.
 */
export function createSignalRSource(organizationId: number, options: SignalRSourceOptions = {}): ActivitySource {
  const events = createEmitter<ActivityEvent>()
  const connection = createEmitter<ConnectionInfo>()
  let hub: HubConnection | null = null
  let attempt = 0

  const retry: IRetryPolicy = {
    nextRetryDelayInMilliseconds: (context) => {
      const delay = activityRetryPolicy.nextRetryDelayInMilliseconds(context)
      attempt = context.previousRetryCount + 1
      connection.emit({
        state: delay === null ? 'disconnected' : 'reconnecting',
        attempt,
        nextRetryAt: delay === null ? null : Date.now() + delay,
      })
      return delay
    },
  }

  const tokenFactory = () => tokenStore.get()?.accessToken ?? ''

  // Groups do not survive a reconnect (a new connection id), so the join runs after every connect.
  const join = async (current: HubConnection): Promise<void> => {
    const joined = await current.invoke<boolean>(JOIN_ORGANIZATION_METHOD, organizationId)
    connection.emit(joined ? { state: 'connected' } : { state: 'disconnected', refused: true })
  }

  const connect = async (renewed = false): Promise<void> => {
    const current = options.build ? options.build(ACTIVITY_HUB_URL, tokenFactory) : buildHub(ACTIVITY_HUB_URL, tokenFactory, retry)
    hub = current
    current.on(ACTIVITY_CLIENT_METHOD, (payload: unknown) => {
      const event = readActivityEvent(payload)
      // The group is the organization's, but a stray event of another organization never reaches its state. A
      // notification comes from the user's own group and belongs to the user, whatever organization it is about.
      if (event && (event.OrganizationId === organizationId || event.Type === RecordEventType.NotificationCreated)) {
        events.emit(event)
      }
    })
    current.onreconnecting(() => connection.emit({ state: 'reconnecting', attempt, nextRetryAt: null }))
    current.onreconnected(() => {
      attempt = 0
      void join(current).catch(() => connection.emit({ state: 'disconnected' }))
    })
    current.onclose(() => connection.emit({ state: 'disconnected' }))
    connection.emit({ state: 'connecting' })
    try {
      await current.start()
    } catch (error) {
      const unauthorized = /401|unauthori[sz]ed/i.test(String((error as Error)?.message ?? error))
      if (unauthorized && !renewed && (await ensureRefresh())) {
        return connect(true)
      }
      connection.emit({ state: 'disconnected' })
      throw error
    }
    try {
      await join(current)
    } catch (error) {
      connection.emit({ state: 'disconnected' })
      throw error
    }
  }

  return {
    start: () => connect(),
    stop: async () => {
      if (hub && hub.state !== HubConnectionState.Disconnected) {
        await hub.stop()
      }
      hub = null
    },
    subscribe: events.subscribe,
    onConnection: connection.subscribe,
  }
}
