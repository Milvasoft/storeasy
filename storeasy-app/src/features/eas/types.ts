import type { ConnectionStatus, IsoDateTime } from '@/lib/api/types'

// Shapes of the EAS endpoints (storeasy-api EasController, Storeasy.Application.Dtos.EasDtos), PascalCase as the API sends
// them.

// Storeasy.Domain.Enums.EasPlatform.
export const EasPlatform = { Ios: 1, Android: 2, All: 3 } as const
export type EasPlatform = (typeof EasPlatform)[keyof typeof EasPlatform]

// Storeasy.Domain.Enums.EasBuildStatus.
export const EasBuildStatus = {
  Requested: 1,
  New: 2,
  InQueue: 3,
  InProgress: 4,
  PendingCancel: 5,
  Finished: 6,
  Errored: 7,
  Canceled: 8,
  NotStarted: 9,
} as const
export type EasBuildStatus = (typeof EasBuildStatus)[keyof typeof EasBuildStatus]

export interface ExpoConnectionDto {
  Id: number
  Fingerprint: string
  Status: ConnectionStatus
  LastVerifiedDate?: IsoDateTime | null
  LastVerifyError?: string | null
}

export interface EasProjectDto {
  Id: string
  AccountName: string
  Slug: string
  Name: string
  GitHubRepository?: string | null
  AppIdentifier?: string | null
  Match: number
  LinkedAppId?: number | null
}

export interface EasProjectLinkDto {
  ProjectId: string
  AccountName: string
  Slug: string
  GitHubRepository?: string | null
  DefaultBranch?: string | null
  ProjectUrl: string
  GitHubSettingsUrl: string
  SyncedDate?: IsoDateTime | null
  SyncError?: string | null
  Chain: EasChainDto
}

export interface EasBuildDto {
  Id: number
  AppId: number
  BuildId?: string | null
  Platform: EasPlatform
  Status: EasBuildStatus
  Profile?: string | null
  GitRef?: string | null
  GitCommitHash?: string | null
  AppVersion?: string | null
  AppBuildVersion?: string | null
  Distribution?: string | null
  StartedBy?: string | null
  StartedInStoreasy: boolean
  PageUrl?: string | null
  LogsUrl?: string | null
  ArtifactUrl?: string | null
  StartedDate?: IsoDateTime | null
  FinishedDate?: IsoDateTime | null
  ErrorCode?: string | null
  ErrorMessage?: string | null
  ArtifactSessionId?: number | null
  AfterBuild: EasAfterBuild
  PrepareReviewPlan: boolean
  SubmitTrack?: string | null
  ReviewPlanIds: number[]
  ChainError?: string | null
}

export interface EasAppDto {
  AppId: number
  AppName: string
  Connected: boolean
  Link?: EasProjectLinkDto | null
  Builds: EasBuildDto[]
  Submissions: EasSubmissionDto[]
}

export interface EasProfilesDto {
  Profiles: string[]
  FromRepository: boolean
  Note?: string | null
}

export interface EasStartInput {
  Platform: EasPlatform
  Profile: string
  GitRef: string
  Chain?: EasChainDto | null
  Confirm: string
}

// Storeasy.Domain.Enums.EasAfterBuild.
export const EasAfterBuild = { None: 1, Intake: 2, Submit: 3 } as const
export type EasAfterBuild = (typeof EasAfterBuild)[keyof typeof EasAfterBuild]

// Storeasy.Domain.Enums.EasSubmissionStatus.
export const EasSubmissionStatus = { AwaitingBuild: 1, InQueue: 2, InProgress: 3, Finished: 4, Errored: 5, Canceled: 6 } as const
export type EasSubmissionStatus = (typeof EasSubmissionStatus)[keyof typeof EasSubmissionStatus]

export interface EasChainDto {
  AfterBuild: EasAfterBuild
  PrepareReviewPlan: boolean
  SubmitTrack?: string | null
  SubmitReleaseStatus?: string | null
}

export interface EasSubmissionDto {
  Id: number
  AppId: number
  EasBuildId: number
  SubmissionId: string
  Platform: EasPlatform
  Status: EasSubmissionStatus
  Track?: string | null
  ReleaseStatus?: string | null
  StartedBy?: string | null
  FromChain: boolean
  StartedDate?: IsoDateTime | null
  FinishedDate?: IsoDateTime | null
  LogsUrl?: string | null
  ErrorCode?: string | null
  ErrorMessage?: string | null
}

export interface EasSubmitInput {
  Track?: string | null
  ReleaseStatus?: string | null
  Confirm: string
}
