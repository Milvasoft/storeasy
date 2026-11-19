import { useCallback, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { isStoreIconKey, type StoreIconKey } from '@/components/ui/StoreIcons'
import { connectionsApi } from '@/lib/api/endpoints'
import type { StoreConnectionDto, StoreKind } from '@/lib/api/types'
import { useOptionalOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'

type IconSource = Pick<StoreConnectionDto, 'Id' | 'Store' | 'Icon'>

/**
 * The icon a store tag shows.
 * - With a connection id that is in the list: that connection's icon, or null (the text code) when it has none.
 * - Otherwise (a place that knows only the store, or a connection that is gone): the icon of the store's oldest
 *   connection (lowest id) that has one, so filters, counts and store columns match the records they stand for.
 * Null means the store's text code (AS / GP / MS). Unknown keys (an older or newer API) count as no icon.
 */
export function resolveStoreIcon(connections: readonly IconSource[], store: StoreKind, connectionId?: number | null): StoreIconKey | null {
  if (connectionId != null) {
    const own = connections.find((connection) => connection.Id === connectionId)
    if (own) {
      return isStoreIconKey(own.Icon) ? own.Icon : null
    }
  }
  const first = connections
    .filter((connection) => connection.Store === store && isStoreIconKey(connection.Icon))
    .sort((a, b) => a.Id - b.Id)[0]
  return first && isStoreIconKey(first.Icon) ? first.Icon : null
}

/**
 * The connection icons of the current organization, from the connections list every member may read (the same query the
 * connections page uses, so an added, edited or removed connection shows everywhere after its refetch). Without an
 * organization, or while the list loads or fails, every store shows its text code.
 */
export function useStoreIcons(): { iconFor: (store: StoreKind, connectionId?: number | null) => StoreIconKey | null } {
  const organizationId = useOptionalOrganization()?.current?.Id ?? null
  const connections = useQuery({
    queryKey: queryKeys.connections.list(organizationId ?? 0),
    queryFn: () => connectionsApi.list(organizationId ?? 0),
    enabled: organizationId != null,
    staleTime: 5 * 60_000,
    refetchOnMount: false,
  })
  const list = useMemo(() => connections.data ?? [], [connections.data])
  const iconFor = useCallback((store: StoreKind, connectionId?: number | null) => resolveStoreIcon(list, store, connectionId), [list])
  return { iconFor }
}
