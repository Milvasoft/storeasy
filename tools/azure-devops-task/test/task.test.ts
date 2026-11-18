// SPDX-License-Identifier: AGPL-3.0-only
import { spawn } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { DEFAULT_CI, FakeStoreasy } from '../../storeasy-cli/test/fakeServer.js'
import { captureIo, makeFile, noSleep, tempDir } from '../../storeasy-cli/test/helpers.js'
import { run, systemAccessToken } from '../src/main.js'

const OIDC = 'eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJzYzovL2t1emV5bGFiL1B1c3VsYS9zdG9yZWFzeS1wdXN1bGEifQ.c2lnbmF0dXJlLXZhbHVl'
const UPLOAD = 'sut_Q4n8Lr2VxT9mKp6ZcH1sYb0aa11'
const ACCESS = 'azure-system-access-token-1'

const variables = (out: string) =>
  Object.fromEntries([...out.matchAll(/##vso\[task\.setvariable variable=(\w+);isOutput=true\](.*)/g)].map((m) => [m[1], m[2]]))

describe('StoreasyUpload@1', () => {
  let fake: FakeStoreasy
  let dir: string
  beforeEach(async () => {
    fake = await new FakeStoreasy({ oidcRules: [{ appId: 1, token: OIDC }], oidcToken: OIDC, uploadToken: UPLOAD }).start()
    dir = tempDir()
    makeFile(dir, 'Pusula.ipa', 300_000)
  })
  afterEach(() => fake.stop())

  const taskEnv = (inputs: Record<string, string>, extra: Record<string, string | undefined> = {}) => ({
    TF_BUILD: 'True',
    AGENT_TEMPDIRECTORY: dir,
    BUILD_BUILDID: '881',
    BUILD_BUILDNUMBER: '20261118.3',
    SYSTEM_COLLECTIONURI: 'https://dev.azure.com/kuzeylab/',
    SYSTEM_TEAMPROJECT: 'Pusula',
    SYSTEM_OIDCREQUESTURI: `${fake.baseUrl}/azure-oidc`,
    ENDPOINT_AUTH_SYSTEMVSSCONNECTION: JSON.stringify({ parameters: { AccessToken: ACCESS }, scheme: 'OAuth' }),
    INPUT_URL: fake.baseUrl,
    INPUT_FILE: join(dir, 'Pusula.ipa'),
    ...Object.fromEntries(Object.entries(inputs).map(([k, v]) => [`INPUT_${k.toUpperCase()}`, v])),
    ...extra,
  })

  it('workload identity: OIDC through the service connection, waits, sets output variables', async () => {
    const io = captureIo()
    const code = await run({ env: taskEnv({ appId: '1', connectedService: 'sc-1234', wait: 'true', group: 'Kuzey ici' }), io, sleep: noSleep, waitIntervalMs: 0 })
    expect(code).toBe(0)
    const az = fake.calls('POST', '/azure-oidc')[0]!
    expect(az.headers.authorization).toBe(`Bearer ${ACCESS}`)
    expect(az.query.get('serviceConnectionId')).toBe('sc-1234')
    expect(fake.calls('POST', '/api/v1.0/oidc/exchange')[0]!.body).toEqual({ appId: 1, token: OIDC, provider: 2 })
    const open = fake.calls('POST', '/api/v1.0/apps/1/artifacts')[0]!
    expect(open.headers.authorization).toBe(`Bearer ${UPLOAD}`)
    expect(open.headers['user-agent']).toMatch(/^storeasy-azure-task\//)
    expect(open.body).toMatchObject({ source: 2, delivery: { group: 'Kuzey ici' }, sourceRef: { runId: '20261118.3' } })
    const v = variables(io.out())
    expect(v).toMatchObject({ StoreasyDuplicate: 'false', StoreasyStatus: 'Delivered', StoreasyExitCode: '0' })
    expect(io.out()).toContain('##vso[task.complete result=Succeeded;]Delivered Halka 1.0.0 (42)')
    const printed = io
      .all()
      .split('\n')
      .filter((l) => !l.startsWith('##vso[task.setsecret]'))
      .join('\n')
    expect(printed).not.toContain(UPLOAD)
    expect(printed).not.toContain(ACCESS)
    expect(io.out()).toContain(`##vso[task.setsecret]${UPLOAD}`)
  })

  it('a CI token from a secret variable, when no service connection is set', async () => {
    const io = captureIo()
    expect(await run({ env: taskEnv({ appId: '3', token: DEFAULT_CI }), io, sleep: noSleep })).toBe(0)
    expect(fake.calls('POST', '/azure-oidc')).toHaveLength(0)
    expect(fake.calls('POST', '/api/v1.0/apps/3/artifacts')[0]!.headers.authorization).toBe(`Bearer ${DEFAULT_CI}`)
    expect(io.all().replace(`##vso[task.setsecret]${DEFAULT_CI}`, '')).not.toContain(DEFAULT_CI)
  })

  it('a rejected package fails the task with exit code 2', async () => {
    await fake.stop()
    fake = await new FakeStoreasy({ sessionScript: [{ status: 6, ciResult: { exitCode: 2, summary: 'Bundle id does not match' } }] }).start()
    const io = captureIo()
    const env = taskEnv({ appId: '3', token: DEFAULT_CI, wait: 'true' })
    expect(await run({ env, io, sleep: noSleep, waitIntervalMs: 0 })).toBe(2)
    expect(io.out()).toContain('##vso[task.logissue type=error]Bundle id does not match')
    expect(io.out()).toContain('##vso[task.complete result=Failed;]')
    expect(variables(io.out()).StoreasyExitCode).toBe('2')
  })

  it('a service connection without OIDC support on the agent fails with exit code 3', async () => {
    const io = captureIo()
    const code = await run({ env: taskEnv({ appId: '1', connectedService: 'sc-1234' }, { SYSTEM_OIDCREQUESTURI: undefined }), io })
    expect(code).toBe(3)
    expect(io.out()).toContain('System.OidcRequestUri')
  })

  it('reads System.AccessToken from the endpoint variables', () => {
    expect(systemAccessToken({ ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN: 'a' })).toBe('a')
    expect(systemAccessToken({ ENDPOINT_AUTH_SYSTEMVSSCONNECTION: '{"parameters":{"AccessToken":"b"}}' })).toBe('b')
    expect(systemAccessToken({})).toBeUndefined()
  })

  it('task.json and the committed bundle agree', async () => {
    const task = JSON.parse(readFileSync(fileURLToPath(new URL('../StoreasyUploadV1/task.json', import.meta.url)), 'utf8')) as {
      name: string
      version: { Major: number }
      execution: Record<string, { target: string }>
      inputs: Array<{ name: string }>
    }
    expect(`${task.name}@${task.version.Major}`).toBe('StoreasyUpload@1')
    expect(task.inputs.map((i) => i.name)).toEqual(
      expect.arrayContaining(['connectedService', 'appId', 'file', 'url', 'token', 'store', 'track', 'group', 'notes', 'wait']),
    )
    const target = fileURLToPath(new URL(`../StoreasyUploadV1/${task.execution.Node20_1!.target}`, import.meta.url))
    const child = spawn(process.execPath, [target], { env: { ...taskEnv({ appId: '3', token: DEFAULT_CI }), PATH: process.env.PATH ?? '' } })
    let stdout = ''
    child.stdout.on('data', (d) => (stdout += d))
    expect(await new Promise((r) => child.on('close', r))).toBe(0)
    expect(stdout).toContain('##vso[task.complete result=Succeeded;]')
  })
})
