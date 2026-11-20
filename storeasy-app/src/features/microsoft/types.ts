import type { IsoDateTime, OrgPermission } from '@/lib/api/types'
import type { AssistedStepStatus } from '@/features/common/types'

// The Microsoft Store contract (storeasy-api Storeasy.Application Dtos/MsStoreDtos, Domain Enums Ms*), field for field.
// Enums travel as numbers, like every enum of the API.

// ---- Enums -------------------------------------------------------------------------------------------------------------
export const MsProductKind = { Msix: 1, MsiExe: 2 } as const
export type MsProductKind = (typeof MsProductKind)[keyof typeof MsProductKind]

export const MsSubmissionTarget = { App: 1, Flight: 2, Addon: 3, Installer: 4 } as const
export type MsSubmissionTarget = (typeof MsSubmissionTarget)[keyof typeof MsSubmissionTarget]

export const MsSubmissionStatus = {
  PendingCommit: 1,
  CommitStarted: 2,
  CommitFailed: 3,
  PreProcessing: 4,
  PreProcessingFailed: 5,
  Certification: 6,
  CertificationFailed: 7,
  Release: 8,
  ReleaseFailed: 9,
  PendingPublication: 10,
  Publishing: 11,
  Published: 12,
  PublishFailed: 13,
  Canceled: 14,
  InProgress: 15,
  Failed: 16,
  Unknown: 17,
} as const
export type MsSubmissionStatus = (typeof MsSubmissionStatus)[keyof typeof MsSubmissionStatus]

export const MsAssistedReason = { StoreManagedConsumable: 1, MandatoryUpdate: 2, PricingV2: 3, ManualEdit: 4 } as const
export type MsAssistedReason = (typeof MsAssistedReason)[keyof typeof MsAssistedReason]

export const MsChangeKind = {
  Listings: 1,
  Images: 2,
  Packages: 3,
  Pricing: 4,
  InstallerPackages: 5,
  Rollout: 6,
  CreateFlight: 7,
  CreateAddon: 8,
  AddonContent: 9,
  DiscardSubmission: 10,
} as const
export type MsChangeKind = (typeof MsChangeKind)[keyof typeof MsChangeKind]

export const MsAddonType = { Durable: 1, Consumable: 2, Subscription: 3, Unknown: 4 } as const
export type MsAddonType = (typeof MsAddonType)[keyof typeof MsAddonType]

export const MsInstallerType = { Exe: 1, Msi: 2 } as const
export type MsInstallerType = (typeof MsInstallerType)[keyof typeof MsInstallerType]

export const MsSignatureStatus = { Unchecked: 1, Signed: 2, Unsigned: 3, Unreachable: 4 } as const
export type MsSignatureStatus = (typeof MsSignatureStatus)[keyof typeof MsSignatureStatus]

export const MsRolloutStatus = { NotStarted: 1, InProgress: 2, Complete: 3, Stopped: 4 } as const
export type MsRolloutStatus = (typeof MsRolloutStatus)[keyof typeof MsRolloutStatus]

export const MsPublishMode = { Immediate: 1, Manual: 2, SpecificDate: 3 } as const
export type MsPublishMode = (typeof MsPublishMode)[keyof typeof MsPublishMode]

export const MsRolloutAction = { SetPercentage: 1, Halt: 2, Complete: 3 } as const
export type MsRolloutAction = (typeof MsRolloutAction)[keyof typeof MsRolloutAction]

// ---- Products and submissions ------------------------------------------------------------------------------------------
export interface MsStatusChange {
  Status: MsSubmissionStatus
  Date: IsoDateTime
}

export interface MsStatusItem {
  Code?: string | null
  Details?: string | null
}

export interface MsCertificationReport {
  Date?: IsoDateTime | null
  ReportUrl?: string | null
}

export interface MsStatusDetails {
  Errors?: MsStatusItem[] | null
  Warnings?: MsStatusItem[] | null
  CertificationReports?: MsCertificationReport[] | null
}

