import type {
  AccountDetailDto,
  AccountSessionDto,
  FeatureFlagStateDto,
  InvitationPreviewDto,
  OrganizationSettingsDto,
  OrgRoleDto,
  PublicSettingsDto,
  WebPushSubscriptionDto,
  AppDetailDto,
  AppListDto,
  MemberListDto,
  MyOrganizationDto,
  StoreAuditLogDto,
  StoreConnectionDto,
  StoreJobDetailDto,
  StoreJobListDto,
} from '@/lib/api/types'
import type { AiSettingsDto } from '@/lib/api/types'
import {
  accountDetail,
  accountSession,
  app,
  appDetail,
  auditLog,
  invitationPreview,
  connection,
  job,
  jobDetail,
  memberList,
  organization,
  orgRoles,
  orgSettings,
  publicSettings,
} from './factories'

/**
 * The fake backend's data. Default handlers read from here, so a test changes what the API returns by assigning a field
 * (testState.connections = [] for an empty list) instead of restating a handler. Reset before every test.
 */
export interface TestState {
  account: AccountDetailDto
  organizations: MyOrganizationDto[]
  members: MemberListDto
  connections: StoreConnectionDto[]
  auditLogs: StoreAuditLogDto[]
  apps: AppListDto[]
  appDetail: AppDetailDto
  jobs: StoreJobListDto[]
  jobDetail: StoreJobDetailDto
  /** Job ids POST apps/sync answers. */
  syncJobIds: number[]
  /** Phase 8: anonymous instance settings, the server flags (none by default: the client defaults apply). */
  publicSettings: PublicSettingsDto
  featureFlags: FeatureFlagStateDto[]
  orgRoles: OrgRoleDto[]
  orgSettings: OrganizationSettingsDto
  /** Null answers 404 (an unknown token). */
  invitationPreview: InvitationPreviewDto | null
  sessions: AccountSessionDto[]
  pushSubscriptions: WebPushSubscriptionDto[]
  /** organizations/ai-settings (a key is set by default). */
  aiSettings: AiSettingsDto
  /** The text reviews/reply-draft/generate answers with. */
  aiDraft: string
  /** Every request the handlers saw, so a test can assert what the app sent. */
  requests: Array<{ method: string; path: string; body: unknown; query: Record<string, string>; headers: Record<string, string> }>
}

function initialState(): TestState {
  return {
    account: accountDetail(),
    organizations: [organization()],
    members: memberList(),
    connections: [connection()],
    auditLogs: [auditLog()],
    apps: [app()],
    appDetail: appDetail(),
    jobs: [job()],
    jobDetail: jobDetail(),
    syncJobIds: [48300],
    publicSettings: publicSettings(),
    featureFlags: [],
    orgRoles: orgRoles(),
    orgSettings: orgSettings(),
    invitationPreview: invitationPreview(),
    sessions: [accountSession()],
    pushSubscriptions: [],
    aiSettings: {
      HasApiKey: true,
      ApiKeyLastFour: 'Xy9Z',
      ApiKeySetDate: '2026-11-18T09:30:00Z',
      ApiKeySetByUserName: 'Ayşe Yılmaz',
      Model: 'claude-haiku-5-5',
    },
    aiDraft: 'Merhaba, geri bildiriminiz için teşekkürler. Ekibimiz hatırlatıcı sorununa bakacak.',
    requests: [],
  }
}

export const testState: TestState = initialState()

export function resetTestState(): void {
  Object.assign(testState, initialState())
}

/** The requests the app made to a path (exact path, query string ignored). */
export function requestsTo(path: string) {
  return testState.requests.filter((request) => request.path === path)
}

export function lastRequestTo(path: string) {
  return requestsTo(path).at(-1)
}

export function calledPath(path: string): boolean {
  return requestsTo(path).length > 0
}
