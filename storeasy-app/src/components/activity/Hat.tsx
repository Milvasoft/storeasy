import { useEffect, useRef, useState, type RefObject } from 'react'
import { Link } from 'react-router-dom'
import { AppAvatar } from '@/components/apps/AppAvatar'
import { LogoMark } from '@/components/layout/Logo'
import { StoreTag, storeName } from '@/components/ui/Badge'
import { useActivity } from '@/lib/activity/ActivityContext'
import { gateState, type GateItem, type GateState, type HourBar, type UpcomingItem } from '@/lib/activity/board'
import {
  activeIntakes,
  isActive,
  lastIntake,
  type HatState,
  type JobView,
  type Lane,
  type PulseGroup,
  type Token,
  type Track,
} from '@/lib/activity/hatState'
import { StoreJobStepStatus, type StoreKind } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import type { Dictionary } from '@/lib/i18n/en'
import type { Formatters } from '@/lib/i18n/format'
import { jobKindLabel, jobStatusView } from '@/lib/jobs/labels'
import { cn } from '@/lib/utils/cn'

// The live line (design 11, second version): storeasy on the left with what flows into it (package uploads, store
// events), one line per store running to the store's gate on the right. Every job on its way is a token at the place
// its real step puts it; store events ride back along the line as pulses; under each line, its last 24 hours. The gate
// says what is with the store now. All of it is HTML laid out by a grid (nothing is placed over anything else) and moved
// with transforms only. `calm` (reduced motion) keeps every element and only takes the motion away.

// A step's progress: bytes for a transfer ("31,4 / 48,0 MB"), plain units otherwise ("4 / 5").
export function progressText(job: Pick<JobView, 'progress' | 'total' | 'stepName'>, format: Formatters): string | null {
  if (job.progress === null || !job.total) {
    return null
  }
  return /upload|download/.test(job.stepName ?? '')
    ? `${format.bytes(job.progress)} / ${format.bytes(job.total)}`
    : `${format.number(job.progress)} / ${format.number(job.total)}`
}

export function storeLabel(store: StoreKind, t: Dictionary): string {
  return storeName(store, t.enums.store)
}

/** "Pusula · Sync apps and builds": the app when the job has one, then what the job does. */
export function jobTitle(job: Pick<JobView, 'kind'>, t: Dictionary, appName: string | null): string {
  return [appName, jobKindLabel(job.kind, t)].filter(Boolean).join(' · ')
}

/** "35 dk", "3 sa 10 dk", "2 g 4 sa": how long something has been going on, two units at most. */
export function elapsedText(ms: number, t: Dictionary): string {
  const minutes = Math.max(1, Math.floor(ms / 60_000))
  if (minutes < 60) {
    return `${minutes} ${t.time.minuteShort}`
  }
  const hours = Math.floor(minutes / 60)
  if (hours < 24) {
    const rest = minutes % 60
    return rest > 0 ? `${hours} ${t.time.hourShort} ${rest} ${t.time.minuteShort}` : `${hours} ${t.time.hourShort}`
  }
  const days = Math.floor(hours / 24)
  const rest = hours % 24
  return rest > 0 ? `${days} ${t.time.dayShort} ${rest} ${t.time.hourShort}` : `${days} ${t.time.dayShort}`
}

export function sinceText(date: string | null | undefined, now: number, t: Dictionary): string | null {
  const from = date ? Date.parse(date) : Number.NaN
  return Number.isNaN(from) ? null : elapsedText(now - from, t)
}

/** "waiting 2 d 4 h" for a review, "in 12 d", "today" or "3 d late" for a date. */
export function upcomingWhen(item: UpcomingItem, t: Dictionary, now: number): string {
  if (item.kind === 'review') {
    return interpolate(t.activity.line.upcomingWaiting, { time: elapsedText(now - Date.parse(item.date), t) })
  }
  const days = item.daysLeft ?? 0
  if (days < 0) {
    return interpolate(t.activity.line.upcomingOverdue, { days: -days })
  }
  return days === 0 ? t.activity.line.upcomingToday : interpolate(t.activity.line.upcomingDays, { days })
}

export function intakeStatus(status: string, t: Dictionary): string {
  const names: Record<string, string> = t.activity.line.intakeStatus
  return names[status] ?? status
}

// ---- Rows: which tokens share a line without touching ----

// A token's width (px, the CSS --tkw) and the gap kept between two tokens of one row.
const TOKEN_WIDTH = 158
const TOKEN_GAP = 8

