import type { BadgeTone } from '@/components/ui/Badge'
import type { DateOnly } from '@/lib/api/types'
import { cultureOf, type Lang } from '@/lib/i18n/lang'
import type { MetricKey } from '@/features/store-metrics/types'
import { storeSlug } from '@/features/common/store'
import type { ImpactDictionary } from './i18n'
import { ImpactEventKind, ImpactMetric, ImpactSignal, ImpactStatus, type AsoImpactEventDto } from './types'

// Labels, links and the chart marker positions of the impact screens.

const KIND_KEYS: Record<ImpactEventKind, keyof ImpactDictionary['kind']> = {
  [ImpactEventKind.Listing]: 'listing',
  [ImpactEventKind.Screenshots]: 'screenshots',
  [ImpactEventKind.Icon]: 'icon',
  [ImpactEventKind.Previews]: 'previews',
  [ImpactEventKind.Price]: 'price',
  [ImpactEventKind.Category]: 'category',
  [ImpactEventKind.VersionReleased]: 'versionReleased',
  [ImpactEventKind.MsChange]: 'msChange',
  [ImpactEventKind.ExperimentStarted]: 'experimentStarted',
  [ImpactEventKind.ExperimentEnded]: 'experimentEnded',
}

export function kindLabel(kind: ImpactEventKind, t: ImpactDictionary): string {
  return t.kind[KIND_KEYS[kind] ?? 'listing']
}

/** The line a timeline or a marker shows: "Store texts: 2.1" or the kind with the version. */
export function eventTitle(event: AsoImpactEventDto, t: ImpactDictionary): string {
  const kind = kindLabel(event.Kind, t)
  if (event.Kind === ImpactEventKind.VersionReleased && event.VersionString) {
    return `${kind}: ${event.VersionString}`
  }
  return event.Locales.length > 0 ? `${kind} (${event.Locales.join(', ')})` : kind
}

export function signalView(signal: ImpactSignal, t: ImpactDictionary): { label: string; tone: BadgeTone } {
  switch (signal) {
    case ImpactSignal.Significant:
      return { label: t.signal.significant, tone: 'success' }
    case ImpactSignal.Suggestive:
      return { label: t.signal.suggestive, tone: 'info' }
    case ImpactSignal.NotSignificant:
      return { label: t.signal.notSignificant, tone: 'neutral' }
    case ImpactSignal.NotApplicable:
      return { label: t.signal.notApplicable, tone: 'neutral' }
    default:
      return { label: t.signal.insufficient, tone: 'warning' }
  }
}

export function statusView(status: ImpactStatus, t: ImpactDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case ImpactStatus.Ready:
      return { label: t.status.ready, tone: 'success' }
    case ImpactStatus.Collecting:
      return { label: t.status.collecting, tone: 'info' }
    case ImpactStatus.AwaitingRelease:
      return { label: t.status.awaiting, tone: 'warning' }
    default:
      return { label: t.status.noData, tone: 'neutral' }
  }
}

export const IMPACT_METRICS: Array<{ metric: ImpactMetric; key: MetricKey }> = [
  { metric: ImpactMetric.Impressions, key: 'Impressions' },
  { metric: ImpactMetric.PageViews, key: 'PageViews' },
  { metric: ImpactMetric.Downloads, key: 'Downloads' },
  { metric: ImpactMetric.ConversionRate, key: 'ConversionRate' },
]

/** Where the change itself is: its plan, or the versions page of its store. */
export function eventLink(event: AsoImpactEventDto): string | null {
  if (event.PlanId != null) {
    return `/apps/${event.AppId}/listing/plans/${event.PlanId}`
  }
  if (event.Id.startsWith('version-') || event.Id.startsWith('ms-')) {
    return `/apps/${event.AppId}/release/versions?store=${storeSlug(event.Store)}`
  }
  return null
}

/** The impact view of a change. */
export function impactLink(event: Pick<AsoImpactEventDto, 'AppId' | 'Id'>): string {
  return `/apps/${event.AppId}/store-performance/impact?event=${encodeURIComponent(event.Id)}`
}

/** An average rank ("12,5" / "12.5"). */
export function formatRank(value: number | null | undefined, lang: Lang): string {
  return value == null ? '-' : new Intl.NumberFormat(cultureOf(lang), { maximumFractionDigits: 1 }).format(value)
}

function dayNumber(date: DateOnly): number {
  return Math.round(Date.UTC(Number(date.slice(0, 4)), Number(date.slice(5, 7)) - 1, Number(date.slice(8, 10))) / 86400000)
}

/**
 * The fractional index of a day among a chart's points (which may skip days without data): exact when the day is a
 * point, between its neighbours otherwise; null outside the chart.
 */
export function markerIndex(date: DateOnly, dates: DateOnly[]): number | null {
  if (dates.length === 0) {
    return null
  }
  const target = dayNumber(date)
  const days = dates.map(dayNumber)
  if (target < days[0] || target > days[days.length - 1]) {
    return null
  }
  for (let index = 0; index < days.length; index += 1) {
    if (days[index] === target) {
      return index
    }
    if (index + 1 < days.length && days[index] < target && target < days[index + 1]) {
      return index + (target - days[index]) / (days[index + 1] - days[index])
    }
  }
  return null
}