export interface MsSubmissionDto {
  Id: number
  StoreAppId: number
  Target: MsSubmissionTarget
  SubmissionId?: string | null
  FlightId?: string | null
  AddonStoreId?: string | null
  FriendlyName?: string | null
  PackageVersion?: string | null
  Status: MsSubmissionStatus
  StatusHistory?: MsStatusChange[] | null
  StatusDetails?: MsStatusDetails | null
  IsPending: boolean
  CreatedByStoreasy: boolean
  ModifiedOutsideApi: boolean
  RolloutPercentage?: number | null
  RolloutStatus?: MsRolloutStatus | null
  /** When the submission publishes after certification; null when the store did not report it. */
  PublishMode?: MsPublishMode | null
  /** Publish date of a SpecificDate submission. */
  PublishDate?: IsoDateTime | null
  /** Passed certification and waits for "Publish now" in Partner Center (computed by the API). */
  AwaitsManualPublish: boolean
  SyncedDate?: IsoDateTime | null
}

export interface MsAssistedStepDto {
  Key: string
  Title?: string | null
  Reason?: MsAssistedReason | null
  Url?: string | null
  Pending: boolean
  StepId?: number | null
  Status?: AssistedStepStatus | null
}

export interface MsProductDto {
  StoreAppId: number
  AppId: number
  Name?: string | null
  ProductId?: string | null
  PackageFamilyName?: string | null
  Kind: MsProductKind
  PricingV2: boolean
  FirstSubmissionDone: boolean
  HasMandatoryUpdate: boolean
  IsReady: boolean
  LastPublishedSubmissionId?: string | null
  PendingSubmissionId?: string | null
  SyncedDate?: IsoDateTime | null
  ActiveSubmission?: MsSubmissionDto | null
  Notices?: string[] | null
  AssistedSteps?: MsAssistedStepDto[] | null
}

export interface MsProductAddedDto {
  AppId: number
  StoreAppId: number
  JobId: number
}

// ---- Content -----------------------------------------------------------------------------------------------------------
export interface MsListingDto {
  Locale: string
  Title?: string | null
  Description?: string | null
  ShortDescription?: string | null
  WhatsNew?: string | null
  Features?: string[] | null
  SearchTerms?: string[] | null
}

export interface MsImageDto {
  Locale?: string | null
  Id?: string | null
  FileName?: string | null
  ImageType?: string | null
  Description?: string | null
}

export interface MsPackageDto {
  Id?: string | null
  FileName?: string | null
  Version?: string | null
  Architecture?: string | null
}

export interface MsPricingDto {
  PriceId?: string | null
  TrialPeriod?: string | null
  Visibility?: string | null
  Markets?: string[] | null
  Discoverability?: string | null
  PricingModel?: string | null
  FreeTrial?: string | null
}

export interface MsRolloutDto {
  IsPackageRollout: boolean
  Percentage: number
  Status?: MsRolloutStatus | null
  FallbackSubmissionId?: string | null
}

export interface MsInstallerPackageInputDto {
  PackageUrl: string
  PackageType: MsInstallerType
  Architecture?: string | null
  Languages: string[]
  IsSilentInstall: boolean
  InstallerParameters?: string | null
  GenericDocUrl?: string | null
}

export interface MsAddonListingDto {
  Locale: string
  Title?: string | null
  Description?: string | null
}

export interface MsContentDto {
  Kind: MsProductKind
  Target: MsSubmissionTarget
  ProductId?: string | null
  SubmissionId?: string | null
  SubmissionIsPending: boolean
  SubmissionStatus?: MsSubmissionStatus | null
  Listings?: MsListingDto[] | null
  Images?: MsImageDto[] | null
  Packages?: MsPackageDto[] | null
  Pricing?: MsPricingDto | null
  Rollout?: MsRolloutDto | null
  InstallerPackages?: MsInstallerPackageInputDto[] | null
  AddonListings?: MsAddonListingDto[] | null
  AddonLifetime?: string | null
  PricingV2: boolean
  MandatoryUpdate: boolean
  ContentHash?: string | null
}

// ---- Flights, add-ons, installers --------------------------------------------------------------------------------------
export interface MsFlightGroupDto {
  Id: string
  Name?: string | null
  MemberCount?: number | null
}

