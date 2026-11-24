import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { easHandlers } from '../../support/eas'

const HELPER_METHOD: Record<string, string> = { apiGet: 'GET', apiPost: 'POST', apiPut: 'PUT', apiPatch: 'PATCH', apiDelete: 'DELETE' }

function calledEndpoints(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), 'src/features/eas/api.ts'), 'utf-8')
  const calls = new Set<string>()
  for (const match of source.matchAll(/(api(?:Get|Post|Put|Patch|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g)) {
    calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
  }
  return calls
}

function stubbed(): Set<string> {
  return new Set(
    easHandlers.map((handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`),
  )
}

// The API side: every route of EasController.
function controllerRoutes(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), '../storeasy-api/src/Storeasy.Api/Controllers/EasController.cs'), 'utf-8')
  return new Set(
    [...source.matchAll(/\[Http(Get|Post|Put|Patch|Delete)\("([^"]+)"\)\]/g)].map((match) => `${match[1].toUpperCase()} /eas/${match[2]}`),
  )
}

describe('EAS api contract', () => {
  it('finds every endpoint of EasController the screens call', () => {
    expect(calledEndpoints().size).toBe(16)
    expect([...calledEndpoints()].sort()).toEqual([...controllerRoutes()].sort())
  })

  it('answers every endpoint the screens call, and nothing else', () => {
    expect([...stubbed()].sort()).toEqual([...calledEndpoints()].sort())
  })
})
