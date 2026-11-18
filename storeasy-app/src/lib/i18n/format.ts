import type { DateOnly, IsoDateTime } from '@/lib/api/types'
import { parseDateOnly } from '@/lib/utils/date'
import type { Dictionary } from './en'
import { cultureOf, type Lang } from './lang'

// Shown for a missing value. A plain hyphen: no dash characters anywhere.
export const EMPTY_VALUE = '-'

interface DateOptions {
  // A value that means a UTC day (midnight UTC): shown as that day, not shifted to the viewer's zone.
  utcDay?: boolean
}

function toDate(value: DateOnly | IsoDateTime): Date | null {
  const date = parseDateOnly(value) ?? new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

// "18 Kas 2026" / "Nov 18, 2026".
export function formatDate(value: DateOnly | IsoDateTime | null | undefined, lang: Lang, options: DateOptions = {}): string {
  const date = value ? toDate(value) : null
  if (!date) {
    return EMPTY_VALUE
  }
  return date.toLocaleDateString(cultureOf(lang), {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: options.utcDay ? 'UTC' : undefined,
  })
}

// "18 Kasım 2026, 14:02".
export function formatDateTime(value: IsoDateTime | null | undefined, lang: Lang): string {
  const date = value ? new Date(value) : null
  if (!date || Number.isNaN(date.getTime())) {
    return EMPTY_VALUE
  }
  return date.toLocaleString(cultureOf(lang), { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

// "14:02:41", for event feeds.
export function formatTime(value: IsoDateTime | null | undefined, lang: Lang): string {
  const date = value ? new Date(value) : null
  if (!date || Number.isNaN(date.getTime())) {
    return EMPTY_VALUE
  }
  return date.toLocaleTimeString(cultureOf(lang), { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

export function formatNumber(value: number, lang: Lang, maximumFractionDigits = 0): string {
  return new Intl.NumberFormat(cultureOf(lang), { maximumFractionDigits }).format(value)
}

// "31,4 MB" / "31.4 MB".
export function formatBytes(bytes: number, lang: Lang): string {
  const units = ['B', 'KB', 'MB', 'GB']
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit += 1
  }
  return `${formatNumber(value, lang, unit === 0 ? 0 : 1)} ${units[unit]}`
}

// "now", "5 dk", "3 sa", "2 g"; older than a week shows the date. Unit texts come from the dictionary.
export function relativeTime(value: IsoDateTime | null | undefined, lang: Lang, t: Dictionary): string {
  const date = value ? new Date(value) : null
  if (!date || Number.isNaN(date.getTime())) {
    return EMPTY_VALUE
  }
  const minutes = Math.round((Date.now() - date.getTime()) / 60_000)
  if (minutes < 1) {
    return t.time.now
  }
  if (minutes < 60) {
    return `${minutes} ${t.time.minuteShort}`
  }
  const hours = Math.round(minutes / 60)
  if (hours < 24) {
    return `${hours} ${t.time.hourShort}`
  }
  const days = Math.round(hours / 24)
  if (days < 7) {
    return `${days} ${t.time.dayShort}`
  }
  return formatDate(value, lang)
}

export interface Formatters {
  date: (value: DateOnly | IsoDateTime | null | undefined, options?: DateOptions) => string
  dateTime: (value: IsoDateTime | null | undefined) => string
  time: (value: IsoDateTime | null | undefined) => string
  number: (value: number, maximumFractionDigits?: number) => string
  bytes: (value: number) => string
  relative: (value: IsoDateTime | null | undefined) => string
}

export function createFormatters(lang: Lang, t: Dictionary): Formatters {
  return {
    date: (value, options) => formatDate(value, lang, options),
    dateTime: (value) => formatDateTime(value, lang),
    time: (value) => formatTime(value, lang),
    number: (value, digits) => formatNumber(value, lang, digits),
    bytes: (value) => formatBytes(value, lang),
    relative: (value) => relativeTime(value, lang, t),
  }
}
