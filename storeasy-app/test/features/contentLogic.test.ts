import { describe, expect, it } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { dataSafetyCsv, importDataSafetyCsv, parseCsv, readContentRating, readPrivacy } from '@/features/answers/answerDocuments'
import { p2en } from '@/features/common/i18n/en'
import { p2tr } from '@/features/common/i18n/tr'
import { fixRouteHref, storeFromSlug } from '@/features/common/store'
import { confirmationText } from '@/features/common/SubmitDialog'
import { byteCount, charCount, overLimit, splitList } from '@/features/common/text'
import { MediaSlotFill, RuleApplicability } from '@/features/common/types'
import { counterText, draftChanges, draftKey } from '@/features/listings/ListingsPage'
import { answerValue } from '@/features/metadata/MetadataPages'
import { invalidRegions } from '@/features/profile/AppProfilePage'
import { isProduction } from '@/features/releases/ReleasesPage'
import { classState, draftChangeCount, emptyDraft, move } from '@/features/screenshots/MediaPage'

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

describe('phase 2 dictionaries', () => {
  it('have the same keys and placeholders in both languages and no empty text', () => {
    const english = leaves(p2en)
    const turkish = leaves(p2tr)

    expect([...turkish.keys()].sort()).toEqual([...english.keys()].sort())
    expect(
      [...turkish].filter(([path, text]) => placeholders(text) !== placeholders(english.get(path) ?? '')).map(([path]) => path),
    ).toEqual([])
    expect([...english, ...turkish].filter(([, text]) => text.trim() === '').map(([path]) => path)).toEqual([])
  })
})

describe('listing text limits', () => {
  it('counts Apple keywords in UTF-8 bytes, so Turkish letters take two', () => {
    expect(charCount('çiğ')).toBe(3)
    expect(byteCount('çiğ')).toBe(5)
    expect(overLimit('ğ'.repeat(60), 100, 100)).toBe(true)
    expect(overLimit('g'.repeat(100), 100, 100)).toBe(false)
  })

  it('counts an emoji as one character', () => {
    expect(charCount('harita 🧭')).toBe(8)
  })

  it('shows the characters and the bytes the catalog limits', () => {
    expect(counterText('ğa', { MaxLength: 100, MaxBytes: 100 }, p2en)).toBe('2 / 100 characters · 3 / 100 bytes')
    expect(counterText('a', {}, p2en)).toBeNull()
  })

  it('plans only the drafted values that differ from the store', () => {
    const fields = [
      { Key: 'name', Locale: 'tr', Value: 'Pusula', Required: true },
      { Key: 'subtitle', Locale: 'tr', Value: 'Eski', Required: false },
    ]
    const draft = { [draftKey('tr', 'name')]: 'Pusula', [draftKey('tr', 'subtitle')]: '', [draftKey(null, 'contactEmail')]: 'a@b.co' }

    expect(draftChanges(draft, fields)).toEqual([
      { Locale: 'tr', Field: 'subtitle', Value: null },
      { Locale: null, Field: 'contactEmail', Value: 'a@b.co' },
    ])
  })
})

describe('app screen helpers', () => {
  it('turns a readiness fix route into an app address with the store', () => {
    expect(fixRouteHref(101, StoreKind.Apple, 'listing/screenshots?locale=en-US')).toBe(
      '/apps/101/listing/screenshots?locale=en-US&store=apple',
    )
    expect(fixRouteHref(101, StoreKind.Google, 'release/builds')).toBe('/apps/101/release/versions?store=google')
    expect(storeFromSlug('ms')).toBe(StoreKind.Microsoft)
    expect(storeFromSlug('x')).toBeNull()
  })

  it('builds the typed confirmation the API expects', () => {
    expect(confirmationText(' Kumbara ', '1.0')).toBe('Kumbara 1.0')
    expect(confirmationText('Kumbara', null)).toBe('Kumbara')
  })

  it('finds region codes that are not two letters', () => {
    expect(invalidRegions('tr, DE, USA,')).toEqual(['USA'])
    expect(splitList(' a,, b ')).toEqual(['a', 'b'])
  })

  it('sends age rating flags as booleans and levels as strings', () => {
    expect(answerValue('true')).toBe(true)
    expect(answerValue('false')).toBe(false)
    expect(answerValue('NONE')).toBe('NONE')
  })

  it('tells production apart from the test tracks', () => {
    expect(isProduction('Production')).toBe(true)
    expect(isProduction('beta')).toBe(false)
  })
})

