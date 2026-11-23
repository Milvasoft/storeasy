import type { ChangePlanStatus } from '@/features/common/types'
import type { ConnectionStatus, IsoDateTime } from '@/lib/api/types'

// Shapes of the package registries endpoints (storeasy-api RegistriesController, Storeasy.Application.Dtos.RegistryDtos),
// PascalCase as the API sends them.

// Storeasy.Domain.Enums.PackageRegistry.
export const PackageRegistry = { Npm: 1, NuGet: 2, PyPI: 3, Crates: 4, Maven: 5, RubyGems: 6, Packagist: 7, Pub: 8 } as const
export type PackageRegistry = (typeof PackageRegistry)[keyof typeof PackageRegistry]

// Storeasy.Domain.Enums.PackageAlertKind.
export const PackageAlertKind = { Deprecated: 1, Yanked: 2, NoProvenance: 3, Advisory: 4, PublisherMismatch: 5, TokenExpiring: 6 } as const
export type PackageAlertKind = (typeof PackageAlertKind)[keyof typeof PackageAlertKind]

// Storeasy.Domain.Enums.CiProvider.
export const CiProvider = { GitHubActions: 1, GitLabCi: 2 } as const
export type CiProvider = (typeof CiProvider)[keyof typeof CiProvider]

// Storeasy.Domain.Enums.PublisherCheckStatus.
export const PublisherCheckStatus = { NotConfigured: 1, NotVerifiable: 2, NoProvenance: 3, Match: 4, Mismatch: 5 } as const
export type PublisherCheckStatus = (typeof PublisherCheckStatus)[keyof typeof PublisherCheckStatus]

export interface TrackedPackageSummaryDto {
  Id: number
  Registry: PackageRegistry
  Name: string
  AppId?: number | null
  AppName?: string | null
  LatestVersion?: string | null
  LatestPublishedDate?: IsoDateTime | null
  LatestCiRunUrl?: string | null
  LatestWorkflow?: string | null
  SourceRepository?: string | null
  HasProvenance: boolean
  ProvenanceSupported: boolean
  DeprecationMessage?: string | null
  OpenAlertCount: number
  SyncedDate?: IsoDateTime | null
  SyncError?: string | null
  RegistryUrl?: string | null
  SetupProvider?: CiProvider | null
  SetupRepository?: string | null
  SetupWorkflow?: string | null
  PublisherCheck: PublisherCheckStatus
  TotalDownloads?: number | null
  RecentDownloads?: number | null
  RecentDownloadsDays?: number | null
  PackageGroupId?: number | null
  PackageGroupName?: string | null
}

export interface TrackedPackageVersionDto {
  Version: string
  PublishedDate?: IsoDateTime | null
  IsYanked: boolean
  IsUnlisted: boolean
  IsDeprecated: boolean
  Message?: string | null
  HasProvenance?: boolean | null
  CiRunUrl?: string | null
  Downloads?: number | null
}

export interface PackageAlertDto {
  Id: number
  TrackedPackageId?: number | null
  TokenReminderId?: number | null
  Registry: PackageRegistry
  PackageName: string
  Kind: PackageAlertKind
  Version?: string | null
  AdvisoryId?: string | null
  Severity?: string | null
  Summary?: string | null
  Url?: string | null
  OpenedDate: IsoDateTime
  ResolvedDate?: IsoDateTime | null
}

export interface TrackedPackageDto extends TrackedPackageSummaryDto {
  Versions?: TrackedPackageVersionDto[] | null
  Alerts?: PackageAlertDto[] | null
}

export interface SetupFieldDto {
  Key: string
  Value?: string | null
}

export interface PublisherCheckDto {
  Status: PublisherCheckStatus
  ExpectedRepository?: string | null
  ExpectedWorkflow?: string | null
  ObservedRepository?: string | null
  ObservedWorkflow?: string | null
  Version?: string | null
  CiRunUrl?: string | null
}

