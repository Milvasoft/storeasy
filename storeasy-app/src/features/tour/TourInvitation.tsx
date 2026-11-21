import { useEffect, useId, useRef } from 'react'
import { createPortal } from 'react-dom'
import { Activity, ArrowRight, Layers, ShieldCheck, type LucideIcon } from 'lucide-react'
import { LogoMark } from '@/components/layout/Logo'
import { Button } from '@/components/ui/primitives'
import { interpolate } from '@/lib/i18n/interpolate'
import { useTourText } from './i18n'

const GAIN_ICONS: LucideIcon[] = [Layers, ShieldCheck, Activity]

/**
 * The first visit invitation: the whole screen, one question. It never starts the tour by itself; either answer is
 * final (the invitation does not come back), and "not now" says where the tour waits.
 */
export function TourInvitation({ stepCount, onStart, onLater }: { stepCount: number; onStart: () => void; onLater: () => void }) {
  const t = useTourText()
  const titleId = useId()
  const bodyId = useId()
  const startRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)
  const later = useRef(onLater)
  later.current = onLater

  useEffect(() => {
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    startRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        later.current()
        return
      }
      if (event.key !== 'Tab' || !panelRef.current) {
        return
      }
      const buttons = Array.from(panelRef.current.querySelectorAll<HTMLElement>('button'))
      const first = buttons[0]
      const last = buttons[buttons.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }
    window.addEventListener('keydown', onKeyDown, true)
    return () => {
      window.removeEventListener('keydown', onKeyDown, true)
      document.body.style.overflow = previousOverflow
    }
  }, [])

  return createPortal(
    <div
      ref={panelRef}
      className="tour-invite animate-fade-in"
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
      aria-describedby={bodyId}
    >
      <LogoMark size={560} className="mk-bg" />
      <div className="relative mx-auto flex min-h-full max-w-[880px] flex-col justify-center px-6 py-14 sm:px-10">
        <div className="tour-invite-in grid gap-6 sm:gap-7">
          <div className="flex items-center gap-3">
            <LogoMark size={40} />
            <span className="text-[13px] font-semibold uppercase tracking-[0.14em] text-accent-text">{t.invite.eyebrow}</span>
          </div>
          <h1
            id={titleId}
            className="m-0 font-display text-[40px] font-extrabold leading-[1.02] tracking-[-0.03em] text-fg sm:text-[64px] lg:text-[76px]"
          >
            {t.invite.title}
          </h1>
          <p id={bodyId} className="m-0 max-w-[620px] text-[17px] leading-relaxed text-fg2 sm:text-[19px]">
            {t.invite.body}
          </p>
          <ul className="m-0 grid list-none gap-2.5 p-0 sm:grid-cols-3 sm:gap-3">
            {t.invite.gains.map((gain, index) => {
              const Icon = GAIN_ICONS[index] ?? Layers
              return (
                <li
                  key={gain}
                  className="flex items-center gap-3 rounded-md border border-border bg-surface/70 px-3.5 py-3 text-[14px] font-medium"
                >
                  <span className="grid h-8 w-8 flex-none place-items-center rounded-sm bg-accent/10 text-accent-text">
                    <Icon className="h-4 w-4" aria-hidden />
                  </span>
                  {gain}
                </li>
              )
            })}
          </ul>
          <div className="flex flex-wrap items-center gap-3 pt-1">
            <Button ref={startRef} variant="primary" className="!h-12 !px-6 !text-[16px]" onClick={onStart}>
              {t.invite.start}
              <ArrowRight aria-hidden />
            </Button>
            <Button variant="ghost" className="!h-12 !px-5 !text-[15px]" onClick={onLater}>
              {t.invite.later}
            </Button>
            <span className="mono text-[12px] text-fg3 max-sm:w-full">{interpolate(t.invite.meta, { count: stepCount })}</span>
          </div>
          <p className="m-0 text-[13px] text-fg3">{t.invite.laterHint}</p>
        </div>
      </div>
    </div>,
    document.body,
  )
}
