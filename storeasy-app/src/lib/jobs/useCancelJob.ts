import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { jobsApi } from '@/lib/api/endpoints'
import { apiErrorMessage, hasApiKey } from '@/lib/api/http'
import { StoreJobStatus } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'

export const JOB_ALREADY_FINISHED_KEY = 'StoreJobAlreadyFinished'

/**
 * Cancels a job after a confirmation (design 11.4 "İşi iptal et"). A queued job is cancelled at once; a running one stops
 * at its next step boundary, which the toast says. A job that finished meanwhile (409) is reported, not treated as an
 * error, and the lists refresh either way.
 */
export function useCancelJob() {
  const t = useT()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: (jobId: number) => jobsApi.cancel(organization.Id, jobId),
    meta: { silentError: true },
    onSuccess: (status) => {
      showToast(status === StoreJobStatus.Cancelled ? t.jobs.cancelled : t.jobs.stopRequested, 'success')
    },
    onError: (error) => {
      if (hasApiKey(error, JOB_ALREADY_FINISHED_KEY)) {
        showToast(t.jobs.alreadyFinished, 'info')
      } else {
        showToast(apiErrorMessage(error, t.common.error), 'error')
      }
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.jobs.all }),
  })

  const cancel = async (jobId: number) => {
    const ok = await confirm({
      title: t.jobs.cancelTitle,
      message: t.jobs.cancelBody,
      confirmLabel: t.jobs.cancel,
      cancelLabel: t.jobs.keep,
      danger: true,
    })
    if (ok) {
      mutation.mutate(jobId)
    }
  }

  return { cancel, pending: mutation.isPending }
}
