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
import { ACTIVITY_EVENT_NAMES, createEmitter, type ActivityEvent, type ActivitySource, type ConnectionInfo } from './events'

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

// The organization group is chosen with a query parameter on the hub URL; the token goes as access_token (K5: the
// browser cannot set headers on a WebSocket). Both are the planned hub contract.
export function activityHubUrl(organizationId: number): string {
  return `${ACTIVITY_HUB_URL}?organizationId=${encodeURIComponent(String(organizationId))}`
}

/**
 * The live source: a SignalR connection to /hubs/activity with the current access token. A 401 while connecting renews
 * the token through the same single-flight refresh the HTTP layer uses, then tries once more.
 */
export function createSignalRSource(organizationId: number): ActivitySource {
  const events = createEmitter<ActivityEvent>()
  const connection = createEmitter<ConnectionInfo>()
  let hub: HubConnection | null = null
  let attempt = 0

  const build = () =>
    new HubConnectionBuilder()
      .withUrl(activityHubUrl(organizationId), { accessTokenFactory: () => tokenStore.get()?.accessToken ?? '' })
      .withAutomaticReconnect({
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
      })
      .configureLogging(LogLevel.None)
      .build()

  const connect = async (renewed = false): Promise<void> => {
    hub = build()
    for (const name of ACTIVITY_EVENT_NAMES) {
      hub.on(name, (payload: unknown) => events.emit({ type: name, data: pascalizeKeys(payload) } as ActivityEvent))
    }
    hub.onreconnecting(() => connection.emit({ state: 'reconnecting', attempt, nextRetryAt: null }))
    hub.onreconnected(() => {
      attempt = 0
      connection.emit({ state: 'connected' })
    })
    hub.onclose(() => connection.emit({ state: 'disconnected' }))
    connection.emit({ state: 'connecting' })
    try {
      await hub.start()
      connection.emit({ state: 'connected' })
    } catch (error) {
      const unauthorized = /401|unauthori[sz]ed/i.test(String((error as Error)?.message ?? error))
      if (unauthorized && !renewed && (await ensureRefresh())) {
        return connect(true)
      }
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
