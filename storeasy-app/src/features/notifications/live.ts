import { useEffect, useRef } from 'react'
import { useQueryClient, type QueryKey } from '@tanstack/react-query'
import { useActivity } from '@/lib/activity/ActivityContext'

/**
 * Refreshes a query when one of the live event types arrives on the hub (notification.created for the bell,
 * review.received for the inbox, rejection.opened for the case list).
 */
export function useLiveRefresh(types: readonly string[], queryKey: QueryKey): void {
  const { subscribe } = useActivity()
  const queryClient = useQueryClient()
  const latest = useRef({ types, queryKey })
  latest.current = { types, queryKey }
  useEffect(
    () =>
      subscribe((event) => {
        if (latest.current.types.includes(event.Type)) {
          void queryClient.invalidateQueries({ queryKey: latest.current.queryKey })
        }
      }),
    [subscribe, queryClient],
  )
}
