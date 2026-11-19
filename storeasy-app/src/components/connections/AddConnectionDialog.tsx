import { useRef, useState, type FormEvent, type ReactNode } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, CircleCheck, FileKey } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { FileDrop } from '@/components/ui/FileDrop'
import { Modal } from '@/components/ui/Modal'
import { SegmentedControl } from '@/components/ui/Tabs'
import { Button, Field, Input, Textarea } from '@/components/ui/primitives'
import { connectionsApi } from '@/lib/api/endpoints'
import { ApiError, apiErrorMessage } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { ConnectionStatus, StoreKind, type DateOnly, type StoreConnectionDto } from '@/lib/api/types'
import { appleSecret, checkP8, ISSUER_ID, KEY_ID, keyIdFromFileName, readServiceAccount } from '@/lib/connections/secrets'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { EMPTY_CREDENTIALS, MsCredentialForm, msFieldErrors } from '@/features/microsoft/connections'
import { useMs } from '@/features/microsoft/i18n/useMs'
import { defaultExpiry, expiryInstant, msSecret, type MsCredentialFields } from '@/features/microsoft/rules'
import { ConnectionStatusBadge } from './ConnectionStatusBadge'

type Step = 'pick' | 'form' | 'result'
type Source = 'file' | 'paste'

const MAX_FILE_BYTES = TEXT_LIMITS.connectionSecret

async function readFile(file: File): Promise<string> {
  return file.text()
}

function StoreCard({
  store,
  title,
  body,
  time,
  selected,
  disabled,
  badge,
  onSelect,
}: {
  store: StoreKind
  title: string
  body: string
  time: string
  selected: boolean
  disabled?: boolean
  badge?: ReactNode
  onSelect: () => void
}) {
  return (
    <label className={cn('card grid cursor-pointer gap-2 p-4', selected && 'sel', disabled && 'cursor-not-allowed opacity-60')}>
      <span className="flex items-center gap-2">
        <input type="radio" name="store" className="peer sr-only" checked={selected} disabled={disabled} onChange={onSelect} />
        <span className={cn('rad', selected && 'on')} aria-hidden />
        <StoreTag store={store} />
        {badge}
        <span className="mono ml-auto text-[11px] text-fg3">{time}</span>
      </span>
      <b className="text-[14px]">{title}</b>
      <span className="text-[13px] text-fg2">{body}</span>
    </label>
  )
}

/**
 * Adding a store connection (design 14.4-14.10): pick the store, give the key as a file or pasted text, check it offline,
 * save it (encrypted on the API, never shown again) and verify it right away; the result shows in the same dialog.
 */
