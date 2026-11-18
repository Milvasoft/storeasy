import { createContext, useCallback, useContext, useEffect, useMemo, useReducer, useRef, useState, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { MOCK_API } from '@/config'
import { appsApi, jobsApi } from '@/lib/api/endpoints'
import { SortType, type ListRequest } from '@/lib/api/list'
import { StoreJobKind, StoreJobStatus } from '@/lib/api/types'
import { useAuth } from '@/lib/auth/AuthContext'
import { getDictionary } from '@/lib/i18n/dictionaries'
import { interpolate } from '@/lib/i18n/interpolate'
import { jobKindLabel } from '@/lib/jobs/labels'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import {
  ActivityEventType,
  createEmitter,
  JOB_FINISHED_TYPES,
  type ActivityEvent,
  type ActivitySource,
  type ConnectionInfo,
} from './events'
import { hatReducer, initialHatState, type HatAction, type HatState } from './hatState'
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

// The jobs the live view starts with: the newest ones, so a job queued before the page opened has its lane and the idle
// lanes show their last finished job.
export const SEED_REQUEST: ListRequest = { PageNumber: 1, RowCount: 50, Sorting: { SortBy: 'Id', Type: SortType.Desc } }
// App names for the lanes, the feed and the toasts (the events carry only the app id).
export const APP_NAMES_REQUEST: ListRequest = { PageNumber: 1, RowCount: 100, Sorting: { SortBy: 'Name', Type: SortType.Asc } }

interface ActivityContextValue {
  state: HatState
  connection: ConnectionInfo
  // "Freeze the stream" stops only the screen; jobs go on and events keep arriving underneath (design 11.1).
  frozen: boolean
  setFrozen: (frozen: boolean) => void
  reconnect: () => void
  appName: (appId: number | null | undefined) => string | null
  // Jobs this screen started (a sync, an added app): their end is announced with a toast wherever the user is.
  watchJobs: (jobIds: number[]) => void
  // Every live event as it arrives (frozen or not), for screens that refresh on their own event types.
  subscribe: (listener: (event: ActivityEvent) => void) => () => void
}

const ActivityContext = createContext<ActivityContextValue | null>(null)

function reducer(state: HatState, action: HatAction | 'reset'): HatState {
  return action === 'reset' ? initialHatState : hatReducer(state, action)
}

/** One live connection for the whole shell: the bar's live pill, the menu counter, job toasts and the Hat page share it. */
export function ActivityProvider({ children }: { children: ReactNode }) {
  const sourceFactory = useContext(ActivitySourceFactoryContext)
  const queryClient = useQueryClient()
  const { current } = useOrganization()
  const { user } = useAuth()
  const [state, dispatch] = useReducer(reducer, initialHatState)
  const [connection, setConnection] = useState<ConnectionInfo>({ state: 'idle' })
  const [frozen, setFrozenState] = useState(false)
  const [snapshot, setSnapshot] = useState<HatState | null>(null)
  const [generation, setGeneration] = useState(0)
  const stateRef = useRef(state)
  stateRef.current = state
  const userRef = useRef(user)
  userRef.current = user
  const watched = useRef(new Set<number>())
  const [listeners] = useState(() => createEmitter<ActivityEvent>())
  const organizationId = current?.Id ?? null

  const apps = useQuery({
    queryKey: queryKeys.apps.list(organizationId ?? 0, APP_NAMES_REQUEST),
    queryFn: () => appsApi.list(organizationId ?? 0, APP_NAMES_REQUEST),
    enabled: organizationId !== null,
    staleTime: 60_000,
  })
  const names = useMemo(() => new Map((apps.data?.Data ?? []).map((app) => [app.Id, app.Name])), [apps.data])
  const namesRef = useRef(names)
  namesRef.current = names
  const appName = useCallback((appId: number | null | undefined) => (appId ? (names.get(appId) ?? null) : null), [names])

  useEffect(() => {
    if (organizationId === null) {
      return
    }
    dispatch('reset')
    const mine = watched.current
    mine.clear()
    let active = true
    const source = sourceFactory(organizationId)

    const announce = (event: ActivityEvent) => {
      const jobId = event.JobId ?? 0
      const job = stateRef.current.jobs[jobId]
      const requested = mine.has(jobId) || (!!job?.requestedBy && job.requestedBy === userRef.current?.userName)
      if (!requested) {
        return
      }
      mine.delete(jobId)
      const t = getDictionary()
      const appId = event.AppId ?? job?.appId
      const subject = [appId ? namesRef.current.get(appId) : null, jobKindLabel(event.JobKind ?? job?.kind, t)].filter(Boolean).join(' · ')
      if (event.Type === ActivityEventType.JobFailed) {
        showToast(interpolate(t.activity.toastFailed, { subject }), 'error', event.Message ?? undefined)
      } else if (event.Type === ActivityEventType.JobSucceeded) {
        showToast(interpolate(t.activity.toastSucceeded, { subject }), 'success', event.Message ?? undefined)
      }
    }

    const offEvents = source.subscribe((event) => {
      if (JOB_FINISHED_TYPES.includes(event.Type)) {
        announce(event)
        void queryClient.invalidateQueries({ queryKey: queryKeys.jobs.all })
        // A finished sync changed the apps and builds.
        if (event.JobKind === StoreJobKind.SyncApps && event.JobStatus !== StoreJobStatus.Cancelled) {
          void queryClient.invalidateQueries({ queryKey: queryKeys.apps.all })
        }
      } else if (event.Type === ActivityEventType.JobQueued) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.jobs.all })
      } else if (event.Type === ActivityEventType.AppSynced) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.apps.all })
      } else if (event.Type === ActivityEventType.ConnectionVerified) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.connections.all })
      }
      dispatch(event)
      listeners.emit(event)
    })
    const offConnection = source.onConnection(setConnection)
    source.start().catch(() => {
      /* the source already reported 'disconnected'; the page offers "Reconnect" */
    })
    // The jobs already on the way when the stream opened; events that arrive meanwhile win over this snapshot.
    jobsApi
      .list(organizationId, SEED_REQUEST)
      .then((list) => {
        if (active) {
          dispatch({ Type: 'seed', Jobs: list.Data ?? [] })
        }
      })
      .catch(() => {
        /* the live view works without the snapshot; the job list page shows the error */
      })
    return () => {
      active = false
      offEvents()
      offConnection()
      void source.stop()
    }
  }, [organizationId, sourceFactory, generation, queryClient, listeners])

  const setFrozen = useCallback(
    (next: boolean) => {
      setFrozenState(next)
      setSnapshot(next ? state : null)
    },
    [state],
  )

  const reconnect = useCallback(() => setGeneration((value) => value + 1), [])

  const watchJobs = useCallback((jobIds: number[]) => {
    for (const id of jobIds) {
      watched.current.add(id)
    }
  }, [])

  const value = useMemo<ActivityContextValue>(
    () => ({
      state: frozen && snapshot ? snapshot : state,
      connection,
      frozen,
      setFrozen,
      reconnect,
      appName,
      watchJobs,
      subscribe: listeners.subscribe,
    }),
    [frozen, snapshot, state, connection, setFrozen, reconnect, appName, watchJobs, listeners],
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
