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

export const StoreKind = { Apple: 1, Google: 2, Microsoft: 3, Winget: 4, Chrome: 5, Firefox: 6 } as const
export type StoreKind = (typeof StoreKind)[keyof typeof StoreKind]

export const ConnectionStatus = { NotVerified: 1, Verified: 2, Rejected: 3, Unreachable: 4 } as const
export type ConnectionStatus = (typeof ConnectionStatus)[keyof typeof ConnectionStatus]

export const ActionChannel = { Web: 1, Mcp: 2, Api: 3, System: 4, Ci: 5 } as const
export type ActionChannel = (typeof ActionChannel)[keyof typeof ActionChannel]

export const AuditAction = {
  OrganizationCreated: 1,
  OrganizationUpdated: 2,
  OrganizationLogoSet: 3,
  OrganizationLogoRemoved: 4,
  MemberInvited: 10,
  InvitationRevoked: 11,
  MemberJoined: 12,
  MemberRoleChanged: 13,
  MemberRemoved: 14,
  OrganizationRoleCreated: 15,
  OrganizationRoleUpdated: 16,
  OrganizationRoleDeleted: 17,
  ConnectionAdded: 20,
  ConnectionUpdated: 21,
  ConnectionRemoved: 22,
  ConnectionVerified: 23,
  AiKeySet: 24,
  AiKeyRemoved: 25,
  AiModelsChanged: 26,
  ConnectionKeyReplaced: 27,
  JobCancelled: 30,
  AppAdded: 40,
  StoreAppLinked: 41,
  StoreAppUnlinked: 42,
  AppProfileUpdated: 43,
  AppHidden: 44,
  AppUnhidden: 45,
  AppsReordered: 46,
  AppAlertsMuted: 47,
  AppAlertsUnmuted: 48,
  AppDeleted: 49,
  PlanApproved: 50,
  PlanRejected: 51,
  PlanDiscarded: 52,
  ListingApplied: 53,
  MediaApplied: 54,
  ReleaseApplied: 55,
  RolloutChanged: 56,
  BuildLinked: 57,
  SubmittedForReview: 58,
  ContentApplied: 59,
  AnswersUpdated: 60,
  AnswersCopied: 61,
  AssistedStepHandedOff: 62,
  AssistedStepCompleted: 63,
  MockupTemplateCreated: 70,
  MockupTemplateUpdated: 71,
  MockupTemplateDeleted: 72,
  MockupCaptionsUpdated: 73,
  MockupExported: 74,
  SubscriptionChanged: 80,
  PriceScheduled: 81,
  OfferChanged: 82,
  IapChanged: 83,
  PurchaseSettingsChanged: 84,
  MsChangeQueued: 150,
  MsChangeApplied: 151,
  MsSubmitted: 152,
  MsProductAdded: 153,
  MsConnectionSecretRotated: 154,
  MsFlightGroupSaved: 155,
  WingetPackageAdded: 160,
  WingetPullRequestOpened: 161,
  WingetReviewStateChanged: 162,
  WingetPublished: 163,
  WingetPullRequestClosed: 164,
  ChromeItemAdded: 170,
  ChromePackageUploaded: 171,
  ChromeSubmitted: 172,
  ChromeReviewStateChanged: 173,
  ChromePublished: 174,
  ChromeRejected: 175,
  ChromeDeployPercentageChanged: 176,
  ChromeSubmissionCancelled: 177,
  ChromeRolledBack: 178,
  ChromePolicyFlagChanged: 179,
  FirefoxAddonAdded: 180,
  FirefoxVersionSubmitted: 181,
  FirefoxListingUpdated: 182,
  FirefoxPreviewsChanged: 183,
  FirefoxIconUpdated: 184,
  FirefoxReviewStateChanged: 185,
  FirefoxApproved: 186,
  FirefoxRejected: 187,
  FirefoxAddonStatusChanged: 188,
  FirefoxReleaseNotesUpdated: 189,
  TokenCreated: 300,
  TokenRevoked: 301,
  CiTokenCreated: 302,
  CiTokenRevoked: 303,
  TrustedPublisherAdded: 304,
  TrustedPublisherRemoved: 305,
  DeliveryRulesUpdated: 306,
  ArtifactSourceUpdated: 307,
  ArtifactReceived: 308,
  ArtifactDelivered: 309,
  ArtifactRejected: 310,
  ArtifactHeld: 311,
  ArtifactDeleted: 312,
  IntakeSettingsUpdated: 313,
  BuildPackageDownloaded: 314,
  WebhookEndpointAdded: 400,
  WebhookSecretRotated: 401,
  WebhookEndpointRemoved: 402,
  WebhookEventReprocessed: 403,
  OrgWebhookAdded: 410,
  OrgWebhookUpdated: 411,
  OrgWebhookRemoved: 412,
  RejectionOpened: 420,
  RejectionTextAdded: 421,
  RejectionChecklistUpdated: 422,
  RejectionReplySent: 423,
  RejectionClosed: 424,
  ReviewReplied: 430,
  SavedReplyChanged: 431,
  ReviewReplyDraftGenerated: 432,
  ListingSuggestionGenerated: 433,
  CategorySuggestionGenerated: 434,
  BetaGroupChanged: 440,
  BetaTestersChanged: 441,
  BetaTextChanged: 442,
  BetaReviewSubmitted: 443,
  PreReviewStarted: 450,
  PreReviewCompleted: 451,
  AsoKeywordsTracked: 470,
  AsoKeywordsUntracked: 471,
  AsoKeywordSeedsGenerated: 472,
  AppleAnalyticsEnabled: 480,
  ReportBucketUpdated: 481,
  AsoExperimentCreated: 490,
  AsoExperimentUpdated: 491,
  AsoExperimentDeleted: 492,
  AsoExperimentWritten: 493,
  AsoExperimentStarted: 494,
  AsoExperimentStopped: 495,
  AsoExperimentWinnerApplied: 496,
  AsoExperimentResultRecorded: 497,
  AsoExperimentSubmitted: 498,
  RegistryPackageAdded: 500,
  RegistryPackageRemoved: 501,
  RegistryVersionPublished: 502,
  RegistryVersionDeprecated: 503,
  RegistryVersionYanked: 504,
  RegistryAdvisoryFound: 505,
  RegistrySetupSaved: 506,
  RegistryPublisherMismatch: 507,
  RegistryTokenReminderSaved: 508,
  RegistryTokenReminderRemoved: 509,
  RegistryTokenExpiring: 510,
  RegistryGroupSaved: 511,
  RegistryGroupRemoved: 512,
  RegistryConnectionSaved: 513,
  RegistryConnectionRemoved: 514,
  RegistryReleaseSettingsSaved: 515,
  RegistryPlanCreated: 516,
  RegistryPlanApproved: 517,
  RegistryPlanRejected: 518,
  RegistryReleaseStarted: 519,
  RegistryReleaseRunFinished: 520,
  RegistryReleasePublished: 521,
  RegistryReleaseFailed: 522,
  RegistryOperationApplied: 523,
} as const
export type AuditAction = (typeof AuditAction)[keyof typeof AuditAction]

