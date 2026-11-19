import { useQuery } from '@tanstack/react-query'
import { systemApi } from '@/lib/api/endpoints'
import { DeploymentMode } from '@/lib/api/types'

/** The anonymous instance settings (registration mode, Google sign in, web push key); read once per session. */
export function usePublicSettings() {
  return useQuery({
    queryKey: ['system', 'public-settings'],
    queryFn: () => systemApi.publicSettings(),
    staleTime: 5 * 60_000,
    retry: false,
  })
}

/**
 * The deployment mode as the web app needs it. Single team only when the API says so: an API without the field (older,
 * or a test that does not state it) keeps today's multi tenant screens.
 */
export function useDeployment() {
  const settings = usePublicSettings()
  return {
    singleTeam: settings.data?.Mode === DeploymentMode.SingleTeam,
    setupRequired: settings.data?.SetupRequired === true,
    isLoading: settings.isLoading,
  }
}
