import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'
import { p2en, type P2Dictionary } from './en'
import { p2tr } from './tr'

export const p2Dictionaries: Record<Lang, P2Dictionary> = { tr: p2tr, en: p2en }

/** The phase 2 dictionary in the UI language (the same language as useT). */
export function useP2(): P2Dictionary {
  return p2Dictionaries[useI18n().lang]
}
