import { cloneElement, useEffect, useId, useRef, useState, type KeyboardEvent, type ReactElement, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { autoUpdate, flip, offset, shift, useFloating, type Placement } from '@floating-ui/react-dom'
import { cn } from '@/lib/utils/cn'
import { usePopover } from './usePopover'

export interface MenuItem {
  key: string
  label: ReactNode
  icon?: ReactNode
  onSelect?: () => void
  danger?: boolean
  disabled?: boolean
  // A selected choice (theme, language): shown with a check-like highlight and aria-checked.
  checked?: boolean
}

export type MenuEntry =
  MenuItem | { key: string; separator: true } | { key: string; heading: ReactNode } | { key: string; content: ReactNode }

/**
 * The kit's menu (.pop): a heading, separators, the dangerous item last in red. The trigger gets aria-haspopup and
 * aria-expanded; arrows move between items, Escape closes and returns focus to the trigger.
 */
export function Menu({
  trigger,
  entries,
  label,
  placement = 'bottom-end',
  className,
}: {
  trigger: ReactElement
  entries: MenuEntry[]
  label: string
  placement?: Placement
  className?: string
}) {
  const menuId = useId()
  const popover = usePopover({ placement })
  const panelRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (popover.open) {
      panelRef.current?.querySelector<HTMLElement>('[role^="menuitem"]:not([aria-disabled="true"])')?.focus()
    }
  }, [popover.open])

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const items = Array.from(panelRef.current?.querySelectorAll<HTMLElement>('[role^="menuitem"]:not([aria-disabled="true"])') ?? [])
    const index = items.indexOf(document.activeElement as HTMLElement)
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      items[(index + 1) % items.length]?.focus()
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      items[(index - 1 + items.length) % items.length]?.focus()
    } else if (event.key === 'Tab') {
      popover.close(false)
    }
  }

  const triggerElement = cloneElement(trigger as ReactElement<Record<string, unknown>>, {
    ref: popover.refs.setReference,
    'aria-haspopup': 'menu',
    'aria-expanded': popover.open,
    'aria-controls': popover.open ? menuId : undefined,
    onClick: () => popover.setOpen((open) => !open),
  })

  return (
    <>
      {triggerElement}
      {popover.open
        ? createPortal(
            <div
              ref={(node) => {
                panelRef.current = node
                popover.refs.setFloating(node)
              }}
              id={menuId}
              role="menu"
              aria-label={label}
              style={popover.floatingStyles}
              className={cn('pop', className)}
              onKeyDown={onKeyDown}
            >
              {entries.map((entry) => {
                if ('separator' in entry) {
                  return <div key={entry.key} className="ms" role="separator" />
                }
                if ('heading' in entry) {
                  return (
                    <div key={entry.key} className="mh" role="presentation">
                      {entry.heading}
                    </div>
                  )
                }
                if ('content' in entry) {
                  return <div key={entry.key}>{entry.content}</div>
                }
                return <MenuRow key={entry.key} item={entry} onDone={() => popover.close()} />
              })}
            </div>,
            document.body,
          )
        : null}
    </>
  )
}

function MenuRow({ item, onDone }: { item: MenuItem; onDone: () => void }) {
  const checkable = item.checked !== undefined
  return (
    <button
      type="button"
      role={checkable ? 'menuitemradio' : 'menuitem'}
      aria-checked={checkable ? item.checked : undefined}
      aria-disabled={item.disabled || undefined}
      tabIndex={-1}
      className={cn('mi', item.danger && 'dng', item.checked && 'on')}
      onClick={() => {
        if (item.disabled) {
          return
        }
        onDone()
        item.onSelect?.()
      }}
    >
      {item.icon}
      <span className="min-w-0 flex-1">{item.label}</span>
    </button>
  )
}

/** A short text in inverse colors over its trigger (hover and keyboard focus). Never the only place of information. */
export function Tooltip({ content, children, placement = 'top' }: { content: string; children: ReactElement; placement?: Placement }) {
  const [open, setOpen] = useState(false)
  const id = useId()
  const { refs, floatingStyles } = useFloating({
    open,
    placement,
    strategy: 'fixed',
    whileElementsMounted: autoUpdate,
    middleware: [offset(6), flip(), shift({ padding: 8 })],
  })
  const child = cloneElement(children as ReactElement<Record<string, unknown>>, {
    ref: refs.setReference,
    'aria-describedby': open ? id : undefined,
    onMouseEnter: () => setOpen(true),
    onMouseLeave: () => setOpen(false),
    onFocus: () => setOpen(true),
    onBlur: () => setOpen(false),
  })
  return (
    <>
      {child}
      {open
        ? createPortal(
            <div ref={refs.setFloating} id={id} role="tooltip" className="tip" style={floatingStyles}>
              {content}
            </div>,
            document.body,
          )
        : null}
    </>
  )
}
