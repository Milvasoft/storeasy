import type { BadgeTone } from '@/components/ui/Badge'
import type { Lang } from '@/lib/i18n/lang'
import type { StudioDictionary } from './i18n'
import { MockupDeviceFamily, MockupFrameKind, MockupRequirement, type MockupClassDto, type MockupFrameDto } from './types'

export const FAMILIES: readonly MockupDeviceFamily[] = [MockupDeviceFamily.Phone, MockupDeviceFamily.Tablet, MockupDeviceFamily.Desktop]

export function familyLabel(family: MockupDeviceFamily, s: StudioDictionary): string {
  switch (family) {
    case MockupDeviceFamily.Phone:
      return s.enums.family.phone
    case MockupDeviceFamily.Tablet:
      return s.enums.family.tablet
    default:
      return s.enums.family.desktop
  }
}

export function frameKindLabel(kind: MockupFrameKind, s: StudioDictionary): string {
  switch (kind) {
    case MockupFrameKind.None:
      return s.enums.frame.none
    case MockupFrameKind.Phone:
      return s.enums.frame.phone
    case MockupFrameKind.Tablet:
      return s.enums.frame.tablet
    case MockupFrameKind.FoldableOuter:
      return s.enums.frame.foldableOuter
    case MockupFrameKind.FoldableInner:
      return s.enums.frame.foldableInner
    default:
      return s.enums.frame.desktop
  }
}

export function requirementView(status: MockupRequirement, s: StudioDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case MockupRequirement.Required:
      return { label: s.enums.requirement.required, tone: 'danger' }
    case MockupRequirement.Recommended:
      return { label: s.enums.requirement.recommended, tone: 'info' }
    default:
      return { label: s.enums.requirement.optional, tone: 'neutral' }
  }
}

/** The catalog's own label in the UI language (the API sends English and Turkish). */
export function classLabel(item: { Label: string; LabelTr?: string | null }, lang: Lang): string {
  return (lang === 'tr' ? item.LabelTr : null) || item.Label
}

/** Every class the studio exports, once, in the frames' order. */
export function classesOf(frames: MockupFrameDto[]): MockupClassDto[] {
  const seen = new Set<string>()
  const out: MockupClassDto[] = []
  for (const frame of frames) {
    for (const item of frame.Classes ?? []) {
      if (!seen.has(item.ClassKey)) {
        seen.add(item.ClassKey)
        out.push(item)
      }
    }
  }
  return out
}

/** The text of a render or export warning: the dictionary's sentence for the known codes, else the server's detail. */
export function warningText(
  warning: { Code: string; Message?: string | null; Lines?: number | null; MaxLines?: number | null; Field?: string | null },
  s: StudioDictionary,
): string {
  const known = s.warnings[warning.Code as keyof StudioDictionary['warnings']]
  if (!known) {
    return warning.Message || warning.Code
  }
  return known
    .replace('{lines}', String(warning.Lines ?? '?'))
    .replace('{maxLines}', String(warning.MaxLines ?? '?'))
    .replace('{field}', warning.Field === 'subtitle' ? s.captions.subtitle : s.captions.title)
}
