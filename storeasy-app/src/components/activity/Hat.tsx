import { useId } from 'react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { LOGO_PATHS } from '@/components/layout/Logo'
import { useActivity } from '@/lib/activity/ActivityContext'
import type { JobView, Lane } from '@/lib/activity/hatState'
import { StepDirection, StoreKind } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import type { Dictionary } from '@/lib/i18n/en'
import type { Formatters } from '@/lib/i18n/format'
import { jobKindLabel } from '@/lib/jobs/labels'
import { cn } from '@/lib/utils/cn'

// The scene's geometry, from design 11.1 (viewBox 720 x 470): storeasy in the middle left, the three stores on the right,
// the CI source below left. Each lane is one path; packets ride it with SMIL animateMotion.
interface Point {
  x: number
  y: number
}

// `waiting` is the middle of the rail (t = 0.5), `stop` three quarters along it (t = 0.75).
const LANE_GEOMETRY: Record<
  StoreKind,
  { path: string; node: Point; label: Point & { anchor: 'end' | 'middle' }; waiting: Point; stop: Point }
> = {
  [StoreKind.Apple]: {
    path: 'M320 230 C 420 230, 420 84, 520 84',
    node: { x: 520, y: 36 },
    label: { x: 510, y: 50, anchor: 'end' },
    waiting: { x: 420, y: 157 },
    stop: { x: 461, y: 107 },
  },
  [StoreKind.Google]: {
    path: 'M320 230 L 520 230',
    node: { x: 520, y: 182 },
    label: { x: 420, y: 212, anchor: 'middle' },
    waiting: { x: 420, y: 230 },
    stop: { x: 461, y: 230 },
  },
  [StoreKind.Microsoft]: {
    path: 'M320 230 C 420 230, 420 376, 520 376',
    node: { x: 520, y: 328 },
    label: { x: 510, y: 414, anchor: 'end' },
    waiting: { x: 420, y: 303 },
    stop: { x: 461, y: 353 },
  },
}
const CI_PATH = 'M118 372 C 118 320, 250 340, 250 302'

// A step's progress: bytes for a transfer ("31,4 / 48,0 MB"), plain units otherwise ("4 / 5").
export function progressText(job: Pick<JobView, 'progress' | 'total' | 'stepName'>, format: Formatters): string | null {
  if (job.progress === null || !job.total) {
    return null
  }
  return /upload|download/.test(job.stepName ?? '')
    ? `${format.bytes(job.progress)} / ${format.bytes(job.total)}`
    : `${format.number(job.progress)} / ${format.number(job.total)}`
}

function packetLabel(job: JobView): string {
  const first = job.stepName ?? ''
  return first.length > 14 ? `${first.slice(0, 13)}…` : first
}

export function storeLabel(store: StoreKind, t: Dictionary): string {
  return store === StoreKind.Apple ? t.auth.artStoreApple : store === StoreKind.Google ? t.auth.artStoreGoogle : t.auth.artStoreMicrosoft
}

export function laneDetail(lane: Lane, t: Dictionary, format: Formatters): string {
  const job = lane.jobs[0]
  if (!job) {
    return lane.last ? t.activity.lastFinished : t.activity.idle
  }
  if (lane.failed) {
    return job.retrying && job.attempt && job.maxAttempts
      ? interpolate(t.activity.retrying, { attempt: job.attempt, max: job.maxAttempts })
      : t.activity.failedStep
  }
  return [job.stepName, progressText(job, format)].filter(Boolean).join(' · ') || t.activity.queued
}

/** "Pusula · Sync apps and builds": the app when the job has one, then what the job does. */
export function jobTitle(job: JobView, t: Dictionary, appName: string | null): string {
  return [appName, jobKindLabel(job.kind, t)].filter(Boolean).join(' · ')
}

function useJobTitle(): (job: JobView | null, fallback: string) => string {
  const t = useT()
  const { appName } = useActivity()
  return (job, fallback) => (job ? jobTitle(job, t, appName(job.appId)) : fallback)
}

