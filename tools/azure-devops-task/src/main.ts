// SPDX-License-Identifier: AGPL-3.0-only
// Azure Pipelines task StoreasyUpload@1. Reads inputs and endpoint data the way the agent passes them to a Node
// handler (environment variables), so it needs no task library.
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
  registerSecret,
  resolveBaseUrl,
  runUpload,
} from 'storeasy'

export interface TaskDeps {
  env?: Env
  io?: Io
  fetch?: FetchLike
  sleep?: (ms: number) => Promise<void>
  waitIntervalMs?: number
}

/** Input as the agent passes it: INPUT_<NAME> upper case. */
export function input(env: Env, name: string): string | undefined {
  const v = env[`INPUT_${name.replace(/[ .]/g, '_').toUpperCase()}`]?.trim()
  return v ? v : undefined
}

/** System.AccessToken of the job, from the SystemVssConnection endpoint (or SYSTEM_ACCESSTOKEN if mapped). */
export function systemAccessToken(env: Env): string | undefined {
  const direct = env.ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN ?? env.SYSTEM_ACCESSTOKEN
  if (direct) {
    return direct
  }
  const raw = env.ENDPOINT_AUTH_SYSTEMVSSCONNECTION
  if (!raw) {
    return undefined
  }
  try {
    const parsed = JSON.parse(raw) as { parameters?: Record<string, string> }
    return parsed.parameters?.AccessToken ?? parsed.parameters?.accessToken
  } catch {
    return undefined
  }
}

/** Escapes a logging command message (%, CR, LF). */
const escapeMessage = (s: string) => s.replace(/%/g, '%AZP25').replace(/\r/g, '%0D').replace(/\n/g, '%0A')
/** Escapes a logging command property value. */
const escapeProperty = (s: string) => escapeMessage(s).replace(/;/g, '%3B').replace(/\]/g, '%5D')

function setVariable(log: Logger, name: string, value: string | number | boolean | undefined): void {
  if (value === undefined) {
    return
  }
  log.command(`##vso[task.setvariable variable=${name};isOutput=true]${escapeMessage(redact(String(value)))}`)
}

export async function run(deps: TaskDeps = {}): Promise<number> {
  const env = deps.env ?? process.env
  const io = deps.io ?? processIo()
  const log = new Logger(io)
  try {
    const appRaw = input(env, 'appId')
    const appId = Number(appRaw)
    if (!appRaw || !Number.isInteger(appId) || appId <= 0) {
      throw new StoreasyError('Input appId must be the storeasy app id, a positive integer.')
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
    const wait = (input(env, 'wait') ?? 'false').toLowerCase() === 'true'
    const timeout = Number(input(env, 'timeoutMinutes') ?? '30')
    if (!Number.isFinite(timeout) || timeout <= 0) {
      throw new StoreasyError('Input timeoutMinutes must be a positive number.')
    }
    const serviceConnectionId = input(env, 'connectedService') ?? input(env, 'serviceConnection')
    const token = input(env, 'token')
    const accessToken = systemAccessToken(env)
    registerSecret(accessToken)
    if (serviceConnectionId && !env.SYSTEM_OIDCREQUESTURI) {
      throw new StoreasyError('This agent does not offer OIDC tokens (System.OidcRequestUri is empty). Update the agent or use a CI token.', {
        exitCode: ExitCode.Auth,
      })
    }
    if (serviceConnectionId && !accessToken) {
      throw new StoreasyError('No System.AccessToken for the OIDC request. Allow scripts to access the OAuth token for this job.', {
        exitCode: ExitCode.Auth,
      })
    }
    // With a service connection, OIDC is the identity: STOREASY_TOKEN from the environment must not win over it.
    const runEnv: Env = serviceConnectionId ? { ...env, STOREASY_TOKEN: undefined } : env
    const r = await runUpload({
      env: runEnv,
      baseUrl: resolveBaseUrl({}, input(env, 'url') ?? 'https://storeasy.milvasoft.com'),
      fetch: deps.fetch,
      sleep: deps.sleep,
      log,
      client: 'storeasy-azure-task',
      token: serviceConnectionId ? undefined : token,
      serviceConnectionId,
      azureAccessToken: accessToken,
      appId,
      filePath: file,
      delivery: { store, track: input(env, 'track'), group: input(env, 'group'), notes: input(env, 'notes') },
      wait,
      timeoutMs: timeout * 60_000,
      intervalMs: deps.waitIntervalMs,
      source: ArtifactSource.AzureDevOps,
      stateDir: env.AGENT_TEMPDIRECTORY ? `${env.AGENT_TEMPDIRECTORY}/storeasy-uploads` : undefined,
    })
    setVariable(log, 'StoreasySessionId', r.sessionId)
    setVariable(log, 'StoreasyDuplicate', r.duplicate)
    setVariable(log, 'StoreasyStatus', r.status)
    setVariable(log, 'StoreasyExitCode', r.exitCode)
    setVariable(log, 'StoreasySessionUrl', r.sessionUrl)
    if (r.exitCode !== ExitCode.Ok) {
      const msg = redact(r.summary ?? `exit code ${r.exitCode}`)
      log.command(`##vso[task.logissue type=error]${escapeMessage(msg)}`)
      log.command(`##vso[task.complete result=Failed;]${escapeMessage(msg)}`)
    } else {
      const done = r.duplicate ? 'Already received, not delivered again' : (r.summary ?? 'Uploaded')
      log.command(`##vso[task.complete result=Succeeded;]${escapeMessage(redact(done))}`)
    }
    return r.exitCode
  } catch (err) {
    const code = exitCodeOf(err)
    const msg = errorMessage(err)
    setVariable(log, 'StoreasyExitCode', code)
    log.command(`##vso[task.logissue type=error;code=${escapeProperty(String(code))}]${escapeMessage(msg)}`)
    log.command(`##vso[task.complete result=Failed;]${escapeMessage(msg)}`)
    return code
  }
}
