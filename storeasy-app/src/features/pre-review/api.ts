import { useQuery } from '@tanstack/react-query'
import { preReviewApi } from '@/lib/api/endpoints'
import { PreReviewStatus, type PreReviewDto, type StoreKind } from '@/lib/api/types'

// The calls live in lib/api/endpoints.ts (preReviewApi); this file holds the query keys and the hooks of the AI pre-review.
export { preReviewApi }

export const preReviewKeys = {
  all: ['pre-review'] as const,
  list: (organizationId: number, appId: number, store: StoreKind) => ['pre-review', 'list', organizationId, appId, store] as const,
  detail: (organizationId: number, id: number) => ['pre-review', 'detail', organizationId, id] as const,
}

/** How often a queued or running pre-review is read again, in milliseconds. */
export const PRE_REVIEW_POLL_MS = 3000

/** Whether the pre-review still runs (queued or running). */
export function isPreReviewActive(status: PreReviewStatus | null | undefined): boolean {
  return status === PreReviewStatus.Queued || status === PreReviewStatus.Running
}

/** The pre-reviews of an app in one store, newest first. */
export function usePreReviewHistory(organizationId: number, appId: number, store: StoreKind, enabled = true) {
  return useQuery({
    queryKey: preReviewKeys.list(organizationId, appId, store),
    queryFn: () => preReviewApi.list(organizationId, appId, store),
    enabled,
  })
}

/** One pre-review, read again every few seconds while it runs. */
export function usePreReview(organizationId: number, id: number | null) {
  return useQuery({
    queryKey: preReviewKeys.detail(organizationId, id ?? 0),
    queryFn: () => preReviewApi.detail(organizationId, id ?? 0),
    enabled: id != null && id > 0,
    refetchInterval: (query) => (isPreReviewActive((query.state.data as PreReviewDto | undefined)?.Status) ? PRE_REVIEW_POLL_MS : false),
  })
}
