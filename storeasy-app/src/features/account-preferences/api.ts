import { apiGet, apiPut } from '@/lib/api/http'
import type { NotificationPreferenceDto, NotificationType } from '@/features/notifications/types'

// storeasy-api AccountPreferencesController: the user's notification channels per kind, in every organization.
export const preferencesApi = {
  list: () => apiGet<NotificationPreferenceDto[]>('/account/preferences'),

  // One kind at a time; a locked kind keeps in-app and e-mail on whatever is sent.
  update: (input: { Kind: NotificationType; InApp: boolean; Email: boolean; WebPush: boolean }) =>
    apiPut<NotificationPreferenceDto>('/account/preferences', input),
}
