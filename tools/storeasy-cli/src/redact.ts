// SPDX-License-Identifier: AGPL-3.0-only

// Log and error masking. Same rules as store-ops `redact()` and plan section 8, plus storeasy's own token prefixes.
const PATTERNS: Array<[RegExp, string]> = [
  // storeasy tokens: personal (sea_), CI (sci_), upload (sut_), webhook secrets (whsec_)
  [/\b(sea|sci|sut)_[A-Za-z0-9_-]{6,}/g, '$1_[redacted]'],
  [/\bwhsec_[A-Za-z0-9_-]{6,}/g, 'whsec_[redacted]'],
  // Bearer tokens and JWTs (OIDC tokens are JWTs)
  [/Bearer\s+[A-Za-z0-9._~+/=-]+/gi, 'Bearer [redacted]'],
  [/eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]*/g, '[redacted-jwt]'],
  // PEM blocks
  [/-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----/g, '[redacted-private-key]'],
  // Google OAuth access tokens
  [/ya29\.[A-Za-z0-9._-]+/g, '[redacted-token]'],
  // access_token in query strings or bodies
  [/(access_token["'=:\s]+)[^\s"'&,}]+/gi, '$1[redacted]'],
  // Signed URLs: drop query strings that carry signatures
  [/(https?:\/\/[^\s"'?]+)\?[^\s"']*(?:Signature|signature|X-Amz|sig=|token=|se=)[^\s"']*/g, '$1?[redacted-query]'],
]

// Exact secret values seen at run time (a token from the environment, an exchanged upload token). Matched before the
// patterns, so a value of any shape is masked.
const secrets = new Set<string>()

/** Registers a value that must never be printed. Short values are ignored (they would mask ordinary text). */
export function registerSecret(value: string | undefined | null): void {
  if (value && value.length >= 8) {
    secrets.add(value)
  }
}

/** For tests. */
export function clearSecrets(): void {
  secrets.clear()
}

/** Removes anything that looks like a credential from a string. */
export function redact(text: string): string {
  let out = text
  for (const s of secrets) {
    out = out.split(s).join('[redacted]')
  }
  for (const [re, rep] of PATTERNS) {
    out = out.replace(re, rep)
  }
  return out
}

/** A URL without its query string (presigned URLs carry the signature there). */
export function stripQuery(url: string): string {
  const i = url.indexOf('?')
  return i === -1 ? url : url.slice(0, i)
}

/** Display form of a token: its prefix and a few characters, never the value. */
export function tokenHint(token: string): string {
  const m = /^(sea|sci|sut)_/.exec(token)
  return m ? `${m[1]}_${token.slice(4, 7)}...` : 'token ...'
}
