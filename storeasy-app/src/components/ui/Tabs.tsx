import type { KeyboardEvent, ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { cn } from '@/lib/utils/cn'

export interface TabItem<T extends string> {
  value: T
  label: ReactNode
  // A mono counter; danger tone when something blocks.
  count?: number
  countTone?: 'danger'
  // A route tab: the tab is a link and selection follows the address.
  to?: string
}

function moveFocus(event: KeyboardEvent<HTMLElement>) {
  if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') {
    return
  }
  const tabs = Array.from(event.currentTarget.querySelectorAll<HTMLElement>('[role="tab"]'))
  const index = tabs.indexOf(document.activeElement as HTMLElement)
  const next = tabs[(index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length]
  next?.focus()
  next?.click()
}

function Count({ count, tone }: { count?: number; tone?: 'danger' }) {
  return count !== undefined ? <span className={cn('n', tone === 'danger' && 'dng')}>{count}</span> : null
}

/**
 * The kit's tabs: the selected one has the accent underline and bold text; counters are mono. Scrolls on phones. Route
 * tabs (items with a `to`) are a navigation of links (aria-current); in-page tabs are a tablist.
 */
export function Tabs<T extends string>({
  items,
  value,
  onChange,
  label,
}: {
  items: TabItem<T>[]
  value?: T
  onChange?: (value: T) => void
  label: string
}) {
  if (items.some((item) => item.to)) {
    return (
      <nav className="tabs" aria-label={label}>
        {items.map((item) => (
          <NavLink key={item.value} to={item.to ?? '.'} end className={({ isActive }) => cn('tab', isActive && 'on')}>
            {item.label}
            <Count count={item.count} tone={item.countTone} />
          </NavLink>
        ))}
      </nav>
    )
  }
  return (
    <div className="tabs" role="tablist" aria-label={label} onKeyDown={moveFocus}>
      {items.map((item) => (
        <button
          key={item.value}
          type="button"
          role="tab"
          aria-selected={item.value === value}
          tabIndex={item.value === value ? 0 : -1}
          className={cn('tab', item.value === value && 'on')}
          onClick={() => onChange?.(item.value)}
        >
          {item.label}
          <Count count={item.count} tone={item.countTone} />
        </button>
      ))}
    </div>
  )
}

/** A segmented choice (kit .seg) used as a radio group: theme, store filter, view. */
export function SegmentedControl<T extends string>({
  options,
  value,
  onChange,
  label,
}: {
  options: Array<{ value: T; label: ReactNode }>
  value: T
  onChange: (value: T) => void
  label: string
}) {
  return (
    <div className="seg" role="radiogroup" aria-label={label}>
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          role="radio"
          aria-checked={option.value === value}
          className={cn(option.value === value && 'on')}
          onClick={() => onChange(option.value)}
        >
          {option.label}
        </button>
      ))}
    </div>
  )
}
