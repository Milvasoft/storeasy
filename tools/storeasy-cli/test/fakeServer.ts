// SPDX-License-Identifier: AGPL-3.0-only
// A local fake of storeasy-api and of S3 that follows tools/CONTRACT.md. Runs on 127.0.0.1 with a random port; no
// test reaches the real network.
import { createHash, randomUUID } from 'node:crypto'
import { type IncomingMessage, type Server as HttpServer, type ServerResponse, createServer } from 'node:http'
import type { AddressInfo } from 'node:net'
import { Server as McpServer } from '@modelcontextprotocol/sdk/server/index.js'
import { StreamableHTTPServerTransport } from '@modelcontextprotocol/sdk/server/streamableHttp.js'
import { CallToolRequestSchema, ListToolsRequestSchema, type Tool } from '@modelcontextprotocol/sdk/types.js'

export interface Recorded {
  method: string
  path: string
  query: URLSearchParams
  headers: Record<string, string | string[] | undefined>
  body: unknown
  bytes: number
}

export interface Identity {
  kind: number
  name: string
  prefix: string
  organizationId: number
  organizationName: string
  userName: string | null
  readOnly: boolean
  scopes: string[]
  appIds: number[]
  allApps: boolean
  expiryDate: string
}

interface Session {
  id: number
  appId: number
  sha256: string
  size: number
  fileName: string
  partSize: number
  status: number
  parts: Map<number, { etag: string; bytes: number; sha: Buffer }>
  body: Record<string, unknown>
  polls: number
  originalSessionId?: number
}

export interface SessionPoll {
  status: number
  statusDescription?: string
  steps?: Array<Record<string, unknown>>
  ciResult?: { exitCode: number; summary: string } | null
  storeJobId?: number
}

export interface FakeOptions {
  partSize?: number
  tokens?: Record<string, Partial<Identity>>
  /** What GET artifacts/session returns on poll 1, 2, ... (the last one repeats). */
  sessionScript?: SessionPoll[]
  /** The JWT the fake GitHub / Azure OIDC endpoints hand out. */
  oidcToken?: string
  /** Rules of the fake OIDC exchange: which OIDC token is accepted for which app. */
  oidcRules?: Array<{ appId: number; token: string }>
  /** Upload token issued by the exchange. */
  uploadToken?: string
  uploadTokenTtlMs?: number
  remoteTools?: Tool[]
}

const json = (res: ServerResponse, status: number, body: unknown, headers: Record<string, string> = {}) => {
  res.writeHead(status, { 'Content-Type': 'application/json', ...headers })
  res.end(JSON.stringify(body))
}
const ok = (res: ServerResponse, data: unknown, extra: Record<string, unknown> = {}) =>
  json(res, 200, { isSuccess: true, statusCode: 200, messages: [], data, ...extra })
const fail = (res: ServerResponse, status: number, key: string, message: string) =>
  json(res, status, { isSuccess: false, statusCode: status, messages: [{ key, message, type: 2 }], data: null })

async function readBody(req: IncomingMessage): Promise<Buffer> {
  const chunks: Buffer[] = []
  for await (const c of req) {
    chunks.push(c as Buffer)
  }
  return Buffer.concat(chunks)
}

export const DEFAULT_PAT = 'sea_9c1f4b7e2a6d0c83f15a9b2e7d4c61aa0f3e8b52'
export const DEFAULT_CI = 'sci_7f2a4QmV9xT2kLr8NwZp3Hc6YbJd1EuS5gAo'

export class FakeStoreasy {
  readonly requests: Recorded[] = []
  readonly sessions = new Map<number, Session>()
  readonly assets = new Map<number, { fileName: string; purpose: number; sha256: string; size: number; stored?: Buffer; complete: boolean }>()
  readonly mcpCalls: Array<{ name: string; arguments: Record<string, unknown>; authorization?: string }> = []
  /** Fault injection on S3 part PUTs: part number to a list of failures, consumed one per attempt. */
  readonly partFaults = new Map<number, Array<number | 'reset'>>()
  /** Make every part PUT of these part numbers fail with 500 (until cleared). */
  readonly brokenParts = new Set<number>()
  /** Errors to return from the API instead of the normal answer: "METHOD /path" to [status, key, message]. */
  readonly apiFaults = new Map<string, [number, string, string]>()
  baseUrl = ''
  private server?: HttpServer
  private nextSession = 4100
  private nextAsset = 500
  private s3Objects = new Map<string, Buffer>()
  readonly opts: Required<Omit<FakeOptions, 'tokens'>> & { tokens: Record<string, Identity> }
  issuedUploadTokens = 0

