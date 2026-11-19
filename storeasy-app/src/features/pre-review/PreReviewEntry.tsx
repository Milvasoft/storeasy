import { Link } from 'react-router-dom'
import { Sparkles } from 'lucide-react'
import { Badge, type BadgeTone } from '@/components/ui/Badge'
import { PreReviewStatus, PreReviewVerdict, type StoreKind } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { storeSlug } from '@/features/common/store'
import { usePreReviewHistory } from './api'
import { usePreReviewText, type PreReviewDictionary } from './i18n'

// The small pieces of the AI pre-review that other screens use (the submission steps), kept apart from the page so the
// page stays its own lazy chunk.

/** The pre-review section of an app, for links from the submission steps. */
export function preReviewHref(appId: number, store: StoreKind): string {
  return `/apps/${appId}/pre-review?store=${storeSlug(store)}`
}

export function verdictView(
  verdict: PreReviewVerdict | null | undefined,
  t: PreReviewDictionary,
): { label: string; tone: BadgeTone } | null {
  switch (verdict) {
    case PreReviewVerdict.LikelyApproved:
      return { label: t.verdict.likelyApproved, tone: 'success' }
    case PreReviewVerdict.AtRisk:
      return { label: t.verdict.atRisk, tone: 'warning' }
    case PreReviewVerdict.LikelyRejected:
      return { label: t.verdict.likelyRejected, tone: 'danger' }
    default:
      return null
  }
}

/**
 * The way into the pre-review from a submission step: a link to the section of this app and store, with the latest
 * verdict when there is one. Advice only; it never stops the submission.
 */
export function PreReviewEntry({ organizationId, appId, store }: { organizationId: number; appId: number; store: StoreKind }) {
  const t = usePreReviewText()
  const history = usePreReviewHistory(organizationId, appId, store)
  const latest = (history.data ?? []).find((item) => item.Status === PreReviewStatus.Succeeded)
  const view = latest ? verdictView(latest.Verdict, t) : null
  return (
    <div className="flex flex-wrap items-center gap-2 text-[13px]">
      <Sparkles className="h-4 w-4 text-fg3" aria-hidden />
      <Link className="lnk" to={preReviewHref(appId, store)}>
        {t.submitEntry}
      </Link>
      {latest && view ? (
        <Badge small tone={view.tone}>
          {interpolate(t.submitLatest, { verdict: view.label, percent: latest.ApprovalLikelihood ?? 0 })}
        </Badge>
      ) : null}
    </div>
  )
}
