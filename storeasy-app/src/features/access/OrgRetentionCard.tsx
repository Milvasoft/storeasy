import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { FormErrors } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, Field, Input, Skeleton } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission, type OrganizationSettingsDto, type UpdateOrganizationSettingsInput } from '@/lib/api/types'
import { useI18n } from '@/lib/i18n/I18nContext'
import { cultureOf } from '@/lib/i18n/lang'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { useAccessText } from './i18n'
import { orgRoleKeys } from './roles'

// Design 15.20: retention and the default language (GET / PUT organizations/settings). Member managers read, owners
// (ManageOrganization) change; only the changed fields are sent.

/** Organization.AuditRetentionChoices. */
export const AUDIT_RETENTION_CHOICES = [90, 180, 365, 730] as const

// The store languages offered first; any code the validator accepts can be typed instead.
const LOCALES = ['tr-TR', 'en-US', 'en-GB', 'de-DE', 'fr-FR', 'es-ES', 'it-IT', 'nl-NL', 'pt-BR', 'ru', 'ar-SA', 'ja', 'ko', 'zh-Hans']
const LOCALE_PATTERN = /^[a-z]{2,3}(-[A-Za-z0-9]{2,8}){0,2}$/

function localeName(code: string, culture: string): string {
  try {
    const name = new Intl.DisplayNames([culture], { type: 'language' }).of(code)
    return name && name !== code ? `${name} (${code})` : code
  } catch {
    return code
  }
}

function RetentionForm({ settings, editable }: { settings: OrganizationSettingsDto; editable: boolean }) {
  const t = useAccessText()
  const { t: base, lang } = useI18n()
  const queryClient = useQueryClient()
  const organization = useCurrentOrganization()
  const [days, setDays] = useState(settings.AuditRetentionDays)
  const [locale, setLocale] = useState(settings.DefaultLocale ?? '')
  const [localeError, setLocaleError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)
  const culture = cultureOf(lang)

  const changes: UpdateOrganizationSettingsInput = {}
  if (days !== settings.AuditRetentionDays) {
    changes.AuditRetentionDays = days
  }
  if (locale.trim() !== (settings.DefaultLocale ?? '')) {
    changes.DefaultLocale = locale.trim()
  }
  const count = Object.keys(changes).length

  const save = useMutation({
    mutationFn: () => organizationsApi.updateSettings(organization.Id, changes),
    meta: { silentError: true },
    onSuccess: () => {
      setServer(null)
      showToast(t.orgSettings.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: orgRoleKeys.settings(organization.Id) })
    },
    onError: (error) => setServer(apiErrorMessage(error, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem =
      typeof changes.DefaultLocale === 'string' && !LOCALE_PATTERN.test(changes.DefaultLocale) ? t.orgSettings.localeInvalid : null
    setLocaleError(problem)
    if (!problem && count > 0) {
      save.mutate()
    }
  }

  const localeOptions = [...new Set([...(settings.DefaultLocale ? [settings.DefaultLocale] : []), ...LOCALES])].map((code) => ({
    value: code,
    label: localeName(code, culture),
  }))

  return (
    <form className="card" onSubmit={submit} noValidate aria-label={t.orgSettings.retentionTitle}>
      <div className="card-h">
        <h2 className="h3">{t.orgSettings.retentionTitle}</h2>
      </div>
      <div className="card-b grid gap-4">
        {server ? <FormErrors title={server} /> : null}
        <Field label={t.orgSettings.defaultLocale} hint={t.orgSettings.defaultLocaleHint} error={localeError}>
          {editable ? (
            <Select<string> searchable value={locale || null} options={localeOptions} onChange={setLocale} />
          ) : (
            <Input value={locale ? localeName(locale, culture) : '-'} readOnly />
          )}
        </Field>
        <Field label={t.orgSettings.auditRetention} hint={t.orgSettings.auditRetentionHint}>
          {editable ? (
            <Select<number>
              value={days}
              options={AUDIT_RETENTION_CHOICES.map((value) => ({ value, label: interpolate(t.orgSettings.days, { count: value }) }))}
              onChange={setDays}
            />
          ) : (
            <Input value={interpolate(t.orgSettings.days, { count: days })} readOnly />
          )}
        </Field>
        <dl className="kv">
          <dt>{t.orgSettings.artifactRetention}</dt>
          <dd>
            {interpolate(t.orgSettings.days, { count: settings.ArtifactRetentionDays })}{' '}
            <span className="text-xs text-fg3">{t.orgSettings.artifactRetentionHint}</span>
          </dd>
          <dt>{t.orgSettings.notificationRetention}</dt>
          <dd>
            {interpolate(t.orgSettings.notificationRetentionValue, {
              seen: settings.SeenNotificationRetentionDays,
              unseen: settings.UnseenNotificationRetentionDays,
            })}
          </dd>
        </dl>
        {!editable ? <p className="m-0 text-xs text-fg3">{t.orgSettings.ownerOnly}</p> : null}
      </div>
      {editable ? (
        <div className="card-f justify-end">
          {count > 0 ? <span className="mr-auto text-xs text-fg3">{interpolate(t.orgSettings.changes, { count })}</span> : null}
          <Button
            variant="ghost"
            disabled={count === 0}
            onClick={() => {
              setDays(settings.AuditRetentionDays)
              setLocale(settings.DefaultLocale ?? '')
              setLocaleError(null)
            }}
          >
            {base.common.cancel}
          </Button>
          <Button type="submit" variant="primary" loading={save.isPending} disabled={count === 0}>
            {base.common.save}
          </Button>
        </div>
      ) : null}
    </form>
  )
}

/** The retention card of organization settings: read by member managers, changed by owners. */
export function OrgRetentionCard() {
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const readable = can(OrgPermission.ManageMembers) || can(OrgPermission.ManageOrganization)
  const settings = useQuery({
    queryKey: orgRoleKeys.settings(organization.Id),
    queryFn: () => organizationsApi.settings(organization.Id),
    enabled: readable,
  })
  if (!readable) {
    return null
  }
  if (settings.isLoading) {
    return <Skeleton shape="b" className="h-40 w-full" />
  }
  if (settings.isError) {
    return <ScreenState error={settings.error} onRetry={() => void settings.refetch()} />
  }
  return settings.data ? (
    // Keyed by the saved values, so a refetch after saving resets the form.
    <RetentionForm
      key={`${settings.data.AuditRetentionDays}-${settings.data.DefaultLocale ?? ''}`}
      settings={settings.data}
      editable={can(OrgPermission.ManageOrganization)}
    />
  ) : null
}
