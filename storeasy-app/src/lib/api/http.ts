import { API_BASE } from '@/config'
import { pascalizeKeys } from '@/lib/api/pascalize'
import { getDeviceId, tokenStore, type AuthTokens } from '@/lib/api/tokenStore'
import type { ApiListResponse, ApiResponse, LoginResponseDto } from '@/lib/api/types'
import { cultureOf, readLang } from '@/lib/i18n/lang'
import { appEvent } from '@/lib/utils/events'

// The session is over (refresh failed): AuthProvider listens, clears the cache and signs out.
export const AUTH_LOST_EVENT = appEvent('auth-lost')
// Fired on every refused request with { keys, message, status }, so app-level UI can react no matter which screen made
// the call.
export const API_REFUSAL_EVENT = appEvent('api-refusal')
// The server-side kill switch ([FeatureFlag] attribute, 503).
export const FEATURE_DISABLED_KEY = 'FeatureDisabled'

export interface ApiRefusal {
  keys: string[]
  message: string | null
  status: number
}

export class ApiError extends Error {
  statusCode: number
  // The API's localized messages, in the request's language.
  messages: string[]
  // The API's message keys (e.g. 'EmailExists'), for callers that react to one specific refusal.
  keys: string[]
  // Seconds from the Retry-After header of a 429, for a countdown; null when the API did not send one.
  retryAfterSeconds: number | null

  constructor(message: string, statusCode: number, messages: string[] = [], keys: string[] = [], retryAfterSeconds: number | null = null) {
    super(message)
    this.name = 'ApiError'
    this.statusCode = statusCode
    this.messages = messages
    this.keys = keys
    this.retryAfterSeconds = retryAfterSeconds
  }
}

// The text to show for a failed request: the API's own localized message when it sent one, otherwise the caller's
// (dictionary) fallback. Never the technical "Request failed (502)" message.
export function apiErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError && error.messages.length > 0) {
    return error.messages[0]
  }
  return fallback
}

export function hasApiKey(error: unknown, key: string): boolean {
  return error instanceof ApiError && error.keys.includes(key)
}

export function apiStatus(error: unknown): number {
  return error instanceof ApiError ? error.statusCode : 0
}

// ---- gates -------------------------------------------------------------------
// A refusal the user can resolve on the spot: the registered handler opens its dialog and resolves true when the user
// did it; the request is then sent once more with the same idempotency key. Without a handler, or when the user closes
// the dialog, the refusal reaches the caller.
type GateHandler = () => Promise<boolean>
const gateHandlers = new Map<string, GateHandler>()

export function setGateHandler(key: string, handler: GateHandler | null): void {
  if (handler) {
    gateHandlers.set(key, handler)
  } else {
    gateHandlers.delete(key)
  }
}

// ---- url and body ------------------------------------------------------------
type QueryValue = string | number | boolean
type Query = Record<string, QueryValue | QueryValue[] | null | undefined>

function buildUrl(path: string, query?: Query): string {
  const url = `${API_BASE}${path.startsWith('/') ? path : `/${path}`}`
  if (!query) {
    return url
  }
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value === null || value === undefined) {
      continue
    }
    // A list goes out as a repeated key (?Ids=1&Ids=2), which ASP.NET binds to a List<T>.
    if (Array.isArray(value)) {
      for (const item of value) {
        params.append(key, String(item))
      }
    } else {
      params.append(key, String(value))
    }
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

// A proxy/CDN error page can arrive as HTML. Never let a non-JSON body turn into a SyntaxError: the caller gets an
// ApiError built from the status code instead.
function parseBody(text: string): unknown {
  if (!text) {
    return null
  }
  try {
    return pascalizeKeys(JSON.parse(text))
  } catch {
    return null
  }
}

function readMessages(body: unknown): { messages: string[]; keys: string[] } {
  const list = (body as ApiResponse<unknown> | null)?.Messages
  if (!Array.isArray(list)) {
    return { messages: [], keys: [] }
  }
  return {
    messages: list.map((m) => m?.Message || '').filter(Boolean),
    keys: list.map((m) => m?.Key || '').filter(Boolean),
  }
}

