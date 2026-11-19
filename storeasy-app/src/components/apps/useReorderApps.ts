import { useMutation, useQueryClient, type QueryKey } from '@tanstack/react-query'
import { appsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import type { ApiListResponse, AppListDto } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'

interface ReorderContext {
  previous?: ApiListResponse<AppListDto>
}

/** The page with its apps in the order of ids; an app the ids do not name keeps its place after them. */
export function inOrder(apps: readonly AppListDto[], ids: readonly number[]): AppListDto[] {
  const placed = ids.flatMap((id) => apps.filter((app) => app.Id === id))
  return [...placed, ...apps.filter((app) => !ids.includes(app.Id))]
}

/**
 * Saves a new order of the apps list (POST apps/app/order) with the ids of every shown app. The list under queryKey
 * takes the new order at once; when the server refuses, it goes back to the order before and a toast says why. Every
 * apps list is read again afterwards.
 */
export function useReorderApps(queryKey: QueryKey) {
  const t = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()

  return useMutation<number, unknown, number[], ReorderContext>({
    mutationFn: (ids) => appsApi.reorder(organization.Id, ids),
    onMutate: async (ids) => {
      await queryClient.cancelQueries({ queryKey })
      const previous = queryClient.getQueryData<ApiListResponse<AppListDto>>(queryKey)
      if (previous?.Data) {
        queryClient.setQueryData<ApiListResponse<AppListDto>>(queryKey, { ...previous, Data: inOrder(previous.Data, ids) })
      }
      return { previous }
    },
    onError: (error, _ids, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous)
      }
      showToast(apiErrorMessage(error, t.apps.reorderFailed), 'error')
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.apps.all }),
  })
}
