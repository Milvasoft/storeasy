import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'
import { easEn, type EasDictionary } from './en'
import { easTr } from './tr'

export const easDictionaries: Record<Lang, EasDictionary> = { tr: easTr, en: easEn }

/** The EAS dictionary in the UI language (the same language as useT). */
export function useEas(): EasDictionary {
  return easDictionaries[useI18n().lang]
}
