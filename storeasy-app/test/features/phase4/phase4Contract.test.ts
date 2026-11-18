import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { phase4Handlers } from '../../support/phase4'

const HELPER_METHOD: Record<string, string> = {
  apiGet: 'GET',
  apiPost: 'POST',
  apiPut: 'PUT',
  apiPatch: 'PATCH',
  apiPatchList: 'PATCH',
  apiDelete: 'DELETE',
}

const API_FILES = ['reviews', 'rejections', 'testflight', 'webhooks', 'notifications', 'account-preferences', 'rules-watcher'].map(
  (feature) => `src/features/${feature}/api.ts`,
)

function calledEndpoints(): Set<string> {
  const calls = new Set<string>()
  for (const file of API_FILES) {
    const source = readFileSync(resolve(process.cwd(), file), 'utf-8')
    for (const match of source.matchAll(/(api(?:Get|Post|Put|PatchList|Patch|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g)) {
      calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
    }
  }
  return calls
}

function stubbed(): Set<string> {
  return new Set(
    phase4Handlers.map((handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`),
  )
}

describe('phase 4 api contract', () => {
  it('finds the endpoints the phase 4 screens call', () => {
    expect(calledEndpoints().size).toBeGreaterThan(50)
  })

  it('answers every endpoint the phase 4 screens call', () => {
    const known = stubbed()
    const missing = [...calledEndpoints()].filter((call) => !known.has(call)).sort()
    expect(missing, 'add these to phase4Handlers in test/support/phase4.ts').toEqual([])
  })

  it('has no phase 4 handler for an endpoint the screens never call', () => {
    const called = calledEndpoints()
    expect([...stubbed()].filter((stub) => !called.has(stub)).sort()).toEqual([])
  })
})
