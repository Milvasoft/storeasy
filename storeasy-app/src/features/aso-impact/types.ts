import type { DateOnly, IsoDateTime, StoreKind } from '@/lib/api/types'
import type { TrafficSource } from '@/features/store-metrics/types'

// The before and after impact contract (storeasy-api AsoImpactController, Application/Dtos/AsoDtos/AsoImpact*).

// Storeasy.Domain.Enums.ImpactEventKind: what changed.
export const ImpactEventKind = {
  Listing: 1,
  Screenshots: 2,
  Icon: 3,
  Previews: 4,
  Price: 5,
  Category: 6,
  VersionReleased: 10,
  MsChange: 11,
  ExperimentStarted: 20,
  ExperimentEnded: 21,
  ExperimentWinnerApplied: 22,
} as const
export type ImpactEventKind = (typeof ImpactEventKind)[keyof typeof ImpactEventKind]

// Storeasy.Domain.Enums.ImpactDateSource: where the effect day comes from.
export const ImpactDateSource = { StoreLive: 1, Applied: 2, AwaitingRelease: 3, Experiment: 4 } as const
export type ImpactDateSource = (typeof ImpactDateSource)[keyof typeof ImpactDateSource]

// Storeasy.Domain.Enums.ImpactSignal: how sure the conversion rate change is (two proportion z-test).
export const ImpactSignal = { InsufficientData: 0, NotSignificant: 1, Suggestive: 2, Significant: 3, NotApplicable: 4 } as const
export type ImpactSignal = (typeof ImpactSignal)[keyof typeof ImpactSignal]

// Storeasy.Domain.Enums.ImpactStatus: whether a report can be read yet.
export const ImpactStatus = { Ready: 1, Collecting: 2, AwaitingRelease: 3, NoData: 4 } as const
export type ImpactStatus = (typeof ImpactStatus)[keyof typeof ImpactStatus]

// Storeasy.Domain.Enums.StoreMetricKind as numbers (the report's Metric field).
export const ImpactMetric = { Impressions: 1, PageViews: 2, Downloads: 3, Redownloads: 4, ConversionRate: 5 } as const
export type ImpactMetric = (typeof ImpactMetric)[keyof typeof ImpactMetric]

export interface AsoImpactEventDto {
  // plan-12, version-34, ms-56
  Id: string
  Kind: ImpactEventKind
  Date: DateOnly
  At: IsoDateTime | null
  DateSource: ImpactDateSource
  AppId: number
  AppName: string | null
  StoreAppId: number
  Store: StoreKind
  Title: string | null
  Locales: string[]
  Fields: string[]
  PlanId: number | null
  VersionString: string | null
  ExternalId: string | null
}

export interface AsoImpactWindowDto {
  From: DateOnly
  To: DateOnly
  DaysWithData: number
}

export interface AsoImpactMetricDto {
  Metric: ImpactMetric
  // Daily average (counts) or the rate (conversion).
  Before: number | null
  After: number | null
  BeforeTotal: number | null
  AfterTotal: number | null
  AbsoluteChange: number | null
  // 0.1 is 10 percent up.
  RelativeChange: number | null
}

export interface AsoImpactConversionTestDto {
  Signal: ImpactSignal
  ZScore: number | null
  PValue: number | null
  BeforeDownloads: number
  BeforePageViews: number
  AfterDownloads: number
  AfterPageViews: number
}

export interface AsoImpactDayDto {
  Offset: number
  Date: DateOnly
  Impressions: number | null
  PageViews: number | null
  Downloads: number | null
  ConversionRate: number | null
}

export interface AsoImpactKeywordDto {
  Keyword: string
  Country: string
  Before: number | null
  After: number | null
  // Negative is an improvement.
  Change: number | null
  BeforeCheckedDays: number
  BeforeRankedDays: number
  AfterCheckedDays: number
  AfterRankedDays: number
}

export interface AsoImpactRanksDto {
  KeywordCount: number
  Compared: number
  BeforeAverage: number | null
  AfterAverage: number | null
  BeforeTop10: number
  AfterTop10: number
  Improved: number
  Declined: number
  Keywords: AsoImpactKeywordDto[]
}

export interface AsoImpactReportDto {
  Event: AsoImpactEventDto
  Status: ImpactStatus
  WindowDays: number
  GapDays: number
  WholeWeeks: boolean
  WeekdayMatched: boolean
  Before: AsoImpactWindowDto
  After: AsoImpactWindowDto
  ReadyDate: DateOnly
  Country: string | null
  Source: TrafficSource | null
  Metrics: AsoImpactMetricDto[]
  Conversion: AsoImpactConversionTestDto
  BeforeDays: AsoImpactDayDto[]
  AfterDays: AsoImpactDayDto[]
  Ranks: AsoImpactRanksDto | null
  Confounders: AsoImpactEventDto[]
  Related: AsoImpactEventDto[]
}

export interface AsoImpactSummaryDto {
  Event: AsoImpactEventDto
  Status: ImpactStatus
  PageViews: AsoImpactMetricDto | null
  Downloads: AsoImpactMetricDto | null
  ConversionRate: AsoImpactMetricDto | null
  Signal: ImpactSignal
  Confounded: boolean
}

export interface AsoImpactRecentDto {
  From: DateOnly
  To: DateOnly
  WindowDays: number
  Items: AsoImpactSummaryDto[]
}

export interface ImpactEventFilter {
  StoreAppId?: number | null
  Store?: StoreKind | null
  From?: DateOnly | null
  To?: DateOnly | null
  Days?: number | null
}

export interface ImpactReportOptions {
  WindowDays: number
  GapDays: number
  Country?: string | null
  Source?: TrafficSource | null
  IncludeRanks?: boolean
}
