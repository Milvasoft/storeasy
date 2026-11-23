import type { IsoDateTime } from '@/lib/api/types'

// Shapes of the package registries endpoints (storeasy-api RegistriesController, Storeasy.Application.Dtos.RegistryDtos),
// PascalCase as the API sends them.

// Storeasy.Domain.Enums.PackageRegistry.
export const PackageRegistry = { Npm: 1, NuGet: 2, PyPI: 3, Crates: 4 } as const
export type PackageRegistry = (typeof PackageRegistry)[keyof typeof PackageRegistry]

// Storeasy.Domain.Enums.PackageAlertKind.
export const PackageAlertKind = { Deprecated: 1, Yanked: 2, NoProvenance: 3, Advisory: 4 } as const
export type PackageAlertKind = (typeof PackageAlertKind)[keyof typeof PackageAlertKind]

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
  TrackedPackageId: number
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

export interface TrackedPackageAddedDto {
  Id: number
  Name: string
  JobId: number
}
