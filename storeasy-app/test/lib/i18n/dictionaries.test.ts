import { describe, expect, it } from 'vitest'
import { en } from '@/lib/i18n/en'
import { interpolate } from '@/lib/i18n/interpolate'
import { tr } from '@/lib/i18n/tr'

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

function placeholders(text: string): string[] {
  return [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort()
}

describe('dictionaries', () => {
  it('has the same keys in every language, arrays included', () => {
    expect([...leaves(tr).keys()].sort()).toEqual([...leaves(en).keys()].sort())
  })

  it('uses the same placeholders in every language', () => {
    const english = leaves(en)
    const mismatched = [...leaves(tr)]
      .filter(([path, text]) => placeholders(text).join() !== placeholders(english.get(path) ?? '').join())
      .map(([path]) => path)
    expect(mismatched).toEqual([])
  })

  it('has no empty text', () => {
    const empty = [...leaves(en), ...leaves(tr)].filter(([, text]) => text.trim() === '').map(([path]) => path)
    expect(empty).toEqual([])
  })

  it('fills every occurrence of a placeholder', () => {
    expect(interpolate('{a} and {a} of {b}', { a: 1, b: 'x' })).toBe('1 and 1 of x')
    expect(interpolate('{missing}', {})).toBe('{missing}')
  })
})
