#!/usr/bin/env node
// The end-to-end stack: deploy/docker-compose.yml plus deploy/e2e/docker-compose.e2e.yml (Mailpit, store hosts blocked).
//
//   node deploy/e2e/stack.mjs up [--mode multi|single|upgrade] [--fresh]
//        generate a throwaway .env (outside the repository), build the images for this machine, start the stack:
//        multi    (default) Storeasy:Mode=MultiTenant, open registration; the database is initialized once like
//                 docs/self-host.md section 5.2 and the root password is written next to the .env
//        single   Storeasy:Mode=SingleTeam, the default of a real install: the API prepares the database itself and logs
//                 a one time setup code (read it with "setup-code"); nothing else is done, the suite runs the setup
//        upgrade  an existing multi tenant install (initialized, only the root user, no organization) restarted as a
//                 single team instance
//        A mode other than the one the running stack was started with, or --fresh, starts from a clean database (the
//        volumes are deleted first).
//   node deploy/e2e/stack.mjs down        stop the stack and delete its volumes
//   node deploy/e2e/stack.mjs env         print the variables the Playwright suite reads
//   node deploy/e2e/stack.mjs setup-code  print the setup code from the API log (docker compose logs), single mode
//
// The state lives in E2E_STATE_DIR (default <os temp>/storeasy-e2e): .env, mode and root-password. Nothing is written
// into the repository. Ports: web 8080, S3 8333, Mailpit 8025, and 5080 for the short-lived initialization container.
// The compose project is "storeasy-e2e", apart from a real install ("storeasy") on the same machine.
import { execFileSync } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const repo = resolve(here, '..', '..')
const stateDir = process.env.E2E_STATE_DIR ?? join(tmpdir(), 'storeasy-e2e')
const envFile = join(stateDir, '.env')
const modeFile = join(stateDir, 'mode')
const rootPasswordFile = join(stateDir, 'root-password')
const webUrl = 'http://localhost:8080'
const initPort = 5080
const project = 'storeasy-e2e'
const MODES = { multi: 'MultiTenant', single: 'SingleTeam', upgrade: 'SingleTeam' }

const compose = (...args) => [
  'compose',
  '-p',
  project,
  '-f',
  join(repo, 'deploy', 'docker-compose.yml'),
  '-f',
  join(here, 'docker-compose.e2e.yml'),
  '--env-file',
  envFile,
  ...args,
]

function docker(args, options = {}) {
  return execFileSync('docker', args, {
    cwd: repo,
    stdio: options.capture ? 'pipe' : 'inherit',
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
    env: options.env ? { ...process.env, ...options.env } : process.env,
  })
}

function git(...args) {
  try {
    return execFileSync('git', args, { cwd: repo, encoding: 'utf8' }).trim()
  } catch {
    return ''
  }
}

function writeEnv() {
  if (existsSync(envFile)) return
  mkdirSync(stateDir, { recursive: true })
  const hex = (bytes) => randomBytes(bytes).toString('hex')
  const lines = [
    `STOREASY_BASE_URL=${webUrl}`,
    `STOREASY_MASTER_KEY=${randomBytes(32).toString('base64')}`,
    'STOREASY_MASTER_KEY_VERSION=1',
    `STOREASY_TOKEN_KEY=${hex(32)}`,
    `POSTGRES_PASSWORD=${hex(32)}`,
    `S3_ACCESS_KEY=${hex(16)}`,
    `S3_SECRET_KEY=${hex(32)}`,
    // s3.localhost: the loopback address in the browser, the SeaweedFS container inside the network (override file).
    'S3_PUBLIC_URL=http://s3.localhost:8333',
    'STOREASY_REGISTRATION_MODE=Open',
    'STOREASY_SCHEDULES_ENABLED=true',
    'SMTP_HOST=mailpit',
    'SMTP_PORT=1025',
    'SMTP_FROM=storeasy@e2e.test',
    'SMTP_ENABLE_SSL=false',
    `STOREASY_COMMIT=${git('rev-parse', '--short', 'HEAD')}`,
    'STOREASY_VERSION=e2e',
    'STOREASY_MODE=MultiTenant',
  ]
  writeFileSync(envFile, lines.join('\n') + '\n', { mode: 0o600 })
}

// Storeasy:Mode of the API container (docker-compose.e2e.yml reads STOREASY_MODE from the .env).
function setApiMode(apiMode) {
  const lines = readFileSync(envFile, 'utf8')
    .split('\n')
    .filter((line) => line && !line.startsWith('STOREASY_MODE='))
  lines.push(`STOREASY_MODE=${apiMode}`)
  writeFileSync(envFile, lines.join('\n') + '\n', { mode: 0o600 })
}

async function waitFor(url, accept = (r) => r.ok, attempts = 120) {
  for (let i = 0; i < attempts; i++) {
    try {
      const response = await fetch(url)
      if (accept(response)) return response
    } catch {
      // not up yet
    }
    await new Promise((r) => setTimeout(r, 1000))
  }
  throw new Error(`${url} did not answer in time`)
}

