import { useMutation } from '@tanstack/react-query'
import { useActivity } from '@/lib/activity/ActivityContext'
import { appsApi } from '@/lib/api/endpoints'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'

/**
 * Queues the apps and builds sync (POST apps/sync): for the given connections, or for every App Store Connect and Google
 * Play connection when none is given. The jobs are followed on the live stream (the Hat, the app's "on the line now")
 * and their end is announced to this user.
 */
export function useSyncApps(onStarted?: (jobIds: number[]) => void) {
  const t = useT()
  const organization = useCurrentOrganization()
  const { watchJobs } = useActivity()

  return useMutation({
    mutationFn: async (connectionIds: number[] | null) => {
      if (connectionIds === null) {
        return appsApi.sync(organization.Id, null)
      }
      const lists = await Promise.all(connectionIds.map((id) => appsApi.sync(organization.Id, id)))
      return lists.flat()
    },
    onSuccess: (jobIds, connectionIds) => {
      if (jobIds.length === 0) {
        showToast(t.apps.syncNothing, 'info')
        return
      }
      watchJobs(jobIds)
      onStarted?.(jobIds)
      showToast(
        connectionIds === null
          ? interpolate(t.apps.syncStarted, { count: jobIds.length })
          : interpolate(t.app.syncStarted, { count: connectionIds.length }),
        'success',
      )
    },
  })
}