  constructor(opts: FakeOptions = {}) {
    const identity = (t: Partial<Identity>, prefix: string): Identity => ({
      kind: 1,
      name: 'Claude Code laptop',
      prefix,
      organizationId: 7,
      organizationName: 'Kuzey Lab',
      userName: 'deniz@kuzeylab.dev',
      readOnly: false,
      scopes: [],
      appIds: [],
      allApps: true,
      expiryDate: '2027-02-16T00:00:00Z',
      ...t,
    })
    const tokens: Record<string, Identity> = {
      [DEFAULT_PAT]: identity({}, 'sea_9c1'),
      [DEFAULT_CI]: identity({ kind: 2, name: 'codemagic-halka', userName: null, scopes: ['artifacts:write'], appIds: [3], allApps: false }, 'sci_7f2'),
    }
    for (const [k, v] of Object.entries(opts.tokens ?? {})) {
      tokens[k] = identity(v, k.slice(0, 7))
    }
    this.opts = {
      partSize: opts.partSize ?? 5 * 1024 * 1024,
      tokens,
      sessionScript: opts.sessionScript ?? [
        { status: 4, steps: [{ kind: 1, status: 3, message: 'uploaded' }, { kind: 5, status: 2, message: 'edits.bundles.upload' }] },
        {
          status: 5,
          steps: [{ kind: 1, status: 3, message: 'uploaded' }, { kind: 5, status: 3, message: 'internal track' }],
          ciResult: { exitCode: 0, summary: 'Delivered Halka 1.0.0 (42) to Google Play internal' },
          storeJobId: 9182,
        },
      ],
      oidcToken: opts.oidcToken ?? 'eyJhbGciOiJSUzI1NiJ9.eyJhdWQiOiJzdG9yZWFzeSIsInJlcG8iOiJrdXpleWxhYi9oYWxrYSJ9.c2lnbmF0dXJlLXZhbHVl',
      oidcRules: opts.oidcRules ?? [],
      uploadToken: opts.uploadToken ?? 'sut_Q4n8Lr2VxT9mKp6ZcH1sYb0aa11',
      uploadTokenTtlMs: opts.uploadTokenTtlMs ?? 15 * 60_000,
      remoteTools: opts.remoteTools ?? [],
    }
  }

  async start(): Promise<this> {
    this.server = createServer((req, res) => {
      this.handle(req, res).catch((err: unknown) => {
        if (!res.headersSent) {
          json(res, 500, { isSuccess: false, statusCode: 500, messages: [{ key: 'Internal', message: String(err) }] })
        } else {
          res.end()
        }
      })
    })
    await new Promise<void>((r) => this.server!.listen(0, '127.0.0.1', r))
    this.baseUrl = `http://127.0.0.1:${(this.server.address() as AddressInfo).port}`
    return this
  }

  async stop(): Promise<void> {
    this.server?.closeAllConnections()
    await new Promise<void>((r) => this.server?.close(() => r()) ?? r())
  }

  /** Requests to a path (without query), in order. */
  calls(method: string, pathPrefix: string): Recorded[] {
    return this.requests.filter((r) => r.method === method && r.path.startsWith(pathPrefix))
  }

  private identity(req: IncomingMessage): Identity | 'upload' | undefined {
    const h = req.headers.authorization
    const token = h?.startsWith('Bearer ') ? h.slice(7) : undefined
    if (!token) {
      return undefined
    }
    if (token === this.opts.uploadToken) {
      return 'upload'
    }
    return this.opts.tokens[token]
  }

