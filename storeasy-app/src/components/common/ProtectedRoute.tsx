import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '@/lib/auth/AuthContext'
import { HOME_PATH } from '@/lib/nav/nav'
import { BootScreen } from './BootScreen'

// Only in-app paths are accepted as a return target: no open redirect through ?returnUrl (design 5.6).
export function safeReturnPath(value: string | null | undefined): string | null {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) {
    return null
  }
  return value
}

export interface LoginState {
  from?: string
  sessionEnded?: boolean
}

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { status, sessionEnded } = useAuth()
  const location = useLocation()
  if (status === 'loading') {
    return <BootScreen />
  }
  if (status === 'guest') {
    const from = location.pathname + location.search
    return <Navigate to={`/login?returnUrl=${encodeURIComponent(from)}`} replace state={{ from, sessionEnded } satisfies LoginState} />
  }
  return <>{children}</>
}

// Guest-only pages (login, register): a signed-in user goes back to where a protected page sent them from.
export function GuestOnly({ children }: { children: ReactNode }) {
  const { status } = useAuth()
  const location = useLocation()
  if (status === 'loading') {
    return <BootScreen />
  }
  if (status === 'authed') {
    const params = new URLSearchParams(location.search)
    const from = safeReturnPath((location.state as LoginState | null)?.from) ?? safeReturnPath(params.get('returnUrl'))
    return <Navigate to={from ?? HOME_PATH} replace />
  }
  return <>{children}</>
}
