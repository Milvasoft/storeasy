import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { createPortal } from 'react-dom'
import { CalendarDays, ChevronLeft, ChevronRight, X } from 'lucide-react'
import type { DateOnly } from '@/lib/api/types'
import { useFormat, useI18n } from '@/lib/i18n/I18nContext'
import { cultureOf, type Lang } from '@/lib/i18n/lang'
import { addDays, parseDateOnly, toDateOnly, todayDateOnly } from '@/lib/utils/date'
import { cn } from '@/lib/utils/cn'
import { Button, IconButton } from './primitives'
import { usePopover } from './usePopover'

// The kit's own calendar (design 19 section 6): the browser's type="date" input is never used. Values are always
// "yyyy-MM-dd" (DateOnly), so there is no time zone shift.

export interface DateRange {
  from: DateOnly
  to: DateOnly
}

// Monday first in Turkish, Sunday first in US English.
function weekStart(lang: Lang): number {
  return lang === 'tr' ? 1 : 0
}

function monthMatrix(year: number, month: number, lang: Lang): Date[] {
  const first = new Date(year, month, 1)
  const offset = (first.getDay() - weekStart(lang) + 7) % 7
  const start = new Date(year, month, 1 - offset)
  return Array.from({ length: 42 }, (_, index) => new Date(start.getFullYear(), start.getMonth(), start.getDate() + index))
}

function weekdayNames(lang: Lang): string[] {
  const base = new Date(2026, 10, 16) // a Monday
  return Array.from({ length: 7 }, (_, index) => {
    const day = new Date(base)
    day.setDate(base.getDate() + ((index + weekStart(lang) - 1 + 7) % 7))
    return day.toLocaleDateString(cultureOf(lang), { weekday: 'short' }).slice(0, 2)
  })
}

interface CalendarProps {
  // The day the keyboard and the view start from.
  focusDay: DateOnly
  selected?: DateOnly | null
  range?: { from: DateOnly | null; to: DateOnly | null; hover?: DateOnly | null }
  min?: DateOnly
  max?: DateOnly
  onPick: (day: DateOnly) => void
  onHover?: (day: DateOnly | null) => void
  onEscape?: () => void
}

