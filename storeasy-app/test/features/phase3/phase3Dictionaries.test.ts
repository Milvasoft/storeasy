import { describe, expect, it } from 'vitest'
import { intakeEn, intakeTr } from '@/features/intake/i18n'
import { mcpEn, mcpTr } from '@/features/mcp/i18n'
import { tokensEn, tokensTr } from '@/features/tokens/i18n'

function leaves(tree: object, prefix = ''): Map<string, string> {
  const out = new Map<string, string>()
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (typeof value === 'string') {
      out.set(path, value)
    } else {
      for (const [childPath, childValue] of leaves(value as object, path)) {
        out.set(childPath, childValue)
      }
    }
  }
  return out
}

function placeholders(text: string): string {
  return [...text.matchAll(/\{(\w+)\}/g)]
    .map((match) => match[1])
    .sort()
    .join()
}

const PAIRS: Array<[string, object, object]> = [
  ['tokens', tokensEn, tokensTr],
  ['mcp', mcpEn, mcpTr],
  ['intake', intakeEn, intakeTr],
]

describe('phase 3 dictionaries', () => {
  it.each(PAIRS)('%s has the same keys, placeholders and no empty text', (_name, en, tr) => {
    const english = leaves(en)
    const turkish = leaves(tr)

    expect([...turkish.keys()].sort()).toEqual([...english.keys()].sort())
    expect(
      [...turkish].filter(([path, text]) => placeholders(text) !== placeholders(english.get(path) ?? '')).map(([path]) => path),
    ).toEqual([])
    expect([...english, ...turkish].filter(([, text]) => text.trim() === '').map(([path]) => path)).toEqual([])
  })
})
