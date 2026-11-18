// Counting listing texts the way the stores do: characters as Unicode code points (an emoji is one), bytes as UTF-8
// (Apple's keywords limit is 100 bytes, so a Turkish "ğ" counts two).

const encoder = new TextEncoder()

export function charCount(value: string): number {
  return [...value].length
}

export function byteCount(value: string): number {
  return encoder.encode(value).length
}

/** Whether a value breaks the field's catalog limits (characters and/or bytes). */
export function overLimit(value: string, maxLength?: number | null, maxBytes?: number | null): boolean {
  return (!!maxLength && charCount(value) > maxLength) || (!!maxBytes && byteCount(value) > maxBytes)
}

/** "a, b ,,c" -> ['a', 'b', 'c']. */
export function splitList(value: string): string[] {
  return value
    .split(',')
    .map((part) => part.trim())
    .filter((part) => part.length > 0)
}
