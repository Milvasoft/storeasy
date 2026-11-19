import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useLocation } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Sparkles } from 'lucide-react'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, Field, Input, Skeleton } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { aiApi, aiKeys, looksLikeAnthropicKey, useAiSettings, type AiSettingsDto } from './api'
import { useAiText } from './i18n'

/** Anchor of the card on the settings page; the reviews page links here when no key is set. */
export const AI_SETTINGS_ANCHOR = 'ai'

/**
 * Settings, AI: the organization's Anthropic API key for review reply drafts. Members who manage store connections save,
 * replace and remove it; everyone else sees whether one is set. The key is typed once and never shown again: the API
 * returns only its last four characters, when and by whom it was saved.
 */
export function AiSettingsCard() {
  const t = useAiText()
  const organization = useCurrentOrganization()
  const settings = useAiSettings(organization.Id)
  const location = useLocation()
  const section = useRef<HTMLElement>(null)

  // The reviews page links to /settings#ai: bring the card into view once it is there.
  useEffect(() => {
    if (location.hash === `#${AI_SETTINGS_ANCHOR}`) {
      section.current?.scrollIntoView?.({ block: 'start' })
    }
  }, [location.hash])

  return (
    <section ref={section} className="card" id={AI_SETTINGS_ANCHOR} aria-label={t.settings.title}>
      <div className="card-h">
        <Sparkles className="h-4 w-4 text-fg3" aria-hidden />
        <h2 className="h3">{t.settings.title}</h2>
      </div>
      <div className="card-b grid gap-4">
        <p className="m-0 text-[13px] text-fg2">{t.settings.intro}</p>
        {settings.isLoading ? (
          <Skeleton className="h-16" />
        ) : settings.isError ? (
          <ScreenState error={settings.error} onRetry={() => void settings.refetch()} />
        ) : settings.data ? (
          <KeyForm settings={settings.data} />
        ) : null}
      </div>
    </section>
  )
}

function KeyForm({ settings }: { settings: AiSettingsDto }) {
  const t = useAiText()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const editable = can(OrgPermission.ManageConnections)
  const [editing, setEditing] = useState(false)
  const [key, setKey] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)

  const refresh = () => void queryClient.invalidateQueries({ queryKey: aiKeys.settings(organization.Id) })

  const save = useMutation({
    mutationFn: () => aiApi.setKey(organization.Id, key.trim()),
    meta: { silentError: true },
    onSuccess: (saved) => {
      setKey('')
      setEditing(false)
      setServer(null)
      queryClient.setQueryData(aiKeys.settings(organization.Id), saved)
      refresh()
      if (saved.ApiKeyVerified === false) {
        showToast(t.settings.savedUnverified, 'warning')
      } else {
        showToast(t.settings.saved, 'success')
      }
    },
    onError: (failure) => setServer(apiErrorMessage(failure, base.common.error)),
  })

  const remove = useMutation({
    mutationFn: () => aiApi.removeKey(organization.Id),
    onSuccess: () => {
      showToast(t.settings.removed, 'success')
      refresh()
    },
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = looksLikeAnthropicKey(key) ? null : t.settings.keyInvalid
    setError(problem)
    if (!problem) {
      save.mutate()
    }
  }

  const askRemove = async () => {
    const ok = await confirm({
      title: t.settings.removeTitle,
      message: t.settings.removeBody,
      confirmLabel: t.settings.remove,
      danger: true,
    })
    if (ok) {
      remove.mutate()
    }
  }

  const status = settings.HasApiKey ? (
    <div className="flex flex-wrap items-center gap-2 text-[13px]">
      <KeyRound className="h-4 w-4 text-fg3" aria-hidden />
      <b className="mono">{interpolate(t.settings.keySet, { lastFour: settings.ApiKeyLastFour ?? '????' })}</b>
      {settings.ApiKeySetDate ? (
        <span className="text-xs text-fg3">
          {settings.ApiKeySetByUserName
            ? interpolate(t.settings.keySetBy, { date: format.dateTime(settings.ApiKeySetDate), user: settings.ApiKeySetByUserName })
            : interpolate(t.settings.keySetDate, { date: format.dateTime(settings.ApiKeySetDate) })}
        </span>
      ) : null}
    </div>
  ) : (
    <Callout tone="neutral">{t.settings.noKey}</Callout>
  )

  const showForm = editable && (editing || !settings.HasApiKey)

  return (
    <div className="grid gap-3">
      {status}
      {settings.Model ? <span className="text-xs text-fg3">{interpolate(t.settings.model, { model: settings.Model })}</span> : null}
      {!editable ? <Callout tone="info">{t.settings.readOnly}</Callout> : null}
      {showForm ? (
        <form className="grid gap-3" onSubmit={submit} noValidate>
          {server ? <FormErrors title={server} /> : null}
          <Field label={t.settings.keyLabel} hint={`${t.settings.keyHint} ${t.settings.cost}`} error={error}>
            <Input
              mono
              type="password"
              autoComplete="off"
              spellCheck={false}
              placeholder={t.settings.keyPlaceholder}
              value={key}
              onChange={(event) => setKey(event.target.value)}
            />
          </Field>
          <div className="flex flex-wrap justify-end gap-2">
            {settings.HasApiKey ? (
              <Button
                variant="ghost"
                onClick={() => {
                  setEditing(false)
                  setKey('')
                  setError(null)
                  setServer(null)
                }}
              >
                {t.settings.cancel}
              </Button>
            ) : null}
            <Button type="submit" variant="primary" loading={save.isPending} disabled={key.trim().length === 0}>
              {t.settings.save}
            </Button>
          </div>
        </form>
      ) : editable ? (
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="danger-outline" loading={remove.isPending} onClick={() => void askRemove()}>
            {t.settings.remove}
          </Button>
          <Button onClick={() => setEditing(true)}>{t.settings.replace}</Button>
        </div>
      ) : null}
    </div>
  )
}
