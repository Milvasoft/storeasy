import { useMutation, useQueryClient } from '@tanstack/react-query'
import { appsApi } from '@/lib/api/endpoints'
import type { AppListDto } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'

interface SetAppAlertsMutedVariables {
  app: Pick<AppListDto, 'Id' | 'Name'>
  muted: boolean
  // Set when the change turns back an earlier one: its toast has no "Undo" of its own.
  undo?: boolean
}

/**
 * Mutes an app's alerts or turns them on again (POST apps/app/alerts-muted). The app stays in every list; the live
 * board and the notifications leave it out. The apps lists and the live board are read again, and a toast says what
 * happened with an "Undo" that turns the change back.
 */
export function useSetAppAlertsMuted() {
  const t = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: ({ app, muted }: SetAppAlertsMutedVariables) => appsApi.setAlertsMuted(organization.Id, app.Id, muted),
    onSuccess: async (_appId, { app, muted, undo }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.apps.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.jobs.board(organization.Id) }),
      ])
      showToast(
        interpolate(muted ? t.apps.mutedDone : t.apps.unmutedDone, { app: app.Name }),
        'success',
        undefined,
        undo ? undefined : { label: t.apps.undo, onClick: () => mutation.mutate({ app, muted: !muted, undo: true }) },
      )
    },
  })

  return mutation
}
