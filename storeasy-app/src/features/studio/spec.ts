import type { MockupCaptionDto, MockupLayerSpec, MockupSlotDto, MockupSpec, MockupTextStyleSpec, MockupWarningDto } from './types'

// Pure helpers of the template document: no React, no API.

export const SPEC_SCHEMA_VERSION = 1
/** Soft counters of the caption cells (design 12.4); the server's measured line count is the real check. */
export const TITLE_SOFT_MAX = 30
export const SUBTITLE_SOFT_MAX = 40
/** render-preview draws at most four slots per call (RenderMockupPreviewQuery.MaxSlots). */
export const PREVIEW_MAX_SLOTS = 4
/** Slots drawn side by side in the editor's canvas. */
export const PREVIEW_PAGE = 3
export const PREVIEW_DEBOUNCE_MS = 400
export const PREVIEW_MAX_WIDTH = 360
export const MAX_SLOTS = 10

export const DEFAULT_TITLE_STYLE: MockupTextStyleSpec = {
  Size: 0.085,
  Weight: 700,
  MaxLines: 2,
  AutoFit: true,
  MinSize: 0.055,
  LineHeight: 1.15,
  Color: null,
}

export const DEFAULT_SUBTITLE_STYLE: MockupTextStyleSpec = {
  Size: 0.048,
  Weight: 400,
  MaxLines: 2,
  AutoFit: true,
  MinSize: 0.036,
  LineHeight: 1.25,
  Color: null,
}

/** The schema's example, used where a stored spec lacks a part (a template saved by an older client). */
export function defaultSpec(): MockupSpec {
  return {
    SchemaVersion: SPEC_SCHEMA_VERSION,
    Canvas: { ClassKey: null },
    Background: { Kind: 'solid', Colors: ['#4636FF', '#1B1464'], Angle: 90, AssetId: null },
    Frame: {
      Kind: 'auto',
      Cutout: 'island',
      Color: '#1C1C22',
      CornerRadius: null,
      Bezel: null,
      Scale: 0.78,
      OffsetX: 0,
      OffsetY: 0.27,
      Rotation: 0,
      Shadow: true,
    },
    Text: {
      Font: 'inter',
      Color: '#FFFFFF',
      Position: 'top',
      Alignment: 'center',
      Margin: 0.08,
      Gap: 0.025,
      Uppercase: false,
      Title: { ...DEFAULT_TITLE_STYLE },
      Subtitle: { ...DEFAULT_SUBTITLE_STYLE },
    },
    Layers: [],
  }
}

// pascalizeKeys capitalizes every key of a response, the badge texts' locale keys included ("tr" -> "Tr"). Locale
// language subtags are lower case (BCP 47), so the first letter goes back down; "*" is untouched.
function restoreLocaleKeys(texts: Record<string, string> | null | undefined): Record<string, string> | null {
  if (!texts) {
    return null
  }
  return Object.fromEntries(Object.entries(texts).map(([key, value]) => [key.charAt(0).toLowerCase() + key.slice(1), value]))
}

/** A spec as the editor works on it: every part present, badge text keys restored. */
export function normalizeSpec(spec: MockupSpec | null | undefined): MockupSpec {
  const base = defaultSpec()
  if (!spec) {
    return base
  }
  const text = spec.Text ? { ...base.Text!, ...spec.Text } : base.Text!
  return {
    SchemaVersion: spec.SchemaVersion || SPEC_SCHEMA_VERSION,
    Canvas: { ...base.Canvas, ...spec.Canvas },
    Background: spec.Background ? { ...base.Background!, ...spec.Background } : base.Background,
    Frame: spec.Frame ? { ...base.Frame!, ...spec.Frame } : base.Frame,
    Text: {
      ...text,
      Title: { ...DEFAULT_TITLE_STYLE, ...text.Title },
      Subtitle: { ...DEFAULT_SUBTITLE_STYLE, ...text.Subtitle },
    },
    Layers: (spec.Layers ?? []).map((layer) => ({ ...layer, Texts: restoreLocaleKeys(layer.Texts) })),
  }
}

export function newLayer(type: 'shape' | 'badge'): MockupLayerSpec {
  if (type === 'badge') {
    return {
      Type: 'badge',
      Shape: null,
      X: 0.8,
      Y: 0.3,
      Width: 0,
      Height: 0,
      Radius: 0,
      Color: '#FFD25A',
      Opacity: 1,
      Above: true,
      Texts: { '*': 'NEW' },
      TextSize: 0.04,
      TextColor: '#1B1464',
    }
  }
  return {
    Type: 'shape',
    Shape: 'round-rect',
    X: 0.05,
    Y: 0.42,
    Width: 0.9,
    Height: 0.42,
    Radius: 0.04,
    Color: '#4636FF',
    Opacity: 0.14,
    Above: false,
    Texts: null,
    TextSize: 0,
    TextColor: null,
  }
}

