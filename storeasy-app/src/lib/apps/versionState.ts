import type { BadgeTone } from '@/components/ui/Badge'
import { reviewPhase, type ReviewPhase } from '@/lib/activity/board'
import type { Dictionary } from '@/lib/i18n/en'

const TONES: Record<ReviewPhase, BadgeTone> = {
  waiting: 'info',
  inReview: 'info',
  pendingRelease: 'warning',
  pendingPublish: 'warning',
  rejected: 'danger',
  live: 'success',
  other: 'neutral',
}

/**
 * A store version state (App Store Connect names such as WAITING_FOR_REVIEW, a Microsoft Store submission status) as a
 * label and a badge tone: the board's words for the states it knows, otherwise the store's name made readable.
 */
export function versionStateView(state: string | null | undefined, t: Dictionary): { label: string; tone: BadgeTone } {
  const phase = reviewPhase(state)
  if (phase !== 'other') {
    return { label: t.activity.line.gateItem[phase], tone: TONES[phase] }
  }
  const raw = (state ?? '').trim()
  if (!raw) {
    return { label: '-', tone: 'neutral' }
  }
  const words = raw.replace(/_/g, ' ').toLowerCase()
  return { label: words[0].toUpperCase() + words.slice(1), tone: 'neutral' }
}