function Packet({ lane, pathId, reverse }: { lane: Lane; pathId: string; reverse?: boolean }) {
  const job = lane.jobs[0]
  const label = packetLabel(job)
  const width = Math.max(44, label.length * 6.4 + 16)
  if (reverse) {
    return (
      <g>
        <circle r="9" className="ack" />
        <path d="m-4 0 3 3 5-6" className="ackm" />
        <animateMotion dur="2.6s" repeatCount="indefinite" keyPoints="1;0" keyTimes="0;1" calcMode="linear">
          <mpath href={`#${pathId}`} />
        </animateMotion>
      </g>
    )
  }
  return (
    <g>
      <rect x={-width / 2} y="-11" width={width} height="22" rx="6" className="pk" />
      <text y="3.5" textAnchor="middle" className="pk-t">
        {label}
      </text>
      <animateMotion dur="2.6s" repeatCount="indefinite">
        <mpath href={`#${pathId}`} />
      </animateMotion>
    </g>
  )
}

/**
 * The animated "Hat" (design 11.1): outbound steps travel as accent packets, answers come back as neutral rings, a
 * failing lane turns red and stops. The CI node stays idle until package intake has its events. Nothing moves when nothing is on the way. Under
 * reduced motion the page shows HatStatusList instead of this scene.
 */
export function HatScene({ lanes, frozen }: { lanes: Lane[]; frozen?: boolean }) {
  const t = useT()
  const format = useFormat()
  const titleOf = useJobTitle()
  const id = useId().replace(/:/g, '')
  const activeJobs = lanes.reduce((sum, lane) => sum + lane.jobs.length, 0)
  const animate = !frozen

  return (
    <svg className="hs hs-wide" viewBox="0 0 720 470" role="img" aria-label={t.activity.sceneLabel}>
      <defs>
        {lanes.map((lane) => (
          <path key={lane.store} id={`${id}-${lane.store}`} d={LANE_GEOMETRY[lane.store].path} />
        ))}
        <path id={`${id}-ci`} d={CI_PATH} />
      </defs>
      {lanes.map((lane) => (
        <g key={`rail-${lane.store}`}>
          <use href={`#${id}-${lane.store}`} className="rail-w" />
          <use href={`#${id}-${lane.store}`} className="rail" />
          {lane.failed ? <path d={LANE_GEOMETRY[lane.store].path} pathLength={100} className="rail-x" /> : null}
        </g>
      ))}
      <use href={`#${id}-ci`} className="rail-w" />
      <use href={`#${id}-ci`} className="rail" />

      {lanes.map((lane) => {
        const job = lane.jobs[0]
        const geometry = LANE_GEOMETRY[lane.store]
        if (!job) {
          return null
        }
        return (
          <g key={`move-${lane.store}`}>
            <text
              x={geometry.label.x}
              y={geometry.label.y}
              textAnchor={geometry.label.anchor}
              className={cn('rl', lane.failed && 'dng')}
              fontSize="10"
              fill="rgb(var(--fg-3))"
            >
              {[job.stepName, job.message].filter(Boolean).join(' · ')}
            </text>
            {lane.moving && animate ? (
              <Packet lane={lane} pathId={`${id}-${lane.store}`} reverse={job.direction === StepDirection.Inbound} />
            ) : null}
            {lane.failed ? (
              <>
                {/* The waiting packet stands still at the middle of the rail; the cross sits where it stopped. */}
                <g transform={`translate(${geometry.waiting.x} ${geometry.waiting.y})`}>
                  <rect x="-26" y="-11" width="52" height="22" rx="6" className="pk-w" />
                  <text y="3.5" textAnchor="middle" className="pk-wt">
                    {truncate(packetLabel(job), 8)}
                  </text>
                </g>
                <g transform={`translate(${geometry.stop.x} ${geometry.stop.y})`}>
                  <circle r="10" className="xm" />
                  <path d="M-4 -4 4 4M4 -4-4 4" className="xl" />
                </g>
              </>
            ) : null}
          </g>
        )
      })}

      <rect x="180" y="160" width="140" height="140" rx="36" className="hub" />
      <g transform="translate(214 180) scale(1.5)">
        {LOGO_PATHS.map((path, index) => (
          <path key={path.d} d={path.d} fill="rgb(var(--accent-fg))" opacity={index === 1 ? 0.55 : 1} />
        ))}
      </g>
      <text x="250" y="282" textAnchor="middle" className="hub-t">
        {activeJobs > 0 ? interpolate(t.activity.hubJobs, { count: activeJobs }) : t.activity.hubIdle}
      </text>

      <rect x="12" y="372" width="212" height="82" rx="20" className="node" />
      <text x="30" y="396" className="lbl">
        {t.activity.ciLabel}
      </text>
      <text x="30" y="420" className="ttl">
        {t.activity.ciIdle}
      </text>
      <text x="30" y="442" className="det">
        {t.activity.ciHint}
      </text>

      {lanes.map((lane) => {
        const { x, y } = LANE_GEOMETRY[lane.store].node
        const job = lane.jobs[0] ?? null
        const percent = job && job.progress !== null && job.total ? Math.min(1, job.progress / job.total) : null
        return (
          <g key={`node-${lane.store}`}>
            <rect x={x} y={y} width="190" height="96" rx="22" className={cn('node', lane.failed && 'x')} />
            <text x={x + 18} y={y + 24} className="lbl">
              {storeLabel(lane.store, t)}
            </text>
            <text x={x + 18} y={y + 50} className="ttl">
              {truncate(titleOf(job ?? lane.last, t.activity.nodeIdle), 18)}
            </text>
            <text x={x + 18} y={y + 74} className={cn('det', lane.failed && 'dng')}>
              {truncate(laneDetail(lane, t, format), 30)}
            </text>
            {percent !== null ? (
              <>
                <rect x={x + 18} y={y + 80} width="154" height="4" rx="2" className="bt" />
                <rect x={x + 18} y={y + 80} width={154 * percent} height="4" rx="2" className="bf" />
              </>
            ) : null}
          </g>
        )
      })}
    </svg>
  )
}

