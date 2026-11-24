import type { BadgeTone } from '@/components/ui/Badge'
import type { EasDictionary } from './i18n/en'
import { EasAfterBuild, EasBuildStatus, EasPlatform, EasSubmissionStatus } from './types'

// The API's EasRules: the typed confirmation, which states are running or cancelable, and the labels of the EAS enums.

export function platformWord(platform: EasPlatform): string {
  if (platform === EasPlatform.Ios) {
    return 'iOS'
  }
  return platform === EasPlatform.Android ? 'Android' : 'all'
}

/** What a member types to start a build: the app name and iOS, Android or all. */
export function confirmText(appName: string, platform: EasPlatform): string {
  return `${appName} ${platformWord(platform)}`
}

export function isActive(status: EasBuildStatus): boolean {
  return (
    status === EasBuildStatus.Requested ||
    status === EasBuildStatus.New ||
    status === EasBuildStatus.InQueue ||
    status === EasBuildStatus.InProgress ||
    status === EasBuildStatus.PendingCancel
  )
}

export function isCancelable(status: EasBuildStatus): boolean {
  return status === EasBuildStatus.New || status === EasBuildStatus.InQueue || status === EasBuildStatus.InProgress
}

/** A finished build whose package (IPA, AAB, APK) the intake takes. */
export function canSendToIntake(build: { Status: EasBuildStatus; ArtifactUrl?: string | null; Distribution?: string | null }): boolean {
  return (
    build.Status === EasBuildStatus.Finished &&
    !!build.ArtifactUrl &&
    build.Distribution !== 'SIMULATOR' &&
    /\.(ipa|aab|apk)$/i.test(build.ArtifactUrl.split('?')[0])
  )
}

// The API's EasRules.IsGitRef and IsProfile.
export function isGitRef(value: string): boolean {
  const ref = value.trim()
  return /^[A-Za-z0-9._/-]{1,200}$/.test(ref) && !ref.includes('..') && !ref.startsWith('-') && !ref.endsWith('/')
}

export function isProfile(value: string): boolean {
  return /^[A-Za-z0-9._-]{1,100}$/.test(value.trim())
}

const STATUS_KEYS: Record<EasBuildStatus, keyof EasDictionary['status']> = {
  [EasBuildStatus.Requested]: 'requested',
  [EasBuildStatus.New]: 'new',
  [EasBuildStatus.InQueue]: 'inQueue',
  [EasBuildStatus.InProgress]: 'inProgress',
  [EasBuildStatus.PendingCancel]: 'pendingCancel',
  [EasBuildStatus.Finished]: 'finished',
  [EasBuildStatus.Errored]: 'errored',
  [EasBuildStatus.Canceled]: 'canceled',
  [EasBuildStatus.NotStarted]: 'notStarted',
}

export function statusLabel(status: EasBuildStatus, t: EasDictionary): string {
  return t.status[STATUS_KEYS[status]]
}

export function statusTone(status: EasBuildStatus): BadgeTone {
  if (status === EasBuildStatus.Finished) {
    return 'success'
  }
  if (status === EasBuildStatus.Errored || status === EasBuildStatus.NotStarted) {
    return 'danger'
  }
  return isActive(status) ? 'info' : 'neutral'
}

export function platformLabel(platform: EasPlatform, t: EasDictionary): string {
  if (platform === EasPlatform.Ios) {
    return t.platform.ios
  }
  return platform === EasPlatform.Android ? t.platform.android : t.platform.all
}

export const TRACKS = ['internal', 'alpha', 'beta', 'production'] as const
export const RELEASE_STATUSES = ['COMPLETED', 'DRAFT', 'IN_PROGRESS', 'HALTED'] as const

export function isSubmissionActive(status: EasSubmissionStatus): boolean {
  return status === EasSubmissionStatus.AwaitingBuild || status === EasSubmissionStatus.InQueue || status === EasSubmissionStatus.InProgress
}

const SUBMISSION_KEYS: Record<EasSubmissionStatus, keyof EasDictionary['submissionStatus']> = {
  [EasSubmissionStatus.AwaitingBuild]: 'awaitingBuild',
  [EasSubmissionStatus.InQueue]: 'inQueue',
  [EasSubmissionStatus.InProgress]: 'inProgress',
  [EasSubmissionStatus.Finished]: 'finished',
  [EasSubmissionStatus.Errored]: 'errored',
  [EasSubmissionStatus.Canceled]: 'canceled',
}

export function submissionStatusLabel(status: EasSubmissionStatus, t: EasDictionary): string {
  return t.submissionStatus[SUBMISSION_KEYS[status]]
}

export function submissionTone(status: EasSubmissionStatus): BadgeTone {
  if (status === EasSubmissionStatus.Finished) {
    return 'success'
  }
  if (status === EasSubmissionStatus.Errored) {
    return 'danger'
  }
  return isSubmissionActive(status) ? 'info' : 'neutral'
}

export function afterBuildLabel(afterBuild: EasAfterBuild, t: EasDictionary): string {
  if (afterBuild === EasAfterBuild.Intake) {
    return t.chain.intake
  }
  return afterBuild === EasAfterBuild.Submit ? t.chain.submit : t.chain.none
}
