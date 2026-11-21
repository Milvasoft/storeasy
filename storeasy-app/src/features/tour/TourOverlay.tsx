import { useEffect, useId, useLayoutEffect, useRef, useState, type CSSProperties } from 'react'
import { createPortal } from 'react-dom'
import { ArrowLeft, ArrowRight } from 'lucide-react'
import { LogoMark } from '@/components/layout/Logo'
import { Button } from '@/components/ui/primitives'
import { usePrefersReducedMotion } from '@/lib/hooks/usePrefersReducedMotion'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { useTourText } from './i18n'
import type { ActiveStop } from './TourProvider'

interface Box {
  top: number
  left: number
  width: number
  height: number
}

// Room around the spotlighted element, the gap to the card and the screen edge margin.
const PAD = 6
const GAP = 14
const EDGE = 16
const PHONE = 640

function boxOf(element: HTMLElement): Box {
  const rect = element.getBoundingClientRect()
  return { top: rect.top - PAD, left: rect.left - PAD, width: rect.width + PAD * 2, height: rect.height + PAD * 2 }
}

function sameBox(a: Box | null, b: Box | null): boolean {
  return !!a && !!b && a.top === b.top && a.left === b.left && a.width === b.width && a.height === b.height
}

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), Math.max(min, max))

// Where the card goes: beside a narrow element at the left (the menu), else under or above it; on a phone it docks to
// the bottom edge (or the top when the element is low). Nothing fits: the bottom right corner.
function cardPosition(box: Box | null, card: { width: number; height: number }): CSSProperties {
  const vw = window.innerWidth
  const vh = window.innerHeight
  if (!box) {
    return { top: '50%', left: '50%', transform: 'translate(-50%, -50%)' }
  }
  if (vw < PHONE) {
    const low = box.top + box.height / 2 > vh / 2
    return low ? { top: EDGE, left: EDGE, right: EDGE } : { bottom: EDGE, left: EDGE, right: EDGE }
  }
  const left = clamp(box.left, EDGE, vw - card.width - EDGE)
  const sideFirst = box.left < vw * 0.25 && box.width < vw * 0.3
  const right = box.left + box.width + GAP
  const fitsRight = right + card.width <= vw - EDGE
  const below = box.top + box.height + GAP
  const fitsBelow = below + card.height <= vh - EDGE
  const above = box.top - GAP - card.height
  const fitsAbove = above >= EDGE
  const sideTop = clamp(box.top, EDGE, vh - card.height - EDGE)
  if (sideFirst && fitsRight) {
    return { top: sideTop, left: right }
  }
  if (fitsBelow) {
    return { top: below, left }
  }
  if (fitsAbove) {
    return { top: above, left }
  }
  if (fitsRight) {
    return { top: sideTop, left: right }
  }
  return { bottom: EDGE, right: EDGE }
}

const FOCUSABLE = 'button:not([disabled]), a[href]'

/**
 * The tour on screen: the page dimmed, a cut-out around the current element and the step card next to it. Esc ends the
 * tour, the arrow keys move; Tab stays in the card. While a step waits for its page only the dim shows.
 */
