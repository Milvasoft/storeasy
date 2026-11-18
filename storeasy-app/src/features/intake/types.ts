// Package intake contract (storeasy-api Storeasy.Application IntakeDtos, the Artifacts, CiTokens, TrustedPublishers,
// DeliveryRules, ArtifactSources and IntakeSettings commands, and the Storeasy.Domain.Intake enums and JSON models).
import type { IsoDateTime, StoreKind } from '@/lib/api/types'

export const ArtifactPlatform = { Ios: 1, Android: 2, Windows: 3 } as const
export type ArtifactPlatform = (typeof ArtifactPlatform)[keyof typeof ArtifactPlatform]

export const ArtifactSource = {
  GitHubActions: 1,
  AzureDevOps: 2,
  GitLab: 3,
  Cli: 4,
  Rest: 5,
  Eas: 6,
  Webhook: 7,
  Manual: 8,
  Sync: 9,
} as const
export type ArtifactSource = (typeof ArtifactSource)[keyof typeof ArtifactSource]

export const ArtifactSessionStatus = {
  Uploading: 1,
  Parsing: 2,
  Checking: 3,
  Delivering: 4,
  Delivered: 5,
  Rejected: 6,
  Duplicate: 7,
  Held: 8,
  Failed: 9,
} as const
export type ArtifactSessionStatus = (typeof ArtifactSessionStatus)[keyof typeof ArtifactSessionStatus]

export const ArtifactStepKind = { Upload: 1, Parse: 2, Check: 3, Rule: 4, Deliver: 5, Report: 6 } as const
export type ArtifactStepKind = (typeof ArtifactStepKind)[keyof typeof ArtifactStepKind]

export const ArtifactStepStatus = { Pending: 1, Running: 2, Done: 3, Failed: 4, Skipped: 5 } as const
export type ArtifactStepStatus = (typeof ArtifactStepStatus)[keyof typeof ArtifactStepStatus]

export const PackageKind = { Ipa: 1, Aab: 2, Msix: 3, Apk: 4 } as const
export type PackageKind = (typeof PackageKind)[keyof typeof PackageKind]

export const IdentityKind = { Oidc: 1, CiToken: 2, User: 3 } as const
export type IdentityKind = (typeof IdentityKind)[keyof typeof IdentityKind]

export const DeliveryTarget = { TestFlightGroup: 1, PlayTrack: 2, MsFlight: 3, None: 4 } as const
export type DeliveryTarget = (typeof DeliveryTarget)[keyof typeof DeliveryTarget]

export const DeliveryWarningAction = { Deliver: 1, Hold: 2 } as const
export type DeliveryWarningAction = (typeof DeliveryWarningAction)[keyof typeof DeliveryWarningAction]

export const OidcProvider = { GitHub: 1, AzureDevOps: 2, GitLab: 3 } as const
export type OidcProvider = (typeof OidcProvider)[keyof typeof OidcProvider]

export const OidcExchangeResult = { Succeeded: 1, ClaimMismatch: 2, InvalidToken: 3 } as const
export type OidcExchangeResult = (typeof OidcExchangeResult)[keyof typeof OidcExchangeResult]

export const ArtifactPullSourceKind = { Eas: 1, Webhook: 2 } as const
export type ArtifactPullSourceKind = (typeof ArtifactPullSourceKind)[keyof typeof ArtifactPullSourceKind]

// Storeasy.Domain ReadinessStatus, the status of a catalog check.
export const CheckStatus = { Ok: 1, Missing: 2, Warning: 3, Assisted: 4, NotApplicable: 5, Unknown: 6 } as const
export type CheckStatus = (typeof CheckStatus)[keyof typeof CheckStatus]

/** The one scope a CI token takes today (CiToken.ArtifactsWriteScope). */
export const ARTIFACTS_WRITE_SCOPE = 'artifacts:write'
/** CiToken.MaxExpiryDays and the form's choices (design 13.9). */
export const CI_TOKEN_EXPIRY_DAYS = [30, 90, 180, 365] as const
export const CI_TOKEN_NAME_MAX = 60
/** Play tracks a rule may never target (IntakeRules.ProductionTracks). */
export const PRODUCTION_TRACKS = ['production']
/** The Play test tracks the rule editor offers; a closed track is typed by name. */
export const PLAY_TEST_TRACKS = ['internal', 'alpha', 'beta']
/** The audience a trusted publisher's token carries; fixed. */
export const OIDC_AUDIENCE = 'storeasy'

