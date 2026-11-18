// SPDX-License-Identifier: AGPL-3.0-only
import { readFileSync, writeFileSync } from 'node:fs'
import { spawn } from 'node:child_process'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { DEFAULT_CI, FakeStoreasy } from '../../storeasy-cli/test/fakeServer.js'
import { captureIo, makeFile, noSleep, tempDir } from '../../storeasy-cli/test/helpers.js'
import { run } from '../src/main.js'

const OIDC = 'eyJhbGciOiJSUzI1NiJ9.eyJhdWQiOiJzdG9yZWFzeSIsInJlcG8iOiJrdXpleWxhYi9oYWxrYSJ9.c2lnbmF0dXJlLXZhbHVl'
const UPLOAD = 'sut_Q4n8Lr2VxT9mKp6ZcH1sYb0aa11'

/** Parses the GITHUB_OUTPUT file (name<<delimiter blocks). */
function outputs(path: string): Record<string, string> {
  const out: Record<string, string> = {}
  const lines = readFileSync(path, 'utf8').split('\n')
  for (let i = 0; i < lines.length; i++) {
    const m = /^([^<]+)<<(.+)$/.exec(lines[i]!)
    if (m) {
      const value: string[] = []
      while (lines[++i] !== m[2]) {
        value.push(lines[i]!)
      }
      out[m[1]!] = value.join('\n')
    }
  }
  return out
}

describe('upload-action', () => {
  let fake: FakeStoreasy
  let dir: string
  beforeEach(async () => {
    fake = await new FakeStoreasy({ oidcRules: [{ appId: 3, token: OIDC }], oidcToken: OIDC, uploadToken: UPLOAD }).start()
    dir = tempDir()
    makeFile(dir, 'app-release.aab', 400_000)
  })
  afterEach(() => fake.stop())

  const actionEnv = (inputs: Record<string, string>, extra: Record<string, string> = {}) => {
    const out = join(dir, 'output.txt')
    const summary = join(dir, 'summary.md')
    writeFileSync(out, '')
    writeFileSync(summary, '')
    const env: Record<string, string> = {
      GITHUB_ACTIONS: 'true',
      GITHUB_WORKSPACE: dir,
      GITHUB_OUTPUT: out,
      GITHUB_STEP_SUMMARY: summary,
      GITHUB_REPOSITORY: 'kuzeylab/halka-mobile',
      GITHUB_RUN_ID: '318',
      RUNNER_TEMP: dir,
      INPUT_URL: fake.baseUrl,
      INPUT_WAIT: 'false',
      'INPUT_TIMEOUT-MINUTES': '30',
      ...Object.fromEntries(Object.entries(inputs).map(([k, v]) => [`INPUT_${k.toUpperCase()}`, v])),
      ...extra,
    }
    return { env, out, summary }
  }

  it('uploads with OIDC (id-token: write), waits and writes the outputs', async () => {
    const { env, out, summary } = actionEnv(
      { 'app-id': '3', file: 'app-release.aab', wait: 'true', track: 'internal' },
      { ACTIONS_ID_TOKEN_REQUEST_URL: `${fake.baseUrl}/github-oidc`, ACTIONS_ID_TOKEN_REQUEST_TOKEN: 'gh-request-token-123456' },
    )
    const io = captureIo()
    const code = await run({ env, io, sleep: noSleep, waitIntervalMs: 0 })
    expect(code).toBe(0)
    const o = outputs(out)
    expect(o).toMatchObject({ duplicate: 'false', status: 'Delivered', 'exit-code': '0', 'store-job-id': '9182' })
    expect(o['session-url']).toBe(`${fake.baseUrl}/intake/sessions/${o['session-id']}`)
    expect(readFileSync(summary, 'utf8')).toContain('Delivered Halka 1.0.0 (42) to Google Play internal')
    const open = fake.calls('POST', '/api/v1.0/apps/3/artifacts')[0]!
    expect(open.headers.authorization).toBe(`Bearer ${UPLOAD}`)
    expect(open.headers['user-agent']).toMatch(/^storeasy-upload-action\//)
    expect(open.body).toMatchObject({ source: 1, delivery: { track: 'internal' }, sourceRef: { repository: 'kuzeylab/halka-mobile' } })
    const printed = io
      .all()
      .split('\n')
      .filter((l) => !l.startsWith('::add-mask::'))
      .join('\n')
    expect(printed).not.toContain(UPLOAD)
    expect(printed).not.toContain(OIDC)
    expect(io.out()).toContain(`::add-mask::${UPLOAD}`)
  })

  it('uses the token input when given and masks it', async () => {
    const { env, out } = actionEnv({ app: '3', file: 'app-release.aab', token: DEFAULT_CI })
    const io = captureIo()
    expect(await run({ env, io, sleep: noSleep })).toBe(0)
    expect(fake.calls('GET', '/github-oidc')).toHaveLength(0)
    expect(fake.calls('POST', '/api/v1.0/apps/3/artifacts')[0]!.headers.authorization).toBe(`Bearer ${DEFAULT_CI}`)
    expect(io.out()).toContain(`::add-mask::${DEFAULT_CI}`)
    expect(outputs(out)['exit-code']).toBe('0')
  })

  it('fails the step with an error annotation and exit code 3 without credentials', async () => {
    const { env, out } = actionEnv({ app: '3', file: 'app-release.aab' })
    const io = captureIo()
    expect(await run({ env, io, sleep: noSleep })).toBe(3)
    expect(io.out()).toMatch(/^::error title=storeasy::No credentials/m)
    expect(outputs(out)['exit-code']).toBe('3')
  })

  it('the committed bundle dist/index.cjs runs as the runner starts it', async () => {
    const { env, out } = actionEnv({ app: '3', file: 'app-release.aab', token: DEFAULT_CI })
    const bundle = fileURLToPath(new URL('../dist/index.cjs', import.meta.url))
    const child = spawn(process.execPath, [bundle], { env: { ...env, PATH: process.env.PATH ?? '' }, cwd: dir })
    let stdout = ''
    child.stdout.on('data', (d) => (stdout += d))
    const code = await new Promise<number | null>((r) => child.on('close', r))
    expect(code).toBe(0)
    expect(outputs(out)['exit-code']).toBe('0')
    expect(stdout).toContain(`::add-mask::${DEFAULT_CI}`)
  })

  it('validates inputs', async () => {
    const io = captureIo()
    expect(await run({ env: actionEnv({ file: 'x.aab' }).env, io })).toBe(1)
    expect(io.out()).toContain('Input app (or app-id)')
    const io2 = captureIo()
    expect(await run({ env: actionEnv({ app: '3', file: 'x.aab', wait: 'maybe' }).env, io: io2 })).toBe(1)
    expect(io2.out()).toContain('Input wait must be true or false')
  })
})
