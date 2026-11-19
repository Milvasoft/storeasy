import { delay, http, HttpResponse, type HttpHandler } from 'msw'
import { appsMockHandlers } from './appsMock'
import { studioMockHandlers } from './studioMock'
import type { FilterCriteria, ListRequest } from '@/lib/api/list'
import {
  ActionChannel,
  AuditAction,
  ConnectionStatus,
  OrgPermission,
  OrgRole,
  StoreKind,
  UserType,
  type MyOrganizationDto,
  type OrganizationInvitationDto,
  type OrganizationMemberDto,
  type StoreAuditLogDto,
  type StoreConnectionDto,
} from '@/lib/api/types'

// The in-browser fake API for `npm run dev` with VITE_MOCK_API=true: the design's sample data (Kuzey Lab, design brief 5)
// behind the same paths and envelopes as storeasy-api. Kept in memory; a reload starts over. Never in a production build.

const BASE = '*/api/v1.0'

const ALL_PERMISSIONS = Object.values(OrgPermission)
const PERMISSIONS: Record<OrgRole, OrgPermission[]> = {
  [OrgRole.Owner]: ALL_PERMISSIONS,
  [OrgRole.Admin]: ALL_PERMISSIONS.filter((permission) => permission !== OrgPermission.ManageOrganization),
  [OrgRole.Editor]: [OrgPermission.View, OrgPermission.EditContent, OrgPermission.ReleaseTest, OrgPermission.ReplyReviews],
  [OrgRole.Viewer]: [OrgPermission.View],
  // A custom role's own set comes with the membership; View is its floor.
  [OrgRole.Custom]: [OrgPermission.View],
}

function envelope<T>(data: T) {
  return HttpResponse.json({ isSuccess: true, statusCode: 200, messages: [], data })
}

function refuse(key: string, message: string, status: number, headers?: Record<string, string>) {
  return HttpResponse.json({ isSuccess: false, statusCode: status, messages: [{ key, message, type: 3 }], data: null }, { status, headers })
}

function org(id: number, name: string, slug: string, role: OrgRole): MyOrganizationDto {
  return { Id: id, Name: name, Slug: slug, Role: role, Permissions: PERMISSIONS[role] }
}

const now = Date.now()
const hoursAgo = (hours: number) => new Date(now - hours * 3_600_000).toISOString()

const db = {
  organizations: [
    org(7, 'Kuzey Lab', 'kuzey-lab', OrgRole.Owner),
    org(8, 'Ada Stüdyo', 'ada-studyo', OrgRole.Editor),
    org(9, "Deniz'in projeleri", 'deniz', OrgRole.Owner),
  ],
  members: [
    {
      Id: 1,
      UserId: 7,
      Email: 'deniz@kuzeylab.dev',
      Name: 'Deniz',
      Surname: 'Arslan',
      Role: OrgRole.Owner,
      JoinDate: '2026-09-02T09:00:00Z',
    },
    { Id: 2, UserId: 8, Email: 'ece@kuzeylab.dev', Name: 'Ece', Surname: 'Yılmaz', Role: OrgRole.Admin, JoinDate: '2026-09-04T10:30:00Z' },
    { Id: 3, UserId: 9, Email: 'mert@kuzeylab.dev', Name: 'Mert', Surname: 'Kaya', Role: OrgRole.Editor, JoinDate: '2026-10-12T08:15:00Z' },
    {
      Id: 4,
      UserId: 10,
      Email: 'selin@kuzeylab.dev',
      Name: 'Selin',
      Surname: 'Demir',
      Role: OrgRole.Viewer,
      JoinDate: '2026-11-03T13:45:00Z',
    },
  ] as OrganizationMemberDto[],
  invitations: [
    { Id: 31, Email: 'can@kuzeylab.dev', Role: OrgRole.Editor, ExpiryDate: '2026-11-25T12:00:00Z', CreationDate: '2026-11-18T12:00:00Z' },
  ] as OrganizationInvitationDto[],
  connections: [
    {
      Id: 11,
      Store: StoreKind.Apple,
      Name: 'Kuzey Lab ASC',
      Icon: 'appstore',
      Fingerprint: { KeyId: '7QX2M4K9PL', IssuerIdSuffix: 'a91f' },
      Status: ConnectionStatus.Verified,
      LastVerifiedDate: hoursAgo(5),
      CreationDate: '2026-09-02T09:20:00Z',
    },
    {
      Id: 12,
      Store: StoreKind.Google,
      Name: 'Play servis hesabı',
      Icon: 'googleplay',
      Fingerprint: { ServiceAccountEmail: 'storeasy@kuzeylab-play.iam.gserviceaccount.com', ProjectId: 'kuzeylab-play' },
      Status: ConnectionStatus.Unreachable,
      LastVerifiedDate: hoursAgo(30),
      LastVerifyError: 'Google Play yanıt vermedi (503). Anahtar muhtemelen sağlam; birkaç dakika sonra tekrar dene.',
      CreationDate: '2026-09-03T11:00:00Z',
    },
  ] as StoreConnectionDto[],
  audit: [] as StoreAuditLogDto[],
  nextId: 100,
}