export interface ArtifactSourceRef {
  Provider?: string | null
  RunId?: string | null
  RunAttempt?: string | null
  RunUrl?: string | null
  Repository?: string | null
  Branch?: string | null
  Commit?: string | null
  Actor?: string | null
}

export interface ArtifactSessionListDto {
  Id: number
  AppId: number
  AppName?: string
  Platform: ArtifactPlatform
  FileName?: string
  Size: number
  Sha256?: string
  Source: ArtifactSource
  SourceRef?: ArtifactSourceRef | null
  Status: ArtifactSessionStatus
  StatusDescription?: string | null
  StartDate: IsoDateTime
  EndDate?: IsoDateTime | null
  StoreJobId?: number | null
  OriginalSessionId?: number | null
}

export interface ArtifactStep {
  Kind: ArtifactStepKind
  Status: ArtifactStepStatus
  Message?: string | null
  Bytes?: number | null
  Total?: number | null
  ResumeCount?: number | null
  StartDate?: IsoDateTime | null
  EndDate?: IsoDateTime | null
}

export interface ArtifactPackageInfo {
  Kind: PackageKind
  Identifier?: string | null
  VersionName?: string | null
  VersionCode?: string | null
  MinOs?: string | null
  TargetSdk?: string | null
  Signature?: string | null
  XcodeVersion?: string | null
  SdkVersion?: string | null
  ApplePlatform?: string | null
  UsesNonExemptEncryption?: boolean | null
  Publisher?: string | null
  DisplayName?: string | null
}

export interface ArtifactCheck {
  Key?: string
  Group?: string | null
  Status: CheckStatus
  RuleId?: string | null
  Title?: string | null
  Message?: string | null
}

export interface ArtifactAppliedRule {
  Platform: ArtifactPlatform
  Target: DeliveryTarget
  TargetId?: string | null
  TargetName?: string | null
  RolloutFraction?: number | null
  Notes?: string | null
  OnWarning: DeliveryWarningAction
  Override: boolean
  RuleUpdatedByUserName?: string | null
  RuleUpdatedDate?: IsoDateTime | null
}

export interface ArtifactIdentityDto {
  Kind?: IdentityKind | null
  TrustedPublisherId?: number | null
  CiTokenPrefix?: string | null
  UserName?: string | null
}

export interface ArtifactCiResultDto {
  ExitCode: number
  Summary?: string | null
}

export interface ArtifactSessionDetailDto extends ArtifactSessionListDto {
  Steps?: ArtifactStep[]
  Package?: ArtifactPackageInfo | null
  Checks?: ArtifactCheck[]
  AppliedRule?: ArtifactAppliedRule | null
  Identity?: ArtifactIdentityDto | null
  HoldReason?: string | null
  CiResult?: ArtifactCiResultDto | null
  PackageDeleted: boolean
}

export interface DeliveryRuleDto {
  Platform: ArtifactPlatform
  Enabled: boolean
  Target: DeliveryTarget
  TargetId?: string | null
  TargetName?: string | null
  OnWarning: DeliveryWarningAction
  RolloutFraction?: number | null
}

export interface DeliveryRulesDto {
  AppId: number
  Rules?: DeliveryRuleDto[]
  UpdatedByUserName?: string | null
  UpdatedDate?: IsoDateTime | null
}

export interface ArtifactAppSummaryDto {
  AppId: number
  AppName?: string
  LastSession?: ArtifactSessionListDto | null
  Rules?: DeliveryRuleDto[]
}

export interface ArtifactPartUrlDto {
  PartNumber: number
  Url: string
}

export interface ArtifactUploadedPartDto {
  PartNumber: number
  ETag?: string | null
}

