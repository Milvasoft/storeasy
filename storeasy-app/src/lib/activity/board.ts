import type { ComplianceItemDto } from '@/features/common/types'
import { ComplianceStatus } from '@/features/common/types'
import type { RejectionCaseListDto } from '@/features/rejections/types'
import {
  BuildProcessingState,
  ConnectionStatus,
  StoreJobStatus,
  StoreKind,
  type ActivityBoardBuildDto,
  type ActivityBoardDto,
  type ActivityBoardVersionDto,
  type IsoDateTime,
  type StoreConnectionDto,
} from '@/lib/api/types'
import { storeLinksOf } from '@/lib/apps/storeLinks'
import { isActive, type JobView } from './hatState'

// The calm half of the live page (pure, unit tested): what each store's gate says right now, what is coming up, and the
// one or two things that need a person. Built from the board endpoint, open rejection cases, compliance deadlines, the
// store connections and the jobs the stream knows. Texts are chosen by the components; this file only decides.

export type Tone = 'danger' | 'warning' | 'accent' | 'success' | 'neutral'

const TONE_RANK: Record<Tone, number> = { danger: 0, warning: 1, accent: 2, success: 3, neutral: 4 }

export interface BoardInputs {
  board: ActivityBoardDto | null
  rejections: RejectionCaseListDto[]
  compliance: ComplianceItemDto[]
  connections: StoreConnectionDto[]
  jobs: JobView[]
}

export const EMPTY_INPUTS: BoardInputs = { board: null, rejections: [], compliance: [], connections: [], jobs: [] }

const DAY_MS = 86_400_000
// A version that went live stays on its gate this long ("live since 2 days").
const LIVE_SHOWN_MS = 3 * DAY_MS
// A failed job asks for attention for a day, unless a later job of the same store went well.
const FAILED_JOB_ATTENTION_MS = DAY_MS
export const EXPIRY_ATTENTION_DAYS = 3
export const DEADLINE_ATTENTION_DAYS = 7
export const UPCOMING_DAYS = 45

// ---- Version states (App Store Connect names, and the one Microsoft Store state the board names) ----

// pendingPublish: a Microsoft Store submission passed certification and waits for "Publish now" in Partner Center; the
// API has no call that publishes it, so the person acts there.
export type ReviewPhase = 'waiting' | 'inReview' | 'pendingRelease' | 'pendingPublish' | 'rejected' | 'live' | 'other'

export function reviewPhase(state: string | null | undefined): ReviewPhase {
  switch ((state ?? '').toUpperCase()) {
    case 'WAITING_FOR_REVIEW':
      return 'waiting'
    case 'IN_REVIEW':
      return 'inReview'
    case 'PENDING_DEVELOPER_RELEASE':
    case 'PENDING_APPLE_RELEASE':
      return 'pendingRelease'
    case 'PENDINGPUBLICATION':
      return 'pendingPublish'
    case 'REJECTED':
    case 'METADATA_REJECTED':
    case 'INVALID_BINARY':
      return 'rejected'
    case 'READY_FOR_SALE':
    case 'READY_FOR_DISTRIBUTION':
    case 'PROCESSING_FOR_DISTRIBUTION':
    case 'PROCESSING_FOR_APP_STORE':
      return 'live'
    default:
      return 'other'
  }
}

// ---- Gates ----

export type GateItemKind =
  | 'inReview'
  | 'waiting'
  | 'pendingRelease'
  | 'pendingPublish'
  | 'rejected'
  | 'live'
  | 'processing'
  | 'connection'
  | 'lastFailed'

export interface GateItem {
  key: string
  kind: GateItemKind
  tone: Tone
  store: StoreKind
  appId: number | null
  // "1.4 (52)" or the connection's name.
  subject: string | null
  // Since when (elapsed time is shown) for states, when it happened for a failure.
  since: IsoDateTime | null
  // The store's reason (a failed job's message, a connection's last error).
  detail: string | null
  // A rate limit or quota answer (429).
  rateLimited: boolean
  href: string | null
  jobId: number | null
}

function versionSubject(version: Pick<ActivityBoardVersionDto, 'VersionString' | 'BuildNumber'>): string | null {
  if (!version.VersionString) {
    return version.BuildNumber ?? null
  }
  return version.BuildNumber ? `${version.VersionString} (${version.BuildNumber})` : version.VersionString
}

