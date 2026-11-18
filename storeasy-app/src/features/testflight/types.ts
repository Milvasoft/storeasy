// TestFlight contract (storeasy-api Storeasy.Application TestFlightDtos BetaBuildDto, BetaGroupDto, BetaTesterDto,
// BetaLocalizationDto and Storeasy.Domain enums). PascalCase keys, numeric enums, null properties left out.
import type { BuildProcessingState, IsoDateTime } from '@/lib/api/types'
import type { LocalizedText } from '@/features/reviews/types'

export const BetaReviewState = { NotSubmitted: 1, WaitingForReview: 2, InReview: 3, Approved: 4, Rejected: 5 } as const
export type BetaReviewState = (typeof BetaReviewState)[keyof typeof BetaReviewState]

export const BetaInviteType = { Email: 1, PublicLink: 2 } as const
export type BetaInviteType = (typeof BetaInviteType)[keyof typeof BetaInviteType]

export const BetaTesterState = { NotInvited: 1, Invited: 2, Accepted: 3, Installed: 4, Revoked: 5 } as const
export type BetaTesterState = (typeof BetaTesterState)[keyof typeof BetaTesterState]

// BetaBuild.WhatToTestMaxLength.
export const WHAT_TO_TEST_MAX = 4000
// A build warns 14 days before its 90 day expiry, louder in the last 7 (design 8.16).
export const EXPIRY_WARN_DAYS = 14
export const EXPIRY_URGENT_DAYS = 7

export interface BetaBuildDto {
  BuildId: number
  VersionString?: string
  BuildNumber?: string
  ProcessingState: BuildProcessingState
  UploadedDate?: IsoDateTime
  ExpirationDate?: IsoDateTime
  Expired: boolean
  DaysLeft?: number
  BetaReviewState: BetaReviewState
  GroupIds?: number[]
  WhatToTest?: LocalizedText[]
}

export interface BetaGroupDto {
  Id: number
  Name?: string
  IsInternal: boolean
  TesterCount: number
  PublicLinkEnabled: boolean
  PublicLink?: string
  PublicLinkLimit?: number
  PublicLinkLimitEnabled: boolean
  BuildIds?: number[]
  SyncedDate: IsoDateTime
}

export interface BetaTesterDto {
  Id: number
  FirstName?: string
  LastName?: string
  Email?: string
  InviteType: BetaInviteType
  State: BetaTesterState
  GroupExternalIds?: string[]
  GroupKeys?: string
}

export interface BetaLocalizationDto {
  Id: number
  Locale?: string
  Description?: string
  FeedbackEmail?: string
  MarketingUrl?: string
  PrivacyPolicyUrl?: string
  SyncedDate: IsoDateTime
}

export interface BetaTesterInvite {
  Email: string
  FirstName: string | null
  LastName: string | null
}

export interface BetaGroupInput {
  Name: string
  IsInternal: boolean
  PublicLinkEnabled: boolean
  PublicLinkLimit: number | null
  PublicLinkLimitEnabled: boolean
}

export interface BetaLocalizationInput {
  Locale: string
  Description: string | null
  FeedbackEmail: string | null
  MarketingUrl: string | null
  PrivacyPolicyUrl: string | null
}
