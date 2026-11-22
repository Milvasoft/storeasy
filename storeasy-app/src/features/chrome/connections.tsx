import { Callout } from '@/components/ui/Feedback'
import { Select } from '@/components/ui/Select'
import { Field, Input, PasswordInput, Textarea } from '@/components/ui/primitives'
import type { ChromeDictionary } from './i18n/en'
import { useChrome } from './i18n/useChrome'
import { credentialProblems, type ChromeAuth, type ChromeCredentialFields, type ChromeCredentialProblem } from './rules'

// The Chrome Web Store parts of the store connections screen: the publisher id, a service account JSON key or an OAuth
// client with a refresh token, and an optional extension id the verification reads (the API has no call that reads a
// publisher). The API checks the credential against Google when the connection is verified.

export type ChromeFieldErrors = Partial<Record<ChromeCredentialProblem, string | null>>

/** Field errors of the Chrome form, in the dictionary's words. */
export function chromeFieldErrors(fields: ChromeCredentialFields, t: ChromeDictionary): ChromeFieldErrors {
  const problems = credentialProblems(fields)
  return Object.fromEntries(problems.map((problem) => [problem, t.connection.problem[problem]]))
}

const NO_MANAGER = { autoComplete: 'off', 'data-1p-ignore': true, 'data-lpignore': 'true', spellCheck: false } as const

/** The publisher id, the credential and the optional item to verify with. */
export function ChromeCredentialForm({
  fields,
  onChange,
  errors,
}: {
  fields: ChromeCredentialFields
  onChange: (fields: ChromeCredentialFields) => void
  errors: ChromeFieldErrors
}) {
  const t = useChrome()
  return (
    <div className="grid gap-4">
      <Field label={t.connection.publisherId} hint={t.connection.publisherIdHint} error={errors.publisherId}>
        <Input
          mono
          value={fields.publisherId}
          name="storeasy-chrome-publisher"
          maxLength={100}
          {...NO_MANAGER}
          onChange={(event) => onChange({ ...fields, publisherId: event.target.value })}
        />
      </Field>
      <Field label={t.connection.auth}>
        <Select<ChromeAuth>
          aria-label={t.connection.auth}
          value={fields.auth}
          options={[
            { value: 'serviceAccount', label: t.connection.serviceAccount },
            { value: 'oauth', label: t.connection.oauth },
          ]}
          onChange={(auth) => onChange({ ...fields, auth })}
        />
      </Field>
      {fields.auth === 'serviceAccount' ? (
        <Field label={t.connection.serviceAccountKey} hint={t.connection.serviceAccountKeyHint} error={errors.serviceAccountKey}>
          <Textarea
            rows={5}
            className="mono"
            value={fields.serviceAccountKey}
            name="storeasy-chrome-key"
            {...NO_MANAGER}
            placeholder='{ "type": "service_account", ... }'
            onChange={(event) => onChange({ ...fields, serviceAccountKey: event.target.value })}
          />
        </Field>
      ) : (
        <>
          <Field label={t.connection.clientId} hint={t.connection.clientIdHint} error={errors.clientId}>
            <Input
              mono
              value={fields.clientId}
              name="storeasy-chrome-client"
              maxLength={255}
              {...NO_MANAGER}
              onChange={(event) => onChange({ ...fields, clientId: event.target.value })}
            />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t.connection.clientSecret} error={errors.clientSecret}>
              <PasswordInput
                value={fields.clientSecret}
                name="storeasy-chrome-client-secret"
                autoComplete="new-password"
                data-1p-ignore
                data-lpignore="true"
                maxLength={255}
                onChange={(event) => onChange({ ...fields, clientSecret: event.target.value })}
              />
            </Field>
            <Field label={t.connection.refreshToken} hint={t.connection.refreshTokenHint} error={errors.refreshToken}>
              <PasswordInput
                value={fields.refreshToken}
                name="storeasy-chrome-refresh-token"
                autoComplete="new-password"
                data-1p-ignore
                data-lpignore="true"
                maxLength={1024}
                onChange={(event) => onChange({ ...fields, refreshToken: event.target.value })}
              />
            </Field>
          </div>
        </>
      )}
      <Field label={t.connection.itemId} hint={t.connection.itemIdHint} error={errors.itemId} optional>
        <Input
          mono
          value={fields.itemId}
          name="storeasy-chrome-item"
          maxLength={32}
          {...NO_MANAGER}
          onChange={(event) => onChange({ ...fields, itemId: event.target.value })}
        />
      </Field>
      <Callout tone="info">{t.connection.note}</Callout>
    </div>
  )
}
