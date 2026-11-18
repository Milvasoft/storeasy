// SPDX-License-Identifier: AGPL-3.0-only
// House rules for every text file in tools/: no em or en dashes, and an SPDX header on every source file.
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const SKIP = new Set(['node_modules', '.git', 'coverage'])
const TEXT = /\.(ts|js|mjs|json|md|yml|yaml)$/
const SOURCE = /\.(ts|mjs)$|eslint\.config\.js$/
const DASHES = new RegExp('[\\u2013\\u2014]')
const problems = []

function walk(dir) {
  for (const name of readdirSync(dir)) {
    if (SKIP.has(name)) {
      continue
    }
    const p = join(dir, name)
    if (statSync(p).isDirectory()) {
      walk(p)
      continue
    }
    if (!TEXT.test(name) || name === 'package-lock.json') {
      continue
    }
    const rel = relative(root, p).split(sep).join('/')
    const text = readFileSync(p, 'utf8')
    text.split('\n').forEach((line, i) => {
      if (DASHES.test(line)) {
        problems.push(`${rel}:${i + 1}: em or en dash`)
      }
    })
    const generated = rel.includes('/dist/')
    if (!generated && SOURCE.test(name) && !text.slice(0, 200).includes('SPDX-License-Identifier: AGPL-3.0-only')) {
      problems.push(`${rel}: missing SPDX-License-Identifier header`)
    }
  }
}

walk(root)
if (problems.length) {
  console.error(problems.join('\n'))
  process.exit(1)
}
console.log('check-text: ok')
