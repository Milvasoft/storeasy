import { useMutation } from '@tanstack/react-query'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { studioApi } from './api'

/** Asks for the export zip's short lived signed URL and opens it; the bytes come straight from the storage. */
export function useZipDownload() {
  const organization = useCurrentOrganization()
  const mutation = useMutation({
    mutationFn: (jobId: number) => studioApi.zip(organization.Id, jobId),
    onSuccess: (download) => {
      window.open(download.Url, '_blank', 'noopener')
    },
  })
  return { download: (jobId: number) => mutation.mutate(jobId), pending: mutation.isPending }
}
