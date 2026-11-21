import { useRef, useState, type FormEvent, type ReactNode } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, CircleCheck } from 'lucide-react'
import { StoreTag } from '@/components/ui/Badge'
import { STORE_LOGO_ICON, type StoreIconKey } from '@/components/ui/StoreIcons'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Button, Field, Input } from '@/components/ui/primitives'
import { connectionsApi } from '@/lib/api/endpoints'
import { ApiError, apiErrorMessage } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { ConnectionStatus, StoreKind, type DateOnly, type StoreConnectionDto } from '@/lib/api/types'
import { appleSecret, checkP8, ISSUER_ID, KEY_ID, keyIdFromFileName, readServiceAccount } from '@/lib/connections/secrets'
import { useT } from '@/lib/i18n/I18nContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { EMPTY_CREDENTIALS, MsCredentialForm, msFieldErrors } from '@/features/microsoft/connections'
import { useMs } from '@/features/microsoft/i18n/useMs'
import { defaultExpiry, expiryInstant, msSecret, type MsCredentialFields } from '@/features/microsoft/rules'
import { AppleKeyFields, ConnectionKeyInput, EMPTY_KEY, type KeyText } from './ConnectionKeyInput'
import { ConnectionStatusBadge } from './ConnectionStatusBadge'
import { StoreIconPicker } from './StoreIconPicker'

type Step = 'pick' | 'form' | 'result'

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
  const [name, setName] = useState('')
  // The store's own logo is suggested; "none" keeps the text code.
  const [icon, setIcon] = useState<StoreIconKey | null>(STORE_LOGO_ICON[StoreKind.Apple])
  const [issuerId, setIssuerId] = useState('')
  const [keyId, setKeyId] = useState('')
  const [key, setKey] = useState<KeyText>(EMPTY_KEY)
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
    setName('')
    setIcon(STORE_LOGO_ICON[store])
    setIssuerId('')
    setKeyId('')
    setKey(EMPTY_KEY)
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

  const save = useMutation({
    mutationFn: async () => {
      const microsoft = store === StoreKind.Microsoft
      const secret = store === StoreKind.Apple ? appleSecret(issuerId, keyId, key.text) : microsoft ? msSecret(ms) : key.text.trim()
      const id = await connectionsApi.create(organizationId, {
        Store: store,
        Name: name.trim(),
        Secret: secret,
        CredentialExpiryDate: microsoft && expiry ? expiryInstant(expiry) : null,
        Icon: icon,
      })
      // Saved as NotVerified; the check runs at once and its answer is shown here (design 14.10).
      try {
        return await connectionsApi.verify(organizationId, id)
      } catch {
        return { Id: id, Store: store, Name: name.trim(), Icon: icon, Status: ConnectionStatus.NotVerified } satisfies StoreConnectionDto
      }
    },
    meta: { silentError: true },
    onSuccess: (connection) => {
      setKey(EMPTY_KEY)
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
      setKey(EMPTY_KEY)
      setMs((current) => ({ ...current, clientSecret: '' }))
      window.setTimeout(() => errorRef.current?.focus(), 0)
    },
  })

  const takeKey = (next: KeyText) => {
    setKey(next)
    if (next.fileName) {
      setErrors((current) => ({ ...current, secret: null }))
    }
  }

  const takeFileName = (fileName: string) => {
    if (store === StoreKind.Apple) {
      const fromName = keyIdFromFileName(fileName)
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
      const p8 = key.text ? checkP8(key.text) : 'notPem'
      next.secret = !key.text ? t.connections.keyRequired : p8 ? t.connections.p8Invalid : null
    } else if (store === StoreKind.Microsoft) {
      Object.assign(next, msFieldErrors(ms, expiry, tm))
    } else {
      const problem = key.text ? readServiceAccount(key.text).problem : 'notJson'
      next.secret = !key.text ? t.connections.jsonRequired : problem ? t.connections.json[problem] : null
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

  const pickStore = (next: StoreKind) => {
    setStore(next)
    setIcon(STORE_LOGO_ICON[next])
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
            onSelect={() => pickStore(StoreKind.Apple)}
          />
          <StoreCard
            store={StoreKind.Google}
            title={t.connections.stores.google.title}
            body={t.connections.stores.google.body}
            time={t.connections.stores.google.time}
            selected={store === StoreKind.Google}
            onSelect={() => pickStore(StoreKind.Google)}
          />
          <StoreCard
            store={StoreKind.Microsoft}
            title={t.connections.stores.microsoft.title}
            body={t.connections.stores.microsoft.body}
            time={t.connections.stores.microsoft.time}
            selected={store === StoreKind.Microsoft}
            onSelect={() => pickStore(StoreKind.Microsoft)}
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
          <StoreIconPicker store={store} value={icon} onChange={setIcon} />
          {store === StoreKind.Apple ? (
            <AppleKeyFields issuerId={issuerId} keyId={keyId} onIssuerId={setIssuerId} onKeyId={setKeyId} errors={shown} />
          ) : null}
          {store === StoreKind.Microsoft ? (
            <>
              <MsCredentialForm fields={ms} onChange={setMs} expiry={expiry} onExpiry={setExpiry} errors={shown} />
              <Callout tone="info">{tm.connection.roleNote}</Callout>
            </>
          ) : (
            <ConnectionKeyInput
              store={store}
              value={key}
              onChange={takeKey}
              onFile={takeFileName}
              onFileRejected={(message) => setErrors((current) => ({ ...current, secret: message }))}
              error={shown.secret}
            />
          )}
          <p className="m-0 text-xs text-fg3">{t.connections.neverShown}</p>
        </form>
      ) : null}

      {step === 'result' && result ? (
        <div className="grid gap-3">
          <div className="flex flex-wrap items-center gap-2">
            <CircleCheck className="h-5 w-5 text-success" aria-hidden />
            <StoreTag store={result.Store} connectionId={result.Id} codeOnly />
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
