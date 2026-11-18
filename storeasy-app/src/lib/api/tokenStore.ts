import type { UserType } from '@/lib/api/types'
import { readJson, readStorage, removeStorage, StorageKey, writeJson, writeStorage } from '@/lib/utils/storage'

export interface AuthTokens {
  accessToken: string
  refreshToken: string
  expiresIn: number
  refreshTokenExpiresIn: number
  issuedAt: number
  userId: number
  /** What the API knows the account by; storeasy signs in by e-mail (LoginCommand.UserName = e-mail, K11). */
  userName: string
  userType: UserType
}

// A random, persistent id per browser: the backend keeps one session per (user, device) and sends it on login,
// refresh and logout.
export function getDeviceId(): string {
  const stored = readStorage(StorageKey.DeviceId)
  if (stored) {
    return stored
  }
  const id =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `web-${Math.random().toString(36).slice(2)}-${Date.now()}`
  writeStorage(StorageKey.DeviceId, id)
  return id
}

export const tokenStore = {
  get(): AuthTokens | null {
    return readJson<AuthTokens>(StorageKey.AuthTokens)
  },
  set(tokens: AuthTokens): void {
    writeJson(StorageKey.AuthTokens, tokens)
  },
  clear(): void {
    removeStorage(StorageKey.AuthTokens)
  },
}
