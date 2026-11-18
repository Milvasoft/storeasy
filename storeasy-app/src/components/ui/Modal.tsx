import { useEffect, useId, useRef, type ReactNode, type RefObject } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'
import { useT } from '@/lib/i18n/I18nContext'
import { cn } from '@/lib/utils/cn'
import { IconButton } from './primitives'

// Open modals, oldest first. Escape and the focus trap act only on the top one, so a confirm raised over a drawer does
// not also close the drawer beneath it.
const openStack: object[] = []

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

export interface ModalProps {
  open: boolean
  onClose: () => void
  title?: ReactNode
  description?: ReactNode
  children?: ReactNode
  footer?: ReactNode
  // 'center': a dialog (a bottom sheet on phones); 'drawer': a right-side panel for details (full screen on phones).
  variant?: 'center' | 'drawer'
  // 420 confirm · 520 form · 720 mapping · 960 diff and plan (design 19: modal sizes).
  size?: 'sm' | 'md' | 'lg' | 'xl'
  // false for gates the user must answer: no backdrop click, no Escape, no close button.
  dismissible?: boolean
  // The element to focus first (a confirm opens on "Cancel", design 18.18); default: the first focusable one.
  initialFocus?: RefObject<HTMLElement | null>
  // The dialog's role: an alertdialog for confirmations.
  role?: 'dialog' | 'alertdialog'
  // A drawer from the left (the phone menu, design 19.6) instead of the right.
  side?: 'left' | 'right'
  // The children are the whole panel (no header, body, footer): the phone menu draws its own.
  bare?: boolean
  // Names a bare panel for screen readers.
  label?: string
  panelClassName?: string
}

export function Modal({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  variant = 'center',
  size = 'md',
  dismissible = true,
  initialFocus,
  role = 'dialog',
  side = 'right',
  bare,
  label,
  panelClassName,
}: ModalProps) {
  const t = useT()
  const titleId = useId()
  const descriptionId = useId()
  const panelRef = useRef<HTMLDivElement>(null)
  const stackId = useRef({})
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  useEffect(() => {
    if (!open) {
      return
    }
    const id = stackId.current
    const previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const previousOverflow = document.body.style.overflow
    openStack.push(id)
    document.body.style.overflow = 'hidden'
    // The first control of the body (or the footer), not the close button in the header.
    const inContent = ['.mdl-b', '.mdl-f'].flatMap((area) => FOCUSABLE.split(', ').map((selector) => `${area} ${selector}`)).join(', ')
    const target = initialFocus?.current ?? panelRef.current?.querySelector<HTMLElement>(inContent) ?? panelRef.current
    target?.focus()

    const onKeyDown = (event: KeyboardEvent) => {
      if (openStack[openStack.length - 1] !== id) {
        return
      }
      if (event.key === 'Escape' && dismissible) {
        event.stopPropagation()
        onCloseRef.current()
        return
      }
      if (event.key !== 'Tab' || !panelRef.current) {
        return
      }
      // Keep Tab inside the dialog: wrap from the last element to the first and back.
      const focusables = Array.from(panelRef.current.querySelectorAll<HTMLElement>(FOCUSABLE))
      if (focusables.length === 0) {
        event.preventDefault()
        return
      }
      const first = focusables[0]
      const last = focusables[focusables.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => {
      window.removeEventListener('keydown', onKeyDown)
      const at = openStack.lastIndexOf(id)
      if (at >= 0) {
        openStack.splice(at, 1)
      }
      document.body.style.overflow = previousOverflow
      previouslyFocused?.focus()
    }
    // initialFocus is read once on open.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, dismissible])

  if (!open) {
    return null
  }

  const drawer = variant === 'drawer'
  const left = drawer && side === 'left'
  return createPortal(
    <div
      className={cn('ovl animate-fade-in', drawer && '!items-stretch !p-0', drawer && (left ? '!justify-start' : '!justify-end'))}
      onClick={dismissible ? onClose : undefined}
    >
      <div
        ref={panelRef}
        role={role}
        aria-modal="true"
        aria-label={bare ? label : undefined}
        aria-labelledby={!bare && title ? titleId : undefined}
        aria-describedby={!bare && description ? descriptionId : undefined}
        tabIndex={-1}
        className={cn(
          drawer
            ? left
              ? 'drw !left-0 !right-auto !border-l-0 animate-slide-in-left'
              : 'drw animate-slide-in-right'
            : 'mdl animate-slide-up sm:animate-scale-in',
          !drawer && size !== 'md' && size,
          panelClassName,
        )}
        onClick={(event) => event.stopPropagation()}
      >
        {bare ? children : null}
        {!bare && (title || dismissible) ? (
          <div className={cn('mdl-h', drawer && 'border-b border-border pb-4')}>
            <div className="min-w-0 flex-1">
              {title ? (
                <h2 id={titleId} className="h2">
                  {title}
                </h2>
              ) : null}
              {description ? (
                <p id={descriptionId} className="mt-1 text-[13.5px] text-fg2">
                  {description}
                </p>
              ) : null}
            </div>
            {dismissible ? (
              <IconButton label={t.common.close} size="sm" ghost onClick={onClose}>
                <X aria-hidden />
              </IconButton>
            ) : null}
          </div>
        ) : null}
        {!bare ? <div className="mdl-b">{children}</div> : null}
        {!bare && footer ? <div className="mdl-f">{footer}</div> : null}
      </div>
    </div>,
    document.body,
  )
}
