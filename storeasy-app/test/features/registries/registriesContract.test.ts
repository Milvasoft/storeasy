import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { registriesHandlers } from '../../support/registries'

const HELPER_METHOD: Record<string, string> = {
  apiGet: 'GET',
  apiPost: 'POST',
  apiPut: 'PUT',
  apiPatch: 'PATCH',
  apiPatchList: 'PATCH',
  apiDelete: 'DELETE',
}

function calledEndpoints(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), 'src/features/registries/api.ts'), 'utf-8')
  const calls = new Set<string>()
  for (const match of source.matchAll(/(api(?:Get|Post|Put|PatchList|Patch|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g)) {
    calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
  }
  return calls
}

function stubbed(): Set<string> {
  return new Set(
    registriesHandlers.map(
      (handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`,
    ),
  )
}

// The API side: every route of RegistriesController.
function controllerRoutes(): Set<string> {
  const source = readFileSync(resolve(process.cwd(), '../storeasy-api/src/Storeasy.Api/Controllers/RegistriesController.cs'), 'utf-8')
  return new Set(
    [...source.matchAll(/\[Http(Get|Post|Put|Patch|Delete)\("([^"]+)"\)\]/g)].map(
      (match) => `${match[1].toUpperCase()} /registries/${match[2]}`,
    ),
  )
}

describe('package registries api contract', () => {
  it('finds every endpoint of RegistriesController the screens call', () => {
    expect(calledEndpoints().size).toBe(29)
    expect([...calledEndpoints()].sort()).toEqual([...controllerRoutes()].sort())
  })

  it('answers every endpoint the screens call, and nothing else', () => {
    expect([...stubbed()].sort()).toEqual([...calledEndpoints()].sort())
  })
})