describe('media drafts', () => {
  const items = [
    { Id: 'b', Position: 2 },
    { Id: 'a', Position: 1 },
  ]

  it('starts in position order and moves one step at a time', () => {
    const draft = emptyDraft(items)

    expect(draft.order).toEqual(['a', 'b'])
    expect(move(draft.order, 'b', -1)).toEqual(['b', 'a'])
    expect(move(draft.order, 'a', -1)).toEqual(['a', 'b'])
  })

  it('counts a reorder, removals and new files as changes', () => {
    const draft = { order: ['b', 'a'], deletes: ['a'], assets: [] }

    expect(draftChangeCount(draft, items)).toBe(2)
    expect(draftChangeCount(emptyDraft(items), items)).toBe(0)
  })

  it('names the class state from the catalog', () => {
    const base = { ClassKey: 'x', Required: true, Applicability: RuleApplicability.Applies, Count: 0 }

    expect(classState({ ...base, Fill: MediaSlotFill.Fallback, FallbackFromClassKey: 'iphone-69' }, p2en).label).toBe(
      interpolate(p2en.media.stateFallback, { key: 'iphone-69' }),
    )
    expect(classState({ ...base, Fill: MediaSlotFill.None }, p2en)).toEqual({ label: p2en.media.stateMissing, tone: 'danger' })
    expect(classState({ ...base, Required: false, Fill: MediaSlotFill.None }, p2en).label).toBe(p2en.media.stateOptional)
    expect(classState({ ...base, Fill: MediaSlotFill.None, Applicability: RuleApplicability.DoesNotApply }, p2en).label).toBe(
      p2en.media.notApplicable,
    )
  })
})

describe('saved answer documents', () => {
  it('generates the Data safety CSV as the API does, quoting where needed', () => {
    const csv = dataSafetyCsv([
      { QuestionId: 'PSL_DATA_COLLECTION', ResponseId: null, ResponseValue: 'true', Label: 'Collects, "any" data' },
    ])

    expect(csv).toBe(
      'Question ID (machine readable),Response ID (machine readable),Response value,Answer requirement,Human-friendly question label\r\n' +
        'PSL_DATA_COLLECTION,,true,,"Collects, ""any"" data"\r\n',
    )
  })

  it('reads back the CSV exported from Play Console', () => {
    const csv = dataSafetyCsv([
      { QuestionId: 'Q1', ResponseId: 'R1', ResponseValue: 'true', Label: 'a, b' },
      { QuestionId: 'Q2', ResponseId: null, ResponseValue: 'text\nmore', Label: null },
    ])

    expect(importDataSafetyCsv(csv)).toEqual([
      { QuestionId: 'Q1', ResponseId: 'R1', ResponseValue: 'true', Label: 'a, b' },
      { QuestionId: 'Q2', ResponseId: null, ResponseValue: 'text\nmore', Label: null },
    ])
    expect(importDataSafetyCsv('a,b\n1,2')).toBeNull()
    expect(parseCsv('"x""y",z')).toEqual([['x"y', 'z']])
  })

  it('reads answer documents defensively', () => {
    expect(readPrivacy(null)).toEqual({ CollectsData: false, PrivacyPolicyUrl: null, DataTypes: [] })
    expect(readContentRating({ Answers: { Violence: 'NO', Count: 3 } }).Answers).toEqual({ Violence: 'NO', Count: '3' })
  })
})
