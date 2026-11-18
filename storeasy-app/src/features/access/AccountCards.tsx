import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { FormErrors } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, Field, Input, TableSkeleton } from '@/components/ui/primitives'
import { authApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { ExternalLoginProvider, type AccountDetailDto, type AccountSessionDto } from '@/lib/api/types'
import { useAuth } from '@/lib/auth/AuthContext'
import { useFormat, useI18n } from '@/lib/i18n/I18nContext'
import { cultureOf, LANGS, type Lang } from '@/lib/i18n/lang'
import { interpolate } from '@/lib/i18n/interpolate'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { usePublicSettings } from './publicSettings'
import { useAccessText } from './i18n'

// Phase 8 account cards (design 16.3, 16.7): the profile form (PUT account/profile), linked sign in providers and the
// signed in devices (GET account/sessions, DELETE account/session, DELETE account/sessions/others).

/** "Chrome · Windows" from a user agent; null when nothing is recognised. */
export function describeAgent(agent: string | null | undefined): string | null {
  if (!agent) {
    return null
  }
  const browser = /Edg\//.test(agent)
    ? 'Edge'
    : /OPR\//.test(agent)
      ? 'Opera'
      : /Firefox\//.test(agent)
        ? 'Firefox'
        : /Chrome\//.test(agent)
          ? 'Chrome'
          : /Safari\//.test(agent)
            ? 'Safari'
            : null
  const system = /iPhone|iPad/.test(agent)
    ? 'iOS'
    : /Android/.test(agent)
      ? 'Android'
      : /Windows/.test(agent)
        ? 'Windows'
        : /Mac OS X/.test(agent)
          ? 'macOS'
          : /Linux/.test(agent)
            ? 'Linux'
            : null
  const parts = [browser, system].filter(Boolean)
  return parts.length > 0 ? parts.join(' · ') : null
}

/** The interface language of a saved culture ("en-US" is en); null for anything the UI does not speak. */
export function langOfCulture(culture: string | null | undefined): Lang | null {
  const prefix = culture?.slice(0, 2).toLowerCase()
  return LANGS.find((lang) => lang === prefix) ?? null
}

/** Name, surname and the preferred language; the e-mail is the sign in address and stays read only. */
export function ProfileForm({ account }: { account: AccountDetailDto }) {
  const t = useAccessText()
  const { t: base, lang, setLang } = useI18n()
  const format = useFormat()
  const queryClient = useQueryClient()
  const { refreshUser } = useAuth()
  const saved = { name: account.Name ?? '', surname: account.Surname ?? '', language: account.PreferredLanguage ?? cultureOf(lang) }
  const [form, setForm] = useState(saved)
  const [nameError, setNameError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)
  const changes =
    (form.name.trim() !== saved.name ? 1 : 0) + (form.surname.trim() !== saved.surname ? 1 : 0) + (form.language !== saved.language ? 1 : 0)

  const save = useMutation({
    mutationFn: () => authApi.updateProfile({ Name: form.name.trim(), Surname: form.surname.trim() || null, Language: form.language }),
    meta: { silentError: true },
    onSuccess: async () => {
      setServer(null)
      showToast(t.profile.saved, 'success')
      const next = langOfCulture(form.language)
      if (next && next !== lang) {
        setLang(next)
      }
      await queryClient.invalidateQueries({ queryKey: queryKeys.account.detail() })
      await refreshUser()
    },
    onError: (error) => setServer(apiErrorMessage(error, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = form.name.trim() ? null : t.profile.nameRequired
    setNameError(problem)
    if (!problem && changes > 0) {
      save.mutate()
    }
  }

  return (
    <form className="card" onSubmit={submit} noValidate aria-label={t.profile.title}>
      <div className="card-h">
        <h2 className="h3">{base.account.profile}</h2>
      </div>
      <div className="card-b grid gap-4">
        {server ? <FormErrors title={server} /> : null}
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={base.fields.name} error={nameError}>
            <Input
              autoComplete="given-name"
              value={form.name}
              maxLength={TEXT_LIMITS.personName}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
            />
          </Field>
          <Field label={base.fields.surname} optional>
            <Input
              autoComplete="family-name"
              value={form.surname}
              maxLength={TEXT_LIMITS.personName}
              onChange={(event) => setForm({ ...form, surname: event.target.value })}
            />
          </Field>
        </div>
        <Field label={base.fields.email} hint={t.profile.emailLocked}>
          <Input value={account.Email ?? ''} readOnly />
        </Field>
        <Field label={t.profile.language} hint={t.profile.languageHint}>
          <Select<string>
            value={form.language}
            options={LANGS.map((value) => ({ value: cultureOf(value), label: base.language.names[value] }))}
            onChange={(language) => setForm({ ...form, language })}
          />
        </Field>
        {account.CreationDate ? (
          <dl className="kv">
            <dt>{t.profile.memberSince}</dt>
            <dd>{format.date(account.CreationDate)}</dd>
          </dl>
        ) : null}
      </div>
      <div className="card-f justify-end">
        {changes > 0 ? <span className="mr-auto text-xs text-fg3">{interpolate(t.profile.changes, { count: changes })}</span> : null}
        <Button
          variant="ghost"
          disabled={changes === 0}
          onClick={() => {
            setForm(saved)
            setNameError(null)
          }}
        >
          {t.profile.cancel}
        </Button>
        <Button type="submit" variant="primary" loading={save.isPending} disabled={changes === 0}>
          {t.profile.save}
        </Button>
      </div>
    </form>
  )
}

/** Sign in providers besides the password; shown when Google sign in is on or the account is already linked. */
export function LinkedAccountsCard() {
  const t = useAccessText()
  const settings = usePublicSettings()
  const detail = useQuery({ queryKey: queryKeys.account.detail(), queryFn: () => authApi.accountDetail() })
  const linked = (detail.data?.ExternalLogins ?? []).includes(ExternalLoginProvider.Google)
  if (!linked && !settings.data?.GoogleSignInEnabled) {
    return null
  }
  return (
    <section className="card" aria-label={t.linked.title}>
      <div className="card-h">
        <h2 className="h3">{t.linked.title}</h2>
      </div>
      <div className="card-b grid gap-2">
        <p className="m-0 text-[13px] text-fg2">{t.linked.body}</p>
        <div className="flex flex-wrap items-center gap-2 text-[13px]">
          <b className="mr-auto">{t.linked.google}</b>
          <Badge small dot tone={linked ? 'success' : undefined}>
            {linked ? t.linked.linked : t.linked.notLinked}
          </Badge>
        </div>
        {!linked ? <p className="m-0 text-xs text-fg3">{t.linked.linkHint}</p> : null}
      </div>
    </section>
  )
}

export const sessionKeys = { list: () => ['account', 'sessions'] as const }

/** The signed in devices with sign out per device and for every other device. */
export function SessionsCard() {
  const t = useAccessText()
  const base = useI18n().t
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const sessions = useQuery({ queryKey: sessionKeys.list(), queryFn: () => authApi.sessions() })
  const rows = sessions.data ?? []
  const others = rows.filter((session) => !session.IsCurrent)
  const deviceOf = (session: AccountSessionDto) => describeAgent(session.UserAgent) ?? t.sessions.unknownDevice

  const revoke = useMutation({
    mutationFn: (session: AccountSessionDto) => authApi.revokeSession(session.Id),
    onSuccess: () => {
      showToast(t.sessions.revoked, 'success')
      void queryClient.invalidateQueries({ queryKey: sessionKeys.list() })
    },
  })
  const revokeOthers = useMutation({
    mutationFn: () => authApi.revokeOtherSessions(),
    onSuccess: (count) => {
      showToast(interpolate(t.sessions.othersRevoked, { count }), 'success')
      void queryClient.invalidateQueries({ queryKey: sessionKeys.list() })
    },
  })

  const askRevoke = async (session: AccountSessionDto) => {
    const device = deviceOf(session)
    if (
      await confirm({
        title: interpolate(t.sessions.revokeTitle, { device }),
        message: t.sessions.revokeBody,
        confirmLabel: t.sessions.revoke,
      })
    ) {
      revoke.mutate(session)
    }
  }

  const askOthers = async () => {
    if (
      await confirm({
        title: interpolate(t.sessions.othersTitle, { count: others.length }),
        message: t.sessions.othersBody,
        confirmLabel: t.sessions.revokeOthers,
      })
    ) {
      revokeOthers.mutate()
    }
  }

  return (
    <section className="card" aria-label={t.sessions.title}>
      <div className="card-h flex-wrap">
        <h2 className="h3">{t.sessions.title}</h2>
        {sessions.data ? <span className="text-xs text-fg3">{interpolate(t.sessions.count, { count: rows.length })}</span> : null}
        <Button
          size="sm"
          className="ml-auto"
          disabled={others.length === 0}
          loading={revokeOthers.isPending}
          onClick={() => void askOthers()}
        >
          {t.sessions.revokeOthers}
        </Button>
      </div>
      {sessions.isLoading ? (
        <div className="card-b">
          <TableSkeleton columns={[t.sessions.device, t.sessions.ip, t.sessions.lastSeen]} rows={2} label={base.common.loading} />
        </div>
      ) : sessions.isError ? (
        <div className="card-b">
          <ScreenState error={sessions.error} onRetry={() => void sessions.refetch()} />
        </div>
      ) : (
        <div className="tbl-w cards">
          <table className="tbl">
            <caption className="sr-only">{t.sessions.title}</caption>
            <thead>
              <tr>
                <th scope="col">{t.sessions.device}</th>
                <th scope="col">{t.sessions.ip}</th>
                <th scope="col">{t.sessions.lastSeen}</th>
                <th scope="col">{t.sessions.expires}</th>
                <th scope="col">
                  <span className="sr-only">{t.sessions.revoke}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((session) => (
                <tr key={session.Id}>
                  <td className="tm">
                    <span className="flex flex-wrap items-center gap-2">
                      <b>{deviceOf(session)}</b>
                      {session.IsCurrent ? (
                        <Badge small tone="success">
                          {t.sessions.current}
                        </Badge>
                      ) : null}
                    </span>
                  </td>
                  <td data-l={t.sessions.ip} className="mono text-[12.5px]">
                    {session.IpAddress ?? '-'}
                  </td>
                  <td data-l={t.sessions.lastSeen}>{session.LastSeenDate ? format.relative(session.LastSeenDate) : '-'}</td>
                  <td data-l={t.sessions.expires}>{format.date(session.ExpiryDate)}</td>
                  <td className="ta">
                    {!session.IsCurrent ? (
                      <Button
                        size="sm"
                        variant="ghost"
                        aria-label={interpolate(t.sessions.revokeOf, { device: deviceOf(session) })}
                        loading={revoke.isPending && revoke.variables?.Id === session.Id}
                        onClick={() => void askRevoke(session)}
                      >
                        {t.sessions.revoke}
                      </Button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {others.length === 0 ? <p className="m-0 px-4 py-3 text-[13px] text-fg3">{t.sessions.empty}</p> : null}
        </div>
      )}
      <p className="card-f m-0 text-xs text-fg3">{t.sessions.tokensNote}</p>
    </section>
  )
}