export function AddConnectionDialog({
  open,
  onClose,
  organizationId,
  onAdded,
}: {
  open: boolean
  onClose: () => void
  organizationId: number
  onAdded?: (connection: StoreConnectionDto) => void
}) {
  const t = useT()
  const tm = useMs()
  const queryClient = useQueryClient()
  const [step, setStep] = useState<Step>('pick')
  const [store, setStore] = useState<StoreKind>(StoreKind.Apple)
  const [source, setSource] = useState<Source>('file')
  const [name, setName] = useState('')
  const [issuerId, setIssuerId] = useState('')
  const [keyId, setKeyId] = useState('')
  const [secretText, setSecretText] = useState('')
  const [fileName, setFileName] = useState<string | null>(null)
  const [ms, setMs] = useState<MsCredentialFields>(EMPTY_CREDENTIALS)
  const [expiry, setExpiry] = useState<DateOnly | null>(defaultExpiry)
  // Only a rejected file's message is kept here; field errors are computed from the values once the form was submitted,
  // so they clear as soon as a value becomes valid.
  const [errors, setErrors] = useState<Record<string, string | null>>({})
  const [submitted, setSubmitted] = useState(false)
  const [server, setServer] = useState<{ title: string; messages: string[] } | null>(null)
  const [result, setResult] = useState<StoreConnectionDto | null>(null)
  const errorRef = useRef<HTMLDivElement>(null)

  const reset = () => {
    setStep('pick')
    setSource('file')
    setName('')
    setIssuerId('')
    setKeyId('')
    setSecretText('')
    setFileName(null)
    setMs(EMPTY_CREDENTIALS)
    setExpiry(defaultExpiry())
    setErrors({})
    setSubmitted(false)
    setServer(null)
    setResult(null)
  }

  const close = () => {
    // The secret leaves the form and the memory with the dialog (design 14.10).
    reset()
    onClose()
  }

  const google = store === StoreKind.Google ? readServiceAccount(secretText) : null

  const save = useMutation({
    mutationFn: async () => {
      const microsoft = store === StoreKind.Microsoft
      const secret = store === StoreKind.Apple ? appleSecret(issuerId, keyId, secretText) : microsoft ? msSecret(ms) : secretText.trim()
      const id = await connectionsApi.create(organizationId, {
        Store: store,
        Name: name.trim(),
        Secret: secret,
        CredentialExpiryDate: microsoft && expiry ? expiryInstant(expiry) : null,
      })
      // Saved as NotVerified; the check runs at once and its answer is shown here (design 14.10).
      try {
        return await connectionsApi.verify(organizationId, id)
      } catch {
        return { Id: id, Store: store, Name: name.trim(), Status: ConnectionStatus.NotVerified } satisfies StoreConnectionDto
      }
    },
    meta: { silentError: true },
    onSuccess: (connection) => {
      setSecretText('')
      setResult(connection)
      setStep('result')
      onAdded?.(connection)
      void queryClient.invalidateQueries({ queryKey: queryKeys.connections.all })
      showToast(t.connections.added, 'success', connection.Name)
    },
    onError: (error) => {
      const messages = error instanceof ApiError ? error.messages : []
      setServer({
        title: messages.length > 1 ? t.connections.saveFailed : apiErrorMessage(error, t.connections.saveFailed),
        messages: messages.length > 1 ? messages : [],
      })
      // Only the secret is chosen again after a refusal; the other values stay (design 18.17).
      setSecretText('')
      setFileName(null)
      setMs((current) => ({ ...current, clientSecret: '' }))
      window.setTimeout(() => errorRef.current?.focus(), 0)
    },
  })

  const takeFile = async (file: File) => {
    const text = await readFile(file)
    setFileName(file.name)
    setSecretText(text)
    setErrors((current) => ({ ...current, secret: null }))
    if (store === StoreKind.Apple) {
      const fromName = keyIdFromFileName(file.name)
      if (fromName && !keyId) {
        setKeyId(fromName)
      }
    }
  }

  const validate = (): Record<string, string | null> => {
    const next: Record<string, string | null> = { name: name.trim() ? null : t.connections.nameRequired }
    if (store === StoreKind.Apple) {
      next.issuerId = ISSUER_ID.test(issuerId.trim()) ? null : t.connections.issuerInvalid
      next.keyId = KEY_ID.test(keyId.trim().toUpperCase()) ? null : t.connections.keyIdInvalid
      const p8 = secretText ? checkP8(secretText) : 'notPem'
      next.secret = !secretText ? t.connections.keyRequired : p8 ? t.connections.p8Invalid : null
    } else if (store === StoreKind.Microsoft) {
      Object.assign(next, msFieldErrors(ms, expiry, tm))
    } else {
      const problem = secretText ? readServiceAccount(secretText).problem : 'notJson'
      next.secret = !secretText ? t.connections.jsonRequired : problem ? t.connections.json[problem] : null
    }
    return next
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const next = validate()
    setSubmitted(true)
    setErrors({})
    setServer(null)
    if (Object.values(next).some(Boolean)) {
      return
    }
    save.mutate()
  }

  const shown: Record<string, string | null> = submitted ? { ...validate(), ...(errors.secret ? { secret: errors.secret } : {}) } : errors

  const title =
    step === 'pick'
      ? t.connections.addTitle
      : step === 'result'
        ? t.connections.savedTitle
        : store === StoreKind.Apple
          ? t.connections.appleTitle
          : store === StoreKind.Microsoft
            ? tm.connection.title
            : t.connections.googleTitle

  return (
    <Modal
      open={open}
      onClose={close}
      title={title}
      description={step === 'pick' ? t.connections.addStep1 : step === 'form' ? t.connections.addStep2 : undefined}
      size="lg"
      footer={
        step === 'pick' ? (
          <>
            <Button variant="ghost" onClick={close}>
              {t.common.cancel}
            </Button>
            <Button variant="primary" onClick={() => setStep('form')}>
              {t.common.continue}
            </Button>
          </>
        ) : step === 'form' ? (
          <>
            <Button
              variant="ghost"
              className="mr-auto"
              onClick={() => {
                setSubmitted(false)
                setStep('pick')
              }}
            >
              <ArrowLeft aria-hidden />
              {t.common.back}
            </Button>
            <Button type="submit" form="add-connection" variant="primary" loading={save.isPending}>
              {t.connections.save}
            </Button>
          </>
        ) : (
          <Button variant="primary" onClick={close}>
            {t.common.done}
          </Button>
        )
      }
    >
      {step === 'pick' ? (
        <div className="grid gap-3" role="radiogroup" aria-label={t.connections.store}>
          <StoreCard
            store={StoreKind.Apple}
            title={t.connections.stores.apple.title}
            body={t.connections.stores.apple.body}
            time={t.connections.stores.apple.time}
            selected={store === StoreKind.Apple}
            onSelect={() => setStore(StoreKind.Apple)}
          />
          <StoreCard
            store={StoreKind.Google}
            title={t.connections.stores.google.title}
            body={t.connections.stores.google.body}
            time={t.connections.stores.google.time}
            selected={store === StoreKind.Google}
            onSelect={() => setStore(StoreKind.Google)}
          />
          <StoreCard
            store={StoreKind.Microsoft}
            title={t.connections.stores.microsoft.title}
            body={t.connections.stores.microsoft.body}
            time={t.connections.stores.microsoft.time}
            selected={store === StoreKind.Microsoft}
            onSelect={() => setStore(StoreKind.Microsoft)}
          />
          <Callout>{t.connections.encryptedNote}</Callout>
        </div>
      ) : null}

      {step === 'form' ? (
        <form id="add-connection" className="grid gap-4" onSubmit={submit} noValidate>
          {server ? <FormErrors ref={errorRef} title={server.title} messages={server.messages} /> : null}
          <Field label={t.connections.name} error={shown.name} hint={t.connections.nameHint}>
            <Input
              value={name}
              name="storeasy-connection-name"
              autoComplete="off"
              data-1p-ignore
              data-lpignore="true"
              maxLength={TEXT_LIMITS.connectionName}
              onChange={(event) => setName(event.target.value)}
            />
          </Field>
          {store === StoreKind.Apple ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t.connections.issuerId} error={shown.issuerId} hint={t.connections.issuerHint}>
                <Input mono value={issuerId} spellCheck={false} autoComplete="off" onChange={(event) => setIssuerId(event.target.value)} />
              </Field>
              <Field label={t.connections.keyId} error={shown.keyId} hint={t.connections.keyIdHint}>
                <Input
                  mono
                  value={keyId}
                  spellCheck={false}
                  autoComplete="off"
                  maxLength={10}
                  onChange={(event) => setKeyId(event.target.value.toUpperCase())}
                />
              </Field>
            </div>
          ) : null}
          {store === StoreKind.Microsoft ? (
            <>
              <MsCredentialForm fields={ms} onChange={setMs} expiry={expiry} onExpiry={setExpiry} errors={shown} />
              <Callout tone="info">{tm.connection.roleNote}</Callout>
            </>
          ) : (
            <div className="grid gap-2">
              <div className="flex flex-wrap items-center gap-2">
                <span className="lbl">{store === StoreKind.Apple ? t.connections.p8 : t.connections.json.label}</span>
                <span className="ml-auto">
                  <SegmentedControl
                    label={t.connections.sourceLabel}
                    value={source}
                    onChange={(value) => {
                      setSource(value)
                      setSecretText('')
                      setFileName(null)
                    }}
                    options={[
                      { value: 'file', label: t.connections.sourceFile },
                      { value: 'paste', label: t.connections.sourcePaste },
                    ]}
                  />
                </span>
              </div>
              {source === 'file' ? (
                fileName ? (
                  <div className="secret">
                    <FileKey className="h-4 w-4 flex-none text-fg3" aria-hidden />
                    <span className="min-w-0 flex-1 truncate">{fileName}</span>
                    <Badge tone="success" small>
                      {t.connections.fileRead}
                    </Badge>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => {
                        setFileName(null)
                        setSecretText('')
                      }}
                    >
                      {t.connections.changeFile}
                    </Button>
                  </div>
                ) : (
                  <FileDrop
                    label={store === StoreKind.Apple ? t.connections.p8 : t.connections.json.label}
                    accept={store === StoreKind.Apple ? ['.p8'] : ['.json']}
                    maxBytes={MAX_FILE_BYTES}
                    hint={store === StoreKind.Apple ? t.connections.p8Hint : t.connections.json.hint}
                    onFile={(file) => void takeFile(file)}
                    onReject={(file, reason) =>
                      setErrors((current) => ({
                        ...current,
                        secret: interpolate(reason === 'type' ? t.connections.fileWrongType : t.connections.fileTooLarge, {
                          name: file.name,
                        }),
                      }))
                    }
                  />
                )
              ) : (
                <Textarea
                  mono
                  rows={6}
                  aria-label={store === StoreKind.Apple ? t.connections.p8 : t.connections.json.label}
                  value={secretText}
                  maxLength={TEXT_LIMITS.connectionSecret}
                  spellCheck={false}
                  autoComplete="off"
                  placeholder={store === StoreKind.Apple ? '-----BEGIN PRIVATE KEY-----' : '{ "type": "service_account", ... }'}
                  onChange={(event) => setSecretText(event.target.value)}
                />
              )}
              {shown.secret ? <span className="err-t">{shown.secret}</span> : null}
            </div>
          )}
          {google && secretText && (google.info.clientEmail || google.info.projectId) ? (
            <dl className="kv rounded-md border border-border p-3">
              <dt>{t.connections.serviceAccount}</dt>
              <dd className="mono">{google.info.clientEmail ?? '-'}</dd>
              <dt>{t.connections.project}</dt>
              <dd className="mono">{google.info.projectId ?? '-'}</dd>
            </dl>
          ) : null}
          <p className="m-0 text-xs text-fg3">{t.connections.neverShown}</p>
        </form>
      ) : null}

      {step === 'result' && result ? (
        <div className="grid gap-3">
          <div className="flex flex-wrap items-center gap-2">
            <CircleCheck className="h-5 w-5 text-success" aria-hidden />
            <b>{result.Name}</b>
            <ConnectionStatusBadge status={result.Status} />
          </div>
          {result.Status === ConnectionStatus.Verified ? (
            <Callout tone="success">{t.connections.verifiedNote}</Callout>
          ) : result.LastVerifyError ? (
            <Callout tone={result.Status === ConnectionStatus.Unreachable ? 'warning' : 'danger'} title={t.connections.verifyFailed}>
              {result.LastVerifyError}
            </Callout>
          ) : (
            <Callout tone="info">{t.connections.notVerifiedNote}</Callout>
          )}
        </div>
      ) : null}
    </Modal>
  )
}
