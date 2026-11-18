// The backend serializes JSON in camelCase while the DTO types are PascalCase: every response object key (HTTP and the
// live hub alike) is normalized here. Requests are sent PascalCase; ASP.NET model binding is case-insensitive.
export function pascalizeKeys(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(pascalizeKeys)
  }
  if (value && typeof value === 'object' && (value as object).constructor === Object) {
    const out: Record<string, unknown> = {}
    for (const [key, val] of Object.entries(value as Record<string, unknown>)) {
      const nextKey = key.length ? key[0].toUpperCase() + key.slice(1) : key
      out[nextKey] = pascalizeKeys(val)
    }
    return out
  }
  return value
}
