#!/usr/bin/env node
// SPDX-License-Identifier: AGPL-3.0-only
import { realpathSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parseArgs } from 'node:util'
import { type CurrentToken, getCurrentToken, getJob, listApps } from './api.js'
import { resolveAuth } from './auth.js'
import { type Env, configDir, readConfig, removeToken, resolveBaseUrl, writeConfig } from './config.js'
import { StoreJobStatus, StoreJobStepStatus, StoreKind, TokenKind, enumName, parseStore } from './enums.js'
import { ExitCode, StoreasyError, errorMessage, exitCodeOf } from './errors.js'
import { ApiClient, type FetchLike } from './http.js'
import { runUpload } from './intake.js'
import { type Io, Logger, processIo } from './log.js'
import { registerSecret, tokenHint } from './redact.js'
import { VERSION } from './version.js'

export interface Stdin {
  isTTY?: boolean
  /** Reads all of stdin. */
  readAll(): Promise<string>
  /** Prompts on the terminal without echoing the input. */
  readHidden(prompt: string): Promise<string>
}

export interface CliDeps {
  env?: Env
  io?: Io
  fetch?: FetchLike
  stdin?: Stdin
  sleep?: (ms: number) => Promise<void>
  /** Poll interval of --wait in ms (tests). */
  waitIntervalMs?: number
}

const USAGE = `storeasy ${VERSION}: upload builds and manage storeasy from the command line.

Usage:
  storeasy login [--url <url>] [--with-token]   Save a personal access token (sea_...) for this machine
  storeasy logout                               Forget the saved token
  storeasy whoami                               Show the token in use, its organization and scope
  storeasy apps list                            List the organization's apps and their store records
  storeasy jobs get <jobId>                     Show a store job and its steps
  storeasy upload <file> --app <id> [options]   Upload an IPA, AAB, APK or MSIX and hand it to the delivery rule

Upload options:
  --app <id>                 storeasy app id (required)
  --store <store>            apple | google | microsoft; checked against the package
  --track <name>             Play test track (internal, alpha, beta, a closed track); overrides the delivery rule
  --group <name>             TestFlight internal group; overrides the delivery rule
  --notes <text>             Release notes / What to Test
  --notes-file <path>        Release notes from a file
  --wait                     Wait for the delivery and stream its steps (exit code tells the result)
  --timeout <minutes>        Wait limit (default 30)
  --service-connection <id>  Azure Pipelines: service connection id for OIDC

Global options:
  --url <url>   storeasy base URL (default: STOREASY_URL, the saved config, https://storeasy.milvasoft.com)
  --json        Machine readable output on stdout
  --quiet       Only results and errors
  --help, --version

Auth, first match wins: STOREASY_TOKEN, OIDC in GitHub Actions / Azure Pipelines (upload only), the saved login.
Exit codes: 0 delivered or duplicate, 1 error, 2 package rejected, 3 identity, 4 store error, 5 wait timed out.`

const OPTIONS = {
  url: { type: 'string' },
  json: { type: 'boolean' },
  quiet: { type: 'boolean' },
  help: { type: 'boolean', short: 'h' },
  version: { type: 'boolean', short: 'v' },
  'with-token': { type: 'boolean' },
  app: { type: 'string' },
  store: { type: 'string' },
  track: { type: 'string' },
  group: { type: 'string' },
  notes: { type: 'string' },
  'notes-file': { type: 'string' },
  wait: { type: 'boolean' },
  timeout: { type: 'string' },
  'service-connection': { type: 'string' },
} as const

function positiveInt(value: string | undefined, name: string): number {
  const n = Number(value)
  if (!value || !Number.isInteger(n) || n <= 0) {
    throw new StoreasyError(`${name} must be a positive integer${value ? `, got "${value}"` : ''}.`)
  }
  return n
}

