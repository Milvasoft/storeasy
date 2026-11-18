// Phase 2 contract (storeasy-api Storeasy.Application ContentDtos, ReadinessDtos, RuleDtos, AnswerDtos, AssistedStepDtos,
// AssetDtos and Storeasy.Domain enums). JSON keys are PascalCase after pascalizeKeys, enums are numeric, and the API
// leaves null properties out (JsonIgnoreCondition.WhenWritingNull), so every reference typed field is optional.
// Kept apart from lib/api/types.ts while the phase 2 screens are built next to other work; merge into it later.
import type { ActionChannel, BuildDto, DateOnly, IsoDateTime, OrgPermission, StoreKind } from '@/lib/api/types'

// ---- Enums (Storeasy.Domain.Enums) ----

export const ReadinessStatus = { Ok: 1, Missing: 2, Warning: 3, Assisted: 4, NotApplicable: 5, Unknown: 6 } as const
export type ReadinessStatus = (typeof ReadinessStatus)[keyof typeof ReadinessStatus]

export const ComplianceStatus = { Done: 1, Missing: 2, Unknown: 3, NotApplicable: 4 } as const
export type ComplianceStatus = (typeof ComplianceStatus)[keyof typeof ComplianceStatus]

export const RuleStatus = { InForce: 1, Upcoming: 2, Deprecated: 3 } as const
export type RuleStatus = (typeof RuleStatus)[keyof typeof RuleStatus]

export const RuleApplicability = { Applies: 1, DoesNotApply: 2, Unknown: 3 } as const
export type RuleApplicability = (typeof RuleApplicability)[keyof typeof RuleApplicability]

export const MediaKind = { Screenshot: 1, Preview: 2 } as const
export type MediaKind = (typeof MediaKind)[keyof typeof MediaKind]

export const MediaSlotFill = { Own: 1, Fallback: 2, None: 3 } as const
export type MediaSlotFill = (typeof MediaSlotFill)[keyof typeof MediaSlotFill]

export const ChangePlanKind = {
  Listing: 1,
  Details: 2,
  Categories: 3,
  AgeRating: 4,
  Pricing: 5,
  Screenshots: 6,
  Previews: 7,
  ReviewInfo: 8,
  BuildLink: 9,
  ExportCompliance: 10,
  ReleaseSettings: 11,
  TrackRelease: 12,
  Rollout: 13,
  Testers: 14,
  BundleUpload: 15,
  DataSafety: 16,
  SubmitForReview: 17,
  MsChange: 50,
  Monetization: 70,
  ServerNotifications: 71,
  SandboxTesters: 72,
} as const
export type ChangePlanKind = (typeof ChangePlanKind)[keyof typeof ChangePlanKind]

export const ChangePlanStatus = { Pending: 1, Approved: 2, Applied: 3, Failed: 4, Rejected: 5, Discarded: 6, Expired: 7 } as const
export type ChangePlanStatus = (typeof ChangePlanStatus)[keyof typeof ChangePlanStatus]

export const ChangeAction = { Create: 1, Update: 2, Delete: 3 } as const
export type ChangeAction = (typeof ChangeAction)[keyof typeof ChangeAction]

export const SavedAnswerKind = {
  PrivacyLabels: 1,
  DataSafety: 2,
  ContentRating: 3,
  TargetAudience: 4,
  ReviewLogin: 5,
  ReviewNotes: 6,
} as const
export type SavedAnswerKind = (typeof SavedAnswerKind)[keyof typeof SavedAnswerKind]

export const AssistedStepStatus = { Todo: 1, InProgress: 2, HandedOff: 3, Done: 4, Skipped: 5 } as const
export type AssistedStepStatus = (typeof AssistedStepStatus)[keyof typeof AssistedStepStatus]

export const ReleasePlanKind = { SetReleaseType: 1, AssignTrack: 2 } as const
export type ReleasePlanKind = (typeof ReleasePlanKind)[keyof typeof ReleasePlanKind]

export const ReleaseType = { Manual: 1, AfterApproval: 2, Scheduled: 3 } as const
export type ReleaseType = (typeof ReleaseType)[keyof typeof ReleaseType]

export const RolloutAction = { Pause: 1, Resume: 2, Complete: 3, SetPercentage: 4, Halt: 5 } as const
export type RolloutAction = (typeof RolloutAction)[keyof typeof RolloutAction]

// ---- Apps ----

// BuildDto as the Builds list sends it; lib/api/types BuildDto does not carry the export compliance answer yet.
export interface BuildLinkDto extends BuildDto {
  UsesNonExemptEncryption?: boolean | null
}

