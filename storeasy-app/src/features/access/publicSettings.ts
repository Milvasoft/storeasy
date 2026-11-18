import { useQuery } from '@tanstack/react-query'
import { systemApi } from '@/lib/api/endpoints'

/** The anonymous instance settings (registration mode, Google sign in, web push key); read once per session. */
export function usePublicSettings() {
  return useQuery({
    queryKey: ['system', 'public-settings'],
    queryFn: () => systemApi.publicSettings(),
    staleTime: 5 * 60_000,
    retry: false,
  })
}