// Storeasy.Domain.Enums.StoreJobKind: what a job does.
export const StoreJobKind = {
  SyncApps: 1,
  ApplyChangePlan: 2,
  ProcessArtifact: 30,
  MsSync: 50,
  MsSubmit: 52,
  MsCheckInstaller: 53,
  MockupExport: 60,
  SyncMonetization: 70,
  SyncAsoExperiments: 80,
  PreReview: 90,
  AsoKeywordResearch: 110,
  SyncStoreMetrics: 120,
  EnableAppleAnalytics: 121,
  WingetSync: 130,
  ChromeSync: 140,
  FirefoxSync: 150,
  ProcessWebhookEvent: 400,
  RegisterWebhooks: 401,
  SyncVersionStates: 402,
  SyncReviews: 403,
  ReplyToReview: 404,
  SyncTestFlight: 405,
  UpdateTestFlight: 406,
  PingWebhooks: 407,
  RegistrySync: 500,
  RegistryPlanApply: 501,
} as const
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

// Storeasy:Mode. SingleTeam: one team on its own instance (first run setup, one organization, no sign up). MultiTenant:
// a hosted instance for many customers.
export const DeploymentMode = { SingleTeam: 1, MultiTenant: 2 } as const
export type DeploymentMode = (typeof DeploymentMode)[keyof typeof DeploymentMode]

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
  /** When the user answered the guided tour invitation (started it or closed it); null until then. */
  TourPromptSeenDate?: IsoDateTime | null
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
  /** Missing on an older API: treated as multi tenant (today's behaviour). */
  Mode?: DeploymentMode
  /** True while a single team instance has no user: the first run setup replaces the sign-in. */
  SetupRequired?: boolean
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
  /** Short lived URL of the organization's square logo; null when it has none and shows its initial. */
  LogoUrl?: string | null
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
  OrganizationLogoUrl?: string | null
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

