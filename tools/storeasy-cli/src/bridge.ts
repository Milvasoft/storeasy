// SPDX-License-Identifier: AGPL-3.0-only
import { readdir, stat } from 'node:fs/promises'
import { homedir } from 'node:os'
import { extname, join, resolve } from 'node:path'
import { Client } from '@modelcontextprotocol/sdk/client/index.js'
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js'
import { Server } from '@modelcontextprotocol/sdk/server/index.js'
import { CallToolRequestSchema, type CallToolResult, ListToolsRequestSchema, type Tool } from '@modelcontextprotocol/sdk/types.js'
import { uploadAsset } from './assets.js'
import { detectSource } from './ci.js'
import type { Env } from './config.js'
import { AssetPurpose, enumValue, parseStore } from './enums.js'
import { StoreasyError, errorMessage } from './errors.js'
import type { ApiClient } from './http.js'
import { redact } from './redact.js'
import { uploadArtifact } from './upload.js'
import { VERSION } from './version.js'
import { waitForSession } from './wait.js'

/** JSON Schema keyword a remote tool puts on an asset id argument that takes local files (CONTRACT 6.1). */
export const UPLOAD_KEYWORD = 'x-storeasy-upload'

export interface UploadSpec {
  kind: 'file' | 'files' | 'folder'
  purpose: number
  accept?: string[]
  maxFiles?: number
}

/** The remote side as the bridge needs it; an MCP `Client` fits. */
export interface RemoteTools {
  listTools(params?: { cursor?: string }): Promise<{ tools: Tool[]; nextCursor?: string }>
  callTool(params: { name: string; arguments?: Record<string, unknown> }): Promise<unknown>
}

export interface BridgeOptions {
  /** REST client with the PAT (asset uploads, artifact intake). */
  api: ApiClient
  /** Opens the remote MCP session; called lazily and again after a failure. */
  connect: () => Promise<RemoteTools>
  env?: Env
  /** Base directory of relative paths (default: process.cwd()). */
  cwd?: string
  /** Test hook. */
  waitIntervalMs?: number
}

/** Opens an MCP client session to `<baseUrl>/mcp` with the PAT. */
export async function connectRemote(api: ApiClient): Promise<Client> {
  const token = await api.token()
  if (!token) {
    throw new StoreasyError('No token. Set STOREASY_TOKEN to a personal access token (sea_...) or run "storeasy login".')
  }
  const client = new Client({ name: 'storeasy-mcp', version: VERSION })
  const transport = new StreamableHTTPClientTransport(new URL(`${api.baseUrl}/mcp`), {
    requestInit: { headers: { Authorization: `Bearer ${token}`, 'User-Agent': api.userAgent.replace(/^storeasy-cli/, 'storeasy-mcp') } },
    fetch: (url, init) => api.fetch(String(url), init),
  })
  await client.connect(transport)
  return client
}

export function uploadSpecs(tool: Tool): Map<string, UploadSpec> {
  const out = new Map<string, UploadSpec>()
  const props = (tool.inputSchema?.properties ?? {}) as Record<string, Record<string, unknown>>
  for (const [name, schema] of Object.entries(props)) {
    const raw = schema?.[UPLOAD_KEYWORD] as Record<string, unknown> | undefined
    if (!raw || typeof raw !== 'object') {
      continue
    }
    const kind = raw.kind
    if (kind !== 'file' && kind !== 'files' && kind !== 'folder') {
      continue
    }
    out.set(name, {
      kind,
      purpose: enumValue(AssetPurpose, raw.purpose) ?? AssetPurpose.Screenshot,
      accept: Array.isArray(raw.accept) ? raw.accept.map((e) => String(e).toLowerCase()) : undefined,
      maxFiles: typeof raw.maxFiles === 'number' ? raw.maxFiles : undefined,
    })
  }
  return out
}

/** The tool as local clients see it: asset id arguments become path arguments. */
export function localTool(tool: Tool, specs: Map<string, UploadSpec>): Tool {
  if (!specs.size) {
    return tool
  }
  const props = { ...((tool.inputSchema.properties ?? {}) as Record<string, Record<string, unknown>>) }
  for (const [name, spec] of specs) {
    const { [UPLOAD_KEYWORD]: _drop, type: _t, items: _i, ...rest } = props[name] ?? {}
    const accept = spec.accept?.length ? ` Accepted: ${spec.accept.join(', ')}.` : ''
    const what =
      spec.kind === 'folder'
        ? `Local folder path; every matching file directly inside it is uploaded, in file name order.${accept}`
        : spec.kind === 'files'
          ? `Local file paths, uploaded in the given order.${accept}`
          : `Local file path, uploaded before the call.${accept}`
    const description = [rest.description, what].filter(Boolean).join(' ')
    props[name] =
      spec.kind === 'files'
        ? { ...rest, type: 'array', items: { type: 'string' }, description }
        : { ...rest, type: 'string', description }
  }
  return { ...tool, inputSchema: { ...tool.inputSchema, properties: props } }
}

