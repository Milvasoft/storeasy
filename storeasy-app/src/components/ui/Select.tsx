import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Check, ChevronDown, Search } from 'lucide-react'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { usePopover } from './usePopover'

export interface SelectOption<T extends string | number> {
  value: T
  label: string
  description?: string
  disabled?: boolean
  // Why the option is disabled ("Only through an ownership transfer").
  disabledReason?: string
}

interface SelectProps<T extends string | number> {
  value: T | null
  options: SelectOption<T>[]
  onChange: (value: T) => void
  placeholder?: string
  searchable?: boolean
  disabled?: boolean
  id?: string
  className?: string
  'aria-label'?: string
  'aria-invalid'?: boolean
  'aria-describedby'?: string
}

// Turkish folding for search: "is" finds both "İsveç" and "Isparta" (design 19: searchable select).
export function foldSearch(value: string): string {
  return value.toLocaleLowerCase('tr').replace(/ı/g, 'i').normalize('NFD').replace(/[̀-ͯ]/g, '')
}

/**
 * The kit's select: a field-sized trigger and a portal listbox the width of the trigger, with keyboard navigation
 * (arrows, Home/End, Enter, Escape, type to jump) and an optional search box. Native-like onChange(value).
 */
export function Select<T extends string | number>({
  value,
  options,
  onChange,
  placeholder,
  searchable,
  disabled,
  id,
  className,
  ...aria
}: SelectProps<T>) {
  const t = useT()
  const listId = useId()
  const popover = usePopover({ matchWidth: true })
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)
  const searchRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLDivElement>(null)

  const visible = useMemo(() => {
    const needle = foldSearch(query.trim())
    return needle ? options.filter((option) => foldSearch(option.label).includes(needle)) : options
  }, [options, query])

  const selected = options.find((option) => option.value === value) ?? null

  useEffect(() => {
    if (!popover.open) {
      return
    }
    setQuery('')
    const index = Math.max(
      0,
      options.findIndex((option) => option.value === value),
    )
    setActive(index)
    if (searchable) {
      searchRef.current?.focus()
    } else {
      listRef.current?.focus()
    }
    // Reset only when the panel opens.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [popover.open])

  const pick = (option: SelectOption<T> | undefined) => {
    if (!option || option.disabled) {
      return
    }
    onChange(option.value)
    popover.close()
  }

  const move = (step: number) => {
    if (visible.length === 0) {
      return
    }
    let next = active
    for (let i = 0; i < visible.length; i += 1) {
      next = (next + step + visible.length) % visible.length
      if (!visible[next].disabled) {
        break
      }
    }
    setActive(next)
  }

  const onListKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      move(1)
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      move(-1)
    } else if (event.key === 'Home') {
      event.preventDefault()
      setActive(0)
    } else if (event.key === 'End') {
      event.preventDefault()
      setActive(visible.length - 1)
    } else if (event.key === 'Enter') {
      event.preventDefault()
      pick(visible[active])
    } else if (event.key === 'Tab') {
      popover.close(false)
    } else if (!searchable && event.key.length === 1) {
      // Type to jump to the first option starting with the letter.
      const letter = foldSearch(event.key)
      const index = visible.findIndex((option) => foldSearch(option.label).startsWith(letter))
      if (index >= 0) {
        setActive(index)
      }
    }
  }

  return (
    <>
      <button
        ref={popover.refs.setReference}
        id={id}
        type="button"
        className={cn('inp justify-between text-left', !selected && 'text-fg3', className)}
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={popover.open}
        aria-controls={popover.open ? listId : undefined}
        onClick={() => popover.setOpen((open) => !open)}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault()
            popover.setOpen(true)
          }
        }}
        {...aria}
      >
        <span className="truncate">{selected?.label ?? placeholder ?? t.common.select}</span>
        <ChevronDown className="h-4 w-4 flex-none text-fg3" aria-hidden />
      </button>
      {popover.open
        ? createPortal(
            <div ref={popover.refs.setFloating} style={popover.floatingStyles} className="pop">
              {searchable ? (
                <span className="inp-wrap mb-1 !min-h-[34px]">
                  <Search aria-hidden />
                  <input
                    ref={searchRef}
                    value={query}
                    aria-label={t.common.search}
                    aria-controls={listId}
                    placeholder={t.common.search}
                    onChange={(event) => {
                      setQuery(event.target.value)
                      setActive(0)
                    }}
                    onKeyDown={onListKeyDown}
                  />
                </span>
              ) : null}
              <div
                ref={listRef}
                id={listId}
                role="listbox"
                tabIndex={-1}
                aria-activedescendant={visible[active] ? `${listId}-${active}` : undefined}
                onKeyDown={onListKeyDown}
                className="outline-none"
              >
                {visible.map((option, index) => (
                  <OptionRow
                    key={String(option.value)}
                    id={`${listId}-${index}`}
                    active={index === active}
                    selected={option.value === value}
                    option={option}
                    onPick={() => pick(option)}
                    onHover={() => setActive(index)}
                  />
                ))}
                {visible.length === 0 ? (
                  <div className="px-2.5 py-2 text-[13px] text-fg3">{interpolate(t.common.noResultsFor, { term: query.trim() })}</div>
                ) : null}
              </div>
            </div>,
            document.body,
          )
        : null}
    </>
  )
}

function OptionRow<T extends string | number>({
  id,
  option,
  active,
  selected,
  onPick,
  onHover,
}: {
  id: string
  option: SelectOption<T>
  active: boolean
  selected: boolean
  onPick: () => void
  onHover: () => void
}): ReactNode {
  return (
    <div
      id={id}
      role="option"
      aria-selected={selected}
      aria-disabled={option.disabled || undefined}
      data-active={active}
      className="mi !items-start !whitespace-normal"
      onClick={onPick}
      onMouseMove={onHover}
    >
      <span className="grid min-w-0 flex-1 gap-0.5">
        <span className="font-medium">{option.label}</span>
        {option.description || option.disabledReason ? (
          <span className="text-xs text-fg3">{option.disabled ? (option.disabledReason ?? option.description) : option.description}</span>
        ) : null}
      </span>
      {selected ? <Check className="h-4 w-4 flex-none text-accent" aria-hidden /> : null}
    </div>
  )
}
