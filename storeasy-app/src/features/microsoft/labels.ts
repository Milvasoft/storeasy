import type { BadgeTone } from '@/components/ui/Badge'
import { interpolate } from '@/lib/i18n/interpolate'
import type { MsDictionary } from './i18n/en'
import { FAILED_STATUSES } from './rules'
import {
  MsAddonType,
  MsAssistedReason,
  MsInstallerType,
  MsRolloutStatus,
  MsSignatureStatus,
  MsSubmissionStatus,
  MsSubmissionTarget,
} from './types'

// Enum labels of the Microsoft Store screens, one function per enum (t.enums.<enum>.<camelCaseValue>).

const STATUS_KEYS: Record<MsSubmissionStatus, keyof MsDictionary['enums']['status']> = {
  [MsSubmissionStatus.PendingCommit]: 'pendingCommit',
  [MsSubmissionStatus.CommitStarted]: 'commitStarted',
  [MsSubmissionStatus.CommitFailed]: 'commitFailed',
  [MsSubmissionStatus.PreProcessing]: 'preProcessing',
  [MsSubmissionStatus.PreProcessingFailed]: 'preProcessingFailed',
  [MsSubmissionStatus.Certification]: 'certification',
  [MsSubmissionStatus.CertificationFailed]: 'certificationFailed',
  [MsSubmissionStatus.Release]: 'release',
  [MsSubmissionStatus.ReleaseFailed]: 'releaseFailed',
  [MsSubmissionStatus.PendingPublication]: 'pendingPublication',
  [MsSubmissionStatus.Publishing]: 'publishing',
  [MsSubmissionStatus.Published]: 'published',
  [MsSubmissionStatus.PublishFailed]: 'publishFailed',
  [MsSubmissionStatus.Canceled]: 'canceled',
  [MsSubmissionStatus.InProgress]: 'inProgress',
  [MsSubmissionStatus.Failed]: 'failed',
  [MsSubmissionStatus.Unknown]: 'unknown',
}

export function statusLabel(status: MsSubmissionStatus, t: MsDictionary): string {
  return t.enums.status[STATUS_KEYS[status] ?? 'unknown']
}

export function statusTone(status: MsSubmissionStatus): BadgeTone {
  if (FAILED_STATUSES.includes(status)) {
    return 'danger'
  }
  switch (status) {
    case MsSubmissionStatus.Published:
      return 'success'
    case MsSubmissionStatus.PendingCommit:
    case MsSubmissionStatus.Canceled:
    case MsSubmissionStatus.Unknown:
      return 'neutral'
    default:
      return 'warning'
  }
}

export function targetLabel(target: MsSubmissionTarget, t: MsDictionary): string {
  switch (target) {
    case MsSubmissionTarget.Flight:
      return t.enums.target.flight
    case MsSubmissionTarget.Addon:
      return t.enums.target.addon
    case MsSubmissionTarget.Installer:
      return t.enums.target.installer
    default:
      return t.enums.target.app
  }
}

export function addonTypeLabel(type: MsAddonType, t: MsDictionary): string {
  switch (type) {
    case MsAddonType.Durable:
      return t.enums.addonType.durable
    case MsAddonType.Consumable:
      return t.enums.addonType.consumable
    case MsAddonType.Subscription:
      return t.enums.addonType.subscription
    default:
      return t.enums.addonType.unknown
  }
}

export function installerTypeLabel(type: MsInstallerType, t: MsDictionary): string {
  return type === MsInstallerType.Msi ? t.enums.installerType.msi : t.enums.installerType.exe
}

export function signatureView(status: MsSignatureStatus, t: MsDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case MsSignatureStatus.Signed:
      return { label: t.enums.signature.signed, tone: 'success' }
    case MsSignatureStatus.Unsigned:
      return { label: t.enums.signature.unsigned, tone: 'danger' }
    case MsSignatureStatus.Unreachable:
      return { label: t.enums.signature.unreachable, tone: 'warning' }
    default:
      return { label: t.enums.signature.unchecked, tone: 'neutral' }
  }
}

export function rolloutLabel(status: MsRolloutStatus | null | undefined, t: MsDictionary): string {
  switch (status) {
    case MsRolloutStatus.InProgress:
      return t.enums.rollout.inProgress
    case MsRolloutStatus.Complete:
      return t.enums.rollout.complete
    case MsRolloutStatus.Stopped:
      return t.enums.rollout.stopped
    default:
      return t.enums.rollout.notStarted
  }
}

export function reasonLabel(reason: MsAssistedReason, t: MsDictionary): string {
  switch (reason) {
    case MsAssistedReason.StoreManagedConsumable:
      return t.enums.reason.storeManagedConsumable
    case MsAssistedReason.MandatoryUpdate:
      return t.enums.reason.mandatoryUpdate
    case MsAssistedReason.PricingV2:
      return t.enums.reason.pricingV2
    default:
      return t.enums.reason.manualEdit
  }
}

/** The text of a plan's BlockKey (a message key of the API). */
export function blockText(key: string | null | undefined, t: MsDictionary): string {
  const known = t.plan.block as Record<string, string>
  return key && key !== 'other' && known[key] ? known[key] : interpolate(t.plan.block.other, { key: key ?? '-' })
}

/** A rollout percentage as the API sends it (0-100), in the language's form ("25%", "%25"). */
export function percentText(value: number | null | undefined, t: MsDictionary): string {
  return value === null || value === undefined ? '-' : interpolate(t.percent, { value: Math.round(value * 100) / 100 })
}