const collator = new Intl.Collator('en', { numeric: true, sensitivity: 'base' })

function expandPath(p: string, cwd: string): string {
  const withHome = p === '~' || p.startsWith('~/') || p.startsWith('~\\') ? join(homedir(), p.slice(1)) : p
  return resolve(cwd, withHome)
}

function checkAccept(path: string, spec: UploadSpec): void {
  if (spec.accept?.length && !spec.accept.includes(extname(path).toLowerCase())) {
    throw new StoreasyError(`${path}: file type not accepted (${spec.accept.join(', ')}).`)
  }
}

/** Local paths of one argument, in upload order. */
export async function collectFiles(value: unknown, spec: UploadSpec, cwd: string): Promise<string[]> {
  let files: string[]
  if (spec.kind === 'folder') {
    if (typeof value !== 'string') {
      throw new StoreasyError('Expected a folder path.')
    }
    const dir = expandPath(value, cwd)
    const st = await stat(dir).catch(() => undefined)
    if (!st?.isDirectory()) {
      throw new StoreasyError(`Folder not found: ${dir}`)
    }
    const entries = await readdir(dir, { withFileTypes: true })
    files = entries
      .filter((e) => e.isFile() && !e.name.startsWith('.'))
      .map((e) => e.name)
      .filter((n) => !spec.accept?.length || spec.accept.includes(extname(n).toLowerCase()))
      .sort(collator.compare)
      .map((n) => join(dir, n))
    if (!files.length) {
      throw new StoreasyError(`No matching files in ${dir}${spec.accept?.length ? ` (${spec.accept.join(', ')})` : ''}.`)
    }
  } else if (spec.kind === 'files') {
    const list = Array.isArray(value) ? value : [value]
    if (!list.every((v) => typeof v === 'string')) {
      throw new StoreasyError('Expected an array of file paths.')
    }
    files = (list as string[]).map((v) => expandPath(v, cwd))
    files.forEach((f) => checkAccept(f, spec))
  } else {
    if (typeof value !== 'string') {
      throw new StoreasyError('Expected a file path.')
    }
    files = [expandPath(value, cwd)]
    checkAccept(files[0]!, spec)
  }
  if (spec.maxFiles && files.length > spec.maxFiles) {
    throw new StoreasyError(`Too many files: ${files.length}, at most ${spec.maxFiles}.`)
  }
  return files
}

const isIds = (v: unknown) => typeof v === 'number' || (Array.isArray(v) && v.length > 0 && v.every((x) => typeof x === 'number'))

/** Uploads the local files of every marked argument and returns the arguments with asset ids in their place. */
export async function replacePaths(
  args: Record<string, unknown>,
  specs: Map<string, UploadSpec>,
  upload: (path: string, purpose: number) => Promise<number>,
  cwd: string,
): Promise<{ args: Record<string, unknown>; uploaded: number }> {
  const out = { ...args }
  let uploaded = 0
  for (const [name, spec] of specs) {
    const value = out[name]
    if (value === undefined || value === null || isIds(value)) {
      continue
    }
    const files = await collectFiles(value, spec, cwd)
    const ids: number[] = []
    for (const f of files) {
      try {
        ids.push(await upload(f, spec.purpose))
      } catch (err) {
        throw new StoreasyError(`${name}: ${f}: ${errorMessage(err)}`)
      }
      uploaded++
    }
    out[name] = spec.kind === 'file' ? ids[0] : ids
  }
  return { args: out, uploaded }
}

export const ARTIFACT_UPLOAD_TOOL: Tool = {
  name: 'artifact_upload',
  title: 'Upload a build (local file)',
  description:
    'Uploads a local IPA, AAB, APK or MSIX to storeasy build intake for an app. The package is read and checked, then the ' +
    "app's delivery rule takes it to a test channel (TestFlight internal group, Play test track, Microsoft package " +
    'flight). Production and review submission are never automatic. Same SHA-256 twice is not delivered again. ' +
    'Runs in the local storeasy-mcp bridge only.',
  inputSchema: {
    type: 'object',
    properties: {
      path: { type: 'string', description: 'Local path of the package.' },
      appId: { type: 'integer', description: 'storeasy app id.' },
      store: { type: 'string', enum: ['apple', 'google', 'microsoft'], description: 'Optional; checked against the package.' },
      track: { type: 'string', description: 'Play test track; overrides the delivery rule for this upload.' },
      group: { type: 'string', description: 'TestFlight internal group; overrides the delivery rule for this upload.' },
      notes: { type: 'string', description: 'Release notes or What to Test.' },
      wait: { type: 'boolean', description: 'Wait for the delivery result (up to 10 minutes). Default false.' },
    },
    required: ['path', 'appId'],
  },
  annotations: { destructiveHint: false, idempotentHint: true, openWorldHint: true },
}

