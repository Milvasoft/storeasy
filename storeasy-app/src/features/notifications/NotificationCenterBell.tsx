import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Menu, type MenuEntry } from '@/components/ui/Menu'
import { IconButton } from '@/components/ui/primitives'
import { RecordEventType } from '@/lib/activity/events'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { BELL_REQUEST, notificationKeys, notificationsApi, UNREAD_COUNT_REQUEST } from './api'
import { useNotificationsText } from './i18n'
import { actionPath, notificationTypeLabel } from './labels'
import { useLiveRefresh } from './live'
import type { AccountNotificationDto } from './types'

const LIVE_TYPES = [RecordEventType.NotificationCreated]

/**
 * The bell of the top bar (design 14.19): the unread counter (refreshed by notification.created and every minute), the
 * newest eight notifications, "mark all as read" and the way to the center and the preferences.
 */
export function NotificationCenterBell() {
  const t = useNotificationsText()
  const format = useFormat()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { organizations } = useOrganization()
  useLiveRefresh(LIVE_TYPES, notificationKeys.all)

  const unread = useQuery({
    queryKey: notificationKeys.unreadCount(),
    queryFn: () => notificationsApi.list(UNREAD_COUNT_REQUEST),
    refetchInterval: 60_000,
    staleTime: 30_000,
  })
  const latest = useQuery({
    queryKey: notificationKeys.list(BELL_REQUEST),
    queryFn: () => notificationsApi.list(BELL_REQUEST),
    staleTime: 30_000,
  })
  const markSeen = useMutation({
    mutationFn: (input: { ids?: number[]; all?: boolean }) => notificationsApi.markSeen(input),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  })

  const count = unread.data?.TotalDataCount ?? 0
  const items = latest.data?.Data ?? []
  const organizationName = (id: number | undefined) =>
    organizations.find((organization) => organization.Id === id)?.Name ?? t.noOrganization

  const open = (notification: AccountNotificationDto) => {
    if (!notification.IsSeen) {
      markSeen.mutate({ ids: [notification.Id] })
    }
    const target = actionPath(notification.ActionLink)
    if (target?.internal) {
      navigate(target.href)
    } else if (target) {
      window.open(target.href, '_blank', 'noopener,noreferrer')
    }
  }

  const entries: MenuEntry[] = [
    {
      key: 'heading',
      heading: (
        <span className="flex items-center gap-2">
          {t.title}
          {count > 0 ? (
            <Badge tone="accent" small>
              {interpolate(t.unreadCount, { count })}
            </Badge>
          ) : null}
        </span>
      ),
    },
    ...(count > 0 ? [{ key: 'mark-all', label: t.markAllRead, onSelect: () => markSeen.mutate({ all: true }) }] : []),
    ...(items.length === 0
      ? [{ key: 'empty', content: <p className="px-3 py-6 text-center text-[13px] text-fg3">{t.empty}</p> }]
      : items.map((notification) => ({
          key: String(notification.Id),
          label: (
            <span className="grid gap-0.5 py-0.5">
              <span className="flex items-center gap-2">
                {!notification.IsSeen ? <span className="h-2 w-2 flex-none rounded-full bg-accent" aria-hidden /> : null}
                <b className="text-[13px]">{notificationTypeLabel(notification.Type, t)}</b>
                <span className="ml-auto text-xs text-fg3">
                  {organizationName(notification.OrganizationId)} · {format.relative(notification.CreationDate)}
                </span>
              </span>
              <span className="line-clamp-2 text-xs text-fg2">{notification.Text}</span>
            </span>
          ),
          onSelect: () => open(notification),
        }))),
    { key: 'sep', separator: true },
    {
      key: 'links',
      content: (
        <span className="flex justify-between gap-2 px-3 py-2 text-[13px]">
          <Link className="lnk" to="/notifications">
            {t.all}
          </Link>
          <Link className="lnk" to="/account/notifications">
            {t.preferences}
          </Link>
        </span>
      ),
    },
  ]

  const label = interpolate(t.bellLabel, { count })
  return (
    <Menu
      label={t.title}
      className="w-[360px]"
      trigger={
        <IconButton label={label} className="relative">
          <Bell aria-hidden />
          {count > 0 ? (
            <span
              className="absolute -right-0.5 -top-0.5 grid h-4 min-w-4 place-items-center rounded-full bg-danger px-1 text-[10px] font-semibold text-white tabular-nums"
              aria-hidden
            >
              {count > 99 ? '99+' : count}
            </span>
          ) : null}
        </IconButton>
      }
      entries={entries}
    />
  )
}
