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

// Custom (5): the member holds a role the organization defined (CustomRoleId); its permissions are its own set.
export const OrgRole = { Owner: 1, Admin: 2, Editor: 3, Viewer: 4, Custom: 5 } as const
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

export const ExternalLoginProvider = { Google: 1 } as const
export type ExternalLoginProvider = (typeof ExternalLoginProvider)[keyof typeof ExternalLoginProvider]

// Who may create an account on this instance (Storeasy:Registration:Mode, or the value a platform manager saved).
export const RegistrationMode = { Open: 1, InviteOnly: 2, Closed: 3 } as const
export type RegistrationMode = (typeof RegistrationMode)[keyof typeof RegistrationMode]

// The state of an invitation as its preview reports it.
export const InvitationStatus = { Pending: 1, Expired: 2, Revoked: 3, Accepted: 4 } as const
export type InvitationStatus = (typeof InvitationStatus)[keyof typeof InvitationStatus]

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
  UserType?: UserType | null
  /** The interface language the user picked ("tr-TR"); null until one is saved. */
  PreferredLanguage?: string | null
  CreationDate?: IsoDateTime | null
  /** Platform roles (Manager users), not organization roles. */
  Roles?: NameIntNavigationDto[] | null
  /** Platform permissions, "Group.Action", App.SuperAdmin already expanded to every permission. */
  Permissions?: string[] | null
  /** Identity providers linked to the account besides the password. */
  ExternalLogins?: ExternalLoginProvider[] | null
}

export interface GoogleSignInStartDto {
  AuthorizationUrl: string
  State: string
}

/** A signed in device (a web session); tokens for MCP and the API are not sessions. */
export interface AccountSessionDto {
  Id: number
  DeviceId?: string | null
  UserAgent?: string | null
  IpAddress?: string | null
  LastSeenDate?: IsoDateTime | null
  ExpiryDate: IsoDateTime
  IsCurrent: boolean
}

export interface WebPushSubscriptionDto {
  Id: number
  PushServiceHost?: string | null
  UserAgent?: string | null
  CreationDate?: IsoDateTime | null
  LastSuccessDate?: IsoDateTime | null
}

/** The browser's PushSubscription.toJSON() fields. */
export interface SaveWebPushSubscriptionInput {
  Endpoint: string
  P256dh: string
  Auth: string
  UserAgent?: string | null
}

export interface UpdateProfileInput {
  Name: string
  Surname?: string | null
  Language?: string | null
}

// ---- System ----

/** Anonymous: what the sign-in and registration screens need before anyone signs in. */
export interface PublicSettingsDto {
  RegistrationMode: RegistrationMode
  GoogleSignInEnabled: boolean
  Version?: string | null
  /** The VAPID public key (base64url); null when web push is not configured. */
  WebPushPublicKey?: string | null
}

export interface FeatureFlagStateDto {
  Key: string
  IsEnabled: boolean
  Description?: string | null
}

// ---- Organizations ----

export interface MyOrganizationDto {
  Id: number
  Name: string
  Slug: string
  Role: OrgRole
  /** Set when Role is Custom. */
  CustomRoleId?: number | null
  CustomRoleName?: string | null
  Permissions: OrgPermission[]
}

export interface OrganizationMemberDto {
  Id: number
  UserId: number
  Email?: string | null
  Name?: string | null
  Surname?: string | null
  Role: OrgRole
  CustomRoleId?: number | null
  CustomRoleName?: string | null
  JoinDate?: IsoDateTime | null
}

export interface OrganizationInvitationDto {
  Id: number
  Email: string
  Role: OrgRole
  CustomRoleId?: number | null
  CustomRoleName?: string | null
  InviterName?: string | null
  ExpiryDate: IsoDateTime
  CreationDate?: IsoDateTime | null
}

/** One role of the organization: the four locked seeded roles (Id null), then the custom roles. */
export interface OrgRoleDto {
  Id?: number | null
  Role: OrgRole
  Name: string
  Description?: string | null
  Permissions: OrgPermission[]
  MemberCount: number
  InvitationCount: number
  IsLocked: boolean
  CreationDate?: IsoDateTime | null
}

export interface OrganizationRoleInput {
  Name: string
  Description?: string | null
  Permissions: OrgPermission[]
}

/** The invitation behind a link, as much as its state allows (anonymous). */
export interface InvitationPreviewDto {
  Status: InvitationStatus
  OrganizationName?: string | null
  OrganizationSlug?: string | null
  InviterName?: string | null
  Role?: OrgRole | null
  CustomRoleName?: string | null
  Permissions?: OrgPermission[] | null
  MaskedEmail?: string | null
  ExpiryDate?: IsoDateTime | null
}

export interface OrganizationSettingsDto {
  AuditRetentionDays: number
  ArtifactRetentionDays: number
  SeenNotificationRetentionDays: number
  UnseenNotificationRetentionDays: number
  DefaultLocale?: string | null
}

/** Only the fields sent change. */
export interface UpdateOrganizationSettingsInput {
  AuditRetentionDays?: number | null
  DefaultLocale?: string | null
}

/** A role to give: a seeded one, or Custom with the custom role's id. */
export interface RoleChoice {
  role: OrgRole
  customRoleId: number | null
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
  // Microsoft: last 4 characters of the Entra tenant and client ids, the Partner Center seller id, the secret's expiry.
  TenantIdSuffix?: string | null
  ClientIdSuffix?: string | null
  SellerId?: string | null
  SecretExpiryDate?: IsoDateTime | null
}

export interface StoreConnectionDto {
  Id: number
  Store: StoreKind
  Name: string
  Fingerprint?: ConnectionFingerprint | null
  Status: ConnectionStatus
  LastVerifiedDate?: IsoDateTime | null
  LastVerifyError?: string | null
  // When the secret expires (Microsoft Entra client secrets); null for stores whose secrets do not expire.
  CredentialExpiryDate?: IsoDateTime | null
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

/** What the rules need to know that the stores do not report (UpdateAppProfileCommand); null until it is saved. */
export interface AppProfileDto {
  Platforms?: string[] | null
  DeviceFamilies?: string[] | null
  Regions?: string[] | null
  DeveloperRegion?: string | null
  AppType?: string | null
  Features?: string[] | null
  BuildSdkVersion?: string | null
  TargetSdk?: number | null
  IsLive?: boolean | null
}

export interface AppDetailDto extends AppListDto {
  /** Newest first, at most 100 across the stores. */
  Builds?: BuildDto[] | null
  Profile?: AppProfileDto | null
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