/**
 * Row 0 sits on the rail, row 1 above it; a token that fits in neither goes to the emptier row. `share` is the part of
 * the line two tokens need between their places so they do not touch (a token's width over the line's travel).
 */
export function assignRows(tokens: Array<Pick<Token, 'x'>>, share = 0.36): number[] {
  const placed: Array<{ x: number; row: number }> = []
  return tokens.map((token) => {
    const clash = (row: number) => placed.some((item) => item.row === row && Math.abs(item.x - token.x) < share)
    const count = (row: number) => placed.filter((item) => item.row === row).length
    const row = !clash(0) ? 0 : !clash(1) ? 1 : count(0) <= count(1) ? 0 : 1
    placed.push({ x: token.x, row })
    return row
  })
}

// ---- Pieces ----

function JobToken({ token, row }: { token: Token; row: number }) {
  const t = useT()
  const format = useFormat()
  const { appInfo } = useActivity()
  const { job, phase } = token
  const app = appInfo(job.appId)
  const title = jobTitle(job, t, app?.Name ?? null)
  const progress = progressText(job, format)
  const percent = job.progress !== null && job.total ? Math.round((job.progress / job.total) * 100) : null
  const step =
    job.stepOrder !== null && job.stepName ? interpolate(t.activity.line.tokenStep, { order: job.stepOrder, name: job.stepName }) : null
  const attempt =
    job.retrying && job.attempt && job.maxAttempts ? interpolate(t.activity.retrying, { attempt: job.attempt, max: job.maxAttempts }) : null
  const reason = phase === 'failed' ? job.message : null
  const label = [title, t.activity.line.phase[phase], step, progress, attempt, reason].filter(Boolean).join(', ')
  const status =
    phase === 'failed'
      ? (reason ?? t.activity.line.phase.failed)
      : percent !== null
        ? `${percent}%`
        : (job.stepName ?? t.activity.line.phase[phase])
  return (
    <li className="tk-slot" data-row={row}>
      <div className="tk-run" style={{ transform: `translateX(${(token.x * 100).toFixed(2)}%)` }}>
        <Link to={`/activity/jobs/${job.id}`} className={cn('tk', phase, token.indeterminate && 'ind')} aria-label={label}>
          <span className="tk-i">{app ? <AppAvatar app={app} size="s" /> : <LogoMark size={18} />}</span>
          <span className="tk-b">
            <span className="tk-t">{app?.Name ?? jobKindLabel(job.kind, t)}</span>
            <span className="tk-s">{status}</span>
          </span>
          {job.steps.length > 0 ? (
            <span className="tk-pips" aria-hidden>
              {job.steps.map((pip) => (
                <i
                  key={pip.order}
                  className={cn(
                    pip.status === StoreJobStepStatus.Failed && 'x',
                    pip.status === StoreJobStepStatus.Running && 'r',
                    pip.status === StoreJobStepStatus.Succeeded && 'ok',
                  )}
                />
              ))}
            </span>
          ) : null}
          <span className="tk-tip" aria-hidden>
            <b>{title}</b>
            <span>{[t.activity.line.phase[phase], step].filter(Boolean).join(' · ')}</span>
            {progress ? <span className="mono">{progress}</span> : null}
            {attempt ? <span>{attempt}</span> : null}
            {reason ? <span className="dng">{reason}</span> : null}
          </span>
        </Link>
      </div>
    </li>
  )
}

function PulseChip({ group, store }: { group: PulseGroup; store: StoreKind }) {
  const t = useT()
  const { appInfo } = useActivity()
  const app = appInfo(group.appId)
  const kind = t.activity.line.pulseKind[group.kind]
  const subject = [app?.Name, group.label].filter(Boolean).join(' · ') || kind
  return (
    <li className="pl-run">
      <span
        className={cn('pl', group.kind === 'rejection' && 'x')}
        title={group.pulses.map((pulse) => pulse.detail ?? pulse.label ?? kind).join('\n')}
        aria-label={interpolate(t.activity.line.pulseLabel, { kind, store: storeLabel(store, t), subject })}
      >
        <i className="pl-dot" aria-hidden />
        <span className="pl-t" aria-hidden>
          {subject}
        </span>
        {group.count > 1 ? (
          <b className="pl-n" aria-hidden>
            ×{group.count}
          </b>
        ) : null}
      </span>
    </li>
  )
}

