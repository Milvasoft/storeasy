import { PASSWORD_POLICY } from '@/lib/api/textLimits'
import type { Dictionary } from '@/lib/i18n/en'

// Client checks shared by every form, run before submit and shown under the field (02 section 9.5). The API checks again.

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/

export function emailError(value: string, t: Dictionary): string | null {
  const email = value.trim()
  if (!email) {
    return t.validation.emailRequired
  }
  return EMAIL.test(email) ? null : t.validation.emailInvalid
}

export function requiredError(value: string, message: string): string | null {
  return value.trim() ? null : message
}

export type PasswordRule = 'length' | 'case' | 'digit' | 'symbol'

// Which policy rules a password meets; the form draws the list live (design 5.10).
export function passwordRules(value: string): Record<PasswordRule, boolean> {
  return {
    length: value.length >= PASSWORD_POLICY.minLength,
    case: (!PASSWORD_POLICY.requireUpper || /\p{Lu}/u.test(value)) && (!PASSWORD_POLICY.requireLower || /\p{Ll}/u.test(value)),
    digit: !PASSWORD_POLICY.requireDigit || /\d/.test(value),
    symbol: !PASSWORD_POLICY.requireSymbol || /[^\p{L}\d]/u.test(value),
  }
}

export function passwordError(value: string, t: Dictionary): string | null {
  if (!value) {
    return t.validation.passwordRequired
  }
  return Object.values(passwordRules(value)).every(Boolean) ? null : t.validation.passwordPolicy
}
