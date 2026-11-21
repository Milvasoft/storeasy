import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { asoHandlers } from '../../support/aso'

const HELPER_METHOD: Record<string, string> = { apiGet: 'GET', apiPost: 'POST', apiPut: 'PUT', apiDelete: 'DELETE' }

function calledEndpoints(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), 'src/features/aso/api.ts'), 'utf-8')
  const calls = new Set<string>()
  for (const match of source.matchAll(/(api(?:Get|Post|Put|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g)) {
    calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
  }
  return calls
}

const stubbed = new Set(
  asoHandlers.map((handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`),
)

describe('aso keywords api contract', () => {
  it('finds the endpoints the keywords tab calls', () => {
    expect(calledEndpoints().size).toBe(8)
  })

  it('answers every endpoint the tab calls and nothing else', () => {
    expect([...calledEndpoints()].filter((call) => !stubbed.has(call)).sort()).toEqual([])
    expect([...stubbed].filter((stub) => !calledEndpoints().has(stub)).sort()).toEqual([])
  })
})
