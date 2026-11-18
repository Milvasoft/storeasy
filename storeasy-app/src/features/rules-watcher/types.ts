// Rule watcher contract (storeasy-api Storeasy.Application RuleDtos RuleSourceDto, RuleWatchResultDto,
// RuleProposalListDto, RuleProposalDetailDto, RuleProposalDecisionDto, IRuleImpactFinder RuleImpact and the
// Storeasy.Domain enums). Platform administrators only (Manager user type, RuleCatalogManagement permissions).
import type { IsoDateTime, StoreKind } from '@/lib/api/types'

export const RuleSourceStatus = { New: 1, Unchanged: 2, Changed: 3, Unreachable: 4 } as const
export type RuleSourceStatus = (typeof RuleSourceStatus)[keyof typeof RuleSourceStatus]

export const RuleProposalStatus = { Pending: 1, Approved: 2, Rejected: 3 } as const
export type RuleProposalStatus = (typeof RuleProposalStatus)[keyof typeof RuleProposalStatus]

export const RuleProposalPreparer = { Watcher: 1, Mcp: 2 } as const
export type RuleProposalPreparer = (typeof RuleProposalPreparer)[keyof typeof RuleProposalPreparer]

export const RuleProposalRejectReason = { FormattingOnly: 1, Misread: 2, SourceUnstable: 3, Other: 4 } as const
export type RuleProposalRejectReason = (typeof RuleProposalRejectReason)[keyof typeof RuleProposalRejectReason]

// The note of a decision (design 17.8: at most 1000 characters).
export const DECISION_NOTE_MAX = 1000

export interface RuleSourceDto {
  Id: number
  Name?: string
  Store: StoreKind
  Url?: string
  IsActive: boolean
  Status: RuleSourceStatus
  LastFetchDate?: IsoDateTime
  LastChangeDate?: IsoDateTime
  ContentHash?: string
  PreviousContentHash?: string
  LastError?: string
}

export interface RuleWatchResultDto {
  Checked: number
  Changed: number
  Unreachable: number
  ProposalsOpened: number
}

export interface RuleProposalListDto {
  Id: number
  Title?: string
  Store: StoreKind
  RuleIds?: string[]
  RuleSourceId?: number
  Status: RuleProposalStatus
  PreparedBy: RuleProposalPreparer
  PreparedByUserName?: string
  CatalogVersionFrom?: string
  CatalogVersionTo?: string
  CreationDate: IsoDateTime
}

export interface RuleImpact {
  OrganizationId: number
  AppId: number
  AppName?: string
  RuleId?: string
  Message?: string
}

export interface RuleProposalDetailDto extends RuleProposalListDto {
  SourceName?: string
  SourceUrl?: string
  ContentHash?: string
  PreviousContentHash?: string
  SourceExcerptBefore?: string
  SourceExcerptAfter?: string
  CatalogPatch?: string
  PreparerNote?: string
  RejectReason?: RuleProposalRejectReason
  DecisionNote?: string
  DecidedByUserName?: string
  DecidedDate?: IsoDateTime
  NotifiedOrganizationCount: number
  Impact?: RuleImpact[]
}

export interface RuleProposalDecisionDto {
  CatalogVersion?: string
  NotifiedOrganizationCount: number
  NotifiedUserCount: number
}
