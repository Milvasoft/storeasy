import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ClipboardCheck, Sparkles } from 'lucide-react'
import { Badge, type BadgeTone } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, EmptyState, PageHeader, Skeleton } from '@/components/ui/primitives'
import { ApiError, apiErrorMessage } from '@/lib/api/http'
import {
  OrgPermission,
  PreReviewArea,
  PreReviewSeverity,
  PreReviewStatus,
  type PreReviewVerdict,
  type PreReviewDto,
  type PreReviewFindingDto,
  type PreReviewListDto,
} from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { useAiSettings } from '@/features/ai/api'
import { AI_SETTINGS_ANCHOR, modelName } from '@/features/ai/AiSettingsCard'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useLiveRefresh } from '@/features/notifications/live'
import { isPreReviewActive, preReviewApi, preReviewKeys, usePreReview, usePreReviewHistory } from './api'
import { usePreReviewText, type PreReviewDictionary } from './i18n'
import { verdictView } from './PreReviewEntry'

// The AI pre-review of an app in one store: run it (a background job), read the latest report, go back to earlier ones.
// It is advice only: nothing here blocks or performs a submission.

const LIVE_TYPES = ['job.succeeded', 'job.failed', 'job.cancelled'] as const

function areaLabel(area: PreReviewArea, t: PreReviewDictionary): string {
  switch (area) {
    case PreReviewArea.Metadata:
      return t.area.metadata
    case PreReviewArea.Screenshots:
      return t.area.screenshots
    case PreReviewArea.Privacy:
      return t.area.privacy
    case PreReviewArea.AgeRating:
      return t.area.ageRating
    case PreReviewArea.Payments:
      return t.area.payments
    case PreReviewArea.Functionality:
      return t.area.functionality
    case PreReviewArea.Legal:
      return t.area.legal
    default:
      return t.area.other
  }
}

const SEVERITIES: Array<{ severity: PreReviewSeverity; tone: BadgeTone; label: (t: PreReviewDictionary) => string }> = [
  { severity: PreReviewSeverity.Blocker, tone: 'danger', label: (t) => t.severity.blocker },
  { severity: PreReviewSeverity.Major, tone: 'warning', label: (t) => t.severity.major },
  { severity: PreReviewSeverity.Minor, tone: 'neutral', label: (t) => t.severity.minor },
]

/** Dollars with enough digits to show a cost of a fraction of a cent. */
export function formatCost(value: number | null | undefined): string {
  const amount = value ?? 0
  return amount >= 1 ? amount.toFixed(2) : amount.toFixed(4)
}

function StatusBadge({ item, t }: { item: { Status: PreReviewStatus; Verdict?: PreReviewVerdict | null }; t: PreReviewDictionary }) {
  if (item.Status === PreReviewStatus.Queued) {
    return <Badge small>{t.status.queued}</Badge>
  }
  if (item.Status === PreReviewStatus.Running) {
    return (
      <Badge small tone="info" live>
        {t.status.running}
      </Badge>
    )
  }
  if (item.Status === PreReviewStatus.Failed) {
    return (
      <Badge small tone="danger">
        {t.status.failed}
      </Badge>
    )
  }
  const view = verdictView(item.Verdict, t)
  return view ? (
    <Badge small tone={view.tone} dot>
      {view.label}
    </Badge>
  ) : null
}

function FindingItem({ finding, t }: { finding: PreReviewFindingDto; t: PreReviewDictionary }) {
  return (
    <li className="ck-i items-start">
      <div className="grid min-w-0 flex-1 gap-1">
        <b className="text-[13.5px]">{finding.Title ?? areaLabel(finding.Area, t)}</b>
        <div className="flex flex-wrap gap-2 text-xs text-fg3">
          <Badge small>{areaLabel(finding.Area, t)}</Badge>
          {finding.Guideline ? (
            <span>
              {t.guideline}: <span className="mono">{finding.Guideline}</span>
            </span>
          ) : null}
        </div>
        {finding.Evidence ? (
          <div className="text-[12.5px] text-fg2">
            <b>{t.evidence}:</b> {finding.Evidence}
          </div>
        ) : null}
        {finding.Fix ? (
          <div className="text-[12.5px] text-fg">
            <b>{t.fix}:</b> {finding.Fix}
          </div>
        ) : null}
      </div>
    </li>
  )
}

