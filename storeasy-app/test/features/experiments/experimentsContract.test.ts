import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { experimentHandlers } from '../../support/experiments'

const HELPER_METHOD: Record<string, string> = { apiGet: 'GET', apiPost: 'POST', apiPut: 'PUT', apiDelete: 'DELETE' }

function calledEndpoints(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), 'src/features/experiments/api.ts'), 'utf-8')
  const calls = new Set<string>()
  for (const match of source.matchAll(/(api(?:Get|Post|Put|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g)) {
    calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
  }
  return calls
}

function stubbed(): Set<string> {
  return new Set(
    experimentHandlers.map(
      (handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`,
    ),
  )
}

describe('experiments api contract', () => {
  it('finds the endpoints of AsoExperimentsController the screens call', () => {
    expect(calledEndpoints().size).toBe(9)
  })

  it('answers every endpoint the screens call, and nothing else', () => {
    expect([...stubbed()].sort()).toEqual([...calledEndpoints()].sort())
  })
})
