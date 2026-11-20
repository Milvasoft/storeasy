import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ExternalLink, Send } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { Timeline, type TimelineTone } from '@/components/ui/Progress'
import { ScreenState } from '@/components/ui/ScreenState'
import { CardSkeleton, EmptyState, PageHeader } from '@/components/ui/primitives'
import { StoreKind } from '@/lib/api/types'
import { storeLinksOf } from '@/lib/apps/storeLinks'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { msApi } from './api'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import { percentText, statusLabel, statusTone, targetLabel } from './labels'
import { MsSubmitDialog, MsWorkspace, SubmitButton, submitTargetOf, type MsContext, type SubmitTarget } from './MsWorkspace'
import { FAILED_STATUSES, isSubmittableDraft } from './rules'
import { MsSubmissionStatus, type MsSubmissionDto } from './types'

// Design 8.22: the submissions of a product with their status machine, errors, warnings and certification reports, and
// sending a draft on (typed Store id, SubmitForReview).

function historyTone(status: MsSubmissionStatus, last: boolean): TimelineTone {
  if (FAILED_STATUSES.includes(status)) {
    return 'dng'
  }
  if (status === MsSubmissionStatus.Published) {
    return 'ok'
  }
  return last ? 'now' : 'ok'
}

/** One submission: status, where it came from, its history and certification. `onSubmit` shows the send button. */
export function SubmissionCard({
  submission,
  context,
  onSubmit,
}: {
  submission: MsSubmissionDto
  context: MsContext
  onSubmit?: (target: SubmitTarget) => void
}) {
  const t = useMs()
  const format = useFormat()
  const history = submission.StatusHistory ?? []
  const details = submission.StatusDetails
  const errors = details?.Errors ?? []
  const warnings = details?.Warnings ?? []
  const reports = details?.CertificationReports ?? []
  const name = submission.FriendlyName ?? submission.PackageVersion ?? submission.SubmissionId ?? '-'
  return (
    <article className="card" aria-label={name}>
      <div className="card-h flex-wrap">
        <h3 className="h3 min-w-0 flex-1 truncate">{name}</h3>
        <Badge tone={statusTone(submission.Status)} dot>
          {statusLabel(submission.Status, t)}
        </Badge>
      </div>
      <div className="card-b grid gap-3">
        <dl className="kv text-[13px]">
          <dt>{t.submissions.target}</dt>
          <dd>{targetLabel(submission.Target, t)}</dd>
          <dt>{t.submissions.version}</dt>
          <dd className="mono">{submission.PackageVersion ?? '-'}</dd>
          <dt>{t.submissions.id}</dt>
          <dd className="mono">{submission.SubmissionId ?? '-'}</dd>
        </dl>
        <div className="flex flex-wrap gap-2">
          <Badge small>{submission.CreatedByStoreasy ? t.submissions.createdHere : t.submissions.createdElsewhere}</Badge>
          {submission.ModifiedOutsideApi ? (
            <Badge small tone="danger">
              {t.submissions.edited}
            </Badge>
          ) : null}
          {submission.RolloutPercentage !== null && submission.RolloutPercentage !== undefined ? (
            <Badge small tone="info">
              {interpolate(t.submissions.rollout, { percent: percentText(submission.RolloutPercentage, t) })}
            </Badge>
          ) : null}
        </div>
        {submission.IsPending && submission.Status === MsSubmissionStatus.PendingPublication ? (
          <Callout tone="warning" title={t.submissions.pendingPublishTitle}>
            <span className="block">{t.submissions.pendingPublishBody}</span>
            <a
              className="lnk mt-1 inline-flex items-center gap-1"
              href={storeLinksOf({ Store: StoreKind.Microsoft, ExternalId: context.product.ProductId ?? null, BundleId: null }).consoleUrl}
              target="_blank"
              rel="noopener noreferrer"
            >
              <ExternalLink className="h-3.5 w-3.5" aria-hidden />
              {t.submissions.publishNow}
            </a>
          </Callout>
        ) : null}
        {history.length > 0 ? (
          <section aria-label={t.submissions.history}>
            <h4 className="lbl mb-1">{t.submissions.history}</h4>
            <Timeline
              items={history.map((change, index) => ({
                id: `${change.Status}-${index}`,
                tone: historyTone(change.Status, index === history.length - 1),
                title: statusLabel(change.Status, t),
                meta: format.dateTime(change.Date),
              }))}
            />
          </section>
        ) : null}
        {errors.length > 0 ? (
          <Callout tone="danger" title={t.submissions.errors}>
            <ul className="m-0 grid list-none gap-1 p-0">
              {errors.map((item, index) => (
                <li key={`${item.Code}-${index}`}>
                  {item.Code ? <span className="mono">{item.Code} · </span> : null}
                  {item.Details ?? ''}
                </li>
              ))}
            </ul>
          </Callout>
        ) : null}
        {warnings.length > 0 ? (
          <Callout tone="warning" title={t.submissions.warnings}>
            <ul className="m-0 grid list-none gap-1 p-0">
              {warnings.map((item, index) => (
                <li key={`${item.Code}-${index}`}>
                  {item.Code ? <span className="mono">{item.Code} · </span> : null}
                  {item.Details ?? ''}
                </li>
              ))}
            </ul>
          </Callout>
        ) : null}
        {reports.length > 0 ? (
          <section aria-label={t.submissions.reports}>
            <h4 className="lbl mb-1">{t.submissions.reports}</h4>
            <ul className="m-0 grid list-none gap-1 p-0 text-[13px]">
              {reports.map((report, index) =>
                report.ReportUrl ? (
                  <li key={index}>
                    <a className="lnk inline-flex items-center gap-1" href={report.ReportUrl} target="_blank" rel="noreferrer">
                      <ExternalLink className="h-3.5 w-3.5" aria-hidden />
                      {interpolate(t.submissions.report, { date: format.dateTime(report.Date) })}
                    </a>
                  </li>
                ) : null,
              )}
            </ul>
            <p className="m-0 mt-1 text-xs text-fg3">{t.submissions.reportNote}</p>
          </section>
        ) : null}
      </div>
      {onSubmit && isSubmittableDraft(submission) ? (
        <div className="card-f justify-end">
          <SubmitButton context={context} onClick={() => onSubmit(submitTargetOf(submission, name))} />
        </div>
      ) : null}
    </article>
  )
}

function Submissions({ context }: { context: MsContext }) {
  const t = useMs()
  const { organizationId, record, product } = context
  const [target, setTarget] = useState<SubmitTarget | null>(null)
  const submissions = useQuery({
    queryKey: msKeys.submissions(organizationId, record.StoreAppId),
    queryFn: () => msApi.submissions(organizationId, record.StoreAppId),
  })

  if (submissions.isLoading) {
    return <CardSkeleton />
  }
  if (submissions.isError) {
    return <ScreenState error={submissions.error} onRetry={() => void submissions.refetch()} />
  }
  const list = submissions.data ?? []
  return (
    <>
      <PageHeader title={t.submissions.title} subtitle={t.submissions.lead} />
      {list.length === 0 ? (
        <EmptyState icon={<Send aria-hidden />} title={t.submissions.none} />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {list.map((submission) => (
            <SubmissionCard key={submission.Id} submission={submission} context={context} onSubmit={setTarget} />
          ))}
        </div>
      )}
      <MsSubmitDialog target={target} onClose={() => setTarget(null)} organizationId={organizationId} product={product} />
    </>
  )
}

export function MsSubmissionsPage() {
  return <MsWorkspace sub="submissions">{(context) => <Submissions context={context} />}</MsWorkspace>
}
