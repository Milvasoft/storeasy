import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { authApi } from '@/lib/api/endpoints'
import { API_REFUSAL_EVENT, AUTH_LOST_EVENT, type ApiRefusal } from '@/lib/api/http'
import { tokenStore, type AuthTokens } from '@/lib/api/tokenStore'
import { UserType, type ExternalLoginProvider, type LoginResponseDto } from '@/lib/api/types'
import { removeStorage, StorageKey, writeStorage } from '@/lib/utils/storage'

// The client-side model of the signed-in user (camelCase: it is not a contract type).
export interface CurrentUser {
  id: number
  userName: string
  email?: string | null
  name?: string | null
  surname?: string | null
  userType: UserType
  // False until the e-mail link is opened; null when unknown (the detail call failed). Platform managers count as verified.
  emailConfirmed: boolean | null
  // Platform permissions from account/detail ("Group.Action"); null when the detail did not say (then the token is read).
  permissions: string[] | null
  // The saved interface language ("tr-TR"); null until one is saved.
  preferredLanguage: string | null
  externalLogins: ExternalLoginProvider[]
}

export const EMAIL_NOT_VERIFIED_KEY = 'EmailNotVerified'

type AuthStatus = 'loading' | 'authed' | 'guest'

interface AuthContextValue {
  status: AuthStatus
  user: CurrentUser | null
  // A platform administrator (Manager user type): sees the Platform menu group.
  isManager: boolean
  // True after the http layer ended the session (refresh failed): the sign-in screen says so (design 5.6, 18.16).
  sessionEnded: boolean
  // Signed in with an e-mail that is not verified yet: organizations and connections are refused (403 EmailNotVerified).
  needsVerification: boolean
  login: (email: string, password: string) => Promise<void>
  // Completes the Google sign in with the callback's code and state.
  loginWithGoogle: (code: string, state: string, invitationToken?: string | null) => Promise<void>
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
    // A Google sign in does not know the user name until the detail answers; the logout call needs it.
    if (!tokens.userName && detail.UserName && tokenStore.get()?.accessToken === tokens.accessToken) {
      tokenStore.set({ ...tokens, userName: detail.UserName })
    }
    return {
      id: tokens.userId,
      userName: detail.UserName || tokens.userName,
      email: detail.Email ?? tokens.userName,
      name: detail.Name,
      surname: detail.Surname,
      userType: tokens.userType,
      emailConfirmed: tokens.userType === UserType.Manager ? true : (detail.EmailConfirmed ?? null),
      permissions: detail.Permissions ?? null,
      preferredLanguage: detail.PreferredLanguage ?? null,
      externalLogins: detail.ExternalLogins ?? [],
    }
  } catch {
    return {
      id: tokens.userId,
      userName: tokens.userName,
      email: tokens.userName,
      userType: tokens.userType,
      emailConfirmed: null,
      permissions: null,
      preferredLanguage: null,
      externalLogins: [],
    }
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
      if (userName) {
        writeStorage(StorageKey.LastEmail, userName)
      }
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

  const loginWithGoogle = useCallback(
    async (code: string, state: string, invitationToken?: string | null) => {
      // The user name comes with the account detail (hydrate stores it).
      await applySession(await authApi.googleLogin(code, state, invitationToken), '')
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

  // A refusal for an unverified e-mail, whichever screen made the call: the account is not verified (any more), so the
  // shell shows the verification banner even when the cached detail said otherwise.
  useEffect(() => {
    const onRefusal = (event: Event) => {
      if ((event as CustomEvent<ApiRefusal>).detail?.keys.includes(EMAIL_NOT_VERIFIED_KEY)) {
        setUser((current) => (current ? { ...current, emailConfirmed: false } : current))
      }
    }
    window.addEventListener(API_REFUSAL_EVENT, onRefusal)
    return () => window.removeEventListener(API_REFUSAL_EVENT, onRefusal)
  }, [])

  const refreshUser = useCallback(async () => {
    const stored = tokenStore.get()
    if (!stored) {
      return
    }
    setUser(await hydrate(stored))
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      isManager: user?.userType === UserType.Manager,
      sessionEnded,
      needsVerification: status === 'authed' && user?.emailConfirmed === false,
      login,
      loginWithGoogle,
      logout,
      endSession,
      refreshUser,
    }),
    [status, user, sessionEnded, login, loginWithGoogle, logout, endSession, refreshUser],
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
