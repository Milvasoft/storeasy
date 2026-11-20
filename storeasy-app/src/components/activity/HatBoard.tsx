import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight, BellOff, CircleCheck, CircleX, Clock3, ExternalLink, TriangleExclamationPoint } from 'lucide-react'
import { AppAvatar } from '@/components/apps/AppAvatar'
import { StoreTag } from '@/components/ui/Badge'
import { storeLabel, upcomingWhen as whenText } from '@/components/activity/Hat'
import { useActivity } from '@/lib/activity/ActivityContext'
import { isExternalHref, MAX_ATTENTION, type AttentionItem, type UpcomingItem } from '@/lib/activity/board'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import type { Dictionary } from '@/lib/i18n/en'
import { cn } from '@/lib/utils/cn'

// The two calm panels of the live page: what needs a person (one or two things, each with the page to act on it) and
// what is coming (reviews waiting, TestFlight expiries, compliance deadlines), a departures board that stays useful
// when nothing moves.

function attentionText(item: AttentionItem, t: Dictionary, app: string): string {
  const line = t.activity.line.attention
  const subject = item.subject ?? ''
  switch (item.kind) {
    case 'rejection':
      return interpolate(line.rejection, { app, subject }).trim()
    case 'jobFailed':
      return interpolate(line.jobFailed, { store: item.store ? storeLabel(item.store, t) : '' })
    case 'connection':
      return interpolate(line.connection, { subject })
    case 'pendingRelease':
      return interpolate(line.pendingRelease, { app, subject }).trim()
    case 'pendingPublish':
      return interpolate(line.pendingPublish, { app, subject }).trim()
    case 'expiry':
      return interpolate(line.expiry, { app, subject, days: Math.max(0, item.daysLeft ?? 0) })
    case 'deadline': {
      // A rule shared by several apps is one card: "3 apps" stands where the app name would be.
      const who = (item.count ?? 1) > 1 ? interpolate(line.apps, { count: item.count ?? 0 }) : app
      return (item.daysLeft ?? 0) < 0
        ? interpolate(line.deadlineOverdue, { app: who, subject })
        : interpolate(line.deadline, { app: who, subject, days: item.daysLeft ?? 0 })
    }
    case 'unverified':
      return interpolate(line.unverified, { count: item.count ?? 0 })
  }
}

function AttentionCard({ item }: { item: AttentionItem }) {
  const t = useT()
  const { appInfo } = useActivity()
  const app = appInfo(item.appId)
  const name = app?.Name ?? item.appName ?? t.activity.line.unknownApp
  const Icon = item.tone === 'danger' ? CircleX : item.kind === 'expiry' || item.kind === 'deadline' || item.kind === 'unverified' ? Clock3 : TriangleExclamationPoint
  return (
    <li className={cn('att', item.tone)}>
      <Icon className="att-ic" aria-hidden />
      <div className="min-w-0">
        <b className="att-t">{attentionText(item, t, name)}</b>
        {item.detail ? <span className="att-d">{item.detail}</span> : null}
      </div>
      {isExternalHref(item.href) ? (
        <a className="att-a" href={item.href} target="_blank" rel="noopener noreferrer">
          {t.activity.line.attentionAction[item.kind]}
          <ExternalLink aria-hidden />
        </a>
      ) : (
        <Link className="att-a" to={item.href}>
          {t.activity.line.attentionAction[item.kind]}
          <ArrowRight aria-hidden />
        </Link>
      )}
    </li>
  )
}

/** Says that apps with muted alerts were left out, so nothing disappears silently; their names are in the tooltip. */
function MutedNote({ appIds }: { appIds: number[] }) {
  const t = useT()
  const { appName } = useActivity()
  if (appIds.length === 0) {
    return null
  }
  const names = appIds.map((id) => appName(id) ?? `#${id}`).join(', ')
  return (
    <p className="att-muted" title={names}>
      <BellOff aria-hidden />
      {interpolate(t.activity.line.mutedNote, { count: appIds.length })}
    </p>
  )
}

/** One or two things a person should act on, most urgent first; the rest behind "+n more". */
export function AttentionBar({ items, mutedAppIds = [] }: { items: AttentionItem[]; mutedAppIds?: number[] }) {
  const t = useT()
  const [open, setOpen] = useState(false)
  if (items.length === 0) {
    return (
      <div className="att-bar">
        <div className="att-none">
          <CircleCheck aria-hidden />
          {t.activity.line.attentionNone}
        </div>
        <MutedNote appIds={mutedAppIds} />
      </div>
    )
  }
  const shown = open ? items : items.slice(0, MAX_ATTENTION)
  return (
    <section className="att-bar" aria-label={t.activity.line.attentionTitle}>
      <h2 className="att-h">{t.activity.line.attentionTitle}</h2>
      <ul className="att-l">
        {shown.map((item) => (
          <AttentionCard key={item.key} item={item} />
        ))}
      </ul>
      {items.length > MAX_ATTENTION ? (
        <button type="button" className="gate-more" onClick={() => setOpen(!open)} aria-expanded={open}>
          {open ? t.activity.line.gateLess : interpolate(t.activity.line.attentionMore, { count: items.length - MAX_ATTENTION })}
        </button>
      ) : null}
      <MutedNote appIds={mutedAppIds} />
    </section>
  )
}

/** The departures board: what waits and what is due, soonest first. `rows` caps it (fewer while jobs are moving). */
export function UpcomingBoard({ items, rows, now }: { items: UpcomingItem[]; rows: number; now: number }) {
  const t = useT()
  const format = useFormat()
  const { appInfo } = useActivity()
  const [open, setOpen] = useState(false)
  const shown = open ? items : items.slice(0, rows)
  return (
    <section className="up" aria-label={t.activity.line.upcomingTitle}>
      <h2 className="up-h">{t.activity.line.upcomingTitle}</h2>
      {items.length === 0 ? (
        <p className="up-e">{t.activity.line.upcomingEmpty}</p>
      ) : (
        <ol className="up-l">
          {shown.map((item) => {
            const app = appInfo(item.appId)
            const name = app?.Name ?? item.appName ?? t.activity.line.unknownApp
            return (
              <li key={item.key}>
                <Link className={cn('up-r', item.tone)} to={item.href}>
                  <span className="up-w" title={item.kind === 'review' ? format.dateTime(item.date) : format.date(item.date)}>
                    {whenText(item, t, now)}
                  </span>
                  <span className="up-a">
                    {app ? <AppAvatar app={app} size="s" /> : null}
                    <b className="truncate">{name}</b>
                  </span>
                  <span className="up-k">
                    <span className="up-kind">{t.activity.line.upcomingKind[item.kind]}</span>
                    <span className="truncate">{item.subject}</span>
                  </span>
                  <span className="up-s">
                    <StoreTag store={item.store} small codeOnly />
                  </span>
                </Link>
              </li>
            )
          })}
        </ol>
      )}
      {items.length > rows ? (
        <button type="button" className="gate-more" onClick={() => setOpen(!open)} aria-expanded={open}>
          {open ? t.activity.line.gateLess : interpolate(t.activity.line.gateMore, { count: items.length - rows })}
        </button>
      ) : null}
    </section>
  )
}