function buildSubject(build: Pick<ActivityBoardBuildDto, 'Version' | 'BuildNumber'>): string | null {
  return versionSubject({ VersionString: build.Version, BuildNumber: build.BuildNumber })
}

const VERSION_TONE: Record<ReviewPhase, Tone> = {
  inReview: 'accent',
  waiting: 'neutral',
  pendingRelease: 'warning',
  pendingPublish: 'warning',
  rejected: 'danger',
  live: 'success',
  other: 'neutral',
}

/** An address outside storeasy (the store's own console), opened in a new tab instead of routed. */
export function isExternalHref(href: string): boolean {
  return /^https?:\/\//i.test(href)
}

/** Where a version is acted on: Partner Center for a submission waiting for "Publish now", storeasy's pages otherwise. */
function versionHref(version: ActivityBoardVersionDto, phase: ReviewPhase): string {
  switch (phase) {
    case 'pendingPublish':
      return storeLinksOf({ Store: version.Store, ExternalId: version.ExternalId ?? null, BundleId: null }).consoleUrl
    case 'rejected':
      return `/apps/${version.AppId}/rejections`
    default:
      return `/apps/${version.AppId}/release/versions`
  }
}

export function isRateLimit(message: string | null | undefined): boolean {
  return /\b429\b|rate.?limit|too many requests|quota/i.test(message ?? '')
}

/** The newest finished job of the store when it failed, a day at most, and no later job of the store went well. */
export function lastFailedJob(jobs: JobView[], store: StoreKind, now: number): JobView | null {
  const finished = jobs
    .filter((job) => job.store === store && !isActive(job) && job.status !== StoreJobStatus.Cancelled)
    .sort((a, b) => (a.updatedAt < b.updatedAt ? 1 : a.updatedAt > b.updatedAt ? -1 : b.id - a.id))
  const newest = finished[0]
  if (!newest || newest.status !== StoreJobStatus.Failed) {
    return null
  }
  const when = Date.parse(newest.updatedAt)
  return Number.isNaN(when) || now - when < FAILED_JOB_ATTENTION_MS ? newest : null
}

/** What a store's gate lists, most urgent first: failures, rejections, waiting releases, reviews, processing, live. */
export function gateItemsOf(store: StoreKind, inputs: BoardInputs, now: number): GateItem[] {
  const items: GateItem[] = []
  for (const connection of inputs.connections) {
    if (
      connection.Store === store &&
      (connection.Status === ConnectionStatus.Rejected || connection.Status === ConnectionStatus.Unreachable)
    ) {
      items.push({
        key: `c${connection.Id}`,
        kind: 'connection',
        tone: 'danger',
        store,
        appId: null,
        subject: connection.Name,
        since: connection.LastVerifiedDate ?? null,
        detail: connection.LastVerifyError ?? null,
        rateLimited: isRateLimit(connection.LastVerifyError),
        href: '/connections',
        jobId: null,
      })
    }
  }
  const failed = lastFailedJob(inputs.jobs, store, now)
  if (failed) {
    items.push({
      key: `j${failed.id}`,
      kind: 'lastFailed',
      tone: isRateLimit(failed.message) ? 'warning' : 'danger',
      store,
      appId: failed.appId,
      subject: null,
      since: failed.updatedAt,
      detail: failed.message,
      rateLimited: isRateLimit(failed.message),
      href: `/activity/jobs/${failed.id}`,
      jobId: failed.id,
    })
  }
  for (const version of inputs.board?.Versions ?? []) {
    if (version.Store !== store) {
      continue
    }
    const phase = reviewPhase(version.State)
    if (phase === 'other' || (phase === 'live' && now - Date.parse(version.ChangedDate) > LIVE_SHOWN_MS)) {
      continue
    }
    items.push({
      key: `v${version.StoreAppId}`,
      kind: phase,
      tone: VERSION_TONE[phase],
      store,
      appId: version.AppId,
      subject: versionSubject(version),
      // Waiting counts from the submission; the other states from when they began.
      since: phase === 'waiting' ? (version.SubmittedDate ?? version.ChangedDate) : version.ChangedDate,
      detail: version.State ?? null,
      rateLimited: false,
      href: versionHref(version, phase),
      jobId: null,
    })
  }
  for (const build of inputs.board?.Builds ?? []) {
    if (build.Store !== store || build.ProcessingState !== BuildProcessingState.Processing) {
      continue
    }
    items.push({
      key: `b${build.StoreAppId}-${build.BuildNumber ?? ''}`,
      kind: 'processing',
      tone: 'accent',
      store,
      appId: build.AppId,
      subject: buildSubject(build),
      since: build.UploadedDate ?? null,
      detail: null,
      rateLimited: false,
      href: `/apps/${build.AppId}/builds`,
      jobId: null,
    })
  }
  return items.sort((a, b) => TONE_RANK[a.tone] - TONE_RANK[b.tone] || (b.since ?? '').localeCompare(a.since ?? ''))
}

