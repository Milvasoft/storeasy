import type { ComponentType, CSSProperties, SVGProps } from 'react'
import { AppWindow, Gamepad2, Globe, Package, Rocket, ShoppingBag, Smartphone, Tablet } from 'lucide-react'
import { StoreKind } from '@/lib/api/types'
import { cn } from '@/lib/utils/cn'

// The icons a member can pick for a store connection (StoreConnectionIcons on the API keeps the same keys). A fixed list,
// drawn here as inline SVG: nothing is loaded from elsewhere.
//
// Store logos: the three marks below are storeasy's own simplified drawings in the stores' colors, not copies of the
// official artwork (no clearly licensed source was used). "App Store" is a trademark of Apple Inc., "Google Play" of
// Google LLC and "Microsoft Store" of Microsoft Corporation; the marks only identify the store a connection belongs to,
// and each is offered only on a connection of its own store. Generic icons are lucide-react glyphs on a colored tile.

export const STORE_LOGO_ICON = {
  [StoreKind.Apple]: 'appstore',
  [StoreKind.Google]: 'googleplay',
  [StoreKind.Microsoft]: 'msstore',
} as const

export const GENERIC_ICONS = ['phone', 'tablet', 'bag', 'window', 'globe', 'box', 'game', 'rocket'] as const

export type StoreLogoIcon = (typeof STORE_LOGO_ICON)[StoreKind]
export type GenericIcon = (typeof GENERIC_ICONS)[number]
export type StoreIconKey = StoreLogoIcon | GenericIcon

const GENERIC: Record<GenericIcon, { glyph: ComponentType<SVGProps<SVGSVGElement>>; color: string }> = {
  phone: { glyph: Smartphone, color: '#2563eb' },
  tablet: { glyph: Tablet, color: '#7c3aed' },
  bag: { glyph: ShoppingBag, color: '#ea580c' },
  window: { glyph: AppWindow, color: '#0d9488' },
  globe: { glyph: Globe, color: '#16a34a' },
  box: { glyph: Package, color: '#a16207' },
  game: { glyph: Gamepad2, color: '#db2777' },
  rocket: { glyph: Rocket, color: '#dc2626' },
}

const LOGOS = new Set<string>(Object.values(STORE_LOGO_ICON))

export function isStoreIconKey(value: string | null | undefined): value is StoreIconKey {
  return !!value && (LOGOS.has(value) || (GENERIC_ICONS as readonly string[]).includes(value))
}

/** The icons a connection of `store` may use: its own store's logo first, then the generic ones. */
export function iconsForStore(store: StoreKind): StoreIconKey[] {
  return [STORE_LOGO_ICON[store], ...GENERIC_ICONS]
}

function AppStoreMark() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden focusable="false">
      <rect width="24" height="24" rx="6" fill="#1a8cf8" />
      <path d="M8 17.6 13.1 8.7M10.9 8.7 16 17.6M6.4 14.6h11.2" fill="none" stroke="#fff" strokeWidth="2" strokeLinecap="round" />
    </svg>
  )
}

function GooglePlayMark() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden focusable="false">
      <rect x="0.5" y="0.5" width="23" height="23" rx="5.5" fill="#fff" stroke="rgb(0 0 0 / 0.14)" />
      <path d="M7 5 11 12 7 19z" fill="#1e88e5" />
      <path d="M7 5 15 9.67 11 12z" fill="#34a853" />
      <path d="M7 19 15 14.33 11 12z" fill="#ea4335" />
      <path d="M15 9.67 19 12 15 14.33 11 12z" fill="#fbbc04" />
    </svg>
  )
}

function MsStoreMark() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden focusable="false">
      <rect x="0.5" y="0.5" width="23" height="23" rx="5.5" fill="#fff" stroke="rgb(0 0 0 / 0.14)" />
      <path d="M9 8.2V7a3 3 0 0 1 6 0v1.2" fill="none" stroke="#6b6b6b" strokeWidth="1.5" strokeLinecap="round" />
      <rect x="5" y="8.2" width="14" height="11.3" rx="1.4" fill="#ececec" />
      <rect x="8.4" y="10.6" width="3.3" height="3.3" fill="#f25022" />
      <rect x="12.3" y="10.6" width="3.3" height="3.3" fill="#7fba00" />
      <rect x="8.4" y="14.5" width="3.3" height="3.3" fill="#00a4ef" />
      <rect x="12.3" y="14.5" width="3.3" height="3.3" fill="#ffb900" />
    </svg>
  )
}

/**
 * One store connection icon as a small square tile (decorative: the caller keeps the store or connection name as text).
 * Sized by the surrounding CSS (`.sti`), 18 px by default.
 */
export function StoreIcon({ icon, className, title }: { icon: StoreIconKey; className?: string; title?: string }) {
  if (icon === 'appstore' || icon === 'googleplay' || icon === 'msstore') {
    return (
      <span className={cn('sti logo', className)} data-icon={icon} title={title} aria-hidden>
        {icon === 'appstore' ? <AppStoreMark /> : icon === 'googleplay' ? <GooglePlayMark /> : <MsStoreMark />}
      </span>
    )
  }
  const { glyph: Glyph, color } = GENERIC[icon]
  return (
    <span className={cn('sti', className)} data-icon={icon} title={title} style={{ '--sti': color } as CSSProperties} aria-hidden>
      <Glyph aria-hidden focusable="false" />
    </span>
  )
}
