import {
  ActionChannel,
  AuditAction,
  ConnectionStatus,
  OrgPermission,
  OrgRole,
  StoreKind,
  UserType,
  type AccountDetailDto,
  type LoginResponseDto,
  type MemberListDto,
  type MyOrganizationDto,
  type OrganizationInvitationDto,
  type OrganizationMemberDto,
  type StoreAuditLogDto,
  type StoreConnectionDto,
} from '@/lib/api/types'

/**
 * Builders for the API's DTOs: each returns a complete, valid object and takes an override patch, so a test names only
 * the fields it is about. Typed with the app's own DTO types: a renamed field breaks the tests at compile time.
 */
export function make<T>(base: T, overrides: Partial<T> = {}): T {
  return { ...base, ...overrides }
}

export const TEST_USER_ID = 7
export const TEST_USER_NAME = 'deniz@kuzeylab.dev'
export const TEST_ORG_ID = 7

const ALL = Object.values(OrgPermission)
export const PERMISSIONS_OF: Record<OrgRole, OrgPermission[]> = {
  [OrgRole.Owner]: ALL,
  [OrgRole.Admin]: ALL.filter((permission) => permission !== OrgPermission.ManageOrganization),
  [OrgRole.Editor]: [OrgPermission.View, OrgPermission.EditContent, OrgPermission.ReleaseTest, OrgPermission.ReplyReviews],
  [OrgRole.Viewer]: [OrgPermission.View],
}

export function loginResponse(overrides: Partial<LoginResponseDto> = {}): LoginResponseDto {
  return make<LoginResponseDto>(
    {
      Id: TEST_USER_ID,
      UserType: UserType.AppUser,
      Token: { AccessToken: 'access-token', RefreshToken: 'refresh-token', ExpiresIn: 3600, RefreshTokenExpiresIn: 86_400 },
    },
    overrides,
  )
}

export function accountDetail(overrides: Partial<AccountDetailDto> = {}): AccountDetailDto {
  return make<AccountDetailDto>(
    { Id: TEST_USER_ID, UserName: TEST_USER_NAME, Email: TEST_USER_NAME, Name: 'Deniz', Surname: 'Arslan', Roles: [] },
    overrides,
  )
}

export function organization(overrides: Partial<MyOrganizationDto> = {}): MyOrganizationDto {
  const role = overrides.Role ?? OrgRole.Owner
  return make<MyOrganizationDto>(
    { Id: TEST_ORG_ID, Name: 'Kuzey Lab', Slug: 'kuzey-lab', Role: role, Permissions: PERMISSIONS_OF[role] },
    overrides,
  )
}

export function member(overrides: Partial<OrganizationMemberDto> = {}): OrganizationMemberDto {
  return make<OrganizationMemberDto>(
    {
      Id: 1,
      UserId: TEST_USER_ID,
      Email: TEST_USER_NAME,
      Name: 'Deniz',
      Surname: 'Arslan',
      Role: OrgRole.Owner,
      JoinDate: '2026-09-02T09:00:00Z',
    },
    overrides,
  )
}

export function invitation(overrides: Partial<OrganizationInvitationDto> = {}): OrganizationInvitationDto {
  return make<OrganizationInvitationDto>(
    { Id: 31, Email: 'can@kuzeylab.dev', Role: OrgRole.Editor, ExpiryDate: '2099-11-25T12:00:00Z', CreationDate: '2026-11-18T12:00:00Z' },
    overrides,
  )
}

export function memberList(overrides: Partial<MemberListDto> = {}): MemberListDto {
  return make<MemberListDto>(
    {
      Members: [
        member(),
        member({ Id: 2, UserId: 8, Email: 'ece@kuzeylab.dev', Name: 'Ece', Surname: 'Yılmaz', Role: OrgRole.Admin }),
        member({ Id: 3, UserId: 9, Email: 'mert@kuzeylab.dev', Name: 'Mert', Surname: 'Kaya', Role: OrgRole.Editor }),
      ],
      Invitations: [invitation()],
    },
    overrides,
  )
}

export function connection(overrides: Partial<StoreConnectionDto> = {}): StoreConnectionDto {
  return make<StoreConnectionDto>(
    {
      Id: 11,
      Store: StoreKind.Apple,
      Name: 'Kuzey Lab ASC',
      Fingerprint: { KeyId: '7QX2M4K9PL', IssuerIdSuffix: 'a91f' },
      Status: ConnectionStatus.Verified,
      LastVerifiedDate: '2026-11-18T09:12:00Z',
      CreationDate: '2026-09-02T09:20:00Z',
    },
    overrides,
  )
}

export function auditLog(overrides: Partial<StoreAuditLogDto> = {}): StoreAuditLogDto {
  return make<StoreAuditLogDto>(
    {
      Id: 90412,
      Date: '2026-11-17T08:12:00Z',
      UserName: 'ece@kuzeylab.dev',
      Channel: ActionChannel.Web,
      Action: AuditAction.MemberInvited,
      Store: null,
      TargetId: '31',
      Summary: 'can@kuzeylab.dev invited.',
      Details: JSON.stringify({ role: 'Editor' }),
    },
    overrides,
  )
}

// A real Apple key shape (not a real key): PEM header and footer around base64.
export const SAMPLE_P8 = '-----BEGIN PRIVATE KEY-----\nMIGTAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBHkwdwIBAQQg\n-----END PRIVATE KEY-----'
export const SAMPLE_ISSUER = '69a6de7e-1234-4abc-9def-0123456aa91f'
export const SAMPLE_SERVICE_ACCOUNT = JSON.stringify({
  type: 'service_account',
  project_id: 'kuzeylab-play',
  client_email: 'storeasy@kuzeylab-play.iam.gserviceaccount.com',
  private_key: '-----BEGIN PRIVATE KEY-----\nabc\n-----END PRIVATE KEY-----\n',
})
