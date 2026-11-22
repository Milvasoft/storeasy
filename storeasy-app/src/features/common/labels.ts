import type { BadgeTone } from '@/components/ui/Badge'
import { ActionChannel, OrgPermission } from '@/lib/api/types'
import type { P2Dictionary } from './i18n/en'
import {
  AssistedStepStatus,
  ChangeAction,
  ChangePlanKind,
  ChangePlanStatus,
  ComplianceStatus,
  ReadinessStatus,
  RuleApplicability,
  RuleStatus,
  SavedAnswerKind,
} from './types'

// Enum labels and badge tones of the phase 2 screens, one switch per enum (02 section 8.6).

export interface StatusView {
  label: string
  tone: BadgeTone
}

export function readinessView(status: ReadinessStatus, t: P2Dictionary): StatusView {
  const labels = t.enums.readinessStatus
  switch (status) {
    case ReadinessStatus.Ok:
      return { label: labels.ok, tone: 'success' }
    case ReadinessStatus.Missing:
      return { label: labels.missing, tone: 'danger' }
    case ReadinessStatus.Warning:
      return { label: labels.warning, tone: 'warning' }
    case ReadinessStatus.Assisted:
      return { label: labels.assisted, tone: 'info' }
    case ReadinessStatus.NotApplicable:
      return { label: labels.notApplicable, tone: 'neutral' }
    default:
      return { label: labels.unknown, tone: 'neutral' }
  }
}

export function complianceView(status: ComplianceStatus, t: P2Dictionary): StatusView {
  const labels = t.enums.complianceStatus
  switch (status) {
    case ComplianceStatus.Done:
      return { label: labels.done, tone: 'success' }
    case ComplianceStatus.Missing:
      return { label: labels.missing, tone: 'warning' }
    case ComplianceStatus.NotApplicable:
      return { label: labels.notApplicable, tone: 'neutral' }
    default:
      return { label: labels.unknown, tone: 'neutral' }
  }
}

export function ruleStatusView(status: RuleStatus, t: P2Dictionary): StatusView {
  const labels = t.enums.ruleStatus
  switch (status) {
    case RuleStatus.InForce:
      return { label: labels.inForce, tone: 'success' }
    case RuleStatus.Upcoming:
      return { label: labels.upcoming, tone: 'warning' }
    default:
      return { label: labels.deprecated, tone: 'neutral' }
  }
}

export function applicabilityLabel(value: RuleApplicability, t: P2Dictionary): string {
  switch (value) {
    case RuleApplicability.Applies:
      return t.enums.applicability.applies
    case RuleApplicability.DoesNotApply:
      return t.enums.applicability.doesNotApply
    default:
      return t.enums.applicability.unknown
  }
}

export function planStatusView(status: ChangePlanStatus, t: P2Dictionary): StatusView {
  const labels = t.enums.planStatus
  switch (status) {
    case ChangePlanStatus.Pending:
      return { label: labels.pending, tone: 'info' }
    case ChangePlanStatus.Approved:
      return { label: labels.approved, tone: 'accent' }
    case ChangePlanStatus.Applied:
      return { label: labels.applied, tone: 'success' }
    case ChangePlanStatus.Failed:
      return { label: labels.failed, tone: 'danger' }
    case ChangePlanStatus.Rejected:
      return { label: labels.rejected, tone: 'danger' }
    case ChangePlanStatus.Discarded:
      return { label: labels.discarded, tone: 'neutral' }
    default:
      return { label: labels.expired, tone: 'neutral' }
  }
}