// The gate's one-word state: the light beside the store's name.
export type GateState = 'error' | 'attention' | 'busy' | 'calm'

export function gateState(items: GateItem[], moving: boolean): GateState {
  if (items.some((item) => item.tone === 'danger')) {
    return 'error'
  }
  if (items.some((item) => item.tone === 'warning')) {
    return 'attention'
  }
  // A review or a processing build is the store's own pace; only work on the line makes the gate busy.
  return moving ? 'busy' : 'calm'
}

// ---- The 24 hour strip ----

export interface HourBar {
  hour: IsoDateTime
  succeeded: number
  failed: number
  inbound: number
}

/** 24 hourly bars of one store, oldest first; hours the board left out are empty. */
export function hourStrip(board: ActivityBoardDto | null, store: StoreKind, now: number): HourBar[] {
  const start = board ? Date.parse(board.HoursStartDate) : Math.floor(now / 3_600_000) * 3_600_000 - 23 * 3_600_000
  const bars: HourBar[] = Array.from({ length: 24 }, (_, index) => ({
    hour: new Date(start + index * 3_600_000).toISOString(),
    succeeded: 0,
    failed: 0,
    inbound: 0,
  }))
  for (const hour of board?.Hours ?? []) {
    if (hour.Store !== store) {
      continue
    }
    const index = Math.round((Date.parse(hour.Hour) - start) / 3_600_000)
    if (index >= 0 && index < 24) {
      bars[index] = { hour: bars[index].hour, succeeded: hour.Succeeded, failed: hour.Failed, inbound: hour.Inbound }
    }
  }
  return bars
}

// ---- Upcoming: dates that are coming (the quiet board) ----

export type UpcomingKind = 'expiry' | 'deadline' | 'review'

export interface UpcomingItem {
  key: string
  kind: UpcomingKind
  tone: Tone
  store: StoreKind
  appId: number | null
  appName: string | null
  // The build ("1.4 (52)"), the rule's title, or the version in review.
  subject: string | null
  // When it is due (expiry, deadline) or since when it waits (review).
  date: IsoDateTime
  // Whole days left (negative when overdue); null for a review, which counts up.
  daysLeft: number | null
  href: string
}

function daysUntil(date: string, now: number): number {
  const target = /^\d{4}-\d{2}-\d{2}$/.test(date) ? Date.parse(`${date}T23:59:59Z`) : Date.parse(date)
  return Math.floor((target - now) / DAY_MS)
}

function openCompliance(item: ComplianceItemDto): boolean {
  return item.Status === ComplianceStatus.Missing || item.Status === ComplianceStatus.Unknown
}

/**
 * What is coming, soonest first: versions waiting with App Review (counting up), TestFlight builds about to expire and
 * open compliance deadlines (counting down).
 */
