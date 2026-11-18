import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { authApi } from '@/lib/api/endpoints'
import { AUTH_LOST_EVENT } from '@/lib/api/http'
import { tokenStore, type AuthTokens } from '@/lib/api/tokenStore'
import { UserType, type LoginResponseDto } from '@/lib/api/types'
import { removeStorage, StorageKey, writeStorage } from '@/lib/utils/storage'

// The client-side model of the signed-in user (camelCase: it is not a contract type).
export interface CurrentUser {
  id: number
  userName: string
  email?: string | null
  name?: string | null
  surname?: string | null
  userType: UserType
}

type AuthStatus = 'loading' | 'authed' | 'guest'

interface AuthContextValue {
  status: AuthStatus
  user: CurrentUser | null
  // A platform administrator (Manager user type): sees the Platform menu group.
  isManager: boolean
  // True after the http layer ended the session (refresh failed): the sign-in screen says so (design 5.6, 18.16).
  sessionEnded: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  // Ends the session locally after the API already invalidated it (password changed, account removed).
  endSession: () => void
  refreshUser: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

function persist(login: LoginResponseDto, userName: string): void {
  const tokens: AuthTokens = {
    accessToken: login.Token.AccessToken,
    refreshToken: login.Token.RefreshToken,
    expiresIn: login.Token.ExpiresIn,
    refreshTokenExpiresIn: login.Token.RefreshTokenExpiresIn,
    issuedAt: Date.now(),
    userId: login.Id,
    userName,
    userType: login.UserType,
  }
  tokenStore.set(tokens)
}

// Loads the account and maps it to the client model. A failure still yields a minimal user, so a flaky detail call does
// not sign anyone out (a real auth failure already cleared the token in the http layer).
async function hydrate(tokens: AuthTokens): Promise<CurrentUser> {
  try {
    const detail = await authApi.accountDetail()
    return {
      id: tokens.userId,
      userName: detail.UserName || tokens.userName,
      email: detail.Email ?? tokens.userName,
      name: detail.Name,
      surname: detail.Surname,
      userType: tokens.userType,
    }
  } catch {
    return { id: tokens.userId, userName: tokens.userName, email: tokens.userName, userType: tokens.userType }
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [sessionEnded, setSessionEnded] = useState(false)
  const queryClient = useQueryClient()

  const applySession = useCallback(
    async (login: LoginResponseDto, userName: string) => {
      persist(login, userName)
      writeStorage(StorageKey.LastEmail, userName)
      // A fresh session must not read the previous one's cached data (another user's organizations on an account
      // switch): drop the whole React Query cache so everything refetches for the new session.
      queryClient.clear()
      setSessionEnded(false)
      setUser(await hydrate(tokenStore.get()!))
      setStatus('authed')
    },
    [queryClient],
  )

  const login = useCallback(
    async (email: string, password: string) => {
      const userName = email.trim()
      await applySession(await authApi.login(userName, password), userName)
    },
    [applySession],
  )

  const endSession = useCallback(() => {
    tokenStore.clear()
    queryClient.clear()
    setUser(null)
    setStatus('guest')
  }, [queryClient])

  const logout = useCallback(async () => {
    const current = tokenStore.get()
    if (current?.userName) {
      try {
        await authApi.logout(current.userName)
      } catch {
        /* best effort: the local session ends either way */
      }
    }
    removeStorage(StorageKey.CurrentOrganization)
    endSession()
  }, [endSession])

  // Restore the stored session on start.
  useEffect(() => {
    const stored = tokenStore.get()
    if (!stored?.accessToken) {
      setStatus('guest')
      return
    }
    let active = true
    void hydrate(stored).then((hydrated) => {
      if (!active) {
        return
      }
      // A refresh that failed while hydrating already cleared the token and fired AUTH_LOST: do not resurrect it.
      if (!tokenStore.get()?.accessToken) {
        setStatus('guest')
        return
      }
      setUser(hydrated)
      setStatus('authed')
    })
    return () => {
      active = false
    }
  }, [])

  // Forced sign-out from the http layer.
  useEffect(() => {
    const onLost = () => {
      queryClient.clear()
      setUser(null)
      setSessionEnded(true)
      setStatus('guest')
    }
    window.addEventListener(AUTH_LOST_EVENT, onLost)
    return () => window.removeEventListener(AUTH_LOST_EVENT, onLost)
  }, [queryClient])

  const refreshUser = useCallback(async () => {
    const stored = tokenStore.get()
    if (!stored) {
      return
    }
    setUser(await hydrate(stored))
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, isManager: user?.userType === UserType.Manager, sessionEnded, login, logout, endSession, refreshUser }),
    [status, user, sessionEnded, login, logout, endSession, refreshUser],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return ctx
}

// "Deniz Arslan", else the e-mail.
export function displayName(user: Pick<CurrentUser, 'name' | 'surname' | 'email' | 'userName'>): string {
  const full = [user.name, user.surname].filter(Boolean).join(' ').trim()
  return full || user.email || user.userName
}

// "DA" for the avatar.
export function initialsOf(text: string): string {
  const words = text
    .replace(/@.*/, '')
    .split(/[\s._-]+/)
    .filter(Boolean)
  const letters = words.length > 1 ? words[0][0] + words[1][0] : (words[0] ?? '?').slice(0, 2)
  return letters.toLocaleUpperCase('tr')
}
