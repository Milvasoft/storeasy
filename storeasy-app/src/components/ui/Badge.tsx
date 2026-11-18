import type { ReactNode } from 'react'
import { StoreKind } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { cn } from '@/lib/utils/cn'

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

const STORE_CODE: Record<StoreKind, string> = { [StoreKind.Apple]: 'AS', [StoreKind.Google]: 'GP', [StoreKind.Microsoft]: 'MS' }

// Stores are named with neutral text and storeasy's own AS / GP / MS tag; no store logo is drawn (design brief 4).
export function StoreTag({ store, small, codeOnly }: { store: StoreKind; small?: boolean; codeOnly?: boolean }) {
  const t = useT()
  const name = storeName(store, t.enums.store)
  return (
    <span className={cn('st', small && 'sm')}>
      <b aria-hidden={codeOnly ? undefined : true} title={codeOnly ? name : undefined}>
        {STORE_CODE[store]}
      </b>
      {codeOnly ? <span className="sr-only">{name}</span> : name}
    </span>
  )
}

export function storeName(store: StoreKind, names: { apple: string; google: string; microsoft: string }): string {
  switch (store) {
    case StoreKind.Apple:
      return names.apple
    case StoreKind.Google:
      return names.google
    default:
      return names.microsoft
  }
}

export function Tag({ children }: { children: ReactNode }) {
  return <span className="tag">{children}</span>
}