// UpdateAppProfileCommand (there is no read endpoint for the profile yet).
export interface AppProfileInput {
  Platforms: string[] | null
  DeviceFamilies: string[] | null
  Regions: string[] | null
  DeveloperRegion: string | null
  AppType: string | null
  Features: string[] | null
  BuildSdkVersion: string | null
  TargetSdk: number | null
  IsLive: boolean | null
}

// ---- Readiness and compliance (ReadinessDtos) ----

export interface ReadinessSlotDto {
  ClassKey: string
  RuleId?: string
  Required: boolean
  FilledBy: MediaSlotFill
  FallbackFromClassKey?: string
  Count: number
}

export interface ReadinessItemDto {
  Key: string
  Group?: string
  Status: ReadinessStatus
  RuleId?: string
  Title?: string
  Message?: string
  Locale?: string
  FixRoute?: string
  DueDate?: DateOnly
  Slots?: ReadinessSlotDto[]
}

export interface ReadinessReportDto {
  AppId: number
  StoreAppId: number
  Store: StoreKind
  VersionId?: string
  VersionString?: string
  CatalogVersion?: string
  CheckedDate: IsoDateTime
  CanSubmit: boolean
  Ok: number
  Missing: number
  Warning: number
  Assisted: number
  Unknown: number
  Items?: ReadinessItemDto[]
}

export interface DeadlineDto {
  Date: DateOnly
  RuleId?: string
  Title?: string
}

export interface AppOverviewStoreDto {
  StoreAppId: number
  Store: StoreKind
  ExternalId?: string
  VersionString?: string
  CheckedDate?: IsoDateTime
  Ok: number
  Missing: number
  Warning: number
  Assisted: number
  Unknown: number
  MissingKeys?: string[]
  Error?: string
}

export interface AppOverviewDto {
  AppId: number
  Name: string
  IconUrl?: string
  Stores?: AppOverviewStoreDto[]
  NextDeadline?: DeadlineDto
}

export interface ComplianceItemDto {
  AppId: number
  AppName?: string
  RuleId: string
  Store: StoreKind
  Title?: string
  Summary?: string
  DueDate?: DateOnly
  DaysLeft?: number
  Status: ComplianceStatus
  RuleStatus: RuleStatus
  Applicability: RuleApplicability
  MatchReason?: string[]
  SourceUrl?: string
  CheckedDate?: DateOnly
  Conflict?: string
  AssistedStepId?: number
}

// ---- Rules catalog (RuleDtos, Storeasy.Domain.Rules) ----

export interface RuleCatalogInfoDto {
  Version: string
  PublishedDate: IsoDateTime
  RuleCount: number
  Source?: string
}

export interface RuleListDto {
  Id: string
  Store: StoreKind
  Category?: string
  Title?: string
  Level?: string
  Status: RuleStatus
  EffectiveDate?: DateOnly
  Deadline?: DateOnly
  DueDate?: DateOnly
  CheckKind?: string
  Verified: boolean
  SourceUrl?: string
  CheckedDate?: DateOnly
  AffectedAppCount: number
}

export interface RuleSize {
  Width: number
  Height: number
  Note?: string
}

export interface RuleConditions {
  Platforms?: string[]
  DeviceFamilies?: string[]
  Regions?: string[]
  DeveloperRegions?: string[]
  AppTypes?: string[]
  PackageTypes?: string[]
  Distribution?: string[]
  Features?: string[]
  Build?: { SdkName?: string; SdkVersionAtLeast?: string }
  Expression?: string
}

export interface RuleParams {
  Sizes?: RuleSize[]
  Formats?: string[]
  AlphaAllowed?: boolean
  MinCount?: number
  MaxCount?: number
  MinChars?: number
  MaxChars?: number
  MaxBytes?: number
  MinDimension?: number
  MaxDimension?: number
  MaxAspectRatio?: number
  MaxFileSizeBytes?: number
  MinTargetSdk?: number
  MinXcodeVersion?: string
  MinSdkVersion?: string
  MinDeploymentTarget?: string
  MaxDays?: number
  WarnDaysBefore?: number
}

export interface RuleSource {
  Url: string
  Title?: string
  CheckedAt: DateOnly
  Quote?: string
}

export interface RuleConflict {
  Description?: string
  Sources?: string[]
  CatalogChoice?: string
}

export interface RuleAffectedAppDto {
  AppId: number
  Name: string
  Applicability: RuleApplicability
  Reasons?: string[]
  UnknownKeys?: string[]
}

export interface RuleDetailDto extends RuleListDto {
  Description?: string
  Condition?: string
  FallbackChain?: string[]
  SatisfiedBy?: string[]
  AppliesWhen?: RuleConditions
  Params?: RuleParams
  Sources?: RuleSource[]
  Conflicts?: RuleConflict[]
  Notes?: string
  DateNote?: string
  CatalogVersion?: string
  AffectedApps?: RuleAffectedAppDto[]
}

