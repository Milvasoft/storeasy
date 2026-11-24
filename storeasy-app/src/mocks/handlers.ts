import { delay, http, HttpResponse, type HttpHandler } from 'msw'
import { activityMockHandlers } from './activityMock'
import { appsMockHandlers } from './appsMock'
import { easMockHandlers } from './easMock'
import { registriesMockHandlers } from './registriesMock'
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

// The fake bucket the logo upload PUTs to, and the last uploaded logo (a blob URL of this page).
const MOCK_LOGO_BUCKET = 'https://mock-storage.invalid/logo'
let mockLogo: string | null = null

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
  aiKey: null as { lastFour: string; setDate: string } | null,
  aiModels: { replyDrafts: 'claude-haiku-5-5', preReview: 'claude-sonnet-5-5' },
  preReviews: [] as MockPreReview[],
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

// The mock shows the guided tour invitation on the first visit (this browser remembers the answer too).
let tourPromptSeenDate: string | null = null

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
      tourPromptSeenDate: tourPromptSeenDate,
      roles: [],
    }),
  ),
  http.put(`${BASE}/Account/tour-prompt-seen`, () => {
    tourPromptSeenDate ??= new Date().toISOString()
    return envelope(7)
  }),
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
  // The logo goes to a fake bucket (answered below); the uploaded file becomes the logo as a blob URL of this page.
  http.post(`${BASE}/Organizations/logo/upload`, async ({ request }) => {
    const { OrganizationId } = await body<{ OrganizationId: number }>(request)
    return envelope({ assetId: OrganizationId, uploadUrl: MOCK_LOGO_BUCKET, partSize: null, parts: [], expiryDate: hoursAgo(-1) })
  }),
  http.put(MOCK_LOGO_BUCKET, async ({ request }) => {
    mockLogo = URL.createObjectURL(await request.blob())
    return new HttpResponse(null, { status: 200, headers: { ETag: '"mock-logo"' } })
  }),
  http.put(`${BASE}/Organizations/logo`, async ({ request }) => {
    const { OrganizationId } = await body<{ OrganizationId: number }>(request)
    db.organizations = db.organizations.map((item) => (item.Id === OrganizationId ? { ...item, LogoUrl: mockLogo } : item))
    audit(AuditAction.OrganizationLogoSet, 'Organization logo set.')
    return envelope(mockLogo)
  }),
  http.delete(`${BASE}/Organizations/logo`, async ({ request }) => {
    const { OrganizationId } = await body<{ OrganizationId: number }>(request)
    db.organizations = db.organizations.map((item) => (item.Id === OrganizationId ? { ...item, LogoUrl: null } : item))
    audit(AuditAction.OrganizationLogoRemoved, 'Organization logo removed.')
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
  http.put(`${BASE}/StoreConnections/connection/key`, async ({ request }) => {
    await delay(900)
    const { ConnectionId, Secret } = await body<{ ConnectionId: number; Secret: string }>(request)
    const connection = db.connections.find((item) => item.Id === ConnectionId)
    if (!connection) {
      return refuse('StoreConnectionNotFound', 'Bağlantı bulunamadı.', 404)
    }
    if (connection.Store !== StoreKind.Apple && connection.Store !== StoreKind.Google) {
      return refuse('StoreKeyReplaceNotSupported', 'Burada yalnızca App Store Connect ya da Google Play anahtarı değiştirilir.', 400)
    }
    let parsed: Record<string, string>
    try {
      parsed = JSON.parse(Secret) as Record<string, string>
    } catch {
      return refuse(connection.Store === StoreKind.Apple ? 'AppleSecretInvalid' : 'GoogleSecretInvalid', 'Anahtar okunamadı.', 400)
    }
    const apple = connection.Store === StoreKind.Apple
    if (apple && parsed.issuerId?.slice(-4) !== connection.Fingerprint?.IssuerIdSuffix) {
      return refuse('StoreKeyIssuerMismatch', 'Yeni anahtar bağlantıyla aynı App Store Connect ekibine ait olmalı.', 400)
    }
    const fingerprint = apple
      ? { KeyId: parsed.keyId, IssuerIdSuffix: parsed.issuerId?.slice(-4) }
      : { ServiceAccountEmail: parsed.client_email, ProjectId: parsed.project_id }
    db.connections = db.connections.map((item) =>
      item.Id === ConnectionId
        ? {
            ...item,
            Fingerprint: fingerprint,
            Status: ConnectionStatus.Verified,
            LastVerifiedDate: new Date().toISOString(),
            LastVerifyError: null,
          }
        : item,
    )
    audit(AuditAction.ConnectionKeyReplaced, `${connection.Name} key replaced.`, connection.Store)
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
  ...activityMockHandlers,
  ...appsMockHandlers,
  ...registriesMockHandlers,
  ...easMockHandlers,
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
  // AI reply drafts: the key is kept only as its last four characters, like the API returns it; a key with "rejected" in
  // it is refused the way Anthropic would, and a draft is a fixed text after a short delay.
  http.get(`${BASE}/Organizations/ai-settings`, () => envelope(aiSettings())),
  http.put(`${BASE}/Organizations/ai-settings/key`, async ({ request }) => {
    const { ApiKey } = await body<{ ApiKey: string }>(request)
    await delay(400)
    if (ApiKey.includes('rejected')) {
      return refuse('AiKeyRejected', 'Anthropic rejected the API key. Check it or replace it in Settings, under AI.', 409)
    }
    db.aiKey = { lastFour: ApiKey.trim().slice(-4), setDate: new Date().toISOString() }
    audit(AuditAction.AiKeySet, 'Anthropic API key added.', null)
    return envelope({ ...aiSettings(), apiKeyVerified: true })
  }),
  http.put(`${BASE}/Organizations/ai-settings/models`, async ({ request }) => {
    const input = await body<{ ReplyDraftModel?: string | null; PreReviewModel?: string | null }>(request)
    if (!db.aiKey) {
      return refuse('AiKeyNotSet', 'No Anthropic API key is set for this organization. Add one in Settings, under AI.', 409)
    }
    const known = AI_MODELS.map((model) => model.id)
    if ([input.ReplyDraftModel, input.PreReviewModel].some((model) => model && !known.includes(model))) {
      return refuse('AiModelInvalid', 'Choose one of the offered models.', 400)
    }
    db.aiModels = {
      replyDrafts: input.ReplyDraftModel ?? db.aiModels.replyDrafts,
      preReview: input.PreReviewModel ?? db.aiModels.preReview,
    }
    audit(AuditAction.AiModelsChanged, 'AI models changed.', null)
    return envelope(aiSettings())
  }),
  // AI pre-review: a run is Queued, Running for a few seconds, then a fixed sample report.
  http.get(`${BASE}/pre-reviews`, ({ request }) => {
    const url = new URL(request.url)
    const appId = Number(url.searchParams.get('AppId'))
    const store = Number(url.searchParams.get('Store'))
    return envelope(
      db.preReviews
        .filter((item) => item.appId === appId && item.store === store)
        .map((item) => {
          const report = preReviewView(item)
          return {
            id: report.id,
            store: report.store,
            versionString: report.versionString,
            status: report.status,
            verdict: report.verdict,
            approvalLikelihood: report.approvalLikelihood,
            model: report.model,
            blockerCount: report.findings.filter((finding) => finding.severity === 1).length,
            majorCount: report.findings.filter((finding) => finding.severity === 2).length,
            minorCount: report.findings.filter((finding) => finding.severity === 3).length,
            estimatedCostUsd: report.estimatedCostUsd,
            createdDate: report.createdDate,
            createdByUserName: report.createdByUserName,
          }
        }),
    )
  }),
  http.get(`${BASE}/pre-reviews/pre-review`, ({ request }) => {
    const id = Number(new URL(request.url).searchParams.get('Id'))
    const item = db.preReviews.find((entry) => entry.id === id)
    return item ? envelope(preReviewView(item)) : refuse('PreReviewNotFound', 'The pre-review was not found.', 404)
  }),
  http.post(`${BASE}/pre-reviews/pre-review`, async ({ request }) => {
    const input = await body<{ AppId: number; Store: number }>(request)
    if (!db.aiKey) {
      return refuse('AiKeyNotSet', 'No Anthropic API key is set for this organization. Add one in Settings, under AI.', 409)
    }
    if (db.preReviews.some((item) => item.appId === input.AppId && item.store === input.Store && Date.now() < item.doneAt)) {
      return refuse('PreReviewInProgress', 'A pre-review of this app is already running for this store.', 409)
    }
    const item: MockPreReview = {
      id: db.nextId++,
      appId: input.AppId,
      store: input.Store,
      model: db.aiModels.preReview,
      createdDate: new Date().toISOString(),
      doneAt: Date.now() + 5000,
    }
    db.preReviews = [item, ...db.preReviews]
    audit(AuditAction.PreReviewStarted, 'AI pre-review started.', input.Store as StoreKind)
    return envelope(preReviewView(item))
  }),
  http.delete(`${BASE}/Organizations/ai-settings/key`, () => {
    db.aiKey = null
    audit(AuditAction.AiKeyRemoved, 'Anthropic API key removed.', null)
    return envelope(1)
  }),
  http.post(`${BASE}/Reviews/reply-draft/generate`, async ({ request }) => {
    const { ReviewId } = await body<{ ReviewId: number }>(request)
    if (!db.aiKey) {
      return refuse('AiKeyNotSet', 'No Anthropic API key is set for this organization. Add one in Settings, under AI.', 409)
    }
    await delay(1200)
    return envelope({
      reviewId: ReviewId,
      body: 'Thank you for taking the time to write this. We are sorry the reminders did not arrive; the team will look into it.',
      shortened: false,
      model: 'claude-haiku-5-5',
    })
  }),
]

function aiSettings() {
  return {
    hasApiKey: !!db.aiKey,
    apiKeyLastFour: db.aiKey?.lastFour ?? null,
    apiKeySetDate: db.aiKey?.setDate ?? null,
    apiKeySetByUserName: db.aiKey ? 'demo' : null,
    model: db.aiModels.replyDrafts,
    replyDraftModel: db.aiModels.replyDrafts,
    preReviewModel: db.aiModels.preReview,
    models: AI_MODELS.map((model) => ({
      id: model.id,
      name: model.name,
      inputUsdPerMTok: model.input,
      outputUsdPerMTok: model.output,
    })),
  }
}

const AI_MODELS = [
  { id: 'claude-haiku-5-5', name: 'Claude Haiku 5.5', input: 0.1, output: 0.5 },
  { id: 'claude-sonnet-5-5', name: 'Claude Sonnet 5.5', input: 2, output: 10 },
  { id: 'claude-opus-5-5', name: 'Claude Opus 5.5', input: 4, output: 20 },
]

interface MockPreReview {
  id: number
  appId: number
  store: number
  model: string
  createdDate: string
  // Until this instant the run is queued or running.
  doneAt: number
}

function preReviewView(item: MockPreReview) {
  const now = Date.now()
  const done = now >= item.doneAt
  const findings = done
    ? [
        {
          severity: 1,
          area: 6,
          guideline: item.store === StoreKind.Google ? 'Google Play: Minimum Functionality' : 'App Store Review Guideline 2.1',
          title: 'Sign-in needed but no demo account',
          evidence: 'Review information: sign-in required, no demo account given.',
          fix: 'Add a working demo account to the review information.',
        },
        {
          severity: 2,
          area: 1,
          guideline: item.store === StoreKind.Google ? 'Google Play: Metadata policy' : 'App Store Review Guideline 2.3.7',
          title: 'Price in the subtitle',
          evidence: 'en-US subtitle: "Free route planner"',
          fix: 'Remove the price wording from the subtitle.',
        },
        {
          severity: 3,
          area: 2,
          guideline: 'App Store Review Guideline 2.3.3',
          title: 'Old device frame',
          evidence: 'APP_IPHONE_67, image 3',
          fix: 'Use a current device frame.',
        },
      ]
    : []
  return {
    id: item.id,
    appId: item.appId,
    storeAppId: item.appId * 10 + item.store,
    store: item.store,
    versionString: '2.4',
    status: done ? 3 : now < item.doneAt - 3500 ? 1 : 2,
    jobId: item.id + 50000,
    model: item.model,
    language: 'en-US',
    verdict: done ? 2 : null,
    approvalLikelihood: done ? 62 : null,
    summary: done ? 'The listing is mostly ready, but the reviewer cannot sign in.' : null,
    findings,
    runtimeChecks: done ? ['Deny the location permission and check the app keeps working.'] : [],
    positiveNotes: done ? ['The privacy policy address is filled in.'] : [],
    imageCount: done ? 6 : 0,
    inputTokens: done ? 18200 : 0,
    outputTokens: done ? 2100 : 0,
    cacheReadInputTokens: 0,
    cacheCreationInputTokens: done ? 3000 : 0,
    estimatedCostUsd: done ? 0.0574 : 0,
    errorKey: null,
    errorMessage: null,
    createdDate: item.createdDate,
    createdByUserName: 'demo',
    completedDate: done ? new Date(item.doneAt).toISOString() : null,
  }
}
