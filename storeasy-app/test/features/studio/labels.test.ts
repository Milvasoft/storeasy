import { describe, expect, it } from 'vitest'
import { studioTr } from '@/features/studio/i18n'
import { classLabel, familyLabel, frameKindLabel, requirementView, warningText } from '@/features/studio/labels'
import { MockupDeviceFamily, MockupFrameKind, MockupRequirement } from '@/features/studio/types'

describe('studio labels', () => {
  it('names every frame kind, device family and requirement', () => {
    expect(Object.values(MockupFrameKind).map((kind) => frameKindLabel(kind, studioTr))).toEqual([
      'Çerçevesiz',
      'Telefon',
      'Tablet',
      'Katlanır: dış ekran',
      'Katlanır: iç ekran',
      'Masaüstü pencere',
    ])
    expect(Object.values(MockupDeviceFamily).map((family) => familyLabel(family, studioTr))).toEqual(['Telefon', 'Tablet', 'Masaüstü'])
    expect(Object.values(MockupRequirement).map((status) => requirementView(status, studioTr))).toEqual([
      { label: 'Zorunlu', tone: 'danger' },
      { label: 'Önerilen', tone: 'info' },
      { label: 'İsteğe bağlı', tone: 'neutral' },
    ])
  })

  it('uses the catalog label of the UI language, English when there is no Turkish one', () => {
    expect(classLabel({ Label: 'Phone', LabelTr: 'Telefon' }, 'tr')).toBe('Telefon')
    expect(classLabel({ Label: 'Phone', LabelTr: null }, 'tr')).toBe('Phone')
    expect(classLabel({ Label: 'Phone', LabelTr: 'Telefon' }, 'en')).toBe('Phone')
  })

  it('writes known warnings from the dictionary and unknown ones from the server', () => {
    expect(warningText({ Code: 'CaptionOverflow', Field: 'subtitle', Lines: 3, MaxLines: 2 }, studioTr)).toBe(
      'Alt başlık 3 satır tutuyor; stil 2 satıra izin veriyor.',
    )
    expect(warningText({ Code: 'CaptionOverflow' }, studioTr)).toBe('Başlık ? satır tutuyor; stil ? satıra izin veriyor.')
    expect(warningText({ Code: 'NewCode', Message: 'Server detail.' }, studioTr)).toBe('Server detail.')
    expect(warningText({ Code: 'NewCode' }, studioTr)).toBe('NewCode')
  })
})
