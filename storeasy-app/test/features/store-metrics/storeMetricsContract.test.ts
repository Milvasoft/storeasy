import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { storeMetricsHandlers } from '../../support/storeMetrics'

const HELPER_METHOD: Record<string, string> = { apiGet: 'GET', apiPost: 'POST', apiPut: 'PUT', apiDelete: 'DELETE' }

function calledEndpoints(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), 'src/features/store-metrics/api.ts'), 'utf-8')
  const calls = new Set<string>()
  for (const match of source.matchAll(/(api(?:Get|Post|Put|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g)) {
    calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
  }
  return calls
}

function stubbed(): Set<string> {
  return new Set(
    storeMetricsHandlers.map(
      (handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`,
    ),
  )
}

describe('store metrics api contract', () => {
  it('finds the endpoints of StoreMetricsController the screens call', () => {
    expect(calledEndpoints().size).toBe(6)
  })

  it('answers every endpoint the screens call, and nothing else', () => {
    expect([...stubbed()].sort()).toEqual([...calledEndpoints()].sort())
  })
})
