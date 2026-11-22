import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { contentKeys } from '@/features/common/keys'
import { appsApi } from '@/lib/api/endpoints'
import type { AppListDto } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'

/**
 * Deletes an app whose store records were all added by id (DELETE apps/app), then goes back to the apps list. The apps
 * lists and the overview are read again; there is no undo, the records can be added again by their id.
 */
export function useDeleteApp() {
  const t = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  return useMutation({
    mutationFn: (app: Pick<AppListDto, 'Id' | 'Name'>) => appsApi.remove(organization.Id, app.Id),
    onSuccess: async (_appId, app) => {
      navigate('/apps')
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.apps.all }),
        queryClient.invalidateQueries({ queryKey: contentKeys.overview(organization.Id) }),
      ])
      showToast(interpolate(t.apps.deletedDone, { app: app.Name }), 'success')
    },
  })
}
