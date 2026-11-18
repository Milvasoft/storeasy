import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { dictionaries } from './dictionaries'
import type { Dictionary } from './en'
import { createFormatters, type Formatters } from './format'
import { readLang, writeLang, type Lang } from './lang'

interface I18nContextValue {
  lang: Lang
  setLang: (lang: Lang) => void
  t: Dictionary
}

const I18nContext = createContext<I18nContextValue | null>(null)

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(readLang)
  const queryClient = useQueryClient()

  // The account's language is not stored on the API yet (PUT account/preferences is planned, design 16.1), so the choice
  // lives in this browser.
  const setLang = useCallback(
    (next: Lang) => {
      setLangState(next)
      writeLang(next)
      // Server messages and translated lookups must come again in the new language.
      void queryClient.invalidateQueries()
    },
    [queryClient],
  )

  // Keep <html lang> in step with the UI language (screen readers, hyphenation, browser translation prompts).
  useEffect(() => {
    document.documentElement.lang = lang
  }, [lang])

  const value = useMemo<I18nContextValue>(() => ({ lang, setLang, t: dictionaries[lang] }), [lang, setLang])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext)
  if (!ctx) {
    throw new Error('useI18n must be used within I18nProvider')
  }
  return ctx
}

export function useT(): Dictionary {
  return useI18n().t
}

// The formatters bound to the current language, so no call site can forget to pass it.
export function useFormat(): Formatters {
  const { lang, t } = useI18n()
  return useMemo(() => createFormatters(lang, t), [lang, t])
}
