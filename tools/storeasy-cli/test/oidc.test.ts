// SPDX-License-Identifier: AGPL-3.0-only
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { main } from '../src/cli.js'
import { DEFAULT_CI, FakeStoreasy } from './fakeServer.js'
import { captureIo, makeFile, noSleep, tempDir, testEnv } from './helpers.js'

const OIDC = 'eyJhbGciOiJSUzI1NiJ9.eyJhdWQiOiJzdG9yZWFzeSIsInJlcG8iOiJrdXpleWxhYi9oYWxrYSJ9.c2lnbmF0dXJlLXZhbHVl'
const UPLOAD = 'sut_Q4n8Lr2VxT9mKp6ZcH1sYb0aa11'

/** Output with the runner's mask commands removed: those must carry the value, nothing else may. */
const withoutMaskCommands = (s: string) =>
  s
    .split('\n')
    .filter((l) => !l.startsWith('::add-mask::') && !l.startsWith('##vso[task.setsecret]'))
    .join('\n')

describe('OIDC trusted publisher', () => {
  let fake: FakeStoreasy
  let file: string

  beforeEach(async () => {
    fake = await new FakeStoreasy({ oidcRules: [{ appId: 3, token: OIDC }], oidcToken: OIDC, uploadToken: UPLOAD }).start()
    file = makeFile(tempDir(), 'halka.aab', 300_000)
  })
  afterEach(() => fake.stop())

  const githubEnv = () =>
    testEnv(fake.baseUrl, {
      GITHUB_ACTIONS: 'true',
      ACTIONS_ID_TOKEN_REQUEST_URL: `${fake.baseUrl}/github-oidc?api-version=2.0`,
      ACTIONS_ID_TOKEN_REQUEST_TOKEN: 'gh-request-token-123456',
      GITHUB_REPOSITORY: 'kuzeylab/halka-mobile',
      GITHUB_RUN_ID: '318',
      GITHUB_REF_NAME: 'main',
      GITHUB_SHA: 'a41f9c2',
      GITHUB_ACTOR: 'deniz',
      GITHUB_SERVER_URL: 'https://github.com',
    })

  it('GitHub Actions: requests a token with audience storeasy, exchanges it and uploads with the upload token', async () => {
    const io = captureIo()
    const code = await main(['upload', file, '--app', '3', '--wait'], { env: githubEnv(), io, sleep: noSleep, waitIntervalMs: 0 })
    expect(code).toBe(0)
    const gh = fake.calls('GET', '/github-oidc')
    expect(gh).toHaveLength(1)
    expect(gh[0]!.query.get('audience')).toBe('storeasy')
    expect(gh[0]!.query.get('api-version')).toBe('2.0')
    const ex = fake.calls('POST', '/api/v1.0/oidc/exchange')
    expect(ex).toHaveLength(1)
    expect(ex[0]!.body).toEqual({ appId: 3, token: OIDC, provider: 1 })
    expect(ex[0]!.headers.authorization).toBeUndefined()
    const open = fake.calls('POST', '/api/v1.0/apps/3/artifacts')[0]!
    expect(open.headers.authorization).toBe(`Bearer ${UPLOAD}`)
    expect(open.body).toMatchObject({
      source: 1,
      sourceRef: { provider: 'github', runId: '318', repository: 'kuzeylab/halka-mobile', branch: 'main', commit: 'a41f9c2', actor: 'deniz' },
    })
    expect(fake.calls('GET', '/api/v1.0/artifacts/session')[0]!.headers.authorization).toBe(`Bearer ${UPLOAD}`)
    // The runner masks both tokens; otherwise they never reach the log.
    expect(io.out()).toContain(`::add-mask::${OIDC}`)
    expect(io.out()).toContain(`::add-mask::${UPLOAD}`)
    const rest = withoutMaskCommands(io.all())
    expect(rest).not.toContain(OIDC)
    expect(rest).not.toContain(UPLOAD)
    expect(rest).not.toContain('gh-request-token-123456')
  })

  it('Azure Pipelines: uses the service connection with System.AccessToken', async () => {
    const env = testEnv(fake.baseUrl, {
      TF_BUILD: 'True',
      SYSTEM_OIDCREQUESTURI: `${fake.baseUrl}/azure-oidc`,
      SYSTEM_ACCESSTOKEN: 'azure-system-access-token-1',
      BUILD_BUILDID: '881',
      BUILD_BUILDNUMBER: '20261118.3',
      SYSTEM_COLLECTIONURI: 'https://dev.azure.com/kuzeylab/',
      SYSTEM_TEAMPROJECT: 'Pusula',
    })
    const io = captureIo()
    const code = await main(['upload', file, '--app', '3', '--service-connection', 'sc-1234'], { env, io, sleep: noSleep })
    expect(code).toBe(0)
    const az = fake.calls('POST', '/azure-oidc')[0]!
    expect(az.query.get('api-version')).toBe('7.1')
    expect(fake.calls('POST', '/api/v1.0/oidc/exchange')[0]!.body).toMatchObject({ provider: 2 })
    expect(fake.calls('POST', '/api/v1.0/apps/3/artifacts')[0]!.body).toMatchObject({
      source: 2,
      sourceRef: { runId: '20261118.3', runUrl: 'https://dev.azure.com/kuzeylab/Pusula/_build/results?buildId=881' },
    })
    expect(io.out()).toContain(`##vso[task.setsecret]${UPLOAD}`)
    const rest = withoutMaskCommands(io.all())
    expect(rest).not.toContain('azure-system-access-token-1')
    expect(rest).not.toContain(UPLOAD)
  })

  it('a claim mismatch exits 3 without opening a session and without naming the claim', async () => {
    await fake.stop()
    fake = await new FakeStoreasy({ oidcRules: [], oidcToken: OIDC }).start()
    const io = captureIo()
    const code = await main(['upload', file, '--app', '3'], { env: githubEnv(), io, sleep: noSleep })
    expect(code).toBe(3)
    expect(io.err()).toContain("did not match this app's trusted publisher rules (403)")
    expect(fake.calls('POST', '/api/v1.0/apps/')).toHaveLength(0)
  })

  it('a missing id-token permission exits 3 with a hint', async () => {
    const env = { ...githubEnv(), ACTIONS_ID_TOKEN_REQUEST_TOKEN: 'wrong-request-token' }
    const io = captureIo()
    expect(await main(['upload', file, '--app', '3'], { env, io, sleep: noSleep })).toBe(3)
    expect(io.err()).toContain('id-token: write')
  })

  it('STOREASY_TOKEN wins over OIDC', async () => {
    const env = { ...githubEnv(), STOREASY_TOKEN: DEFAULT_CI }
    const code = await main(['upload', file, '--app', '3'], { env, io: captureIo(), sleep: noSleep })
    expect(code).toBe(0)
    expect(fake.calls('GET', '/github-oidc')).toHaveLength(0)
    expect(fake.calls('POST', '/api/v1.0/apps/3/artifacts')[0]!.headers.authorization).toBe(`Bearer ${DEFAULT_CI}`)
  })

  it('STOREASY_OIDC_TOKEN (GitLab id_tokens) is exchanged as is', async () => {
    const env = testEnv(fake.baseUrl, { GITLAB_CI: 'true', STOREASY_OIDC_TOKEN: OIDC, CI_PROJECT_PATH: 'kuzeylab/mola' })
    const code = await main(['upload', file, '--app', '3'], { env, io: captureIo(), sleep: noSleep })
    expect(code).toBe(0)
    expect(fake.calls('POST', '/api/v1.0/oidc/exchange')[0]!.body).toMatchObject({ provider: 3 })
    expect(fake.calls('POST', '/api/v1.0/apps/3/artifacts')[0]!.body).toMatchObject({ source: 3, sourceRef: { repository: 'kuzeylab/mola' } })
  })

  it('renews the upload token when it is about to expire', async () => {
    await fake.stop()
    fake = await new FakeStoreasy({ oidcRules: [{ appId: 3, token: OIDC }], oidcToken: OIDC, uploadToken: UPLOAD, uploadTokenTtlMs: 30_000 }).start()
    const code = await main(['upload', file, '--app', '3', '--wait'], { env: githubEnv(), io: captureIo(), sleep: noSleep, waitIntervalMs: 0 })
    expect(code).toBe(0)
    // 30 s left is under the one minute margin: every call asks for a fresh token.
    expect(fake.issuedUploadTokens).toBeGreaterThan(1)
  })

  it('without any credentials exits 3', async () => {
    const io = captureIo()
    expect(await main(['upload', file, '--app', '3'], { env: testEnv(fake.baseUrl), io, sleep: noSleep })).toBe(3)
    expect(io.err()).toContain('No credentials')
  })
})