function truncate(text: string, length: number): string {
  return text.length > length ? `${text.slice(0, length - 1)}…` : text
}

/** The same lanes as text: one row per lane (design 11.2), what the reduced motion view shows instead of the scene. */
export function HatStatusList({ lanes }: { lanes: Lane[] }) {
  const t = useT()
  const format = useFormat()
  const titleOf = useJobTitle()
  return (
    <ul className="m-0 list-none p-0" aria-label={t.activity.statusListLabel}>
      {lanes.map((lane) => {
        const job = lane.jobs[0] ?? null
        return (
          <li key={lane.store} className="rml">
            <span className="ei">
              <StoreTag store={lane.store} small codeOnly />
            </span>
            <div>
              <div className="rt">{interpolate(t.activity.routeOut, { store: storeLabel(lane.store, t) })}</div>
              <b className="text-sm">{titleOf(job ?? lane.last, t.activity.nodeIdle)}</b>
              <div className={cn('s', lane.failed && 'text-danger')}>{laneDetail(lane, t, format)}</div>
            </div>
            {job ? (
              lane.failed ? (
                <Badge tone="danger" small>
                  {t.activity.statusRetrying}
                </Badge>
              ) : (
                <Badge tone="accent" small dot>
                  {t.activity.statusRunning}
                </Badge>
              )
            ) : (
              <Badge small>{t.activity.statusIdle}</Badge>
            )}
          </li>
        )
      })}
    </ul>
  )
}

const PHONE_GEOMETRY: Record<StoreKind, { path: string; node: Point; width: number }> = {
  [StoreKind.Apple]: { path: 'M180 82 C 180 130, 55 128, 55 180', node: { x: 0, y: 180 }, width: 110 },
  [StoreKind.Google]: { path: 'M180 82 L 180 180', node: { x: 125, y: 180 }, width: 110 },
  [StoreKind.Microsoft]: { path: 'M180 82 C 180 130, 305 128, 305 180', node: { x: 250, y: 180 }, width: 108 },
}

