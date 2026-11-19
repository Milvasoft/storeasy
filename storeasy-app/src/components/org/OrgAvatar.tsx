import { useState } from 'react'
import { cn } from '@/lib/utils/cn'

/** The first letter of an organization's name, upper case in Turkish rules ("i" becomes "İ"). */
export function initialOf(name: string): string {
  return name.trim().slice(0, 1).toLocaleUpperCase('tr') || '?'
}

/**
 * An organization's uploaded logo, else its initial on the ink tile (design 5.26). The initial also comes back when the
 * logo does not load (an expired link, the storage unreachable); a new logo URL is tried again.
 */
export function OrgAvatar({
  name,
  logoUrl,
  size,
  className,
}: {
  name: string
  logoUrl?: string | null
  size?: 'l'
  className?: string
}) {
  const [brokenUrl, setBrokenUrl] = useState<string | null>(null)
  if (logoUrl && brokenUrl !== logoUrl) {
    return <img className={cn('oav oimg', size, className)} src={logoUrl} alt="" onError={() => setBrokenUrl(logoUrl)} />
  }
  return <span className={cn('oav', size, className)}>{initialOf(name)}</span>
}
