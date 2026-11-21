import {
  cloneElement,
  forwardRef,
  useId,
  useState,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type ReactElement,
  type ReactNode,
  type TextareaHTMLAttributes,
} from 'react'
import { Eye, EyeOff } from 'lucide-react'
import { useT } from '@/lib/i18n/I18nContext'
import { cn } from '@/lib/utils/cn'

// ---- Buttons -------------------------------------------------------------------------------------------------------
// Kit .btn (design 19 section 5): outline is the default secondary; primary at most once per screen; inverse for strong
// secondary and warning actions; danger only inside a confirm dialog; danger-outline for a danger zone trigger.
export type ButtonVariant = 'primary' | 'outline' | 'inverse' | 'ghost' | 'danger' | 'danger-outline'

const VARIANT_CLASS: Record<ButtonVariant, string> = {
  primary: 'pri',
  outline: '',
  inverse: 'inv',
  ghost: 'ghost',
  danger: 'dng',
  'danger-outline': 'dng-o',
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: 'sm' | 'lg'
  block?: boolean
  // Disables itself and shows the spinner in place of the label (the label stays for screen readers).
  loading?: boolean
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'outline', size, block, loading, className, children, disabled, type = 'button', ...rest },
  ref,
) {
  return (
    <button
      ref={ref}
      type={type}
      className={cn('btn', VARIANT_CLASS[variant], size, block && 'blk', loading && 'ld', className)}
      disabled={loading || disabled}
      aria-busy={loading || undefined}
      {...rest}
    >
      {children}
    </button>
  )
})

// An icon-only button: the label is required (aria-label and tooltip), the touch target is widened to 44 px in CSS.
export const IconButton = forwardRef<
  HTMLButtonElement,
  ButtonHTMLAttributes<HTMLButtonElement> & { label: string; size?: 'sm'; ghost?: boolean }
>(function IconButton({ label, size, ghost, className, children, type = 'button', ...rest }, ref) {
  return (
    <button ref={ref} type={type} aria-label={label} title={label} className={cn('ib', size, ghost && 'ghost', className)} {...rest}>
      {children}
    </button>
  )
})

// ---- Inputs --------------------------------------------------------------------------------------------------------
export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & { mono?: boolean }>(function Input(
  { className, mono, ...rest },
  ref,
) {
  return <input ref={ref} className={cn('inp', mono && 'mono', className)} {...rest} />
})

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement> & { mono?: boolean }>(function Textarea(
  { className, mono, ...rest },
  ref,
) {
  return <textarea ref={ref} className={cn('inp', mono && 'mono', className)} {...rest} />
})

// A password input with a show/hide toggle; the toggle's label comes from the dictionary.
export const PasswordInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(function PasswordInput(
  { className, ...rest },
  ref,
) {
  const t = useT()
  const [visible, setVisible] = useState(false)
  return (
    <span className={cn('inp-wrap', className)}>
      <input ref={ref} type={visible ? 'text' : 'password'} {...rest} />
      <IconButton
        label={visible ? t.common.hidePassword : t.common.showPassword}
        size="sm"
        ghost
        onClick={() => setVisible((value) => !value)}
      >
        {visible ? <EyeOff aria-hidden /> : <Eye aria-hidden />}
      </IconButton>
    </span>
  )
})

interface FieldProps {
  label: string
  hint?: ReactNode
  error?: string | null
  optional?: boolean
  // A character counter in the label row: warning from 90 % of the limit, error above it.
  counter?: { value: number; max: number }
  children: ReactElement
}

