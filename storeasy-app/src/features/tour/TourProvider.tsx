import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { appsApi, authApi } from '@/lib/api/endpoints'
import { useAuth } from '@/lib/auth/AuthContext'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { readJson, StorageKey, writeJson } from '@/lib/utils/storage'
import { findTarget, resolveSteps, TOUR_STEPS, type TourStep, type TourStop } from './steps'
import { TourInvitation } from './TourInvitation'
import { TourOverlay } from './TourOverlay'

// How long a step waits for its anchor to appear (a lazy page, its first fetch) before it is skipped, and how long an
// anchor that exists but is hidden (the menu on a phone) is given to come on screen before the card goes to the middle.
export const TARGET_TIMEOUT_MS = 2000
export const HIDDEN_GRACE_MS = 500
const POLL_MS = 80

interface TourContextValue {
  /** Starts the tour from its first step (the Settings button, the invitation). */
  start: () => void
  running: boolean
}

const TourContext = createContext<TourContextValue | null>(null)

/** The guided tour, or null outside the signed in shell. */
export function useTour(): TourContextValue | null {
  return useContext(TourContext)
}

// This browser's record of the users who answered the invitation: it stands in when the API could not store the answer
// (offline, an older API) and hides the invitation at once, before account/detail is read again.
function seenHere(userId: number): boolean {
  const ids = readJson<number[]>(StorageKey.TourPromptSeen)
  return Array.isArray(ids) && ids.includes(userId)
}

function rememberSeen(userId: number): void {
  const ids = readJson<number[]>(StorageKey.TourPromptSeen)
  const list = Array.isArray(ids) ? ids : []
  if (!list.includes(userId)) {
    writeJson(StorageKey.TourPromptSeen, [...list, userId])
  }
}

export interface ActiveStop {
  stop: TourStop
  // The spotlighted element; null shows the card in the middle of the screen.
  element: HTMLElement | null
}

interface RunState {
  stops: TourStop[]
  index: number
  // The step is waiting for its page and anchor.
  direction: 1 | -1
}

export function TourProvider({
  children,
  steps = TOUR_STEPS,
  targetTimeoutMs = TARGET_TIMEOUT_MS,
  hiddenGraceMs = HIDDEN_GRACE_MS,
}: {
  children: ReactNode
  steps?: TourStep[]
  targetTimeoutMs?: number
  hiddenGraceMs?: number
}) {
  const { user } = useAuth()
  const { current } = useOrganization()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const [answered, setAnswered] = useState<number[]>([])
  const [run, setRun] = useState<RunState | null>(null)
  const [active, setActive] = useState<ActiveStop | null>(null)
  const starting = useRef(false)

  const userId = user?.id ?? null
  const invitationOpen =
    userId !== null && !!current && user?.tourPromptSeen === false && !answered.includes(userId) && !seenHere(userId) && !run

  const start = useCallback(() => {
    if (starting.current || !current) {
      return
    }
    starting.current = true
    const organizationId = current.Id
    // The app pages need an app: the first one of the list (hidden ones aside); none leaves those steps out.
    void appsApi
      .list(organizationId, { PageNumber: 1, RowCount: 1 })
      .then((page) => page.Data?.[0]?.Id ?? null)
      .catch(() => null)
      .then((appId) => {
        starting.current = false
        const stops = resolveSteps(steps, { appId })
        if (stops.length > 0) {
          setActive(null)
          setRun({ stops, index: 0, direction: 1 })
        }
      })
  }, [current, steps])

  const stop = useCallback(() => {
    setRun(null)
    setActive(null)
  }, [])

  const answer = useCallback(
    (startTour: boolean) => {
      if (userId === null) {
        return
      }
      setAnswered((list) => [...list, userId])
      rememberSeen(userId)
      // Stored on the account so no other device asks again; a failure is fine, this browser remembers it.
      authApi.markTourPromptSeen().catch(() => undefined)
      if (startTour) {
        start()
      }
    },
    [start, userId],
  )

  const go = useCallback((direction: 1 | -1) => {
    setActive(null)
    setRun((state) => {
      if (!state) {
        return state
      }
      const index = state.index + direction
      return index < 0 || index >= state.stops.length ? (direction === 1 ? null : state) : { ...state, index, direction }
    })
  }, [])

  // A step: open its page, then wait for an anchor. Found: spotlight it. Hidden: the card in the middle. Missing: skip.
  const stopAt = run ? run.stops[run.index] : null
  const runIndex = run?.index ?? -1
  const direction = run?.direction ?? 1
  useEffect(() => {
    if (!stopAt) {
      return
    }
    if (stopAt.path && stopAt.path !== pathname) {
      navigate(stopAt.path)
      return
    }
    if (stopAt.targets.length === 0) {
      setActive({ stop: stopAt, element: null })
      return
    }
    const startedAt = Date.now()
    let hiddenSince: number | null = null
    let timer = 0
    const check = () => {
      const found = findTarget(stopAt.targets)
      const now = Date.now()
      if (found.state === 'found') {
        // A fallback anchor (the menu item) waits a moment for the page's own element, which may still be loading.
        if (found.rank === 0 || now - startedAt >= hiddenGraceMs) {
          setActive({ stop: stopAt, element: found.element })
          return
        }
      } else if (found.state === 'hidden') {
        hiddenSince ??= now
        if (now - hiddenSince >= hiddenGraceMs) {
          setActive({ stop: stopAt, element: null })
          return
        }
      } else if (now - startedAt >= targetTimeoutMs) {
        // Nothing to point at (no permission, a section that is off): move on in the same direction.
        setRun((state) => {
          if (!state || state.index !== runIndex) {
            return state
          }
          const index = state.index + direction
          if (index < 0) {
            return { ...state, index: 0, direction: 1 }
          }
          return index >= state.stops.length ? null : { ...state, index }
        })
        return
      }
      timer = window.setTimeout(check, POLL_MS)
    }
    check()
    return () => window.clearTimeout(timer)
  }, [stopAt, runIndex, direction, pathname, navigate, targetTimeoutMs, hiddenGraceMs])

  // Signing out or switching the organization ends the tour.
  useEffect(() => {
    if (!userId || !current) {
      setRun(null)
      setActive(null)
    }
  }, [userId, current])

  const value = useMemo(() => ({ start, running: !!run }), [start, run])

  return (
    <TourContext.Provider value={value}>
      {children}
      {invitationOpen ? (
        <TourInvitation stepCount={resolveSteps(steps, { appId: 1 }).length} onStart={() => answer(true)} onLater={() => answer(false)} />
      ) : null}
      {run ? (
        <TourOverlay
          active={active && active.stop === stopAt ? active : null}
          index={run.index}
          total={run.stops.length}
          onBack={() => go(-1)}
          onNext={() => go(1)}
          onExit={stop}
        />
      ) : null}
    </TourContext.Provider>
  )
}
