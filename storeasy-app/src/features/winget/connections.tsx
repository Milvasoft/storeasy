import { DatePicker } from '@/components/ui/DatePicker'
import { Callout } from '@/components/ui/Feedback'
import { Field, Input, PasswordInput } from '@/components/ui/primitives'
import type { DateOnly } from '@/lib/api/types'
import { expiryBounds, expiryValid } from '@/features/microsoft/rules'
import type { WingetDictionary } from './i18n/en'
import { useWinget } from './i18n/useWinget'
import { credentialProblems, type WingetCredentialFields } from './rules'

// The winget parts of the store connections screen: the GitHub token, the fork of winget-pkgs and the token's optional
// expiry (classic tokens may have none). The API checks the token against GitHub when the connection is verified.

export type WingetFieldErrors = Partial<Record<keyof WingetCredentialFields | 'expiry', string | null>>

/** Field errors of the winget form, in the dictionary's words. */
export function wingetFieldErrors(fields: WingetCredentialFields, expiry: DateOnly | null, t: WingetDictionary): WingetFieldErrors {
  const problems = credentialProblems(fields)
  return {
    token: problems.includes('token') ? t.connection.problem.token : null,
    fork: problems.includes('fork') ? t.connection.problem.fork : null,
    expiry: expiry && !expiryValid(expiry) ? t.connection.problem.expiry : null,
  }
}

/** The GitHub token, the fork and the token's expiry. */
export function WingetCredentialForm({
  fields,
  onChange,
  expiry,
  onExpiry,
  errors,
  tokenLabel,
}: {
  fields: WingetCredentialFields
  onChange: (fields: WingetCredentialFields) => void
  expiry: DateOnly | null
  onExpiry: (day: DateOnly | null) => void
  errors: WingetFieldErrors
  tokenLabel?: string
}) {
  const t = useWinget()
  const bounds = expiryBounds()
  return (
    <div className="grid gap-4">
      <Field label={tokenLabel ?? t.connection.token} hint={t.connection.tokenHint} error={errors.token}>
        <PasswordInput
          value={fields.token}
          // new-password keeps password managers from filling the saved storeasy sign-in into the token.
          name="storeasy-winget-token"
          autoComplete="new-password"
          data-1p-ignore
          data-lpignore="true"
          maxLength={255}
          onChange={(event) => onChange({ ...fields, token: event.target.value })}
        />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label={t.connection.fork} hint={t.connection.forkHint} error={errors.fork}>
          <Input
            mono
            value={fields.fork}
            name="storeasy-winget-fork"
            autoComplete="off"
            data-1p-ignore
            data-lpignore="true"
            spellCheck={false}
            placeholder="octocat/winget-pkgs"
            onChange={(event) => onChange({ ...fields, fork: event.target.value })}
          />
        </Field>
        <Field label={t.connection.expiry} hint={t.connection.expiryHint} error={errors.expiry} optional>
          <DatePicker value={expiry} min={bounds.min} max={bounds.max} onChange={onExpiry} aria-label={t.connection.expiry} />
        </Field>
      </div>
      <Callout tone="info">{t.connection.note}</Callout>
    </div>
  )
}