  private async handle(req: IncomingMessage, res: ServerResponse): Promise<void> {
    const url = new URL(req.url ?? '/', this.baseUrl)
    const path = url.pathname
    const method = req.method ?? 'GET'

    if (path === '/mcp') {
      return this.handleMcp(req, res)
    }
    const raw = await readBody(req)
    let body: unknown = undefined
    if ((req.headers['content-type'] ?? '').includes('json') && raw.length) {
      body = JSON.parse(raw.toString('utf8'))
    }
    this.requests.push({ method, path, query: url.searchParams, headers: req.headers, body, bytes: raw.length })

    // ---- fake S3 ----
    if (path.startsWith('/s3/') && method === 'PUT') {
      return this.handleS3(path, raw, res)
    }
    // ---- fake CI OIDC issuers ----
    if (path === '/github-oidc' && method === 'GET') {
      if (req.headers.authorization !== 'Bearer gh-request-token-123456' || url.searchParams.get('audience') !== 'storeasy') {
        return json(res, 401, { message: 'bad request token or audience' })
      }
      return json(res, 200, { value: this.opts.oidcToken })
    }
    if (path === '/azure-oidc' && method === 'POST') {
      if (req.headers.authorization !== 'Bearer azure-system-access-token-1' || url.searchParams.get('serviceConnectionId') !== 'sc-1234') {
        return json(res, 401, { message: 'bad access token or service connection' })
      }
      return json(res, 200, { oidcToken: this.opts.oidcToken })
    }

    const fault = this.apiFaults.get(`${method} ${path}`)
    if (fault) {
      return fail(res, fault[0], fault[1], fault[2])
    }

    // ---- anonymous: OIDC exchange ----
    if (path === '/api/v1.0/oidc/exchange' && method === 'POST') {
      const b = body as { appId?: number; token?: string; provider?: number }
      if (!b?.token || !b.appId) {
        return fail(res, 400, 'ValidationError', 'appId and token are required.')
      }
      if (!b.token.startsWith('eyJ')) {
        return fail(res, 401, 'OidcTokenInvalid', 'The OIDC token is not valid.')
      }
      const rule = this.opts.oidcRules.find((r) => r.appId === b.appId && r.token === b.token)
      if (!rule) {
        return fail(res, 403, 'OidcClaimMismatch', "The OIDC token did not match this app's trusted publisher rules.")
      }
      this.issuedUploadTokens++
      return ok(res, { uploadToken: this.opts.uploadToken, expiryDate: new Date(Date.now() + this.opts.uploadTokenTtlMs).toISOString(), appId: b.appId })
    }

    const who = this.identity(req)
    if (!who) {
      // A careless server could echo what it got; the client must still not print it.
      return fail(res, 401, 'Unauthorized', `Invalid token: ${req.headers.authorization ?? 'none'}`)
    }

    if (path === '/api/v1.0/tokens/current' && method === 'GET') {
      if (who === 'upload') {
        return ok(res, { kind: 3, name: 'upload token', prefix: 'sut_Q4n', organizationId: 7, organizationName: 'Kuzey Lab', scopes: ['artifacts:write'], appIds: [3], allApps: false })
      }
      return ok(res, who)
    }
    if (path === '/api/v1.0/apps' && method === 'PATCH') {
      const page = (body as { pageNumber?: number })?.pageNumber ?? 1
      const pages = [
        [{ id: 1, name: 'Pusula', stores: [{ storeAppId: 11, store: 1, bundleId: 'com.kuzeylab.pusula' }] }],
        [{ id: 3, name: 'Halka', stores: [{ storeAppId: 31, store: 2, bundleId: 'com.kuzeylab.halka' }] }],
      ]
      return ok(res, pages[page - 1] ?? [], { currentPageNumber: page, totalPageCount: 2, totalDataCount: 2 })
    }
    if (path === '/api/v1.0/storeJobs/job' && method === 'GET') {
      const id = Number(url.searchParams.get('JobId'))
      if (id !== 9182) {
        return fail(res, 404, 'StoreJobNotFound', 'Job not found.')
      }
      return ok(res, {
        id,
        kind: 1,
        status: 3,
        store: 2,
        appId: 3,
        summary: 'Delivered to internal',
        steps: [
          { order: 1, name: 'edit.create', status: 2 },
          { order: 2, name: 'bundle.upload', status: 2, progress: 10, total: 10 },
        ],
      })
    }

    const open = /^\/api\/v1\.0\/apps\/(\d+)\/artifacts$/.exec(path)
    if (open && method === 'POST') {
      return this.openSession(Number(open[1]), who, body as Record<string, unknown>, res)
    }
    const complete = /^\/api\/v1\.0\/artifacts\/(\d+)\/complete$/.exec(path)
    if (complete && method === 'PUT') {
      return this.completeSession(Number(complete[1]), body as { parts: Array<{ partNumber: number; eTag: string }> }, res)
    }
    if (path === '/api/v1.0/artifacts/session' && method === 'GET') {
      return this.getSession(Number(url.searchParams.get('SessionId')), res)
    }
    if (path === '/api/v1.0/assets/upload' && method === 'POST') {
      const b = body as { purpose: number; fileName: string; sha256: string; size: number }
      const id = this.nextAsset++
      this.assets.set(id, { fileName: b.fileName, purpose: b.purpose, sha256: b.sha256, size: b.size, complete: false })
      return ok(res, { assetId: id, uploadUrl: `${this.baseUrl}/s3/asset-${id}/1?X-Amz-Signature=abc`, partSize: null, parts: [] })
    }
    if (path === '/api/v1.0/assets/asset/complete' && method === 'POST') {
      const b = body as { assetId: number }
      const a = this.assets.get(b.assetId)
      if (!a) {
        return fail(res, 404, 'AssetNotFound', 'Asset not found.')
      }
      const stored = this.s3Objects.get(`asset-${b.assetId}/1`)
      if (!stored || createHash('sha256').update(stored).digest('hex') !== a.sha256) {
        return fail(res, 400, 'UploadIncomplete', 'Stored object does not match.')
      }
      a.stored = stored
      a.complete = true
      return ok(res, { id: b.assetId, fileName: a.fileName })
    }
    return fail(res, 404, 'NotFound', `No route ${method} ${path}`)
  }

