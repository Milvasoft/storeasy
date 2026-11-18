// SPDX-License-Identifier: AGPL-3.0-only
import { createHash } from 'node:crypto'
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { main } from '../src/cli.js'
import { ApiClient } from '../src/http.js'
import { waitForSession } from '../src/wait.js'
import { DEFAULT_CI, FakeStoreasy } from './fakeServer.js'
import { captureIo, makeFile, noSleep, tempDir, testEnv } from './helpers.js'

const MiB = 1024 * 1024

describe('storeasy upload', () => {
  let fake: FakeStoreasy
  let dir: string
  let file: string

  beforeEach(async () => {
    fake = await new FakeStoreasy({ partSize: 5 * MiB }).start()
    dir = tempDir()
    file = makeFile(dir, 'halka-1.0.0-42.aab', 12 * MiB + 123)
  })
  afterEach(() => fake.stop())

  const run = (args: string[], env = testEnv(fake.baseUrl, { STOREASY_TOKEN: DEFAULT_CI })) => {
    const io = captureIo()
    return main(args, { env, io, sleep: noSleep, waitIntervalMs: 0 }).then((code) => ({ code, io, env }))
  }

  it('uploads in parts with the file SHA-256 and completes', async () => {
    const { code, io } = await run(['upload', file, '--app', '3', '--json'])
    expect(code).toBe(0)
    const open = fake.calls('POST', '/api/v1.0/apps/3/artifacts')
    expect(open).toHaveLength(1)
    const body = open[0]!.body as Record<string, unknown>
    expect(body.sha256).toBe(createHash('sha256').update(readFileSync(file)).digest('hex'))
    expect(body.size).toBe(12 * MiB + 123)
    expect(body.fileName).toBe('halka-1.0.0-42.aab')
    expect(body.source).toBe(4) // Cli
    expect(fake.calls('PUT', '/s3/').map((r) => r.bytes)).toEqual(expect.arrayContaining([5 * MiB, 5 * MiB, 2 * MiB + 123]))
    const complete = fake.calls('PUT', '/api/v1.0/artifacts/')
    expect(complete).toHaveLength(1)
    expect((complete[0]!.body as { parts: Array<{ partNumber: number }> }).parts.map((p) => p.partNumber)).toEqual([1, 2, 3])
    const result = JSON.parse(io.out()) as { sessionId: number; duplicate: boolean; exitCode: number }
    expect(result).toMatchObject({ duplicate: false, exitCode: 0 })
    expect(io.err()).toContain(`Session ${result.sessionId}`)
  })

  it('sends the delivery override (store, track, notes)', async () => {
    const { code } = await run(['upload', file, '--app', '3', '--store', 'google', '--track', 'internal', '--notes', 'Fixes login'])
    expect(code).toBe(0)
    const body = fake.calls('POST', '/api/v1.0/apps/3/artifacts')[0]!.body as Record<string, unknown>
    expect(body.delivery).toEqual({ store: 2, track: 'internal', notes: 'Fixes login' })
  })

  it('retries a failed part (500, then a dropped connection) and still completes', async () => {
    fake.partFaults.set(2, [500, 'reset'])
    const { code, io } = await run(['upload', file, '--app', '3'])
    expect(code).toBe(0)
    // Part 2: the 500, the dropped connection, then the success.
    expect(fake.calls('PUT', '/s3/').filter((r) => r.path.endsWith('/2'))).toHaveLength(3)
    expect(fake.calls('PUT', '/s3/')).toHaveLength(5)
    expect(fake.sessions.values().next().value?.parts.size).toBe(3)
    expect(io.err()).toContain('2 part retries')
  })

  it('resumes after a failed run: only the missing part is sent again', async () => {
    fake.brokenParts.add(3)
    const first = await run(['upload', file, '--app', '3'])
    expect(first.code).toBe(1)
    expect(first.io.err()).toContain('Run the same command again to resume')
    // The error names the storage URL without its signature.
    expect(first.io.err()).not.toContain('X-Amz-Signature')
    const stateDir = join(first.env.STOREASY_CONFIG_DIR!, 'uploads')
    expect(readdirSync(stateDir)).toHaveLength(1)
    expect(fake.calls('PUT', '/api/v1.0/artifacts/')).toHaveLength(0)

    fake.brokenParts.clear()
    const putsBefore = fake.calls('PUT', '/s3/').length
    const second = await run(['upload', file, '--app', '3'], first.env)
    expect(second.code).toBe(0)
    const newPuts = fake.calls('PUT', '/s3/').slice(putsBefore)
    expect(newPuts.map((r) => r.path.split('/').pop())).toEqual(['3'])
    expect(second.io.err()).toContain('Resuming upload session')
    expect(fake.calls('PUT', '/api/v1.0/artifacts/')).toHaveLength(1)
    expect(existsSync(stateDir) ? readdirSync(stateDir) : []).toHaveLength(0)
  })

  it('refreshes an expired part URL by opening the session again', async () => {
    fake.partFaults.set(1, [403])
    const { code } = await run(['upload', file, '--app', '3'])
    expect(code).toBe(0)
    expect(fake.calls('POST', '/api/v1.0/apps/3/artifacts')).toHaveLength(2)
  })

  it('reports a duplicate without uploading, and --wait follows the original session', async () => {
    expect((await run(['upload', file, '--app', '3'])).code).toBe(0)
    const putsBefore = fake.calls('PUT', '/s3/').length
    const { code, io } = await run(['upload', file, '--app', '3', '--wait', '--json'])
    expect(code).toBe(0)
    expect(fake.calls('PUT', '/s3/').length).toBe(putsBefore)
    const r = JSON.parse(io.out()) as { duplicate: boolean; originalSessionId: number; status: string }
    expect(r.duplicate).toBe(true)
    expect(r.status).toBe('Delivered')
    expect(io.err()).toContain('already received')
    expect(io.err()).toContain(`duplicate of session ${r.originalSessionId}`)
  })

  it('--wait streams the steps and exits 0 when delivered', async () => {
    const { code, io } = await run(['upload', file, '--app', '3', '--wait'])
    expect(code).toBe(0)
    expect(io.err()).toContain('deliver  running: edits.bundles.upload')
    expect(io.err()).toContain('deliver  done: internal track')
    expect(io.out()).toContain('Delivered Halka 1.0.0 (42) to Google Play internal')
  })

  it('--wait exits 2 when the package is rejected and 4 on a store error', async () => {
    await fake.stop()
    fake = await new FakeStoreasy({
      partSize: 5 * MiB,
      sessionScript: [{ status: 6, ciResult: { exitCode: 2, summary: 'Package name does not match: com.kuzeylab.defter.dev' } }],
    }).start()
    const rejected = await run(['upload', file, '--app', '3', '--wait'])
    expect(rejected.code).toBe(2)
    expect(rejected.io.out()).toContain('com.kuzeylab.defter.dev')

    await fake.stop()
    fake = await new FakeStoreasy({ partSize: 5 * MiB, sessionScript: [{ status: 9 }] }).start()
    const failed = await run(['upload', file, '--app', '3', '--wait'])
    expect(failed.code).toBe(4)
  })

  it('wait times out with exit code 5', async () => {
    await fake.stop()
    fake = await new FakeStoreasy({ partSize: 5 * MiB, sessionScript: [{ status: 4, statusDescription: 'Delivering' }] }).start()
    expect((await run(['upload', file, '--app', '3'])).code).toBe(0)
    const sessionId = [...fake.sessions.keys()][0]!
    const api = new ApiClient({ baseUrl: fake.baseUrl, token: DEFAULT_CI, sleep: noSleep })
    let t = 0
    await expect(waitForSession(api, sessionId, { intervalMs: 0, timeoutMs: 1000, now: () => (t += 400) })).rejects.toMatchObject({
      exitCode: 5,
    })
  })

  it('refuses a missing file and a bad --app', async () => {
    expect((await run(['upload', join(dir, 'nope.aab'), '--app', '3'])).code).toBe(1)
    const bad = await run(['upload', file, '--app', 'abc'])
    expect(bad.code).toBe(1)
    expect(bad.io.err()).toContain('--app must be a positive integer')
  })
})