const text = (t: string, isError = false): CallToolResult => ({ content: [{ type: 'text', text: redact(t) }], isError })

/**
 * The stdio MCP server of `storeasy-mcp`: lists the remote `/mcp` tools, rewrites the asset id arguments marked with
 * x-storeasy-upload into local path arguments, uploads those files on a call and forwards the call with asset ids.
 * Adds the local `artifact_upload` tool.
 */
export function createBridge(opts: BridgeOptions): Server {
  const cwd = opts.cwd ?? process.cwd()
  const env = opts.env ?? process.env
  const server = new Server({ name: 'storeasy-mcp', version: VERSION }, { capabilities: { tools: {} } })
  let remote: Promise<RemoteTools> | undefined
  const specsByTool = new Map<string, Map<string, UploadSpec>>()

  const getRemote = () => {
    remote ??= opts.connect().catch((err) => {
      remote = undefined
      throw err
    })
    return remote
  }

  const refreshTools = async (): Promise<Tool[]> => {
    const r = await getRemote()
    const tools: Tool[] = []
    let cursor: string | undefined
    for (let i = 0; i < 100; i++) {
      const page = await r.listTools(cursor ? { cursor } : undefined)
      tools.push(...page.tools)
      cursor = page.nextCursor
      if (!cursor) {
        break
      }
    }
    specsByTool.clear()
    for (const t of tools) {
      specsByTool.set(t.name, uploadSpecs(t))
    }
    return tools.filter((t) => t.name !== ARTIFACT_UPLOAD_TOOL.name)
  }

  server.setRequestHandler(ListToolsRequestSchema, async () => {
    const tools = await refreshTools()
    return { tools: [...tools.map((t) => localTool(t, specsByTool.get(t.name)!)), ARTIFACT_UPLOAD_TOOL] }
  })

  server.setRequestHandler(CallToolRequestSchema, async (req) => {
    const { name } = req.params
    const args = (req.params.arguments ?? {}) as Record<string, unknown>
    try {
      if (name === ARTIFACT_UPLOAD_TOOL.name) {
        return await artifactUpload(opts, args, cwd, env)
      }
      if (!specsByTool.has(name)) {
        await refreshTools()
      }
      const specs = specsByTool.get(name) ?? new Map<string, UploadSpec>()
      const { args: remoteArgs } = await replacePaths(args, specs, (p, purpose) => uploadAsset(opts.api, p, purpose), cwd)
      const r = await getRemote()
      return (await r.callTool({ name, arguments: remoteArgs })) as CallToolResult
    } catch (err) {
      return text(errorMessage(err), true)
    }
  })

  return server
}

async function artifactUpload(opts: BridgeOptions, args: Record<string, unknown>, cwd: string, env: Env): Promise<CallToolResult> {
  const path = typeof args.path === 'string' ? expandPath(args.path, cwd) : undefined
  const appId = Number(args.appId)
  if (!path || !Number.isInteger(appId) || appId <= 0) {
    return text('artifact_upload needs path (string) and appId (positive integer).', true)
  }
  const store = parseStore(typeof args.store === 'string' ? args.store : undefined)
  const { sourceRef } = detectSource(env)
  const str = (v: unknown) => (typeof v === 'string' && v ? v : undefined)
  const up = await uploadArtifact({
    api: opts.api,
    appId,
    filePath: path,
    source: 8, // Manual: a person asked for it through an MCP client
    sourceRef,
    delivery: { store, track: str(args.track), group: str(args.group), notes: str(args.notes) },
  })
  const result: Record<string, unknown> = {
    sessionId: up.sessionId,
    duplicate: up.duplicate,
    originalSessionId: up.originalSessionId,
    sha256: up.sha256,
    size: up.size,
    parts: up.partsTotal,
    sessionUrl: `${opts.api.baseUrl}/intake/sessions/${up.sessionId}`,
  }
  if (args.wait === true) {
    const w = await waitForSession(opts.api, up.sessionId, { timeoutMs: 10 * 60_000, intervalMs: opts.waitIntervalMs })
    Object.assign(result, { status: w.status, summary: w.summary, exitCode: w.exitCode, storeJobId: w.session.storeJobId })
    return text(JSON.stringify(result, null, 2), w.exitCode !== 0)
  }
  return text(JSON.stringify(result, null, 2))
}
