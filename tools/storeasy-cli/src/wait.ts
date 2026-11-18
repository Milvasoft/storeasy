// SPDX-License-Identifier: AGPL-3.0-only
import { SessionStatus, StepKind, StepStatus, enumName, enumValue } from './enums.js'
import { ExitCode, StoreasyError, errorMessage } from './errors.js'
import type { ApiClient } from './http.js'
import { formatBytes } from './log.js'

export interface SessionStep {
  kind: number | string
  status: number | string
  message?: string | null
  bytes?: number | null
  total?: number | null
  resumeCount?: number | null
}

/** ArtifactSessionDetailDto, the fields the tools read (CONTRACT 3.4). */
export interface SessionDetail {
  id: number
  appId?: number
  appName?: string
  fileName?: string
  status: number | string
  statusDescription?: string | null
  storeJobId?: number | null
  originalSessionId?: number | null
  steps?: SessionStep[]
  ciResult?: { exitCode?: number | null; summary?: string | null } | null
}

export interface WaitResult {
  exitCode: number
  status: string
  summary: string
  session: SessionDetail
  /** The session waited on when the upload was a duplicate. */
  followedSessionId?: number
}

export interface WaitOptions {
  intervalMs?: number
  timeoutMs?: number
  onLine?: (line: string) => void
  now?: () => number
}

const FINAL = new Set<number>([SessionStatus.Delivered, SessionStatus.Rejected, SessionStatus.Held, SessionStatus.Failed])

const EXIT_BY_STATUS: Record<number, number> = {
  [SessionStatus.Delivered]: ExitCode.Ok,
  [SessionStatus.Held]: ExitCode.Ok,
  [SessionStatus.Rejected]: ExitCode.Rejected,
  [SessionStatus.Failed]: ExitCode.Store,
}

export function getSession(api: ApiClient, sessionId: number): Promise<SessionDetail> {
  return api.request<SessionDetail>('GET', '/api/v1.0/artifacts/session', { query: { SessionId: sessionId } })
}

function stepLine(step: SessionStep): string {
  const kind = enumName(StepKind, step.kind).toLowerCase()
  const status = enumName(StepStatus, step.status).toLowerCase()
  const bytes = step.total ? ` ${formatBytes(step.bytes ?? 0)} / ${formatBytes(step.total)}` : ''
  const resumed = step.resumeCount ? `, ${step.resumeCount} resumed` : ''
  return `${kind.padEnd(8)} ${status}${step.message ? `: ${step.message}` : ''}${bytes}${resumed}`
}

/**
 * Polls GET artifacts/session until the session is final and streams step changes as lines. A duplicate session is
 * followed to its original (design 13.17). Exit code from `ciResult.exitCode`, else from the status.
 */
export async function waitForSession(api: ApiClient, sessionId: number, opts: WaitOptions = {}): Promise<WaitResult> {
  const interval = opts.intervalMs ?? 3000
  const timeout = opts.timeoutMs ?? 30 * 60_000
  const now = opts.now ?? Date.now
  const started = now()
  const seen = new Map<string, string>()
  let lastDescription = ''
  let id = sessionId
  let followed: number | undefined
  for (;;) {
    let s: SessionDetail | undefined
    try {
      s = await getSession(api, id)
    } catch (err) {
      if (err instanceof StoreasyError && err.status !== undefined && err.status >= 400 && err.status < 500) {
        throw err
      }
      opts.onLine?.(`poll failed, retrying: ${errorMessage(err)}`)
    }
    if (s) {
      const status = enumValue(SessionStatus, s.status) ?? 0
      if (status === SessionStatus.Duplicate && s.originalSessionId && s.originalSessionId !== id) {
        opts.onLine?.(`duplicate of session ${s.originalSessionId}; waiting for it`)
        id = s.originalSessionId
        followed = id
        seen.clear()
        continue
      }
      for (const step of s.steps ?? []) {
        const key = String(enumValue(StepKind, step.kind) ?? step.kind)
        const line = stepLine(step)
        if (seen.get(key) !== line && enumValue(StepStatus, step.status) !== StepStatus.Pending) {
          seen.set(key, line)
          opts.onLine?.(line)
        }
      }
      if (s.statusDescription && s.statusDescription !== lastDescription) {
        lastDescription = s.statusDescription
      }
      const exit = s.ciResult?.exitCode
      if ((exit !== undefined && exit !== null) || FINAL.has(status)) {
        const code = exit ?? EXIT_BY_STATUS[status] ?? ExitCode.Error
        return {
          exitCode: code,
          status: enumName(SessionStatus, s.status),
          summary: s.ciResult?.summary ?? s.statusDescription ?? enumName(SessionStatus, s.status),
          session: s,
          followedSessionId: followed,
        }
      }
    }
    if (now() - started >= timeout) {
      throw new StoreasyError(
        `Timed out after ${Math.round(timeout / 60_000)} min waiting for session ${id}${lastDescription ? ` (${lastDescription})` : ''}. The delivery continues on the server.`,
        { exitCode: ExitCode.Timeout },
      )
    }
    await api.sleep(interval)
  }
}
