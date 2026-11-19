import type { MouseEvent } from 'react'
import { Eye, EyeOff } from 'lucide-react'
import { IconButton } from '@/components/ui/primitives'
import type { AppListDto } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useSetAppHidden } from '@/components/apps/useSetAppHidden'

/**
 * A small icon button on an app card or table row: "Hide" for a shown app, "Show" for a hidden one. No confirmation;
 * the toast that follows has an "Undo". The click does not reach the row, which opens the app.
 */
export function HideAppButton({ app }: { app: AppListDto }) {
  const t = useT()
  const setHidden = useSetAppHidden()
  const hidden = !!app.HiddenDate
  const label = interpolate(hidden ? t.apps.unhideApp : t.apps.hideApp, { app: app.Name })

  return (
    <IconButton
      label={label}
      size="sm"
      ghost
      disabled={setHidden.isPending}
      onClick={(event: MouseEvent) => {
        event.stopPropagation()
        setHidden.mutate({ app, hidden: !hidden })
      }}
    >
      {hidden ? <Eye aria-hidden /> : <EyeOff aria-hidden />}
    </IconButton>
  )
}