export interface ArtifactUploadDto {
  SessionId: number
  Duplicate: boolean
  Resumed: boolean
  PartSize?: number | null
  Parts?: ArtifactPartUrlDto[]
  UploadedParts?: ArtifactUploadedPartDto[]
  UrlExpiryDate?: IsoDateTime | null
  OriginalSessionId?: number | null
}

export interface ArtifactDeliveryOverride {
  Store?: StoreKind | null
  Track?: string | null
  Group?: string | null
  Notes?: string | null
  /** Read and check the package, then hold it until a member confirms the delivery (artifacts/session/deliver). */
  HoldForConfirmation?: boolean
}

export interface OpenArtifactSessionInput {
  FileName: string
  Size: number
  Sha256: string
  ContentType: string
  Source: ArtifactSource
  Delivery?: ArtifactDeliveryOverride | null
}

export interface CiTokenDto {
  Id: number
  Name?: string
  AppIds?: number[]
  AppNames?: string[]
  AllApps: boolean
  Scopes?: string[]
  Prefix?: string
  ExpiryDate: IsoDateTime
  IsExpired: boolean
  LastUseDate?: IsoDateTime | null
  LastUseSource?: string | null
  CreatorUserName?: string | null
  CreationDate?: IsoDateTime | null
}

export interface CiTokenCreatedDto {
  Id: number
  Token?: string
  Prefix?: string
  ExpiryDate: IsoDateTime
}

export interface CreateCiTokenInput {
  Name: string
  AppIds: number[]
  AllApps: boolean
  Scopes: string[]
  ExpiryDays: number
}

export interface OidcClaim {
  Name?: string
  Value?: string | null
}

export interface TrustedPublisherDto {
  Id: number
  AppId: number
  AppName?: string
  Provider: OidcProvider
  Repository?: string | null
  Workflow?: string | null
  Organization?: string | null
  Project?: string | null
  ServiceConnection?: string | null
  Branch?: string | null
  Environment?: string | null
  Issuer?: string | null
  Audience?: string | null
  ExpectedClaims?: Record<string, string>
  LastExchangeDate?: IsoDateTime | null
  LastExchangeResult?: OidcExchangeResult | null
  LastExchangeRunUrl?: string | null
  LastClaims?: OidcClaim[] | null
  CreatorUserName?: string | null
  CreationDate?: IsoDateTime | null
}

export interface CreateTrustedPublisherInput {
  AppId: number
  Provider: OidcProvider
  Repository?: string | null
  Workflow?: string | null
  Branch?: string | null
  Environment?: string | null
  Organization?: string | null
  Project?: string | null
  ServiceConnection?: string | null
  Issuer?: string | null
}

export interface ClaimComparisonDto {
  Claim?: string
  Expected?: string | null
  Actual?: string | null
  Matches: boolean
}

export interface TrustedPublisherTestDto {
  Valid: boolean
  Matched: boolean
  Error?: string | null
  Claims?: ClaimComparisonDto[]
}

export interface ArtifactSourceDto {
  Id: number
  Kind: ArtifactPullSourceKind
  AppId: number
  AppName?: string
  Url?: string
  AllowedDomains?: string[]
  Platforms?: ArtifactPlatform[]
  IncludeApk: boolean
  SecretSetDate: IsoDateTime
  LastCallDate?: IsoDateTime | null
  LastCallResult?: string | null
}

export interface ArtifactSourceSavedDto {
  Id: number
  Url?: string
  Secret?: string | null
}

export interface SaveArtifactSourceInput {
  Id?: number | null
  Kind: ArtifactPullSourceKind
  AppId: number
  AllowedDomains: string[]
  Platforms: ArtifactPlatform[]
  IncludeApk: boolean
  RotateSecret: boolean
}

export interface IntakeSettingsDto {
  ArtifactRetentionDays: number
  MaxArtifactBytes: number
}

/** GET artifacts/counts: a period (default the last 30 days) by status and by source. */
export interface ArtifactCountsDto {
  From: IsoDateTime
  To: IsoDateTime
  Total: number
  ByStatus?: Array<{ Status: ArtifactSessionStatus; Count: number }> | null
  BySource?: Array<{ Source: ArtifactSource; Count: number }> | null
}
