import type { IsoDateTime } from '@/lib/api/types'
import type { ArtifactSessionStatus } from '@/features/intake/types'

// The Firefox Add-ons contract (storeasy-api FirefoxController, Storeasy.Application.Dtos.FirefoxDtos and the Firefox
// enums of Storeasy.Domain.Enums). Lists, never maps keyed by locale: dictionary keys lose their case (pascalizeKeys).

export const FirefoxChangeKind = { Listing: 1, Version: 2, Previews: 3, Icon: 4, ReleaseNotes: 5 } as const
export type FirefoxChangeKind = (typeof FirefoxChangeKind)[keyof typeof FirefoxChangeKind]

export const FirefoxAddonStatus = { None: 0, Public: 1, Nominated: 2, Incomplete: 3, Disabled: 4, Rejected: 5, Deleted: 6 } as const
export type FirefoxAddonStatus = (typeof FirefoxAddonStatus)[keyof typeof FirefoxAddonStatus]

export const FirefoxFileStatus = { None: 0, Unreviewed: 1, Public: 2, Disabled: 3 } as const
export type FirefoxFileStatus = (typeof FirefoxFileStatus)[keyof typeof FirefoxFileStatus]

export const FirefoxVersionChannel = { None: 0, Listed: 1, Unlisted: 2 } as const
export type FirefoxVersionChannel = (typeof FirefoxVersionChannel)[keyof typeof FirefoxVersionChannel]

/** The listing fields the API reads and writes. */
export const FIREFOX_FIELDS = ['Name', 'Summary', 'Description', 'Homepage', 'SupportUrl', 'SupportEmail'] as const
export type FirefoxField = (typeof FIREFOX_FIELDS)[number]

export interface FirefoxAddonAddedDto {
  AppId: number
  StoreAppId: number
  JobId: number
}

export interface FirefoxLocalizedText {
  Locale: string
  Value: string | null
}

export interface FirefoxListingValue {
  Field: string
  Locale: string
  Value: string | null
}

export interface FirefoxPreviewDto {
  Id: number
  ImageUrl: string | null
  ThumbnailUrl: string | null
  Position: number
  Caption: string | null
  Width: number | null
  Height: number | null
}

export interface FirefoxVersionDto {
  Id: number
  Version: string
  Channel: FirefoxVersionChannel
  FileStatus: FirefoxFileStatus
  IsDisabled: boolean
  ReleaseNotes: FirefoxLocalizedText[] | null
  License: string | null
  HasSource: boolean
  Created: IsoDateTime | null
  FileUrl: string | null
  DataCollectionPermissions: string[] | null
}

export interface FirefoxStateChange {
  Subject: string
  State: string
  Version: string | null
  Channel: FirefoxVersionChannel
  Date: IsoDateTime
}

export interface FirefoxPackageDto {
  SessionId: number
  FileName: string
  Size: number
  Version: string | null
  GeckoId: string | null
  Name: string | null
  ManifestVersion: number | null
  Status: ArtifactSessionStatus
  StartDate: IsoDateTime
  PackageDeleted: boolean
  Issues: string[] | null
  Blocked: boolean
}

export interface FirefoxAddonDto {
  StoreAppId: number
  AppId: number
  AppName: string | null
  ConnectionId: number | null
  AddonId: number
  Guid: string | null
  Slug: string | null
  Status: FirefoxAddonStatus
  IsDisabled: boolean
  DefaultLocale: string | null
  ListedVersion: string | null
  LatestVersion: string | null
  LatestChannel: FirefoxVersionChannel
  LatestFileStatus: FirefoxFileStatus
  Rating: number | null
  RatingCount: number | null
  AverageDailyUsers: number | null
  WeeklyDownloads: number | null
  Listing: FirefoxListingValue[] | null
  Categories: string[] | null
  Previews: FirefoxPreviewDto[] | null
  IconUrl: string | null
  Versions: FirefoxVersionDto[] | null
  ReadError: string | null
  StatusHistory: FirefoxStateChange[] | null
  SyncedDate: IsoDateTime | null
  SyncError: string | null
  StoreUrl: string | null
  DeveloperHubUrl: string | null
  Packages: FirefoxPackageDto[] | null
}

/** The body of a Firefox plan (PlanFirefoxChangeCommand), without the organization and record. */
export interface FirefoxChangeInput {
  Kind: FirefoxChangeKind
  Listing?: FirefoxListingValue[]
  Categories?: string[] | null
  SessionId?: number | null
  Channel?: FirefoxVersionChannel
  ReleaseNotes?: FirefoxLocalizedText[]
  ApprovalNotes?: string | null
  License?: string | null
  Compatibility?: string[]
  SourceAssetId?: number | null
  AddPreviewAssetIds?: number[]
  DeletePreviewIds?: number[]
  IconAssetId?: number | null
  Version?: string | null
}
