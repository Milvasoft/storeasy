// SPDX-License-Identifier: AGPL-3.0-only
import { release } from 'node:os'
import { ExitCode, StoreasyError, errorMessage } from './errors.js'
import { redact, registerSecret, stripQuery } from './redact.js'
import { VERSION } from './version.js'

export type FetchLike = (input: string, init?: RequestInit) => Promise<Response>

export interface RetryPolicy {
  /** Attempts after the first one. */
  retries: number
  /** Base delay in ms; attempt n waits base * 2^n (or Retry-After). */
  baseDelayMs: number
}

export const DEFAULT_RETRY: RetryPolicy = { retries: 3, baseDelayMs: 500 }
const RETRY_STATUS = new Set([408, 429, 500, 502, 503, 504])

export const sleep = (ms: number) => new Promise<void>((r) => setTimeout(r, ms))

export function userAgent(client = 'storeasy-cli'): string {
  return `${client}/${VERSION} (${process.platform} ${release()}; node ${process.versions.node})`
}

/** The Milvasoft response envelope, `Response<T>` / `ListResponse<T>`. */
export interface Envelope<T> {
  isSuccess: boolean
  statusCode: number
  messages: Array<{ key?: string; message?: string; type?: number | string }>
  data: T
  currentPageNumber?: number | null
  totalPageCount?: number | null
  totalDataCount?: number | null
}

/** The API sends camelCase; tolerate PascalCase too. */
function normalizeEnvelope<T>(raw: unknown): Envelope<T> | undefined {
  if (!raw || typeof raw !== 'object') {
    return undefined
  }
  const o = raw as Record<string, unknown>
  const pick = (k: string) => (k in o ? o[k] : o[k[0]!.toUpperCase() + k.slice(1)])
  const isSuccess = pick('isSuccess')
  if (typeof isSuccess !== 'boolean') {
    return undefined
  }
  const messages = (pick('messages') as unknown[] | undefined) ?? []
  return {
    isSuccess,
    statusCode: Number(pick('statusCode') ?? 0),
    messages: messages.map((m) => {
      const mo = (m ?? {}) as Record<string, unknown>
      return {
        key: (mo.key ?? mo.Key) as string | undefined,
        message: (mo.message ?? mo.Message) as string | undefined,
        type: (mo.type ?? mo.Type) as number | string | undefined,
      }
    }),
    data: pick('data') as T,
    currentPageNumber: pick('currentPageNumber') as number | null | undefined,
    totalPageCount: pick('totalPageCount') as number | null | undefined,
    totalDataCount: pick('totalDataCount') as number | null | undefined,
  }
}

export async function readJson(res: Response): Promise<unknown> {
  const text = await res.text()
  if (!text) {
    return undefined
  }
  try {
    return JSON.parse(text) as unknown
  } catch {
    return text
  }
}

export type TokenSource = string | (() => Promise<string>)

export interface ApiClientOptions {
  baseUrl: string
  token?: TokenSource
  fetch?: FetchLike
  client?: string
  retry?: Partial<RetryPolicy>
  sleep?: (ms: number) => Promise<void>
}

export interface RequestOptions {
  body?: unknown
  query?: Record<string, string | number | boolean | undefined>
  /** Retry on network errors, 408, 429 and 5xx. Only for calls that are safe to repeat. Default: GET only. */
  retry?: boolean
  /** Send the Authorization header (default true). */
  auth?: boolean
}

/** REST client of storeasy-api: auth header, envelope unwrap, retries, redacted errors. */
export class ApiClient {
  readonly baseUrl: string
  readonly fetch: FetchLike
  readonly retryPolicy: RetryPolicy
  readonly sleep: (ms: number) => Promise<void>
  readonly userAgent: string
  private tokenSource?: TokenSource

  constructor(opts: ApiClientOptions) {
    this.baseUrl = opts.baseUrl.replace(/\/+$/, '')
    const f = opts.fetch
    this.fetch = f ?? ((input, init) => fetch(input, init))
    this.retryPolicy = { ...DEFAULT_RETRY, ...opts.retry }
    this.sleep = opts.sleep ?? sleep
    this.userAgent = userAgent(opts.client)
    this.setToken(opts.token)
  }

