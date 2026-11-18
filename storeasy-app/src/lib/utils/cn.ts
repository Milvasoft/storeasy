// Joins the truthy class names; a conflict is resolved with a `!` prefixed class, not a merge library.
export function cn(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(' ')
}