  private handleS3(path: string, raw: Buffer, res: ServerResponse): void {
    const [, , key, partStr] = path.split('/')
    const part = Number(partStr)
    const faults = this.partFaults.get(part)
    const f = faults?.shift()
    if (this.brokenParts.has(part) && key?.startsWith('session-')) {
      res.writeHead(500)
      res.end('broken')
      return
    }
    if (f === 'reset') {
      res.socket?.destroy()
      return
    }
    if (typeof f === 'number') {
      res.writeHead(f)
      res.end('fault')
      return
    }
    this.s3Objects.set(`${key}/${part}`, raw)
    const etag = `"${createHash('md5').update(raw).digest('hex')}"`
    const m = /^session-(\d+)$/.exec(key ?? '')
    if (m) {
      const s = this.sessions.get(Number(m[1]))
      s?.parts.set(part, { etag, bytes: raw.length, sha: raw })
    }
    res.writeHead(200, { ETag: etag })
    res.end()
  }

  private partUrl(sessionId: number, n: number): string {
    return `${this.baseUrl}/s3/session-${sessionId}/${n}?X-Amz-Signature=deadbeef&X-Amz-Expires=3600`
  }

  private openSession(appId: number, who: Identity | 'upload', body: Record<string, unknown>, res: ServerResponse): void {
    if (who !== 'upload' && who.allApps === false && !who.appIds.includes(appId)) {
      return fail(res, 403, 'AppForbidden', 'This token cannot upload to this app.')
    }
    const sha256 = String(body.sha256)
    const size = Number(body.size)
    const existing = [...this.sessions.values()].find((s) => s.appId === appId && s.sha256 === sha256 && s.status !== 7)
    const totalParts = (s: Session) => Math.ceil(s.size / s.partSize)
    if (existing && existing.status !== 1) {
      const id = this.nextSession++
      this.sessions.set(id, { ...existing, id, status: 7, parts: new Map(), polls: 0, originalSessionId: existing.id, body })
      return ok(res, { sessionId: id, duplicate: true, originalSessionId: existing.id, parts: [], uploadedParts: [] })
    }
    if (existing) {
      const missing: Array<{ partNumber: number; url: string }> = []
      for (let n = 1; n <= totalParts(existing); n++) {
        if (!existing.parts.has(n)) {
          missing.push({ partNumber: n, url: this.partUrl(existing.id, n) })
        }
      }
      return ok(res, {
        sessionId: existing.id,
        duplicate: false,
        resumed: true,
        partSize: existing.partSize,
        parts: missing,
        uploadedParts: [...existing.parts.entries()].map(([partNumber, p]) => ({ partNumber, eTag: p.etag })),
        urlExpiryDate: new Date(Date.now() + 3600_000).toISOString(),
      })
    }
    const id = this.nextSession++
    const s: Session = { id, appId, sha256, size, fileName: String(body.fileName), partSize: this.opts.partSize, status: 1, parts: new Map(), body, polls: 0 }
    this.sessions.set(id, s)
    const parts = Array.from({ length: totalParts(s) }, (_, i) => ({ partNumber: i + 1, url: this.partUrl(id, i + 1) }))
    ok(res, { sessionId: id, duplicate: false, resumed: false, partSize: s.partSize, parts, uploadedParts: [], urlExpiryDate: new Date(Date.now() + 3600_000).toISOString() })
  }

