// Rejection cases contract (storeasy-api Storeasy.Application FeedbackDtos RejectionCaseDto, RejectionCaseListDto,
// RejectionHistoryDto, Storeasy.Domain enums and JsonModels). PascalCase keys, numeric enums, null properties left out.
import type { ActionChannel, IsoDateTime, StoreKind } from '@/lib/api/types'

export const RejectionKind = { Rejected: 1, MetadataRejected: 2, CertificationFailed: 3 } as const
export type RejectionKind = (typeof RejectionKind)[keyof typeof RejectionKind]

export const RejectionState = { Open: 1, Closed: 2 } as const
export type RejectionState = (typeof RejectionState)[keyof typeof RejectionState]

export const RejectionSource = { Webhook: 1, Sync: 2, Manual: 3 } as const
export type RejectionSource = (typeof RejectionSource)[keyof typeof RejectionSource]

export const RejectionCloseReason = { Submission: 1, Manual: 2 } as const
export type RejectionCloseReason = (typeof RejectionCloseReason)[keyof typeof RejectionCloseReason]

export const ChecklistItemStatus = { Todo: 1, InProgress: 2, Done: 3 } as const
export type ChecklistItemStatus = (typeof ChecklistItemStatus)[keyof typeof ChecklistItemStatus]

// RejectionCase.ReplyMaxLength: what Resolution Center accepts (line breaks count as one).
export const REJECTION_REPLY_MAX = 4000
// The assisted step that sends the reply in Resolution Center (rules catalog).
export const REPLY_STEP_KEY = 'apple.review.resolution-center-reply'

export interface RejectionGuideline {
  Code?: string
  Title?: string
}

export interface RejectionChecklistItem {
  Id?: string
  GuidelineCode?: string
  Text?: string
  FixRoute?: string
  AssigneeUserId?: number
  Status: ChecklistItemStatus
  DoneDate?: IsoDateTime
}

export interface RejectionTimelineEntry {
  Date: IsoDateTime
  Kind?: string
  Text?: string
  Channel: ActionChannel
}

export interface RejectionReplyDraftDto {
  Body?: string
  Length: number
  MaxLength: number
  OverLimit: boolean
  UpdatedDate?: IsoDateTime
  ByUserName?: string
  Channel?: ActionChannel
  SentDate?: IsoDateTime
  SentByUserName?: string
}

export interface RejectionCaseDto {
  Id: number
  AppId: number
  AppName?: string
  StoreAppId: number
  Store: StoreKind
  ExternalVersionId?: string
  VersionString?: string
  BuildNumber?: string
  Kind: RejectionKind
  State: RejectionState
  OpenedVia: RejectionSource
  OpenedDate: IsoDateTime
  ClosedDate?: IsoDateTime
  ClosedVia?: RejectionCloseReason
  CloseNote?: string
  ResolutionCenterUrl?: string
  RawText?: string
  RawTextByUserName?: string
  RawTextDate?: IsoDateTime
  Guidelines?: RejectionGuideline[]
  RepeatedGuidelines?: string[]
  Checklist?: RejectionChecklistItem[]
  ReplyStepId?: number
  ReplyDraft?: RejectionReplyDraftDto
  PreviousCaseId?: number
  Timeline?: RejectionTimelineEntry[]
}

export interface RejectionCaseListDto {
  Id: number
  AppId: number
  Store: StoreKind
  StoreAppId: number
  VersionString?: string
  BuildNumber?: string
  Kind: RejectionKind
  State: RejectionState
  Guidelines?: RejectionGuideline[]
  OpenedVia: RejectionSource
  OpenedDate: IsoDateTime
  ClosedDate?: IsoDateTime
  ChecklistDone: number
  ChecklistTotal: number
}

export interface SubmissionHistoryItemDto {
  StoreAppId: number
  VersionString?: string
  BuildNumber?: string
  State?: string
  SubmittedDate?: IsoDateTime
  Rejections?: RejectionCaseListDto[]
}

export interface RejectionHistoryDto {
  AppId: number
  Submissions?: SubmissionHistoryItemDto[]
  SubmissionCount: number
  RejectionCount: number
  RepeatedGuidelines?: string[]
}
