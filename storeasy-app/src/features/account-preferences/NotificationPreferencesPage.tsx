import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Lock } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { ScreenState } from '@/components/ui/ScreenState'
import { Tabs } from '@/components/ui/Tabs'
import { CardSkeleton, PageHeader, Switch } from '@/components/ui/primitives'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { notificationKeys } from '@/features/notifications/api'
import { useNotificationsText } from '@/features/notifications/i18n'
import { notificationTypeDescription, notificationTypeLabel } from '@/features/notifications/labels'
import type { NotificationPreferenceDto, NotificationType } from '@/features/notifications/types'
import { preferencesApi } from './api'

// Design 14.22: per kind, in-app and e-mail; saved on every switch (one PUT per kind), "Saved" next to the row. Locked
// kinds (security and connections) cannot be turned off; web push waits for phase 8.

type Channel = 'InApp' | 'Email'

/** The account tabs, with this page's tab (the account page draws the same row). */
export function accountTabItems(t: { profile: string; security: string }, notifications: string) {
  return [
    { value: 'profile', label: t.profile, to: '/account' },
    { value: 'security', label: t.security, to: '/account/security' },
    { value: 'notifications', label: notifications, to: '/account/notifications' },
  ]
}

function PreferenceRow({ preference }: { preference: NotificationPreferenceDto }) {
  const t = useNotificationsText()
  const queryClient = useQueryClient()
  const [saved, setSaved] = useState(false)
  const kind = notificationTypeLabel(preference.Kind, t)
  const save = useMutation({
    mutationFn: (change: Partial<Record<Channel, boolean>>) =>
      preferencesApi.update({
        Kind: preference.Kind as NotificationType,
        InApp: change.InApp ?? preference.InApp,
        Email: change.Email ?? preference.Email,
        WebPush: preference.WebPush,
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData<NotificationPreferenceDto[]>(notificationKeys.preferences(), (list) =>
        (list ?? []).map((item) => (item.Kind === updated.Kind ? updated : item)),
      )
      setSaved(true)
    },
  })
  return (
    <tr>
      <td className="tm">
        <span className="grid gap-0.5">
          <b className="flex items-center gap-1.5">
            {kind}
            {preference.Locked ? (
              <Badge small>
                <Lock className="h-3 w-3" aria-hidden />
                {t.prefs.locked}
              </Badge>
            ) : null}
          </b>
          <span className="text-xs text-fg3">{notificationTypeDescription(preference.Kind, t)}</span>
        </span>
      </td>
      <td data-l={t.prefs.inApp}>
        <Switch
          checked={preference.InApp}
          disabled={preference.Locked || save.isPending}
          label={interpolate(t.prefs.switchOf, { channel: t.prefs.inApp, kind })}
          onChange={(value) => save.mutate({ InApp: value })}
        />
      </td>
      <td data-l={t.prefs.email}>
        <Switch
          checked={preference.Email}
          disabled={preference.Locked || save.isPending}
          label={interpolate(t.prefs.switchOf, { channel: t.prefs.email, kind })}
          onChange={(value) => save.mutate({ Email: value })}
        />
      </td>
      <td data-l={t.prefs.webPush}>
        <Switch checked={preference.WebPush} disabled label={interpolate(t.prefs.switchOf, { channel: t.prefs.webPush, kind })} onChange={() => undefined} />
      </td>
      <td className="ta text-xs text-success" aria-live="polite">
        {saved ? t.prefs.saved : null}
      </td>
    </tr>
  )
}

/** /account/notifications. */
export function NotificationPreferencesPage() {
  const t = useNotificationsText()
  const base = useT()
  const preferences = useQuery({
    queryKey: notificationKeys.preferences(),
    queryFn: () => preferencesApi.list(),
  })
  return (
    <>
      <PageHeader title={base.account.title} subtitle={base.account.subtitle} />
      <Tabs label={base.account.title} items={accountTabItems(base.account, t.prefs.tab)} />
      <div className="mt-4 grid max-w-3xl gap-3">
        <div>
          <h2 className="h3">{t.prefs.title}</h2>
          <p className="text-[13px] text-fg2">{t.prefs.subtitle}</p>
        </div>
        {preferences.isLoading ? (
          <CardSkeleton count={1} />
        ) : preferences.isError ? (
          <ScreenState error={preferences.error} onRetry={() => void preferences.refetch()} />
        ) : (
          <div className="tbl-w cards">
            <table className="tbl">
              <caption className="sr-only">{t.prefs.title}</caption>
              <thead>
                <tr>
                  <th scope="col">{t.prefs.kind}</th>
                  <th scope="col">{t.prefs.inApp}</th>
                  <th scope="col">{t.prefs.email}</th>
                  <th scope="col">
                    {t.prefs.webPush} <span className="text-xs font-normal text-fg3">{t.prefs.webPushSoon}</span>
                  </th>
                  <th scope="col">
                    <span className="sr-only">{t.prefs.saved}</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {(preferences.data ?? []).map((preference) => (
                  <PreferenceRow key={preference.Kind} preference={preference} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  )
}
