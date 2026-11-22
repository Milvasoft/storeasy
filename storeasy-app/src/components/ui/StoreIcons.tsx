import type { ComponentType, CSSProperties, SVGProps } from 'react'
import { AppWindow, Gamepad2, Globe, Package, Rocket, ShoppingBag, Smartphone, Tablet } from 'lucide-react'
import { type StoreKind } from '@/lib/api/types'
import { byStore } from '@/lib/stores/channels'
import { cn } from '@/lib/utils/cn'

// The icons a member can pick for a store connection (StoreConnectionIcons on the API keeps the same keys). A fixed list,
// drawn here as inline SVG: nothing is loaded from elsewhere.
//
// Store logos: the App Store and Google Play marks are the paths of Simple Icons 13.21.0 (https://simpleicons.org,
// CC0-1.0), drawn on a tile of our own. Simple Icons has no Microsoft Store icon (Microsoft asked for its removal), so
// the Microsoft Store mark is storeasy's own simplified drawing. winget has no logo of its own on Simple Icons either; its
// mark is storeasy's own drawing of a package with a download arrow. The Chrome Web Store mark is storeasy's own drawing
// too (a store bag with a ring), not Google's logo. "App Store" is a trademark of Apple Inc., "Google Play"
// of Google LLC and "Microsoft Store" of Microsoft Corporation; the logos are used only to identify the store a
// connection belongs to, and each is offered only on a connection of its own store. Generic icons are lucide-react
// glyphs on a colored tile.

// Simple Icons 13.21.0, CC0-1.0 (viewBox 0 0 24 24).
const APP_STORE_PATH =
  'M8.8086 14.9194l6.1107-11.0368c.0837-.1513.1682-.302.2437-.4584.0685-.142.1267-.2854.1646-.4403.0803-.3259.0588-.6656-.066-.9767-.1238-.3095-.3417-.5678-.6201-.7355a1.4175 1.4175 0 0 0-.921-.1924c-.3207.043-.6135.1935-.8443.4288-.1094.1118-.1996.2361-.2832.369-.092.1463-.175.2979-.259.4492l-.3864.6979-.3865-.6979c-.0837-.1515-.1667-.303-.2587-.4492-.0837-.1329-.1739-.2572-.2835-.369-.2305-.2353-.5233-.3857-.844-.429a1.4181 1.4181 0 0 0-.921.1926c-.2784.1677-.4964.426-.6203.7355-.1246.311-.1461.6508-.066.9767.038.155.0962.2984.1648.4403.0753.1564.1598.307.2437.4584l1.248 2.2543-4.8625 8.7825H2.0295c-.1676 0-.3351-.0007-.5026.0092-.1522.009-.3004.0284-.448.0714-.3108.0906-.5822.2798-.7783.548-.195.2665-.3006.5929-.3006.9279 0 .3352.1057.6612.3006.9277.196.2683.4675.4575.7782.548.1477.043.296.0623.4481.0715.1675.01.335.009.5026.009h13.0974c.0171-.0357.059-.1294.1-.2697.415-1.4151-.6156-2.843-2.0347-2.843zM3.113 18.5418l-.7922 1.5008c-.0818.1553-.1644.31-.2384.4705-.067.1458-.124.293-.1611.452-.0785.3346-.0576.6834.0645 1.0029.1212.3175.3346.583.607.7549.2727.172.5891.2416.9013.1975.3139-.044.6005-.1986.8263-.4402.1072-.1148.1954-.2424.2772-.3787.0902-.1503.1714-.3059.2535-.4612L6 19.4636c-.0896-.149-.9473-1.4704-2.887-.9218m20.5861-3.0056a1.4707 1.4707 0 0 0-.779-.5407c-.1476-.0425-.2961-.0616-.4483-.0705-.1678-.0099-.3352-.0091-.503-.0091H18.648l-4.3891-7.817c-.6655.7005-.9632 1.485-1.0773 2.1976-.1655 1.0333.0367 2.0934.546 3.0004l5.2741 9.3933c.084.1494.167.299.2591.4435.0837.131.1739.2537.2836.364.231.2323.5238.3809.8449.4232.3192.0424.643-.0244.9217-.1899.2784-.1653.4968-.4204.621-.7257.1246-.3072.146-.6425.0658-.9641-.0381-.1529-.0962-.2945-.165-.4346-.0753-.1543-.1598-.303-.2438-.4524l-1.216-2.1662h1.596c.1677 0 .3351.0009.5029-.009.1522-.009.3007-.028.4483-.0705a1.4707 1.4707 0 0 0 .779-.5407A1.5386 1.5386 0 0 0 24 16.452a1.539 1.539 0 0 0-.3009-.9158Z'
