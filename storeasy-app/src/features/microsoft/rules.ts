import type { DateOnly, IsoDateTime } from '@/lib/api/types'
import { charCount } from '@/features/common/text'
import { parseDateOnly, toDateOnly } from '@/lib/utils/date'
import {
  MsInstallerType,
  MsProductKind,
  MsSubmissionStatus,
  type MsInstallerPackageInputDto,
  type MsListingDto,
  type MsProductDto,
  type MsSubmissionDto,
} from './types'

// The Microsoft Store rules the screens check before asking the API (storeasy-api MsListingLimits, MsSecretRules,
// MicrosoftCredentialParser, the MS validators). The API checks them again; these only spare a round trip.

// ---- Listing limits ----------------------------------------------------------------------------------------------------
export const MS_LIMITS = {
  title: 256,
  description: 10_000,
  shortDescription: 1_000,
  whatsNew: 1_500,
  featureCount: 20,
  featureLength: 200,
  searchTermCount: 7,
  searchTermLength: 30,
  desktopScreenshots: 10,
  otherScreenshots: 8,
  imageCaption: 200,
  addonTitle: 100,
  addonDescription: 200,
} as const

export type MsTextField = 'title' | 'description' | 'shortDescription' | 'whatsNew'
export type MsListField = 'features' | 'searchTerms'

export const MS_TEXT_LIMIT: Record<MsTextField, number> = {
  title: MS_LIMITS.title,
  description: MS_LIMITS.description,
  shortDescription: MS_LIMITS.shortDescription,
  whatsNew: MS_LIMITS.whatsNew,
}

export const MS_LIST_LIMIT: Record<MsListField, { count: number; length: number }> = {
  features: { count: MS_LIMITS.featureCount, length: MS_LIMITS.featureLength },
  searchTerms: { count: MS_LIMITS.searchTermCount, length: MS_LIMITS.searchTermLength },
}

/** One line per item; blank lines are dropped. */
export function linesOf(value: string): string[] {
  return value
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
}

/** Whether a list breaks its count or per item length. */
export function listOverLimit(items: string[], field: MsListField): boolean {
  const limit = MS_LIST_LIMIT[field]
  return items.length > limit.count || items.some((item) => charCount(item) > limit.length)
}

export interface MsListingDraft {
  title: string
  description: string
  shortDescription: string
  whatsNew: string
  features: string
  searchTerms: string
}

export function listingDraftOf(listing: MsListingDto | undefined): MsListingDraft {
  return {
    title: listing?.Title ?? '',
    description: listing?.Description ?? '',
    shortDescription: listing?.ShortDescription ?? '',
    whatsNew: listing?.WhatsNew ?? '',
    features: (listing?.Features ?? []).join('\n'),
    searchTerms: (listing?.SearchTerms ?? []).join('\n'),
  }
}

/** The fields of a draft that break a limit. */
export function listingProblems(draft: MsListingDraft): Array<MsTextField | MsListField> {
  const problems: Array<MsTextField | MsListField> = []
  for (const field of Object.keys(MS_TEXT_LIMIT) as MsTextField[]) {
    if (charCount(draft[field]) > MS_TEXT_LIMIT[field]) {
      problems.push(field)
    }
  }
  for (const field of Object.keys(MS_LIST_LIMIT) as MsListField[]) {
    if (listOverLimit(linesOf(draft[field]), field)) {
      problems.push(field)
    }
  }
  return problems
}

/**
 * The change of one language: only the fields that differ from the store's; a field left out (null) stays as it is.
 * Null when nothing changed. The title is read only for MSI/EXE products.
 */
export function listingChange(
  locale: string,
  draft: MsListingDraft,
  original: MsListingDraft,
  titleEditable: boolean,
): MsListingDto | null {
  const text = (field: MsTextField) => (draft[field] !== original[field] ? draft[field] : null)
  const list = (field: MsListField) => (draft[field] !== original[field] ? linesOf(draft[field]) : null)
  const change: MsListingDto = {
    Locale: locale,
    Title: titleEditable ? text('title') : null,
    Description: text('description'),
    ShortDescription: text('shortDescription'),
    WhatsNew: text('whatsNew'),
    Features: list('features'),
    SearchTerms: list('searchTerms'),
  }
  const changed = [change.Title, change.Description, change.ShortDescription, change.WhatsNew, change.Features, change.SearchTerms]
  return changed.some((value) => value !== null) ? change : null
}

