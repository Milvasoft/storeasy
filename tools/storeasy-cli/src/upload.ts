// SPDX-License-Identifier: AGPL-3.0-only
import { createHash } from 'node:crypto'
import { createReadStream, existsSync, readFileSync, rmSync } from 'node:fs'
import { open, stat } from 'node:fs/promises'
import { basename, join } from 'node:path'
import type { SourceRef } from './ci.js'
import { writePrivateFile } from './config.js'
import type { ArtifactSource } from './enums.js'
import { StoreasyError, errorMessage } from './errors.js'
import { type ApiClient, causeOf, retryDelay } from './http.js'
import { stripQuery } from './redact.js'

export interface PartUrl {
  partNumber: number
  url: string
}

export interface UploadedPart {
  partNumber: number
  eTag: string
}

/** ArtifactUploadDto (CONTRACT 3.2). */
export interface ArtifactUploadDto {
  sessionId: number
  duplicate: boolean
  resumed?: boolean
  originalSessionId?: number | null
  partSize?: number
  parts?: PartUrl[]
  uploadedParts?: UploadedPart[]
  urlExpiryDate?: string
}

export interface Delivery {
  store?: number
  track?: string
  group?: string
  notes?: string
}

export interface UploadProgress {
  phase: 'hash' | 'upload'
  done: number
  total: number
  partsDone?: number
  partsTotal?: number
}

export interface UploadOptions {
  api: ApiClient
  appId: number
  filePath: string
  delivery?: Delivery
  source: ArtifactSource
  sourceRef?: SourceRef
  onProgress?: (p: UploadProgress) => void
  onInfo?: (message: string) => void
  /** Directory for resume state (`<dir>/<sha256>.json`); omit to keep no state on disk. */
  stateDir?: string
  /** Parts uploaded in parallel (default 4). */
  concurrency?: number
  /** Attempts per part after the first (default 3). */
  partRetries?: number
}

export interface UploadResult {
  sessionId: number
  duplicate: boolean
  originalSessionId?: number
  resumed: boolean
  sha256: string
  size: number
  fileName: string
  partsTotal: number
  /** Parts sent by this run (resumed parts are not counted). */
  partsUploaded: number
  /** Part PUTs that failed and were sent again. */
  partRetries: number
  status?: unknown
}

const CONTENT_TYPES: Record<string, string> = {
  '.ipa': 'application/octet-stream',
  '.aab': 'application/octet-stream',
  '.apk': 'application/vnd.android.package-archive',
  '.msix': 'application/msix',
  '.msixbundle': 'application/msixbundle',
  '.appx': 'application/appx',
  '.appxbundle': 'application/appxbundle',
}

export function contentTypeOf(fileName: string, fallback = 'application/octet-stream'): string {
  const m = /\.[A-Za-z0-9]+$/.exec(fileName)
  return (m && CONTENT_TYPES[m[0].toLowerCase()]) ?? fallback
}

/** SHA-256 (lowercase hex) of a file, streamed. */
export async function sha256File(path: string, onProgress?: (done: number) => void): Promise<string> {
  const hash = createHash('sha256')
  let done = 0
  for await (const chunk of createReadStream(path, { highWaterMark: 1024 * 1024 })) {
    hash.update(chunk as Buffer)
    done += (chunk as Buffer).length
    onProgress?.(done)
  }
  return hash.digest('hex')
}

interface ResumeState {
  sessionId: number
  appId: number
  partSize: number
  parts: Record<string, string>
}

function readState(path: string | undefined): ResumeState | undefined {
  if (!path || !existsSync(path)) {
    return undefined
  }
  try {
    return JSON.parse(readFileSync(path, 'utf8')) as ResumeState
  } catch {
    return undefined
  }
}

/**
 * Uploads a build to storeasy (CONTRACT 3.2 and 3.3): opens (or resumes) the upload session, PUTs every missing
 * part to its presigned URL with retries, refreshes expired URLs by opening the session again, then completes.
 */