/** One month grid with month and year views; arrows move a day, PageUp/PageDown a month, Home/End the week. */
export function Calendar({ focusDay, selected, range, min, max, onPick, onHover, onEscape }: CalendarProps) {
  const { lang, t } = useI18n()
  const [cursor, setCursor] = useState<DateOnly>(focusDay)
  const [view, setView] = useState<'days' | 'months' | 'years'>('days')
  const gridRef = useRef<HTMLDivElement>(null)
  const cursorDate = parseDateOnly(cursor) ?? new Date()
  const year = cursorDate.getFullYear()
  const month = cursorDate.getMonth()
  const today = todayDateOnly()
  const days = useMemo(() => monthMatrix(year, month, lang), [year, month, lang])
  const culture = cultureOf(lang)

  useEffect(() => {
    if (view === 'days') {
      gridRef.current?.querySelector<HTMLElement>(`[data-day="${cursor}"]`)?.focus()
    }
  }, [cursor, view])

  const outOfBounds = (day: DateOnly) => (min !== undefined && day < min) || (max !== undefined && day > max)

  const shiftMonth = (count: number) => {
    const next = new Date(year, month + count, Math.min(cursorDate.getDate(), 28))
    setCursor(toDateOnly(next))
  }

  const onKeyDown = (event: KeyboardEvent) => {
    const moves: Record<string, () => DateOnly> = {
      ArrowLeft: () => addDays(cursor, -1),
      ArrowRight: () => addDays(cursor, 1),
      ArrowUp: () => addDays(cursor, -7),
      ArrowDown: () => addDays(cursor, 7),
      Home: () => addDays(cursor, -((cursorDate.getDay() - weekStart(lang) + 7) % 7)),
      End: () => addDays(cursor, 6 - ((cursorDate.getDay() - weekStart(lang) + 7) % 7)),
      PageUp: () => toDateOnly(new Date(year, month - 1, Math.min(cursorDate.getDate(), 28))),
      PageDown: () => toDateOnly(new Date(year, month + 1, Math.min(cursorDate.getDate(), 28))),
    }
    if (moves[event.key]) {
      event.preventDefault()
      setCursor(moves[event.key]())
    } else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      if (!outOfBounds(cursor)) {
        onPick(cursor)
      }
    } else if (event.key === 'Escape') {
      onEscape?.()
    }
  }

  const monthTitle = cursorDate.toLocaleDateString(culture, { month: 'long', year: 'numeric' })

  if (view === 'months') {
    return (
      <div className="grid gap-2 p-1">
        <button type="button" className="btn ghost sm justify-self-center" onClick={() => setView('years')}>
          {year}
        </button>
        <div className="grid grid-cols-3 gap-1">
          {Array.from({ length: 12 }, (_, index) => (
            <button
              key={index}
              type="button"
              className={cn('btn ghost sm', index === month && '!bg-bg2 !text-fg')}
              onClick={() => {
                setCursor(toDateOnly(new Date(year, index, 1)))
                setView('days')
              }}
            >
              {new Date(year, index, 1).toLocaleDateString(culture, { month: 'short' })}
            </button>
          ))}
        </div>
      </div>
    )
  }

  if (view === 'years') {
    const first = year - (year % 12) + 1
    return (
      <div className="grid gap-2 p-1">
        <span className="text-center text-xs font-semibold text-fg3">
          {first}-{first + 11}
        </span>
        <div className="grid grid-cols-3 gap-1">
          {Array.from({ length: 12 }, (_, index) => first + index).map((value) => (
            <button
              key={value}
              type="button"
              className={cn('btn ghost sm', value === year && '!bg-bg2 !text-fg')}
              onClick={() => {
                setCursor(toDateOnly(new Date(value, month, 1)))
                setView('months')
              }}
            >
              {value}
            </button>
          ))}
        </div>
      </div>
    )
  }

  return (
    <div className="grid gap-2">
      <div className="flex items-center justify-between gap-2">
        <IconButton label={t.datePicker.previousMonth} size="sm" ghost onClick={() => shiftMonth(-1)}>
          <ChevronLeft aria-hidden />
        </IconButton>
        <button type="button" className="btn ghost sm" aria-label={t.datePicker.chooseMonth} onClick={() => setView('months')}>
          {monthTitle}
        </button>
        <IconButton label={t.datePicker.nextMonth} size="sm" ghost onClick={() => shiftMonth(1)}>
          <ChevronRight aria-hidden />
        </IconButton>
      </div>
      <div
        ref={gridRef}
        role="grid"
        aria-label={monthTitle}
        className="grid grid-cols-7 gap-0.5 text-center"
        onKeyDown={onKeyDown}
        onMouseLeave={() => onHover?.(null)}
      >
        {weekdayNames(lang).map((name) => (
          <span key={name} role="columnheader" className="py-1 text-[11px] font-semibold text-fg3">
            {name}
          </span>
        ))}
        {days.map((date) => {
          const day = toDateOnly(date)
          const disabled = outOfBounds(day)
          const inMonth = date.getMonth() === month
          const isSelected = selected === day || range?.from === day || range?.to === day
          const rangeEnd = range?.to ?? range?.hover ?? null
          const inRange =
            !!range?.from &&
            !!rangeEnd &&
            day > (range.from < rangeEnd ? range.from : rangeEnd) &&
            day < (range.from < rangeEnd ? rangeEnd : range.from)
          return (
            <button
              key={day}
              type="button"
              role="gridcell"
              data-day={day}
              tabIndex={day === cursor ? 0 : -1}
              aria-selected={isSelected}
              aria-disabled={disabled || undefined}
              aria-label={date.toLocaleDateString(culture, { day: 'numeric', month: 'long', year: 'numeric', weekday: 'long' })}
              className={cn(
                'grid h-11 place-items-center rounded-sm text-[13px] tabular-nums sm:h-9',
                !inMonth && 'text-fg3',
                disabled && 'cursor-not-allowed opacity-40',
                !isSelected && !disabled && 'hover:bg-bg2',
                inRange && 'bg-accent/10',
                day === today && !isSelected && 'ring-1 ring-inset ring-fg',
                isSelected && 'bg-accent font-semibold text-accent-fg',
              )}
              onClick={() => {
                setCursor(day)
                if (!disabled) {
                  onPick(day)
                }
              }}
              onMouseEnter={() => onHover?.(day)}
            >
              {date.getDate()}
            </button>
          )
        })}
      </div>
    </div>
  )
}

/** A single day field: the trigger looks like the other inputs, with a calendar icon and a clear button. */
export function DatePicker({
  value,
  onChange,
  min,
  max,
  placeholder,
  id,
  ...aria
}: {
  value: DateOnly | null
  onChange: (value: DateOnly | null) => void
  min?: DateOnly
  max?: DateOnly
  placeholder?: string
  id?: string
  'aria-label'?: string
  'aria-invalid'?: boolean
  'aria-describedby'?: string
}) {
  const { t } = useI18n()
  const format = useFormat()
  const popover = usePopover()
  return (
    <span className="inp-wrap !pl-3">
      <CalendarDays aria-hidden />
      <button
        ref={popover.refs.setReference}
        id={id}
        type="button"
        className={cn('h-[38px] flex-1 text-left', !value && 'text-fg3')}
        aria-haspopup="dialog"
        aria-expanded={popover.open}
        onClick={() => popover.setOpen((open) => !open)}
        {...aria}
      >
        {value ? format.date(value) : (placeholder ?? t.datePicker.pick)}
      </button>
      {value ? (
        <IconButton label={t.datePicker.clear} size="sm" ghost onClick={() => onChange(null)}>
          <X aria-hidden />
        </IconButton>
      ) : null}
      {popover.open
        ? createPortal(
            <div
              ref={popover.refs.setFloating}
              style={popover.floatingStyles}
              className="pop w-[300px] !p-3"
              role="dialog"
              aria-label={aria['aria-label'] ?? t.datePicker.pick}
            >
              <Calendar
                focusDay={value ?? todayDateOnly()}
                selected={value}
                min={min}
                max={max}
                onEscape={() => popover.close()}
                onPick={(day) => {
                  onChange(day)
                  popover.close()
                }}
              />
            </div>,
            document.body,
          )
        : null}
    </span>
  )
}