// A month of audit rows so paging and filters have something to show.
const SAMPLE_ACTIONS: Array<[AuditAction, string, ActionChannel, StoreKind | null]> = [
  [AuditAction.ConnectionVerified, 'Kuzey Lab ASC verified.', ActionChannel.Web, StoreKind.Apple],
  [AuditAction.MemberInvited, 'can@kuzeylab.dev invited as Editor.', ActionChannel.Web, null],
  [AuditAction.MemberRoleChanged, 'mert@kuzeylab.dev: Viewer -> Editor.', ActionChannel.Web, null],
  [AuditAction.ConnectionAdded, 'Play servis hesabı added.', ActionChannel.Web, StoreKind.Google],
  [AuditAction.MemberJoined, 'selin@kuzeylab.dev joined.', ActionChannel.Web, null],
  [AuditAction.ConnectionVerified, 'Play servis hesabı unreachable.', ActionChannel.System, StoreKind.Google],
  [AuditAction.OrganizationUpdated, 'Name changed.', ActionChannel.Mcp, null],
]
for (let index = 0; index < 46; index += 1) {
  const [action, summary, channel, store] = SAMPLE_ACTIONS[index % SAMPLE_ACTIONS.length]
  db.audit.push({
    Id: 90_400 + index,
    Date: hoursAgo(index * 13 + 1),
    UserName: index % 3 === 0 ? 'ece@kuzeylab.dev' : 'deniz@kuzeylab.dev',
    Channel: channel,
    Action: action,
    Store: store,
    TargetId: String(10 + (index % 5)),
    Summary: summary,
    Details: index % 2 === 0 ? JSON.stringify({ before: 'Viewer', after: 'Editor' }) : undefined,
  })
}

function audit(action: AuditAction, summary: string, store: StoreKind | null = null) {
  db.audit.unshift({
    Id: (db.nextId += 1),
    Date: new Date().toISOString(),
    UserName: 'deniz@kuzeylab.dev',
    Channel: ActionChannel.Web,
    Action: action,
    Store: store,
    TargetId: null,
    Summary: summary,
  })
}

function matches(row: StoreAuditLogDto, criteria: FilterCriteria): boolean {
  const value = (row as unknown as Record<string, unknown>)[criteria.FilterBy]
  if (criteria.FilterBy === 'Date') {
    return String(value) >= String(criteria.Value) && String(value) <= String(criteria.OtherValue)
  }
  if (criteria.FilterBy === 'UserName') {
    return String(value ?? '')
      .toLowerCase()
      .includes(String(criteria.Value).toLowerCase())
  }
  return value === criteria.Value
}

