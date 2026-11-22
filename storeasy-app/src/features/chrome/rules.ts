import type { BadgeTone } from '@/components/ui/Badge'
import { ChromeItemState } from './types'

// The Chrome Web Store rules the screens check before the API does: extension and publisher ids, manifest versions (the
// same rules as the API's ChromeVersions), the credential fields, and the review states.

const ITEM_ID = /^[a-p]{32}$/
const PUBLISHER_ID = /^[A-Za-z0-9_-]{1,100}$/

/** Whether text is an extension id (32 letters from a to p). */
export function isItemId(text: string): boolean {
  return ITEM_ID.test(text.trim())
}

/** The parts of a manifest version, or null: one to four dot separated integers from 0 to 65535, no leading zeros. */
export function parseVersion(version: string | null | undefined): number[] | null {
  if (!version) {
    return null
  }
  const parts = version.split('.')
  if (parts.length < 1 || parts.length > 4) {
    return null
  }
  const values: number[] = []
  for (const part of parts) {
    if (!/^\d{1,5}$/.test(part) || (part.length > 1 && part.startsWith('0'))) {
      return null
    }
    const value = Number(part)
    if (value > 65535) {
      return null
    }
    values.push(value)
  }
  return values
}

/** Compares two manifest versions part by part (missing parts are zero); an invalid one sorts first. */
export function compareVersions(left: string | null | undefined, right: string | null | undefined): number {
  const a = parseVersion(left)
  const b = parseVersion(right)
  if (!a || !b) {
    return (a ? 1 : 0) - (b ? 1 : 0)
  }
  for (let i = 0; i < 4; i++) {
    const diff = (a[i] ?? 0) - (b[i] ?? 0)
    if (diff !== 0) {
      return diff
    }
  }
  return 0
}

export type ChromeAuth = 'oauth' | 'serviceAccount'

export interface ChromeCredentialFields {
  publisherId: string
  auth: ChromeAuth
  serviceAccountKey: string
  clientId: string
  clientSecret: string
  refreshToken: string
  itemId: string
}

export const EMPTY_CHROME_CREDENTIALS: ChromeCredentialFields = {
  publisherId: '',
  auth: 'serviceAccount',
  serviceAccountKey: '',
  clientId: '',
  clientSecret: '',
  refreshToken: '',
  itemId: '',
}

export type ChromeCredentialProblem = 'publisherId' | 'serviceAccountKey' | 'clientId' | 'clientSecret' | 'refreshToken' | 'itemId'

/** The fields the API would refuse (ChromeCredentialParser). */
export function credentialProblems(fields: ChromeCredentialFields): ChromeCredentialProblem[] {
  const problems: ChromeCredentialProblem[] = []
  if (!PUBLISHER_ID.test(fields.publisherId.trim())) {
    problems.push('publisherId')
  }
  if (fields.auth === 'serviceAccount') {
    if (!serviceAccountLooksValid(fields.serviceAccountKey)) {
      problems.push('serviceAccountKey')
    }
  } else {
    if (!fields.clientId.trim()) {
      problems.push('clientId')
    }
    if (!fields.clientSecret.trim()) {
      problems.push('clientSecret')
    }
    if (!fields.refreshToken.trim()) {
      problems.push('refreshToken')
    }
  }
  if (fields.itemId.trim() && !isItemId(fields.itemId)) {
    problems.push('itemId')
  }
  return problems
}

function serviceAccountLooksValid(text: string): boolean {
  try {
    const key = JSON.parse(text) as Record<string, unknown>
    return key.type === 'service_account' && typeof key.client_email === 'string' && typeof key.private_key === 'string'
  } catch {
    return false
  }
}

/** The secret JSON the API takes for a Chrome Web Store connection. */
export function chromeSecret(fields: ChromeCredentialFields): string {
  const itemId = fields.itemId.trim() || null
  return fields.auth === 'serviceAccount'
    ? JSON.stringify({ publisherId: fields.publisherId.trim(), serviceAccountKey: fields.serviceAccountKey.trim(), itemId })
    : JSON.stringify({
        publisherId: fields.publisherId.trim(),
        clientId: fields.clientId.trim(),
        clientSecret: fields.clientSecret.trim(),
        refreshToken: fields.refreshToken.trim(),
        itemId,
      })
}

export type ChromeStateKey = 'none' | 'pendingReview' | 'staged' | 'published' | 'publishedToTesters' | 'rejected' | 'cancelled'

const STATE_KEYS: Record<ChromeItemState, ChromeStateKey> = {
  [ChromeItemState.None]: 'none',
  [ChromeItemState.PendingReview]: 'pendingReview',
  [ChromeItemState.Staged]: 'staged',
  [ChromeItemState.Published]: 'published',
  [ChromeItemState.PublishedToTesters]: 'publishedToTesters',
  [ChromeItemState.Rejected]: 'rejected',
  [ChromeItemState.Cancelled]: 'cancelled',
}

export function stateKey(state: ChromeItemState): ChromeStateKey {
  return STATE_KEYS[state] ?? 'none'
}

export function stateTone(state: ChromeItemState): BadgeTone {
  switch (state) {
    case ChromeItemState.Published:
    case ChromeItemState.PublishedToTesters:
      return 'success'
    case ChromeItemState.PendingReview:
    case ChromeItemState.Staged:
      return 'warning'
    case ChromeItemState.Rejected:
      return 'danger'
    default:
      return 'neutral'
  }
}

/** Partial rollout needs more than this many seven day users (setPublishedDeployPercentage). */
export const PARTIAL_ROLLOUT_MIN_USERS = 10000