// AI settings of the organization (GET organizations/ai-settings). The Anthropic API key itself never comes back.
export interface AiSettingsDto {
  HasApiKey: boolean
  ApiKeyLastFour?: string | null
  ApiKeySetDate?: string | null
  ApiKeySetByUserName?: string | null
  Model?: string | null
  // Only in the answer of saving a key: true when Anthropic accepted it, false when it could not be checked.
  ApiKeyVerified?: boolean | null
  // The model of each AI feature (the organization's choice, or the default) and the models one may choose.
  ReplyDraftModel?: string | null
  PreReviewModel?: string | null
  Models?: AiModelOptionDto[] | null
}

// A model offered in the AI settings, with its approximate price in US dollars per million tokens.
export interface AiModelOptionDto {
  Id: string
  Name: string
  InputUsdPerMTok: number
  OutputUsdPerMTok: number
}

/** PUT organizations/ai-settings/models: only the fields sent change. */
export interface UpdateAiModelsInput {
  ReplyDraftModel?: string | null
  PreReviewModel?: string | null
}

// ---- AI pre-review (pre-reviews/*) ----

export const PreReviewStatus = { Queued: 1, Running: 2, Succeeded: 3, Failed: 4 } as const
export type PreReviewStatus = (typeof PreReviewStatus)[keyof typeof PreReviewStatus]

export const PreReviewVerdict = { LikelyApproved: 1, AtRisk: 2, LikelyRejected: 3 } as const
export type PreReviewVerdict = (typeof PreReviewVerdict)[keyof typeof PreReviewVerdict]

export const PreReviewSeverity = { Blocker: 1, Major: 2, Minor: 3 } as const
export type PreReviewSeverity = (typeof PreReviewSeverity)[keyof typeof PreReviewSeverity]

export const PreReviewArea = {
  Metadata: 1,
  Screenshots: 2,
  Privacy: 3,
  AgeRating: 4,
  Payments: 5,
  Functionality: 6,
  Legal: 7,
  Other: 8,
} as const
export type PreReviewArea = (typeof PreReviewArea)[keyof typeof PreReviewArea]

export interface PreReviewFindingDto {
  Severity: PreReviewSeverity
  Area: PreReviewArea
  Guideline?: string | null
  Title?: string | null
  Evidence?: string | null
  Fix?: string | null
}

// One AI pre-review of an app in one store: an estimate of how the store's reviewer would judge the submission. It never
// blocks or performs a submission.
export interface PreReviewDto {
  Id: number
  AppId: number
  StoreAppId: number
  Store: StoreKind
  VersionString?: string | null
  Status: PreReviewStatus
  JobId?: number | null
  Model: string
  Language?: string | null
  Verdict?: PreReviewVerdict | null
  // 0 to 100.
  ApprovalLikelihood?: number | null
  Summary?: string | null
  Findings?: PreReviewFindingDto[] | null
  // What a reviewer would test in the running app: to check before submitting.
  RuntimeChecks?: string[] | null
  PositiveNotes?: string[] | null
  ImageCount?: number
  InputTokens?: number
  OutputTokens?: number
  CacheReadInputTokens?: number
  CacheCreationInputTokens?: number
  EstimatedCostUsd?: number
  ErrorKey?: string | null
  ErrorMessage?: string | null
  CreatedDate: string
  CreatedByUserName?: string | null
  CompletedDate?: string | null
}

