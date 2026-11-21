import { useId, useMemo, useState } from 'react'
import type { DateOnly } from '@/lib/api/types'
import { ChartMarkers, type ChartMarker } from '@/features/aso-impact/ChartMarkers'

// A small dependency free line chart of one metric per day: a line over the days with data (a gap where a day has no
// value), three horizontal guides, the first, middle and last day on the axis and a hover read out. The numbers are also
// in a visually hidden table for screen readers. Optional change markers (vertical dashed lines) open the change's impact.

export interface ChartPoint {
  date: DateOnly
  value: number | null
}

interface MetricChartProps {
  points: ChartPoint[]
  label: string
  formatValue: (value: number) => string
  formatDate: (date: DateOnly) => string
  height?: number
  // Changes to mark on the chart (store page changes, releases).
  markers?: ChartMarker[]
  onMarker?: (id: string) => void
}

const WIDTH = 720
const PAD = { top: 12, right: 12, bottom: 28, left: 56 }

/** Splits the points into runs without gaps, as SVG path data. */
export function linePaths(points: Array<{ x: number; y: number | null }>): string[] {
  const paths: string[] = []
  let current = ''
  for (const point of points) {
    if (point.y == null) {
      if (current) {
        paths.push(current)
      }
      current = ''
      continue
    }
    current += `${current ? 'L' : 'M'}${point.x.toFixed(1)},${point.y.toFixed(1)}`
  }
  if (current) {
    paths.push(current)
  }
  return paths
}

/** A rounded top of the value axis: 1, 2, 2.5 or 5 times a power of ten, at least the largest value. */
export function niceMax(value: number): number {
  if (value <= 0) {
    return 1
  }
  const power = 10 ** Math.floor(Math.log10(value))
  const step = [1, 2, 2.5, 5, 10].find((factor) => factor * power >= value) ?? 10
  return step * power
}

export function MetricChart({ points, label, formatValue, formatDate, height = 220, markers = [], onMarker }: MetricChartProps) {
  const titleId = useId()
  const [hover, setHover] = useState<number | null>(null)

  const layout = useMemo(() => {
    const values = points.map((point) => point.value).filter((value): value is number => value != null)
    const top = niceMax(Math.max(0, ...values))
    const innerWidth = WIDTH - PAD.left - PAD.right
    const innerHeight = height - PAD.top - PAD.bottom
    const step = points.length > 1 ? innerWidth / (points.length - 1) : 0
    const x = (index: number) => PAD.left + (points.length > 1 ? index * step : innerWidth / 2)
    const y = (value: number) => PAD.top + innerHeight - (value / top) * innerHeight
    const placed = points.map((point, index) => ({ x: x(index), y: point.value == null ? null : y(point.value) }))
    return { top, innerWidth, innerHeight, x, y, placed, paths: linePaths(placed) }
  }, [points, height])

  const guides = [0, 0.5, 1].map((share) => layout.top * share)
  const ticks = points.length > 2 ? [0, Math.floor((points.length - 1) / 2), points.length - 1] : points.map((_, index) => index)
  const hovered = hover != null ? points[hover] : null

  return (
    <figure className="m-0">
      <svg
        viewBox={`0 0 ${WIDTH} ${height}`}
        className="h-auto w-full"
        role="img"
        aria-labelledby={titleId}
        onMouseLeave={() => setHover(null)}
        onMouseMove={(event) => {
          if (points.length === 0) {
            return
          }
          const box = event.currentTarget.getBoundingClientRect()
          const ratio = box.width > 0 ? (event.clientX - box.left) / box.width : 0
          const position = ratio * WIDTH
          const index = points.length > 1 ? Math.round((position - PAD.left) / (layout.innerWidth / (points.length - 1))) : 0
          setHover(Math.min(points.length - 1, Math.max(0, index)))
        }}
      >
        <title id={titleId}>{label}</title>
        {guides.map((value) => (
          <g key={value}>
            <line
              x1={PAD.left}
              x2={WIDTH - PAD.right}
              y1={layout.y(value)}
              y2={layout.y(value)}
              className="stroke-border"
              strokeDasharray={value === 0 ? undefined : '3 4'}
            />
            <text x={PAD.left - 8} y={layout.y(value) + 4} textAnchor="end" className="fill-fg3 text-[11px]">
              {formatValue(value)}
            </text>
          </g>
        ))}
        {ticks.map((index) => (
          <text
            key={index}
            x={layout.x(index)}
            y={height - 8}
            textAnchor={index === 0 ? 'start' : index === points.length - 1 ? 'end' : 'middle'}
            className="fill-fg3 text-[11px]"
          >
            {formatDate(points[index].date)}
          </text>
        ))}
        <ChartMarkers
          markers={markers}
          dates={points.map((point) => point.date)}
          x={layout.x}
          top={PAD.top}
          bottom={PAD.top + layout.innerHeight}
          onOpen={onMarker}
        />
        {layout.paths.map((path) => (
          <path key={path} d={path} fill="none" className="stroke-accent" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
        ))}
        {layout.placed.map((point, index) =>
          point.y != null && (points.length <= 31 || index === hover) ? (
            <circle key={index} cx={point.x} cy={point.y} r={index === hover ? 4 : 2.5} className="fill-accent" />
          ) : null,
        )}
        {hover != null ? (
          <line
            x1={layout.x(hover)}
            x2={layout.x(hover)}
            y1={PAD.top}
            y2={PAD.top + layout.innerHeight}
            className="stroke-fg3"
            strokeDasharray="2 3"
          />
        ) : null}
      </svg>
      <figcaption className="min-h-[20px] text-xs text-fg2" aria-live="polite">
        {hovered ? `${formatDate(hovered.date)}: ${hovered.value == null ? '-' : formatValue(hovered.value)}` : null}
      </figcaption>
      <table className="sr-only">
        <caption>{label}</caption>
        <tbody>
          {points.map((point) => (
            <tr key={point.date}>
              <th scope="row">{formatDate(point.date)}</th>
              <td>{point.value == null ? '-' : formatValue(point.value)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}
