import { randomUUID } from 'node:crypto'

/**
 * Test setup over the real API (the flow under test goes through the UI). Answers come back as the API sends them
 * (camelCase envelope); `apiCall` unwraps `data` and throws on a failed envelope.
 */
export const BASE_URL = process.env.E2E_BASE_URL ?? process.env.E2E_API_URL ?? 'http://localhost:5000'
export const API = `${BASE_URL}/api/v1.0`

export interface Envelope<T> {
  isSuccess: boolean
  statusCode: number
  messages?: { key: string; message: string }[]
  data: T
}

export class ApiCallError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly body: unknown,
  ) {
    super(message)
  }
}

export interface CallOptions {
  token?: string
  query?: Record<string, string | number | boolean | undefined>
  lang?: 'tr-TR' | 'en-US'
}

/**
 * A client address per call. The e2e stack has no TLS proxy in front of nginx, so the address in X-Forwarded-For is the
 * one the API sees (it trusts two hops); every test then has its own per-address rate limits (register, sign in).
 */
export function clientAddress(): string {
  const octet = () => 1 + Math.floor(Math.random() * 250)
  return `10.${octet()}.${octet()}.${octet()}`
}

export async function apiRaw(method: string, path: string, body?: unknown, options: CallOptions = {}): Promise<Response> {
  const url = new URL(`${API}${path.startsWith('/') ? path : `/${path}`}`)
  for (const [key, value] of Object.entries(options.query ?? {})) {
    if (value !== undefined) {
      url.searchParams.set(key, String(value))
    }
  }
  const headers: Record<string, string> = { 'Accept-Language': options.lang ?? 'en-US', 'X-Forwarded-For': clientAddress() }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (options.token) {
    headers['Authorization'] = `Bearer ${options.token}`
  }
  if (method !== 'GET') {
    headers['Idempotency-Key'] = randomUUID()
  }
  return fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
}

export async function apiCall<T>(method: string, path: string, body?: unknown, options: CallOptions = {}): Promise<T> {
  const response = await apiRaw(method, path, body, options)
  const text = await response.text()
  let envelope: Envelope<T> | null = null
  try {
    envelope = text ? (JSON.parse(text) as Envelope<T>) : null
  } catch {
    // not JSON
  }
  if (!response.ok || !envelope?.isSuccess) {
    throw new ApiCallError(`${method} ${path} -> ${response.status} ${text.slice(0, 500)}`, response.status, envelope ?? text)
  }
  return envelope.data
}

/** A unique, readable identity per test run, so tests never share data. */
export function uniqueIdentity(prefix = 'e2e') {
  const id = `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`
  return {
    id,
    email: `${prefix}-${id}@e2e.test`,
    name: 'Ece',
    surname: `Test ${id}`,
    password: `Sx!${id}9a`,
  }
}

export type Identity = ReturnType<typeof uniqueIdentity>

export interface LoginData {
  id: number
  token: { accessToken: string; refreshToken: string }
}

export async function login(email: string, password: string): Promise<string> {
  const data = await apiCall<LoginData>('POST', '/Account/login', { UserName: email, Password: password, DeviceId: randomUUID() })
  return data.token.accessToken
}
