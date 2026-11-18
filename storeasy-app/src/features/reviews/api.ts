import { apiDelete, apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type { ReviewFilter, ReviewListDto, SavedReplyDto, SavedReplyInput } from './types'

// storeasy-api ReviewsController. A reply is a store job (the answer is its id); reading and drafts stay in storeasy.
export const reviewsApi = {
  list: (organizationId: number, filter: ReviewFilter, request: ListRequest) =>
    apiPatchList<ReviewListDto>('/Reviews', { OrganizationId: organizationId, ...filter, ...request }),

  // Publishes or replaces the reply; 429 past 300 replies an hour.
  reply: (organizationId: number, reviewId: number, body: string, savedReplyId: number | null) =>
    apiPost<number>(
      '/Reviews/reply',
      { OrganizationId: organizationId, ReviewId: reviewId, Body: body, SavedReplyId: savedReplyId },
      { idempotent: true },
    ),

  markRead: (organizationId: number, reviewIds: number[], isRead: boolean) =>
    apiPut<number>('/Reviews/read', { OrganizationId: organizationId, ReviewIds: reviewIds, IsRead: isRead }),

  saveDraft: (organizationId: number, reviewId: number, body: string) =>
    apiPut<number>('/Reviews/reply-draft', { OrganizationId: organizationId, ReviewId: reviewId, Body: body }),

  // Reads new reviews now (one job per store app); AppId null syncs every app.
  sync: (organizationId: number, appId: number | null) =>
    apiPost<number[]>('/Reviews/sync', { OrganizationId: organizationId, AppId: appId }),

  savedReplies: (organizationId: number, query: { Locale?: string | null; AppId?: number | null } = {}) =>
    apiGet<SavedReplyDto[]>('/Reviews/saved-replies', { OrganizationId: organizationId, ...query }),

  createSavedReply: (organizationId: number, input: SavedReplyInput) =>
    apiPost<number>('/Reviews/saved-reply', { OrganizationId: organizationId, ...input }, { idempotent: true }),

  updateSavedReply: (organizationId: number, savedReplyId: number, input: SavedReplyInput) =>
    apiPut<number>('/Reviews/saved-reply', { OrganizationId: organizationId, SavedReplyId: savedReplyId, ...input }),

  deleteSavedReply: (organizationId: number, savedReplyId: number) =>
    apiDelete<number>('/Reviews/saved-reply', undefined, { OrganizationId: organizationId, SavedReplyId: savedReplyId }),
}

// Query keys: [resource, scope, ...args]; reviewKeys.all refreshes the inbox and the saved replies together.
export const reviewKeys = {
  all: ['reviews'] as const,
  list: (organizationId: number, filter: ReviewFilter, request: ListRequest) =>
    ['reviews', 'list', organizationId, filter, request] as const,
  savedReplies: (organizationId: number, locale: string | null, appId: number | null) =>
    ['reviews', 'saved-replies', organizationId, locale, appId] as const,
}
