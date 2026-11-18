import { HttpResponse, http, type HttpHandler } from 'msw'
import { asset, loginResponse } from './factories'
import { testState } from './state'

const BASE = '*/api/v1.0'

/** The id POST register answers. */
export const TEST_NEW_USER_ID = 42

/** The presigned storage the asset handlers point at; tests answer its PUTs themselves. */
export const STORAGE_URL = 'https://storage.test/bucket'

/** The API's success envelope (Response<T>), camelCase on the wire like the real API. */
export function envelope<T>(data: T, statusCode = 200) {
  return { IsSuccess: true, StatusCode: statusCode, Messages: [], Data: data }
}

/** The paged list envelope (ListResponse<T>), what apiPatchList reads. */
export function listEnvelope<T>(data: T[], page = 1, total = data.length, pageCount = 1) {
  return {
    IsSuccess: true,
    StatusCode: 200,
    Messages: [],
    Data: data,
    CurrentPageNumber: page,
    TotalPageCount: pageCount,
    TotalDataCount: total,
  }
}

/** The failure envelope with a real HTTP status, exactly as the backend answers: a key, a localized message, the status. */
export function errorResponse(key: string, message: string, status = 400, headers?: Record<string, string>) {
  return HttpResponse.json(
    { IsSuccess: false, StatusCode: status, Messages: [{ Key: key, Message: message, Type: 3 }], Data: null },
    { status, headers },
  )
}

