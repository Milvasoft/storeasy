// The organization slug suggestion, mirroring the API's rule: lower case Latin letters, digits and single hyphens.
// Turkish letters fold to their Latin base ("Mavi Atölye" -> "mavi-atolye").
const FOLD: Record<string, string> = { ı: 'i', İ: 'i', ş: 's', Ş: 's', ğ: 'g', Ğ: 'g', ü: 'u', Ü: 'u', ö: 'o', Ö: 'o', ç: 'c', Ç: 'c' }

export const MIN_SLUG_LENGTH = 3

export function toSlug(value: string): string {
  const folded = value.replace(/[ıİşŞğĞüÜöÖçÇ]/g, (char) => FOLD[char] ?? char)
  return folded
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

// While typing: the same rule, but a trailing separator stays so the user can type "mavi-atolye" letter by letter.
export function toSlugDraft(value: string): string {
  const slug = toSlug(value)
  return slug && /[\s\-_.]$/.test(value) ? `${slug}-` : slug
}

export function isValidSlug(value: string): boolean {
  return value.length >= MIN_SLUG_LENGTH && /^[a-z0-9]+(-[a-z0-9]+)*$/.test(value)
}
