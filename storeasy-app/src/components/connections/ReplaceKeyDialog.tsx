import { useRef, useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { KeyRound } from 'lucide-react'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/primitives'
import { connectionsApi } from '@/lib/api/endpoints'
import { ApiError, apiErrorMessage } from '@/lib/api/http'
import { StoreKind, type StoreConnectionDto } from '@/lib/api/types'
import { appleSecret, checkP8, ISSUER_ID, KEY_ID, keyIdFromFileName, readServiceAccount } from '@/lib/connections/secrets'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { AppleKeyFields, ConnectionKeyInput, EMPTY_KEY, type KeyText } from './ConnectionKeyInput'

/** Whether a connection's key can be replaced in place (Microsoft secrets are renewed with "Renew secret"). */
export function canReplaceKey(connection: StoreConnectionDto): boolean {
  return connection.Store === StoreKind.Apple || connection.Store === StoreKind.Google
}

/** The "Replace key" button of an App Store Connect or Google Play connection card. */
export function ReplaceKeyButton({ onClick }: { onClick: () => void }) {
  const t = useT()
  return (
    <Button size="sm" onClick={onClick}>
      <KeyRound aria-hidden />
      {t.connections.replaceKey}
    </Button>
  )
}

/**
 * Replaces the key of an App Store Connect or Google Play connection in place: the connection id stays, so its apps, jobs
 * and history stay too. An Apple key must be of the same team (its Issuer ID ends with the fingerprint's last 4
 * characters; the API compares the whole id). The API verifies the new key with the store first and keeps the old one when
 * that fails; its answer is shown in the form. Mount it with a key per connection so the fields start empty.
 */
export function ReplaceKeyDialog({
  connection,
  organizationId,
  onClose,
}: {
  connection: StoreConnectionDto | null
  organizationId: number
  onClose: () => void
}) {
  const t = useT()
  const queryClient = useQueryClient()
  const store = connection?.Store ?? StoreKind.Apple
  const suffix = connection?.Fingerprint?.IssuerIdSuffix ?? null
  const [issuerId, setIssuerId] = useState('')
  const [keyId, setKeyId] = useState('')
  const [key, setKey] = useState<KeyText>(EMPTY_KEY)
  const [fileError, setFileError] = useState<string | null>(null)
  const [submitted, setSubmitted] = useState(false)
  const [server, setServer] = useState<{ title: string; messages: string[] } | null>(null)
  const errorRef = useRef<HTMLDivElement>(null)

  const close = () => {
    // The key leaves the form and the memory with the dialog.
    setIssuerId('')
    setKeyId('')
    setKey(EMPTY_KEY)
    setFileError(null)
    setSubmitted(false)
    setServer(null)
    onClose()
  }

  const replace = useMutation({
    mutationFn: () =>
      connectionsApi.replaceKey(
        organizationId,
        connection?.Id ?? 0,
        store === StoreKind.Apple ? appleSecret(issuerId, keyId, key.text) : key.text.trim(),
      ),
    meta: { silentError: true },
    onSuccess: (updated) => {
      queryClient.setQueryData<StoreConnectionDto[]>(queryKeys.connections.list(organizationId), (list) =>
        list?.map((item) => (item.Id === updated.Id ? updated : item)),
      )
      void queryClient.invalidateQueries({ queryKey: queryKeys.connections.all })
      showToast(t.connections.replaced, 'success', updated.Name)
      close()
    },
    onError: (failure) => {
      const messages = failure instanceof ApiError ? failure.messages : []
      setServer({
        title: messages.length > 1 ? t.connections.replaceFailed : apiErrorMessage(failure, t.connections.replaceFailed),
        messages: messages.length > 1 ? messages : [],
      })
      // Only the key is chosen again after a refusal; the ids stay.
      setKey(EMPTY_KEY)
      window.setTimeout(() => errorRef.current?.focus(), 0)
    },
  })

  // Field errors follow the values once the form was submitted, so a corrected value clears its message at once.
  const validate = (): Record<string, string | null> => {
    if (store === StoreKind.Apple) {
      const issuer = issuerId.trim()
      const p8 = key.text ? checkP8(key.text) : 'notPem'
      return {
        issuerId: !ISSUER_ID.test(issuer)
          ? t.connections.issuerInvalid
          : suffix && !issuer.toLowerCase().endsWith(suffix.toLowerCase())
            ? interpolate(t.connections.replaceIssuerMismatch, { suffix })
            : null,
        keyId: KEY_ID.test(keyId.trim().toUpperCase()) ? null : t.connections.keyIdInvalid,
        secret: !key.text ? t.connections.keyRequired : p8 ? t.connections.p8Invalid : null,
      }
    }
    const problem = key.text ? readServiceAccount(key.text).problem : 'notJson'
    return { secret: !key.text ? t.connections.jsonRequired : problem ? t.connections.json[problem] : null }
  }

  const shown: Record<string, string | null> = submitted
    ? { ...validate(), ...(fileError ? { secret: fileError } : {}) }
    : { secret: fileError }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setSubmitted(true)
    setFileError(null)
    setServer(null)
    if (!Object.values(validate()).some(Boolean)) {
      replace.mutate()
    }
  }

  const takeFileName = (fileName: string) => {
    const fromName = store === StoreKind.Apple ? keyIdFromFileName(fileName) : null
    if (fromName && !keyId) {
      setKeyId(fromName)
    }
  }

  return (
    <Modal
      open={!!connection}
      onClose={close}
      size="lg"
      title={interpolate(t.connections.replaceTitle, { name: connection?.Name ?? '' })}
      description={store === StoreKind.Apple ? t.connections.replaceAppleBody : t.connections.replaceGoogleBody}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="replace-connection-key" variant="primary" loading={replace.isPending}>
            {t.connections.replaceSubmit}
          </Button>
        </>
      }
    >
      {connection ? (
        <form id="replace-connection-key" className="grid gap-4" onSubmit={submit} noValidate>
          {server ? <FormErrors ref={errorRef} title={server.title} messages={server.messages} /> : null}
          {store === StoreKind.Apple ? (
            <AppleKeyFields
              issuerId={issuerId}
              keyId={keyId}
              onIssuerId={setIssuerId}
              onKeyId={setKeyId}
              errors={shown}
              issuerHint={suffix ? interpolate(t.connections.replaceIssuerHint, { suffix }) : undefined}
            />
          ) : null}
          <ConnectionKeyInput
            store={store}
            value={key}
            onChange={(next) => {
              setKey(next)
              if (next.fileName) {
                setFileError(null)
              }
            }}
            onFile={takeFileName}
            onFileRejected={setFileError}
            error={shown.secret}
          />
          <Callout tone="info">{t.connections.replaceNote}</Callout>
          <p className="m-0 text-xs text-fg3">{t.connections.neverShown}</p>
        </form>
      ) : null}
    </Modal>
  )
}
