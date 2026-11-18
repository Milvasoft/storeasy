// SPDX-License-Identifier: AGPL-3.0-only
import { join } from 'node:path'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { main } from '../src/cli.js'
import { configDir, resolveBaseUrl } from '../src/config.js'
import { DEFAULT_CI, DEFAULT_PAT, FakeStoreasy } from './fakeServer.js'
import { captureIo, noSleep, testEnv } from './helpers.js'

describe('storeasy commands', () => {
  let fake: FakeStoreasy
  beforeEach(async () => {
    fake = await new FakeStoreasy().start()
  })
  afterEach(() => fake.stop())

  const run = async (args: string[], token = DEFAULT_PAT) => {
    const io = captureIo()
    const code = await main(args, { env: testEnv(fake.baseUrl, { STOREASY_TOKEN: token }), io, sleep: noSleep })
    return { code, io }
  }

  it('whoami describes a PAT and a CI token', async () => {
    const pat = await run(['whoami'])
    expect(pat.code).toBe(0)
    expect(pat.io.out()).toContain('deniz@kuzeylab.dev @ Kuzey Lab')
    expect(pat.io.out()).toContain('PersonalAccessToken "Claude Code laptop" sea_9c1...')
    const ci = await run(['whoami', '--json'], DEFAULT_CI)
    expect(JSON.parse(ci.io.out())).toMatchObject({ kind: 2, appIds: [3], allApps: false, source: 'env' })
    expect(fake.calls('GET', '/api/v1.0/tokens/current')).toHaveLength(2)
  })

  it('apps list reads every page', async () => {
    const { code, io } = await run(['apps', 'list'])
    expect(code).toBe(0)
    expect(io.out()).toContain('Pusula  (apple:com.kuzeylab.pusula)')
    expect(io.out()).toContain('Halka  (google:com.kuzeylab.halka)')
    expect(fake.calls('PATCH', '/api/v1.0/apps').map((r) => (r.body as { pageNumber: number }).pageNumber)).toEqual([1, 2])
  })

  it('jobs get prints the job and its steps; an unknown job exits 1', async () => {
    const { code, io } = await run(['jobs', 'get', '9182'])
    expect(code).toBe(0)
    expect(io.out()).toContain('job 9182  Succeeded  Delivered to internal')
    expect(io.out()).toContain('2. bundle.upload  Succeeded 10/10')
    expect(fake.calls('GET', '/api/v1.0/storeJobs/job')[0]!.query.get('JobId')).toBe('9182')
    const missing = await run(['jobs', 'get', '1'])
    expect(missing.code).toBe(1)
    expect(missing.io.err()).toContain('Job not found. (GET /api/v1.0/storeJobs/job, 404)')
  })

  it('a 403 maps to exit code 3', async () => {
    fake.apiFaults.set('PATCH /api/v1.0/apps', [403, 'OrganizationForbidden', 'You do not have permission.'])
    const { code, io } = await run(['apps', 'list'])
    expect(code).toBe(3)
    expect(io.err()).toContain('You do not have permission.')
  })

  it('retries a 503 on reads', async () => {
    let n = 0
    const fetchImpl = async (url: string, init?: RequestInit) => (n++ === 0 ? new Response('', { status: 503 }) : fetch(url, init))
    const io = captureIo()
    const code = await main(['whoami'], { env: testEnv(fake.baseUrl, { STOREASY_TOKEN: DEFAULT_PAT }), io, sleep: noSleep, fetch: fetchImpl })
    expect(code).toBe(0)
    expect(n).toBe(2)
  })

  it('help, version and usage errors', async () => {
    expect((await run(['--help'])).io.out()).toContain('storeasy upload <file> --app <id>')
    expect((await run(['--version'])).io.out().trim()).toMatch(/^\d+\.\d+\.\d+$/)
    expect((await run(['frobnicate'])).code).toBe(1)
    expect((await run(['upload', 'x.aab', '--bogus'])).code).toBe(1)
    expect((await run(['upload', 'x.aab', '--app', '3', '--store', 'nokia'])).io.err()).toContain('--store must be apple, google or microsoft')
  })
})

describe('config', () => {
  it('uses the OS config directory', () => {
    expect(configDir({ APPDATA: 'C:\\Users\\x\\AppData\\Roaming' }, 'win32')).toBe(join('C:\\Users\\x\\AppData\\Roaming', 'storeasy'))
    expect(configDir({ HOME: '/home/x' }, 'linux')).toBe(join('/home/x', '.config', 'storeasy'))
    expect(configDir({ HOME: '/home/x', XDG_CONFIG_HOME: '/cfg' }, 'linux')).toBe(join('/cfg', 'storeasy'))
    expect(configDir({ HOME: '/Users/x' }, 'darwin')).toBe(join('/Users/x', 'Library', 'Application Support', 'storeasy'))
  })

  it('base URL precedence and https rule', () => {
    const env = { STOREASY_CONFIG_DIR: join(process.cwd(), 'no-such-dir') }
    expect(resolveBaseUrl(env)).toBe('https://storeasy.milvasoft.com')
    expect(resolveBaseUrl({ ...env, STOREASY_URL: 'https://example.test/' })).toBe('https://example.test')
    expect(resolveBaseUrl({ ...env, STOREASY_URL: 'https://example.test' }, 'http://localhost:5000')).toBe('http://localhost:5000')
    expect(() => resolveBaseUrl({ ...env, STOREASY_URL: 'http://example.test' })).toThrow(/https/)
  })
})
