import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'
import { firefoxEn, type FirefoxDictionary } from './en'
import { firefoxTr } from './tr'

export const firefoxDictionaries: Record<Lang, FirefoxDictionary> = { tr: firefoxTr, en: firefoxEn }

/** The Firefox Add-ons dictionary in the UI language (the same language as useT). */
export function useFirefox(): FirefoxDictionary {
  return firefoxDictionaries[useI18n().lang]
}
