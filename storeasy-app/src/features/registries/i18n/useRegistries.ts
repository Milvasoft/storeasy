import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'
import { registriesEn, type RegistriesDictionary } from './en'
import { registriesTr } from './tr'

export const registriesDictionaries: Record<Lang, RegistriesDictionary> = { tr: registriesTr, en: registriesEn }

/** The package registries dictionary in the UI language (the same language as useT). */
export function useRegistries(): RegistriesDictionary {
  return registriesDictionaries[useI18n().lang]
}
