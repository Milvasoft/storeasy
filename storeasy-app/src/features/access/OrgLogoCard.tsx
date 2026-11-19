import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Trash2 } from 'lucide-react'
import { OrgAvatar } from '@/components/org/OrgAvatar'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { FileDrop } from '@/components/ui/FileDrop'
import { Button } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission } from '@/lib/api/types'
import { putBlob } from '@/lib/assets/upload'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { useAccessText } from './i18n'

// The organization logo (POST organizations/logo/upload, the presigned PUT, PUT organizations/logo; DELETE to remove).
// Owners (ManageOrganization) change it; the API checks the file's signature and redraws it as a square PNG.

/** OrganizationLogoRules.MaxBytes. */
export const LOGO_MAX_BYTES = 2 * 1024 * 1024

/** The accepted extensions and the type each is declared with (OrganizationLogoRules.AcceptedContentTypes). */
const LOGO_TYPES: Record<string, string> = {
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
}

function contentTypeOf(file: File): string {
  const name = file.name.toLowerCase()
  const extension = Object.keys(LOGO_TYPES).find((key) => name.endsWith(key))
  return extension ? LOGO_TYPES[extension] : file.type
}

/** The logo card of organization settings: preview, upload or replace (drop or pick), remove. */
export function OrgLogoCard() {
  const t = useAccessText()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const { can, refetch } = useOrganization()
  const editable = can(OrgPermission.ManageOrganization)
  const [error, setError] = useState<string | null>(null)

  const upload = useMutation({
    mutationFn: async (file: File) => {
      const contentType = contentTypeOf(file)
      const ticket = await organizationsApi.createLogoUpload(organization.Id, contentType, file.size)
      if (!ticket.UploadUrl) {
        throw new Error(base.upload.failed)
      }
      await putBlob(ticket.UploadUrl, file, contentType)
      return organizationsApi.setLogo(organization.Id, ticket.AssetId)
    },
    meta: { silentError: true },
    onSuccess: async () => {
      setError(null)
      await refetch()
      showToast(t.orgLogo.saved, 'success')
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.upload.failed)),
  })

  const remove = useMutation({
    mutationFn: () => organizationsApi.removeLogo(organization.Id),
    meta: { silentError: true },
    onSuccess: async () => {
      setError(null)
      await refetch()
      showToast(t.orgLogo.removed, 'success')
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const askRemove = async () => {
    const ok = await confirm({ title: t.orgLogo.removeTitle, message: t.orgLogo.removeBody, confirmLabel: t.orgLogo.remove, danger: true })
    if (ok) {
      remove.mutate()
    }
  }

  const hasLogo = !!organization.LogoUrl

  return (
    <section className="card" aria-label={t.orgLogo.title}>
      <div className="card-h">
        <h2 className="h3">{t.orgLogo.title}</h2>
      </div>
      <div className="card-b grid gap-4">
        <div className="flex items-center gap-4">
          <OrgAvatar name={organization.Name} logoUrl={organization.LogoUrl} size="l" />
          <div className="grid min-w-0 flex-1 gap-1 text-[13px]">
            <b>{hasLogo ? t.orgLogo.current : organization.Name}</b>
            <span className="text-fg3">{hasLogo ? t.orgLogo.hint : t.orgLogo.none}</span>
          </div>
          {editable && hasLogo ? (
            <Button variant="ghost" size="sm" loading={remove.isPending} disabled={upload.isPending} onClick={() => void askRemove()}>
              <Trash2 aria-hidden />
              {t.orgLogo.remove}
            </Button>
          ) : null}
        </div>
        {editable ? (
          upload.isPending ? (
            <div className="drop up" role="status">
              <b>{t.orgLogo.uploading}</b>
            </div>
          ) : (
            <FileDrop
              label={hasLogo ? t.orgLogo.replace : t.orgLogo.dropLabel}
              accept={Object.keys(LOGO_TYPES)}
              maxBytes={LOGO_MAX_BYTES}
              hint={t.orgLogo.dropHint}
              error={error}
              onFile={(file) => {
                setError(null)
                upload.mutate(file)
              }}
              onReject={(file, reason) =>
                setError(
                  reason === 'type'
                    ? interpolate(base.upload.rejectedType, { name: file.name })
                    : interpolate(base.upload.rejectedSize, { name: file.name, max: format.bytes(LOGO_MAX_BYTES) }),
                )
              }
            />
          )
        ) : (
          <p className="m-0 text-xs text-fg3">{t.orgLogo.ownerOnly}</p>
        )}
      </div>
    </section>
  )
}