function isFailedEnvelope(body: unknown): boolean {
  return !!body && (body as ApiResponse<unknown>).IsSuccess === false
}

// Retry-After is either seconds or an HTTP date.
function readRetryAfter(res: Response): number | null {
  const header = res.headers.get('Retry-After')
  if (!header) {
    return null
  }
  const seconds = Number(header)
  if (Number.isFinite(seconds)) {
    return Math.max(0, Math.ceil(seconds))
  }
  const date = Date.parse(header)
  return Number.isNaN(date) ? null : Math.max(0, Math.ceil((date - Date.now()) / 1000))
}

// ---- refresh -----------------------------------------------------------------
let refreshPromise: Promise<boolean> | null = null

async function refreshTokens(): Promise<boolean> {
  const current = tokenStore.get()
  if (!current?.refreshToken || !current.userName) {
    return false
  }
  try {
    // The refresh endpoint is anonymous but matches the current (expired) access token against the session row, so
    // the expired token must still go in the Authorization header. This call bypasses rawFetch, so it is added here.
    const headers: Record<string, string> = { 'Content-Type': 'application/json', 'Accept-Language': cultureOf(readLang()) }
    if (current.accessToken) {
      headers['Authorization'] = `Bearer ${current.accessToken}`
    }
    const res = await fetch(buildUrl('/Account/login/refresh'), {
      method: 'POST',
      headers,
      body: JSON.stringify({ UserName: current.userName, RefreshToken: current.refreshToken, DeviceId: getDeviceId() }),
    })
    const body = parseBody(await res.text().catch(() => '')) as ApiResponse<LoginResponseDto> | null
    const token = body?.Data?.Token
    if (!res.ok || !body?.IsSuccess || !token?.AccessToken) {
      return false
    }
    const next: AuthTokens = {
      ...current,
      accessToken: token.AccessToken,
      refreshToken: token.RefreshToken,
      expiresIn: token.ExpiresIn,
      refreshTokenExpiresIn: token.RefreshTokenExpiresIn,
      issuedAt: Date.now(),
      userId: body.Data.Id ?? current.userId,
      userType: body.Data.UserType ?? current.userType,
    }
    tokenStore.set(next)
    return true
  } catch {
    return false
  }
}

// Concurrent 401s share one refresh: the backend rotates the refresh token, so a second refresh would fail. Exported
// for the live hub, whose connection has to renew its token the same way.
export function ensureRefresh(): Promise<boolean> {
  if (!refreshPromise) {
    refreshPromise = refreshTokens().finally(() => {
      refreshPromise = null
    })
  }
  return refreshPromise
}

function isSessionPath(path: string): boolean {
  return path.toLowerCase().startsWith('/account/login')
}