export function TourOverlay({
  active,
  index,
  total,
  onBack,
  onNext,
  onExit,
}: {
  active: ActiveStop | null
  index: number
  total: number
  onBack: () => void
  onNext: () => void
  onExit: () => void
}) {
  const t = useTourText()
  const reduced = usePrefersReducedMotion()
  const titleId = useId()
  const bodyId = useId()
  const cardRef = useRef<HTMLDivElement>(null)
  const nextRef = useRef<HTMLButtonElement>(null)
  const [box, setBox] = useState<Box | null>(null)
  const [cardSize, setCardSize] = useState({ width: 380, height: 240 })
  const element = active?.element ?? null
  const last = index === total - 1

  // Bring the element into view, then follow it (scrolling, resizing, a page that grows) frame by frame.
  useEffect(() => {
    if (!element) {
      setBox(null)
      return
    }
    // A small element is centered; a tall one (the whole menu) is only brought in when its top is off screen.
    const rect = element.getBoundingClientRect()
    const tall = rect.height > window.innerHeight * 0.6
    const offScreen = rect.top < 0 || rect.bottom > window.innerHeight
    if (offScreen && (!tall || rect.top < 0 || rect.top > window.innerHeight)) {
      element.scrollIntoView?.({ block: tall ? 'start' : 'center', inline: 'nearest', behavior: reduced ? 'auto' : 'smooth' })
    }
    let frame = 0
    let previous: Box | null = null
    const follow = () => {
      const next = boxOf(element)
      if (!sameBox(previous, next)) {
        previous = next
        setBox(next)
      }
      frame = window.requestAnimationFrame(follow)
    }
    follow()
    return () => window.cancelAnimationFrame(frame)
  }, [element, reduced])

  // The card's real size, measured once per step, places it next to the element.
  useLayoutEffect(() => {
    const rect = cardRef.current?.getBoundingClientRect()
    if (rect && rect.width > 0) {
      setCardSize((size) => (size.width === rect.width && size.height === rect.height ? size : { width: rect.width, height: rect.height }))
    }
  }, [active])

  // The primary button takes the focus on every step, so Enter moves on.
  useEffect(() => {
    if (active) {
      nextRef.current?.focus()
    }
  }, [active])

  const handlers = useRef({ onBack, onNext, onExit, index })
  handlers.current = { onBack, onNext, onExit, index }
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const current = handlers.current
      if (event.key === 'Escape') {
        event.preventDefault()
        event.stopPropagation()
        current.onExit()
      } else if (event.key === 'ArrowRight') {
        event.preventDefault()
        current.onNext()
      } else if (event.key === 'ArrowLeft') {
        event.preventDefault()
        if (current.index > 0) {
          current.onBack()
        }
      } else if (event.key === 'Tab' && cardRef.current) {
        const focusables = Array.from(cardRef.current.querySelectorAll<HTMLElement>(FOCUSABLE))
        if (focusables.length === 0) {
          return
        }
        const first = focusables[0]
        const end = focusables[focusables.length - 1]
        if (!cardRef.current.contains(document.activeElement)) {
          event.preventDefault()
          first.focus()
        } else if (event.shiftKey && document.activeElement === first) {
          event.preventDefault()
          end.focus()
        } else if (!event.shiftKey && document.activeElement === end) {
          event.preventDefault()
          first.focus()
        }
      }
    }
    window.addEventListener('keydown', onKeyDown, true)
    return () => window.removeEventListener('keydown', onKeyDown, true)
  }, [])

  const text = active ? t.steps[active.stop.id] : null
  const spotlight = active && box

  return createPortal(
    <div className="tour" data-testid="tour">
      {/* Catches clicks, so the page under the tour cannot be used by accident. */}
      <div className="tour-block" aria-hidden />
      {spotlight ? (
        <div className="tour-spot" aria-hidden style={{ top: box.top, left: box.left, width: box.width, height: box.height }} />
      ) : (
        <div className="tour-dim animate-fade-in" aria-hidden />
      )}
      {active && text ? (
        <div
          ref={cardRef}
          key={active.stop.id}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          aria-describedby={bodyId}
          className={cn('tour-card', spotlight ? 'animate-scale-in' : 'animate-slide-up sm:animate-scale-in', !spotlight && 'centered')}
          style={cardPosition(spotlight ? box : null, cardSize)}
        >
          <div className="flex items-center gap-2">
            <LogoMark size={18} />
            <span className="text-[12px] font-semibold text-fg2">{t.label}</span>
            <span className="mono ml-auto text-[12px] text-fg3" aria-live="polite">
              {interpolate(t.card.counter, { current: index + 1, total })}
            </span>
          </div>
          <div className="tour-progress" aria-hidden>
            <i style={{ width: `${((index + 1) / total) * 100}%` }} />
          </div>
          <h2 id={titleId} className="m-0 mt-3 font-display text-[19px] font-bold leading-tight tracking-tight">
            {text.title}
          </h2>
          <p id={bodyId} className="m-0 mt-1.5 text-[14px] leading-relaxed text-fg2">
            {text.body}
          </p>
          <div className="mt-4 flex items-center gap-2">
            <button type="button" className="lnk text-[13px] text-fg3" onClick={onExit}>
              {t.card.exit}
            </button>
            <span className="ml-auto flex gap-2">
              {index > 0 ? (
                <Button size="sm" onClick={onBack}>
                  <ArrowLeft aria-hidden />
                  {t.card.back}
                </Button>
              ) : null}
              <Button ref={nextRef} size="sm" variant="primary" onClick={onNext}>
                {last ? t.card.finish : t.card.next}
                {last ? null : <ArrowRight aria-hidden />}
              </Button>
            </span>
          </div>
          <p className="m-0 mt-3 text-[11.5px] text-fg3 max-sm:hidden">{t.card.keys}</p>
        </div>
      ) : null}
    </div>,
    document.body,
  )
}
