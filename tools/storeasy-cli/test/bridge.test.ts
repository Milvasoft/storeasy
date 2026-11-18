// SPDX-License-Identifier: AGPL-3.0-only
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { Client } from '@modelcontextprotocol/sdk/client/index.js'
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js'
import type { Tool } from '@modelcontextprotocol/sdk/types.js'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { connectRemote, createBridge } from '../src/bridge.js'
import { ApiClient } from '../src/http.js'
import { DEFAULT_PAT, FakeStoreasy } from './fakeServer.js'
import { makeFile, noSleep, tempDir } from './helpers.js'

const REMOTE_TOOLS: Tool[] = [
  {
    name: 'apps_summary',
    description: 'Summary of every app.',
    inputSchema: { type: 'object', properties: { store: { type: 'string' } } },
  },
  {
    name: 'screenshots_upload_dir',
    description: 'Plan a screenshot set from uploaded images.',
    inputSchema: {
      type: 'object',
      properties: {
        appId: { type: 'integer' },
        locale: { type: 'string' },
        screenshots: {
          type: 'array',
          items: { type: 'integer' },
          description: 'Screenshots in display order.',
          'x-storeasy-upload': { kind: 'folder', purpose: 1, accept: ['.png', '.jpg'] },
        },
      },
      required: ['appId', 'locale', 'screenshots'],
    },
  },
  {
    name: 'graphic_set',
    description: 'Set the feature graphic.',
    inputSchema: {
      type: 'object',
      properties: {
        appId: { type: 'integer' },
        graphic: { type: 'integer', 'x-storeasy-upload': { kind: 'file', purpose: 'Graphic', accept: ['.png'] } },
        extras: { type: 'array', items: { type: 'integer' }, 'x-storeasy-upload': { kind: 'files', purpose: 2, maxFiles: 2 } },
      },
    },
  },
]

type TextResult = { content: Array<{ type: string; text: string }>; isError?: boolean }

