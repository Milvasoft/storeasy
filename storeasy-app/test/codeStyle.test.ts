import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// The em dash and the en dash are not used anywhere: code, comments, dictionaries, tests, docs, config.
const FORBIDDEN = new RegExp('[\\u2013\\u2014]')
const ROOTS = ['src', 'test', 'e2e', 'public']
const ROOT_FILES = [
  'README.md',
  'index.html',
  'nginx.conf',
  'nginx-security-headers.conf',
  'Dockerfile',
  'Dockerfile-dev',
  '.env.example',
  'vite.config.ts',
  'vitest.config.ts',
  'eslint.config.js',
  'tailwind.config.js',
]
const EXTENSIONS = /\.(ts|tsx|js|css|html|json|md|svg|conf)$/

function files(dir: string): string[] {
  if (!existsSync(dir)) {
    return []
  }
  return readdirSync(dir, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile() && EXTENSIONS.test(entry.name))
    .map((entry) => join(entry.parentPath, entry.name))
}

describe('code style', () => {
  it('has no em dash or en dash in the sources', () => {
    const all = [
      ...ROOTS.flatMap((root) => files(resolve(process.cwd(), root))),
      ...ROOT_FILES.map((name) => resolve(process.cwd(), name)).filter((file) => existsSync(file)),
    ]
    const offenders = all.filter((file) => FORBIDDEN.test(readFileSync(file, 'utf-8')))
    expect(all.length).toBeGreaterThan(50)
    expect(offenders).toEqual([])
  })
})
