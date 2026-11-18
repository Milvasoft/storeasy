import { createContext, useCallback, useContext, useEffect, useMemo, useReducer, useRef, useState, type ReactNode } from 'react'
import { MOCK_API } from '@/config'
import { useAuth } from '@/lib/auth/AuthContext'
import { getDictionary } from '@/lib/i18n/dictionaries'
import { interpolate } from '@/lib/i18n/interpolate'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { ActivityEventName, JobStatus, type ActivityEvent, type ActivitySource, type ConnectionInfo } from './events'
import { hatReducer, initialHatState, type HatState } from './hatState'
import { createMockActivitySource } from './mockSource'
import { createSignalRSource } from './signalrSource'

export type ActivitySourceFactory = (organizationId: number) => ActivitySource

// The live hub; in development a hub that cannot be reached falls back to the demo stream (marked as such), production
// never shows made-up events.
export const defaultSourceFactory: ActivitySourceFactory = (organizationId) => {
  if (MOCK_API) {
    return createMockActivitySource()
  }
  const live = createSignalRSource(organizationId)
  if (!import.meta.env.DEV) {
    return live
  }
  const demo = createMockActivitySource()
  let current: ActivitySource = live
  return {
    start: async () => {
      try {
        await live.start()
      } catch {
        current = demo
        await demo.start()
      }
    },
    stop: async () => current.stop(),
    subscribe: (listener) => {
      const offLive = live.subscribe(listener)
      const offDemo = demo.subscribe(listener)
      return () => {
        offLive()
        offDemo()
      }
    },
    onConnection: (listener) => {
      const offLive = live.onConnection((info) => current === live && listener(info))
      const offDemo = demo.onConnection(listener)
      return () => {
        offLive()
        offDemo()
      }
    },
  }
}

// Which source the provider uses; tests (and a future replay view) put their own factory here.
export const ActivitySourceFactoryContext = createContext<ActivitySourceFactory>(defaultSourceFactory)

interface ActivityContextValue {
  state: HatState
  connection: ConnectionInfo
  // "Freeze the stream" stops only the screen; jobs go on and events keep arriving underneath (design 11.1).
  frozen: boolean
  setFrozen: (frozen: boolean) => void
  reconnect: () => void
}

const ActivityContext = createContext<ActivityContextValue | null>(null)

function reducer(state: HatState, event: ActivityEvent | 'reset'): HatState {
  return event === 'reset' ? initialHatState : hatReducer(state, event)
}

/** One live connection for the whole shell: the bar's live pill, the menu counter, job toasts and the Hat page share it. */
export function ActivityProvider({ children }: { children: ReactNode }) {
  const sourceFactory = useContext(ActivitySourceFactoryContext)
  const { current } = useOrganization()
  const { user } = useAuth()
  const [state, dispatch] = useReducer(reducer, initialHatState)
  const [connection, setConnection] = useState<ConnectionInfo>({ state: 'idle' })
  const [frozen, setFrozenState] = useState(false)
  const [snapshot, setSnapshot] = useState<HatState | null>(null)
  const [generation, setGeneration] = useState(0)
  const userRef = useRef(user)
  userRef.current = user
  const organizationId = current?.Id ?? null

  useEffect(() => {
    if (organizationId === null) {
      return
    }
    dispatch('reset')
    const source = sourceFactory(organizationId)
    const offEvents = source.subscribe((event) => {
      dispatch(event)
      // A finished job is announced to the person who asked for it, wherever they are (design 11.12).
      if (
        event.type === ActivityEventName.JobFinished &&
        event.data.RequestedByUserName &&
        event.data.RequestedByUserName === userRef.current?.userName
      ) {
        const t = getDictionary()
        const subject = [event.data.AppName, event.data.Title].filter(Boolean).join(' · ')
        if (event.data.Status === JobStatus.Failed) {
          showToast(interpolate(t.activity.toastFailed, { subject }), 'error', event.data.Message ?? undefined)
        } else if (event.data.Status === JobStatus.Succeeded) {
          showToast(interpolate(t.activity.toastSucceeded, { subject }), 'success')
        }
      }
    })
    const offConnection = source.onConnection(setConnection)
    source.start().catch(() => {
      /* the source already reported 'disconnected'; the page offers "Reconnect" */
    })
    return () => {
      offEvents()
      offConnection()
      void source.stop()
    }
  }, [organizationId, sourceFactory, generation])

  const setFrozen = useCallback(
    (next: boolean) => {
      setFrozenState(next)
      setSnapshot(next ? state : null)
    },
    [state],
  )

  const reconnect = useCallback(() => setGeneration((value) => value + 1), [])

  const value = useMemo<ActivityContextValue>(
    () => ({ state: frozen && snapshot ? snapshot : state, connection, frozen, setFrozen, reconnect }),
    [frozen, snapshot, state, connection, setFrozen, reconnect],
  )

  return <ActivityContext.Provider value={value}>{children}</ActivityContext.Provider>
}

export function useActivity(): ActivityContextValue {
  const ctx = useContext(ActivityContext)
  if (!ctx) {
    throw new Error('useActivity must be used within ActivityProvider')
  }
  return ctx
}
