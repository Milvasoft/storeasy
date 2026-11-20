import { BellOff } from 'lucide-react'
import type { AppListDto } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'

/** A small bell-off beside an app's name when its alerts are muted; nothing otherwise. */
export function MutedAlertsIcon({ app }: { app: Pick<AppListDto, 'Name' | 'AlertsMutedDate'> }) {
  const t = useT()
  if (!app.AlertsMutedDate) {
    return null
  }
  const label = interpolate(t.apps.mutedIndicator, { app: app.Name })
  return (
    <span className="inline-flex shrink-0 text-fg3" role="img" aria-label={label} title={label}>
      <BellOff className="h-3.5 w-3.5" aria-hidden />
    </span>
  )
}
