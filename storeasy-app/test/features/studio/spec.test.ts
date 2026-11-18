import { describe, expect, it } from 'vitest'
import { studioEn, studioTr } from '@/features/studio/i18n'
import {
  captionMap,
  changedCaptions,
  copyCaptions,
  fromPercent,
  isLocaleTag,
  normalizeSpec,
  parseLocales,
  previewPage,
  removeSlotAsset,
  setSlotAsset,
  slotAsset,
  toPercent,
  trimSlots,
} from '@/features/studio/spec'
import { MockupDeviceFamily, type MockupSpec } from '@/features/studio/types'

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

const placeholders = (text: string) =>
  [...text.matchAll(/\{(\w+)\}/g)]
    .map((match) => match[1])
    .sort()
    .join()

describe('studio dictionary', () => {
  it('has the same keys and placeholders in both languages and no empty text', () => {
    const en = leaves(studioEn)
    const tr = leaves(studioTr)

    expect([...tr.keys()].sort()).toEqual([...en.keys()].sort())
    expect([...tr].filter(([path, text]) => placeholders(text) !== placeholders(en.get(path) ?? '')).map(([path]) => path)).toEqual([])
    expect([...en, ...tr].filter(([, text]) => text.trim() === '')).toEqual([])
  })
})

describe('normalizeSpec', () => {
  it('fills missing parts with the schema defaults', () => {
    const spec = normalizeSpec({ SchemaVersion: 1, Background: { Kind: 'solid', Colors: ['#000000'], Angle: 0 } } as MockupSpec)

    expect(spec.Background?.Colors).toEqual(['#000000'])
    expect(spec.Frame?.Scale).toBe(0.78)
    expect(spec.Text?.Title?.MaxLines).toBe(2)
    expect(spec.Layers).toEqual([])
  })

  it('restores the badge locale keys the response normalization capitalized', () => {
    const spec = normalizeSpec({
      SchemaVersion: 1,
      Layers: [
        {
          Type: 'badge',
          X: 0,
          Y: 0,
          Width: 0,
          Height: 0,
          Radius: 0,
          Opacity: 1,
          Above: true,
          TextSize: 0.04,
          Texts: { '*': 'NEW', Tr: 'YENİ', 'De-DE': 'NEU' },
        },
      ],
    })

    expect(spec.Layers?.[0].Texts).toEqual({ '*': 'NEW', tr: 'YENİ', 'de-DE': 'NEU' })
  })
})

describe('slots', () => {
  const slots = [
    { Order: 1, Family: MockupDeviceFamily.Phone, Locale: null, AssetId: 1 },
    { Order: 1, Family: MockupDeviceFamily.Phone, Locale: 'de-DE', AssetId: 2 },
    { Order: 3, Family: MockupDeviceFamily.Tablet, Locale: null, AssetId: 3 },
  ]

  it('uses the locale own screenshot, else the one for every locale', () => {
    expect(slotAsset(slots, 1, MockupDeviceFamily.Phone, 'de-DE')?.AssetId).toBe(2)
    expect(slotAsset(slots, 1, MockupDeviceFamily.Phone, 'tr')?.AssetId).toBe(1)
    expect(slotAsset(slots, 2, MockupDeviceFamily.Phone, null)).toBeNull()
  })

  it('replaces one slot, family and locale, and removes and trims', () => {
    const next = setSlotAsset(slots, { Order: 1, Family: MockupDeviceFamily.Phone, Locale: null, AssetId: 9 })

    expect(
      next
        .filter((slot) => slot.Order === 1)
        .map((slot) => slot.AssetId)
        .sort(),
    ).toEqual([2, 9])
    expect(removeSlotAsset(next, 1, MockupDeviceFamily.Phone, 'de-DE').map((slot) => slot.AssetId)).toEqual([9, 3])
    expect(trimSlots(slots, 2).map((slot) => slot.AssetId)).toEqual([1, 2])
  })

  it('pages the preview three slots at a time', () => {
    expect(previewPage(1, 6)).toEqual([1, 2, 3])
    expect(previewPage(5, 6)).toEqual([4, 5, 6])
    expect(previewPage(4, 4)).toEqual([4])
  })
})

describe('captions', () => {
  const saved = captionMap([{ Locale: 'tr', SlotOrder: 1, Title: 'Bir', Subtitle: 'Alt' }])

  it('sends only the changed cells, an emptied one as empty texts', () => {
    const draft = {
      ...saved,
      'tr|1': { title: '', subtitle: '' },
      'en-US|1': { title: 'One', subtitle: '' },
      'tr|2': { title: '', subtitle: '' },
    }

    expect(changedCaptions(saved, draft)).toEqual([
      { Locale: 'en-US', SlotOrder: 1, Title: 'One', Subtitle: '' },
      { Locale: 'tr', SlotOrder: 1, Title: '', Subtitle: '' },
    ])
  })

  it('copies into empty cells only unless told to overwrite', () => {
    const draft = { ...saved, 'en-US|1': { title: 'Kept', subtitle: '' } }

    expect(copyCaptions(draft, 'tr', 'en-US', 2, false)['en-US|1']).toEqual({ title: 'Kept', subtitle: 'Alt' })
    expect(copyCaptions(draft, 'tr', 'en-US', 2, true)['en-US|1']).toEqual({ title: 'Bir', subtitle: 'Alt' })
  })
})

describe('small helpers', () => {
  it('converts fractions and percents and reads locale lists', () => {
    expect(toPercent(0.78)).toBe(78)
    expect(fromPercent(27)).toBe(0.27)
    expect(parseLocales('tr, en-US  en-US;de-DE')).toEqual(['tr', 'en-US', 'de-DE'])
    expect(isLocaleTag('de-DE')).toBe(true)
    expect(isLocaleTag('Deutsch')).toBe(false)
  })
})
