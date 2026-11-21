import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'
import { wingetEn, type WingetDictionary } from './en'
import { wingetTr } from './tr'

export const wingetDictionaries: Record<Lang, WingetDictionary> = { tr: wingetTr, en: wingetEn }

/** The winget dictionary in the UI language (the same language as useT). */
export function useWinget(): WingetDictionary {
  return wingetDictionaries[useI18n().lang]
}