function HourStrip({ bars, store }: { bars: HourBar[]; store: StoreKind }) {
  const t = useT()
  const format = useFormat()
  const top = Math.max(1, ...bars.map((bar) => bar.succeeded + bar.failed))
  const sum = bars.reduce(
    (all, bar) => ({ ok: all.ok + bar.succeeded, failed: all.failed + bar.failed, inbound: all.inbound + bar.inbound }),
    {
      ok: 0,
      failed: 0,
      inbound: 0,
    },
  )
  return (
    <div className="hs-strip">
      <div className="hs-bars" role="img" aria-label={interpolate(t.activity.line.stripLabel, { store: storeLabel(store, t), ...sum })}>
        {bars.map((bar) => {
          const jobs = bar.succeeded + bar.failed
          const height = jobs > 0 ? Math.max(22, Math.round((jobs / top) * 100)) : 0
          return (
            <span
              key={bar.hour}
              className="hb"
              title={interpolate(t.activity.line.stripHour, {
                hour: format.time(bar.hour),
                ok: bar.succeeded,
                failed: bar.failed,
                inbound: bar.inbound,
              })}
            >
              {jobs > 0 ? <span className={cn('hb-f', bar.failed > 0 && 'x')} style={{ height: `${height}%` }} /> : null}
              {bar.inbound > 0 ? <i className="hb-in" /> : null}
            </span>
          )
        })}
      </div>
      <div className="hs-axis" aria-hidden>
        <span>{t.activity.line.stripStart}</span>
        <span>{t.activity.line.stripNow}</span>
      </div>
    </div>
  )
}

// The line's width, measured, so tokens that would touch are put on different rows.
function useWidth<T extends HTMLElement>(): [RefObject<T>, number] {
  const ref = useRef<T>(null)
  const [width, setWidth] = useState(0)
  useEffect(() => {
    const element = ref.current
    if (!element || typeof ResizeObserver === 'undefined') {
      return
    }
    const observer = new ResizeObserver(([entry]) => setWidth(Math.round(entry.contentRect.width)))
    observer.observe(element)
    return () => observer.disconnect()
  }, [])
  return [ref, width]
}

// When nothing moves on a line, what comes next on it waits on the rail: a review that waits, a date that is due.
const PARKED_PLACES = [0.08, 0.62]

function ParkedChip({ item, now, row, x }: { item: UpcomingItem; now: number; row: number; x: number }) {
  const t = useT()
  const { appInfo } = useActivity()
  const app = appInfo(item.appId)
  const name = app?.Name ?? item.appName ?? t.activity.line.unknownApp
  const kind = t.activity.line.upcomingKind[item.kind]
  const when = upcomingWhen(item, t, now)
  return (
    <li className="tk-slot" data-row={row}>
      <div className="tk-run" style={{ transform: `translateX(${(x * 100).toFixed(2)}%)` }}>
        <Link
          to={item.href}
          className={cn('tk park', item.tone)}
          title={[name, item.subject, kind, when].filter(Boolean).join(' · ')}
          aria-label={[name, item.subject, kind, when].filter(Boolean).join(', ')}
        >
          <span className="tk-i">{app ? <AppAvatar app={app} size="s" /> : <LogoMark size={18} />}</span>
          <span className="tk-b">
            <span className="tk-t">{name}</span>
            <span className="tk-s">
              {kind} · {when}
            </span>
          </span>
        </Link>
      </div>
    </li>
  )
}

function LineTrack({ track, bars, parked, now }: { track: Track; bars: HourBar[]; parked: UpcomingItem[]; now: number }) {
  const t = useT()
  const [laneRef, width] = useWidth<HTMLDivElement>()
  const travel = width - TOKEN_WIDTH
  const share = width > 0 ? (travel > 0 ? Math.min(1.01, (TOKEN_WIDTH + TOKEN_GAP) / travel) : 1.01) : 0.36
  const rows = assignRows(track.tokens, share)
  const parkedRows = assignRows(
    PARKED_PLACES.map((x) => ({ x })),
    share,
  )
  const outbound = track.tokens.some((token) => token.phase === 'out')
  const inbound = track.tokens.some((token) => token.phase === 'in') || track.pulses.length > 0
  return (
    <div className={cn('ln-track', track.failed && 'x')}>
      <div className="ln-lane" ref={laneRef}>
        <span className={cn('ln-rail', outbound && 'mv', track.failed && 'x')} aria-hidden />
        <span className={cn('ln-ret', inbound && 'mv')} aria-hidden />
        <ul className="tk-list" aria-label={interpolate(t.activity.line.trackLabel, { store: storeLabel(track.store, t) })}>
          {track.tokens.map((token, index) => (
            <JobToken key={token.job.id} token={token} row={rows[index]} />
          ))}
          {track.tokens.length === 0
            ? parked
                .slice(0, PARKED_PLACES.length)
                .map((item, index) => <ParkedChip key={item.key} item={item} now={now} row={parkedRows[index]} x={PARKED_PLACES[index]} />)
            : null}
        </ul>
        {track.hidden > 0 ? <span className="tk-more">{interpolate(t.activity.line.moreJobs, { count: track.hidden })}</span> : null}
        {track.pulses.length > 0 ? (
          <ul className="pl-list" aria-label={t.activity.legendIn}>
            {track.pulses.map((group) => (
              <PulseChip key={group.id} group={group} store={track.store} />
            ))}
          </ul>
        ) : null}
      </div>
      <HourStrip bars={bars} store={track.store} />
    </div>
  )
}

