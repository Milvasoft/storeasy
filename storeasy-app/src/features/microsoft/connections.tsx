import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { DatePicker } from '@/components/ui/DatePicker'
import { Banner, Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Button, Field, Input, PasswordInput } from '@/components/ui/primitives'
import { ApiError, apiErrorMessage } from '@/lib/api/http'
import { StoreKind, type DateOnly, type StoreConnectionDto } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { msApi } from './api'
import type { MsDictionary } from './i18n/en'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import {
  MS_SECRET_WARN_DAYS,
  credentialProblems,
  daysUntil,
  defaultExpiry,
  expiryBounds,
  expiryInstant,
  expiryLevel,
  expiryValid,
  msSecret,
  sameIdentity,
  type MsCredentialFields,
} from './rules'

// The Microsoft parts of the store connections screen (design 14.7, 14.13, 14.14): the Entra app form with the secret's
// expiry (at most 24 months), the fingerprint with its expiry, renewing the secret, and the expiring-soon banner.

export const EMPTY_CREDENTIALS: MsCredentialFields = { tenantId: '', clientId: '', clientSecret: '', sellerId: '' }

export type MsFieldErrors = Partial<Record<keyof MsCredentialFields | 'expiry', string | null>>

/** Field errors of the Entra form, in the dictionary's words. */
export function msFieldErrors(fields: MsCredentialFields, expiry: DateOnly | null, t: MsDictionary): MsFieldErrors {
  const problems = credentialProblems(fields)
  return {
    tenantId: problems.includes('tenantId') ? t.connection.problem.tenantId : null,
    clientId: problems.includes('clientId') ? t.connection.problem.clientId : null,
    clientSecret: problems.includes('clientSecret') ? t.connection.problem.clientSecret : null,
    sellerId: problems.includes('sellerId') ? t.connection.problem.sellerId : null,
    expiry: expiryValid(expiry) ? null : t.connection.problem.expiry,
  }
}

/** The Entra app fields and the secret's expiry date (design 14.7). */
export function MsCredentialForm({
  fields,
  onChange,
  expiry,
  onExpiry,
  errors,
  secretLabel,
  expiryLabel,
}: {
  fields: MsCredentialFields
  onChange: (fields: MsCredentialFields) => void
  expiry: DateOnly | null
  onExpiry: (day: DateOnly | null) => void
  errors: MsFieldErrors
  secretLabel?: string
  expiryLabel?: string
}) {
  const t = useMs()
  const bounds = expiryBounds()
  const set = (key: keyof MsCredentialFields) => (value: string) => onChange({ ...fields, [key]: value })
  return (
    <div className="grid gap-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label={t.connection.tenantId} hint={t.connection.tenantHint} error={errors.tenantId}>
          <Input
            mono
            value={fields.tenantId}
            name="storeasy-ms-tenantid"
            autoComplete="off"
            data-1p-ignore
            data-lpignore="true"
            spellCheck={false}
            onChange={(event) => set('tenantId')(event.target.value)}
          />
        </Field>
        <Field label={t.connection.clientId} hint={t.connection.clientHint} error={errors.clientId}>
          <Input
            mono
            value={fields.clientId}
            name="storeasy-ms-clientid"
            autoComplete="off"
            data-1p-ignore
            data-lpignore="true"
            spellCheck={false}
            onChange={(event) => set('clientId')(event.target.value)}
          />
        </Field>
      </div>
      <Field label={secretLabel ?? t.connection.clientSecret} hint={t.connection.clientSecretHint} error={errors.clientSecret}>
        <PasswordInput
          value={fields.clientSecret}
          // new-password keeps password managers from filling the saved storeasy sign-in into the Entra secret.
          name="storeasy-ms-client-secret"
          autoComplete="new-password"
          data-1p-ignore
          data-lpignore="true"
          maxLength={512}
          onChange={(event) => set('clientSecret')(event.target.value)}
        />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label={t.connection.sellerId} hint={t.connection.sellerHint} error={errors.sellerId}>
          <Input
            mono
            inputMode="numeric"
            value={fields.sellerId}
            name="storeasy-ms-sellerid"
            autoComplete="off"
            data-1p-ignore
            data-lpignore="true"
            onChange={(event) => set('sellerId')(event.target.value.trim())}
          />
        </Field>
        <Field label={expiryLabel ?? t.connection.expiry} hint={t.connection.expiryHint} error={errors.expiry}>
          <DatePicker
            value={expiry}
            min={bounds.min}
            max={bounds.max}
            onChange={onExpiry}
            aria-label={expiryLabel ?? t.connection.expiry}
          />
        </Field>
      </div>
    </div>
  )
}

