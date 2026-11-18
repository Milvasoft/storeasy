import { MutationCache, QueryClient } from '@tanstack/react-query'
import { apiErrorMessage, ApiError, FEATURE_DISABLED_KEY } from '@/lib/api/http'
import { getDictionary } from '@/lib/i18n/dictionaries'
import { showToast } from '@/lib/toast/toast'

declare module '@tanstack/react-query' {
  interface Register {
    // silentError: the mutation shows its own error (inline in a form) and the global toast stays quiet.
    mutationMeta: { silentError?: boolean }
  }
}

// The API answers business failures with a real 4xx (404, 403, 409...) and a switched-off feature with 503
// FeatureDisabled; retrying those only repeats the same answer. Network and other 5xx errors get one retry.
export function shouldRetry(failureCount: number, error: Error): boolean {
  if (error instanceof ApiError && error.statusCode >= 400 && error.statusCode < 500) {
    return false
  }
  if (error instanceof ApiError && error.keys.includes(FEATURE_DISABLED_KEY)) {
    return false
  }
  return failureCount < 1
}

// The one place a failed mutation is shown: the API's localized message, else the dictionary's generic error.
// A factory, so the test query client gets the same behaviour with a cache of its own.
export function createMutationCache(): MutationCache {
  return new MutationCache({
    onError: (error, _variables, _context, mutation) => {
      if (mutation.meta?.silentError) {
        return
      }
      showToast(apiErrorMessage(error, getDictionary().common.error), 'error')
    },
  })
}

export const queryClient = new QueryClient({
  mutationCache: createMutationCache(),
  defaultOptions: {
    queries: {
      retry: shouldRetry,
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
  },
})