// ---- Change plans (ContentDtos, Storeasy.Domain.JsonModels) ----

export interface PlanItem {
  Group?: string
  Locale?: string
  Field?: string
  Before?: string
  After?: string
  Action: ChangeAction
}

export interface PlanWarning {
  Key?: string
  Message?: string
  Field?: string
  Locale?: string
}

export interface PlanCall {
  Method?: string
  Path?: string
  Target?: string
}

export interface PlanFile {
  AssetId: number
  FileName?: string
  Width?: number
  Height?: number
  Md5?: string
  Result?: string
  Reason?: string
  SuggestedClassKey?: string
}

export interface ChangePlanListDto {
  PlanId: number
  Kind: ChangePlanKind
  Title?: string
  AppId: number
  AppName?: string
  StoreAppId: number
  Store: StoreKind
  Source: ActionChannel
  CreatedByUserName?: string
  RequiredPermission: OrgPermission
  ConfirmRequired: boolean
  ItemCount: number
  WarningCount: number
  Status: ChangePlanStatus
  CreationDate?: IsoDateTime
  ExpiryDate: IsoDateTime
  JobId?: number
}

export interface ChangePlanDto extends ChangePlanListDto {
  VersionId?: string
  VersionString?: string
  ConfirmText?: string
  Items?: PlanItem[]
  Warnings?: PlanWarning[]
  Calls?: PlanCall[]
  Files?: PlanFile[]
  BaseHash?: string
  CatalogVersion?: string
  DecidedByUserName?: string
  DecisionNote?: string
  DecisionDate?: IsoDateTime
  AppliedDate?: IsoDateTime
  JobError?: string
}

// ---- Listings ----

export interface ListingLocaleDto {
  Locale: string
  IsPrimary: boolean
  FilledCount: number
  RequiredCount: number
}

export interface ListingFieldDto {
  Key: string
  Group?: string
  Locale?: string
  Value?: string
  MaxLength?: number
  MaxBytes?: number
  Required: boolean
  StoreValueHash?: string
}

export interface ListingDto {
  StoreAppId: number
  Store: StoreKind
  VersionId?: string
  VersionString?: string
  VersionState?: string
  VersionEditable: boolean
  CatalogVersion?: string
  Locales?: ListingLocaleDto[]
  Fields?: ListingFieldDto[]
}

export interface ListingDetailsDto {
  StoreAppId: number
  ContactEmail?: string
  ContactPhone?: string
  ContactWebsite?: string
  DefaultLanguage?: string
}

export interface ListingChangeDto {
  Locale: string | null
  Field: string
  Value: string | null
}

// ---- Screenshots and previews ----

export interface MediaItemDto {
  Id: string
  Position: number
  FileName?: string
  Width?: number
  Height?: number
  Checksum?: string
  State?: string
  Url?: string
}

export interface MediaClassDto {
  ClassKey: string
  RuleId?: string
  Title?: string
  Sizes?: RuleSize[]
  Requirement?: string
  Required: boolean
  Applicability: RuleApplicability
  Condition?: string
  Fill: MediaSlotFill
  FallbackFromClassKey?: string
  Count: number
  Max?: number
  Items?: MediaItemDto[]
}

export interface MediaSetListDto {
  StoreAppId: number
  Store: StoreKind
  Kind: MediaKind
  Locale?: string
  VersionId?: string
  CatalogVersion?: string
  Classes?: MediaClassDto[]
}

// ---- Categories, age rating, pricing, review info ----

export interface StoreOptionDto {
  Key: string
  Title?: string
  Extra?: string
}

export interface CategoryDto {
  StoreAppId: number
  Primary?: string
  Secondary?: string
  Options?: StoreOptionDto[]
  Copyright?: string
  ContentRightsDeclaration?: string
}

export interface AgeRatingAnswerDto {
  Key: string
  Value?: string
  IsNew: boolean
  Assisted: boolean
  DueDate?: DateOnly
}

export interface AgeRatingDto {
  StoreAppId: number
  Answers?: AgeRatingAnswerDto[]
  AssistedRuleIds?: string[]
}

export interface PriceEntryDto {
  Territory?: string
  PricePointId?: string
  CustomerPrice?: string
  StartDate?: DateOnly
  EndDate?: DateOnly
}

export interface TerritoryDto {
  Code: string
  Available: boolean
}