  setToken(token: TokenSource | undefined): void {
    this.tokenSource = token
    if (typeof token === 'string') {
      registerSecret(token)
    }
  }

  hasToken(): boolean {
    return Boolean(this.tokenSource)
  }

  async token(): Promise<string | undefined> {
    if (!this.tokenSource) {
      return undefined
    }
    const t = typeof this.tokenSource === 'string' ? this.tokenSource : await this.tokenSource()
    registerSecret(t)
    return t
  }

  url(path: string, query?: RequestOptions['query']): string {
    const u = new URL(`${this.baseUrl}${path.startsWith('/') ? path : `/${path}`}`)
    for (const [k, v] of Object.entries(query ?? {})) {
      if (v !== undefined) {
        u.searchParams.set(k, String(v))
      }
    }
    return u.toString()
  }

  /** Sends a request and returns the full envelope. */
  async send<T>(method: string, path: string, opts: RequestOptions = {}): Promise<Envelope<T>> {
    const headers: Record<string, string> = { Accept: 'application/json', 'User-Agent': this.userAgent }
    if (opts.auth !== false) {
      const t = await this.token()
      if (!t) {
        throw new StoreasyError('Not signed in. Run "storeasy login", set STOREASY_TOKEN, or run in a CI with OIDC.', {
          exitCode: ExitCode.Auth,
        })
      }
      headers.Authorization = `Bearer ${t}`
    }
    let body: string | undefined
    if (opts.body !== undefined) {
      headers['Content-Type'] = 'application/json'
      body = JSON.stringify(opts.body)
    }
    const url = this.url(path, opts.query)
    const res = await this.fetchWithRetry(url, { method, headers, body }, opts.retry ?? method === 'GET')
    const raw = await readJson(res)
    const env = normalizeEnvelope<T>(raw)
    if (!res.ok || !env || !env.isSuccess) {
      throw apiError(method, url, res.status, env, raw)
    }
    return env
  }

  /** Sends a request and returns `data`. */
  async request<T>(method: string, path: string, opts: RequestOptions = {}): Promise<T> {
    return (await this.send<T>(method, path, opts)).data
  }

  async fetchWithRetry(url: string, init: RequestInit, retry: boolean): Promise<Response> {
    const attempts = retry ? this.retryPolicy.retries : 0
    let lastErr: unknown
    for (let attempt = 0; attempt <= attempts; attempt++) {
      try {
        const res = await this.fetch(url, init)
        if (!RETRY_STATUS.has(res.status) || attempt === attempts) {
          return res
        }
        await res.body?.cancel().catch(() => undefined)
        await this.sleep(retryDelay(res, this.retryPolicy.baseDelayMs, attempt))
      } catch (err) {
        lastErr = err
        if (attempt === attempts) {
          break
        }
        await this.sleep(this.retryPolicy.baseDelayMs * 2 ** attempt)
      }
    }
    throw new StoreasyError(`Network error calling ${stripQuery(url)}: ${errorMessage(causeOf(lastErr))}`)
  }
}

export function causeOf(err: unknown): unknown {
  // undici wraps the real reason (ECONNREFUSED, ENOTFOUND) in `cause`.
  if (err instanceof Error && err.cause instanceof Error) {
    return `${err.message} (${err.cause.message})`
  }
  return err
}

export function retryDelay(res: Response, baseDelayMs: number, attempt: number): number {
  const ra = Number(res.headers.get('retry-after'))
  if (Number.isFinite(ra) && ra > 0) {
    return Math.min(ra * 1000, 60_000)
  }
  return baseDelayMs * 2 ** attempt
}

function apiError(method: string, url: string, status: number, env: Envelope<unknown> | undefined, raw: unknown): StoreasyError {
  const keys = (env?.messages ?? []).map((m) => m.key).filter((k): k is string => Boolean(k))
  const text = env?.messages.find((m) => m.message)?.message
  const fallback = typeof raw === 'string' && raw.length < 300 ? raw : `HTTP ${status}`
  const code = status >= 400 ? status : env?.statusCode || status
  const path = stripQuery(url).replace(/^https?:\/\/[^/]+/, '')
  return new StoreasyError(redact(`${text ?? fallback} (${method} ${path}, ${code})`), { status: code, keys })
}