export interface PreReviewListDto {
  Id: number
  Store: StoreKind
  VersionString?: string | null
  Status: PreReviewStatus
  Verdict?: PreReviewVerdict | null
  ApprovalLikelihood?: number | null
  Model: string
  BlockerCount: number
  MajorCount: number
  MinorCount: number
  EstimatedCostUsd?: number
  CreatedDate: string
  CreatedByUserName?: string | null
}

// A reply draft written with AI (POST reviews/reply-draft/generate): already saved on the review, nothing sent.
export interface GeneratedReplyDraftDto {
  ReviewId: number
  Body: string
  // The model wrote more than the store accepts and the text was cut after its last full sentence that fits.
  Shortened: boolean
  Model?: string | null
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
  // Channels that write through a code host (winget): the fork owner, the fork (owner/name) and the token kind.
  AccountLogin?: string | null
  Repository?: string | null
  TokenKind?: string | null
  // Channels that address items under a publisher (Chrome Web Store).
  PublisherId?: string | null
}

export interface StoreConnectionDto {
  Id: number
  Store: StoreKind
  Name: string
  /** Icon key a member picked (components/ui/StoreIcons.tsx); null shows the store's text code. */
  Icon?: string | null
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
  /** App detail only: state of the latest known store version (WAITING_FOR_REVIEW...), null when none is known. */
  VersionState?: string | null
  VersionString?: string | null
  VersionBuildNumber?: string | null
  VersionStateDate?: IsoDateTime | null
}

export interface AppListDto {
  Id: number
  Name: string
  IconUrl?: string | null
  CreationDate?: IsoDateTime | null
  /** When a member hid the app from the apps list; null while it is shown. */
  HiddenDate?: IsoDateTime | null
  /** When a member muted the app's alerts (the live board and the notifications leave it out); null while they are on. */
  AlertsMutedDate?: IsoDateTime | null
  /** Place in the apps list a member chose (0 is the first); null for an app never placed. */
  DisplayOrder?: number | null
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
  /** Upload session whose package storeasy still keeps; null when it was deleted or the build reached the store without storeasy. */
  PackageSessionId?: number | null
}

/** A short lived presigned download URL of the package storeasy keeps for a build (BuildDownloadDto). */
export interface BuildDownloadDto {
  BuildId: number
  SessionId: number
  Url: string
  ExpiryDate: IsoDateTime
  FileName: string
  Size: number
  Sha256: string
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

/** One version's last known review state (ActivityBoardVersionDto). State as the store names it (IN_REVIEW, REJECTED). */
export interface ActivityBoardVersionDto {
  AppId: number
  StoreAppId: number
  Store: StoreKind
  VersionString?: string | null
  BuildNumber?: string | null
  State?: string | null
  PreviousState?: string | null
  ChangedDate: IsoDateTime
  SubmittedDate?: IsoDateTime | null
  // The store's id of the record (App Store app id, Microsoft Store product id), for links to the store's console.
  ExternalId?: string | null
}

/** A build still processing at the store, or a valid one within 14 days of its TestFlight expiry. */
export interface ActivityBoardBuildDto {
  AppId: number
  StoreAppId: number
  Store: StoreKind
  Version?: string | null
  BuildNumber?: string | null
  ProcessingState: BuildProcessingState
  UploadedDate?: IsoDateTime | null
  ExpiryDate?: IsoDateTime | null
}

/** One hour of one store: finished jobs and store events (webhooks) received. Empty hours are left out. */
export interface ActivityBoardHourDto {
  Store: StoreKind
  Hour: IsoDateTime
  Succeeded: number
  Failed: number
  Inbound: number
}

/** GET StoreJobs/board: what the live page shows beside the moving jobs. */
export interface ActivityBoardDto {
  GeneratedDate: IsoDateTime
  HoursStartDate: IsoDateTime
  Versions?: ActivityBoardVersionDto[] | null
  Builds?: ActivityBoardBuildDto[] | null
  Hours?: ActivityBoardHourDto[] | null
  /** Apps whose alerts are muted: their versions and builds are already left out; the page leaves out the rest too. */
  MutedAppIds?: number[] | null
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
