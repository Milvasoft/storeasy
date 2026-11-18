import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { contentHandlers } from '../support/content'
import { defaultHandlers } from '../support/handlers'

const HELPER_METHOD: Record<string, string> = {
  apiGet: 'GET',
  apiPost: 'POST',
  apiPut: 'PUT',
  apiPatch: 'PATCH',
  apiPatchList: 'PATCH',
  apiDelete: 'DELETE',
}

function calledEndpoints(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), 'src/features/common/api.ts'), 'utf-8')
  const calls = new Set<string>()
  for (const match of source.matchAll(/(api(?:Get|Post|Put|PatchList|Patch|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g)) {
    calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
  }
  return calls
}

function stubbed(handlers: typeof contentHandlers): Set<string> {
  return new Set(
    handlers.map((handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`),
  )
}

describe('phase 2 api contract', () => {
  it('finds the endpoints the phase 2 screens call', () => {
    expect(calledEndpoints().size).toBeGreaterThan(40)
  })

  it('answers every endpoint the phase 2 screens call', () => {
    const known = new Set([...stubbed(contentHandlers), ...stubbed(defaultHandlers)])
    const missing = [...calledEndpoints()].filter((call) => !known.has(call)).sort()
    expect(missing, 'add these to contentHandlers in test/support/content.ts').toEqual([])
  })

  it('has no phase 2 handler for an endpoint the screens never call', () => {
    const called = calledEndpoints()
    const unused = [...stubbed(contentHandlers)].filter((stub) => !called.has(stub)).sort()
    expect(unused).toEqual([])
  })
})