// ---- Images ------------------------------------------------------------------------------------------------------------
export const DESKTOP_SCREENSHOT = 'Screenshot'
export const SCREENSHOT_TYPES = ['Screenshot', 'MobileScreenshot', 'XboxScreenshot', 'SurfaceHubScreenshot', 'HoloLensScreenshot'] as const
export const LOGO_TYPES = ['StoreLogo9x16', 'StoreLogoSquare', 'Icon', 'PromotionalArt16x9', 'PromotionalArtwork2400X1200'] as const

/** How many images of a type one language may hold: 10 desktop screenshots, 8 of every other family, one logo each. */
export function imageLimit(imageType: string): number {
  if (imageType === DESKTOP_SCREENSHOT) {
    return MS_LIMITS.desktopScreenshots
  }
  return (SCREENSHOT_TYPES as readonly string[]).includes(imageType) ? MS_LIMITS.otherScreenshots : 1
}

// ---- Installer packages (MSI/EXE) --------------------------------------------------------------------------------------
export const ARCHITECTURES = ['Neutral', 'X86', 'X64', 'Arm', 'Arm64'] as const

export type InstallerProblem = 'url' | 'parameters' | 'docUrl' | 'languages'

function isHttps(value: string): boolean {
  try {
    return new URL(value.trim()).protocol === 'https:'
  } catch {
    return false
  }
}

/** What is wrong with one installer row, as the API's MsInstallerInvalid checks it. */
export function installerProblems(input: MsInstallerPackageInputDto): InstallerProblem[] {
  const problems: InstallerProblem[] = []
  if (!isHttps(input.PackageUrl)) {
    problems.push('url')
  }
  if (!input.IsSilentInstall && !input.InstallerParameters?.trim()) {
    problems.push('parameters')
  }
  if (input.PackageType === MsInstallerType.Exe && !isHttps(input.GenericDocUrl ?? '')) {
    problems.push('docUrl')
  }
  if (input.Languages.length === 0) {
    problems.push('languages')
  }
  return problems
}

// ---- Connections -------------------------------------------------------------------------------------------------------
export const MS_SECRET_MAX_MONTHS = 24
export const MS_SECRET_WARN_DAYS = 30

const GUID = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/
const SELLER_ID = /^[0-9]{1,20}$/
const STORE_ID = /^[A-Za-z0-9]{12}$/

export interface MsCredentialFields {
  tenantId: string
  clientId: string
  clientSecret: string
  sellerId: string
}

export type MsCredentialProblem = 'tenantId' | 'clientId' | 'clientSecret' | 'sellerId'

export function credentialProblems(fields: MsCredentialFields): MsCredentialProblem[] {
  const problems: MsCredentialProblem[] = []
  if (!GUID.test(fields.tenantId.trim())) {
    problems.push('tenantId')
  }
  if (!GUID.test(fields.clientId.trim())) {
    problems.push('clientId')
  }
  if (!fields.clientSecret.trim() || fields.clientSecret.trim().length > 512) {
    problems.push('clientSecret')
  }
  if (!SELLER_ID.test(fields.sellerId.trim())) {
    problems.push('sellerId')
  }
  return problems
}

/** The secret the API seals: {"tenantId","clientId","clientSecret","sellerId"}. */
export function msSecret(fields: MsCredentialFields): string {
  return JSON.stringify({
    tenantId: fields.tenantId.trim().toLowerCase(),
    clientId: fields.clientId.trim().toLowerCase(),
    clientSecret: fields.clientSecret.trim(),
    sellerId: fields.sellerId.trim(),
  })
}

/** Whether the new identifiers are the connection's own (the API answers MsSecretMismatch otherwise). */
export function sameIdentity(fields: MsCredentialFields, tenantSuffix?: string | null, clientSuffix?: string | null): boolean {
  const ends = (value: string, suffix?: string | null) => !suffix || value.trim().toLowerCase().endsWith(suffix.toLowerCase())
  return ends(fields.tenantId, tenantSuffix) && ends(fields.clientId, clientSuffix)
}

function addMonths(day: Date, months: number): Date {
  const next = new Date(day)
  next.setMonth(next.getMonth() + months)
  return next
}

/** The days a secret expiry may take: from tomorrow up to 24 months away (MsSecretRules.IsValidExpiry). */
export function expiryBounds(today: Date = new Date()): { min: DateOnly; max: DateOnly } {
  const tomorrow = new Date(today.getFullYear(), today.getMonth(), today.getDate() + 1)
  return { min: toDateOnly(tomorrow), max: toDateOnly(addMonths(new Date(today.getFullYear(), today.getMonth(), today.getDate()), 24)) }
}

