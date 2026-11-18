// SPDX-License-Identifier: AGPL-3.0-only
// Lists the licenses of the runtime dependencies of the storeasy package (what an `npm install storeasy` pulls in)
// and fails on a license outside the allow list. Direct dependencies must be MIT or Apache-2.0; transitive ones may
// also use the other permissive licenses below. The action and the task bundle only storeasy's own code.
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const DIRECT = new Set(['MIT', 'Apache-2.0'])
const TRANSITIVE = new Set(['MIT', 'Apache-2.0', 'ISC', 'BSD-2-Clause', 'BSD-3-Clause', '0BSD', 'BlueOak-1.0.0'])

const tree = JSON.parse(
  execFileSync(process.platform === 'win32' ? 'npm.cmd' : 'npm', ['ls', '--omit=dev', '--all', '--json', '-w', 'storeasy'], {
    cwd: root,
    encoding: 'utf8',
    shell: process.platform === 'win32',
  }),
)

const seen = new Map()
function licenseOf(name) {
  for (const base of [join(root, 'node_modules'), join(root, 'storeasy-cli', 'node_modules')]) {
    try {
      const pkg = JSON.parse(readFileSync(join(base, name, 'package.json'), 'utf8'))
      return typeof pkg.license === 'string' ? pkg.license : JSON.stringify(pkg.license ?? pkg.licenses ?? 'UNKNOWN')
    } catch {
      // try the next location
    }
  }
  return 'UNKNOWN'
}
function walk(deps, depth) {
  for (const [name, info] of Object.entries(deps ?? {})) {
    if (!info.version) {
      continue // optional peer not installed
    }
    if (!seen.has(name)) {
      seen.set(name, { license: licenseOf(name), direct: depth === 0 })
    }
    walk(info.dependencies, depth + 1)
  }
}
walk(tree.dependencies?.storeasy?.dependencies, 0)

const bad = []
const counts = {}
for (const [name, { license, direct }] of seen) {
  counts[license] = (counts[license] ?? 0) + 1
  const allowed = direct ? DIRECT : TRANSITIVE
  // SPDX "OR" expressions are fine when one side is allowed.
  if (!license.split(/\s+OR\s+|[()]/).some((l) => allowed.has(l.trim()))) {
    bad.push(`${name}: ${license}${direct ? ' (direct)' : ''}`)
  }
}
console.log(`runtime dependencies: ${seen.size}`, counts)
if (bad.length) {
  console.error(`not allowed:\n${bad.join('\n')}`)
  process.exit(1)
}