const PLAN_KIND_KEYS: Record<ChangePlanKind, keyof P2Dictionary['enums']['planKind']> = {
  [ChangePlanKind.Listing]: 'listing',
  [ChangePlanKind.Details]: 'details',
  [ChangePlanKind.Categories]: 'categories',
  [ChangePlanKind.AgeRating]: 'ageRating',
  [ChangePlanKind.Pricing]: 'pricing',
  [ChangePlanKind.Screenshots]: 'screenshots',
  [ChangePlanKind.Previews]: 'previews',
  [ChangePlanKind.ReviewInfo]: 'reviewInfo',
  [ChangePlanKind.BuildLink]: 'buildLink',
  [ChangePlanKind.ExportCompliance]: 'exportCompliance',
  [ChangePlanKind.ReleaseSettings]: 'releaseSettings',
  [ChangePlanKind.TrackRelease]: 'trackRelease',
  [ChangePlanKind.Rollout]: 'rollout',
  [ChangePlanKind.Testers]: 'testers',
  [ChangePlanKind.BundleUpload]: 'bundleUpload',
  [ChangePlanKind.DataSafety]: 'dataSafety',
  [ChangePlanKind.SubmitForReview]: 'submitForReview',
  [ChangePlanKind.MsChange]: 'msChange',
  [ChangePlanKind.Monetization]: 'monetization',
  [ChangePlanKind.ServerNotifications]: 'serverNotifications',
  [ChangePlanKind.SandboxTesters]: 'sandboxTesters',
  [ChangePlanKind.AsoExperiment]: 'asoExperiment',
  [ChangePlanKind.WingetChange]: 'wingetChange',
  [ChangePlanKind.ChromeChange]: 'chromeChange',
}

export function planKindLabel(kind: ChangePlanKind, t: P2Dictionary): string {
  return t.enums.planKind[PLAN_KIND_KEYS[kind] ?? 'listing']
}

export function changeActionLabel(action: ChangeAction, t: P2Dictionary): string {
  switch (action) {
    case ChangeAction.Create:
      return t.plan.action.create
    case ChangeAction.Delete:
      return t.plan.action.delete
    default:
      return t.plan.action.update
  }
}

export function assistedView(status: AssistedStepStatus, t: P2Dictionary): StatusView {
  const labels = t.enums.assistedStatus
  switch (status) {
    case AssistedStepStatus.Todo:
      return { label: labels.todo, tone: 'warning' }
    case AssistedStepStatus.InProgress:
      return { label: labels.inProgress, tone: 'info' }
    case AssistedStepStatus.HandedOff:
      return { label: labels.handedOff, tone: 'accent' }
    case AssistedStepStatus.Done:
      return { label: labels.done, tone: 'success' }
    default:
      return { label: labels.skipped, tone: 'neutral' }
  }
}

export function permissionLabel(permission: OrgPermission, t: P2Dictionary): string {
  const labels = t.enums.permission
  switch (permission) {
    case OrgPermission.View:
      return labels.view
    case OrgPermission.EditContent:
      return labels.editContent
    case OrgPermission.ReleaseTest:
      return labels.releaseTest
    case OrgPermission.ReleaseProduction:
      return labels.releaseProduction
    case OrgPermission.SubmitForReview:
      return labels.submitForReview
    default:
      return labels.other
  }
}

export function channelLabel(channel: ActionChannel, t: P2Dictionary): string {
  const labels = t.enums.channel
  switch (channel) {
    case ActionChannel.Mcp:
      return labels.mcp
    case ActionChannel.Api:
      return labels.api
    case ActionChannel.System:
      return labels.system
    case ActionChannel.Ci:
      return labels.ci
    default:
      return labels.web
  }
}

export const ANSWER_KINDS: readonly SavedAnswerKind[] = [
  SavedAnswerKind.PrivacyLabels,
  SavedAnswerKind.DataSafety,
  SavedAnswerKind.ContentRating,
  SavedAnswerKind.TargetAudience,
  SavedAnswerKind.ReviewLogin,
  SavedAnswerKind.ReviewNotes,
]

export function answerKindLabel(kind: SavedAnswerKind, t: P2Dictionary): string {
  const labels = t.answers.kinds
  switch (kind) {
    case SavedAnswerKind.PrivacyLabels:
      return labels.privacyLabels
    case SavedAnswerKind.DataSafety:
      return labels.dataSafety
    case SavedAnswerKind.ContentRating:
      return labels.contentRating
    case SavedAnswerKind.TargetAudience:
      return labels.targetAudience
    case SavedAnswerKind.ReviewLogin:
      return labels.reviewLogin
    default:
      return labels.reviewNotes
  }
}

/** The label of a listing field key ("keywords" -> "Keywords"); an unknown key shows as it is. */
export function fieldLabel(key: string, t: P2Dictionary): string {
  return (t.fields as Record<string, string>)[key] ?? key
}
