// Backend contract (Milvasoft, storeasy-api). JSON keys are PascalCase (pascalizeKeys) and enums are numeric.
// Keep these in sync with Storeasy.Application DTOs and Storeasy.Domain enums: a renamed field breaks the build here and
// in the test factories.

/** A calendar day, "yyyy-MM-dd" (backend DateOnly). Never parse with new Date(value). */
export type DateOnly = string
/** A moment, ISO 8601 UTC ("2026-11-18T09:12:00Z"). */
export type IsoDateTime = string

export interface ResponseMessage {
  Key?: string
  Message?: string
  Type?: number
}

export interface ApiResponse<T> {
  IsSuccess: boolean
  StatusCode: number
  Messages?: ResponseMessage[]
  Data: T
}

export interface ApiListResponse<T> {
  IsSuccess: boolean
  StatusCode: number
  Messages?: ResponseMessage[]
  Data: T[]
  CurrentPageNumber?: number | null
  TotalPageCount?: number | null
  TotalDataCount?: number | null
}

// ---- Enums: an `as const` object plus a type of the same name. Values mirror Storeasy.Domain.Enums. ----

export const UserType = { Manager: 1, AppUser: 2 } as const
export type UserType = (typeof UserType)[keyof typeof UserType]

export const OrgRole = { Owner: 1, Admin: 2, Editor: 3, Viewer: 4 } as const
export type OrgRole = (typeof OrgRole)[keyof typeof OrgRole]

export const OrgPermission = {
  View: 1,
  EditContent: 2,
  ReleaseTest: 3,
  ReleaseProduction: 4,
  SubmitForReview: 5,
  ReplyReviews: 6,
  ManageConnections: 7,
  ManageWebhooks: 8,
  ManageMembers: 9,
  ManageOrganization: 10,
} as const
export type OrgPermission = (typeof OrgPermission)[keyof typeof OrgPermission]

// Microsoft is planned (design 14.7, backend gap); the client already knows the value the API will use.
export const StoreKind = { Apple: 1, Google: 2, Microsoft: 3 } as const
export type StoreKind = (typeof StoreKind)[keyof typeof StoreKind]

export const ConnectionStatus = { NotVerified: 1, Verified: 2, Rejected: 3, Unreachable: 4 } as const
export type ConnectionStatus = (typeof ConnectionStatus)[keyof typeof ConnectionStatus]

export const ActionChannel = { Web: 1, Mcp: 2, Api: 3, System: 4, Ci: 5 } as const
export type ActionChannel = (typeof ActionChannel)[keyof typeof ActionChannel]

export const AuditAction = {
  OrganizationCreated: 1,
  OrganizationUpdated: 2,
  MemberInvited: 10,
  InvitationRevoked: 11,
  MemberJoined: 12,
  MemberRoleChanged: 13,
  MemberRemoved: 14,
  ConnectionAdded: 20,
  ConnectionUpdated: 21,
  ConnectionRemoved: 22,
  ConnectionVerified: 23,
  JobCancelled: 30,
  AppAdded: 40,
  StoreAppLinked: 41,
  StoreAppUnlinked: 42,
} as const
export type AuditAction = (typeof AuditAction)[keyof typeof AuditAction]

export const StoreJobKind = { SyncApps: 1 } as const
export type StoreJobKind = (typeof StoreJobKind)[keyof typeof StoreJobKind]

export const StoreJobStatus = { Queued: 1, Running: 2, Succeeded: 3, Failed: 4, Cancelled: 5 } as const
export type StoreJobStatus = (typeof StoreJobStatus)[keyof typeof StoreJobStatus]

export const StoreJobStepStatus = { Running: 1, Succeeded: 2, Failed: 3, Skipped: 4 } as const
export type StoreJobStepStatus = (typeof StoreJobStepStatus)[keyof typeof StoreJobStepStatus]

// Which way a step moves data: to the store, back from it, or work inside storeasy.
export const StepDirection = { Outbound: 1, Inbound: 2, Internal: 3 } as const
export type StepDirection = (typeof StepDirection)[keyof typeof StepDirection]

export const BuildProcessingState = { Processing: 1, Valid: 2, Failed: 3, Invalid: 4, Expired: 5 } as const
export type BuildProcessingState = (typeof BuildProcessingState)[keyof typeof BuildProcessingState]

// What a stored file is for; it decides the size limit (AssetRules.MaxBytes).
export const AssetPurpose = { Screenshot: 1, Graphic: 2, MockupOutput: 3, BuildArtifact: 4 } as const
export type AssetPurpose = (typeof AssetPurpose)[keyof typeof AssetPurpose]

export const AssetStatus = { Pending: 1, Uploaded: 2, Rejected: 3 } as const
export type AssetStatus = (typeof AssetStatus)[keyof typeof AssetStatus]

// ---- Account ----

export interface MilvaToken {
  AccessToken: string
  RefreshToken: string
  ExpiresIn: number
  RefreshTokenExpiresIn: number
}

export interface LoginResponseDto {
  Id: number
  UserType: UserType
  UserTypeDescription?: string | null
  Token: MilvaToken
}

export interface NameIntNavigationDto {
  Id: number
  Name?: string | null
}

export interface AccountDetailDto {
  Id: number
  UserName: string
  Email?: string | null
  /** Unverified users can sign in but cannot create organizations or store connections (403 EmailNotVerified). */
  EmailConfirmed?: boolean | null
  Name?: string | null
  Surname?: string | null
  /** Platform roles (Manager users), not organization roles. */
  Roles?: NameIntNavigationDto[] | null
}