/** The slots of a preview page: the page that holds `selected`, at most PREVIEW_PAGE wide. */
export function previewPage(selected: number, slotCount: number, size = PREVIEW_PAGE): number[] {
  const width = Math.min(size, PREVIEW_MAX_SLOTS)
  const start = Math.floor((Math.max(1, selected) - 1) / width) * width + 1
  const orders: number[] = []
  for (let order = start; order < start + width && order <= slotCount; order += 1) {
    orders.push(order)
  }
  return orders
}

/** The raw screenshot of a slot for a family and locale: the locale's own, else the one for every locale. */
export function slotAsset(slots: MockupSlotDto[], order: number, family: number, locale: string | null): MockupSlotDto | null {
  const own = locale ? slots.find((slot) => slot.Order === order && slot.Family === family && slot.Locale === locale) : undefined
  return own ?? slots.find((slot) => slot.Order === order && slot.Family === family && !slot.Locale) ?? null
}

/** Puts (or replaces) the screenshot of one slot, family and locale. */
export function setSlotAsset(slots: MockupSlotDto[], next: MockupSlotDto): MockupSlotDto[] {
  const locale = next.Locale ?? null
  const rest = slots.filter((slot) => !(slot.Order === next.Order && slot.Family === next.Family && (slot.Locale ?? null) === locale))
  return [...rest, { ...next, Locale: locale }].sort((a, b) => a.Order - b.Order || a.Family - b.Family)
}

export function removeSlotAsset(slots: MockupSlotDto[], order: number, family: number, locale: string | null): MockupSlotDto[] {
  return slots.filter((slot) => !(slot.Order === order && slot.Family === family && (slot.Locale ?? null) === locale))
}

/** Drops the screenshots of slots past a smaller slot count. */
export function trimSlots(slots: MockupSlotDto[], slotCount: number): MockupSlotDto[] {
  return slots.filter((slot) => slot.Order <= slotCount)
}

// ---- Captions grid ----

export type CaptionField = 'title' | 'subtitle'

export function cellKey(locale: string, slotOrder: number): string {
  return `${locale}|${slotOrder}`
}

export interface CaptionCell {
  title: string
  subtitle: string
}

export function captionMap(captions: MockupCaptionDto[]): Record<string, CaptionCell> {
  const map: Record<string, CaptionCell> = {}
  for (const caption of captions) {
    map[cellKey(caption.Locale, caption.SlotOrder)] = { title: caption.Title ?? '', subtitle: caption.Subtitle ?? '' }
  }
  return map
}

/** The cells that differ from the saved captions, as PUT captions items (an empty cell removes its row). */
export function changedCaptions(saved: Record<string, CaptionCell>, draft: Record<string, CaptionCell>): MockupCaptionDto[] {
  const items: MockupCaptionDto[] = []
  for (const [key, cell] of Object.entries(draft)) {
    const before = saved[key] ?? { title: '', subtitle: '' }
    if (before.title === cell.title && before.subtitle === cell.subtitle) {
      continue
    }
    const [locale, order] = key.split('|')
    items.push({ Locale: locale, SlotOrder: Number(order), Title: cell.title, Subtitle: cell.subtitle })
  }
  return items.sort((a, b) => a.Locale.localeCompare(b.Locale) || a.SlotOrder - b.SlotOrder)
}

/** Copies one locale's captions to another: only the empty cells, or every cell. */
export function copyCaptions(
  draft: Record<string, CaptionCell>,
  from: string,
  to: string,
  slotCount: number,
  overwrite: boolean,
): Record<string, CaptionCell> {
  const next = { ...draft }
  for (let order = 1; order <= slotCount; order += 1) {
    const source = draft[cellKey(from, order)]
    if (!source) {
      continue
    }
    const target = next[cellKey(to, order)] ?? { title: '', subtitle: '' }
    next[cellKey(to, order)] = {
      title: overwrite || !target.title ? source.title : target.title,
      subtitle: overwrite || !target.subtitle ? source.subtitle : target.subtitle,
    }
  }
  return next
}

/** Caption warnings of one cell (overflow and fallback), from PUT captions or a preview. */
export function cellWarnings(warnings: MockupWarningDto[], locale: string, slotOrder: number): MockupWarningDto[] {
  return warnings.filter((warning) => warning.Locale === locale && warning.SlotOrder === slotOrder)
}

/** Whole numbers of percent for the sliders (0.78 -> 78). */
export function toPercent(value: number | null | undefined): number {
  return Math.round((value ?? 0) * 1000) / 10
}

export function fromPercent(value: number): number {
  return Math.round(value * 10) / 1000
}

export function sameJson(a: unknown, b: unknown): boolean {
  return JSON.stringify(a) === JSON.stringify(b)
}

/** Splits a typed list of locales ("tr, en-US de-DE") into unique tags in order. */
export function parseLocales(text: string): string[] {
  return [
    ...new Set(
      text
        .split(/[\s,;]+/)
        .map((value) => value.trim())
        .filter(Boolean),
    ),
  ]
}

const LOCALE_PATTERN = /^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$/

export function isLocaleTag(value: string): boolean {
  return LOCALE_PATTERN.test(value)
}