/** The default expiry: 12 months, what Microsoft recommends. */
export function defaultExpiry(today: Date = new Date()): DateOnly {
  return toDateOnly(addMonths(new Date(today.getFullYear(), today.getMonth(), today.getDate()), 12))
}

export function expiryValid(day: DateOnly | null, today: Date = new Date()): boolean {
  if (!day || !parseDateOnly(day)) {
    return false
  }
  const { min, max } = expiryBounds(today)
  return day >= min && day <= max
}

/** The picked day as the UTC instant the API stores. */
export function expiryInstant(day: DateOnly): IsoDateTime {
  return `${day}T00:00:00Z`
}

/** Whole days until an instant (negative when it passed). */
export function daysUntil(value: IsoDateTime, now: Date = new Date()): number {
  return Math.floor((Date.parse(value) - now.getTime()) / 86_400_000)
}

export type ExpiryLevel = 'expired' | 'soon' | 'ok'

export function expiryLevel(daysLeft: number): ExpiryLevel {
  return daysLeft < 0 ? 'expired' : daysLeft <= MS_SECRET_WARN_DAYS ? 'soon' : 'ok'
}

export function isStoreId(value: string): boolean {
  return STORE_ID.test(value.trim())
}

// ---- Submissions -------------------------------------------------------------------------------------------------------
export const FAILED_STATUSES: readonly MsSubmissionStatus[] = [
  MsSubmissionStatus.CommitFailed,
  MsSubmissionStatus.PreProcessingFailed,
  MsSubmissionStatus.CertificationFailed,
  MsSubmissionStatus.ReleaseFailed,
  MsSubmissionStatus.PublishFailed,
  MsSubmissionStatus.Failed,
]

// The five steps of the design's status machine (8.22), then Published.
export const STATUS_STEPS: readonly MsSubmissionStatus[] = [
  MsSubmissionStatus.PendingCommit,
  MsSubmissionStatus.CommitStarted,
  MsSubmissionStatus.PreProcessing,
  MsSubmissionStatus.Certification,
  MsSubmissionStatus.Release,
  MsSubmissionStatus.Published,
]

const STEP_OF: Partial<Record<MsSubmissionStatus, number>> = {
  [MsSubmissionStatus.PendingCommit]: 0,
  [MsSubmissionStatus.CommitStarted]: 1,
  [MsSubmissionStatus.CommitFailed]: 1,
  [MsSubmissionStatus.PreProcessing]: 2,
  [MsSubmissionStatus.PreProcessingFailed]: 2,
  [MsSubmissionStatus.InProgress]: 2,
  [MsSubmissionStatus.Failed]: 2,
  [MsSubmissionStatus.Certification]: 3,
  [MsSubmissionStatus.CertificationFailed]: 3,
  [MsSubmissionStatus.Release]: 4,
  [MsSubmissionStatus.ReleaseFailed]: 4,
  [MsSubmissionStatus.PendingPublication]: 4,
  [MsSubmissionStatus.Publishing]: 4,
  [MsSubmissionStatus.PublishFailed]: 4,
  [MsSubmissionStatus.Published]: 5,
}

/** Which step of STATUS_STEPS a status sits on; -1 for canceled and unknown. */
export function statusStep(status: MsSubmissionStatus): number {
  return STEP_OF[status] ?? -1
}

/** A draft storeasy can still send on: created here, not committed, not changed in Partner Center. */
export function isSubmittableDraft(submission: MsSubmissionDto): boolean {
  return (
    submission.IsPending &&
    submission.CreatedByStoreasy &&
    !submission.ModifiedOutsideApi &&
    submission.Status === MsSubmissionStatus.PendingCommit
  )
}

export interface MsLockState {
  // The Partner Center rule: a draft storeasy created was changed there; only discarding it is left.
  editedInPartnerCenter: boolean
  // The single active submission rule: one is already committed and on its way (certification and so on).
  inProgress: MsSubmissionDto | null
  // MSIX: the API works only after the first submission, which is made in Partner Center.
  firstSubmissionMissing: boolean
}

export function lockState(product: MsProductDto): MsLockState {
  const active = product.ActiveSubmission ?? null
  return {
    editedInPartnerCenter: !!active?.ModifiedOutsideApi,
    inProgress: active && active.Status !== MsSubmissionStatus.PendingCommit && !active.ModifiedOutsideApi ? active : null,
    firstSubmissionMissing: product.Kind === MsProductKind.Msix && !product.FirstSubmissionDone,
  }
}

/** Whether edits can be planned at all (the API plans the rest and blocks what it must). */
export function canPlan(lock: MsLockState): boolean {
  return !lock.editedInPartnerCenter && !lock.firstSubmissionMissing
}
