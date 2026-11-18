import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BellRing } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { Button } from '@/components/ui/primitives'
import { pushApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import type { WebPushSubscriptionDto } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { describeAgent } from './AccountCards'
import { usePublicSettings } from './publicSettings'
import { useAccessText } from './i18n'
import {
  currentSubscription,
  isPushSupported,
  notificationPermission,
  PushPermissionDeniedError,
  subscribeBrowser,
  unsubscribeBrowser,
} from './push'

// Design 14.22 and phase 8: turning browser push on for this browser, a test push and the other subscribed browsers.

export const pushKeys = {
  subscriptions: () => ['account', 'push-subscriptions'] as const,
  current: () => ['account', 'push-current'] as const,
}

export function PushCard() {
  const t = useAccessText()
  const format = useFormat()
  const queryClient = useQueryClient()
  const settings = usePublicSettings()
  const supported = isPushSupported()
  const publicKey = settings.data?.WebPushPublicKey ?? null
  const [denied, setDenied] = useState(() => notificationPermission() === 'denied' && supported)
  const [error, setError] = useState<string | null>(null)
  const current = useQuery({ queryKey: pushKeys.current(), queryFn: () => currentSubscription(), enabled: supported })
  const subscriptions = useQuery({ queryKey: pushKeys.subscriptions(), queryFn: () => pushApi.subscriptions(), enabled: !!publicKey })
  const subscription = current.data ?? null

  useEffect(() => {
    setDenied(supported && notificationPermission() === 'denied')
  }, [supported])

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: pushKeys.current() })
    void queryClient.invalidateQueries({ queryKey: pushKeys.subscriptions() })
  }

  const enable = useMutation({
    mutationFn: () => subscribeBrowser(publicKey ?? ''),
    meta: { silentError: true },
    onSuccess: () => {
      setError(null)
      showToast(t.push.enabled, 'success')
      refresh()
    },
    onError: (failure) => {
      if (failure instanceof PushPermissionDeniedError) {
        setDenied(true)
        setError(null)
      } else {
        setError(apiErrorMessage(failure, t.push.failed))
      }
    },
  })
  const disable = useMutation({
    mutationFn: (target: PushSubscription) => unsubscribeBrowser(target),
    onSuccess: () => {
      showToast(t.push.disabled, 'success')
      refresh()
    },
  })
  const remove = useMutation({
    mutationFn: (target: WebPushSubscriptionDto) => pushApi.unsubscribe({ Id: target.Id }),
    onSuccess: () => {
      showToast(t.push.removed, 'success')
      refresh()
    },
  })
  const test = useMutation({
    mutationFn: () => pushApi.test(),
    onSuccess: (count) => showToast(count > 0 ? interpolate(t.push.testSent, { count }) : t.push.testNone, count > 0 ? 'success' : 'info'),
  })

  const others = subscriptions.data ?? []
  const state = !supported
    ? 'unsupported'
    : settings.isSuccess && !publicKey
      ? 'notConfigured'
      : denied && !subscription
        ? 'denied'
        : subscription
          ? 'on'
          : 'off'

  return (
    <section className="card" aria-label={t.push.title}>
      <div className="card-h flex-wrap">
        <BellRing className="h-4 w-4 text-fg3" aria-hidden />
        <h2 className="h3">{t.push.title}</h2>
        {state === 'on' ? (
          <Badge small dot tone="success">
            {t.push.on}
          </Badge>
        ) : null}
      </div>
      <div className="card-b grid gap-3">
        <p className="m-0 text-[13px] text-fg2">{t.push.body}</p>
        {state === 'unsupported' ? <Callout tone="info">{t.push.unsupported}</Callout> : null}
        {state === 'notConfigured' ? <Callout tone="info">{t.push.notConfigured}</Callout> : null}
        {state === 'denied' ? <Callout tone="warning">{t.push.denied}</Callout> : null}
        {error ? <Callout tone="danger">{error}</Callout> : null}
        {state === 'off' || state === 'on' ? (
          <div className="flex flex-wrap items-center gap-2">
            <span className="mr-auto text-[13px]">{state === 'on' ? t.push.on : t.push.off}</span>
            {state === 'on' && subscription ? (
              <Button loading={disable.isPending} onClick={() => disable.mutate(subscription)}>
                {t.push.disable}
              </Button>
            ) : (
              <Button variant="primary" loading={enable.isPending} disabled={!publicKey} onClick={() => enable.mutate()}>
                {t.push.enable}
              </Button>
            )}
          </div>
        ) : null}
        {publicKey && others.length > 0 ? (
          <div className="grid gap-1.5">
            <span className="flex items-center gap-2">
              <b className="mr-auto text-[13px]">{t.push.others}</b>
              <Button size="sm" loading={test.isPending} onClick={() => test.mutate()}>
                {t.push.test}
              </Button>
            </span>
            <ul className="m-0 grid list-none gap-1 p-0">
              {others.map((item) => {
                const name = describeAgent(item.UserAgent) ?? item.PushServiceHost ?? `#${item.Id}`
                return (
                  <li key={item.Id} className="flex flex-wrap items-center gap-2 text-[13px]">
                    <span className="mr-auto min-w-0">
                      <b>{name}</b>{' '}
                      <span className="text-xs text-fg3">
                        {item.PushServiceHost ? `${item.PushServiceHost} · ` : ''}
                        {item.LastSuccessDate
                          ? interpolate(t.push.lastDelivered, { time: format.relative(item.LastSuccessDate) })
                          : t.push.never}
                      </span>
                    </span>
                    <Button
                      size="sm"
                      variant="ghost"
                      aria-label={interpolate(t.push.removeOf, { name })}
                      loading={remove.isPending && remove.variables?.Id === item.Id}
                      onClick={() => remove.mutate(item)}
                    >
                      {t.push.remove}
                    </Button>
                  </li>
                )
              })}
            </ul>
          </div>
        ) : null}
      </div>
    </section>
  )
}
