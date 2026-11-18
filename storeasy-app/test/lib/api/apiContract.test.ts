import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { defaultHandlers } from '../../support/handlers'

const API_DIR = resolve(process.cwd(), 'src/lib/api')

// endpoints.ts, or every file of endpoints/ once it is split.
function endpointSources(): string {
  const files = [join(API_DIR, 'endpoints.ts')].filter((file) => existsSync(file))
  const folder = join(API_DIR, 'endpoints')
  if (existsSync(folder)) {
    for (const name of readdirSync(folder)) {
      files.push(join(folder, name))
    }
  }
  return files.map((file) => readFileSync(file, 'utf-8')).join('\n')
}

const HELPER_METHOD: Record<string, string> = {
  apiGet: 'GET',
  apiGetList: 'GET',
  apiPost: 'POST',
  apiPut: 'PUT',
  apiPatch: 'PATCH',
  apiPatchList: 'PATCH',
  apiDelete: 'DELETE',
}

function calledEndpoints(): Set<string> {
  const calls = new Set<string>()
  // The path can sit on the next line when the call is wrapped; the type argument is optional.
  for (const match of endpointSources().matchAll(
    /(api(?:GetList|Get|Post|Put|PatchList|Patch|Delete))(?:<[^>]*(?:<[^<>]*>)?[^>]*>)?\(\s*'(\/[^']+)'/g,
  )) {
    calls.add(`${HELPER_METHOD[match[1]]} ${match[2]}`)
  }
  // The token refresh bypasses the helpers (it sends the expired token itself), so it is read from http.ts.
  const httpSource = readFileSync(join(API_DIR, 'http.ts'), 'utf-8')
  for (const match of httpSource.matchAll(/fetch\(buildUrl\('(\/[^']+)'\)/g)) {
    calls.add(`POST ${match[1]}`)
  }
  return calls
}

function stubbedEndpoints(): Set<string> {
  return new Set(
    defaultHandlers.map((handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`),
  )
}

describe('api contract', () => {
  it('finds the endpoints the app calls', () => {
    // Guards the reader: a refactor beyond recognition must not make the next test pass on an empty set.
    expect(calledEndpoints().size).toBeGreaterThan(20)
  })

  it('answers every endpoint the app calls', () => {
    const missing = [...calledEndpoints()].filter((call) => !stubbedEndpoints().has(call)).sort()
    expect(missing, 'add these to defaultHandlers in test/support/handlers.ts').toEqual([])
  })

  it('has no handler for an endpoint the app never calls', () => {
    const called = calledEndpoints()
    const unused = [...stubbedEndpoints()].filter((stub) => !called.has(stub)).sort()
    expect(unused, 'remove these from defaultHandlers, or add the missing api client method').toEqual([])
  })
})