// ---- Organizations ----

export interface MyOrganizationDto {
  Id: number
  Name: string
  Slug: string
  Role: OrgRole
  Permissions: OrgPermission[]
}

export interface OrganizationMemberDto {
  Id: number
  UserId: number
  Email?: string | null
  Name?: string | null
  Surname?: string | null
  Role: OrgRole
  JoinDate?: IsoDateTime | null
}

export interface OrganizationInvitationDto {
  Id: number
  Email: string
  Role: OrgRole
  ExpiryDate: IsoDateTime
  CreationDate?: IsoDateTime | null
}

export interface MemberListDto {
  Members: OrganizationMemberDto[]
  /** Filled only for callers with ManageMembers. */
  Invitations?: OrganizationInvitationDto[] | null
}

export interface InvitationCreatedDto {
  Id: number
  /** The one time token for the accept link; returned only in this response. */
  Token: string
  ExpiryDate: IsoDateTime
}

export interface StoreAuditLogDto {
  Id: number
  Date: IsoDateTime
  UserName?: string | null
  Channel: ActionChannel
  Action: AuditAction
  Store?: StoreKind | null
  TargetId?: string | null
  Summary?: string | null
  /** Free text today (often a JSON string); structured before/after is a backend gap (design 15.16). */
  Details?: string | null
}

// ---- Store connections ----

export interface ConnectionFingerprint {
  KeyId?: string | null
  IssuerIdSuffix?: string | null
  ServiceAccountEmail?: string | null
  ProjectId?: string | null
}

export interface StoreConnectionDto {
  Id: number
  Store: StoreKind
  Name: string
  Fingerprint?: ConnectionFingerprint | null
  Status: ConnectionStatus
  LastVerifiedDate?: IsoDateTime | null
  LastVerifyError?: string | null
  CreationDate?: IsoDateTime | null
}

// ---- Apps and builds ----

/** An app's record in one store. */
export interface AppStoreRecordDto {
  StoreAppId: number
  Store: StoreKind
  /** The store's id of the app (App Store Connect app id, Play package name, Microsoft Store product id). */
  ExternalId?: string | null
  /** Bundle id or package name. */
  BundleId?: string | null
  Name?: string | null
  StoreConnectionId?: number | null
  /** Null until the first sync confirmed the record. */
  SyncedDate?: IsoDateTime | null
}

export interface AppListDto {
  Id: number
  Name: string
  IconUrl?: string | null
  CreationDate?: IsoDateTime | null
  /** One record per store at most. */
  Stores?: AppStoreRecordDto[] | null
}

export interface BuildDto {
  Id: number
  StoreAppId: number
  Store: StoreKind
  ExternalId?: string | null
  Version?: string | null
  /** Build number or version code. */
  BuildNumber?: string | null
  ProcessingState: BuildProcessingState
  /** Play tracks the build is released to. */
  Tracks?: string | null
  MinOsVersion?: string | null
  UploadedDate?: IsoDateTime | null
  /** TestFlight expiry. */
  ExpiryDate?: IsoDateTime | null
}

export interface AppDetailDto extends AppListDto {
  /** Newest first, at most 100 across the stores. */
  Builds?: BuildDto[] | null
}

/** Adding a Google app by hand: the sync job (JobId) confirms the record, or removes it when Play refuses it. */
export interface AppAddedDto {
  AppId: number
  StoreAppId: number
  JobId: number
}

// ---- Store jobs ----

export interface StoreJobListDto {
  Id: number
  Kind: StoreJobKind
  Status: StoreJobStatus
  Store?: StoreKind | null
  AppId?: number | null
  Channel: ActionChannel
  /** "system" for system jobs. */
  RequestedByUserName?: string | null
  Attempt: number
  MaxAttempts: number
  CancelRequested: boolean
  Summary?: string | null
  /** Redacted reason of the last failed attempt. */
  Error?: string | null
  CreationDate?: IsoDateTime | null
  StartDate?: IsoDateTime | null
  EndDate?: IsoDateTime | null
}

export interface StoreJobStepDto {
  Order: number
  Attempt: number
  /** Machine name, "area.action". */
  Name: string
  Direction: StepDirection
  Status: StoreJobStepStatus
  Progress?: number | null
  Total?: number | null
  Message?: string | null
  StartDate: IsoDateTime
  EndDate?: IsoDateTime | null
}

export interface StoreJobDetailDto extends StoreJobListDto {
  /** Parameters as JSON (never a secret). */
  Payload?: string | null
  /** Structured result as JSON. */
  Result?: string | null
  NextAttemptDate?: IsoDateTime | null
  /** In order, across attempts. */
  Steps?: StoreJobStepDto[] | null
}

// ---- Assets ----

export interface AssetUploadPartDto {
  PartNumber: number
  Url: string
}

/** A small file has one UploadUrl; a large one (over 64 MB) has Parts of PartSize bytes each. */
export interface AssetUploadDto {
  AssetId: number
  UploadUrl?: string | null
  PartSize?: number | null
  Parts?: AssetUploadPartDto[] | null
  ExpiryDate: IsoDateTime
}

export interface UploadedPart {
  PartNumber: number
  ETag: string
}

export interface AssetDto {
  Id: number
  Purpose: AssetPurpose
  FileName: string
  ContentType: string
  Size: number
  Sha256?: string | null
  Status: AssetStatus
  CreationDate?: IsoDateTime | null
  UploadDate?: IsoDateTime | null
}

export interface AssetDownloadDto {
  Url: string
  ExpiryDate: IsoDateTime
}