export interface PricingDto {
  StoreAppId: number
  BaseTerritory?: string
  PricePointId?: string
  Schedule?: PriceEntryDto[]
  Territories?: TerritoryDto[]
  AvailableInNewTerritories?: boolean
}

export interface ReviewInfoDto {
  StoreAppId: number
  VersionId?: string
  ContactFirstName?: string
  ContactLastName?: string
  ContactEmail?: string
  ContactPhone?: string
  DemoAccountRequired: boolean
  DemoAccountName?: string
  DemoPasswordSet: boolean
  StorePasswordSet: boolean
  Notes?: string
  StoreDiffers: boolean
}

// ---- Releases ----

export interface ReleaseNote {
  Language: string
  Text: string
}

export interface PhasedReleaseDto {
  State?: string
  CurrentDayNumber?: number
  StartDate?: IsoDateTime
}

export interface StoreVersionDto {
  VersionId: string
  VersionString?: string
  Platform?: string
  State?: string
  ReleaseType?: string
  EarliestReleaseDate?: IsoDateTime
  BuildId?: string
  BuildNumber?: string
  PhasedRelease?: PhasedReleaseDto
}

export interface TrackReleaseDto {
  Name?: string
  VersionCodes?: string[]
  Status?: string
  UserFraction?: number
  ReleaseNotes?: ReleaseNote[]
  Countries?: string[]
  IncludeRestOfWorld?: boolean
}

export interface TrackDto {
  Track: string
  Releases?: TrackReleaseDto[]
  GoogleGroups?: string[]
}

export interface ReleaseOverviewDto {
  StoreAppId: number
  Store: StoreKind
  Versions?: StoreVersionDto[]
  Tracks?: TrackDto[]
}

export interface PlayTestersDto {
  StoreAppId: number
  Track?: string
  GoogleGroups?: string[]
}

// ---- Saved answers ----

// The answer documents are free JSON on the wire; after pascalizeKeys their member names are PascalCase, which the API
// reads back case-insensitively (SavedAnswerDocuments uses JsonSerializerDefaults.Web).
export interface SavedAnswerSectionDto {
  Key: SavedAnswerKind
  UpdatedDate?: IsoDateTime
  UpdatedByUserName?: string
  Answers?: unknown
}

export interface SavedAnswerSetDto {
  AppId: number
  Sections?: SavedAnswerSectionDto[]
  DemoPasswordSet: boolean
}

export interface PrivacyDataTypeAnswer {
  Category: string
  Type: string
  Purposes: string[]
  LinkedToUser: boolean
  UsedForTracking: boolean
}

export interface PrivacyLabelsAnswers {
  CollectsData: boolean
  DataTypes: PrivacyDataTypeAnswer[]
  PrivacyPolicyUrl: string | null
}

export interface DataSafetyRow {
  QuestionId: string
  ResponseId: string | null
  ResponseValue: string | null
  Label: string | null
}

export interface DataSafetyAnswers {
  Rows: DataSafetyRow[]
}

export interface ContentRatingAnswers {
  Category: string | null
  Email: string | null
  Answers: Record<string, string>
}

export interface TargetAudienceAnswers {
  AgeGroups: string[]
  AppealsToChildren: boolean
  ContainsAds: boolean
}

export interface ReviewLoginAnswers {
  ContactFirstName: string | null
  ContactLastName: string | null
  ContactEmail: string | null
  ContactPhone: string | null
  DemoAccountRequired: boolean
  DemoAccountName: string | null
}

export interface ReviewNotesAnswers {
  Notes: string | null
}

// ---- Assisted steps ----

export interface AssistedStepItemDto {
  Order: number
  Text?: string
  Done: boolean
}

export interface AssistedStepListDto {
  Id: number
  AppId: number
  Key: string
  RuleId?: string
  Store: StoreKind
  Title?: string
  Status: AssistedStepStatus
  DueDate?: DateOnly
  LastDoneDate?: IsoDateTime
  LastDoneByUserName?: string
  Channel?: ActionChannel
}

export interface AssistedStepDetailDto extends AssistedStepListDto {
  StoreUrl?: string
  Steps?: AssistedStepItemDto[]
  Pitfalls?: string[]
  AnswerKeys?: SavedAnswerKind[]
  Answers?: Record<string, unknown>
  HandedOffDate?: IsoDateTime
  HandedOffByUserName?: string
  Note?: string
  EvidenceAssetId?: number
}

export interface AssistedStepHandOffDto {
  StepId: number
  Key: string
  Store: StoreKind
  AppName?: string
  ExternalId?: string
  Title?: string
  StoreUrl?: string
  Steps?: AssistedStepItemDto[]
  Pitfalls?: string[]
  Answers?: Record<string, unknown>
  Prompt?: string
  McpTool?: string
}