/** The phone form of the scene (design 11.1): storeasy on top, the stores below, packets as unlabeled dots. */
export function HatScenePhone({ lanes, frozen }: { lanes: Lane[]; frozen?: boolean }) {
  const t = useT()
  const format = useFormat()
  const titleOf = useJobTitle()
  const id = useId().replace(/:/g, '')
  const activeJobs = lanes.reduce((sum, lane) => sum + lane.jobs.length, 0)
  return (
    <svg className="hs" viewBox="0 0 358 250" role="img" aria-label={t.activity.sceneLabel}>
      <defs>
        {lanes.map((lane) => (
          <path key={lane.store} id={`${id}-p${lane.store}`} d={PHONE_GEOMETRY[lane.store].path} />
        ))}
        <path id={`${id}-pci`} d="M100 46 L 144 46" />
      </defs>
      {lanes.map((lane) => (
        <g key={`rail-${lane.store}`}>
          <use href={`#${id}-p${lane.store}`} className="rail-w" />
          <use href={`#${id}-p${lane.store}`} className="rail" />
          {lane.failed ? <path d={PHONE_GEOMETRY[lane.store].path} pathLength={100} className="rail-x" /> : null}
        </g>
      ))}
      <use href={`#${id}-pci`} className="rail-w" />
      <use href={`#${id}-pci`} className="rail" />
      {lanes.map((lane) =>
        lane.moving && !frozen ? (
          <g key={`dot-${lane.store}`}>
            <circle r="6" className={lane.jobs[0]?.direction === StepDirection.Inbound ? 'ack' : 'pk'} />
            <animateMotion
              dur="2.6s"
              repeatCount="indefinite"
              {...(lane.jobs[0]?.direction === StepDirection.Inbound ? { keyPoints: '1;0', keyTimes: '0;1', calcMode: 'linear' } : {})}
            >
              <mpath href={`#${id}-p${lane.store}`} />
            </animateMotion>
          </g>
        ) : null,
      )}
      <rect x="144" y="10" width="72" height="72" rx="20" className="hub" />
      <g transform="translate(162 16) scale(0.75)">
        {LOGO_PATHS.map((path, index) => (
          <path key={path.d} d={path.d} fill="rgb(var(--accent-fg))" opacity={index === 1 ? 0.55 : 1} />
        ))}
      </g>
      <text x="180" y="72" textAnchor="middle" className="hub-t" style={{ fontSize: 8.5 }}>
        {activeJobs > 0 ? interpolate(t.activity.hubJobs, { count: activeJobs }) : t.activity.hubIdle}
      </text>
      <rect x="0" y="24" width="100" height="44" rx="12" className="node" />
      <text x="10" y="40" className="lbl" style={{ fontSize: 8 }}>
        {t.activity.ciShort}
      </text>
      <text x="10" y="58" className="ttl s">
        {truncate(t.activity.ciIdle, 11)}
      </text>
      {lanes.map((lane) => {
        const { node, width } = PHONE_GEOMETRY[lane.store]
        const job = lane.jobs[0] ?? null
        return (
          <g key={`node-${lane.store}`}>
            <rect x={node.x} y={node.y} width={width} height="64" rx="16" className={cn('node', lane.failed && 'x')} />
            <text x={node.x + 12} y={node.y + 18} className="lbl" style={{ fontSize: 8.5 }}>
              {truncate(storeLabel(lane.store, t), 14)}
            </text>
            <text x={node.x + 12} y={node.y + 37} className="ttl s">
              {truncate(titleOf(job ?? lane.last, t.activity.nodeIdle), 12)}
            </text>
            <text x={node.x + 12} y={node.y + 54} className={cn('det s', lane.failed && 'dng')}>
              {truncate(laneDetail(lane, t, format), 17)}
            </text>
          </g>
        )
      })}
    </svg>
  )
}