function Report({ report }: { report: PreReviewDto }) {
  const t = usePreReviewText()
  const format = useFormat()

  if (isPreReviewActive(report.Status)) {
    return (
      <section className="card" aria-busy="true">
        <div className="card-b grid gap-2">
          <b>{report.Status === PreReviewStatus.Queued ? t.queued : t.running}</b>
          <span className="text-[13px] text-fg2">{t.runningHint}</span>
          <Skeleton className="h-16" />
        </div>
      </section>
    )
  }

  if (report.Status === PreReviewStatus.Failed) {
    return (
      <Callout tone="danger" title={t.failed}>
        {report.ErrorMessage ?? report.ErrorKey ?? ''}
      </Callout>
    )
  }

  const view = verdictView(report.Verdict, t)
  const findings = report.Findings ?? []
  const runtime = report.RuntimeChecks ?? []
  const positive = report.PositiveNotes ?? []

  return (
    <div className="grid gap-4">
      <section className="card" aria-label={t.summary}>
        <div className="card-b grid gap-3">
          <div className="flex flex-wrap items-center gap-3">
            {view ? (
              <Badge tone={view.tone} dot>
                {view.label}
              </Badge>
            ) : null}
            {report.ApprovalLikelihood != null ? (
              <span className="text-2xl font-semibold tabular-nums" aria-label={t.likelihoodLabel}>
                {interpolate(t.likelihood, { percent: report.ApprovalLikelihood })}
              </span>
            ) : null}
          </div>
          {report.ApprovalLikelihood != null ? (
            <div
              className="h-2 overflow-hidden rounded-full bg-bg2"
              role="meter"
              aria-label={t.likelihoodLabel}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={report.ApprovalLikelihood}
            >
              <div
                className={cn('h-full', view?.tone === 'success' ? 'bg-success' : view?.tone === 'danger' ? 'bg-danger' : 'bg-warning')}
                style={{ width: `${Math.max(0, Math.min(100, report.ApprovalLikelihood))}%` }}
              />
            </div>
          ) : null}
          {report.Summary ? <p className="m-0 text-[13.5px]">{report.Summary}</p> : null}
          <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs text-fg3">
            {report.VersionString ? <span className="mono">{interpolate(t.version, { version: report.VersionString })}</span> : null}
            <span>{interpolate(t.model, { model: report.Model })}</span>
            {report.ImageCount ? <span>{interpolate(t.images, { count: report.ImageCount })}</span> : null}
            <span>
              {interpolate(t.usage, {
                input: format.number(report.InputTokens ?? 0),
                output: format.number(report.OutputTokens ?? 0),
                cost: formatCost(report.EstimatedCostUsd),
              })}
            </span>
            {report.CompletedDate ? <span>{format.dateTime(report.CompletedDate)}</span> : null}
          </div>
        </div>
      </section>

      <section className="card" aria-label={t.findings}>
        <div className="card-h">
          <h2 className="h3">{t.findings}</h2>
        </div>
        {findings.length === 0 ? (
          <div className="card-b text-[13px] text-fg2">{t.noFindings}</div>
        ) : (
          <div className="grid gap-1 px-4 py-2">
            {SEVERITIES.map(({ severity, tone, label }) => {
              const group = findings.filter((finding) => finding.Severity === severity)
              if (group.length === 0) {
                return null
              }
              return (
                <section key={severity} aria-label={label(t)}>
                  <h3 className="h4 my-2 flex items-center gap-2">
                    <Badge small tone={tone}>
                      {label(t)}
                    </Badge>
                    <span className="text-xs text-fg3">{group.length}</span>
                  </h3>
                  <ul className="m-0 grid list-none p-0">
                    {group.map((finding, index) => (
                      <FindingItem key={index} finding={finding} t={t} />
                    ))}
                  </ul>
                </section>
              )
            })}
          </div>
        )}
      </section>

      {runtime.length > 0 ? (
        <section className="card" aria-label={t.runtime}>
          <div className="card-h">
            <h2 className="h3">{t.runtime}</h2>
          </div>
          <div className="card-b grid gap-2">
            <span className="text-xs text-fg3">{t.runtimeHint}</span>
            <ul className="m-0 grid gap-1 pl-5 text-[13px]">
              {runtime.map((check, index) => (
                <li key={index}>{check}</li>
              ))}
            </ul>
          </div>
        </section>
      ) : null}

      {positive.length > 0 ? (
        <section className="card" aria-label={t.positive}>
          <div className="card-h">
            <h2 className="h3">{t.positive}</h2>
          </div>
          <ul className="card-b m-0 grid gap-1 pl-9 text-[13px]">
            {positive.map((note, index) => (
              <li key={index}>{note}</li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  )
}

function History({
  items,
  selectedId,
  onSelect,
}: {
  items: PreReviewListDto[]
  selectedId: number | null
  onSelect: (id: number) => void
}) {
  const t = usePreReviewText()
  const format = useFormat()
  return (
    <section className="card" aria-label={t.history}>
      <div className="card-h">
        <h2 className="h3">{t.history}</h2>
      </div>
      {items.length === 0 ? (
        <div className="card-b text-[13px] text-fg2">{t.historyEmpty}</div>
      ) : (
        <ul className="m-0 grid list-none p-2">
          {items.map((item) => {
            const on = item.Id === selectedId
            return (
              <li key={item.Id}>
                <button
                  type="button"
                  className={cn('grid w-full gap-1 rounded-md px-2 py-2 text-left hover:bg-bg2', on && 'bg-accent/10')}
                  aria-current={on ? 'true' : undefined}
                  onClick={() => onSelect(item.Id)}
                >
                  <span className="flex flex-wrap items-center gap-2">
                    <StatusBadge item={item} t={t} />
                    {item.ApprovalLikelihood != null && item.Status === PreReviewStatus.Succeeded ? (
                      <b className="tabular-nums">{interpolate(t.percent, { percent: item.ApprovalLikelihood })}</b>
                    ) : null}
                    {item.VersionString ? <span className="mono text-xs text-fg3">{item.VersionString}</span> : null}
                  </span>
                  <span className="text-xs text-fg3">
                    {interpolate(t.historyItem, { date: format.dateTime(item.CreatedDate), user: item.CreatedByUserName ?? '-' })}
                  </span>
                  {item.Status === PreReviewStatus.Succeeded ? (
                    <span className="text-xs text-fg3">
                      {interpolate(t.counts, { blockers: item.BlockerCount, major: item.MajorCount, minor: item.MinorCount })}
                    </span>
                  ) : null}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}

function PreReview({ context }: { context: WorkspaceContext }) {
  const t = usePreReviewText()
  const base = useT()
  const queryClient = useQueryClient()
  const { app, organizationId, store, can } = context
  const settings = useAiSettings(organizationId)
  const history = usePreReviewHistory(organizationId, app.Id, store)
  const [selected, setSelected] = useState<number | null>(null)
  const selectedId = selected ?? history.data?.[0]?.Id ?? null
  const report = usePreReview(organizationId, selectedId)
  const [error, setError] = useState<string | null>(null)
  useLiveRefresh(LIVE_TYPES, preReviewKeys.all)

  // When the shown pre-review finishes, the history row changes too.
  const lastStatus = useRef<PreReviewStatus | null>(null)
  const status = report.data?.Status ?? null
  useEffect(() => {
    if (lastStatus.current != null && isPreReviewActive(lastStatus.current) && !isPreReviewActive(status)) {
      void queryClient.invalidateQueries({ queryKey: preReviewKeys.list(organizationId, app.Id, store) })
    }
    lastStatus.current = status
  }, [status, queryClient, organizationId, app.Id, store])

  const start = useMutation({
    mutationFn: () => preReviewApi.start(organizationId, app.Id, store),
    meta: { silentError: true },
    onSuccess: (created) => {
      setError(null)
      setSelected(created.Id)
      queryClient.setQueryData(preReviewKeys.detail(organizationId, created.Id), created)
      void queryClient.invalidateQueries({ queryKey: preReviewKeys.list(organizationId, app.Id, store) })
      void queryClient.invalidateQueries({ queryKey: preReviewKeys.detail(organizationId, created.Id) })
      showToast(t.started, 'success')
    },
    onError: (failure) => {
      if (failure instanceof ApiError && failure.keys.includes('PreReviewInProgress')) {
        setError(t.inProgress)
        void queryClient.invalidateQueries({ queryKey: preReviewKeys.list(organizationId, app.Id, store) })
        return
      }
      setError(apiErrorMessage(failure, base.common.error))
    },
  })

  const hasKey = settings.data?.HasApiKey ?? false
  const editor = can(OrgPermission.EditContent)
  const running = (history.data ?? []).some((item) => isPreReviewActive(item.Status)) || isPreReviewActive(report.data?.Status)
  const model = settings.data ? modelName(settings.data.Models, settings.data.PreReviewModel) : null

  return (
    <>
      <PageHeader
        title={t.title}
        subtitle={t.subtitle}
        actions={
          <Button
            variant="primary"
            loading={start.isPending}
            disabled={!hasKey || !editor || running || settings.isLoading}
            title={!hasKey ? t.noKeyHint : !editor ? t.noPermission : undefined}
            onClick={() => start.mutate()}
          >
            <Sparkles aria-hidden />
            {running ? t.running : t.run}
          </Button>
        }
      />
      <div className="mb-3 grid gap-2">
        {model && hasKey ? <span className="text-xs text-fg3">{interpolate(t.model, { model })}</span> : null}
        {settings.data && !hasKey ? (
          <p className="m-0 text-xs text-fg3">
            {t.noKeyHint}{' '}
            <Link className="lnk" to={`/settings#${AI_SETTINGS_ANCHOR}`}>
              {t.noKeyLink}
            </Link>
          </p>
        ) : null}
        {hasKey && !editor ? <p className="m-0 text-xs text-fg3">{t.noPermission}</p> : null}
        {error ? <Callout tone="danger">{error}</Callout> : null}
        <Callout tone="info">{t.disclaimer}</Callout>
      </div>
      {history.isLoading ? (
        <CardSkeleton />
      ) : history.isError ? (
        <ScreenState error={history.error} onRetry={() => void history.refetch()} />
      ) : (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_280px]">
          <div className="min-w-0">
            {selectedId == null ? (
              <EmptyState icon={<ClipboardCheck aria-hidden />} title={t.empty} />
            ) : report.isLoading ? (
              <CardSkeleton />
            ) : report.isError || !report.data ? (
              <ScreenState error={report.error} onRetry={() => void report.refetch()} />
            ) : (
              <Report report={report.data} />
            )}
          </div>
          <History items={history.data ?? []} selectedId={selectedId} onSelect={setSelected} />
        </div>
      )}
    </>
  )
}

export function PreReviewPage() {
  return <AppWorkspace section="preReview">{(context) => <PreReview key={context.store} context={context} />}</AppWorkspace>
}
