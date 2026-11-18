import { useI18n } from '@/lib/i18n/I18nContext'
import { LANGS } from '@/lib/i18n/lang'
import { cn } from '@/lib/utils/cn'

/** "TR · EN" in the auth footer (design 5.1): works before sign-in, the choice is kept in this browser. */
export function LanguageSwitcher() {
  const { lang, setLang, t } = useI18n()
  return (
    <span className="flex items-center gap-1" role="group" aria-label={t.language.label}>
      {LANGS.map((value, index) => (
        <span key={value} className="flex items-center gap-1">
          {index > 0 ? <span aria-hidden>·</span> : null}
          <button
            type="button"
            lang={value}
            aria-pressed={lang === value}
            aria-label={t.language.names[value]}
            className={cn('min-h-[28px] px-1 uppercase', lang === value ? 'font-bold text-fg' : 'text-fg3 hover:text-fg')}
            onClick={() => setLang(value)}
          >
            {value}
          </button>
        </span>
      ))}
    </span>
  )
}