// docs/self-host.md section 5.2: a temporary MILVA_ENV=dev instance of the same image, bound to the loopback address,
// with a bootstrap init key generated here at runtime. The key reaches the container through the docker process
// environment (`-e NAME` without a value), so it never appears on a command line or in a file, and it dies with the
// container.
async function initialize() {
  if (existsSync(rootPasswordFile)) return
  docker(compose('stop', 'storeasy-api'))
  const name = 'storeasy-e2e-init'
  const initKey = randomBytes(32).toString('hex')
  docker(['rm', '-f', name], { capture: true })
  docker(
    compose(
      'run',
      '-d',
      '--name',
      name,
      '-e',
      'MILVA_ENV=dev',
      '-e',
      'Storeasy__Bootstrap__InitKey',
      '-p',
      `127.0.0.1:${initPort}:8080`,
      'storeasy-api',
    ),
    { capture: true, env: { Storeasy__Bootstrap__InitKey: initKey } },
  )
  try {
    await waitFor(`http://127.0.0.1:${initPort}/api/v1.0/healthcheck/ready`)
    const response = await fetch(`http://127.0.0.1:${initPort}/api/v1.0/developer/database/init`, {
      method: 'POST',
      headers: { 'X-Storeasy-Init-Key': initKey },
    })
    const body = await response.json()
    const password = body.data ?? body.Data
    if (!response.ok || !password) throw new Error(`database/init failed: ${response.status} ${JSON.stringify(body)}`)
    writeFileSync(rootPasswordFile, password, { mode: 0o600 })
  } finally {
    docker(['rm', '-f', name], { capture: true })
  }
  docker(compose('up', '-d', '--wait', 'storeasy-api', 'storeasy-web'))
}

function down() {
  if (existsSync(envFile)) docker(compose('down', '-v', '--remove-orphans'))
  // The database is gone with its volume, so the next "up" initializes again.
  rmSync(rootPasswordFile, { force: true })
  rmSync(modeFile, { force: true })
}

/** The newest setup code the API wrote to its log ("Storeasy setup code: XXXX-XXXX-XXXX"), or null. */
function setupCode() {
  const logs = docker(compose('logs', '--no-color', '--no-log-prefix', 'storeasy-api'), { capture: true })
  const codes = [...logs.matchAll(/Storeasy setup code: ([A-Z0-9-]+)/g)].map((match) => match[1])
  return codes.at(-1) ?? null
}

function option(name) {
  const index = process.argv.indexOf(`--${name}`)
  return index > 0 ? process.argv[index + 1] : undefined
}

const command = process.argv[2]
if (command === 'up') {
  const mode = option('mode') ?? 'multi'
  if (!(mode in MODES)) {
    console.error(`unknown mode ${mode}: multi, single or upgrade`)
    process.exit(1)
  }
  const previous = existsSync(modeFile) ? readFileSync(modeFile, 'utf8').trim() : null
  if (process.argv.includes('--fresh') || (previous && previous !== mode)) {
    down()
  }
  writeEnv()
  // The upgrade starts as the multi tenant install it upgrades.
  setApiMode(mode === 'upgrade' ? MODES.multi : MODES[mode])
  writeFileSync(modeFile, mode)
  docker(compose('up', '-d', '--build', '--wait'))
  await waitFor(`${webUrl}/api/v1.0/healthcheck/ready`)
  if (mode !== 'single') {
    await initialize()
    await waitFor(`${webUrl}/api/v1.0/healthcheck/ready`)
  }
  if (mode === 'upgrade') {
    // The same database, now started in single team mode (docs/self-host.md: switching an existing install).
    setApiMode(MODES.upgrade)
    docker(compose('up', '-d', '--wait', 'storeasy-api', 'storeasy-web'))
    await waitFor(`${webUrl}/api/v1.0/healthcheck/ready`)
  }
  if (mode === 'single') {
    let code = null
    for (let i = 0; i < 60 && !(code = setupCode()); i++) await new Promise((r) => setTimeout(r, 1000))
    if (!code) throw new Error('the API did not log a setup code')
  }
  console.log(`storeasy e2e stack is up at ${webUrl} in ${mode} mode (state: ${stateDir})`)
} else if (command === 'down') {
  down()
  console.log('storeasy e2e stack is down, volumes deleted')
} else if (command === 'env') {
  const mode = existsSync(modeFile) ? readFileSync(modeFile, 'utf8').trim() : 'multi'
  console.log(`E2E_MODE=${mode}`)
  console.log(`E2E_BASE_URL=${webUrl}`)
  console.log('E2E_MAILPIT_URL=http://localhost:8025')
  console.log(`E2E_ROOT_PASSWORD_FILE=${rootPasswordFile}`)
  if (existsSync(rootPasswordFile)) console.log(`(root password file present: ${readFileSync(rootPasswordFile, 'utf8').length} chars)`)
} else if (command === 'setup-code') {
  const code = setupCode()
  if (!code) {
    console.error('no setup code in the API log (not single team mode, or the setup is already done)')
    process.exit(1)
  }
  process.stdout.write(code)
} else {
  console.error('usage: node deploy/e2e/stack.mjs up [--mode multi|single|upgrade] [--fresh] | down | env | setup-code')
  process.exit(1)
}
