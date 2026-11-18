import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'
import { msEn, type MsDictionary } from './en'
import { msTr } from './tr'

export const msDictionaries: Record<Lang, MsDictionary> = { tr: msTr, en: msEn }

/** The Microsoft Store dictionary in the UI language (the same language as useT). */
export function useMs(): MsDictionary {
  return msDictionaries[useI18n().lang]
}
