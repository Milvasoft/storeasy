import type { ReactNode } from 'react'
import { Check, X } from 'lucide-react'
import { cn } from '@/lib/utils/cn'

// Progress is the accent's job (design 19 section 2); the value is also given as text by the caller.
export function Progress({
  value,
  max = 100,
  tone,
  small,
  label,
}: {
  value: number
  max?: number
  tone?: 'ok' | 'warn' | 'dng' | 'neu'
  small?: boolean
  label: string
}) {
  const percent = max > 0 ? Math.min(100, Math.max(0, (value / max) * 100)) : 0
  return (
    <div
      className={cn('prog', tone, small && 'sm')}
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={max}
      aria-valuenow={value}
    >
      <i style={{ width: `${percent}%` }} />
    </div>
  )
}

export type StepState = 'done' | 'now' | 'failed' | 'pending'

const STEP_CLASS: Record<StepState, string | undefined> = { done: 'd', now: 'n', failed: 'x', pending: undefined }

// A step bar: one segment per step, the running one breathes (stopped under reduced motion).
export function StepBar({ steps, label }: { steps: StepState[]; label: string }) {
  return (
    <div className="stp" role="img" aria-label={label}>
      {steps.map((state, index) => (
        <span key={index} className={STEP_CLASS[state]} />
      ))}
    </div>
  )
}

export type TimelineTone = 'ok' | 'now' | 'dng' | 'warn' | 'pending'

export function Timeline({ items }: { items: Array<{ id: string | number; tone: TimelineTone; title: ReactNode; meta?: ReactNode }> }) {
  return (
    <ol className="m-0 grid list-none p-0">
      {items.map((item) => (
        <li key={item.id} className="tl-i">
          <span className={cn('tl-d', item.tone !== 'pending' && item.tone)} aria-hidden />
          <div>
            <div className="text-[13.5px] font-semibold">{item.title}</div>
            {item.meta ? <div className="mt-0.5 text-xs text-fg3">{item.meta}</div> : null}
          </div>
        </li>
      ))}
    </ol>
  )
}

export type ChecklistState = 'ok' | 'missing' | 'warning' | 'assisted'

const CHECK_CLASS: Record<ChecklistState, string> = { ok: 'ok', missing: 'dng', warning: 'warn', assisted: 'asst' }

export function Checklist({
  items,
}: {
  items: Array<{ id: string; state: ChecklistState; title: ReactNode; detail?: ReactNode; status: string }>
}) {
  return (
    <ul className="m-0 grid list-none p-0">
      {items.map((item) => (
        <li key={item.id} className="ck-i">
          <span className={cn('ck-s', CHECK_CLASS[item.state])} aria-hidden>
            {item.state === 'ok' ? <Check /> : item.state === 'missing' ? <X /> : null}
          </span>
          <div className="min-w-0 flex-1">
            <b className="text-[13.5px]">{item.title}</b>
            {item.detail ? <div className="text-[12.5px] text-fg3">{item.detail}</div> : null}
          </div>
          <span className="text-xs font-semibold text-fg2">{item.status}</span>
        </li>
      ))}
    </ul>
  )
}
