import type { BadgeTone } from '@/components/ui/Badge'
import { cultureOf, type Lang } from '@/lib/i18n/lang'
import type { StoreMetricsDictionary } from './i18n'
import { MetricFeedStatus, TrafficSource, type MetricKey, type StoreMetricValuesDto } from './types'

// Labels and number formats of the store performance screens.

const SOURCE_KEYS: Record<TrafficSource, keyof StoreMetricsDictionary['source']> = {
  [TrafficSource.All]: 'all',
  [TrafficSource.Search]: 'search',
  [TrafficSource.Browse]: 'browse',
  [TrafficSource.AppReferrer]: 'appReferrer',
  [TrafficSource.WebReferrer]: 'webReferrer',
  [TrafficSource.Referral]: 'referral',
  [TrafficSource.AppClip]: 'appClip',
  [TrafficSource.Notification]: 'notification',
  [TrafficSource.StoreClient]: 'storeClient',
  [TrafficSource.StoreWeb]: 'storeWeb',
  [TrafficSource.Other]: 'other',
  [TrafficSource.Unavailable]: 'unavailable',
}

/** The traffic sources a filter offers, in a stable order. */
export const SOURCES: TrafficSource[] = (Object.values(TrafficSource) as TrafficSource[]).filter((source) => source !== TrafficSource.All)

export function sourceLabel(source: TrafficSource | null | undefined, t: StoreMetricsDictionary): string {
  return t.source[SOURCE_KEYS[source ?? TrafficSource.All] ?? 'other']
}

export function feedStatusView(status: MetricFeedStatus, t: StoreMetricsDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case MetricFeedStatus.Active:
      return { label: t.feeds.status.active, tone: 'success' }
    case MetricFeedStatus.Waiting:
      return { label: t.feeds.status.waiting, tone: 'info' }
    case MetricFeedStatus.NeedsSetup:
      return { label: t.feeds.status.needsSetup, tone: 'warning' }
    case MetricFeedStatus.AccessDenied:
      return { label: t.feeds.status.accessDenied, tone: 'danger' }
    case MetricFeedStatus.Failed:
      return { label: t.feeds.status.failed, tone: 'danger' }
    default:
      return { label: t.feeds.status.notEnabled, tone: 'neutral' }
  }
}

/** A country's name in the UI language ("TR" is "Türkiye"), the code when the browser does not know it. */
export function countryName(code: string | null | undefined, lang: Lang, t: StoreMetricsDictionary): string {
  if (!code || code === 'ZZ') {
    return t.unknownCountry
  }
  try {
    return new Intl.DisplayNames([cultureOf(lang)], { type: 'region' }).of(code) ?? code
  } catch {
    return code
  }
}

export const METRIC_KEYS: MetricKey[] = ['Impressions', 'PageViews', 'Downloads', 'ConversionRate']

export function metricLabel(metric: MetricKey | 'Redownloads', t: StoreMetricsDictionary): string {
  switch (metric) {
    case 'Impressions':
      return t.metric.impressions
    case 'PageViews':
      return t.metric.pageViews
    case 'Downloads':
      return t.metric.downloads
    case 'Redownloads':
      return t.metric.redownloads
    default:
      return t.metric.conversionRate
  }
}

/** "1.234" or "%5,2" / "5.2%": counts as whole numbers, the conversion rate as a percentage. */
export function formatMetric(metric: MetricKey | 'Redownloads', value: number | null | undefined, lang: Lang): string {
  if (value == null) {
    return '-'
  }
  const culture = cultureOf(lang)
  return metric === 'ConversionRate'
    ? new Intl.NumberFormat(culture, { style: 'percent', maximumFractionDigits: 1 }).format(value)
    : new Intl.NumberFormat(culture, { maximumFractionDigits: 0 }).format(value)
}

/** A share as a percentage without a sign ("%10" / "10%"). */
export function formatPercent(value: number, lang: Lang): string {
  return new Intl.NumberFormat(cultureOf(lang), { style: 'percent', maximumFractionDigits: 1 }).format(value)
}

/** A change as a signed percentage ("+12%"), or null when there is nothing to compare with. */
export function formatChange(change: number | null | undefined, lang: Lang): string | null {
  if (change == null) {
    return null
  }
  return new Intl.NumberFormat(cultureOf(lang), { style: 'percent', maximumFractionDigits: 1, signDisplay: 'exceptZero' }).format(change)
}

export function metricValue(values: StoreMetricValuesDto | null | undefined, metric: MetricKey | 'Redownloads'): number | null {
  return values ? (values[metric] ?? null) : null
}
