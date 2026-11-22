import type { BadgeTone } from '@/components/ui/Badge'
import { FirefoxAddonStatus, FirefoxFileStatus } from './types'

// The Firefox Add-ons rules the screens check before the API does: the AMO API key (JWT issuer and secret), add-on
// references, versions (the same rules as the API's FirefoxVersions) and the review states.

const ISSUER = /^user:\d{1,12}:\d{1,12}$/
const SECRET_MIN_LENGTH = 32
const EMAIL_ID = /^[a-zA-Z0-9\-._]*@[a-zA-Z0-9\-._]+$/
const UUID_ID = /^\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}$/
const SLUG = /^[a-zA-Z0-9\-_~]+$/
const VERSION = /^(0|[1-9][0-9]{0,8})(\.(0|[1-9][0-9]{0,8})){0,3}$/

export interface FirefoxCredentialFields {
  issuer: string
  secret: string
}

export const EMPTY_FIREFOX_CREDENTIALS: FirefoxCredentialFields = { issuer: '', secret: '' }

export type FirefoxCredentialProblem = 'issuer' | 'secret'

/** What is wrong with the API key fields (empty when it can be sent). */
export function credentialProblems(fields: FirefoxCredentialFields): FirefoxCredentialProblem[] {
  const problems: FirefoxCredentialProblem[] = []
  if (!ISSUER.test(fields.issuer.trim())) {
    problems.push('issuer')
  }
  const secret = fields.secret.trim()
  if (secret.length < SECRET_MIN_LENGTH || /\s/.test(secret)) {
    problems.push('secret')
  }
  return problems
}

/** The secret JSON the API parses (FirefoxCredentialParser). */
export function firefoxSecret(fields: FirefoxCredentialFields): string {
  return JSON.stringify({ issuer: fields.issuer.trim(), secret: fields.secret.trim() })
}

/** Whether text is an add-on guid: an e-mail like id or a braced UUID. */
export function isGuid(text: string): boolean {
  const value = text.trim()
  return value.length > 0 && value.length <= 255 && (EMAIL_ID.test(value) || UUID_ID.test(value))
}

/** Whether text names an add-on: a numeric id, a guid or a slug. */
export function isAddonReference(text: string): boolean {
  const value = text.trim()
  return value.length > 0 && value.length <= 255 && (isGuid(value) || SLUG.test(value))
}

/** Whether a version has the format AMO takes for a new upload. */
export function isVersion(version: string | null | undefined): boolean {
  return !!version && VERSION.test(version)
}

function part(text: string): [number, string] {
  const match = /^(\d{0,18})(.*)$/.exec(text) ?? ['', '', text]
  return [match[1] ? Number(match[1]) : 0, match[2]]
}

/** Compares two versions like the API: number parts as numbers, a pre-release suffix below the plain number. */
export function compareVersions(left: string | null | undefined, right: string | null | undefined): number {
  const a = (left ?? '').split('.')
  const b = (right ?? '').split('.')
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    const [numberA, restA] = part(a[i] ?? '0')
    const [numberB, restB] = part(b[i] ?? '0')
    if (numberA !== numberB) {
      return numberA - numberB
    }
    if (restA !== restB) {
      if (!restA) {
        return 1
      }
      if (!restB) {
        return -1
      }
      return restA < restB ? -1 : 1
    }
  }
  return 0
}

export type AddonStatusKey = 'none' | 'public' | 'nominated' | 'incomplete' | 'disabled' | 'rejected' | 'deleted'

export function addonStatusKey(status: FirefoxAddonStatus): AddonStatusKey {
  switch (status) {
    case FirefoxAddonStatus.Public:
      return 'public'
    case FirefoxAddonStatus.Nominated:
      return 'nominated'
    case FirefoxAddonStatus.Incomplete:
      return 'incomplete'
    case FirefoxAddonStatus.Disabled:
      return 'disabled'
    case FirefoxAddonStatus.Rejected:
      return 'rejected'
    case FirefoxAddonStatus.Deleted:
      return 'deleted'
    default:
      return 'none'
  }
}

export function addonStatusTone(status: FirefoxAddonStatus): BadgeTone {
  switch (status) {
    case FirefoxAddonStatus.Public:
      return 'success'
    case FirefoxAddonStatus.Nominated:
      return 'info'
    case FirefoxAddonStatus.Incomplete:
      return 'warning'
    case FirefoxAddonStatus.None:
      return 'neutral'
    default:
      return 'danger'
  }
}

export type FileStatusKey = 'none' | 'unreviewed' | 'public' | 'disabled'

export function fileStatusKey(status: FirefoxFileStatus): FileStatusKey {
  switch (status) {
    case FirefoxFileStatus.Unreviewed:
      return 'unreviewed'
    case FirefoxFileStatus.Public:
      return 'public'
    case FirefoxFileStatus.Disabled:
      return 'disabled'
    default:
      return 'none'
  }
}

export function fileStatusTone(status: FirefoxFileStatus): BadgeTone {
  switch (status) {
    case FirefoxFileStatus.Public:
      return 'success'
    case FirefoxFileStatus.Unreviewed:
      return 'info'
    case FirefoxFileStatus.Disabled:
      return 'danger'
    default:
      return 'neutral'
  }
}

/** Asset ids typed as a comma or space separated list; invalid parts are left out. */
export function parseIds(text: string): number[] {
  return text
    .split(/[\s,]+/)
    .map((part) => Number(part))
    .filter((id) => Number.isInteger(id) && id > 0)
}
