import type { ReactNode } from 'react'
import { type StoreKind } from '@/lib/api/types'
import { useStoreIcons } from '@/lib/connections/storeIcons'
import { useT } from '@/lib/i18n/I18nContext'
import { byStore, channelOf } from '@/lib/stores/channels'
import { cn } from '@/lib/utils/cn'
import { StoreIcon } from './StoreIcons'

// Badge tones (08 section 3.8). Color never carries meaning alone: a badge always has text, optionally a dot.
export type BadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'accent' | 'solid'

const TONE_CLASS: Record<BadgeTone, string> = {
  neutral: '',
  success: 'ok',
  warning: 'warn',
  danger: 'dng',
  info: 'info',
  accent: 'acc',
  solid: 'sol',
}

export function Badge({
  tone = 'neutral',
  dot,
  live,
  small,
  children,
}: {
  tone?: BadgeTone
  dot?: boolean
  live?: boolean
  small?: boolean
  children: ReactNode
}) {
  return (
    <span className={cn('bdg', TONE_CLASS[tone], small && 'sm', live && 'live')}>
      {dot || live ? <span className="dot" aria-hidden /> : null}
      {children}
    </span>
  )
}

// storeasy's own text code of each store, from the channel table (lib/stores/channels.ts).
const STORE_CODE: Record<StoreKind, string> = byStore((channel) => channel.code)

// Stores are named with neutral text and a mark in front: the icon a member picked for the connection (its own store
// logo or a generic icon, StoreIcons.tsx), else storeasy's own AS / GP / MS code. Store logos were left out at first
// (design brief 4); on 19 Nov 2026 the user chose to allow them as an optional, per connection choice.
// connectionId: the connection the record came from; without it (filters, counts, store columns) the store's oldest
// connection with an icon decides (resolveStoreIcon). The mark is decorative, the store name stays as text (sr-only and
// a title in codeOnly).
export function StoreTag({
  store,
  small,
  codeOnly,
  connectionId,
}: {
  store: StoreKind
  small?: boolean
  codeOnly?: boolean
  connectionId?: number | null
}) {
  const t = useT()
  const { iconFor } = useStoreIcons()
  const name = storeName(store, t.enums.store)
  const icon = iconFor(store, connectionId)
  return (
    <span className={cn('st', small && 'sm')}>
      {icon ? (
        <StoreIcon icon={icon} title={codeOnly ? name : undefined} />
      ) : (
        <b aria-hidden={codeOnly ? undefined : true} title={codeOnly ? name : undefined}>
          {STORE_CODE[store]}
        </b>
      )}
      {codeOnly ? <span className="sr-only">{name}</span> : name}
    </span>
  )
}

/** storeasy's own text code of a store (AS / GP / MS), shown when no icon was picked. */
export function storeCode(store: StoreKind): string {
  return STORE_CODE[store]
}

export function storeName(
  store: StoreKind,
  names: { apple: string; google: string; microsoft: string; winget?: string; firefox?: string },
): string {
  // A dictionary written before a channel existed names it by its key ("winget"), never as another store.
  const key = channelOf(store).key
  return names[key] ?? key
}

export function Tag({ children }: { children: ReactNode }) {
  return <span className="tag">{children}</span>
}
