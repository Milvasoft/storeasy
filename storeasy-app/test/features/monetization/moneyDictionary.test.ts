import { describe, expect, it } from 'vitest'
import { moneyEn, moneyTr } from '@/features/monetization/i18n'

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

describe('monetization dictionary', () => {
  it('has the same keys and placeholders in Turkish and English, and no empty text', () => {
    const english = leaves(moneyEn)
    const turkish = leaves(moneyTr)

    expect([...turkish.keys()].sort()).toEqual([...english.keys()].sort())
    expect(
      [...turkish].filter(([path, text]) => placeholders(text) !== placeholders(english.get(path) ?? '')).map(([path]) => path),
    ).toEqual([])
    expect([...english, ...turkish].filter(([, text]) => !text.trim()).map(([path]) => path)).toEqual([])
  })
})
