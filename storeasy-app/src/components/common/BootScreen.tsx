import { useEffect, useState } from 'react'
import { LogoMark } from '@/components/layout/Logo'
import { useT } from '@/lib/i18n/I18nContext'

// Below this the screen is never shown, so a fast start does not flash (design 18.21).
const SHOW_AFTER_MS = 400
const SLOW_AFTER_MS = 10_000

/** The start screen while a stored session is checked: the mark turns (and stands still under reduced motion). */
export function BootScreen() {
  const t = useT()
  const [visible, setVisible] = useState(false)
  const [slow, setSlow] = useState(false)

  useEffect(() => {
    const show = window.setTimeout(() => setVisible(true), SHOW_AFTER_MS)
    const late = window.setTimeout(() => setSlow(true), SLOW_AFTER_MS)
    return () => {
      window.clearTimeout(show)
      window.clearTimeout(late)
    }
  }, [])

  return (
    <div className="grid min-h-full place-items-center bg-bg" role="status" aria-live="polite">
      {visible ? (
        <div className="grid justify-items-center gap-3 text-center">
          <LogoMark size={64} spinning />
          <span className="text-[13px] text-fg2">{t.boot.checking}</span>
          {slow ? <span className="text-xs text-fg3">{t.boot.slow}</span> : null}
        </div>
      ) : (
        <span className="sr-only">{t.common.loading}</span>
      )}
    </div>
  )
}