const GOOGLE_PLAY_PATH =
  'M22.018 13.298l-3.919 2.218-3.515-3.493 3.543-3.521 3.891 2.202a1.49 1.49 0 0 1 0 2.594zM1.337.924a1.486 1.486 0 0 0-.112.568v21.017c0 .217.045.419.124.6l11.155-11.087L1.337.924zm12.207 10.065l3.258-3.238L3.45.195a1.466 1.466 0 0 0-.946-.179l11.04 10.973zm0 2.067l-11 10.933c.298.036.612-.016.906-.183l13.324-7.54-3.23-3.21z'

export type StoreLogoIcon = 'appstore' | 'googleplay' | 'msstore' | 'winget' | 'chromewebstore'

// The logo of each store, from the channel table (lib/stores/channels.ts).
export const STORE_LOGO_ICON: Record<StoreKind, StoreLogoIcon> = byStore((channel) => channel.logo as StoreLogoIcon)

export const GENERIC_ICONS = ['phone', 'tablet', 'bag', 'window', 'globe', 'box', 'game', 'rocket'] as const

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
      <rect width="24" height="24" rx="6" fill="#0d96f6" />
      <path d={APP_STORE_PATH} fill="#fff" transform="translate(4.5 4.5) scale(0.625)" />
    </svg>
  )
}

function GooglePlayMark() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden focusable="false">
      <rect x="0.5" y="0.5" width="23" height="23" rx="5.5" fill="#fff" stroke="rgb(0 0 0 / 0.14)" />
      <path d={GOOGLE_PLAY_PATH} fill="#414141" transform="translate(5 5) scale(0.5833)" />
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

function WingetMark() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden focusable="false">
      <rect width="24" height="24" rx="6" fill="#0b5cad" />
      <path d="M5.5 9.2 12 6l6.5 3.2v6.6L12 19l-6.5-3.2z" fill="#e8f1fb" />
      <path d="M5.5 9.2 12 12.4l6.5-3.2M12 12.4V19" fill="none" stroke="#0b5cad" strokeWidth="1.1" strokeLinejoin="round" />
      <path
        d="M12 2.6v5.6m-2.1-2.1L12 8.2l2.1-2.1"
        fill="none"
        stroke="#fff"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}

/**
 * One store connection icon as a small square tile (decorative: the caller keeps the store or connection name as text).
 * Sized by the surrounding CSS (`.sti`), 18 px by default.
 */
function ChromeWebStoreMark() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden focusable="false">
      <rect width="24" height="24" rx="6" fill="#1a73e8" />
      <path d="M5 9h14l-1.2 9.2a1.6 1.6 0 0 1-1.6 1.4H7.8a1.6 1.6 0 0 1-1.6-1.4z" fill="#e8f0fe" />
      <path d="M9 9V7.5a3 3 0 0 1 6 0V9" fill="none" stroke="#e8f0fe" strokeWidth="1.6" strokeLinecap="round" />
      <circle cx="12" cy="14.2" r="3" fill="none" stroke="#1a73e8" strokeWidth="1.6" />
      <circle cx="12" cy="14.2" r="1.1" fill="#1a73e8" />
    </svg>
  )
}

const MARKS: Record<StoreLogoIcon, () => JSX.Element> = {
  appstore: AppStoreMark,
  googleplay: GooglePlayMark,
  msstore: MsStoreMark,
  winget: WingetMark,
  chromewebstore: ChromeWebStoreMark,
}

export function StoreIcon({ icon, className, title }: { icon: StoreIconKey; className?: string; title?: string }) {
  if (LOGOS.has(icon)) {
    const Mark = MARKS[icon as StoreLogoIcon]
    return (
      <span className={cn('sti logo', className)} data-icon={icon} title={title} aria-hidden>
        <Mark />
      </span>
    )
  }
  const { glyph: Glyph, color } = GENERIC[icon as GenericIcon]
  return (
    <span className={cn('sti', className)} data-icon={icon} title={title} style={{ '--sti': color } as CSSProperties} aria-hidden>
      <Glyph aria-hidden focusable="false" />
    </span>
  )
}
