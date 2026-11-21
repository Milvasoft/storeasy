import type { ChangePlanStatus, MediaKind } from '@/features/common/types'
import type { IsoDateTime, StoreKind } from '@/lib/api/types'

// AsoExperimentsController (/api/v1.0/aso-experiments): store listing A/B tests and custom pages ("Deneyler"). Numeric
// enums mirror Storeasy.Domain.Enums.

export const AsoExperimentKind = {
  ProductPageOptimization: 1,
  CustomProductPage: 2,
  PlayListingExperiment: 3,
  PlayCustomListing: 4,
} as const
export type AsoExperimentKind = (typeof AsoExperimentKind)[keyof typeof AsoExperimentKind]

export const AsoExperimentStatus = {
  Draft: 1,
  Prepared: 2,
  InReview: 3,
  Approved: 4,
  Running: 5,
  Rejected: 6,
  Stopped: 7,
  Completed: 8,
} as const
export type AsoExperimentStatus = (typeof AsoExperimentStatus)[keyof typeof AsoExperimentStatus]

// Push: create or update in the store; Submit: send to App Review; Start, Stop; ApplyWinner: write the winner's media to
// the editable App Store version.
export const AsoExperimentOperation = { Push: 1, Submit: 2, Start: 3, Stop: 4, ApplyWinner: 5 } as const
export type AsoExperimentOperation = (typeof AsoExperimentOperation)[keyof typeof AsoExperimentOperation]

export const AsoStoreSupport = { Api: 1, Assisted: 2, NotSupported: 3 } as const
export type AsoStoreSupport = (typeof AsoStoreSupport)[keyof typeof AsoStoreSupport]

export interface AsoVariantMediaDto {
  Kind: MediaKind
  ClassKey: string
  AssetIds: number[]
}

export interface AsoVariantLocaleDto {
  Locale: string
  PromotionalText?: string | null
  ShortDescription?: string | null
  FullDescription?: string | null
  Media: AsoVariantMediaDto[]
}

export interface AsoExperimentVariantDto {
  /** "A", "B", "C". */
  Key: string
  Name: string
  /** Apple product page optimization: an alternate icon name from the build. */
  AppIconName?: string | null
  ExternalId?: string | null
  Locales: AsoVariantLocaleDto[]
}

export interface AsoExperimentSummaryDto {
  Id: number
  AppId: number
  StoreAppId: number
  Store: StoreKind
  Kind: AsoExperimentKind
  Name: string
  Status: AsoExperimentStatus
  StoreState: string | null
  TrafficProportion: number | null
  VariantCount: number
  StartDate: IsoDateTime | null
  EndDate: IsoDateTime | null
  WinnerVariantKey: string | null
  Url: string | null
  UpdatedDate: IsoDateTime
}

export interface AsoKindSupportDto {
  Kind: AsoExperimentKind
  Support: AsoStoreSupport
}

/** What one store record supports; Microsoft comes with no kinds (not supported). */
export interface AsoStoreCapabilityDto {
  StoreAppId: number
  Store: StoreKind
  Kinds: AsoKindSupportDto[]
}

export interface AsoExperimentOverviewDto {
  Experiments: AsoExperimentSummaryDto[]
  Capabilities: AsoStoreCapabilityDto[]
}

export type AsoTimelineKind = 'created' | 'plan' | 'submitted' | 'started' | 'ended' | 'result' | 'synced'

export interface AsoTimelineEntryDto {
  Date: IsoDateTime
  Kind: AsoTimelineKind
  Title: string
  PlanId: number | null
  PlanStatus: ChangePlanStatus | null
}

/** The assisted step of a Play experiment, localized by the API. */
export interface AsoAssistedDto {
  Url: string
  Steps: string[]
  Pitfalls: string[]
}

export interface AsoExperimentDto extends AsoExperimentSummaryDto {
  ExternalId: string | null
  Variants: AsoExperimentVariantDto[]
  ResultSummary: string | null
  ResultUrl: string | null
  Note: string | null
  ReviewRequired: boolean | null
  ChecklistDone: number[]
  Assisted: AsoAssistedDto | null
  Timeline: AsoTimelineEntryDto[]
  ConsoleUrl: string | null
  CreatedByUserName: string | null
  SyncedDate: IsoDateTime | null
  /** What can be planned now. */
  Operations: AsoExperimentOperation[]
}

export interface AsoExportFileDto {
  VariantKey: string
  Locale: string
  ClassKey: string
  AssetId: number
  FileName: string
  /** Presigned download. */
  Url: string
}

export type AsoExportTextField = 'promotionalText' | 'shortDescription' | 'fullDescription'

export interface AsoExportTextDto {
  VariantKey: string
  Locale: string
  Field: AsoExportTextField
  Value: string
}

export interface AsoExperimentExportDto {
  Files: AsoExportFileDto[]
  Texts: AsoExportTextDto[]
}

export interface SaveExperimentInput {
  Id?: number | null
  AppId: number
  StoreAppId: number
  Kind: AsoExperimentKind
  Name: string
  TrafficProportion?: number | null
  Variants: AsoExperimentVariantDto[]
  Note?: string | null
}

export interface PlanExperimentInput {
  Id: number
  Operation: AsoExperimentOperation
  WithVersion?: boolean
  WinnerVariantKey?: string | null
}

export interface RecordResultInput {
  Id: number
  Status?: AsoExperimentStatus | null
  StartDate?: IsoDateTime | null
  EndDate?: IsoDateTime | null
  WinnerVariantKey?: string | null
  ResultSummary?: string | null
  ResultUrl?: string | null
}
