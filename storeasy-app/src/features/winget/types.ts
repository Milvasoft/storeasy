import type { IsoDateTime } from '@/lib/api/types'

// Shapes of the winget endpoints (storeasy-api WingetController, Storeasy.Application.Dtos.WingetDtos), PascalCase as the
// API sends them.

// Storeasy.Domain.Enums.WingetPullRequestState: the review state of a winget-pkgs pull request.
export const WingetPullRequestState = {
  Validating: 1,
  ValidationFailed: 2,
  NeedsAuthorFeedback: 3,
  ValidationCompleted: 4,
  Approved: 5,
  Merged: 6,
  Closed: 7,
  Draft: 8,
} as const
export type WingetPullRequestState = (typeof WingetPullRequestState)[keyof typeof WingetPullRequestState]

// Storeasy.Domain.Enums.WingetChangeKind.
export const WingetChangeKind = { NewVersion: 1, Listing: 2 } as const
export type WingetChangeKind = (typeof WingetChangeKind)[keyof typeof WingetChangeKind]

export interface WingetCheck {
  Name?: string | null
  Status?: string | null
  Conclusion?: string | null
  Url?: string | null
}

export interface WingetComment {
  Author?: string | null
  IsBot: boolean
  Excerpt?: string | null
  Url?: string | null
  Date: IsoDateTime
}

export interface WingetStateChange {
  State: WingetPullRequestState
  Date: IsoDateTime
}

export interface WingetPullRequestDto {
  Id: number
  StoreAppId: number
  Number: number
  Url?: string | null
  Title?: string | null
  PackageVersion?: string | null
  ChangeKind?: WingetChangeKind | null
  State: WingetPullRequestState
  IsOpen: boolean
  NeedsAction: boolean
  Labels?: string[] | null
  Checks?: WingetCheck[] | null
  Comments?: WingetComment[] | null
  StatusHistory?: WingetStateChange[] | null
  CreatedByStoreasy: boolean
  PlanId?: number | null
  OpenedDate?: IsoDateTime | null
  MergedDate?: IsoDateTime | null
  ClosedDate?: IsoDateTime | null
  SyncedDate?: IsoDateTime | null
}

export interface WingetInstallerSourceDto {
  Url: string
  Type: string
  Architectures: string[]
  SilentSwitches?: string | null
  Locales: string[]
  SignatureStatus: number
}

export interface WingetPackageDto {
  StoreAppId: number
  AppId: number
  AppName: string
  ConnectionId?: number | null
  Fork?: string | null
  PackageIdentifier: string
  IsPublished: boolean
  LatestVersion?: string | null
  Versions?: string[] | null
  Publisher?: string | null
  PackageName?: string | null
  DefaultLocale?: string | null
  Locales?: string[] | null
  ManifestVersion?: string | null
  WrittenManifestVersion: string
  SyncedDate?: IsoDateTime | null
  SyncError?: string | null
  PackagePageUrl: string
  PullRequests?: WingetPullRequestDto[] | null
  InstallerSources?: WingetInstallerSourceDto[] | null
}

export interface WingetInstallerDto {
  Architecture?: string | null
  Type?: string | null
  Scope?: string | null
  Url?: string | null
  Sha256?: string | null
  SilentSwitches?: string | null
  SilentWithProgressSwitches?: string | null
  ProductCode?: string | null
  Locale?: string | null
}

export interface WingetIssueDto {
  Key: string
  Message: string
  Field?: string | null
  Locale?: string | null
  Blocking: boolean
}

export interface WingetLocaleTextsDto {
  Locale: string
  IsDefault: boolean
  Fields?: Record<string, string> | null
  Tags?: string[] | null
}

export interface WingetManifestDto {
  StoreAppId: number
  PackageIdentifier: string
  Version?: string | null
  Versions?: string[] | null
  DefaultLocale?: string | null
  ManifestVersion?: string | null
  // A list, not a record keyed by locale: response keys are PascalCased (lib/api/pascalize.ts), locale tags must stay as sent.
  Locales?: WingetLocaleTextsDto[] | null
  Installers?: WingetInstallerDto[] | null
  Files?: Array<{ FileName: string; Content: string }> | null
  Issues?: WingetIssueDto[] | null
}

export interface WingetPackageAddedDto {
  AppId: number
  StoreAppId: number
  JobId: number
}

export interface WingetInstallerInputDto {
  Url: string
  Architecture: string
  Type?: string | null
  Scope?: string | null
  Sha256?: string | null
  SilentSwitches?: string | null
  SilentWithProgressSwitches?: string | null
  ProductCode?: string | null
  Locale?: string | null
}

export interface WingetLocaleInputDto {
  Locale: string
  Fields: Record<string, string>
  Tags?: string[] | null
}

export interface WingetChangeInput {
  Kind: WingetChangeKind
  Version?: string | null
  Installers?: WingetInstallerInputDto[]
  FromMicrosoftStore?: boolean
  Locales?: WingetLocaleInputDto[]
}
