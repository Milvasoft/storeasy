// SPDX-License-Identifier: AGPL-3.0-only
import { readFileSync, statSync } from 'node:fs'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { main } from '../src/cli.js'
import { configPath } from '../src/config.js'
import { clearSecrets, redact, registerSecret } from '../src/redact.js'
import { DEFAULT_PAT, FakeStoreasy } from './fakeServer.js'
import { captureIo, noSleep, testEnv } from './helpers.js'

describe('redact()', () => {
  afterEach(() => clearSecrets())

  it('masks storeasy tokens, bearer headers, JWTs and signed URL queries', () => {
    const text = [
      'pat sea_9c1f4b7e2a6d0c83f15a9b2e7d4c61aa',
      'ci sci_7f2a4QmV9xT2kLr8NwZp3Hc6',
      'upload sut_Q4n8Lr2VxT9mKp6Zc',
      'secret whsec_eas_Q4n8Lr2VxT9mKp6ZcH1sYb',
      'Authorization: Bearer abc.def-ghi',
      'jwt eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJyZXBvOmEvYiJ9.c2ln',
      'url https://bucket.s3.amazonaws.com/a/b?X-Amz-Signature=abcd&X-Amz-Credential=x',
      '{"access_token":"ya29.a0AfH6SMB"}',
    ].join('\n')
    const out = redact(text)
    for (const leaked of ['9c1f4b7e', '7f2a4QmV', 'Q4n8Lr2V', 'abc.def-ghi', 'eyJzdWIi', 'X-Amz-Signature', 'a0AfH6SMB']) {
      expect(out).not.toContain(leaked)
    }
    expect(out).toContain('sea_[redacted]')
    expect(out).toContain('https://bucket.s3.amazonaws.com/a/b?[redacted-query]')
  })

  it('masks registered values of any shape', () => {
    registerSecret('plain-secret-value-42')
    expect(redact('x plain-secret-value-42 y')).toBe('x [redacted] y')
  })
})

describe('tokens never reach the output', () => {
  let fake: FakeStoreasy
  beforeEach(async () => {
    fake = await new FakeStoreasy().start()
  })
  afterEach(() => fake.stop())

  it('an invalid token echoed back by the server is not printed', async () => {
    const bad = 'sea_THISisAleakedTOKENvalue000000000001'
    const io = captureIo()
    const code = await main(['whoami'], { env: testEnv(fake.baseUrl, { STOREASY_TOKEN: bad }), io, sleep: noSleep })
    expect(code).toBe(3)
    expect(io.err()).toContain('Invalid token')
    expect(io.all()).not.toContain('THISisAleakedTOKENvalue')
  })

  it('a token without a known prefix is masked as a registered secret', async () => {
    const bad = 'opaque-token-without-prefix-123'
    const io = captureIo()
    expect(await main(['whoami', '--json'], { env: testEnv(fake.baseUrl, { STOREASY_TOKEN: bad }), io, sleep: noSleep })).toBe(3)
    expect(io.all()).not.toContain(bad)
  })

  it('login saves the token to a private file and never prints it', async () => {
    const env = testEnv(fake.baseUrl)
    const io = captureIo()
    const stdin = { isTTY: false, readAll: async () => `${DEFAULT_PAT}\n`, readHidden: async () => '' }
    const code = await main(['login', '--url', fake.baseUrl], { env, io, stdin, sleep: noSleep })
    expect(code).toBe(0)
    expect(io.all()).not.toContain(DEFAULT_PAT)
    expect(io.out()).toContain('deniz@kuzeylab.dev (Kuzey Lab), token sea_9c1...')
    const saved = JSON.parse(readFileSync(configPath(env), 'utf8')) as { url: string; token: string }
    expect(saved).toEqual({ url: fake.baseUrl, token: DEFAULT_PAT })
    if (process.platform !== 'win32') {
      expect(statSync(configPath(env)).mode & 0o777).toBe(0o600)
    }

    // The saved login is used by later commands.
    const who = captureIo()
    expect(await main(['whoami'], { env: { STOREASY_CONFIG_DIR: env.STOREASY_CONFIG_DIR }, io: who, sleep: noSleep })).toBe(0)
    expect(who.out()).toContain('deniz@kuzeylab.dev @ Kuzey Lab')
    expect(who.out()).toContain('saved login (sea_9c1...)')
    expect(who.all()).not.toContain(DEFAULT_PAT)

    const out = captureIo()
    expect(await main(['logout'], { env, io: out })).toBe(0)
    expect(await main(['whoami'], { env, io: captureIo(), sleep: noSleep })).toBe(3)
  })

  it('login refuses a token the server does not know, and saves nothing', async () => {
    const env = testEnv(fake.baseUrl)
    const io = captureIo()
    const stdin = { isTTY: true, readAll: async () => '', readHidden: async () => 'sea_unknownTOKEN0000000000000' }
    expect(await main(['login'], { env, io, stdin, sleep: noSleep })).toBe(3)
    expect(io.all()).not.toContain('unknownTOKEN')
    expect(() => readFileSync(configPath(env))).toThrow()
  })
})
