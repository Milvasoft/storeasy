import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { organizationsApi } from '@/lib/api/endpoints'
import type { MyOrganizationDto, OrgPermission } from '@/lib/api/types'
import { useAuth } from '@/lib/auth/AuthContext'
import { queryKeys } from '@/lib/query/keys'
import { readStorage, removeStorage, StorageKey, writeStorage } from '@/lib/utils/storage'

interface OrganizationContextValue {
  organizations: MyOrganizationDto[]
  // The organization every organization request is made for; null while loading or when the user has none.
  current: MyOrganizationDto | null
  isLoading: boolean
  error: unknown
  switchTo: (organizationId: number) => void
  // The caller's permission in the current organization. The API checks again on every request (K3); this only shapes
  // the UI.
  can: (permission: OrgPermission) => boolean
  refetch: () => Promise<unknown>
}

const OrganizationContext = createContext<OrganizationContextValue | null>(null)

function readStoredId(): number | null {
  const raw = Number(readStorage(StorageKey.CurrentOrganization))
  return Number.isInteger(raw) && raw > 0 ? raw : null
}

export function OrganizationProvider({ children }: { children: ReactNode }) {
  const { status } = useAuth()
  const queryClient = useQueryClient()
  const [selectedId, setSelectedId] = useState<number | null>(readStoredId)

  // The membership list is the source of the role and permissions; a role change shows up on the next refetch (focus).
  const mine = useQuery({
    queryKey: queryKeys.organizations.mine(),
    queryFn: () => organizationsApi.mine(),
    enabled: status === 'authed',
    staleTime: 60_000,
    refetchOnWindowFocus: true,
  })

  const organizations = useMemo(() => mine.data ?? [], [mine.data])

  // The stored choice when it is still one of the user's organizations (a removed membership falls back to the first).
  const current = useMemo(() => organizations.find((org) => org.Id === selectedId) ?? organizations[0] ?? null, [organizations, selectedId])

  useEffect(() => {
    if (!mine.data) {
      return
    }
    if (current) {
      writeStorage(StorageKey.CurrentOrganization, String(current.Id))
    } else {
      removeStorage(StorageKey.CurrentOrganization)
    }
  }, [mine.data, current])

  const switchTo = useCallback(
    (organizationId: number) => {
      setSelectedId(organizationId)
      writeStorage(StorageKey.CurrentOrganization, String(organizationId))
      // Organization scoped data of the previous organization must not show for a moment under the new one.
      void queryClient.invalidateQueries({ predicate: (query) => query.queryKey[0] !== queryKeys.organizations.all[0] })
    },
    [queryClient],
  )

  const can = useCallback((permission: OrgPermission) => !!current?.Permissions?.includes(permission), [current])

  const value = useMemo<OrganizationContextValue>(
    () => ({
      organizations,
      current,
      isLoading: status === 'authed' && mine.isLoading,
      error: mine.error,
      switchTo,
      can,
      refetch: mine.refetch,
    }),
    [organizations, current, status, mine.isLoading, mine.error, switchTo, can, mine.refetch],
  )

  return <OrganizationContext.Provider value={value}>{children}</OrganizationContext.Provider>
}

export function useOrganization(): OrganizationContextValue {
  const ctx = useContext(OrganizationContext)
  if (!ctx) {
    throw new Error('useOrganization must be used within OrganizationProvider')
  }
  return ctx
}

// For pages inside the shell, which renders only when an organization is selected.
export function useCurrentOrganization(): MyOrganizationDto {
  const { current } = useOrganization()
  if (!current) {
    throw new Error('useCurrentOrganization needs a selected organization (OrganizationGate)')
  }
  return current
}