export async function main(argv: string[], deps: CliDeps = {}): Promise<number> {
  const env = deps.env ?? process.env
  const io = deps.io ?? processIo()
  let parsed: ReturnType<typeof parseArgs<{ options: typeof OPTIONS; allowPositionals: true; strict: true }>>
  try {
    parsed = parseArgs({ args: argv, options: OPTIONS, allowPositionals: true, strict: true })
  } catch (err) {
    io.stderr.write(`error: ${errorMessage(err)}\n\n${USAGE}\n`)
    return ExitCode.Error
  }
  const { values: o, positionals } = parsed
  const log = new Logger(io, { quiet: o.quiet })
  const [cmd, ...rest] = positionals
  if (o.version) {
    log.out(VERSION)
    return 0
  }
  if (!cmd || o.help || cmd === 'help') {
    io.stdout.write(`${USAGE}\n`)
    return 0
  }
  const printJson = (value: unknown) => log.out(JSON.stringify(value, null, 2))

  try {
    const baseUrl = resolveBaseUrl(env, o.url)
    const client = () => resolveAuth({ env, baseUrl, fetch: deps.fetch, sleep: deps.sleep, log, oidc: false })

    switch (cmd) {
      case 'login':
        return await login({ env, log, baseUrl, urlGiven: Boolean(o.url ?? env.STOREASY_URL), withToken: Boolean(o['with-token']), deps, json: o.json })

      case 'logout': {
        const removed = removeToken(env)
        log.info(removed ? 'Signed out; the saved token was removed. Revoke it in storeasy > Developer if it may have leaked.' : 'No saved token.')
        return 0
      }

      case 'whoami': {
        const auth = client()
        if (auth.kind === 'none') {
          throw new StoreasyError('Not signed in. Run "storeasy login" or set STOREASY_TOKEN.', { exitCode: ExitCode.Auth })
        }
        const t = await getCurrentToken(auth.api)
        if (o.json) {
          printJson({ ...t, url: baseUrl, source: auth.kind })
        } else {
          log.out(describeToken(t, baseUrl, auth.describe))
        }
        return 0
      }

      case 'apps': {
        if (rest[0] !== 'list') {
          throw new StoreasyError('Usage: storeasy apps list')
        }
        const apps = await listApps(client().api)
        if (o.json) {
          printJson(apps)
        } else if (!apps.length) {
          log.info('No apps yet. Connect a store in storeasy > Connections.')
        } else {
          for (const a of apps) {
            const stores = (a.stores ?? []).map((s) => `${enumName(StoreKind, s.store).toLowerCase()}:${s.bundleId ?? s.externalId ?? '?'}`)
            log.out(`${String(a.id).padStart(5)}  ${a.name}${stores.length ? `  (${stores.join(', ')})` : ''}`)
          }
        }
        return 0
      }

      case 'jobs': {
        if (rest[0] !== 'get') {
          throw new StoreasyError('Usage: storeasy jobs get <jobId>')
        }
        const job = await getJob(client().api, positiveInt(rest[1], 'jobId'))
        if (o.json) {
          printJson(job)
        } else {
          log.out(`job ${job.id}  ${enumName(StoreJobStatus, job.status)}${job.summary ? `  ${job.summary}` : ''}`)
          if (job.error) {
            log.out(`error: ${job.error}`)
          }
          for (const s of job.steps ?? []) {
            const prog = s.total ? ` ${s.progress ?? 0}/${s.total}` : ''
            log.out(`  ${String(s.order).padStart(2)}. ${s.name}  ${enumName(StoreJobStepStatus, s.status)}${prog}${s.message ? `  ${s.message}` : ''}`)
          }
        }
        return 0
      }

      case 'upload': {
        const file = rest[0]
        if (!file || rest.length > 1) {
          throw new StoreasyError('Usage: storeasy upload <file> --app <id> [--store] [--track | --group] [--notes] [--wait]')
        }
        const appId = positiveInt(o.app, '--app')
        const store = parseStore(o.store)
        if (o.store !== undefined && store === undefined) {
          throw new StoreasyError(`--store must be apple, google or microsoft, got "${o.store}".`)
        }
        if (o.notes !== undefined && o['notes-file'] !== undefined) {
          throw new StoreasyError('Use --notes or --notes-file, not both.')
        }
        const notes = o['notes-file'] !== undefined ? (await readFile(o['notes-file'], 'utf8')).trim() : o.notes
        const timeoutMin = o.timeout !== undefined ? positiveInt(o.timeout, '--timeout') : 30
        const r = await runUpload({
          env,
          baseUrl,
          fetch: deps.fetch,
          sleep: deps.sleep,
          log,
          appId,
          filePath: file,
          delivery: { store, track: o.track, group: o.group, notes },
          wait: Boolean(o.wait),
          timeoutMs: timeoutMin * 60_000,
          intervalMs: deps.waitIntervalMs,
          serviceConnectionId: o['service-connection'],
          stateDir: join(configDir(env), 'uploads'),
        })
        if (o.json) {
          printJson(r)
        } else {
          log.out(`session ${r.sessionId}${r.duplicate ? ` duplicate of ${r.originalSessionId}` : ''}${r.status ? `  ${r.status}` : ''}`)
          if (r.summary) {
            log.out(r.summary)
          }
        }
        if (r.exitCode !== 0) {
          log.error(`exit code ${r.exitCode}`)
        }
        return r.exitCode
      }

      default:
        throw new StoreasyError(`Unknown command: ${cmd}. Run "storeasy --help".`)
    }
  } catch (err) {
    log.error(errorMessage(err))
    return exitCodeOf(err)
  }
}

