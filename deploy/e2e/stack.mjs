#!/usr/bin/env node
// The end-to-end stack: deploy/docker-compose.yml plus deploy/e2e/docker-compose.e2e.yml (Mailpit, open registration).
//
//   node deploy/e2e/stack.mjs up      generate a throwaway .env (outside the repository), build, start, initialize the
//                                     database once and write the root password next to the .env
//   node deploy/e2e/stack.mjs down    stop the stack and delete its volumes
//   node deploy/e2e/stack.mjs env     print the variables the Playwright suite reads
//
// The state lives in E2E_STATE_DIR (default <os temp>/storeasy-e2e): .env and root-password. Nothing is written into the
// repository. Ports: web 8080, S3 8333, Mailpit 8025, and 5080 for the short-lived initialization container.
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
const rootPasswordFile = join(stateDir, 'root-password')
const webUrl = 'http://localhost:8080'
const initPort = 5080

const compose = (...args) => [
  'compose',
  '-f',
  join(repo, 'deploy', 'docker-compose.yml'),
  '-f',
  join(here, 'docker-compose.e2e.yml'),
  '--env-file',
  envFile,
  ...args,
]

function docker(args, options = {}) {
  return execFileSync('docker', args, { cwd: repo, stdio: options.capture ? 'pipe' : 'inherit', encoding: 'utf8' })
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
  ]
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

// docs/self-host.md section 5: a temporary MILVA_ENV=dev instance of the same image serves the documentation page, whose
// token is accepted by database/init before the root user exists.
async function initialize() {
  if (existsSync(rootPasswordFile)) return
  docker(compose('stop', 'storeasy-api'))
  const name = 'storeasy-e2e-init'
  docker(['rm', '-f', name], { capture: true })
  docker(compose('run', '-d', '--name', name, '-e', 'MILVA_ENV=dev', '-p', `127.0.0.1:${initPort}:8080`, 'storeasy-api'), {
    capture: true,
  })
  try {
    const page = await (await waitFor(`http://127.0.0.1:${initPort}/api/documentation`)).text()
    const token = page.match(/eyJ[\w-]+\.[\w-]+\.[\w-]+/)?.[0]
    if (!token) throw new Error('No token on the documentation page')
    const response = await fetch(`http://127.0.0.1:${initPort}/api/v1.0/developer/database/init`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` },
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

const command = process.argv[2]
if (command === 'up') {
  writeEnv()
  docker(compose('up', '-d', '--build', '--wait'))
  await waitFor(`${webUrl}/api/v1.0/healthcheck/ready`)
  await initialize()
  await waitFor(`${webUrl}/api/v1.0/healthcheck/ready`)
  console.log(`storeasy e2e stack is up at ${webUrl} (state: ${stateDir})`)
} else if (command === 'down') {
  if (existsSync(envFile)) docker(compose('down', '-v', '--remove-orphans'))
  // The database is gone with its volume, so the next "up" initializes again.
  rmSync(rootPasswordFile, { force: true })
  console.log('storeasy e2e stack is down, volumes deleted')
} else if (command === 'env') {
  console.log(`E2E_BASE_URL=${webUrl}`)
  console.log('E2E_MAILPIT_URL=http://localhost:8025')
  console.log(`E2E_ROOT_PASSWORD_FILE=${rootPasswordFile}`)
  if (existsSync(rootPasswordFile)) console.log(`(root password file present: ${readFileSync(rootPasswordFile, 'utf8').length} chars)`)
} else {
  console.error('usage: node deploy/e2e/stack.mjs up|down|env')
  process.exit(1)
}
