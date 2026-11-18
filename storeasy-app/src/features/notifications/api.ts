import { apiDelete, apiPatchList, apiPut } from '@/lib/api/http'
import { FilterType, SortType, type FilterCriteria, type ListRequest } from '@/lib/api/list'
import type { AccountNotificationDto, NotificationType } from './types'

// storeasy-api AccountController notifications: the user's own notifications from every organization.
export const notificationsApi = {
  list: (request: ListRequest) => apiPatchList<AccountNotificationDto>('/Account/notifications', request),

  markSeen: (input: { ids?: number[]; all?: boolean }) =>
    apiPut<null>('/Account/notifications/seen', { NotificationIdList: input.ids ?? [], MarkAll: input.all ?? false }),

  // Permanent; the record a notification points at stays.
  remove: (input: { ids?: number[]; all?: boolean }) =>
    apiDelete<null>('/Account/notifications', undefined, { NotificationIdList: input.ids ?? [], DeleteAll: input.all ?? false }),
}

export interface NotificationFilter {
  unread: boolean
  type: NotificationType | null
}

/** The list request of a filter: newest first, unread through IsSeen, the kind through Type. */
export function notificationRequest(filter: NotificationFilter, page: number, rowCount: number): ListRequest {
  const criterias: FilterCriteria[] = []
  if (filter.unread) {
    criterias.push({ FilterBy: 'IsSeen', Value: false, Type: FilterType.EqualTo })
  }
  if (filter.type !== null) {
    criterias.push({ FilterBy: 'Type', Value: filter.type, Type: FilterType.EqualTo })
  }
  return {
    PageNumber: page,
    RowCount: rowCount,
    Sorting: { SortBy: 'CreationDate', Type: SortType.Desc },
    ...(criterias.length > 0 ? { Filtering: { Criterias: criterias } } : {}),
  }
}

// The bell asks for one unread row: TotalDataCount is the counter.
export const UNREAD_COUNT_REQUEST = notificationRequest({ unread: true, type: null }, 1, 1)
export const BELL_REQUEST = notificationRequest({ unread: false, type: null }, 1, 8)

export const notificationKeys = {
  all: ['notifications'] as const,
  unreadCount: () => ['notifications', 'unread-count'] as const,
  list: (request: ListRequest) => ['notifications', 'list', request] as const,
  preferences: () => ['notifications', 'preferences'] as const,
}