export function upcomingOf(inputs: BoardInputs, now: number): UpcomingItem[] {
  const items: UpcomingItem[] = []
  for (const version of inputs.board?.Versions ?? []) {
    const phase = reviewPhase(version.State)
    if (phase !== 'waiting' && phase !== 'inReview') {
      continue
    }
    items.push({
      key: `r${version.StoreAppId}`,
      kind: 'review',
      tone: phase === 'inReview' ? 'accent' : 'neutral',
      store: version.Store,
      appId: version.AppId,
      appName: null,
      subject: versionSubject(version),
      date: phase === 'waiting' ? (version.SubmittedDate ?? version.ChangedDate) : version.ChangedDate,
      daysLeft: null,
      href: `/apps/${version.AppId}/release/versions`,
    })
  }
  for (const build of inputs.board?.Builds ?? []) {
    if (build.ProcessingState !== BuildProcessingState.Valid || !build.ExpiryDate) {
      continue
    }
    const days = daysUntil(build.ExpiryDate, now)
    items.push({
      key: `e${build.StoreAppId}-${build.BuildNumber ?? ''}`,
      kind: 'expiry',
      tone: days <= EXPIRY_ATTENTION_DAYS ? 'warning' : 'neutral',
      store: build.Store,
      appId: build.AppId,
      appName: null,
      subject: buildSubject(build),
      date: build.ExpiryDate,
      daysLeft: days,
      href: `/apps/${build.AppId}/release/testflight`,
    })
  }
  for (const item of inputs.compliance) {
    if (!openCompliance(item) || !item.DueDate) {
      continue
    }
    const days = item.DaysLeft ?? daysUntil(item.DueDate, now)
    if (days > UPCOMING_DAYS) {
      continue
    }
    items.push({
      key: `d${item.AppId}-${item.Store}-${item.RuleId}`,
      kind: 'deadline',
      tone: days < 0 ? 'danger' : days <= DEADLINE_ATTENTION_DAYS ? 'warning' : 'neutral',
      store: item.Store,
      appId: item.AppId,
      appName: item.AppName ?? null,
      subject: item.Title ?? item.RuleId,
      date: item.DueDate,
      daysLeft: days,
      href: `/apps/${item.AppId}/compliance`,
    })
  }
  // Reviews first (they are happening now), then by how soon.
  return items.sort((a, b) => {
    if ((a.kind === 'review') !== (b.kind === 'review')) {
      return a.kind === 'review' ? -1 : 1
    }
    if (a.kind === 'review') {
      return a.date.localeCompare(b.date)
    }
    return (a.daysLeft ?? 0) - (b.daysLeft ?? 0)
  })
}

// ---- Needs attention ----

export type AttentionKind =
  | 'rejection'
  | 'jobFailed'
  | 'connection'
  | 'pendingRelease'
  | 'pendingPublish'
  | 'expiry'
  | 'deadline'
  | 'unverified'

export interface AttentionItem {
  key: string
  kind: AttentionKind
  tone: Tone
  store: StoreKind | null
  appId: number | null
  appName: string | null
  subject: string | null
  detail: string | null
  date: IsoDateTime | null
  daysLeft: number | null
  href: string
  /** How many apps a grouped item stands for (a compliance rule shared by several apps); 1 otherwise. */
  count?: number
}

/**
 * Compliance deadlines that need a person: one item per rule and store, only for apps where the gap is confirmed
 * (Missing), due within DEADLINE_ATTENTION_DAYS or overdue. Rows storeasy cannot verify (Unknown) never become one item
 * each: they fold into a single "could not be verified" item, so a catalog rule that applies to every app does not
 * flood the bar.
 */
function deadlineAttentionOf(compliance: ComplianceItemDto[], now: number): AttentionItem[] {
  const groups = new Map<string, { first: ComplianceItemDto; days: number; apps: Set<number> }>()
  let unverified = 0
  for (const item of compliance) {
    if (!item.DueDate || !openCompliance(item)) {
      continue
    }
    const days = item.DaysLeft ?? daysUntil(item.DueDate, now)
    if (days > DEADLINE_ATTENTION_DAYS) {
      continue
    }
    if (item.Status !== ComplianceStatus.Missing) {
      unverified += 1
      continue
    }
    const key = `${item.Store}-${item.RuleId}`
    const group = groups.get(key)
    if (group) {
      group.apps.add(item.AppId)
      group.days = Math.min(group.days, days)
    } else {
      groups.set(key, { first: item, days, apps: new Set([item.AppId]) })
    }
  }
  const items: AttentionItem[] = [...groups.entries()].map(([key, { first, days, apps }]) => ({
    key: `dl${key}`,
    kind: 'deadline',
    tone: days < 0 ? 'danger' : 'warning',
    store: first.Store,
    appId: apps.size === 1 ? first.AppId : null,
    appName: apps.size === 1 ? (first.AppName ?? null) : null,
    subject: first.Title ?? first.RuleId,
    detail: null,
    date: first.DueDate ?? null,
    daysLeft: days,
    href: apps.size === 1 ? `/apps/${first.AppId}/compliance` : '/compliance',
    count: apps.size,
  }))
  if (unverified > 0) {
    items.push({
      key: 'unverified',
      kind: 'unverified',
      tone: 'neutral',
      store: null,
      appId: null,
      appName: null,
      subject: null,
      detail: null,
      date: null,
      daysLeft: null,
      href: '/compliance',
      count: unverified,
    })
  }
  return items
}

