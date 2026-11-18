import { useCallback, useEffect, useState } from 'react'
import { autoUpdate, flip, offset, shift, size, useFloating, type Placement } from '@floating-ui/react-dom'

/**
 * Positioning for every panel that opens over the page (select, menu, date picker): drawn in a portal, flips up when
 * there is no room below, shifts in from the edges, never taller than the space left, follows scroll and resize
 * (02 section 9.4 "Açılır paneller"). Outside click and Escape close it; focus returns to the trigger.
 */
export function usePopover({ placement = 'bottom-start', matchWidth = false }: { placement?: Placement; matchWidth?: boolean } = {}) {
  const [open, setOpen] = useState(false)
  const floating = useFloating({
    open,
    placement,
    strategy: 'fixed',
    whileElementsMounted: autoUpdate,
    middleware: [
      offset(6),
      flip({ padding: 8 }),
      shift({ padding: 8 }),
      size({
        padding: 8,
        apply({ availableHeight, rects, elements }) {
          Object.assign(elements.floating.style, {
            maxHeight: `${Math.max(120, Math.min(availableHeight, 360))}px`,
            ...(matchWidth ? { width: `${rects.reference.width}px` } : {}),
          })
        },
      }),
    ],
  })

  const close = useCallback(
    (returnFocus = true) => {
      setOpen(false)
      if (returnFocus) {
        const reference = floating.refs.reference.current
        if (reference instanceof HTMLElement) {
          reference.focus()
        }
      }
    },
    [floating.refs.reference],
  )

  useEffect(() => {
    if (!open) {
      return
    }
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node
      const reference = floating.refs.reference.current
      const panel = floating.refs.floating.current
      if (panel?.contains(target) || (reference instanceof Element && reference.contains(target))) {
        return
      }
      close(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.stopPropagation()
        close()
      }
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open, close, floating.refs.reference, floating.refs.floating])

  return { open, setOpen, close, ...floating }
}
