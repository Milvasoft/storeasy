import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { studioHandlers } from '../../support/studio'

const HELPER_METHOD: Record<string, string> = {
  apiGet: 'GET',
  apiPost: 'POST',
  apiPut: 'PUT',
  apiPatch: 'PATCH',
  apiPatchList: 'PATCH',
  apiDelete: 'DELETE',
}

function calledEndpoints(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), 'src/features/studio/api.ts'), 'utf-8')
  const calls = new Set<string>()
  for (const match of source.matchAll(/(api(?:Get|Post|Put|PatchList|Patch|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g)) {
    calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
  }
  return calls
}

function stubbed(): Set<string> {
  return new Set(
    studioHandlers.map((handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`),
  )
}

describe('studio api contract', () => {
  it('finds the endpoints of MockupsController the studio calls', () => {
    expect(calledEndpoints().size).toBe(14)
  })

  it('answers every endpoint the studio calls, and nothing else', () => {
    expect([...stubbed()].sort()).toEqual([...calledEndpoints()].sort())
  })
})
