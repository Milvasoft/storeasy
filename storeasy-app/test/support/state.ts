import type { AccountDetailDto, MemberListDto, MyOrganizationDto, StoreAuditLogDto, StoreConnectionDto } from '@/lib/api/types'
import { accountDetail, auditLog, connection, memberList, organization } from './factories'

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
