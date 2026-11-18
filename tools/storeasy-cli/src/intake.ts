// SPDX-License-Identifier: AGPL-3.0-only
import { type ResolveAuthOptions, resolveAuth } from './auth.js'
import { type SourceRef, detectSource } from './ci.js'
import type { ArtifactSource } from './enums.js'
import { ExitCode, StoreasyError } from './errors.js'
import { type Logger, formatBytes } from './log.js'
import { type Delivery, type UploadProgress, uploadArtifact } from './upload.js'
import { waitForSession } from './wait.js'

export interface RunUploadOptions extends Omit<ResolveAuthOptions, 'appId' | 'log'> {
  log: Logger
  appId: number
  filePath: string
  delivery?: Delivery
  wait?: boolean
  timeoutMs?: number
  intervalMs?: number
  stateDir?: string
  concurrency?: number
  /** Overrides the detected source (the action and the task set theirs). */
  source?: ArtifactSource
  sourceRef?: SourceRef
}

export interface RunUploadResult {
  exitCode: number
  sessionId: number
  duplicate: boolean
  originalSessionId?: number
  sha256: string
  status?: string
  summary?: string
  storeJobId?: number
  sessionUrl: string
}

/**
 * The whole upload command shared by the CLI, the GitHub Action and the Azure DevOps task: resolve auth, upload,
 * optionally wait, and turn the result into an exit code (design 13.21).
 */
export async function runUpload(opts: RunUploadOptions): Promise<RunUploadResult> {
  const { log } = opts
  const auth = resolveAuth({ ...opts, log, appId: opts.appId })
  if (auth.kind === 'none') {
    throw new StoreasyError(
      'No credentials. Set STOREASY_TOKEN (a CI token), enable OIDC (GitHub: permissions id-token: write), or run "storeasy login".',
      { exitCode: ExitCode.Auth },
    )
  }
  const detected = detectSource(opts.env)
  const source = opts.source ?? detected.source
  const sourceRef = opts.sourceRef ?? detected.sourceRef
  log.info(`Uploading ${opts.filePath} to app ${opts.appId} (${auth.describe})`)

  let lastPct = -1
  const onProgress = (p: UploadProgress) => {
    const pct = p.total ? Math.floor((p.done / p.total) * 100) : 100
    if (p.phase === 'hash') {
      if (pct === 100) {
        log.progress(`hashed ${formatBytes(p.total)}`)
      }
      return
    }
    // On a terminal every update; in a CI log every 10 percent.
    const step = log.io.stderr.isTTY ? 1 : 10
    if (pct === 100 || pct - lastPct >= step || lastPct < 0) {
      lastPct = pct
      log.progress(`upload ${String(pct).padStart(3)}% ${formatBytes(p.done)} / ${formatBytes(p.total)} (part ${p.partsDone}/${p.partsTotal})`)
    }
  }

  const up = await uploadArtifact({
    api: auth.api,
    appId: opts.appId,
    filePath: opts.filePath,
    delivery: opts.delivery,
    source,
    sourceRef,
    stateDir: opts.stateDir,
    concurrency: opts.concurrency,
    onProgress,
    onInfo: (m) => log.info(m),
  })
  log.endProgress()
  const sessionUrl = `${auth.api.baseUrl}/intake/sessions/${up.sessionId}`
  const result: RunUploadResult = {
    exitCode: ExitCode.Ok,
    sessionId: up.sessionId,
    duplicate: up.duplicate,
    originalSessionId: up.originalSessionId,
    sha256: up.sha256,
    sessionUrl,
  }
  if (up.duplicate) {
    log.info(`This package was already received (session ${up.originalSessionId ?? '?'}); it is not uploaded or delivered again.`)
  } else {
    const retried = up.partRetries ? `, ${up.partRetries} part retries` : ''
    const resumed = up.resumed ? `, resumed (${up.partsTotal - up.partsUploaded} parts were already there)` : ''
    log.info(`Uploaded ${up.fileName} (${formatBytes(up.size)}, ${up.partsTotal} parts${resumed}${retried}). sha256 ${up.sha256}`)
  }
  log.info(`Session ${up.sessionId}: ${sessionUrl}`)
  if (!opts.wait) {
    return result
  }
  const w = await waitForSession(auth.api, up.sessionId, {
    intervalMs: opts.intervalMs,
    timeoutMs: opts.timeoutMs,
    onLine: (line) => log.info(line),
  })
  return {
    ...result,
    exitCode: w.exitCode,
    status: w.status,
    summary: w.summary,
    storeJobId: w.session.storeJobId ?? undefined,
  }
}
