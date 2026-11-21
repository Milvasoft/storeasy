import type { BadgeTone } from '@/components/ui/Badge'
import type { TimelineTone } from '@/components/ui/Progress'
import { ChangePlanStatus } from '@/features/common/types'
import type { ExperimentsDictionary } from './i18n'
import { AsoExperimentKind, AsoExperimentOperation, AsoExperimentStatus, AsoStoreSupport, type AsoTimelineEntryDto } from './types'

export function kindLabel(kind: AsoExperimentKind, t: ExperimentsDictionary): string {
  switch (kind) {
    case AsoExperimentKind.ProductPageOptimization:
      return t.kind.ppo
    case AsoExperimentKind.CustomProductPage:
      return t.kind.cpp
    case AsoExperimentKind.PlayListingExperiment:
      return t.kind.playExperiment
    default:
      return t.kind.playCustomListing
  }
}

export function kindHint(kind: AsoExperimentKind, t: ExperimentsDictionary): string {
  switch (kind) {
    case AsoExperimentKind.ProductPageOptimization:
      return t.kindHint.ppo
    case AsoExperimentKind.CustomProductPage:
      return t.kindHint.cpp
    case AsoExperimentKind.PlayListingExperiment:
      return t.kindHint.playExperiment
    default:
      return t.kindHint.playCustomListing
  }
}

/** The status chip: the label says it, the tone only repeats it. */
export function statusView(status: AsoExperimentStatus, t: ExperimentsDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case AsoExperimentStatus.Draft:
      return { label: t.status.draft, tone: 'neutral' }
    case AsoExperimentStatus.Prepared:
      return { label: t.status.prepared, tone: 'info' }
    case AsoExperimentStatus.InReview:
      return { label: t.status.inReview, tone: 'warning' }
    case AsoExperimentStatus.Approved:
      return { label: t.status.approved, tone: 'accent' }
    case AsoExperimentStatus.Running:
      return { label: t.status.running, tone: 'success' }
    case AsoExperimentStatus.Rejected:
      return { label: t.status.rejected, tone: 'danger' }
    case AsoExperimentStatus.Stopped:
      return { label: t.status.stopped, tone: 'neutral' }
    default:
      return { label: t.status.completed, tone: 'solid' }
  }
}

export function supportLabel(support: AsoStoreSupport, t: ExperimentsDictionary): string {
  switch (support) {
    case AsoStoreSupport.Api:
      return t.support.api
    case AsoStoreSupport.Assisted:
      return t.support.assisted
    default:
      return t.support.notSupported
  }
}

const OPERATION_KEYS: Record<AsoExperimentOperation, keyof ExperimentsDictionary['detail']['operation']> = {
  [AsoExperimentOperation.Push]: 'push',
  [AsoExperimentOperation.Submit]: 'submit',
  [AsoExperimentOperation.Start]: 'start',
  [AsoExperimentOperation.Stop]: 'stop',
  [AsoExperimentOperation.ApplyWinner]: 'applyWinner',
}

export function operationLabel(operation: AsoExperimentOperation, t: ExperimentsDictionary): string {
  return t.detail.operation[OPERATION_KEYS[operation]]
}

export function operationHint(operation: AsoExperimentOperation, t: ExperimentsDictionary): string {
  return t.detail.operationHint[OPERATION_KEYS[operation]]
}

export function timelineTone(entry: AsoTimelineEntryDto): TimelineTone {
  if (entry.Kind === 'plan') {
    switch (entry.PlanStatus) {
      case ChangePlanStatus.Applied:
        return 'ok'
      case ChangePlanStatus.Failed:
      case ChangePlanStatus.Rejected:
        return 'dng'
      case ChangePlanStatus.Pending:
      case ChangePlanStatus.Approved:
        return 'now'
      default:
        return 'pending'
    }
  }
  if (entry.Kind === 'started') {
    return 'now'
  }
  return 'ok'
}