// Wraps one input with its label, hint and error. The label element makes getByLabelText work; the error is tied to the
// input through aria-describedby so a screen reader reads it.
export function Field({ label, hint, error, optional, counter, children }: FieldProps) {
  const t = useT()
  const inputId = useId()
  const messageId = useId()
  const childProps = children.props as { id?: string; className?: string }
  const id = childProps.id ?? inputId
  const input = cloneElement(children as ReactElement<Record<string, unknown>>, {
    id,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': error || hint ? messageId : undefined,
    className: cn(childProps.className, error && 'err'),
  })
  const counterClass = counter ? (counter.value > counter.max ? 'over' : counter.value >= counter.max * 0.9 ? 'near' : '') : ''
  return (
    <div className="fld">
      <span className="lbl">
        <label htmlFor={id}>{label}</label>
        {optional ? <span className="opt">{t.common.optional}</span> : null}
        {counter ? (
          <span className={cn('cnt', counterClass)}>
            {counter.value} / {counter.max}
          </span>
        ) : null}
      </span>
      {input}
      {error ? (
        <span id={messageId} className="err-t">
          {error}
        </span>
      ) : hint ? (
        <span id={messageId} className="hint">
          {hint}
        </span>
      ) : null}
    </div>
  )
}

// A real checkbox (sr-only, peer) with the kit's visual box; the label is always text.
export function Checkbox({
  checked,
  indeterminate,
  onChange,
  disabled,
  children,
  className,
}: {
  checked: boolean
  indeterminate?: boolean
  onChange: (checked: boolean) => void
  disabled?: boolean
  children: ReactNode
  className?: string
}) {
  return (
    <label
      className={cn('inline-flex cursor-pointer items-center gap-2 text-[13px] text-fg2', disabled && 'cursor-not-allowed', className)}
    >
      <input
        type="checkbox"
        className="peer sr-only"
        checked={checked}
        disabled={disabled}
        aria-checked={indeterminate ? 'mixed' : checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <span className={cn('chk', indeterminate ? 'mix' : checked && 'on')} aria-hidden />
      <span>{children}</span>
    </label>
  )
}

export interface RadioOption<T extends string | number> {
  value: T
  label: string
  description?: string
  disabled?: boolean
}

export function RadioGroup<T extends string | number>({
  name,
  value,
  options,
  onChange,
  label,
}: {
  name: string
  value: T
  options: RadioOption<T>[]
  onChange: (value: T) => void
  label: string
}) {
  return (
    <div role="radiogroup" aria-label={label} className="grid gap-1">
      {options.map((option) => (
        <label
          key={String(option.value)}
          className={cn('flex cursor-pointer items-start gap-2.5 py-1.5', option.disabled && 'cursor-not-allowed opacity-60')}
        >
          <input
            type="radio"
            className="peer sr-only"
            name={name}
            checked={value === option.value}
            disabled={option.disabled}
            onChange={() => onChange(option.value)}
          />
          <span className={cn('rad mt-0.5', value === option.value && 'on')} aria-hidden />
          <span className="grid gap-0.5">
            <span className="text-[13.5px] font-medium text-fg">{option.label}</span>
            {option.description ? <span className="text-xs text-fg3">{option.description}</span> : null}
          </span>
        </label>
      ))}
    </div>
  )
}

// role="switch" + aria-checked; the visible label sits next to it and names it.
export function Switch({
  checked,
  onChange,
  label,
  disabled,
}: {
  checked: boolean
  onChange: (checked: boolean) => void
  label: string
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      className={cn('sw', checked && 'on')}
      onClick={() => onChange(!checked)}
    />
  )
}

export function Chip({
  selected,
  onClick,
  children,
  icon,
}: {
  selected?: boolean
  onClick?: () => void
  children: ReactNode
  icon?: ReactNode
}) {
  return (
    <button type="button" className={cn('chip', selected && 'on')} aria-pressed={selected} onClick={onClick}>
      {icon}
      {children}
    </button>
  )
}

// ---- Avatar and app icon -----------------------------------------------------------------------------------------
export function Avatar({ initials, size, label }: { initials: string; size?: 's' | 'l' | 'xl'; label?: string }) {
  return (
    <span className={cn('av', size)} role={label ? 'img' : undefined} aria-label={label} aria-hidden={label ? undefined : true}>
      {initials}
    </span>
  )
}

// The store's icon when it has one; else the app's color and initial (raw hex only here, design 19 section 2).
export function AppIcon({ name, color, size }: { name: string; color: string; size?: 's' | 'm' | 'l' }) {
  return (
    <span className={cn('aic', size)} style={{ background: color }} aria-hidden>
      {name.slice(0, 1).toLocaleUpperCase('tr')}
    </span>
  )
}

// ---- Loading ------------------------------------------------------------------------------------------------------
export function Spinner({ size = 20 }: { size?: number }) {
  return <span className="spinner" style={{ width: size, height: size }} aria-hidden />
}

export function SpinnerCenter({ label }: { label: string }) {
  return (
    <div className="flex justify-center py-10" role="status">
      <Spinner />
      <span className="sr-only">{label}</span>
    </div>
  )
}

export function Skeleton({ className, shape }: { className?: string; shape?: 't' | 'b' | 'ci' }) {
  return <span className={cn('sk', shape, className)} aria-hidden />
}

// A table-shaped skeleton (design 18.1): header and column names are real, only the data places are grey.
export function TableSkeleton({ columns, rows = 8, label }: { columns: string[]; rows?: number; label: string }) {
  return (
    <div className="tbl-w cards" aria-busy="true">
      <span className="sr-only" role="status">
        {label}
      </span>
      <table className="tbl">
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column}>{column}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {Array.from({ length: rows }, (_, row) => (
            <tr key={row}>
              {columns.map((column, index) => (
                <td key={column} className={index === 0 ? 'tm' : undefined}>
                  <Skeleton className={index === 0 ? 'w-40' : 'w-24'} />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function CardSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3" aria-hidden>
      {Array.from({ length: count }, (_, index) => (
        <div key={index} className="card grid gap-3 p-[18px]">
          <div className="flex items-center gap-3">
            <Skeleton shape="b" className="w-9" />
            <Skeleton shape="t" className="w-40" />
          </div>
          <Skeleton className="w-3/4" />
          <Skeleton className="w-1/2" />
        </div>
      ))}
    </div>
  )
}

export function PageSkeleton() {
  return (
    <div className="grid gap-5" aria-busy="true">
      <div className="grid gap-2">
        <Skeleton shape="t" className="h-8 w-56" />
        <Skeleton className="w-80 max-w-full" />
      </div>
      <CardSkeleton />
    </div>
  )
}

// ---- Empty state ---------------------------------------------------------------------------------------------------
// The kit's dashed box: icon, title, description, at most one action (design 19 section 5).
export function EmptyState({
  icon,
  title,
  description,
  action,
  children,
}: {
  icon?: ReactNode
  title: string
  description?: ReactNode
  action?: ReactNode
  children?: ReactNode
}) {
  return (
    <div className="empty">
      {icon ? <div className="eic">{icon}</div> : null}
      <h2>{title}</h2>
      {description ? <p>{description}</p> : null}
      {action ? <div className="mt-1.5 flex flex-wrap justify-center gap-2">{action}</div> : null}
      {children}
    </div>
  )
}

// The page heading row: title, subtitle and the screen's actions.
export function PageHeader({
  title,
  subtitle,
  actions,
  badge,
  tour,
}: {
  title: string
  subtitle?: ReactNode
  actions?: ReactNode
  badge?: ReactNode
  // The guided tour's anchor (data-tour) for this page.
  tour?: string
}) {
  return (
    <div className="pg-h" data-tour={tour}>
      <div>
        <div className="flex flex-wrap items-center gap-2.5">
          <h1 className="pg-t">{title}</h1>
          {badge}
        </div>
        {subtitle ? <p className="pg-s">{subtitle}</p> : null}
      </div>
      {actions ? <div className="pg-a">{actions}</div> : null}
    </div>
  )
}
