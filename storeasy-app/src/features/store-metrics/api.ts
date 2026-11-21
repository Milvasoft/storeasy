import { useQuery } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut } from '@/lib/api/http'
import type { MetricFilter, MetricRange, StoreMetricFeedDto, StoreMetricsReportDto, StoreMetricsSummaryDto } from './types'

// storeasy-api StoreMetricsController. Reads come from storeasy's daily rows; the only store write is enabling the App
// Store analytics reports (a confirmed, audited job).
export const storeMetricsApi = {
  // View. 404 AppNotFound; 400 StoreMetricsRangeInvalid past 400 days or a reversed range.
  report: (organizationId: number, appId: number, filter: MetricFilter) =>
    apiGet<StoreMetricsReportDto>('/store-metrics', { OrganizationId: organizationId, AppId: appId, ...clean(filter) }),

  // View. Every app of the organization.
  summary: (organizationId: number, range: MetricRange & { Store?: number | null }) =>
    apiGet<StoreMetricsSummaryDto>('/store-metrics/summary', { OrganizationId: organizationId, ...clean(range) }),

  // View. One row per store record of the app.
  feeds: (organizationId: number, appId: number) =>
    apiGet<StoreMetricFeedDto[]>('/store-metrics/feeds', { OrganizationId: organizationId, AppId: appId }),

  // EditContent. Read only jobs (one per store record); 429 past 30 an hour. Returns the job ids.
  sync: (organizationId: number, appId: number | null) =>
    apiPost<number[]>('/store-metrics/sync', { OrganizationId: organizationId, AppId: appId }),

  // ManageConnections. The one store write: an ONGOING analytics report request at Apple. 400 without Confirm, 409 when
  // already enabled. Returns the job id.
  enableApple: (organizationId: number, storeAppId: number) =>
    apiPost<number>('/store-metrics/apple/enable', { OrganizationId: organizationId, StoreAppId: storeAppId, Confirm: true }),

  // ManageConnections. Empty clears it; answers the stored bucket name. 400 ReportBucketInvalid.
  setBucket: (organizationId: number, connectionId: number, bucket: string) =>
    apiPut<string | null>('/store-metrics/google/bucket', { OrganizationId: organizationId, ConnectionId: connectionId, Bucket: bucket }),
}

function clean<T extends object>(value: T): Record<string, string | number> {
  return Object.fromEntries(Object.entries(value).filter(([, item]) => item != null && item !== '')) as Record<string, string | number>
}

export const storeMetricsKeys = {
  all: ['store-metrics'] as const,
  report: (organizationId: number, appId: number, filter: MetricFilter) =>
    ['store-metrics', 'report', organizationId, appId, filter] as const,
  feeds: (organizationId: number, appId: number) => ['store-metrics', 'feeds', organizationId, appId] as const,
  summary: (organizationId: number, range: MetricRange & { Store?: number | null }) =>
    ['store-metrics', 'summary', organizationId, range] as const,
}

export function useStoreMetrics(organizationId: number, appId: number, filter: MetricFilter) {
  return useQuery({
    queryKey: storeMetricsKeys.report(organizationId, appId, filter),
    queryFn: () => storeMetricsApi.report(organizationId, appId, filter),
    placeholderData: (previous) => previous,
  })
}

export function useMetricFeeds(organizationId: number, appId: number) {
  return useQuery({
    queryKey: storeMetricsKeys.feeds(organizationId, appId),
    queryFn: () => storeMetricsApi.feeds(organizationId, appId),
  })
}

export function useMetricsSummary(organizationId: number, range: MetricRange & { Store?: number | null }) {
  return useQuery({
    queryKey: storeMetricsKeys.summary(organizationId, range),
    queryFn: () => storeMetricsApi.summary(organizationId, range),
    placeholderData: (previous) => previous,
  })
}
