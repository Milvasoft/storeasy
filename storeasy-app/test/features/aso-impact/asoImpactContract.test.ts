import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { impactHandlers } from '../../support/asoImpact'

const HELPER_METHOD: Record<string, string> = { apiGet: 'GET', apiPost: 'POST', apiPut: 'PUT', apiDelete: 'DELETE' }

function calledEndpoints(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), 'src/features/aso-impact/api.ts'), 'utf-8')
  const calls = new Set<string>()
  for (const match of source.matchAll(/(api(?:Get|Post|Put|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g)) {
    calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
  }
  return calls
}

function stubbed(): Set<string> {
  return new Set(
    impactHandlers.map((handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`),
  )
}

describe('impact api contract', () => {
  it('finds the endpoints of AsoImpactController the screens call, all of them reads', () => {
    expect([...calledEndpoints()].sort()).toEqual(['GET /aso-impact', 'GET /aso-impact/events', 'GET /aso-impact/recent'])
  })

  it('answers every endpoint the screens call, and nothing else', () => {
    expect([...stubbed()].sort()).toEqual([...calledEndpoints()].sort())
  })
})