const GATE_LIGHT: Record<GateState, string> = { error: 'x', attention: 'w', busy: 'b', calm: 'c' }
const VISIBLE_GATE_ITEMS = 3

function GateRow({ item, now }: { item: GateItem; now: number }) {
  const t = useT()
  const { appInfo } = useActivity()
  const app = appInfo(item.appId)
  const since = sinceText(item.since, now, t)
  const failure = item.kind === 'lastFailed' || item.kind === 'connection'
  const when = since ? interpolate(failure ? t.activity.line.ago : t.activity.line.since, { time: since }) : null
  const name = item.kind === 'connection' ? item.subject : (app?.Name ?? (item.appId ? t.activity.line.unknownApp : null))
  const body = (
    <>
      <span className="gi-i">{app ? <AppAvatar app={app} size="s" /> : <i className={cn('gi-d', item.tone)} />}</span>
      <span className="gi-b">
        <span className="gi-t">
          {name ? <b>{name}</b> : null}
          {item.kind !== 'connection' && item.subject ? <span className="mono"> {item.subject}</span> : null}
        </span>
        <span className={cn('gi-s', item.tone)}>
          {t.activity.line.gateItem[item.kind]}
          {item.rateLimited ? ` · ${t.activity.line.rateLimited}` : null}
          {when ? ` · ${when}` : null}
        </span>
        {failure && item.detail ? <span className="gi-m">{item.detail}</span> : null}
      </span>
    </>
  )
  return (
    <li>
      {item.href ? (
        <Link className="gi" to={item.href}>
          {body}
        </Link>
      ) : (
        <span className="gi">{body}</span>
      )}
    </li>
  )
}

function Gate({
  store,
  items,
  lane,
  moving,
  now,
}: {
  store: StoreKind
  items: GateItem[]
  lane: Lane | undefined
  moving: boolean
  now: number
}) {
  const t = useT()
  const { appName } = useActivity()
  const [open, setOpen] = useState(false)
  const state = gateState(items, moving)
  const shown = open ? items : items.slice(0, VISIBLE_GATE_ITEMS)
  const last = lane?.last ?? null
  const lastAgo = last ? sinceText(last.updatedAt, now, t) : null
  return (
    <section className={cn('gate', GATE_LIGHT[state])} aria-label={storeLabel(store, t)}>
      <header className="gate-h">
        <StoreTag store={store} small />
        <span className={cn('gate-l', GATE_LIGHT[state])}>
          <i aria-hidden />
          {t.activity.line.gateState[state]}
        </span>
      </header>
      {items.length > 0 ? (
        <ul className="gate-b">
          {shown.map((item) => (
            <GateRow key={item.key} item={item} now={now} />
          ))}
        </ul>
      ) : (
        <p className="gate-calm">{t.activity.line.gateCalm}</p>
      )}
      {items.length > VISIBLE_GATE_ITEMS ? (
        <button type="button" className="gate-more" onClick={() => setOpen(!open)} aria-expanded={open}>
          {open ? t.activity.line.gateLess : interpolate(t.activity.line.gateMore, { count: items.length - VISIBLE_GATE_ITEMS })}
        </button>
      ) : null}
      <footer className="gate-f">
        {last && lastAgo
          ? interpolate(t.activity.line.gateLast, {
              title: jobTitle(last, t, appName(last.appId)),
              status: jobStatusView(last.status, t).label.toLocaleLowerCase(),
              time: lastAgo,
            })
          : t.activity.line.gateNever}
      </footer>
    </section>
  )
}

