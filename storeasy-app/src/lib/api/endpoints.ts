import { apiDelete, apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import { getDeviceId } from '@/lib/api/tokenStore'
import type {
  AccountDetailDto,
  AppAddedDto,
  AppDetailDto,
  AppListDto,
  AssetDownloadDto,
  AssetDto,
  AssetPurpose,
  AssetUploadDto,
  InvitationCreatedDto,
  LoginResponseDto,
  MemberListDto,
  MyOrganizationDto,
  OrgRole,
  StoreAuditLogDto,
  StoreConnectionDto,
  StoreJobDetailDto,
  StoreJobListDto,
  StoreJobStatus,
  StoreKind,
  UploadedPart,
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
  invite: (organizationId: number, email: string, role: OrgRole) =>
    apiPost<InvitationCreatedDto>(
      '/Organizations/invitation',
      { OrganizationId: organizationId, Email: email, Role: role },
      { idempotent: true },
    ),

  revokeInvitation: (organizationId: number, invitationId: number) =>
    apiDelete<number>('/Organizations/invitation', undefined, { OrganizationId: organizationId, InvitationId: invitationId }),

  // Answers the organization id the invitation belongs to.
  acceptInvitation: (token: string) => apiPost<number>('/Organizations/invitation/accept', { Token: token }),

  // 400 OrganizationLastOwner when the last owner would step down; 403 when an admin touches an owner.
  changeRole: (organizationId: number, memberId: number, role: OrgRole) =>
    apiPut<number>('/Organizations/member/role', { OrganizationId: organizationId, MemberId: memberId, Role: role }),

  // Removes a member, or leaves the organization when the id is the caller's own membership.
  removeMember: (organizationId: number, memberId: number) =>
    apiDelete<number>('/Organizations/member', undefined, { OrganizationId: organizationId, MemberId: memberId }),

  // Newest first by default.
  auditLogs: (organizationId: number, request: ListRequest) =>
    apiPatchList<StoreAuditLogDto>('/Organizations/audit-logs', { OrganizationId: organizationId, ...request }),
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

  // Wipes the encrypted secret; cannot be undone.
  remove: (organizationId: number, connectionId: number) =>
    apiDelete<number>('/StoreConnections/connection', undefined, { OrganizationId: organizationId, ConnectionId: connectionId }),
}

// ---- Apps and builds -------------------------------------------------------
export const appsApi = {
  // Every store in one list, with the store records of each app.
  list: (organizationId: number, request: ListRequest) => apiPatchList<AppListDto>('/Apps', { OrganizationId: organizationId, ...request }),

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