export async function uploadArtifact(opts: UploadOptions): Promise<UploadResult> {
  const { api } = opts
  const st = await stat(opts.filePath).catch(() => undefined)
  if (!st || !st.isFile()) {
    throw new StoreasyError(`File not found: ${opts.filePath}`)
  }
  if (st.size === 0) {
    throw new StoreasyError(`File is empty: ${opts.filePath}`)
  }
  const size = st.size
  const fileName = basename(opts.filePath)
  const sha256 = await sha256File(opts.filePath, (done) => opts.onProgress?.({ phase: 'hash', done, total: size }))

  const body = {
    fileName,
    size,
    sha256,
    contentType: contentTypeOf(fileName),
    source: opts.source,
    sourceRef: opts.sourceRef,
    delivery: opts.delivery && Object.values(opts.delivery).some((v) => v !== undefined) ? opts.delivery : undefined,
  }
  const openSession = () =>
    api.request<ArtifactUploadDto>('POST', `/api/v1.0/apps/${opts.appId}/artifacts`, { body, retry: true })

  let session = await openSession()
  const base: Omit<UploadResult, 'partsTotal' | 'partsUploaded' | 'partRetries' | 'resumed'> = {
    sessionId: session.sessionId,
    duplicate: Boolean(session.duplicate),
    originalSessionId: session.originalSessionId ?? undefined,
    sha256,
    size,
    fileName,
  }
  if (session.duplicate) {
    return { ...base, resumed: false, partsTotal: 0, partsUploaded: 0, partRetries: 0 }
  }
  const partSize = session.partSize ?? 0
  if (!partSize || partSize <= 0) {
    throw new StoreasyError('The server did not send a part size for the upload session.')
  }
  const partsTotal = Math.ceil(size / partSize)
  const statePath = opts.stateDir ? join(opts.stateDir, `${sha256}.json`) : undefined
  const etags = new Map<number, string>()
  const missing = new Set((session.parts ?? []).map((p) => p.partNumber))
  for (const p of session.uploadedParts ?? []) {
    etags.set(p.partNumber, p.eTag)
  }
  // Parts this machine sent before that the server did not list (an older server without ListParts): keep them unless
  // the server asks for them again.
  const saved = readState(statePath)
  if (saved && saved.sessionId === session.sessionId && saved.partSize === partSize) {
    for (const [n, e] of Object.entries(saved.parts)) {
      if (!missing.has(Number(n)) && !etags.has(Number(n))) {
        etags.set(Number(n), e)
      }
    }
  }
  const resumed = Boolean(session.resumed) || etags.size > 0
  if (resumed) {
    opts.onInfo?.(`Resuming upload session ${session.sessionId}: ${etags.size} of ${partsTotal} parts already uploaded.`)
  }
  const saveState = () => {
    if (statePath) {
      writePrivateFile(
        statePath,
        JSON.stringify({ sessionId: session.sessionId, appId: opts.appId, partSize, parts: Object.fromEntries(etags) } satisfies ResumeState),
      )
    }
  }

  const urls = new Map((session.parts ?? []).map((p) => [p.partNumber, p.url]))
  const queue = [...urls.keys()].filter((n) => !etags.has(n)).sort((a, b) => a - b)
  for (let n = 1; n <= partsTotal; n++) {
    if (!etags.has(n) && !urls.has(n)) {
      throw new StoreasyError(`The server sent no upload URL for part ${n} of ${partsTotal}.`)
    }
  }

  let refreshing: Promise<void> | undefined
  const refreshUrls = async () => {
    refreshing ??= (async () => {
      session = await openSession()
      for (const p of session.parts ?? []) {
        urls.set(p.partNumber, p.url)
      }
    })().finally(() => {
      refreshing = undefined
    })
    await refreshing
  }

  let bytesDone = [...etags.keys()].reduce((sum, n) => sum + partLength(n, partSize, size), 0)
  let partsUploaded = 0
  let partRetries = 0
  const report = () =>
    opts.onProgress?.({ phase: 'upload', done: bytesDone, total: size, partsDone: etags.size, partsTotal })
  report()

  const fh = await open(opts.filePath, 'r')
  try {
    const maxAttempts = (opts.partRetries ?? 3) + 1
    const uploadPart = async (n: number) => {
      const len = partLength(n, partSize, size)
      const buf = Buffer.alloc(len)
      await fh.read(buf, 0, len, (n - 1) * partSize)
      let lastError = ''
      for (let attempt = 0; attempt < maxAttempts; attempt++) {
        if (attempt > 0) {
          partRetries++
        }
        const url = urls.get(n)!
        try {
          const res = await api.fetch(url, { method: 'PUT', body: buf })
          if (res.ok) {
            const etag = res.headers.get('etag')
            await res.body?.cancel().catch(() => undefined)
            if (!etag) {
              throw new StoreasyError(`Storage accepted part ${n} but sent no ETag header.`)
            }
            etags.set(n, etag)
            partsUploaded++
            bytesDone += len
            saveState()
            report()
            return
          }
          lastError = `HTTP ${res.status}`
          await res.body?.cancel().catch(() => undefined)
          if (res.status === 403 || res.status === 401) {
            // Expired or revoked presigned URL: ask the server for fresh ones (same session, missing parts only).
            await refreshUrls()
            continue
          }
          if (res.status >= 400 && res.status < 500 && res.status !== 408 && res.status !== 429) {
            break
          }
          await api.sleep(retryDelay(res, api.retryPolicy.baseDelayMs, attempt))
        } catch (err) {
          if (err instanceof StoreasyError) {
            throw err
          }
          lastError = errorMessage(causeOf(err))
          await api.sleep(api.retryPolicy.baseDelayMs * 2 ** attempt)
        }
      }
      throw new StoreasyError(
        `Uploading part ${n} of ${partsTotal} failed (${lastError}, ${stripQuery(urls.get(n) ?? '')}). Run the same command again to resume.`,
      )
    }

    const workers = Math.max(1, Math.min(opts.concurrency ?? 4, queue.length))
    let next = 0
    let failure: unknown
    await Promise.all(
      Array.from({ length: workers }, async () => {
        while (failure === undefined && next < queue.length) {
          const n = queue[next++]!
          try {
            await uploadPart(n)
          } catch (err) {
            failure ??= err
          }
        }
      }),
    )
    if (failure !== undefined) {
      throw failure
    }
  } finally {
    await fh.close()
  }

  const parts: UploadedPart[] = []
  for (let n = 1; n <= partsTotal; n++) {
    parts.push({ partNumber: n, eTag: etags.get(n)! })
  }
  const completed = await api.request<{ status?: unknown }>('PUT', `/api/v1.0/artifacts/${session.sessionId}/complete`, {
    body: { parts },
    retry: true,
  })
  if (statePath) {
    rmSync(statePath, { force: true })
  }
  return { ...base, resumed, partsTotal, partsUploaded, partRetries, status: completed?.status }
}

function partLength(n: number, partSize: number, size: number): number {
  return Math.min(n * partSize, size) - (n - 1) * partSize
}
