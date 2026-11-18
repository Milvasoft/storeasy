import { Bell, BellOff } from 'lucide-react'
import { Menu } from '@/components/ui/Menu'
import { IconButton } from '@/components/ui/primitives'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { useT } from '@/lib/i18n/I18nContext'

/**
 * The bell in the top bar (design 14.19). The notification center comes with its API round (types, organization field,
 * live counter); until then the bell opens a short note instead of an empty list.
 */
export function NotificationBell() {
  const t = useT()
  const enabled = useFeature('Notifications')
  return (
    <Menu
      label={t.notifications.label}
      className="w-[320px]"
      trigger={
        <IconButton label={t.notifications.label}>
          <Bell aria-hidden />
        </IconButton>
      }
      entries={[
        { key: 'heading', heading: t.notifications.label },
        {
          key: 'body',
          content: (
            <div className="grid justify-items-center gap-2 px-3 py-6 text-center">
              <span className="grid h-10 w-10 place-items-center rounded-md bg-bg2 text-fg2">
                <BellOff className="h-5 w-5" aria-hidden />
              </span>
              <b className="text-[13.5px]">{enabled ? t.notifications.empty : t.notifications.comingTitle}</b>
              <span className="text-xs text-fg3">{t.notifications.comingBody}</span>
            </div>
          ),
        },
      ]}
    />
  )
}