function Hub({ state, tracks, inboundToday, now }: { state: HatState; tracks: Track[]; inboundToday: number; now: number }) {
  const t = useT()
  const { appInfo } = useActivity()
  const tokens = tracks.flatMap((track) => track.tokens)
  const onLine = tokens.filter((token) => isActive(token.job)).length + tracks.reduce((sum, track) => sum + track.hidden, 0)
  const count = (phase: Token['phase']) => tokens.filter((token) => token.phase === phase).length
  const parts = (['out', 'in', 'queued', 'failed'] as const)
    .filter((phase) => count(phase) > 0)
    .map((phase) =>
      interpolate(
        { out: t.activity.line.hubOut, in: t.activity.line.hubIn, queued: t.activity.line.hubQueued, failed: t.activity.line.hubFailed }[
          phase
        ],
        { count: count(phase) },
      ),
    )
  const intakes = activeIntakes(state)
  const last = lastIntake(state)
  const lastEvent = state.lastEventAt ? sinceText(state.lastEventAt, now, t) : null
  return (
    <div className="ln-hub">
      <div className={cn('hub-core', onLine > 0 && 'on')}>
        <span className="hub-mark" aria-hidden>
          <LogoMark size={30} />
        </span>
        <b className="hub-n">{onLine > 0 ? interpolate(t.activity.line.hubOnLine, { count: onLine }) : t.activity.quietTitle}</b>
        <span className="hub-d">
          {parts.length > 0
            ? parts.join(' · ')
            : lastEvent
              ? interpolate(t.activity.line.hubLastEvent, { time: lastEvent })
              : t.activity.line.hubNoEvent}
        </span>
      </div>
      <div className="hub-src">
        <h3 className="hub-k">{t.activity.line.sources}</h3>
        <div className={cn('src', intakes.length > 0 && 'on')}>
          <span className="src-t">{t.activity.line.sourceIntake}</span>
          {intakes.length > 0 ? (
            <ul className="src-l">
              {intakes.slice(0, 3).map((item) => {
                const app = appInfo(item.appId)
                return (
                  <li key={item.sessionId} className="src-i">
                    {app ? <AppAvatar app={app} size="s" /> : null}
                    <span className="min-w-0 truncate">{app?.Name ?? t.activity.line.unknownApp}</span>
                    <span className="src-st">{intakeStatus(item.status, t)}</span>
                  </li>
                )
              })}
            </ul>
          ) : (
            <span className="src-d">
              {last
                ? interpolate(t.activity.line.sourceIntakeLast, {
                    subject: [appInfo(last.appId)?.Name, intakeStatus(last.status, t)].filter(Boolean).join(' '),
                    time: sinceText(new Date(last.receivedAt).toISOString(), now, t) ?? '',
                  })
                : t.activity.line.sourceIntakeIdle}
            </span>
          )}
        </div>
        <div className="src">
          <span className="src-t">{t.activity.line.sourceStore}</span>
          <span className="src-d">
            {inboundToday > 0 ? interpolate(t.activity.line.sourceStoreCount, { count: inboundToday }) : t.activity.line.sourceStoreNone}
          </span>
        </div>
      </div>
    </div>
  )
}

export interface StoreRow {
  store: StoreKind
  track: Track
  lane: Lane | undefined
  items: GateItem[]
  bars: HourBar[]
  // What comes next on this line (shown on the rail while nothing moves).
  parked: UpcomingItem[]
}

/**
 * The line: storeasy and its sources, then one row per store (its line, then its gate). The grid keeps every row's line
 * and gate side by side on wide screens and stacks them on a phone; nothing is positioned over another element.
 */
export function HatLine({
  state,
  rows,
  inboundToday,
  now,
  calm,
  frozen,
}: {
  state: HatState
  rows: StoreRow[]
  inboundToday: number
  now: number
  calm: boolean
  frozen: boolean
}) {
  const t = useT()
  return (
    <div
      className={cn('ln', calm && 'calm', frozen && 'frozen', rows.length === 1 && 'one')}
      role="group"
      aria-label={t.activity.sceneLabel}
    >
      <Hub state={state} tracks={rows.map((row) => row.track)} inboundToday={inboundToday} now={now} />
      <div className="ln-rows">
        {rows.map((row) => (
          <div className="ln-row" key={row.store}>
            <LineTrack track={row.track} bars={row.bars} parked={row.parked} now={now} />
            <Gate
              store={row.store}
              items={row.items}
              lane={row.lane}
              moving={row.track.tokens.some((token) => isActive(token.job))}
              now={now}
            />
          </div>
        ))}
      </div>
    </div>
  )
}
