import { useState } from 'react'
import { AppIcon } from '@/components/ui/primitives'
import type { AppListDto } from '@/lib/api/types'
import { appColor } from '@/lib/apps/apps'
import { cn } from '@/lib/utils/cn'

/** The store's icon when the sync brought one, else the app's initial on its color (design 19 section 2). */
export function AppAvatar({ app, size }: { app: Pick<AppListDto, 'Id' | 'Name' | 'IconUrl'>; size?: 's' | 'm' | 'l' }) {
  const [broken, setBroken] = useState(false)
  if (app.IconUrl && !broken) {
    return <img className={cn('aic', size)} src={app.IconUrl} alt="" onError={() => setBroken(true)} />
  }
  return <AppIcon name={app.Name || '?'} color={appColor(app.Id)} size={size} />
}
