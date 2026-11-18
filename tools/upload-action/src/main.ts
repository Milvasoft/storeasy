// SPDX-License-Identifier: AGPL-3.0-only
// GitHub Action entry: reads the inputs, uploads with the storeasy library (OIDC or a token), writes the outputs.
import { appendFileSync } from 'node:fs'
import { randomUUID } from 'node:crypto'
import {
  ArtifactSource,
  type Env,
  ExitCode,
  type FetchLike,
  type Io,
  Logger,
  StoreasyError,
  errorMessage,
  exitCodeOf,
  parseStore,
  processIo,
  redact,
  resolveBaseUrl,
  runUpload,
} from 'storeasy'

export interface ActionDeps {
  env?: Env
  io?: Io
  fetch?: FetchLike
  sleep?: (ms: number) => Promise<void>
  waitIntervalMs?: number
}

/** Input as the runner passes it: INPUT_<NAME> with spaces as underscores, upper case (hyphens stay). */
export function input(env: Env, name: string): string | undefined {
  const v = env[`INPUT_${name.replace(/ /g, '_').toUpperCase()}`]?.trim()
  return v ? v : undefined
}

function boolInput(env: Env, name: string): boolean {
  const v = (input(env, name) ?? 'false').toLowerCase()
  if (!['true', 'false'].includes(v)) {
    throw new StoreasyError(`Input ${name} must be true or false, got "${v}".`)
  }
  return v === 'true'
}

/** Escapes a workflow command message (%, CR, LF). */
const escapeData = (s: string) => s.replace(/%/g, '%25').replace(/\r/g, '%0D').replace(/\n/g, '%0A')

function setOutputs(env: Env, outputs: Record<string, string | number | boolean | undefined>): void {
  const file = env.GITHUB_OUTPUT
  if (!file) {
    return
  }
  let text = ''
  for (const [k, v] of Object.entries(outputs)) {
    if (v === undefined) {
      continue
    }
    const value = redact(String(v))
    const delim = `ghadelimiter_${randomUUID()}`
    text += `${k}<<${delim}\n${value}\n${delim}\n`
  }
  appendFileSync(file, text)
}

export async function run(deps: ActionDeps = {}): Promise<number> {
  const env = deps.env ?? process.env
  const io = deps.io ?? processIo()
  const log = new Logger(io)
  try {
    const appRaw = input(env, 'app') ?? input(env, 'app-id')
    const appId = Number(appRaw)
    if (!appRaw || !Number.isInteger(appId) || appId <= 0) {
      throw new StoreasyError('Input app (or app-id) must be the storeasy app id, a positive integer.')
    }
    const file = input(env, 'file')
    if (!file) {
      throw new StoreasyError('Input file is required.')
    }
    const storeRaw = input(env, 'store')
    const store = parseStore(storeRaw)
    if (storeRaw && store === undefined) {
      throw new StoreasyError(`Input store must be apple, google or microsoft, got "${storeRaw}".`)
    }
    const timeout = Number(input(env, 'timeout-minutes') ?? '30')
    if (!Number.isFinite(timeout) || timeout <= 0) {
      throw new StoreasyError('Input timeout-minutes must be a positive number.')
    }
    const workspace = env.GITHUB_WORKSPACE
    const r = await runUpload({
      env,
      baseUrl: resolveBaseUrl({}, input(env, 'url') ?? 'https://storeasy.milvasoft.com'),
      fetch: deps.fetch,
      sleep: deps.sleep,
      log,
      client: 'storeasy-upload-action',
      token: input(env, 'token'),
      appId,
      filePath: workspace && !/^([A-Za-z]:)?[\\/]/.test(file) ? `${workspace}/${file}` : file,
      delivery: { store, track: input(env, 'track'), group: input(env, 'group'), notes: input(env, 'notes') },
      wait: boolInput(env, 'wait'),
      timeoutMs: timeout * 60_000,
      intervalMs: deps.waitIntervalMs,
      source: ArtifactSource.GitHubActions,
      // Resume state lives in the runner's temp folder: a re-run of the job on the same runner resumes.
      stateDir: env.RUNNER_TEMP ? `${env.RUNNER_TEMP}/storeasy-uploads` : undefined,
    })
    setOutputs(env, {
      'session-id': r.sessionId,
      duplicate: r.duplicate,
      'original-session-id': r.originalSessionId,
      sha256: r.sha256,
      status: r.status,
      summary: r.summary,
      'store-job-id': r.storeJobId,
      'session-url': r.sessionUrl,
      'exit-code': r.exitCode,
    })
    if (env.GITHUB_STEP_SUMMARY) {
      const head = r.duplicate ? 'Already received (same SHA-256), not delivered again' : (r.summary ?? 'Uploaded; storeasy is processing it')
      appendFileSync(
        env.GITHUB_STEP_SUMMARY,
        redact(`### storeasy\n\n${head}\n\n[Session ${r.sessionId}](${r.sessionUrl})${r.status ? ` · ${r.status}` : ''}\n`),
      )
    }
    if (r.exitCode !== ExitCode.Ok) {
      log.command(`::error title=storeasy::${escapeData(redact(r.summary ?? `exit code ${r.exitCode}`))}`)
    }
    return r.exitCode
  } catch (err) {
    const code = exitCodeOf(err)
    setOutputs(env, { 'exit-code': code })
    log.command(`::error title=storeasy::${escapeData(errorMessage(err))}`)
    return code
  }
}
