import type { KeyboardEvent } from 'react'
import type { DateOnly } from '@/lib/api/types'
import { markerIndex } from './labels'

// Change markers on a chart: a dashed vertical line per change with a small flag on top. Each marker is a button (click
// or Enter opens the change's impact) with its date and title as the tooltip and the accessible name.

export interface ChartMarker {
  id: string
  date: DateOnly
  // Tooltip and accessible name ("12 Eki 2026: Store texts (tr)").
  label: string
  // Muted for changes that are not live yet.
  pending?: boolean
}

interface ChartMarkersProps {
  markers: ChartMarker[]
  // The days of the chart's points, in order.
  dates: DateOnly[]
  // The x of a (fractional) point index.
  x: (index: number) => number
  top: number
  bottom: number
  onOpen?: (id: string) => void
}

export function ChartMarkers({ markers, dates, x, top, bottom, onOpen }: ChartMarkersProps) {
  const placed = markers
    .map((marker) => ({ marker, index: markerIndex(marker.date, dates) }))
    .filter((item): item is { marker: ChartMarker; index: number } => item.index != null)

  if (placed.length === 0) {
    return null
  }

  const open = (id: string) => onOpen?.(id)
  const onKey = (event: KeyboardEvent<SVGGElement>, id: string) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      open(id)
    }
  }

  return (
    <g data-testid="chart-markers">
      {placed.map(({ marker, index }) => {
        const position = x(index)
        return (
          <g
            key={marker.id}
            role={onOpen ? 'button' : undefined}
            tabIndex={onOpen ? 0 : undefined}
            aria-label={marker.label}
            data-marker={marker.id}
            className={onOpen ? 'cursor-pointer focus:outline-none' : undefined}
            onClick={onOpen ? () => open(marker.id) : undefined}
            onKeyDown={onOpen ? (event) => onKey(event, marker.id) : undefined}
          >
            <title>{marker.label}</title>
            {/* A wide transparent strip so the thin line is easy to hit. */}
            <rect x={position - 6} y={top} width={12} height={bottom - top} fill="transparent" />
            <line
              x1={position}
              x2={position}
              y1={top}
              y2={bottom}
              stroke="currentColor"
              strokeWidth={1.5}
              strokeDasharray="4 3"
              className={marker.pending ? 'text-fg3' : 'text-warningText'}
            />
            <path d={`M${position},${top} l7,3 l-7,3 z`} fill="currentColor" className={marker.pending ? 'text-fg3' : 'text-warningText'} />
          </g>
        )
      })}
    </g>
  )
}
