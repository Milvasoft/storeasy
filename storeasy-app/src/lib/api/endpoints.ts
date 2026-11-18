import { apiDelete, apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import { getDeviceId } from '@/lib/api/tokenStore'
import type {
  AccountDetailDto,
  InvitationCreatedDto,
  LoginResponseDto,
  MemberListDto,
  MyOrganizationDto,
  OrgRole,
  StoreAuditLogDto,
  StoreConnectionDto,
  StoreKind,
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

// ---- Account: registration and recovery (planned contract) -----------------
// These endpoints are being built on the API now. The contract below is the agreed plan; when the API lands, this block
// and its MSW handlers are the only places to adjust.
export interface RegisterInput {
  Email: string
  Name: string
  Surname: string
  Password: string
  InvitationToken?: string | null
}

export const registrationApi = {
  // 409 EmailExists; 400 with the password policy messages; 403 RegistrationInviteOnly or RegistrationClosed.
  register: (input: RegisterInput) => apiPost<unknown>('/Account/register', input, { auth: false, idempotent: true }),

  // 400 VerificationTokenExpired / VerificationTokenUsed.
  verifyEmail: (token: string) => apiPost<unknown>('/Account/email/verify', { Token: token }, { auth: false }),

  // Always answers the same, whether or not the address has an account; 429 with Retry-After.
  resendVerification: (email: string) => apiPost<unknown>('/Account/email/verify/resend', { Email: email }, { auth: false }),

  // Always 200: the answer never tells whether the address has an account.
  forgotPassword: (email: string) => apiPost<unknown>('/Account/password/forgot', { Email: email }, { auth: false }),

  // 400 ResetTokenInvalid for an expired, used or replaced token. Signs out every device.
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

  // Renames (ManageOrganization). The slug cannot change yet (design 15.20 backend gap).
  update: (organizationId: number, name: string) =>
    apiPut<number>('/Organizations/organization', { OrganizationId: organizationId, Name: name }),

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
  // Apple: JSON {"issuerId","keyId","privateKey"}; Google: the service account JSON file content. Never comes back.
  Secret: string
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