export const MAX_ATTENTION = 2

/** Every open matter a person should act on, most urgent first; the page shows the first MAX_ATTENTION. */
export function attentionOf(inputs: BoardInputs, now: number): AttentionItem[] {
  const items: AttentionItem[] = []
  const rejectedWithCase = new Set<number>()
  for (const item of inputs.rejections) {
    rejectedWithCase.add(item.StoreAppId)
    items.push({
      key: `rc${item.Id}`,
      kind: 'rejection',
      tone: 'danger',
      store: item.Store,
      appId: item.AppId,
      appName: null,
      subject: item.VersionString ?? null,
      detail: item.ChecklistTotal > 0 ? `${item.ChecklistDone}/${item.ChecklistTotal}` : null,
      date: item.OpenedDate,
      daysLeft: null,
      href: `/apps/${item.AppId}/rejections/${item.Id}`,
    })
  }
  for (const version of inputs.board?.Versions ?? []) {
    const phase = reviewPhase(version.State)
    if (phase === 'rejected' && !rejectedWithCase.has(version.StoreAppId)) {
      items.push({
        key: `rv${version.StoreAppId}`,
        kind: 'rejection',
        tone: 'danger',
        store: version.Store,
        appId: version.AppId,
        appName: null,
        subject: versionSubject(version),
        detail: null,
        date: version.ChangedDate,
        daysLeft: null,
        href: `/apps/${version.AppId}/rejections`,
      })
    }
    if (phase === 'pendingRelease' || phase === 'pendingPublish') {
      items.push({
        key: `pr${version.StoreAppId}`,
        kind: phase,
        tone: 'warning',
        store: version.Store,
        appId: version.AppId,
        appName: null,
        subject: versionSubject(version),
        detail: null,
        date: version.ChangedDate,
        daysLeft: null,
        href: versionHref(version, phase),
      })
    }
  }
  for (const connection of inputs.connections) {
    if (connection.Status === ConnectionStatus.Rejected || connection.Status === ConnectionStatus.Unreachable) {
      items.push({
        key: `cn${connection.Id}`,
        kind: 'connection',
        tone: 'danger',
        store: connection.Store,
        appId: null,
        appName: null,
        subject: connection.Name,
        detail: connection.LastVerifyError ?? null,
        date: connection.LastVerifiedDate ?? null,
        daysLeft: null,
        href: '/connections',
      })
    }
  }
  for (const store of [StoreKind.Apple, StoreKind.Google, StoreKind.Microsoft]) {
    const failed = lastFailedJob(inputs.jobs, store, now)
    if (failed) {
      items.push({
        key: `jf${failed.id}`,
        kind: 'jobFailed',
        tone: isRateLimit(failed.message) ? 'warning' : 'danger',
        store,
        appId: failed.appId,
        appName: null,
        subject: null,
        detail: failed.message,
        date: failed.updatedAt,
        daysLeft: null,
        href: `/activity/jobs/${failed.id}`,
      })
    }
  }
  for (const item of upcomingOf(inputs, now)) {
    if (item.kind === 'expiry' && item.daysLeft !== null && item.daysLeft <= EXPIRY_ATTENTION_DAYS) {
      items.push({ ...item, key: `x${item.key}`, kind: 'expiry', detail: null, tone: 'warning' })
    }
  }
  items.push(...deadlineAttentionOf(inputs.compliance, now))
  const kindRank: Record<AttentionKind, number> = {
    rejection: 0,
    jobFailed: 1,
    connection: 2,
    deadline: 3,
    expiry: 4,
    pendingRelease: 5,
    pendingPublish: 5,
    unverified: 6,
  }
  return items.sort((a, b) => TONE_RANK[a.tone] - TONE_RANK[b.tone] || kindRank[a.kind] - kindRank[b.kind])
}