describe('storeasy-mcp bridge', () => {
  let fake: FakeStoreasy
  let client: Client
  let cwd: string

  beforeEach(async () => {
    fake = await new FakeStoreasy({ remoteTools: REMOTE_TOOLS, partSize: 5 * 1024 * 1024 }).start()
    cwd = tempDir()
    const api = new ApiClient({ baseUrl: fake.baseUrl, token: DEFAULT_PAT, sleep: noSleep })
    const bridge = createBridge({ api, connect: () => connectRemote(api), cwd, env: {}, waitIntervalMs: 0 })
    const [a, b] = InMemoryTransport.createLinkedPair()
    await bridge.connect(a)
    client = new Client({ name: 'test', version: '1' })
    await client.connect(b)
  })
  afterEach(async () => {
    await client.close()
    await fake.stop()
  })

  it('lists remote tools with upload arguments rewritten to local paths, plus artifact_upload', async () => {
    const { tools } = await client.listTools()
    expect(tools.map((t) => t.name)).toEqual(['apps_summary', 'screenshots_upload_dir', 'graphic_set', 'artifact_upload'])
    const shots = tools.find((t) => t.name === 'screenshots_upload_dir')!.inputSchema.properties!.screenshots as Record<string, unknown>
    expect(shots.type).toBe('string')
    expect(shots.items).toBeUndefined()
    expect(shots['x-storeasy-upload']).toBeUndefined()
    expect(String(shots.description)).toMatch(/^Screenshots in display order\. Local folder path/)
    const g = tools.find((t) => t.name === 'graphic_set')!.inputSchema.properties as Record<string, Record<string, unknown>>
    expect(g.graphic!.type).toBe('string')
    expect(g.extras).toMatchObject({ type: 'array', items: { type: 'string' } })
    expect(tools[0]!.inputSchema).toEqual(REMOTE_TOOLS[0]!.inputSchema)
  })

  it('uploads a folder in natural name order and calls the remote tool with asset ids', async () => {
    const dir = join(cwd, 'shots', 'tr')
    mkdirSync(dir, { recursive: true })
    for (const n of ['10.png', '2.png', '1.jpg', 'notes.txt', '.DS_Store']) {
      writeFileSync(join(dir, n), `image ${n}`)
    }
    const r = (await client.callTool({
      name: 'screenshots_upload_dir',
      arguments: { appId: 3, locale: 'tr', screenshots: 'shots/tr' },
    })) as TextResult
    expect(r.isError).toBeFalsy()
    const call = fake.mcpCalls.at(-1)!
    expect(call.name).toBe('screenshots_upload_dir')
    expect(call.authorization).toBe(`Bearer ${DEFAULT_PAT}`)
    const ids = call.arguments.screenshots as number[]
    expect(ids).toHaveLength(3)
    expect(ids.map((id) => fake.assets.get(id)!.fileName)).toEqual(['1.jpg', '2.png', '10.png'])
    for (const id of ids) {
      const a = fake.assets.get(id)!
      expect(a.complete).toBe(true)
      expect(a.purpose).toBe(1)
      expect(a.stored!.toString()).toBe(`image ${a.fileName}`)
    }
    expect(call.arguments).toMatchObject({ appId: 3, locale: 'tr' })
    expect(JSON.parse(r.content[0]!.text).received.screenshots).toEqual(ids)
  })

  it('uploads single files and file lists; asset ids given by the client pass through', async () => {
    const icon = makeFile(cwd, 'feature.png', 2048)
    const e1 = makeFile(cwd, 'a.bin', 10)
    const r = (await client.callTool({ name: 'graphic_set', arguments: { appId: 3, graphic: 'feature.png', extras: [e1, 'a.bin'] } })) as TextResult
    expect(r.isError).toBeFalsy()
    const args = fake.mcpCalls.at(-1)!.arguments
    expect(typeof args.graphic).toBe('number')
    expect(fake.assets.get(args.graphic as number)).toMatchObject({ fileName: 'feature.png', purpose: 2 })
    expect(fake.assets.get(args.graphic as number)!.stored).toEqual(readFileSync(icon))
    expect(args.extras).toHaveLength(2)

    await client.callTool({ name: 'graphic_set', arguments: { appId: 3, graphic: 77, extras: [5, 6] } })
    expect(fake.mcpCalls.at(-1)!.arguments).toEqual({ appId: 3, graphic: 77, extras: [5, 6] })
  })

  it('refuses a wrong file type or a missing folder without calling the remote tool', async () => {
    makeFile(cwd, 'feature.jpg', 10)
    const before = fake.mcpCalls.length
    const wrong = (await client.callTool({ name: 'graphic_set', arguments: { appId: 3, graphic: 'feature.jpg' } })) as TextResult
    expect(wrong.isError).toBe(true)
    expect(wrong.content[0]!.text).toContain('file type not accepted (.png)')
    const missing = (await client.callTool({ name: 'screenshots_upload_dir', arguments: { appId: 3, locale: 'tr', screenshots: 'nope' } })) as TextResult
    expect(missing.isError).toBe(true)
    expect(missing.content[0]!.text).toContain('Folder not found')
    const tooMany = (await client.callTool({ name: 'graphic_set', arguments: { appId: 3, extras: ['a', 'b', 'c'] } })) as TextResult
    expect(tooMany.content[0]!.text).toContain('Too many files: 3, at most 2.')
    expect(fake.mcpCalls.length).toBe(before)
  })

  it('forwards tools without file arguments unchanged', async () => {
    const r = (await client.callTool({ name: 'apps_summary', arguments: { store: 'google' } })) as TextResult
    expect(JSON.parse(r.content[0]!.text)).toEqual({ tool: 'apps_summary', received: { store: 'google' } })
  })

  it('artifact_upload uploads a local build through build intake', async () => {
    makeFile(cwd, 'halka.aab', 6 * 1024 * 1024)
    const r = (await client.callTool({ name: 'artifact_upload', arguments: { path: 'halka.aab', appId: 3, track: 'internal', wait: true } })) as TextResult
    expect(r.isError).toBeFalsy()
    const out = JSON.parse(r.content[0]!.text) as Record<string, unknown>
    expect(out).toMatchObject({ duplicate: false, parts: 2, status: 'Delivered', exitCode: 0, storeJobId: 9182 })
    const open = fake.calls('POST', '/api/v1.0/apps/3/artifacts')[0]!
    expect(open.headers.authorization).toBe(`Bearer ${DEFAULT_PAT}`)
    expect(open.body).toMatchObject({ source: 8, delivery: { track: 'internal' } })
    expect(r.content[0]!.text).not.toContain('X-Amz-Signature')
  })
})