// ---- idempotency -------------------------------------------------------------
function newIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`
}

// A double tap never reaches the network twice: an identical idempotent request already in flight shares its promise.
const inFlight = new Map<string, Promise<unknown>>()

// ---- core --------------------------------------------------------------------
interface RequestOptions {
  query?: Query
  body?: unknown
  auth?: boolean
  idempotent?: boolean
}

async function rawFetch(method: string, url: string, body: unknown, withAuth: boolean, idempotencyKey?: string): Promise<Response> {
  const headers: Record<string, string> = { 'Accept-Language': cultureOf(readLang()) }
  // Same key on every attempt at one action, so the API answers a retry with the first attempt's response.
  if (idempotencyKey) {
    headers['Idempotency-Key'] = idempotencyKey
  }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (withAuth) {
    const tokens = tokenStore.get()
    if (tokens?.accessToken) {
      headers['Authorization'] = `Bearer ${tokens.accessToken}`
    }
  }
  return fetch(url, { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined })
}

// Sends the request, and once more with a fresh token when the session expired (401, or 419 SessionTimeout). When the
// token cannot be refreshed the session is over: the stored token is dropped and the app is told to sign out.
async function fetchWithRefresh(
  method: string,
  path: string,
  url: string,
  body: unknown,
  withAuth: boolean,
  idempotencyKey?: string,
): Promise<Response> {
  let res = await rawFetch(method, url, body, withAuth, idempotencyKey)
  if ((res.status === 401 || res.status === 419) && withAuth && !isSessionPath(path)) {
    const refreshed = await ensureRefresh()
    if (refreshed) {
      res = await rawFetch(method, url, body, true, idempotencyKey)
    } else {
      tokenStore.clear()
      window.dispatchEvent(new CustomEvent(AUTH_LOST_EVENT))
    }
  }
  return res
}

async function sendOnce<TBody>(
  method: string,
  path: string,
  opts: RequestOptions,
  idempotencyKey?: string,
  retried = false,
): Promise<TBody> {
  const res = await fetchWithRefresh(method, path, buildUrl(path, opts.query), opts.body, opts.auth !== false, idempotencyKey)
  const body = parseBody(await res.text())

  if (!res.ok || isFailedEnvelope(body)) {
    const { messages, keys } = readMessages(body)
    const status = res.ok ? ((body as ApiResponse<unknown> | null)?.StatusCode ?? res.status) : res.status
    window.dispatchEvent(new CustomEvent<ApiRefusal>(API_REFUSAL_EVENT, { detail: { keys, message: messages[0] ?? null, status } }))
    // The refused request did not run (a failed attempt also frees its idempotency key), so it is safe to send it once
    // more after the gate is passed. Only once: a second refusal goes to the caller.
    const gate = retried ? undefined : keys.map((key) => gateHandlers.get(key)).find((handler) => !!handler)
    if (gate && (await gate())) {
      return sendOnce<TBody>(method, path, opts, idempotencyKey, true)
    }
    throw new ApiError(messages[0] ?? `Request failed (${status})`, status, messages, keys, readRetryAfter(res))
  }

  return body as TBody
}

function send<TBody>(method: string, path: string, opts: RequestOptions = {}): Promise<TBody> {
  if (!opts.idempotent) {
    return sendOnce<TBody>(method, path, opts)
  }
  const dedupeKey = `${method} ${buildUrl(path, opts.query)} ${JSON.stringify(opts.body ?? null)}`
  const running = inFlight.get(dedupeKey)
  if (running) {
    return running as Promise<TBody>
  }
  const request = sendOnce<TBody>(method, path, opts, newIdempotencyKey()).finally(() => {
    inFlight.delete(dedupeKey)
  })
  inFlight.set(dedupeKey, request)
  return request
}

// ---- public helpers ----------------------------------------------------------
export interface CallOptions {
  // false = anonymous call (login, register, public endpoints).
  auth?: boolean
  // A create with no natural uniqueness: sends an Idempotency-Key and shares an identical in-flight call.
  idempotent?: boolean
}

export async function apiGet<T>(path: string, query?: Query, options: CallOptions = {}): Promise<T> {
  const res = await send<ApiResponse<T>>('GET', path, { query, ...options })
  return res.Data
}

export function apiGetList<T>(path: string, query?: Query): Promise<ApiListResponse<T>> {
  return send<ApiListResponse<T>>('GET', path, { query })
}

export async function apiPost<T>(path: string, body?: unknown, options: CallOptions = {}): Promise<T> {
  const res = await send<ApiResponse<T>>('POST', path, { body, ...options })
  return res.Data
}

export async function apiPut<T>(path: string, body?: unknown): Promise<T> {
  const res = await send<ApiResponse<T>>('PUT', path, { body })
  return res.Data
}

export async function apiPatch<T>(path: string, body?: unknown): Promise<T> {
  const res = await send<ApiResponse<T>>('PATCH', path, { body })
  return res.Data
}

export function apiPatchList<T>(path: string, body?: unknown): Promise<ApiListResponse<T>> {
  return send<ApiListResponse<T>>('PATCH', path, { body })
}

export async function apiDelete<T>(path: string, query?: Query, body?: unknown): Promise<T> {
  const res = await send<ApiResponse<T>>('DELETE', path, { query, body })
  return res.Data
}
