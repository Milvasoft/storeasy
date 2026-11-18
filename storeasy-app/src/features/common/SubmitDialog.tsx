import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Modal } from '@/components/ui/Modal'
import { Button, Field, Input } from '@/components/ui/primitives'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { releasesApi } from './api'
import { useP2 } from './i18n/useP2'
import { contentKeys } from './keys'

/** The text the API expects for a submission (ChangePlanRules.ConfirmationText): "App 1.0", or the name alone. */
export function confirmationText(appName: string, version?: string | null): string {
  return version?.trim() ? `${appName.trim()} ${version.trim()}` : appName.trim()
}

// Spaces are squashed on both sides, as the API compares (ChangePlanRules.ConfirmationMatches).
function squash(value: string): string {
  return value.split(' ').filter(Boolean).join(' ')
}

/**
 * The submission gate (design 6.10): a summary, the app name and version typed exactly, the button disabled until the
 * text matches. The API makes the plan and approves it in one call.
 */
export function SubmitDialog({
  open,
  onClose,
  organizationId,
  storeAppId,
  appName,
  versionId,
  versionString,
}: {
  open: boolean
  onClose: () => void
  organizationId: number
  storeAppId: number
  appName: string
  versionId: string | null
  versionString: string | null
}) {
  const t = useP2()
  const queryClient = useQueryClient()
  const [typed, setTyped] = useState('')
  const expected = confirmationText(appName, versionString)
  const matches = squash(typed) === squash(expected)

  const submit = useMutation({
    mutationFn: () => releasesApi.submitForReview(organizationId, storeAppId, versionId ?? '', typed),
    onSuccess: () => {
      showToast(t.submit.sent, 'success', expected)
      void queryClient.invalidateQueries({ queryKey: contentKeys.all })
      setTyped('')
      onClose()
    },
  })

  return (
    <Modal
      open={open}
      onClose={onClose}
      role="alertdialog"
      title={interpolate(t.submit.title, { name: expected })}
      description={t.submit.body}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.plan.close}
          </Button>
          <Button variant="primary" disabled={!matches || !versionId} loading={submit.isPending} onClick={() => submit.mutate()}>
            {t.submit.send}
          </Button>
        </>
      }
    >
      {!versionId ? <p className="text-[13px] text-fg2">{t.submit.noVersion}</p> : null}
      <dl className="kv mb-3">
        <dt>{t.submit.version}</dt>
        <dd className="mono">{versionString ?? '-'}</dd>
      </dl>
      <Field
        label={interpolate(t.submit.typeLabel, { text: expected })}
        error={typed.trim() && !matches ? t.submit.mismatch : null}
        hint={matches ? t.submit.match : undefined}
      >
        <Input value={typed} onChange={(event) => setTyped(event.target.value)} autoComplete="off" spellCheck={false} />
      </Field>
    </Modal>
  )
}
