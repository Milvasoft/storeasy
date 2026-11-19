import { useState, type KeyboardEvent } from 'react'
import { ChevronLeft, ChevronRight, GripVertical } from 'lucide-react'
import { IconButton } from '@/components/ui/primitives'
import type { AppListDto } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'

// Where the focus goes back after a move (the card moves in the page, so the browser may drop it).
export type OrderFocus = 'handle' | 'earlier' | 'later'

/**
 * The handle on an app card that moves it in the apps list without dragging: Alt with an arrow key moves it one place
 * (left and up earlier, right and down later), and a click opens "earlier" and "later" buttons for touch and pointer
 * users. Dragging the card itself is handled by the list.
 */
export function AppOrderHandle({
  app,
  first,
  last,
  onMove,
}: {
  app: AppListDto
  first: boolean
  last: boolean
  onMove: (offset: -1 | 1, focus: OrderFocus) => void
}) {
  const t = useT()
  const [open, setOpen] = useState(false)

  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (!event.altKey) {
      return
    }
    const offset = event.key === 'ArrowLeft' || event.key === 'ArrowUp' ? -1 : event.key === 'ArrowRight' || event.key === 'ArrowDown' ? 1 : 0
    if (offset === 0) {
      return
    }
    event.preventDefault()
    if ((offset === -1 && !first) || (offset === 1 && !last)) {
      onMove(offset, 'handle')
    }
  }

  return (
    <span className="flex items-center gap-1">
      {open ? (
        <>
          <IconButton
            label={interpolate(t.apps.moveEarlier, { app: app.Name })}
            size="sm"
            ghost
            disabled={first}
            data-reorder="earlier"
            onClick={() => onMove(-1, 'earlier')}
          >
            <ChevronLeft aria-hidden />
          </IconButton>
          <IconButton
            label={interpolate(t.apps.moveLater, { app: app.Name })}
            size="sm"
            ghost
            disabled={last}
            data-reorder="later"
            onClick={() => onMove(1, 'later')}
          >
            <ChevronRight aria-hidden />
          </IconButton>
        </>
      ) : null}
      <IconButton
        label={interpolate(t.apps.reorderHandle, { app: app.Name })}
        size="sm"
        ghost
        className="cursor-grab"
        aria-expanded={open}
        aria-keyshortcuts="Alt+ArrowLeft Alt+ArrowRight Alt+ArrowUp Alt+ArrowDown"
        data-reorder="handle"
        onClick={() => setOpen((current) => !current)}
        onKeyDown={onKeyDown}
      >
        <GripVertical aria-hidden />
      </IconButton>
    </span>
  )
}
