import { useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { MonitorSmartphone } from 'lucide-react'
import { PasswordPolicy } from '@/components/auth/PasswordPolicy'
import { ThemeSwitcher } from '@/components/common/ThemeSwitcher'
import { OrgAvatar } from '@/components/org/OrgAvatar'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { FormErrors } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { SegmentedControl, Tabs } from '@/components/ui/Tabs'
import { Avatar, Button, Field, PageHeader, PasswordInput, Skeleton } from '@/components/ui/primitives'
import { authApi } from '@/lib/api/endpoints'
import { ApiError, apiErrorMessage } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { displayName, initialsOf, useAuth } from '@/lib/auth/AuthContext'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { useI18n } from '@/lib/i18n/I18nContext'
import { LANGS } from '@/lib/i18n/lang'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { roleLabel } from '@/lib/org/roles'
import { queryKeys } from '@/lib/query/keys'
import { passwordError } from '@/lib/validation'
import { accountTabItems } from '@/features/account-preferences/NotificationPreferencesPage'
import { useNotificationsText } from '@/features/notifications/i18n'
import { LinkedAccountsCard, ProfileForm, SessionsCard } from '@/features/access/AccountCards'

function AccountTabs() {
  const { t } = useI18n()
  const notifications = useNotificationsText()
  return <Tabs label={t.account.title} items={accountTabItems(t.account, notifications.prefs.tab)} />
}

/** Profile (design 16.3): read from account/detail, saved with PUT account/profile (name, surname, language). */
export function AccountPage() {
  const { t, lang, setLang } = useI18n()
  const { organizations } = useOrganization()
  const detail = useQuery({ queryKey: queryKeys.account.detail(), queryFn: () => authApi.accountDetail() })
  const account = detail.data
  const name = account
    ? displayName({ name: account.Name, surname: account.Surname, email: account.Email, userName: account.UserName })
    : ''

  return (
    <>
      <PageHeader title={t.account.title} subtitle={t.account.subtitle} />
      <AccountTabs />
      {detail.isError ? (
        <ScreenState error={detail.error} onRetry={() => void detail.refetch()} />
      ) : (
        <div className="grid max-w-3xl gap-5">
          {account ? (
            <>
              <div className="flex items-center gap-3">
                <Avatar initials={initialsOf(name)} size="l" />
                <div className="min-w-0">
                  <b className="block truncate text-[15px]">{name}</b>
                  <span className="block truncate text-[13px] text-fg3">{account.Email}</span>
                </div>
              </div>
              {/* Keyed by the saved values, so a refetch after saving resets the form. */}
              <ProfileForm key={`${account.Name ?? ''}|${account.Surname ?? ''}|${account.PreferredLanguage ?? ''}`} account={account} />
            </>
          ) : (
            <Skeleton shape="b" className="h-48 w-full" />
          )}
          <section className="card">
            <div className="card-h">
              <h2 className="h3">{t.account.preferences}</h2>
            </div>
            <div className="card-b grid gap-4">
              <div className="grid gap-2">
                <span className="lbl">{t.theme.label}</span>
                <ThemeSwitcher />
              </div>
              <div className="grid gap-2">
                <span className="lbl">{t.language.label}</span>
                <SegmentedControl
                  label={t.language.label}
                  value={lang}
                  onChange={setLang}
                  options={LANGS.map((value) => ({ value, label: t.language.names[value] }))}
                />
              </div>
              <p className="m-0 text-xs text-fg3">{t.account.preferencesLocal}</p>
            </div>
          </section>
          <section className="card">
            <div className="card-h">
              <h2 className="h3">{t.account.organizations}</h2>
            </div>
            <ul className="m-0 list-none p-0">
              {organizations.map((organization) => (
                <li key={organization.Id} className="lst-i">
                  <OrgAvatar name={organization.Name} logoUrl={organization.LogoUrl} />
                  <span className="min-w-0 flex-1">
                    <b className="block truncate">{organization.Name}</b>
                    <span className="mono block text-xs text-fg3">{organization.Slug}</span>
                  </span>
                  <Badge>{roleLabel(organization.Role, t)}</Badge>
                </li>
              ))}
            </ul>
          </section>
        </div>
      )}
    </>
  )
}

/**
 * Password and sessions (design 16.7, 18.19). Changing the password renews the security stamp: every session, this
 * one included, ends, and the sign-in screen says so. The devices list (AccountSessions flag) signs out one device or
 * every other one; linked accounts show the Google sign in.
 */
export function SecurityPage() {
  const { t } = useI18n()
  const confirm = useConfirm()
  const navigate = useNavigate()
  const { user, endSession } = useAuth()
  const sessionsEnabled = useFeature('AccountSessions')
  const [form, setForm] = useState({ current: '', next: '', repeat: '' })
  const [errors, setErrors] = useState<{ current?: string | null; next?: string | null; repeat?: string | null }>({})
  const [server, setServer] = useState<{ title: string; messages: string[] } | null>(null)
  const errorRef = useRef<HTMLDivElement>(null)

  const change = useMutation({
    mutationFn: () => authApi.changePassword(user?.userName ?? '', form.current, form.next),
    meta: { silentError: true },
    onSuccess: () => {
      endSession()
      navigate('/login?reset=ok', { replace: true })
    },
    onError: (error) => {
      const messages = error instanceof ApiError ? error.messages : []
      setServer({
        title: messages.length > 1 ? t.security.failed : apiErrorMessage(error, t.common.error),
        messages: messages.length > 1 ? messages : [],
      })
      window.setTimeout(() => errorRef.current?.focus(), 0)
    },
  })

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const next = {
      current: form.current ? null : t.security.currentRequired,
      next: passwordError(form.next, t) ?? (form.next === form.current ? t.security.sameAsCurrent : null),
      repeat: form.repeat === form.next ? null : t.reset.mismatch,
    }
    setErrors(next)
    setServer(null)
    if (Object.values(next).some(Boolean)) {
      return
    }
    // A sign-out action says so before it runs (design 18.19); nothing is deleted, so the button is not red.
    if (await confirm({ title: t.security.confirmTitle, message: t.security.confirmBody, confirmLabel: t.security.submit })) {
      change.mutate()
    }
  }

  return (
    <>
      <PageHeader title={t.account.title} subtitle={t.account.subtitle} />
      <AccountTabs />
      <div className="grid max-w-3xl gap-5">
        <form className="card" onSubmit={(event) => void submit(event)} noValidate>
          <div className="card-h">
            <h2 className="h3">{t.security.passwordTitle}</h2>
          </div>
          <div className="card-b grid gap-4">
            {server ? <FormErrors ref={errorRef} title={server.title} messages={server.messages} /> : null}
            <Field label={t.security.current} error={errors.current}>
              <PasswordInput
                autoComplete="current-password"
                value={form.current}
                maxLength={TEXT_LIMITS.password}
                onChange={(event) => setForm({ ...form, current: event.target.value })}
              />
            </Field>
            <Field label={t.reset.newPassword} error={errors.next} hint={<PasswordPolicy value={form.next} />}>
              <PasswordInput
                autoComplete="new-password"
                value={form.next}
                maxLength={TEXT_LIMITS.password}
                onChange={(event) => setForm({ ...form, next: event.target.value })}
              />
            </Field>
            <Field label={t.reset.repeat} error={errors.repeat}>
              <PasswordInput
                autoComplete="new-password"
                value={form.repeat}
                maxLength={TEXT_LIMITS.password}
                onChange={(event) => setForm({ ...form, repeat: event.target.value })}
              />
            </Field>
            <p className="m-0 text-[13px] text-fg2">{t.security.signOutNote}</p>
          </div>
          <div className="card-f justify-end">
            <Button type="submit" variant="primary" loading={change.isPending}>
              {t.security.submit}
            </Button>
          </div>
        </form>
        {sessionsEnabled ? (
          <SessionsCard />
        ) : (
          <section className="card dash">
            <div className="card-h">
              <MonitorSmartphone className="h-4 w-4 text-fg3" aria-hidden />
              <h2 className="h3">{t.security.sessionsTitle}</h2>
              <Badge small>{t.coming.short}</Badge>
            </div>
            <p className="card-b m-0 text-[13px] text-fg2">{t.security.sessionsBody}</p>
          </section>
        )}
        <LinkedAccountsCard />
      </div>
    </>
  )
}
