import type { DateOnly, IsoDateTime, StoreKind } from '@/lib/api/types'

// The store page performance contract (storeasy-api StoreMetricsController, Application/Dtos/StoreMetricDtos).

// Storeasy.Domain.Enums.TrafficSource: where a store visitor came from. All is a row not split by source.
export const TrafficSource = {
  All: 0,
  Search: 1,
  Browse: 2,
  AppReferrer: 3,
  WebReferrer: 4,
  Referral: 5,
  AppClip: 6,
  Notification: 7,
  StoreClient: 8,
  StoreWeb: 9,
  Other: 10,
  Unavailable: 11,
} as const
export type TrafficSource = (typeof TrafficSource)[keyof typeof TrafficSource]

// Storeasy.Domain.Enums.MetricFeedStatus: whether data arrives for a store record.
export const MetricFeedStatus = { NotEnabled: 0, Waiting: 1, Active: 2, NeedsSetup: 3, AccessDenied: 4, Failed: 5 } as const
export type MetricFeedStatus = (typeof MetricFeedStatus)[keyof typeof MetricFeedStatus]

// The metrics a chart can show (Storeasy.Domain.Enums.StoreMetricKind, as names).
export type MetricKey = 'Impressions' | 'PageViews' | 'Downloads' | 'ConversionRate'

export interface StoreMetricValuesDto {
  Impressions: number | null
  PageViews: number | null
  Downloads: number | null
  Redownloads: number | null
  // Downloads divided by page views (0.05 is 5 percent), over the stores that report both.
  ConversionRate: number | null
}

export interface StoreMetricDayDto extends StoreMetricValuesDto {
  Date: DateOnly
}

// Fractions against the previous period (0.25 is 25 percent more); null when there is nothing to compare with.
export type StoreMetricChangesDto = StoreMetricValuesDto

export interface StoreMetricBreakdownDto {
  Source: TrafficSource | null
  Country: string | null
  Store: StoreKind | null
  Totals: StoreMetricValuesDto
}

export interface StoreMetricsReportDto {
  From: DateOnly
  To: DateOnly
  PreviousFrom: DateOnly
  PreviousTo: DateOnly
  Totals: StoreMetricValuesDto
  PreviousTotals: StoreMetricValuesDto
  Changes: StoreMetricChangesDto
  Days: StoreMetricDayDto[]
  Sources: StoreMetricBreakdownDto[]
  Countries: StoreMetricBreakdownDto[]
  Stores: StoreMetricBreakdownDto[]
}

export interface StoreMetricFeedDto {
  StoreAppId: number
  Store: StoreKind
  ConnectionId: number | null
  ConnectionName: string | null
  Status: MetricFeedStatus
  // App Store: the analytics reports can be enabled (a connection and no request yet).
  CanEnable: boolean
  // Google Play: the connection's reports bucket.
  ReportBucket: string | null
  EnabledDate: IsoDateTime | null
  EnabledByUserName: string | null
  LastFetchedDate: IsoDateTime | null
  LastSucceededDate: IsoDateTime | null
  EarliestDataDate: DateOnly | null
  LatestDataDate: DateOnly | null
  LastError: string | null
  LastJobId: number | null
}

export interface AppStoreMetricsDto {
  AppId: number
  AppName: string
  Totals: StoreMetricValuesDto
  Changes: StoreMetricChangesDto
}

export interface StoreMetricsSummaryDto {
  From: DateOnly
  To: DateOnly
  Totals: StoreMetricValuesDto
  Changes: StoreMetricChangesDto
  Apps: AppStoreMetricsDto[]
}

// The range of a read: the last Days days ending yesterday, or From to To.
export interface MetricRange {
  Days?: number | null
  From?: DateOnly | null
  To?: DateOnly | null
}

export interface MetricFilter extends MetricRange {
  Store?: StoreKind | null
  Country?: string | null
  Source?: TrafficSource | null
}