export interface TrustedPublishingDto {
  TrackedPackageId: number
  Registry: PackageRegistry
  PackageName: string
  Providers?: CiProvider[] | null
  SettingsUrl?: string | null
  Saved: boolean
  Provider?: CiProvider | null
  Repository?: string | null
  Workflow?: string | null
  Environment?: string | null
  RegistryUser?: string | null
  SetupDate?: IsoDateTime | null
  RegistryFields?: SetupFieldDto[] | null
  WorkflowPath?: string | null
  WorkflowText?: string | null
  Check?: PublisherCheckDto | null
}

export interface SetupInput {
  Provider: CiProvider | null
  Repository: string
  Workflow: string
  Environment: string
  RegistryUser: string
}

export interface TokenReminderDto {
  Id: number
  Registry: PackageRegistry
  Label: string
  UsedIn?: string | null
  ExpiryDate: IsoDateTime
  TrackedPackageId?: number | null
  PackageName?: string | null
  DaysLeft: number
  Stage: number
  TrustedPublishingAvailable: boolean
}

export interface TokenReminderInput {
  Id: number | null
  Registry: PackageRegistry
  Label: string
  UsedIn: string
  ExpiryDate: string
}

// Storeasy.Domain.Enums.DownloadScope.
export const DownloadScope = { Total: 1, LastWeek: 2 } as const
export type DownloadScope = (typeof DownloadScope)[keyof typeof DownloadScope]

// Storeasy.Domain.Enums.ListingCheckStatus.
export const ListingCheckStatus = { Ok: 1, Warning: 2, Missing: 3 } as const
export type ListingCheckStatus = (typeof ListingCheckStatus)[keyof typeof ListingCheckStatus]

// Storeasy.Domain.Enums.ReadmeFormat.
export const ReadmeFormat = { Markdown: 1, Html: 2, Text: 3 } as const
export type ReadmeFormat = (typeof ReadmeFormat)[keyof typeof ReadmeFormat]

export interface PackageDownloadsDto {
  TrackedPackageId: number
  Registry: PackageRegistry
  Available: boolean
  TotalDownloads?: number | null
  RecentDownloads?: number | null
  RecentDownloadsDays?: number | null
  VersionScope?: DownloadScope | null
  Likes?: number | null
  Points?: number | null
  MaxPoints?: number | null
  DownloadsSyncedDate?: IsoDateTime | null
  Series?: Array<{ Day: IsoDateTime; Downloads: number }> | null
  Versions?: Array<{ Version: string; Downloads: number }> | null
}

export interface PackageListingDto {
  TrackedPackageId: number
  Registry: PackageRegistry
  Available: boolean
  ReadError?: string | null
  Version?: string | null
  Description?: string | null
  Readme?: string | null
  ReadmeFormat?: ReadmeFormat | null
  Keywords?: string[] | null
  License?: string | null
  Homepage?: string | null
  Repository?: string | null
  Authors?: string[] | null
  IconUrl?: string | null
  Checks?: Array<{ Key: string; Status: ListingCheckStatus }> | null
  ShipsWithRelease: boolean
  RegistryUrl?: string | null
}

export interface PackageGroupMemberDto {
  TrackedPackageId: number
  Registry: PackageRegistry
  Name: string
  LatestVersion?: string | null
  LatestPublishedDate?: IsoDateTime | null
  Behind: boolean
  OpenAlertCount: number
}

export interface PackageGroupDto {
  Id: number
  Name: string
  HighestVersion?: string | null
  BehindCount: number
  LastReleaseDate?: IsoDateTime | null
  OpenAlertCount: number
  Members?: PackageGroupMemberDto[] | null
}

export interface PackageGroupSuggestionDto {
  Name: string
  Repository?: string | null
  TrackedPackageIds?: number[] | null
  PackageNames?: string[] | null
}

export interface TrackedPackageAddedDto {
  Id: number
  Name: string
  JobId: number
}

// Releases and registry operations (phase 4).

// Storeasy.Domain.Enums.RegistryConnectionKind: a GitHub token or a registry's key.
export const RegistryConnectionKind = { GitHub: 1, NuGet: 2, Npm: 3, Crates: 4, RubyGems: 5 } as const
export type RegistryConnectionKind = (typeof RegistryConnectionKind)[keyof typeof RegistryConnectionKind]

