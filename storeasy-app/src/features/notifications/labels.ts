import type { BadgeTone } from '@/components/ui/Badge'
import type { NotificationsDictionary } from './i18n'
import { NotificationType } from './types'

type TypeKey = keyof NotificationsDictionary['type']

function typeKey(type: NotificationType): TypeKey {
  switch (type) {
    case NotificationType.Rejection:
      return 'rejection'
    case NotificationType.CertificationFailed:
      return 'certificationFailed'
    case NotificationType.JobSucceeded:
      return 'jobSucceeded'
    case NotificationType.JobFailed:
      return 'jobFailed'
    case NotificationType.PlanPending:
      return 'planPending'
    case NotificationType.Deadline:
      return 'deadline'
    case NotificationType.NewReview:
      return 'newReview'
    case NotificationType.ReviewReplyFailed:
      return 'reviewReplyFailed'
    case NotificationType.Invitation:
      return 'invitation'
    case NotificationType.ConnectionAlert:
      return 'connectionAlert'
    case NotificationType.ArtifactRejected:
      return 'artifactRejected'
    case NotificationType.RuleChanged:
      return 'ruleChanged'
    case NotificationType.RuleProposal:
      return 'ruleProposal'
    case NotificationType.VersionState:
      return 'versionState'
    case NotificationType.PackageRegistry:
      return 'packageRegistry'
    default:
      return 'other'
  }
}

export function notificationTypeLabel(type: NotificationType, t: NotificationsDictionary): string {
  return t.type[typeKey(type)]
}

export function notificationTypeDescription(type: NotificationType, t: NotificationsDictionary): string {
  return t.describe[typeKey(type)]
}

// The badge color says the kind; the text says it too.
export function notificationTone(type: NotificationType): BadgeTone {
  switch (type) {
    case NotificationType.Rejection:
    case NotificationType.CertificationFailed:
    case NotificationType.JobFailed:
    case NotificationType.ReviewReplyFailed:
    case NotificationType.ArtifactRejected:
    case NotificationType.ConnectionAlert:
      return 'danger'
    case NotificationType.Deadline:
    case NotificationType.PlanPending:
      return 'warning'
    case NotificationType.JobSucceeded:
      return 'success'
    case NotificationType.NewReview:
    case NotificationType.RuleChanged:
    case NotificationType.RuleProposal:
      return 'info'
    default:
      return 'neutral'
  }
}

/** The in-app path of an ActionLink: a path stays, a URL of this app becomes its path, anything else is external. */
export function actionPath(link: string | null | undefined): { internal: boolean; href: string } | null {
  if (!link) {
    return null
  }
  if (link.startsWith('/')) {
    return { internal: true, href: link }
  }
  try {
    const url = new URL(link)
    if (url.origin === window.location.origin) {
      return { internal: true, href: `${url.pathname}${url.search}${url.hash}` }
    }
    return { internal: false, href: url.toString() }
  } catch {
    return null
  }
}

// The filter chips of the notification center (design 14.20).
export const FILTER_TYPES: readonly NotificationType[] = [
  NotificationType.Rejection,
  NotificationType.JobSucceeded,
  NotificationType.JobFailed,
  NotificationType.Deadline,
  NotificationType.NewReview,
  NotificationType.Invitation,
]