/** The badge of a secret's expiry: expired, expiring within 30 days, or nothing. */
export function SecretExpiryBadge({ connection }: { connection: StoreConnectionDto }) {
  const t = useMs()
  const expiry = connection.CredentialExpiryDate ?? connection.Fingerprint?.SecretExpiryDate
  if (connection.Store !== StoreKind.Microsoft || !expiry) {
    return null
  }
  const days = daysUntil(expiry)
  const level = expiryLevel(days)
  if (level === 'ok') {
    return null
  }
  return (
    <Badge small tone={level === 'expired' ? 'danger' : 'warning'}>
      {level === 'expired' ? t.connection.expired : interpolate(t.connection.expiresSoon, { days })}
    </Badge>
  )
}

/** The fingerprint of a Microsoft connection: tenant and client suffixes, seller id, secret expiry. */
export function MsFingerprint({ connection }: { connection: StoreConnectionDto }) {
  const t = useMs()
  const format = useFormat()
  const fingerprint = connection.Fingerprint ?? {}
  const expiry = connection.CredentialExpiryDate ?? fingerprint.SecretExpiryDate
  const days = expiry ? daysUntil(expiry) : 0
  const rows: Array<[string, string | null | undefined]> = [
    [t.connection.tenant, fingerprint.TenantIdSuffix ? `…${fingerprint.TenantIdSuffix}` : null],
    [t.connection.client, fingerprint.ClientIdSuffix ? `…${fingerprint.ClientIdSuffix}` : null],
    [t.connection.seller, fingerprint.SellerId],
    [
      t.connection.secretExpiry,
      expiry
        ? days < 0
          ? interpolate(t.connection.expiredOn, { date: format.date(expiry) })
          : interpolate(t.connection.expiresIn, { date: format.date(expiry), days })
        : null,
    ],
  ]
  return (
    <dl className="kv">
      {rows.map(([label, value]) => (
        <div key={label} className="contents">
          <dt>{label}</dt>
          <dd className="mono text-[13px]">{value || '-'}</dd>
        </div>
      ))}
    </dl>
  )
}

/** Whether a connection's secret expires within 30 days or already did. */
export function secretExpiring(connection: StoreConnectionDto): boolean {
  const expiry = connection.CredentialExpiryDate ?? connection.Fingerprint?.SecretExpiryDate
  return connection.Store === StoreKind.Microsoft && !!expiry && expiryLevel(daysUntil(expiry)) !== 'ok'
}

/**
 * Renewing the client secret (design 14.14): the new secret is verified against Microsoft first and replaces the old one
 * only when that works. Tenant, client and seller stay the connection's own (MsSecretMismatch otherwise). Render it with
 * key={connection.Id}, so the form starts from the connection it renews.
 */
