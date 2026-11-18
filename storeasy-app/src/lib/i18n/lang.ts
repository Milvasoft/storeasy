import { readStorage, StorageKey, writeStorage } from '@/lib/utils/storage'

export type Lang = 'tr' | 'en'

export const DEFAULT_LANG: Lang = 'tr'

export const LANGS: readonly Lang[] = ['tr', 'en']

// Backend cultures (RequestLocalization): what Accept-Language and Intl get.
const CULTURES: Record<Lang, string> = { tr: 'tr-TR', en: 'en-US' }

export function cultureOf(lang: Lang): string {
  return CULTURES[lang]
}

function detectLang(): Lang {
  const browser = typeof navigator !== 'undefined' ? navigator.language.toLowerCase() : ''
  return browser.startsWith('en') ? 'en' : DEFAULT_LANG
}

// The UI language: the stored choice, else the browser's. The http layer and the provider both read this one function,
// so the server's messages always arrive in the language the UI shows.
export function readLang(): Lang {
  const stored = readStorage(StorageKey.Lang)
  if (stored === 'tr' || stored === 'en') {
    return stored
  }
  return detectLang()
}

export function writeLang(lang: Lang): void {
  writeStorage(StorageKey.Lang, lang)
}
