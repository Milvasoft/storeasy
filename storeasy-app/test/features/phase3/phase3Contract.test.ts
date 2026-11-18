import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { phase3Handlers } from '../../support/phase3'

const HELPER_METHOD: Record<string, string> = {
  apiGet: 'GET',
  apiPost: 'POST',
  apiPut: 'PUT',
  apiPatch: 'PATCH',
  apiPatchList: 'PATCH',
  apiDelete: 'DELETE',
}

const API_FILES = ['tokens', 'intake'].map((feature) => `src/features/${feature}/api.ts`)

// A template path (`/apps/${appId}/artifacts`) is compared as its MSW pattern (/apps/:appId/artifacts).
function calledEndpoints(): Set<string> {
  const calls = new Set<string>()
  for (const file of API_FILES) {
    const source = readFileSync(resolve(process.cwd(), file), 'utf-8')
    for (const match of source.matchAll(
      /(api(?:Get|Post|Put|PatchList|Patch|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*['`](\/[^'`]+)['`]/g,
    )) {
      calls.add(`${HELPER_METHOD[match[1]]} ${match[2].replace(/\$\{(\w+)\}/g, ':$1')}`)
    }
  }
  return calls
}

function stubbed(): Set<string> {
  return new Set(
    phase3Handlers.map((handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`),
  )
}

describe('phase 3 api contract', () => {
  it('finds the endpoints the token and intake screens call', () => {
    expect(calledEndpoints().size).toBeGreaterThan(20)
  })

  it('answers every endpoint the screens call', () => {
    const known = stubbed()
    expect([...calledEndpoints()].filter((call) => !known.has(call)).sort()).toEqual([])
  })

  it('has no handler for an endpoint the screens never call', () => {
    const called = calledEndpoints()
    expect([...stubbed()].filter((stub) => !called.has(stub)).sort()).toEqual([])
  })
})
