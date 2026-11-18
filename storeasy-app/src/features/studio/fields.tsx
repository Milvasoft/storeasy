import { useEffect, useState, type ReactNode } from 'react'
import { Select } from '@/components/ui/Select'
import { Field, Input, Switch } from '@/components/ui/primitives'
import { useStudioT } from './i18n'

// The property panel's small controls. Each is labelled (getByLabelText) and reports a parsed value.

export function useDebounced<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delayMs)
    return () => window.clearTimeout(timer)
  }, [value, delayMs])
  return debounced
}

export function NumberField({
  label,
  value,
  onChange,
  min,
  max,
  step = 1,
  suffix,
}: {
  label: string
  value: number
  onChange: (value: number) => void
  min?: number
  max?: number
  step?: number
  suffix?: string
}) {
  const [text, setText] = useState(String(value))
  const [focused, setFocused] = useState(false)
  // Follows the document while the field is not being typed in (undo, reload, another control).
  const shown = focused ? text : String(value)
  return (
    <Field label={suffix ? `${label} (${suffix})` : label}>
      <Input
        type="number"
        inputMode="decimal"
        value={shown}
        min={min}
        max={max}
        step={step}
        onFocus={() => {
          setText(String(value))
          setFocused(true)
        }}
        onBlur={() => setFocused(false)}
        onChange={(event) => {
          setText(event.target.value)
          const next = Number(event.target.value)
          if (event.target.value !== '' && Number.isFinite(next)) {
            onChange(min !== undefined || max !== undefined ? Math.min(max ?? next, Math.max(min ?? next, next)) : next)
          }
        }}
      />
    </Field>
  )
}

const HEX = /^#[0-9a-fA-F]{6}$/

export function ColorField({
  label,
  value,
  onChange,
  optional,
}: {
  label: string
  value: string | null | undefined
  onChange: (value: string | null) => void
  optional?: boolean
}) {
  const s = useStudioT()
  const [text, setText] = useState(value ?? '')
  const [focused, setFocused] = useState(false)
  const shown = focused ? text : (value ?? '')
  const invalid = shown !== '' && !HEX.test(shown)
  return (
    <div className="flex items-end gap-2">
      <span
        className="mb-[9px] h-6 w-6 flex-none rounded border border-borderControl"
        style={{ background: HEX.test(shown) ? shown : 'transparent' }}
        aria-hidden
      />
      <div className="min-w-0 flex-1">
        <Field label={label} optional={optional} error={invalid ? s.props.colorInvalid : null}>
          <Input
            mono
            value={shown}
            placeholder="#RRGGBB"
            maxLength={7}
            onFocus={() => {
              setText(value ?? '')
              setFocused(true)
            }}
            onBlur={() => setFocused(false)}
            onChange={(event) => {
              const next = event.target.value.trim()
              setText(next)
              if (HEX.test(next)) {
                onChange(next.toUpperCase())
              } else if (next === '' && optional) {
                onChange(null)
              }
            }}
          />
        </Field>
      </div>
    </div>
  )
}

export function SelectField<T extends string | number>({
  label,
  value,
  options,
  onChange,
}: {
  label: string
  value: T | null
  options: Array<{ value: T; label: string }>
  onChange: (value: T) => void
}) {
  return (
    <Field label={label}>
      <Select value={value} options={options} onChange={onChange} />
    </Field>
  )
}

export function SwitchRow({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <div className="flex items-center justify-between gap-3 text-[13px]">
      <span>{label}</span>
      <Switch label={label} checked={checked} onChange={onChange} />
    </div>
  )
}

export function PanelSection({ title, children, actions }: { title: string; children: ReactNode; actions?: ReactNode }) {
  return (
    <section className="grid gap-3 border-b border-border pb-4 last:border-b-0" aria-label={title}>
      <div className="flex items-center gap-2">
        <h3 className="text-[13px] font-semibold uppercase tracking-wide text-fg3">{title}</h3>
        {actions ? <div className="ml-auto flex gap-1">{actions}</div> : null}
      </div>
      {children}
    </section>
  )
}