// Storeasy.Domain.Enums.ReleaseTrigger: what starts the repository's CI.
export const ReleaseTrigger = { VersionBump: 1, Tag: 2, VersionBumpAndTag: 3, Dispatch: 4 } as const
export type ReleaseTrigger = (typeof ReleaseTrigger)[keyof typeof ReleaseTrigger]

// Storeasy.Domain.Enums.VersionFileFormat.
export const VersionFileFormat = { MsBuild: 1, PackageJson: 2, Toml: 3, Ruby: 4, Pom: 5, PlainText: 6 } as const
export type VersionFileFormat = (typeof VersionFileFormat)[keyof typeof VersionFileFormat]

// Storeasy.Domain.Enums.RegistryPlanKind.
export const RegistryPlanKind = { Release: 1, Unlist: 2, Relist: 3, Deprecate: 4, Undeprecate: 5, DistTag: 6, Yank: 7, Unyank: 8 } as const
export type RegistryPlanKind = (typeof RegistryPlanKind)[keyof typeof RegistryPlanKind]

// Storeasy.Domain.Enums.ReleaseFollowStatus.
export const ReleaseFollowStatus = { Following: 1, Published: 2, Failed: 3, TimedOut: 4 } as const
export type ReleaseFollowStatus = (typeof ReleaseFollowStatus)[keyof typeof ReleaseFollowStatus]

export interface RegistryConnectionDto {
  Id: number
  Kind: RegistryConnectionKind
  Fingerprint: string
  Status: ConnectionStatus
  LastVerifiedDate?: IsoDateTime | null
  LastVerifyError?: string | null
  ExpiryDate?: IsoDateTime | null
}

export interface RegistryConnectionInput {
  Kind: RegistryConnectionKind
  Secret: string
  ExpiryDate: string
}

export interface VersionFileSetting {
  Path: string
  Format: VersionFileFormat
}

export interface ReleaseSettings {
  Repository: string
  Branch: string
  Trigger: ReleaseTrigger
  VersionFiles: VersionFileSetting[]
  TagPattern?: string | null
  Workflow?: string | null
  DispatchVersionInput?: string | null
  DispatchPrereleaseInput?: string | null
  DispatchTakesTag: boolean
}

export interface VersionSuggestionDto {
  Kind: string
  Version: string
}

export interface ReleaseSettingsDto {
  TrackedPackageId?: number | null
  PackageGroupId?: number | null
  PackageGroupName?: string | null
  FromGroup: boolean
  Settings?: ReleaseSettings | null
  Problems: string[]
  CurrentVersion?: string | null
  NextVersions: VersionSuggestionDto[]
  GitHubConnected: boolean
}

export interface ReleaseSuggestionDto {
  Settings: ReleaseSettings
  Reason: string
}

export interface ReleaseFileChange {
  Path: string
  BaseSha: string
  OldVersion: string
  Before: string
  After: string
  Line: number
}

export interface RegistryPlanDto {
  Id: number
  TrackedPackageId?: number | null
  PackageGroupId?: number | null
  Kind: RegistryPlanKind
  Status: ChangePlanStatus
  Title: string
  Version: string
  CurrentVersion?: string | null
  Tag?: string | null
  ConfirmText: string
  RequiredPermission: number
  Release?: ReleaseSettings | null
  Files: ReleaseFileChange[]
  PackageIds: number[]
  DispatchInputs: Record<string, string>
  BaseSha?: string | null
  Argument?: string | null
  ExpiryDate: IsoDateTime
  CreatedByUserName?: string | null
  CreationDate?: IsoDateTime | null
  DecidedByUserName?: string | null
  DecisionDate?: IsoDateTime | null
  StoreJobId?: number | null
  CommitSha?: string | null
  RunId?: number | null
  RunUrl?: string | null
  RunStatus?: string | null
  RunConclusion?: string | null
  FollowStatus?: ReleaseFollowStatus | null
  StartedDate?: IsoDateTime | null
  FinishedDate?: IsoDateTime | null
  Error?: string | null
}

// The package or the group a release belongs to.
export interface ReleaseScope {
  TrackedPackageId?: number
  PackageGroupId?: number
}
