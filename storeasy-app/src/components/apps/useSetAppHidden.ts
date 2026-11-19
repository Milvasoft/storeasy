import { useMutation, useQueryClient } from '@tanstack/react-query'
import { contentKeys } from '@/features/common/keys'
import { appsApi } from '@/lib/api/endpoints'
import type { AppListDto } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'

interface SetAppHiddenVariables {
  app: Pick<AppListDto, 'Id' | 'Name'>
  hidden: boolean
  // Set when the change turns back an earlier one: its toast has no "Undo" of its own.
  undo?: boolean
}

/**
 * Hides an app from the apps list or shows it again (POST apps/app/hidden). Every apps list, the overview and the
 * compliance calendar are read again, and a toast says what happened with an "Undo" that turns the change back.
 */
export function useSetAppHidden() {
  const t = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: ({ app, hidden }: SetAppHiddenVariables) => appsApi.setHidden(organization.Id, app.Id, hidden),
    onSuccess: async (_appId, { app, hidden, undo }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.apps.all }),
        queryClient.invalidateQueries({ queryKey: contentKeys.overview(organization.Id) }),
        queryClient.invalidateQueries({ queryKey: ['content', 'compliance', organization.Id] }),
      ])
      showToast(
        interpolate(hidden ? t.apps.hiddenDone : t.apps.unhiddenDone, { app: app.Name }),
        'success',
        undefined,
        undo ? undefined : { label: t.apps.undo, onClick: () => mutation.mutate({ app, hidden: !hidden, undo: true }) },
      )
    },
  })

  return mutation
}