  private completeSession(id: number, body: { parts: Array<{ partNumber: number; eTag: string }> }, res: ServerResponse): void {
    const s = this.sessions.get(id)
    if (!s) {
      return fail(res, 404, 'ArtifactSessionNotFound', 'Session not found.')
    }
    if (s.status !== 1) {
      return ok(res, { id, status: s.status })
    }
    const total = Math.ceil(s.size / s.partSize)
    const bad = []
    for (let n = 1; n <= total; n++) {
      const sent = body.parts.find((p) => p.partNumber === n)
      if (!sent || sent.eTag !== s.parts.get(n)?.etag) {
        bad.push(n)
      }
    }
    if (bad.length) {
      return fail(res, 400, 'UploadIncomplete', `Parts missing or not matching: ${bad.join(', ')}`)
    }
    const whole = Buffer.concat([...Array(total).keys()].map((i) => s.parts.get(i + 1)!.sha))
    if (whole.length !== s.size || createHash('sha256').update(whole).digest('hex') !== s.sha256) {
      s.status = 6
      return fail(res, 400, 'SizeMismatch', 'Stored file does not match the declared size or hash.')
    }
    s.status = 2
    ok(res, { id, status: 2, fileName: s.fileName })
  }

  private getSession(id: number, res: ServerResponse): void {
    const s = this.sessions.get(id)
    if (!s) {
      return fail(res, 404, 'ArtifactSessionNotFound', 'Session not found.')
    }
    if (s.status === 7) {
      return ok(res, { id, status: 7, originalSessionId: s.originalSessionId, steps: [], ciResult: { exitCode: 0, summary: 'Duplicate' } })
    }
    const script = this.opts.sessionScript
    const poll = script[Math.min(s.polls, script.length - 1)]!
    s.polls++
    s.status = poll.status
    ok(res, { id, appId: s.appId, fileName: s.fileName, ciResult: null, ...poll })
  }

  private async handleMcp(req: IncomingMessage, res: ServerResponse): Promise<void> {
    const auth = req.headers.authorization
    if (!auth || !this.opts.tokens[auth.slice(7)]) {
      res.writeHead(401, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({ error: 'unauthorized' }))
      return
    }
    const raw = await readBody(req)
    const parsed = raw.length ? (JSON.parse(raw.toString('utf8')) as unknown) : undefined
    const server = new McpServer({ name: 'storeasy', version: 'test' }, { capabilities: { tools: {} } })
    server.setRequestHandler(ListToolsRequestSchema, async () => ({ tools: this.opts.remoteTools }))
    server.setRequestHandler(CallToolRequestSchema, async (r) => {
      const args = (r.params.arguments ?? {}) as Record<string, unknown>
      this.mcpCalls.push({ name: r.params.name, arguments: args, authorization: auth })
      return { content: [{ type: 'text', text: JSON.stringify({ tool: r.params.name, received: args }) }] }
    })
    const transport = new StreamableHTTPServerTransport({ sessionIdGenerator: undefined, enableJsonResponse: true })
    res.on('close', () => {
      void transport.close()
      void server.close()
    })
    await server.connect(transport)
    await transport.handleRequest(req, res, parsed)
  }
}

export const randomName = () => randomUUID().slice(0, 8)