export type RangePreset = 'today' | 'yesterday' | 'last7' | 'last30' | 'thisMonth' | 'lastMonth'

export function presetRange(preset: RangePreset, today: DateOnly = todayDateOnly()): DateRange {
  const date = parseDateOnly(today) ?? new Date()
  switch (preset) {
    case 'today':
      return { from: today, to: today }
    case 'yesterday':
      return { from: addDays(today, -1), to: addDays(today, -1) }
    case 'last7':
      return { from: addDays(today, -6), to: today }
    case 'last30':
      return { from: addDays(today, -29), to: today }
    case 'thisMonth':
      return { from: toDateOnly(new Date(date.getFullYear(), date.getMonth(), 1)), to: today }
    default:
      return {
        from: toDateOnly(new Date(date.getFullYear(), date.getMonth() - 1, 1)),
        to: toDateOnly(new Date(date.getFullYear(), date.getMonth(), 0)),
      }
  }
}

const PRESETS: RangePreset[] = ['today', 'yesterday', 'last7', 'last30', 'thisMonth', 'lastMonth']

/** A day range with ready choices on the left (design 19 section 6); the first click starts, the second ends it. */
export function DateRangePicker({
  value,
  onChange,
  max,
  label,
}: {
  value: DateRange | null
  onChange: (value: DateRange | null) => void
  max?: DateOnly
  label: string
}) {
  const { t } = useI18n()
  const format = useFormat()
  const popover = usePopover()
  const [draft, setDraft] = useState<{ from: DateOnly | null; to: DateOnly | null }>({ from: value?.from ?? null, to: value?.to ?? null })
  const [hover, setHover] = useState<DateOnly | null>(null)

  useEffect(() => {
    if (popover.open) {
      setDraft({ from: value?.from ?? null, to: value?.to ?? null })
    }
    // Copy the value in only when the panel opens.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [popover.open])

  const pick = (day: DateOnly) => {
    if (!draft.from || draft.to) {
      setDraft({ from: day, to: null })
    } else if (day < draft.from) {
      setDraft({ from: day, to: draft.from })
    } else {
      setDraft({ from: draft.from, to: day })
    }
  }

  const apply = (range: DateRange | null) => {
    onChange(range)
    popover.close()
  }

  const text = value
    ? value.from === value.to
      ? format.date(value.from)
      : `${format.date(value.from)} → ${format.date(value.to)}`
    : t.datePicker.anyDate

  return (
    <>
      <button
        ref={popover.refs.setReference}
        type="button"
        className={cn('chip', value && 'on')}
        aria-haspopup="dialog"
        aria-expanded={popover.open}
        aria-label={`${label}: ${text}`}
        onClick={() => popover.setOpen((open) => !open)}
      >
        <CalendarDays aria-hidden />
        {text}
      </button>
      {popover.open
        ? createPortal(
            <div
              ref={popover.refs.setFloating}
              style={popover.floatingStyles}
              className="pop w-[min(560px,calc(100vw-16px))] !p-3"
              role="dialog"
              aria-label={label}
            >
              <div className="grid gap-3 sm:grid-cols-[150px_1fr]">
                <div className="grid grid-cols-2 gap-1 sm:grid-cols-1 sm:content-start">
                  {PRESETS.map((preset) => (
                    <button key={preset} type="button" className="mi" onClick={() => apply(presetRange(preset))}>
                      {t.datePicker.presets[preset]}
                    </button>
                  ))}
                </div>
                <Calendar
                  focusDay={draft.from ?? todayDateOnly()}
                  range={{ ...draft, hover }}
                  max={max}
                  onPick={pick}
                  onHover={setHover}
                  onEscape={() => popover.close()}
                />
              </div>
              <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-border pt-3">
                <span className="mono text-xs text-fg3">
                  {draft.from ? `${draft.from} → ${draft.to ?? '...'}` : t.datePicker.pickStart}
                </span>
                <span className="ml-auto flex gap-2">
                  <Button size="sm" variant="ghost" onClick={() => apply(null)}>
                    {t.datePicker.clear}
                  </Button>
                  <Button
                    size="sm"
                    variant="primary"
                    disabled={!draft.from}
                    onClick={() => apply({ from: draft.from!, to: draft.to ?? draft.from! })}
                  >
                    {t.datePicker.apply}
                  </Button>
                </span>
              </div>
            </div>,
            document.body,
          )
        : null}
    </>
  )
}
