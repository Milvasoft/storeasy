import type { LocalizedText, ReviewListDto, SavedReplyDto } from './types'

// The stores count a reply in user-perceived characters (ReviewRules.Length: StringInfo text elements), so an emoji with
// a skin tone is one; Intl.Segmenter counts the same graphemes, code points are the fallback.
// Intl.Segmenter is ES2022, past the project's lib setting; typed here for the one call.
type Segmenter = new (locale: undefined, options: { granularity: 'grapheme' }) => { segment: (value: string) => Iterable<unknown> }

export function graphemeCount(value: string): number {
  const segmenter = (Intl as unknown as { Segmenter?: Segmenter }).Segmenter
  if (typeof segmenter === 'function') {
    return Array.from(new segmenter(undefined, { granularity: 'grapheme' }).segment(value)).length
  }
  return [...value].length
}

// SavedReplies.Variables: Turkish and English names of the three values a saved reply may use.
export const REPLY_VARIABLES = ['ad', 'uygulama', 'sürüm'] as const
const ALIASES: Record<string, 'author' | 'app' | 'version'> = {
  ad: 'author',
  name: 'author',
  uygulama: 'app',
  app: 'app',
  ['sürüm']: 'version',
  version: 'version',
}

const VARIABLE = /\{([^{}\s]+)\}/g

/** Variables the API refuses (SavedReplyVariableUnknown), e.g. {isim}. */
export function unknownVariables(text: string): string[] {
  return [...new Set([...text.matchAll(VARIABLE)].map((match) => match[1]).filter((name) => !(name in ALIASES)))]
}

/** Fills {ad}, {uygulama} and {sürüm} (and the English names) with the review's values; unknown ones stay as written. */
export function fillVariables(text: string, values: { author?: string | null; app?: string | null; version?: string | null }): string {
  return text.replace(VARIABLE, (whole, name: string) => {
    const key = ALIASES[name]
    const value = key ? values[key] : null
    return value ?? whole
  })
}

function language(locale: string | null | undefined): string {
  return (locale ?? '').split('-')[0].toLowerCase()
}

/** The variant for a locale: the same locale, else the same language, else the first one. */
export function variantFor(reply: SavedReplyDto, locale: string | null | undefined): LocalizedText | null {
  const variants = reply.Variants ?? []
  return (
    variants.find((variant) => variant.Locale === locale) ??
    variants.find((variant) => language(variant.Locale) === language(locale) && !!locale) ??
    variants[0] ??
    null
  )
}

/** Saved replies offered for a review: the ones for its app (or every app), its language first. */
export function repliesFor(replies: SavedReplyDto[], review: ReviewListDto): SavedReplyDto[] {
  const forApp = replies.filter((reply) => !reply.AppIds || reply.AppIds.length === 0 || reply.AppIds.includes(review.AppId))
  const speaks = (reply: SavedReplyDto) => (reply.Variants ?? []).some((variant) => language(variant.Locale) === language(review.Locale))
  return [...forApp.filter(speaks), ...forApp.filter((reply) => !speaks(reply))]
}
