import type { ActivityEvent } from '@/lib/activity/events'
import {
  ActionChannel,
  AssetPurpose,
  AssetStatus,
  AuditAction,
  BuildProcessingState,
  ConnectionStatus,
  OrgPermission,
  InvitationStatus,
  OrgRole,
  RegistrationMode,
  StepDirection,
  StoreJobKind,
  StoreJobStatus,
  StoreJobStepStatus,
  StoreKind,
  UserType,
  type AccountDetailDto,
  type AccountSessionDto,
  type InvitationPreviewDto,
  type AppDetailDto,
  type AppListDto,
  type AppStoreRecordDto,
  type AssetDto,
  type BuildDto,
  type LoginResponseDto,
  type MemberListDto,
  type MyOrganizationDto,
  type OrganizationInvitationDto,
  type OrganizationMemberDto,
  type OrganizationSettingsDto,
  type OrgRoleDto,
  type PublicSettingsDto,
  type StoreAuditLogDto,
  type StoreConnectionDto,
  type StoreJobDetailDto,
  type StoreJobListDto,
  type StoreJobStepDto,
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
  // A custom role's own set comes with the membership; View is its floor.
  [OrgRole.Custom]: [OrgPermission.View],
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
    {
      Id: TEST_USER_ID,
      UserName: TEST_USER_NAME,
      Email: TEST_USER_NAME,
      EmailConfirmed: true,
      Name: 'Deniz',
      Surname: 'Arslan',
      Roles: [],
    },
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

export function storeRecord(overrides: Partial<AppStoreRecordDto> = {}): AppStoreRecordDto {
  return make<AppStoreRecordDto>(
    {
      StoreAppId: 201,
      Store: StoreKind.Apple,
      ExternalId: '6740056789',
      BundleId: 'dev.kuzeylab.pusula',
      Name: 'Pusula',
      StoreConnectionId: 11,
      SyncedDate: '2026-11-18T09:12:00Z',
    },
    overrides,
  )
}

export function app(overrides: Partial<AppListDto> = {}): AppListDto {
  return make<AppListDto>(
    {
      Id: 101,
      Name: 'Pusula',
      IconUrl: null,
      CreationDate: '2026-09-02T09:30:00Z',
      Stores: [
        storeRecord(),
        storeRecord({ StoreAppId: 202, Store: StoreKind.Google, ExternalId: 'dev.kuzeylab.pusula', StoreConnectionId: 12 }),
      ],
    },
    overrides,
  )
}

export function build(overrides: Partial<BuildDto> = {}): BuildDto {
  return make<BuildDto>(
    {
      Id: 9001,
      StoreAppId: 201,
      Store: StoreKind.Apple,
      ExternalId: 'b-9001',
      Version: '2.3.0',
      BuildNumber: '412',
      ProcessingState: BuildProcessingState.Valid,
      Tracks: null,
      MinOsVersion: '16.0',
      UploadedDate: '2026-11-17T15:00:00Z',
      ExpiryDate: '2027-02-15T15:00:00Z',
    },
    overrides,
  )
}

export function appDetail(overrides: Partial<AppDetailDto> = {}): AppDetailDto {
  return make<AppDetailDto>(
    {
      ...app(),
      Builds: [
        build({ Id: 9002, BuildNumber: '413', ProcessingState: BuildProcessingState.Processing, UploadedDate: '2026-11-18T08:00:00Z' }),
        build(),
        build({
          Id: 9003,
          StoreAppId: 202,
          Store: StoreKind.Google,
          BuildNumber: '42',
          Tracks: 'internal',
          ExpiryDate: null,
          UploadedDate: '2026-11-16T10:00:00Z',
        }),
      ],
    },
    overrides,
  )
}

export function job(overrides: Partial<StoreJobListDto> = {}): StoreJobListDto {
  return make<StoreJobListDto>(
    {
      Id: 48213,
      Kind: StoreJobKind.SyncApps,
      Status: StoreJobStatus.Succeeded,
      Store: StoreKind.Apple,
      AppId: null,
      Channel: ActionChannel.Web,
      RequestedByUserName: 'ece@kuzeylab.dev',
      Attempt: 1,
      MaxAttempts: 3,
      CancelRequested: false,
      Summary: '5 apps, 3 new builds.',
      Error: null,
      CreationDate: '2026-11-18T09:00:00Z',
      StartDate: '2026-11-18T09:00:02Z',
      EndDate: '2026-11-18T09:01:08Z',
    },
    overrides,
  )
}

export function jobStep(overrides: Partial<StoreJobStepDto> = {}): StoreJobStepDto {
  return make<StoreJobStepDto>(
    {
      Order: 1,
      Attempt: 1,
      Name: 'apps.list',
      Direction: StepDirection.Outbound,
      Status: StoreJobStepStatus.Succeeded,
      Progress: null,
      Total: null,
      Message: '5 apps.',
      StartDate: '2026-11-18T09:00:03Z',
      EndDate: '2026-11-18T09:00:05Z',
    },
    overrides,
  )
}

export function jobDetail(overrides: Partial<StoreJobDetailDto> = {}): StoreJobDetailDto {
  return make<StoreJobDetailDto>(
    {
      ...job(),
      Payload: JSON.stringify({ ConnectionId: 11 }),
      Result: null,
      NextAttemptDate: '2026-11-18T09:00:00Z',
      Steps: [
        jobStep(),
        jobStep({ Order: 2, Name: 'apps.save', Direction: StepDirection.Internal, Message: '5 records saved.' }),
        jobStep({ Order: 3, Name: 'builds.list', Progress: 5, Total: 5, Message: '42 builds.' }),
      ],
    },
    overrides,
  )
}

export function asset(overrides: Partial<AssetDto> = {}): AssetDto {
  return make<AssetDto>(
    {
      Id: 501,
      Purpose: AssetPurpose.Screenshot,
      FileName: '01-home.png',
      ContentType: 'image/png',
      Size: 4,
      Sha256: null,
      Status: AssetStatus.Uploaded,
      CreationDate: '2026-11-18T10:00:00Z',
      UploadDate: '2026-11-18T10:00:02Z',
    },
    overrides,
  )
}

/** One hub event of the test organization, as the app reads it (ActivityEventDto, pascalized). */
export function activityEvent(overrides: Partial<ActivityEvent> & Pick<ActivityEvent, 'Type'>): ActivityEvent {
  return { OrganizationId: TEST_ORG_ID, Date: '2026-11-18T09:12:00Z', ...overrides }
}

// ---- Phase 8: organization roles, instance settings, sessions -------------------------------------------------------

export function orgRole(overrides: Partial<OrgRoleDto> = {}): OrgRoleDto {
  const role = overrides.Role ?? OrgRole.Custom
  return make<OrgRoleDto>(
    {
      Id: role === OrgRole.Custom ? 14 : null,
      Role: role,
      Name: role === OrgRole.Custom ? 'Yayın sorumlusu' : String(role),
      Description: null,
      Permissions: role === OrgRole.Custom ? [OrgPermission.View, OrgPermission.ReleaseTest] : PERMISSIONS_OF[role],
      MemberCount: 0,
      InvitationCount: 0,
      IsLocked: role !== OrgRole.Custom,
      CreationDate: '2026-11-10T09:00:00Z',
    },
    overrides,
  )
}

/** The seeded roles as GET organizations/roles lists them, then one custom role. */
export function orgRoles(): OrgRoleDto[] {
  return [
    orgRole({ Role: OrgRole.Owner, Name: 'Owner', MemberCount: 1 }),
    orgRole({ Role: OrgRole.Admin, Name: 'Admin', MemberCount: 1 }),
    orgRole({ Role: OrgRole.Editor, Name: 'Editor', MemberCount: 1, InvitationCount: 1 }),
    orgRole({ Role: OrgRole.Viewer, Name: 'Viewer' }),
    orgRole(),
  ]
}

export function publicSettings(overrides: Partial<PublicSettingsDto> = {}): PublicSettingsDto {
  return make<PublicSettingsDto>(
    { RegistrationMode: RegistrationMode.Open, GoogleSignInEnabled: false, Version: '1.0.0', WebPushPublicKey: null },
    overrides,
  )
}

export function accountSession(overrides: Partial<AccountSessionDto> = {}): AccountSessionDto {
  return make<AccountSessionDto>(
    {
      Id: 501,
      DeviceId: 'this-device',
      UserAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0 Safari/537.36',
      IpAddress: '88.241.10.4',
      LastSeenDate: '2026-11-18T09:00:00Z',
      ExpiryDate: '2026-11-25T09:00:00Z',
      IsCurrent: true,
    },
    overrides,
  )
}

export function orgSettings(overrides: Partial<OrganizationSettingsDto> = {}): OrganizationSettingsDto {
  return make<OrganizationSettingsDto>(
    {
      AuditRetentionDays: 365,
      ArtifactRetentionDays: 30,
      SeenNotificationRetentionDays: 30,
      UnseenNotificationRetentionDays: 90,
      DefaultLocale: 'tr-TR',
    },
    overrides,
  )
}

export function invitationPreview(overrides: Partial<InvitationPreviewDto> = {}): InvitationPreviewDto {
  return make<InvitationPreviewDto>(
    {
      Status: InvitationStatus.Pending,
      OrganizationName: 'Kuzey Lab',
      OrganizationSlug: 'kuzey-lab',
      InviterName: 'Deniz Arslan',
      Role: OrgRole.Editor,
      CustomRoleName: null,
      Permissions: PERMISSIONS_OF[OrgRole.Editor],
      MaskedEmail: 'c**@kuzeylab.dev',
      ExpiryDate: '2099-11-25T12:00:00Z',
    },
    overrides,
  )
}
