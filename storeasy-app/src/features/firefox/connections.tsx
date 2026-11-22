import { Callout } from '@/components/ui/Feedback'
import { Field, Input, PasswordInput } from '@/components/ui/primitives'
import type { FirefoxDictionary } from './i18n/en'
import { useFirefox } from './i18n/useFirefox'
import { credentialProblems, type FirefoxCredentialFields, type FirefoxCredentialProblem } from './rules'

// The Firefox Add-ons parts of the store connections screen: the AMO API key (JWT issuer and JWT secret) from the Developer
// Hub, Tools, Manage API Keys. The API checks the key against addons.mozilla.org when the connection is verified.

export type FirefoxFieldErrors = Partial<Record<FirefoxCredentialProblem, string | null>>

/** Field errors of the Firefox form, in the dictionary's words. */
export function firefoxFieldErrors(fields: FirefoxCredentialFields, t: FirefoxDictionary): FirefoxFieldErrors {
  const problems = credentialProblems(fields)
  return Object.fromEntries(problems.map((problem) => [problem, t.connection.problem[problem]]))
}

/** The JWT issuer and the JWT secret. */
export function FirefoxCredentialForm({
  fields,
  onChange,
  errors,
}: {
  fields: FirefoxCredentialFields
  onChange: (fields: FirefoxCredentialFields) => void
  errors: FirefoxFieldErrors
}) {
  const t = useFirefox()
  return (
    <div className="grid gap-4">
      <Field label={t.connection.issuer} hint={t.connection.issuerHint} error={errors.issuer}>
        <Input
          mono
          value={fields.issuer}
          name="storeasy-firefox-issuer"
          placeholder="user:12345:67"
          maxLength={40}
          autoComplete="off"
          data-1p-ignore
          data-lpignore="true"
          spellCheck={false}
          onChange={(event) => onChange({ ...fields, issuer: event.target.value })}
        />
      </Field>
      <Field label={t.connection.secret} hint={t.connection.secretHint} error={errors.secret}>
        <PasswordInput
          value={fields.secret}
          // new-password keeps password managers from filling the saved storeasy sign-in into the secret.
          name="storeasy-firefox-secret"
          autoComplete="new-password"
          data-1p-ignore
          data-lpignore="true"
          maxLength={255}
          onChange={(event) => onChange({ ...fields, secret: event.target.value })}
        />
      </Field>
      <Callout tone="info">{t.connection.note}</Callout>
    </div>
  )
}
