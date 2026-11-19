import { apiDelete, apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import { getDeviceId } from '@/lib/api/tokenStore'
import type {
  AccountDetailDto,
  AiSettingsDto,
  AccountSessionDto,
  AppAddedDto,
  AppDetailDto,
  AppListDto,
  AssetDownloadDto,
  AssetDto,
  AssetPurpose,
  AssetUploadDto,
  FeatureFlagStateDto,
  GeneratedReplyDraftDto,
  GoogleSignInStartDto,
  InvitationCreatedDto,
  InvitationPreviewDto,
  LoginResponseDto,
  MemberListDto,
  MyOrganizationDto,
  OrganizationRoleInput,
  OrganizationSettingsDto,
  OrgRoleDto,
  PublicSettingsDto,
  RoleChoice,
  SaveWebPushSubscriptionInput,
  StoreAuditLogDto,
  StoreConnectionDto,
  StoreJobDetailDto,
  StoreJobListDto,
  StoreJobStatus,
  StoreKind,
  UpdateOrganizationSettingsInput,
  UpdateProfileInput,
  UploadedPart,
  WebPushSubscriptionDto,
} from '@/lib/api/types'

// ---- Account ---------------------------------------------------------------
export const authApi = {
  // UserName carries the e-mail (K11). 401 with the lockout text when the account is locked; 429 with Retry-After.
  login: (email: string, password: string) =>
    apiPost<LoginResponseDto>('/Account/login', { UserName: email, Password: password, DeviceId: getDeviceId() }, { auth: false }),

  // Ends this device's session only.
  logout: (userName: string) => apiPost<unknown>('/Account/logout', { UserName: userName, DeviceId: getDeviceId() }),

  // The signed-in user's own account (the id is taken from the token server-side).
  accountDetail: () => apiGet<AccountDetailDto>('/Account/detail'),

  // On success the security stamp changes and every other session gets 401 on its next request.
  changePassword: (userName: string, oldPassword: string, newPassword: string) =>
    apiPut<unknown>('/Account/password/change', { UserName: userName, OldPassword: oldPassword, NewPassword: newPassword }),

  // Google sign in (OpenID Connect code flow; the PKCE verifier and the state live on the server). 404 GoogleSignInDisabled.
  googleStart: () => apiPost<GoogleSignInStartDto>('/Account/login/google/start', {}, { auth: false }),

  // The callback's code and state; the same token pair as the password login. An invitation token lets an invite only
  // installation create the account and joins the organization in the same step.
  googleLogin: (code: string, state: string, invitationToken?: string | null) =>
    apiPost<LoginResponseDto>(
      '/Account/login/google',
      { Code: code, State: state, DeviceId: getDeviceId(), InvitationToken: invitationToken || null },
      { auth: false },
    ),

  updateProfile: (input: UpdateProfileInput) => apiPut<number>('/Account/profile', input),

  // The caller's signed in devices; IsCurrent marks this one.
  sessions: () => apiGet<AccountSessionDto[]>('/Account/sessions'),

  // Its tokens stop working and its live connection closes. 404 SessionNotFound.
  revokeSession: (sessionId: number) => apiDelete<number>('/Account/session', undefined, { Id: sessionId }),

  // Answers how many sessions were signed out.
  revokeOtherSessions: () => apiDelete<number>('/Account/sessions/others'),
}

// ---- Account: browser push ---------------------------------------------------
export const pushApi = {
  subscriptions: () => apiGet<WebPushSubscriptionDto[]>('/Account/push/subscriptions'),

  // 404 WebPushDisabled when the instance has no VAPID key pair.
  subscribe: (input: SaveWebPushSubscriptionInput) => apiPost<number>('/Account/push/subscription', input),

  // By the subscription's address (this browser) or its id (another one).
  unsubscribe: (target: { Id?: number | null; Endpoint?: string | null }) =>
    apiDelete<number>('/Account/push/subscription', undefined, { Id: target.Id ?? null, Endpoint: target.Endpoint ?? null }),

  // Answers how many browsers it was queued to; 429 after 5 an hour.
  test: () => apiPost<number>('/Account/push/test', {}),
}

// ---- System ------------------------------------------------------------------
export const systemApi = {
  // Anonymous: registration mode, Google sign in, the version and the web push public key.
  publicSettings: () => apiGet<PublicSettingsDto>('/System/public-settings', undefined, { auth: false }),

  // Anonymous, single team only: creates the administrator and the team with the setup code from the API log and signs
  // in (403 wrong code, 409 already set up).
  completeSetup: (input: CompleteSetupInput) =>
    apiPost<LoginResponseDto>('/System/setup', { ...input, DeviceId: getDeviceId() }, { auth: false }),

  // Every signed in user: whether each server side feature is on.
  featureFlags: () => apiGet<FeatureFlagStateDto[]>('/System/feature-flags'),
}

/** A role choice as the member and invitation commands take it: a seeded role, or Custom with its id. */
function roleFields(choice: RoleChoice) {
  return { Role: choice.role, CustomRoleId: choice.customRoleId }
}

export interface CompleteSetupInput {
  SetupCode: string
  Name: string
  Surname?: string | null
  Email: string
  Password: string
  OrganizationName: string
}

// ---- Account: registration and recovery -----------------------------------
export interface RegisterInput {
  Email: string
  Name: string
  // Optional on the API.
  Surname?: string | null
  Password: string
  // Required when the installation is invite only; accepted in the same step.
  InvitationToken?: string | null
}

export const registrationApi = {
  // Answers the new user id. The account can sign in at once; organizations and connections wait for the verification.
  // 409 User.ExistWithSameEmail; 403 RegistrationClosed / RegistrationInvitationRequired; 400 password policy messages.
  register: (input: RegisterInput) => apiPost<number>('/Account/register', input, { auth: false, idempotent: true }),

  // Anonymous: the link may be opened on another device. 400 AccountTokenInvalid for an expired, used or replaced link.
  verifyEmail: (token: string) => apiPost<unknown>('/Account/email/verify', { Token: token }, { auth: false }),

  // Signed in only: a new link to the caller's own address; the earlier links stop working. 409 EmailAlreadyVerified;
  // 429 with Retry-After (5 an hour).
  resendVerification: () => apiPost<unknown>('/Account/email/verify/resend', {}),

  // The same answer whether or not the address has an account.
  forgotPassword: (email: string) => apiPost<unknown>('/Account/password/forgot', { Email: email }, { auth: false }),

  // 400 AccountTokenInvalid for an expired, used or replaced token. Signs out every device and verifies the e-mail.
  resetPassword: (token: string, newPassword: string) =>
    apiPost<unknown>('/Account/password/reset', { Token: token, NewPassword: newPassword }, { auth: false }),
}

// ---- Organizations ---------------------------------------------------------
export interface CreateOrganizationInput {
  Name: string
  Slug: string
}

export const organizationsApi = {
  // The caller's organizations with the caller's role and permissions in each.
  mine: () => apiGet<MyOrganizationDto[]>('/Organizations/mine'),

  // The caller becomes the owner. 409 OrganizationSlugTaken. Answers the new organization's id.
  create: (input: CreateOrganizationInput) => apiPost<number>('/Organizations/organization', input, { idempotent: true }),

  // Renames and, when a slug is sent, changes the URL key (ManageOrganization). 409 OrganizationSlugTaken; 400
  // OrganizationSlugInvalid. A null slug keeps the current one.
  update: (organizationId: number, name: string, slug?: string | null) =>
    apiPut<number>('/Organizations/organization', { OrganizationId: organizationId, Name: name, Slug: slug || null }),

  // Invitations are filled only for callers with ManageMembers.
  members: (organizationId: number) => apiGet<MemberListDto>('/Organizations/members', { OrganizationId: organizationId }),

  // The token comes back once, for the accept link. 409 OrganizationAlreadyMember; 429 with Retry-After.
  invite: (organizationId: number, email: string, choice: RoleChoice) =>
    apiPost<InvitationCreatedDto>(
      '/Organizations/invitation',
      { OrganizationId: organizationId, Email: email, ...roleFields(choice) },
      { idempotent: true },
    ),

  // Anonymous: organization, inviter, role, masked address and expiry of the link's invitation. 404 for an unknown token.
  invitationPreview: (token: string) =>
    apiGet<InvitationPreviewDto>('/Organizations/invitation/preview', { Token: token }, { auth: false }),

  revokeInvitation: (organizationId: number, invitationId: number) =>
    apiDelete<number>('/Organizations/invitation', undefined, { OrganizationId: organizationId, InvitationId: invitationId }),

  // Answers the organization id the invitation belongs to.
  acceptInvitation: (token: string) => apiPost<number>('/Organizations/invitation/accept', { Token: token }),

  // 400 OrganizationLastOwner when the last owner would step down; 403 when an admin touches an owner.
  changeRole: (organizationId: number, memberId: number, choice: RoleChoice) =>
    apiPut<number>('/Organizations/member/role', { OrganizationId: organizationId, MemberId: memberId, ...roleFields(choice) }),

  // The seeded roles (locked) and the custom ones, with member and pending invitation counts.
  roles: (organizationId: number) => apiGet<OrgRoleDto[]>('/Organizations/roles', { OrganizationId: organizationId }),

  // Permissions the caller holds only, never ManageOrganization. 409 OrganizationRoleNameExists; answers the new id.
  createRole: (organizationId: number, input: OrganizationRoleInput) =>
    apiPost<number>('/Organizations/role', { OrganizationId: organizationId, ...input }, { idempotent: true }),

  updateRole: (organizationId: number, roleId: number, input: OrganizationRoleInput) =>
    apiPut<number>('/Organizations/role', { OrganizationId: organizationId, RoleId: roleId, ...input }),

  // 409 OrganizationRoleInUse while a member or a pending invitation holds it.
  deleteRole: (organizationId: number, roleId: number) =>
    apiDelete<number>('/Organizations/role', undefined, { OrganizationId: organizationId, RoleId: roleId }),

  // Member managers read; viewers get 403.
  settings: (organizationId: number) => apiGet<OrganizationSettingsDto>('/Organizations/settings', { OrganizationId: organizationId }),

  // Owners (ManageOrganization). Only the fields sent change.
  updateSettings: (organizationId: number, input: UpdateOrganizationSettingsInput) =>
    apiPut<number>('/Organizations/settings', { OrganizationId: organizationId, ...input }),

  // Removes a member, or leaves the organization when the id is the caller's own membership.
  removeMember: (organizationId: number, memberId: number) =>
    apiDelete<number>('/Organizations/member', undefined, { OrganizationId: organizationId, MemberId: memberId }),

  // Newest first by default.
  auditLogs: (organizationId: number, request: ListRequest) =>
    apiPatchList<StoreAuditLogDto>('/Organizations/audit-logs', { OrganizationId: organizationId, ...request }),
}

// ---- AI reply drafts -------------------------------------------------------
// The Anthropic API key is sent once when it is saved and never comes back: only whether one is set and its last four
// characters do.
export const aiApi = {
  // Every member reads it (the reviews page needs to know whether a key is set).
  settings: (organizationId: number) => apiGet<AiSettingsDto>('/Organizations/ai-settings', { OrganizationId: organizationId }),

  // ManageConnections. Checked with Anthropic first: a key Anthropic rejects is refused (409 AiKeyRejected); when
  // Anthropic cannot be reached it is saved with ApiKeyVerified false.
  setKey: (organizationId: number, apiKey: string) =>
    apiPut<AiSettingsDto>('/Organizations/ai-settings/key', { OrganizationId: organizationId, ApiKey: apiKey }),

  removeKey: (organizationId: number) => apiDelete<number>('/Organizations/ai-settings/key', undefined, { OrganizationId: organizationId }),

  // ReplyReviews. Writes a draft with Claude and saves it as the review's draft; 409 AiKeyNotSet without a key, 429 past
  // 60 an hour.
  generateReplyDraft: (organizationId: number, reviewId: number) =>
    apiPost<GeneratedReplyDraftDto>('/Reviews/reply-draft/generate', { OrganizationId: organizationId, ReviewId: reviewId }),
}

// ---- Store connections -----------------------------------------------------
export interface AddConnectionInput {
  Store: StoreKind
  Name: string
  // Apple: JSON {"issuerId","keyId","privateKey"}; Google: the service account JSON file content; Microsoft: JSON
  // {"tenantId","clientId","clientSecret","sellerId"}. Never comes back.
  Secret: string
  // Microsoft only, required: when the Entra client secret expires (at most 24 months away).
  CredentialExpiryDate?: string | null
  // Optional icon key: a generic icon or the store's own logo (400 StoreConnectionIconInvalid otherwise).
  Icon?: string | null
}

export const connectionsApi = {
  // Fingerprints only; the secret never leaves the API.
  list: (organizationId: number) => apiGet<StoreConnectionDto[]>('/StoreConnections', { OrganizationId: organizationId }),

  // 400 AppleSecretInvalid / AppleKeyInvalid / GoogleSecretInvalid / GoogleKeyInvalid. Answers the new id.
  create: (organizationId: number, input: AddConnectionInput) =>
    apiPost<number>('/StoreConnections/connection', { OrganizationId: organizationId, ...input }, { idempotent: true }),

  // Checks the key against the store and answers the updated connection. 429 after 20 checks in 10 minutes.
  verify: (organizationId: number, connectionId: number, packageName?: string | null) =>
    apiPost<StoreConnectionDto>('/StoreConnections/connection/verify', {
      OrganizationId: organizationId,
      ConnectionId: connectionId,
      PackageName: packageName?.trim() || null,
    }),

  // Renames the connection and sets or clears its icon; the secret is not touched. Answers the updated connection.
  update: (organizationId: number, connectionId: number, input: { Name: string; Icon: string | null }) =>
    apiPut<StoreConnectionDto>('/StoreConnections/connection', { OrganizationId: organizationId, ConnectionId: connectionId, ...input }),

  // Wipes the encrypted secret; cannot be undone.
  remove: (organizationId: number, connectionId: number) =>
    apiDelete<number>('/StoreConnections/connection', undefined, { OrganizationId: organizationId, ConnectionId: connectionId }),
}

// ---- Apps and builds -------------------------------------------------------
// The apps list request: Hidden lists only the apps a member hid; IncludeHidden lists shown and hidden apps together
// (for name lookups). With neither, hidden apps are left out.
export type AppListRequest = ListRequest & { Hidden?: boolean; IncludeHidden?: boolean }

export const appsApi = {
  // Every store in one list, with the store records of each app.
  // Store narrows the list on the server to the apps with a record in that store.
  list: (organizationId: number, request: AppListRequest, store?: StoreKind | null) =>
    apiPatchList<AppListDto>('/Apps', { OrganizationId: organizationId, ...request, ...(store ? { Store: store } : {}) }),

  // The app with its store records and its newest 100 builds. 404 AppNotFound.
  detail: (organizationId: number, appId: number) => apiGet<AppDetailDto>('/Apps/app', { OrganizationId: organizationId, AppId: appId }),

  // Play has no API that lists apps, so a Google app is added by its package name. The sync job in the answer confirms
  // it, or removes it when Play refuses it. 409 StoreAppAlreadyAdded; 400 PackageNameInvalid / StoreConnectionStoreMismatch.
  addGoogle: (organizationId: number, connectionId: number, packageName: string) =>
    apiPost<AppAddedDto>('/Apps/google', { OrganizationId: organizationId, ConnectionId: connectionId, PackageName: packageName }),

  // Moves a store record to another app (one record per store there; 409 AppStoreAlreadyLinked). Answers the target app.
  link: (organizationId: number, appId: number, storeAppId: number) =>
    apiPost<number>('/Apps/app/link', { OrganizationId: organizationId, AppId: appId, StoreAppId: storeAppId }),

  // Splits a store record off into a new app named after it. Answers the record's app id.
  unlink: (organizationId: number, storeAppId: number) =>
    apiPost<number>('/Apps/app/unlink', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  // Hides the app from the apps list and the overviews, or shows it again. Nothing is deleted; sync keeps it hidden.
  setHidden: (organizationId: number, appId: number, hidden: boolean) =>
    apiPost<number>('/Apps/app/hidden', { OrganizationId: organizationId, AppId: appId, Hidden: hidden }),

  // Sets the order of the apps list: these apps first, in this order; every other app follows by name. Answers the count.
  reorder: (organizationId: number, appIds: number[]) =>
    apiPost<number>('/Apps/app/order', { OrganizationId: organizationId, AppIds: appIds }),

  // Queues a sync for one connection, or every App Store and Play connection when null. Answers the job ids.
  sync: (organizationId: number, connectionId?: number | null) =>
    apiPost<number[]>('/Apps/sync', { OrganizationId: organizationId, ConnectionId: connectionId ?? null }),
}

// ---- Store jobs ------------------------------------------------------------
export const jobsApi = {
  // Newest first; the standard list filters apply to the DTO fields. AppId narrows to one app.
  list: (organizationId: number, request: ListRequest, appId?: number | null) =>
    apiPatchList<StoreJobListDto>('/StoreJobs', { OrganizationId: organizationId, AppId: appId ?? null, ...request }),

  // The job with its steps across attempts. 404 StoreJobNotFound.
  detail: (organizationId: number, jobId: number) =>
    apiGet<StoreJobDetailDto>('/StoreJobs/job', { OrganizationId: organizationId, JobId: jobId }),

  // A queued job stops at once (Cancelled); a running one at its next step (Running). 409 StoreJobAlreadyFinished.
  cancel: (organizationId: number, jobId: number) =>
    apiPost<StoreJobStatus>('/StoreJobs/job/cancel', { OrganizationId: organizationId, JobId: jobId }),
}

// ---- Assets ----------------------------------------------------------------
export interface CreateUploadInput {
  Purpose: AssetPurpose
  FileName: string
  ContentType: string
  Size: number
  Sha256?: string | null
}

export const assetsApi = {
  // Presigned PUT URLs: one for a small file, one per part over 64 MB. 400 AssetTooLarge; 503 StorageNotConfigured.
  createUpload: (organizationId: number, input: CreateUploadInput) =>
    apiPost<AssetUploadDto>('/Assets/upload', { OrganizationId: organizationId, ...input }),

  // Confirms the upload (with the parts' ETags for a multipart one). 409 AssetNotUploaded; 400 AssetSizeMismatch.
  complete: (organizationId: number, assetId: number, parts: UploadedPart[] = []) =>
    apiPost<AssetDto>('/Assets/asset/complete', { OrganizationId: organizationId, AssetId: assetId, Parts: parts }),

  // A short lived presigned GET URL.
  downloadUrl: (organizationId: number, assetId: number) =>
    apiGet<AssetDownloadDto>('/Assets/asset/download-url', { OrganizationId: organizationId, AssetId: assetId }),
}
