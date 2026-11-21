import type { BadgeTone } from '@/components/ui/Badge'
import { BuildProcessingState, ConnectionStatus, StoreJobKind, StoreJobStatus, StoreJobStepStatus, type IsoDateTime } from '@/lib/api/types'
import type { Dictionary } from '@/lib/i18n/en'
import { interpolate } from '@/lib/i18n/interpolate'

// Labels and tones of the job, step and build enums (design 11.4, 8.1), translated in one place.

const KIND_KEY: Record<StoreJobKind, keyof Dictionary['jobs']['kind']> = {
  [StoreJobKind.SyncApps]: 'syncApps',
  [StoreJobKind.ApplyChangePlan]: 'applyChangePlan',
  [StoreJobKind.ProcessArtifact]: 'processArtifact',
  [StoreJobKind.MsSync]: 'msSync',
  [StoreJobKind.MsSubmit]: 'msSubmit',
  [StoreJobKind.MsCheckInstaller]: 'msCheckInstaller',
  [StoreJobKind.MockupExport]: 'mockupExport',
  [StoreJobKind.SyncMonetization]: 'syncMonetization',
  [StoreJobKind.SyncAsoExperiments]: 'syncAsoExperiments',
  [StoreJobKind.PreReview]: 'preReview',
  [StoreJobKind.AsoKeywordResearch]: 'asoKeywordResearch',
  [StoreJobKind.SyncStoreMetrics]: 'syncStoreMetrics',
  [StoreJobKind.EnableAppleAnalytics]: 'enableAppleAnalytics',
  [StoreJobKind.WingetSync]: 'wingetSync',
  [StoreJobKind.ProcessWebhookEvent]: 'processWebhookEvent',
  [StoreJobKind.RegisterWebhooks]: 'registerWebhooks',
  [StoreJobKind.SyncVersionStates]: 'syncVersionStates',
  [StoreJobKind.SyncReviews]: 'syncReviews',
  [StoreJobKind.ReplyToReview]: 'replyToReview',
  [StoreJobKind.SyncTestFlight]: 'syncTestFlight',
  [StoreJobKind.UpdateTestFlight]: 'updateTestFlight',
  [StoreJobKind.PingWebhooks]: 'pingWebhooks',
}

export function jobKindLabel(kind: StoreJobKind | null | undefined, t: Dictionary): string {
  const key = kind ? KIND_KEY[kind] : undefined
  // A kind newer than this client reads as "Job", never as its bare number.
  return key ? t.jobs.kind[key] : t.jobs.job
}

export interface StatusView {
  label: string
  tone: BadgeTone
  dot?: boolean
}

// `retrying` is a queued job whose last attempt failed; `stopping` a running one asked to stop.
export function jobStatusView(status: StoreJobStatus, t: Dictionary, flags: { retrying?: boolean; stopping?: boolean } = {}): StatusView {
  switch (status) {
    case StoreJobStatus.Queued:
      return flags.retrying ? { label: t.jobs.state.retrying, tone: 'danger' } : { label: t.jobs.state.queued, tone: 'neutral' }
    case StoreJobStatus.Running:
      return flags.stopping ? { label: t.jobs.state.stopping, tone: 'warning' } : { label: t.jobs.state.running, tone: 'accent', dot: true }
    case StoreJobStatus.Succeeded:
      return { label: t.jobs.state.succeeded, tone: 'success', dot: true }
    case StoreJobStatus.Failed:
      return { label: t.jobs.state.failed, tone: 'danger', dot: true }
    default:
      return { label: t.jobs.state.cancelled, tone: 'neutral' }
  }
}

export function isJobActive(status: StoreJobStatus): boolean {
  return status === StoreJobStatus.Queued || status === StoreJobStatus.Running
}

export function stepStatusLabel(status: StoreJobStepStatus, t: Dictionary): string {
  switch (status) {
    case StoreJobStepStatus.Running:
      return t.jobs.stepState.running
    case StoreJobStepStatus.Succeeded:
      return t.jobs.stepState.succeeded
    case StoreJobStepStatus.Failed:
      return t.jobs.stepState.failed
    default:
      return t.jobs.stepState.skipped
  }
}

export function buildStateView(state: BuildProcessingState, t: Dictionary): StatusView {
  switch (state) {
    case BuildProcessingState.Processing:
      return { label: t.builds.state.processing, tone: 'info', dot: true }
    case BuildProcessingState.Valid:
      return { label: t.builds.state.valid, tone: 'success' }
    case BuildProcessingState.Failed:
      return { label: t.builds.state.failed, tone: 'danger' }
    case BuildProcessingState.Invalid:
      return { label: t.builds.state.invalid, tone: 'danger' }
    default:
      return { label: t.builds.state.expired, tone: 'neutral' }
  }
}

export function connectionStatusLabel(status: ConnectionStatus, t: Dictionary): string {
  switch (status) {
    case ConnectionStatus.Verified:
      return t.enums.connectionStatus.verified
    case ConnectionStatus.Rejected:
      return t.enums.connectionStatus.rejected
    case ConnectionStatus.Unreachable:
      return t.enums.connectionStatus.unreachable
    default:
      return t.enums.connectionStatus.notVerified
  }
}

/** "42 s", "1 min 06 s", "2 h 05 min" (design 11.4). */
export function formatDuration(ms: number, t: Dictionary): string {
  const total = Math.max(0, Math.round(ms / 1000))
  const hours = Math.floor(total / 3600)
  const minutes = Math.floor((total % 3600) / 60)
  const seconds = total % 60
  const pad = (value: number) => String(value).padStart(2, '0')
  if (hours > 0) {
    return interpolate(t.jobs.hours, { h: hours, m: pad(minutes) })
  }
  if (minutes > 0) {
    return interpolate(t.jobs.minutes, { m: minutes, s: pad(seconds) })
  }
  return interpolate(t.jobs.seconds, { s: seconds })
}

/** How long a job ran: start to end, or to now while it runs; null before it started. */
export function jobDuration(
  start: IsoDateTime | null | undefined,
  end: IsoDateTime | null | undefined,
  now: number = Date.now(),
): number | null {
  if (!start) {
    return null
  }
  const from = Date.parse(start)
  const to = end ? Date.parse(end) : now
  return Number.isNaN(from) || Number.isNaN(to) ? null : Math.max(0, to - from)
}
