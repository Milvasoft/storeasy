import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'
import { chromeEn, type ChromeDictionary } from './en'
import { chromeTr } from './tr'

export const chromeDictionaries: Record<Lang, ChromeDictionary> = { tr: chromeTr, en: chromeEn }

/** The Chrome Web Store dictionary in the UI language (the same language as useT). */
export function useChrome(): ChromeDictionary {
  return chromeDictionaries[useI18n().lang]
}