async function readBody(request: Request): Promise<unknown> {
  const text = await request.text().catch(() => '')
  if (!text) {
    return null
  }
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

type Method = 'get' | 'post' | 'put' | 'patch' | 'delete'
type Resolver = (args: { request: Request; url: URL; body: unknown }) => Response | Promise<Response>

/**
 * Registers one endpoint and records every call into testState.requests, so a test asserts what the app sent without a
 * handler of its own. Tests that need a different answer keep the recording: server.use(endpoint('post', '/x', ...)).
 */
export function endpoint(method: Method, path: string, resolver: Resolver): HttpHandler {
  return http[method](`${BASE}${path}`, async ({ request }) => {
    const url = new URL(request.url)
    const body = method === 'get' ? null : await readBody(request.clone())
    testState.requests.push({
      method: method.toUpperCase(),
      path: url.pathname.replace(/^.*\/api\/v1\.0/, ''),
      body,
      query: Object.fromEntries(url.searchParams),
      headers: Object.fromEntries(request.headers),
    })
    return resolver({ request, url, body })
  })
}

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

/**
 * Default answers for every endpoint in src/lib/api/endpoints.ts; method, path and envelope mirror the API controllers.
 * The data comes from testState. apiContract.test.ts fails when an endpoint the app calls has no handler here.
 */
export const defaultHandlers: HttpHandler[] = [
  // ---- account ------------------------------------------------------------
  endpoint('post', '/Account/login', ({ body }) => {
    const credentials = body as { Password?: string } | null
    if (credentials?.Password === 'wrong-password') {
      return errorResponse('Unauthorized', 'E-posta veya şifre hatalı.', 401)
    }
    return json(envelope(loginResponse()))
  }),
  endpoint('post', '/Account/login/refresh', () => json(envelope(loginResponse()))),
  endpoint('post', '/Account/logout', () => json(envelope(null))),
  endpoint('get', '/Account/detail', () => json(envelope(testState.account))),
  endpoint('put', '/Account/password/change', () => json(envelope(null))),
  endpoint('post', '/Account/login/google/start', () =>
    json(envelope({ AuthorizationUrl: 'https://accounts.google.test/o/oauth2/v2/auth?state=google-state', State: 'google-state' })),
  ),
  endpoint('post', '/Account/login/google', ({ body }) => {
    const input = body as { State?: string } | null
    if (input?.State === 'used-state') {
      return errorResponse('GoogleSignInStateInvalid', 'Google ile giriş isteği geçersiz ya da süresi dolmuş.', 400)
    }
    return json(envelope(loginResponse()))
  }),
  endpoint('put', '/Account/profile', () => json(envelope(testState.account.Id))),
  endpoint('get', '/Account/sessions', () => json(envelope(testState.sessions))),
  endpoint('delete', '/Account/session', ({ body }) => json(envelope((body as { Id: number }).Id))),
  endpoint('delete', '/Account/sessions/others', () => json(envelope(testState.sessions.filter((session) => !session.IsCurrent).length))),
  endpoint('get', '/Account/push/subscriptions', () => json(envelope(testState.pushSubscriptions))),
  endpoint('post', '/Account/push/subscription', () => json(envelope(91))),
  endpoint('delete', '/Account/push/subscription', () => json(envelope(91))),
  endpoint('post', '/Account/push/test', () => json(envelope(testState.pushSubscriptions.length))),

  // ---- system -------------------------------------------------------------
  endpoint('get', '/System/public-settings', () => json(envelope(testState.publicSettings))),
  endpoint('get', '/System/feature-flags', () => json(envelope(testState.featureFlags))),

  // ---- account: registration and recovery ---------------------------------
  endpoint('post', '/Account/register', () => json(envelope(TEST_NEW_USER_ID))),
  endpoint('post', '/Account/email/verify', () => json(envelope(null))),
  endpoint('post', '/Account/email/verify/resend', () => json(envelope(null))),
  endpoint('post', '/Account/password/forgot', () => json(envelope(null))),
  endpoint('post', '/Account/password/reset', () => json(envelope(null))),

  // ---- organizations ------------------------------------------------------
  endpoint('get', '/Organizations/mine', () => json(envelope(testState.organizations))),
  endpoint('post', '/Organizations/organization', () => json(envelope(42))),
  endpoint('put', '/Organizations/organization', () => json(envelope(testState.organizations[0]?.Id ?? 0))),
  endpoint('get', '/Organizations/members', () => json(envelope(testState.members))),
  endpoint('post', '/Organizations/invitation', ({ body }) =>
    json(envelope({ Id: 77, Token: `token-for-${(body as { Email?: string }).Email}`, ExpiryDate: '2026-11-25T12:00:00Z' })),
  ),
  endpoint('delete', '/Organizations/invitation', () => json(envelope(31))),
  endpoint('get', '/Organizations/invitation/preview', () =>
    testState.invitationPreview
      ? json(envelope(testState.invitationPreview))
      : errorResponse('InvitationInvalid', 'Davet bulunamadı.', 404),
  ),
  endpoint('get', '/Organizations/roles', () => json(envelope(testState.orgRoles))),
  endpoint('post', '/Organizations/role', () => json(envelope(14))),
  endpoint('put', '/Organizations/role', ({ body }) => json(envelope((body as { RoleId: number }).RoleId))),
  endpoint('delete', '/Organizations/role', ({ body }) => json(envelope((body as { RoleId: number }).RoleId))),
  endpoint('get', '/Organizations/settings', () => json(envelope(testState.orgSettings))),
  endpoint('put', '/Organizations/settings', () => json(envelope(testState.organizations[0]?.Id ?? 0))),
  endpoint('post', '/Organizations/invitation/accept', () => json(envelope(8))),
  endpoint('put', '/Organizations/member/role', () => json(envelope(2))),
  endpoint('delete', '/Organizations/member', () => json(envelope(2))),
  endpoint('patch', '/Organizations/audit-logs', () => json(listEnvelope(testState.auditLogs))),

  // ---- store connections --------------------------------------------------
  endpoint('get', '/StoreConnections', () => json(envelope(testState.connections))),
  endpoint('post', '/StoreConnections/connection', () => json(envelope(55))),
  endpoint('post', '/StoreConnections/connection/verify', ({ body }) => {
    const id = (body as { ConnectionId: number }).ConnectionId
    const known = testState.connections.find((item) => item.Id === id) ?? { ...testState.connections[0], Id: id }
    return json(envelope({ ...known, Status: 2, LastVerifiedDate: '2026-11-18T10:00:00Z' }))
  }),
  endpoint('delete', '/StoreConnections/connection', () => json(envelope(11))),

  // ---- apps and builds ----------------------------------------------------
  endpoint('patch', '/Apps', ({ body }) => {
    const store = (body as { Store?: number } | null)?.Store
    return json(
      listEnvelope(store ? testState.apps.filter((item) => item.Stores?.some((record) => record.Store === store)) : testState.apps),
    )
  }),
  endpoint('get', '/Apps/app', ({ url }) =>
    Number(url.searchParams.get('AppId')) === testState.appDetail.Id
      ? json(envelope(testState.appDetail))
      : errorResponse('AppNotFound', 'Uygulama bulunamadı.', 404),
  ),
  endpoint('post', '/Apps/google', () => json(envelope({ AppId: 103, StoreAppId: 230, JobId: 48400 }))),
  endpoint('post', '/Apps/app/link', ({ body }) => json(envelope((body as { AppId: number }).AppId))),
  endpoint('post', '/Apps/app/unlink', () => json(envelope(104))),
  endpoint('post', '/Apps/sync', () => json(envelope(testState.syncJobIds))),

  // ---- store jobs ---------------------------------------------------------
  endpoint('patch', '/StoreJobs', () => json(listEnvelope(testState.jobs))),
  endpoint('get', '/StoreJobs/job', ({ url }) =>
    Number(url.searchParams.get('JobId')) === testState.jobDetail.Id
      ? json(envelope(testState.jobDetail))
      : errorResponse('StoreJobNotFound', 'İş bulunamadı.', 404),
  ),
  endpoint('post', '/StoreJobs/job/cancel', () => json(envelope(5))),

  // ---- assets -------------------------------------------------------------
  endpoint('post', '/Assets/upload', () =>
    json(envelope({ AssetId: 501, UploadUrl: `${STORAGE_URL}/single`, PartSize: null, Parts: [], ExpiryDate: '2026-11-18T11:00:00Z' })),
  ),
  endpoint('post', '/Assets/asset/complete', () => json(envelope(asset()))),
  endpoint('get', '/Assets/asset/download-url', () =>
    json(envelope({ Url: `${STORAGE_URL}/download`, ExpiryDate: '2026-11-18T11:00:00Z' })),
  ),
]

/**
 * Endpoints of the feature screens the shell itself calls on every page (the notification bell), answered empty by
 * default; test/support/phase4.ts answers them with data. Kept apart from defaultHandlers, which mirror endpoints.ts.
 */
export const shellHandlers: HttpHandler[] = [
  endpoint('patch', '/Account/notifications', () => json(listEnvelope([]))),
  endpoint('put', '/Account/notifications/seen', () => json(envelope(null))),
  endpoint('delete', '/Account/notifications', () => json(envelope(null))),
]