export interface MsFlightDto {
  StoreAppId: number
  FlightId: string
  FriendlyName?: string | null
  Rank?: number | null
  GroupIds?: string[] | null
  Groups?: MsFlightGroupDto[] | null
  LastPublishedSubmissionId?: string | null
  PendingSubmissionId?: string | null
  SyncedDate?: IsoDateTime | null
}

export interface MsAddonDto {
  StoreAppId: number
  StoreId: string
  ProductId?: string | null
  Title?: string | null
  Type: MsAddonType
  Lifetime?: string | null
  PriceId?: string | null
  LastPublishedSubmissionId?: string | null
  PendingSubmissionId?: string | null
  ApiWritable: boolean
  AssistedReason?: MsAssistedReason | null
  SyncedDate?: IsoDateTime | null
}

export interface MsInstallerPackageDto {
  Id: number
  StoreAppId: number
  PackageId?: string | null
  PackageUrl: string
  PackageType: MsInstallerType
  Architectures?: string[] | null
  Languages?: string[] | null
  InstallerParameters?: string | null
  IsSilentInstall: boolean
  GenericDocUrl?: string | null
  SignatureStatus: MsSignatureStatus
  SignatureNote?: string | null
  CheckedDate?: IsoDateTime | null
}

export interface MsExpiringConnectionDto {
  ConnectionId: number
  OrganizationId: number
  Name: string
  CredentialExpiryDate: IsoDateTime
  DaysLeft: number
}

// ---- Change input and plan ---------------------------------------------------------------------------------------------
export interface MsImageInputDto {
  AssetId: number
  ImageType: string
  Description?: string | null
}

export interface MsImageChangeDto {
  Locale: string
  Add: MsImageInputDto[]
  RemoveFileNames: string[]
}

export interface MsPackageChangeDto {
  AddAssetIds: number[]
  RemoveFileNames: string[]
  RolloutPercentage?: number | null
}

export interface MsRolloutChangeDto {
  SubmissionId: string
  Action: MsRolloutAction
  Percentage?: number | null
}

export interface MsFlightInputDto {
  FriendlyName: string
  GroupIds: string[]
  RankHigherThan?: string | null
}

export interface MsAddonInputDto {
  ProductId: string
  ProductType: MsAddonType
}

export interface MsAddonContentDto {
  Listings: MsAddonListingDto[]
  PriceId?: string | null
  Lifetime?: string | null
}

/** One change; Kind says which section the API reads, the others are left out. */
export interface MsChangeInputDto {
  Kind: MsChangeKind
  FlightId?: string | null
  AddonStoreId?: string | null
  Listings?: MsListingDto[]
  Images?: MsImageChangeDto | null
  Packages?: MsPackageChangeDto | null
  Pricing?: MsPricingDto | null
  InstallerPackages?: MsInstallerPackageInputDto[]
  Rollout?: MsRolloutChangeDto | null
  Flight?: MsFlightInputDto | null
  Addon?: MsAddonInputDto | null
  AddonContent?: MsAddonContentDto | null
}

export interface MsPlanItemDto {
  Group?: string | null
  Locale?: string | null
  Field?: string | null
  Before?: string | null
  After?: string | null
  Action?: string | null
}

export interface MsPlanWarningDto {
  Key: string
  Message?: string | null
  Field?: string | null
  Locale?: string | null
}

export interface MsPlanCallDto {
  Method?: string | null
  Path?: string | null
  Target?: string | null
}

export interface MsChangePlanDto {
  StoreAppId: number
  Kind: MsChangeKind
  Target: MsSubmissionTarget
  SubmissionId?: string | null
  CreatesSubmission: boolean
  Items?: MsPlanItemDto[] | null
  Warnings?: MsPlanWarningDto[] | null
  Calls?: MsPlanCallDto[] | null
  BaseHash?: string | null
  RequiredPermission: OrgPermission
  Blocked: boolean
  BlockKey?: string | null
  AssistedReason?: MsAssistedReason | null
  DryRun: boolean
  JobId?: number | null
  PlanId?: number | null
}