function describeToken(t: CurrentToken, url: string, source: string): string {
  const kind = enumName(TokenKind, t.kind)
  const lines = [
    `${t.userName ?? t.name ?? kind} @ ${t.organizationName ?? `organization ${t.organizationId ?? '?'}`}`,
    `  server   ${url}`,
    `  token    ${kind}${t.name ? ` "${t.name}"` : ''} ${t.prefix ? `${t.prefix}...` : ''} via ${source}`,
    `  access   ${t.readOnly ? 'read only' : 'read and write'}${t.scopes?.length ? ` (${t.scopes.join(', ')})` : ''}${
      t.allApps === false && t.appIds?.length ? `, apps ${t.appIds.join(', ')}` : ''
    }`,
  ]
  if (t.expiryDate) {
    lines.push(`  expires  ${t.expiryDate}`)
  }
  return lines.join('\n')
}

async function login(a: {
  env: Env
  log: Logger
  baseUrl: string
  urlGiven: boolean
  withToken: boolean
  deps: CliDeps
  json?: boolean
}): Promise<number> {
  const stdin = a.deps.stdin ?? processStdin()
  let token: string
  if (a.withToken || !stdin.isTTY) {
    token = (await stdin.readAll()).trim()
  } else {
    a.log.info(`Create a personal access token in storeasy > Developer > MCP and tokens (${a.baseUrl}/developer).`)
    token = (await stdin.readHidden('Token: ')).trim()
  }
  if (!token) {
    throw new StoreasyError('No token given.', { exitCode: ExitCode.Auth })
  }
  if (/\s/.test(token)) {
    throw new StoreasyError('The token contains whitespace; paste only the token.', { exitCode: ExitCode.Auth })
  }
  registerSecret(token)
  if (token.startsWith('sci_')) {
    a.log.warn('This is a CI token. In CI, pass it as the STOREASY_TOKEN secret instead of saving it.')
  } else if (!token.startsWith('sea_')) {
    a.log.warn('Personal access tokens start with "sea_"; trying anyway.')
  }
  const api = new ApiClient({ baseUrl: a.baseUrl, token, fetch: a.deps.fetch, sleep: a.deps.sleep })
  const t = await getCurrentToken(api)
  const existing = readConfig(a.env)
  const path = writeConfig({ url: a.urlGiven ? a.baseUrl : existing.url, token }, a.env)
  if (a.json) {
    a.log.out(JSON.stringify({ url: a.baseUrl, organizationName: t.organizationName, userName: t.userName, prefix: t.prefix, configPath: path }))
  } else {
    a.log.out(`Signed in to ${a.baseUrl} as ${t.userName ?? t.name ?? '?'} (${t.organizationName ?? '?'}), token ${t.prefix ? `${t.prefix}...` : tokenHint(token)}.`)
    a.log.info(`Saved to ${path} (readable only by you).`)
  }
  return 0
}

function processStdin(): Stdin {
  return {
    isTTY: Boolean(process.stdin.isTTY),
    async readAll() {
      const chunks: Buffer[] = []
      for await (const c of process.stdin) {
        chunks.push(c as Buffer)
      }
      return Buffer.concat(chunks).toString('utf8')
    },
    readHidden(prompt: string) {
      return new Promise((resolve, reject) => {
        const input = process.stdin
        process.stderr.write(prompt)
        let value = ''
        input.setRawMode(true)
        input.resume()
        input.setEncoding('utf8')
        const done = (err?: Error) => {
          input.setRawMode(false)
          input.pause()
          input.removeListener('data', onData)
          process.stderr.write('\n')
          if (err) {
            reject(err)
          } else {
            resolve(value)
          }
        }
        const onData = (s: string) => {
          for (const ch of s) {
            if (ch === '\r' || ch === '\n' || ch === '\u0004') {
              return done()
            }
            if (ch === '\u0003') {
              return done(new StoreasyError('Cancelled.'))
            }
            if (ch === '\u007f' || ch === '\b') {
              value = value.slice(0, -1)
            } else {
              value += ch
            }
          }
        }
        input.on('data', onData)
      })
    },
  }
}

function isEntry(): boolean {
  try {
    return realpathSync(process.argv[1] ?? '') === realpathSync(fileURLToPath(import.meta.url))
  } catch {
    return false
  }
}

if (isEntry()) {
  main(process.argv.slice(2)).then((code) => {
    process.exitCode = code
  })
}
