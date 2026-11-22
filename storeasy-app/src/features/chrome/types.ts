import type { IsoDateTime as DateTime } from '@/lib/api/types'
import type { ArtifactSessionStatus } from '@/features/intake/types'

// The Chrome Web Store DTOs (storeasy-api Application/Dtos/ChromeDtos). Enum values are the API's numbers.

export const ChromeItemState = {
  None: 0,
  PendingReview: 1,
  Staged: 2,
  Published: 3,
  PublishedToTesters: 4,
  Rejected: 5,
  Cancelled: 6,
} as const
export type ChromeItemState = (typeof ChromeItemState)[keyof typeof ChromeItemState]

export const ChromeUploadState = { None: 0, Succeeded: 1, InProgress: 2, Failed: 3, Missing: 4 } as const
export type ChromeUploadState = (typeof ChromeUploadState)[keyof typeof ChromeUploadState]

export const ChromeChangeKind = { Publish: 1, DeployPercentage: 2, CancelSubmission: 3, Rollback: 4, PublishStaged: 5 } as const
export type ChromeChangeKind = (typeof ChromeChangeKind)[keyof typeof ChromeChangeKind]

export interface ChromeItemAddedDto {
  AppId: number
  StoreAppId: number
  JobId: number
}

export interface ChromeStateChange {
  Revision: 'published' | 'submitted' | string
  State: ChromeItemState
  Version?: string | null
  Date: DateTime
}

export interface ChromePackageDto {
  SessionId: number
  FileName: string
  Size: number
  Version?: string | null
  Name?: string | null
  ManifestVersion?: number | null
  Status: ArtifactSessionStatus
  StartDate: DateTime
  PackageDeleted: boolean
  Uploaded: boolean
  Issues?: string[] | null
  Blocked: boolean
}

export interface ChromeItemDto {
  StoreAppId: number
  AppId: number
  AppName?: string | null
  ItemId: string
  ConnectionId?: number | null
  PublisherId?: string | null
  PublishedState: ChromeItemState
  PublishedVersion?: string | null
  PublishedDeployPercentage?: number | null
  SubmittedState: ChromeItemState
  SubmittedVersion?: string | null
  SubmittedDeployPercentage?: number | null
  LastUploadState: ChromeUploadState
  UploadedVersion?: string | null
  Warned: boolean
  TakenDown: boolean
  Rating?: number | null
  RatingCount?: number | null
  WeeklyUsers?: number | null
  StatusHistory?: ChromeStateChange[] | null
  SyncedDate?: DateTime | null
  SyncError?: string | null
  StoreUrl: string
  DashboardUrl: string
  Packages?: ChromePackageDto[] | null
}

/** The body of a Chrome plan (PlanChromeChangeCommand without the ids). */
export interface ChromeChangeInput {
  Kind: ChromeChangeKind
  SessionId?: number | null
  Staged?: boolean
  DeployPercentage?: number | null
  SkipReview?: boolean
  TargetVersion?: string | null
  NewVersion?: string | null
  Reason?: string | null
}
