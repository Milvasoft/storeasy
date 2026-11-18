import { APP_KEY } from '@/config'

// Every key the app keeps in localStorage. Values are versioned when their shape may change (`x.v2`).
export const StorageKey = {
  AuthTokens: 'auth.tokens',
  DeviceId: 'device-id',
  Lang: 'lang',
  Theme: 'theme',
  // The organization the user worked in last; organization requests carry its id.
  CurrentOrganization: 'org.current',
  // The e-mail of the last sign-in, so the "session ended" screen can fill the field.
  LastEmail: 'auth.last-email',
} as const

export type StorageKey = (typeof StorageKey)[keyof typeof StorageKey]

export function storageKey(name: StorageKey): string {
  return `${APP_KEY}.${name}`
}

// Storage can be missing or full (private mode, quota): every access is guarded and a failure reads as "nothing stored".
export function readStorage(name: StorageKey): string | null {
  try {
    return localStorage.getItem(storageKey(name))
  } catch {
    return null
  }
}

export function writeStorage(name: StorageKey, value: string): void {
  try {
    localStorage.setItem(storageKey(name), value)
  } catch {
    /* ignore: the value lives for this session only */
  }
}

export function removeStorage(name: StorageKey): void {
  try {
    localStorage.removeItem(storageKey(name))
  } catch {
    /* ignore */
  }
}

export function readJson<T>(name: StorageKey): T | null {
  const raw = readStorage(name)
  if (!raw) {
    return null
  }
  try {
    return JSON.parse(raw) as T
  } catch {
    return null
  }
}

export function writeJson(name: StorageKey, value: unknown): void {
  writeStorage(name, JSON.stringify(value))
}