export function RenewSecretDialog({
  connection,
  organizationId,
  onClose,
}: {
  connection: StoreConnectionDto | null
  organizationId: number
  onClose: () => void
}) {
  const t = useMs()
  const base = useT()
  const queryClient = useQueryClient()
  const fingerprint = connection?.Fingerprint ?? {}
  // The seller id is shown in the fingerprint, so it comes filled; the rest is typed again.
  const initial: MsCredentialFields = { ...EMPTY_CREDENTIALS, sellerId: fingerprint.SellerId ?? '' }
  const [fields, setFields] = useState<MsCredentialFields>(initial)
  const [expiry, setExpiry] = useState<DateOnly | null>(defaultExpiry())
  const [submitted, setSubmitted] = useState(false)
  const [server, setServer] = useState<{ title: string; messages: string[] } | null>(null)

  const close = () => {
    // The secret leaves the form and the memory with the dialog.
    setFields(initial)
    setExpiry(defaultExpiry())
    setSubmitted(false)
    setServer(null)
    onClose()
  }

  const rotate = useMutation({
    mutationFn: () => msApi.rotateSecret(organizationId, connection?.Id ?? 0, msSecret(fields), expiryInstant(expiry ?? '')),
    meta: { silentError: true },
    onSuccess: (updated) => {
      queryClient.setQueryData<StoreConnectionDto[]>(queryKeys.connections.list(organizationId), (list) =>
        list?.map((item) => (item.Id === updated.Id ? updated : item)),
      )
      void queryClient.invalidateQueries({ queryKey: msKeys.expiring(organizationId) })
      showToast(t.connection.renewed, 'success', updated.Name)
      close()
    },
    onError: (failure) => {
      const messages = failure instanceof ApiError ? failure.messages : []
      setServer({ title: apiErrorMessage(failure, base.connections.saveFailed), messages: messages.length > 1 ? messages : [] })
      setFields((current) => ({ ...current, clientSecret: '' }))
    },
  })

  // Field errors follow the values once the form was submitted, so a corrected value clears its message at once.
  const validate = (): MsFieldErrors => {
    const next = msFieldErrors(fields, expiry, t)
    if (!next.tenantId && !next.clientId && !sameIdentity(fields, fingerprint.TenantIdSuffix, fingerprint.ClientIdSuffix)) {
      next.clientId = t.connection.problem.mismatch
    }
    return next
  }
  const errors: MsFieldErrors = submitted ? validate() : {}

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const next = validate()
    setSubmitted(true)
    setServer(null)
    if (!Object.values(next).some(Boolean)) {
      rotate.mutate()
    }
  }

  return (
    <Modal
      open={!!connection}
      onClose={close}
      size="lg"
      title={t.connection.renewTitle}
      description={interpolate(t.connection.renewLead, {
        name: connection?.Name ?? '',
        tenant: fingerprint.TenantIdSuffix ?? '-',
        client: fingerprint.ClientIdSuffix ?? '-',
      })}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {base.common.cancel}
          </Button>
          <Button type="submit" form="ms-renew-secret" variant="primary" loading={rotate.isPending}>
            {t.connection.renewSubmit}
          </Button>
        </>
      }
    >
      <form id="ms-renew-secret" className="grid gap-4" noValidate onSubmit={submit}>
        {server ? <FormErrors title={server.title} messages={server.messages} /> : null}
        <MsCredentialForm
          fields={fields}
          onChange={setFields}
          expiry={expiry}
          onExpiry={setExpiry}
          errors={errors}
          secretLabel={t.connection.newSecret}
          expiryLabel={t.connection.newExpiry}
        />
        <Callout tone="info">{t.connection.renewNote}</Callout>
      </form>
    </Modal>
  )
}

/** The renew button of a Microsoft connection card. */
export function RenewSecretButton({ onClick }: { onClick: () => void }) {
  const t = useMs()
  return (
    <Button size="sm" onClick={onClick}>
      <KeyRound aria-hidden />
      {t.connection.renew}
    </Button>
  )
}

/** The page strip of Microsoft secrets that expire within 30 days (or did); asked only when a Microsoft connection exists. */
export function MsExpiringBanner({
  organizationId,
  enabled,
  onRenew,
}: {
  organizationId: number
  enabled: boolean
  onRenew?: (connectionId: number) => void
}) {
  const t = useMs()
  const format = useFormat()
  const expiring = useQuery({
    queryKey: msKeys.expiring(organizationId),
    queryFn: () => msApi.expiringConnections(organizationId, MS_SECRET_WARN_DAYS),
    enabled,
  })
  const list = expiring.data ?? []
  if (!enabled || list.length === 0) {
    return null
  }
  const first = list[0]
  const text =
    list.length > 1
      ? interpolate(t.connection.banner.many, { count: list.length })
      : interpolate(first.DaysLeft < 0 ? t.connection.banner.expired : t.connection.banner.one, {
          name: first.Name,
          date: format.date(first.CredentialExpiryDate),
          days: first.DaysLeft,
        })
  return (
    <div className="mb-4">
      <Banner
        tone={list.some((item) => item.DaysLeft < 0) ? 'danger' : 'warning'}
        role="alert"
        action={
          onRenew && list.length === 1 ? (
            <Button size="sm" variant="inverse" onClick={() => onRenew(first.ConnectionId)}>
              {t.connection.renew}
            </Button>
          ) : undefined
        }
      >
        {text}
      </Banner>
    </div>
  )
}