async function body<T>(request: Request): Promise<T> {
  return (await request.json().catch(() => ({}))) as T
}

const loginToken = { accessToken: 'mock-access', refreshToken: 'mock-refresh', expiresIn: 3600, refreshTokenExpiresIn: 86_400 }

export const mockHandlers: HttpHandler[] = [
  http.post(`${BASE}/Account/login`, async ({ request }) => {
    await delay(300)
    const { Password } = await body<{ Password?: string }>(request)
    if (Password === 'wrong') {
      return refuse('Unauthorized', 'E-posta veya şifre hatalı.', 401)
    }
    if (Password === 'busy') {
      return refuse('TooManyRequests', 'Çok fazla deneme yapıldı.', 429, { 'Retry-After': '42' })
    }
    return envelope({ id: 7, userType: UserType.Manager, token: loginToken })
  }),
  http.post(`${BASE}/Account/login/refresh`, () => envelope({ id: 7, userType: UserType.Manager, token: loginToken })),
  http.post(`${BASE}/Account/logout`, () => envelope(null)),
  http.get(`${BASE}/Account/detail`, () =>
    envelope({
      id: 7,
      userName: 'deniz@kuzeylab.dev',
      email: 'deniz@kuzeylab.dev',
      emailConfirmed: true,
      name: 'Deniz',
      surname: 'Arslan',
      roles: [],
    }),
  ),
  http.put(`${BASE}/Account/password/change`, async () => {
    await delay(300)
    return envelope(null)
  }),
  http.post(`${BASE}/Account/register`, async () => {
    await delay(400)
    return envelope(7)
  }),
  http.post(`${BASE}/Account/email/verify`, () => envelope(null)),
  http.post(`${BASE}/Account/email/verify/resend`, () => envelope(null)),
  http.post(`${BASE}/Account/password/forgot`, () => envelope(null)),
  http.post(`${BASE}/Account/password/reset`, () => envelope(null)),

  http.get(`${BASE}/Organizations/mine`, () => envelope(db.organizations)),
  http.post(`${BASE}/Organizations/organization`, async ({ request }) => {
    const { Name, Slug } = await body<{ Name: string; Slug: string }>(request)
    if (db.organizations.some((item) => item.Slug === Slug)) {
      return refuse('OrganizationSlugTaken', `${Slug} başka bir organizasyonda.`, 409)
    }
    const id = (db.nextId += 1)
    db.organizations.push(org(id, Name, Slug, OrgRole.Owner))
    return envelope(id)
  }),
  http.put(`${BASE}/Organizations/organization`, async ({ request }) => {
    const { OrganizationId, Name, Slug } = await body<{ OrganizationId: number; Name: string; Slug?: string | null }>(request)
    if (Slug && db.organizations.some((item) => item.Slug === Slug && item.Id !== OrganizationId)) {
      return refuse('OrganizationSlugTaken', 'Bu kısa ad kullanımda.', 409)
    }
    db.organizations = db.organizations.map((item) => (item.Id === OrganizationId ? { ...item, Name, Slug: Slug || item.Slug } : item))
    audit(AuditAction.OrganizationUpdated, `Renamed to ${Name}.`)
    return envelope(OrganizationId)
  }),
  http.get(`${BASE}/Organizations/members`, async () => {
    await delay(250)
    return envelope({ members: db.members, invitations: db.invitations })
  }),
  http.post(`${BASE}/Organizations/invitation`, async ({ request }) => {
    const { Email, Role } = await body<{ Email: string; Role: OrgRole }>(request)
    if (db.members.some((member) => member.Email === Email)) {
      return refuse('OrganizationAlreadyMember', `${Email} zaten üye.`, 409)
    }
    db.invitations = db.invitations.filter((item) => item.Email !== Email)
    const invitation = {
      Id: (db.nextId += 1),
      Email,
      Role,
      ExpiryDate: new Date(Date.now() + 7 * 86_400_000).toISOString(),
      CreationDate: new Date().toISOString(),
    }
    db.invitations.push(invitation)
    audit(AuditAction.MemberInvited, `${Email} invited.`)
    return envelope({
      id: invitation.Id,
      token: `inv-${invitation.Id}-${Math.random().toString(36).slice(2, 10)}`,
      expiryDate: invitation.ExpiryDate,
    })
  }),
  http.delete(`${BASE}/Organizations/invitation`, async ({ request }) => {
    const { InvitationId } = await body<{ InvitationId: number }>(request)
    db.invitations = db.invitations.filter((item) => item.Id !== InvitationId)
    audit(AuditAction.InvitationRevoked, 'Invitation revoked.')
    return envelope(InvitationId)
  }),
  http.post(`${BASE}/Organizations/invitation/accept`, () => envelope(8)),
  http.put(`${BASE}/Organizations/member/role`, async ({ request }) => {
    const { MemberId, Role } = await body<{ MemberId: number; Role: OrgRole }>(request)
    db.members = db.members.map((member) => (member.Id === MemberId ? { ...member, Role } : member))
    audit(AuditAction.MemberRoleChanged, 'Role changed.')
    return envelope(MemberId)
  }),
  http.delete(`${BASE}/Organizations/member`, async ({ request }) => {
    const { MemberId } = await body<{ MemberId: number }>(request)
    db.members = db.members.filter((member) => member.Id !== MemberId)
    audit(AuditAction.MemberRemoved, 'Member removed.')
    return envelope(MemberId)
  }),
  http.patch(`${BASE}/Organizations/audit-logs`, async ({ request }) => {
    await delay(250)
    const list = await body<ListRequest>(request)
    const rows = db.audit.filter((row) => (list.Filtering?.Criterias ?? []).every((criteria) => matches(row, criteria)))
    const start = (list.PageNumber - 1) * list.RowCount
    return HttpResponse.json({
      isSuccess: true,
      statusCode: 200,
      messages: [],
      data: rows.slice(start, start + list.RowCount),
      currentPageNumber: list.PageNumber,
      totalPageCount: Math.max(1, Math.ceil(rows.length / list.RowCount)),
      totalDataCount: rows.length,
    })
  }),

  http.get(`${BASE}/StoreConnections`, async () => {
    await delay(250)
    return envelope(db.connections)
  }),
  http.post(`${BASE}/StoreConnections/connection`, async ({ request }) => {
    const { Store, Name, Secret, Icon } = await body<{ Store: StoreKind; Name: string; Secret: string; Icon?: string | null }>(request)
    const id = (db.nextId += 1)
    let fingerprint = {}
    try {
      const parsed = JSON.parse(Secret) as Record<string, string>
      fingerprint =
        Store === StoreKind.Apple
          ? { KeyId: parsed.keyId, IssuerIdSuffix: parsed.issuerId?.slice(-4) }
          : { ServiceAccountEmail: parsed.client_email, ProjectId: parsed.project_id }
    } catch {
      return refuse(Store === StoreKind.Apple ? 'AppleSecretInvalid' : 'GoogleSecretInvalid', 'Anahtar okunamadı.', 400)
    }
    db.connections.push({
      Id: id,
      Store,
      Name,
      Icon: Icon || null,
      Fingerprint: fingerprint,
      Status: ConnectionStatus.NotVerified,
      CreationDate: new Date().toISOString(),
    })
    audit(AuditAction.ConnectionAdded, `${Name} added.`, Store)
    return envelope(id)
  }),
  http.put(`${BASE}/StoreConnections/connection`, async ({ request }) => {
    const { ConnectionId, Name, Icon } = await body<{ ConnectionId: number; Name: string; Icon: string | null }>(request)
    const connection = db.connections.find((item) => item.Id === ConnectionId)
    if (!connection) {
      return refuse('StoreConnectionNotFound', 'Bağlantı bulunamadı.', 404)
    }
    const logos: Record<string, StoreKind> = { appstore: StoreKind.Apple, googleplay: StoreKind.Google, msstore: StoreKind.Microsoft }
    if (Icon && Icon in logos && logos[Icon] !== connection.Store) {
      return refuse('StoreConnectionIconInvalid', 'Bilinmeyen bağlantı simgesi ya da başka bir mağazanın logosu seçildi.', 400)
    }
    db.connections = db.connections.map((item) => (item.Id === ConnectionId ? { ...item, Name: Name.trim(), Icon: Icon || null } : item))
    audit(AuditAction.ConnectionUpdated, `${Name} updated.`, connection.Store)
    return envelope(db.connections.find((item) => item.Id === ConnectionId))
  }),
  http.post(`${BASE}/StoreConnections/connection/verify`, async ({ request }) => {
    await delay(900)
    const { ConnectionId } = await body<{ ConnectionId: number }>(request)
    db.connections = db.connections.map((item) =>
      item.Id === ConnectionId
        ? { ...item, Status: ConnectionStatus.Verified, LastVerifiedDate: new Date().toISOString(), LastVerifyError: null }
        : item,
    )
    const connection = db.connections.find((item) => item.Id === ConnectionId)
    audit(AuditAction.ConnectionVerified, `${connection?.Name} verified.`, connection?.Store ?? null)
    return envelope(connection)
  }),
  ...studioMockHandlers,
  ...appsMockHandlers,
  // Phase 8: instance settings, server feature flags, organization roles and settings, sessions and push.
  http.get(`${BASE}/System/public-settings`, () =>
    envelope({ registrationMode: 1, googleSignInEnabled: false, version: '1.0.0-dev', webPushPublicKey: null }),
  ),
  http.get(`${BASE}/System/feature-flags`, () =>
    envelope([
      { key: 'Export', isEnabled: true, description: 'Excel export' },
      { key: 'Studio.AiCaptions', isEnabled: false, description: 'AI caption suggestions' },
    ]),
  ),
  http.get(`${BASE}/Organizations/roles`, () =>
    envelope(
      [OrgRole.Owner, OrgRole.Admin, OrgRole.Editor, OrgRole.Viewer].map((role) => ({
        id: null,
        role,
        name: String(role),
        description: null,
        permissions: PERMISSIONS[role],
        memberCount: db.members.filter((member) => member.Role === role).length,
        invitationCount: db.invitations.filter((invitation) => invitation.Role === role).length,
        isLocked: true,
      })),
    ),
  ),
  http.get(`${BASE}/Organizations/settings`, () =>
    envelope({
      auditRetentionDays: 365,
      artifactRetentionDays: 30,
      seenNotificationRetentionDays: 30,
      unseenNotificationRetentionDays: 90,
      defaultLocale: 'tr-TR',
    }),
  ),
  http.get(`${BASE}/Account/sessions`, () =>
    envelope([
      {
        id: 1,
        deviceId: 'this-browser',
        userAgent: navigator.userAgent,
        ipAddress: '127.0.0.1',
        lastSeenDate: hoursAgo(0),
        expiryDate: hoursAgo(-24 * 7),
        isCurrent: true,
      },
    ]),
  ),
  http.get(`${BASE}/Account/push/subscriptions`, () => envelope([])),
  http.delete(`${BASE}/StoreConnections/connection`, async ({ request }) => {
    const { ConnectionId } = await body<{ ConnectionId: number }>(request)
    const connection = db.connections.find((item) => item.Id === ConnectionId)
    db.connections = db.connections.filter((item) => item.Id !== ConnectionId)
    audit(AuditAction.ConnectionRemoved, `${connection?.Name} removed.`, connection?.Store ?? null)
    return envelope(ConnectionId)
  }),
]
