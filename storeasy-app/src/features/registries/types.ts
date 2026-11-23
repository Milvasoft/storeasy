import type { IsoDateTime } from '@/lib/api/types'

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

export interface TrackedPackageAddedDto {
  Id: number
  Name: string
  JobId: number
}
