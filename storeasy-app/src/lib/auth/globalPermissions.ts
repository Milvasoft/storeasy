import { useMemo } from 'react'
import { tokenStore } from '@/lib/api/tokenStore'
import { useAuth } from '@/lib/auth/AuthContext'

// Platform (global) permissions: account/detail lists them (AccountDetailDto.Permissions, App.SuperAdmin already expanded);
// an older API that does not send the list falls back to the access token, whose role claims carry "Group.Action"
// (AccountManager.BuildRoleClaims). The client reads them only to hide what the API would refuse (08 section 1.5); the API
// still checks every request.
export const SUPER_ADMIN = 'App.SuperAdmin'

const ROLE_CLAIMS = ['role', 'roles', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role']

function decodePayload(token: string): Record<string, unknown> | null {
  const parts = token.split('.')
  if (parts.length !== 3 || !parts[1]) {
    return null
  }
  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4)
    const bytes = Uint8Array.from(atob(padded), (char) => char.charCodeAt(0))
    const payload: unknown = JSON.parse(new TextDecoder().decode(bytes))
    return payload && typeof payload === 'object' && !Array.isArray(payload) ? (payload as Record<string, unknown>) : null
  } catch {
    return null
  }
}

/** The permission claims of an access token; null when the token cannot be read (then nothing is hidden). */
export function readGlobalPermissions(accessToken: string | null | undefined): Set<string> | null {
  const payload = accessToken ? decodePayload(accessToken) : null
  if (!payload) {
    return null
  }
  const values = ROLE_CLAIMS.flatMap((claim) => {
    const value = payload[claim]
    return Array.isArray(value) ? value : value === undefined ? [] : [value]
  })
  return new Set(values.filter((value): value is string => typeof value === 'string'))
}

export interface GlobalPermissions {
  /** True when the token carries the permission or App.SuperAdmin (always true when the token is unreadable). */
  has: (permission: string) => boolean
  isSuperAdmin: boolean
}

/** The signed-in platform administrator's global permissions; an app user has none. */
export function useGlobalPermissions(): GlobalPermissions {
  const { isManager, user } = useAuth()
  const accessToken = user ? tokenStore.get()?.accessToken : null
  const listed = user?.permissions ?? null
  return useMemo(() => {
    if (!isManager) {
      return { has: () => false, isSuperAdmin: false }
    }
    const permissions = listed ? new Set(listed) : readGlobalPermissions(accessToken)
    if (!permissions) {
      return { has: () => true, isSuperAdmin: false }
    }
    const isSuperAdmin = permissions.has(SUPER_ADMIN)
    return { has: (permission) => isSuperAdmin || permissions.has(permission), isSuperAdmin }
  }, [isManager, accessToken, listed])
}
