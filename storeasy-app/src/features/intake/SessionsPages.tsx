import { useMemo, type ReactNode } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Inbox, Send, Trash2 } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { Banner, Callout } from '@/components/ui/Feedback'
import { Checklist, Progress, Timeline, type ChecklistState, type TimelineTone } from '@/components/ui/Progress'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, EmptyState, TableSkeleton } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { SortType, type ListRequest } from '@/lib/api/list'
import { OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { intakeApi, intakeKeys, type SessionFilter } from './api'
import { useIntakeText, type IntakeDictionary } from './i18n'
import { IntakeShell, useIntakeApps, useLivePoll } from './IntakeShell'
import { SESSION_STATUSES, SOURCES, isFinal, platformLabel, sourceLabel, sourceText, statusView, storeOf } from './labels'
import {
  ArtifactSessionStatus,
  ArtifactStepKind,
  ArtifactStepStatus,
  CheckStatus,
  DeliveryTarget,
  IdentityKind,
  type ArtifactSessionDetailDto,
  type ArtifactSessionListDto,
  type ArtifactSessionStatus as Status,
  type ArtifactSource,
  type ArtifactStep,
} from './types'

// Design 13.13-13.17: the upload sessions with filters and live status, and one session with its steps, what was read
// from the package, the checks, the applied rule, the identity and the result for the CI. A held session can be
// delivered and a finished one's stored package deleted (ReleaseTest), each after a confirmation.

const DEFAULT_PAGE_SIZE = 20

function numberParam(params: URLSearchParams, name: string): number | null {
  const value = Number(params.get(name))
  return Number.isInteger(value) && value > 0 ? value : null
}

function hasRunning(list: ArtifactSessionListDto[] | undefined): boolean {
  return (list ?? []).some((session) => !isFinal(session.Status))
}

function LiveNote() {
  const t = useIntakeText()
  const { connection } = useActivity()
  return connection.state === 'connected' ? null : <Banner tone="info">{t.sessions.polling}</Banner>
}

export function IntakeSessionsPage() {
  const t = useIntakeText()
  const base = useT()
  const format = useFormat()
  const navigate = useNavigate()
  const organization = useCurrentOrganization()
  const apps = useIntakeApps()
  const [params, setParams] = useSearchParams()
  const filter: SessionFilter = {
    AppId: numberParam(params, 'app'),
    Status: numberParam(params, 'status') as Status | null,
    Source: numberParam(params, 'source') as ArtifactSource | null,
  }
  const page = numberParam(params, 'page') ?? 1
  const size = numberParam(params, 'size') ?? DEFAULT_PAGE_SIZE
  const request = useMemo<ListRequest & SessionFilter>(
    () => ({
      PageNumber: page,
      RowCount: size,
      Sorting: { SortBy: 'StartDate', Type: SortType.Desc },
      AppId: filter.AppId,
      Status: filter.Status,
      Source: filter.Source,
    }),
    [page, size, filter.AppId, filter.Status, filter.Source],
  )
  const poll = useLivePoll<{ Data?: ArtifactSessionListDto[] | null }>((data) => hasRunning(data?.Data ?? undefined))
  const sessions = useQuery({
    queryKey: intakeKeys.sessions(organization.Id, request),
    queryFn: () => intakeApi.sessions(organization.Id, request),
    placeholderData: keepPreviousData,
    refetchInterval: poll,
  })

  const update = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value === null) {
        next.delete(key)
      } else {
        next.set(key, value)
      }
    }
    setParams(next, { replace: true })
  }

  const columns: Column<ArtifactSessionListDto>[] = [
    { key: 'app', header: t.sessions.app, cell: (session) => session.AppName ?? `#${session.AppId}` },
    {
      key: 'file',
      header: t.sessions.file,
      role: 'main',
      cell: (session) => (
        <Link className="lnk mono break-all" to={`/intake/sessions/${session.Id}`} onClick={(event) => event.stopPropagation()}>
          {session.FileName}
        </Link>
      ),
    },
    { key: 'source', header: t.sessions.source, cell: (session) => sourceText(session, t) },
    {
      key: 'size',
      header: t.sessions.size,
      role: 'hidePhone',
      cell: (session) => <span className="tabular-nums">{format.bytes(session.Size)}</span>,
    },
    {
      key: 'sha',
      header: t.sessions.sha,
      role: 'hidePhone',
      cell: (session) => <code className="mono">{(session.Sha256 ?? '').slice(0, 8)}</code>,
    },
    {
      key: 'status',
      header: t.sessions.status,
      cell: (session) => {
        const view = statusView(session.Status, t)
        return (
          <Badge small dot tone={view.tone}>
            {view.label}
          </Badge>
        )
      },
    },
    { key: 'started', header: t.sessions.started, cell: (session) => format.dateTime(session.StartDate) },
  ]

  const rows = sessions.data?.Data ?? []
  const filtered = filter.AppId !== null || filter.Status !== null || filter.Source !== null
  return (
    <IntakeShell>
      <div className="flex flex-wrap items-center gap-2">
        <Select<number>
          aria-label={t.sessions.app}
          className="w-auto min-w-[160px]"
          value={filter.AppId ?? 0}
          options={[{ value: 0, label: t.sessions.allApps }, ...(apps.data ?? []).map((app) => ({ value: app.Id, label: app.Name }))]}
          onChange={(value) => update({ app: value ? String(value) : null, page: null })}
        />
        <Select<number>
          aria-label={t.sessions.status}
          className="w-auto min-w-[160px]"
          value={filter.Status ?? 0}
          options={[
            { value: 0, label: t.sessions.allStatuses },
            ...SESSION_STATUSES.map((status) => ({ value: status, label: statusView(status, t).label })),
          ]}
          onChange={(value) => update({ status: value ? String(value) : null, page: null })}
        />
        <Select<number>
          aria-label={t.sessions.source}
          className="w-auto min-w-[160px]"
          value={filter.Source ?? 0}
          options={[
            { value: 0, label: t.sessions.allSources },
            ...SOURCES.map((source) => ({ value: source, label: sourceLabel(source, t) })),
          ]}
          onChange={(value) => update({ source: value ? String(value) : null, page: null })}
        />
        {filtered ? (
          <Button size="sm" variant="ghost" onClick={() => update({ app: null, status: null, source: null, page: null })}>
            {base.common.clearFilters}
          </Button>
        ) : null}
      </div>
      {hasRunning(rows) ? <LiveNote /> : null}
      {sessions.isLoading ? (
        <TableSkeleton columns={[t.sessions.app, t.sessions.file, t.sessions.status, t.sessions.started]} label={base.common.loading} />
      ) : sessions.isError ? (
        <ScreenState error={sessions.error} onRetry={() => void sessions.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={<Inbox aria-hidden />} title={t.sessions.emptyTitle} description={t.sessions.emptyBody} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={rows}
            rowKey={(session) => session.Id}
            caption={t.sessions.caption}
            busy={sessions.isFetching && sessions.isPlaceholderData}
            onRowClick={(session) => navigate(`/intake/sessions/${session.Id}`)}
          />
          <Pagination
            page={sessions.data?.CurrentPageNumber ?? page}
            pageCount={sessions.data?.TotalPageCount ?? 1}
            total={sessions.data?.TotalDataCount ?? rows.length}
            pageSize={size}
            onPage={(next) => update({ page: String(next) })}
            onPageSize={(next) => update({ size: String(next), page: null })}
          />
        </>
      )}
    </IntakeShell>
  )
}

// ---- Detail ----------------------------------------------------------------------------------------------------------

function stepTitle(kind: ArtifactStepKind, t: IntakeDictionary): string {
  switch (kind) {
    case ArtifactStepKind.Upload:
      return t.detail.step.upload
    case ArtifactStepKind.Parse:
      return t.detail.step.parse
    case ArtifactStepKind.Check:
      return t.detail.step.check
    case ArtifactStepKind.Rule:
      return t.detail.step.rule
    case ArtifactStepKind.Deliver:
      return t.detail.step.deliver
    default:
      return t.detail.step.report
  }
}

function stepView(status: ArtifactStepStatus, t: IntakeDictionary): { tone: TimelineTone; label: string } {
  switch (status) {
    case ArtifactStepStatus.Done:
      return { tone: 'ok', label: t.detail.stepStatus.done }
    case ArtifactStepStatus.Running:
      return { tone: 'now', label: t.detail.stepStatus.running }
    case ArtifactStepStatus.Failed:
      return { tone: 'dng', label: t.detail.stepStatus.failed }
    case ArtifactStepStatus.Skipped:
      return { tone: 'pending', label: t.detail.stepStatus.skipped }
    default:
      return { tone: 'pending', label: t.detail.stepStatus.pending }
  }
}

function checkState(status: CheckStatus): ChecklistState {
  return status === CheckStatus.Missing
    ? 'missing'
    : status === CheckStatus.Warning
      ? 'warning'
      : status === CheckStatus.Assisted
        ? 'assisted'
        : 'ok'
}

function StepItem({ step }: { step: ArtifactStep }) {
  const t = useIntakeText()
  const format = useFormat()
  const view = stepView(step.Status, t)
  const times = [step.StartDate ? format.time(step.StartDate) : null, step.EndDate ? format.time(step.EndDate) : null]
    .filter(Boolean)
    .join(' → ')
  return (
    <div className="grid gap-1">
      <span className="flex flex-wrap items-center gap-2">
        <b className="text-[13.5px]">{stepTitle(step.Kind, t)}</b>
        <span className="text-xs text-fg3">{view.label}</span>
      </span>
      {step.Message ? <span className="text-[13px] text-fg2">{step.Message}</span> : null}
      {step.Status === ArtifactStepStatus.Running && step.Total ? (
        <span className="grid gap-1">
          <Progress small value={step.Bytes ?? 0} max={step.Total} label={stepTitle(step.Kind, t)} />
          <span className="mono text-xs text-fg3">
            {format.bytes(step.Bytes ?? 0)} / {format.bytes(step.Total)}
          </span>
        </span>
      ) : null}
      {step.ResumeCount ? <span className="text-xs text-fg3">{interpolate(t.detail.resumed, { count: step.ResumeCount })}</span> : null}
      {times ? <span className="mono text-xs text-fg3">{times}</span> : null}
    </div>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid gap-0.5">
      <dt className="text-xs text-fg3">{label}</dt>
      <dd className="break-words">{children}</dd>
    </div>
  )
}

function identityText(session: ArtifactSessionDetailDto, t: IntakeDictionary): string | null {
  const identity = session.Identity
  switch (identity?.Kind) {
    case IdentityKind.Oidc:
      return interpolate(t.detail.identityOidc, { id: identity.TrustedPublisherId ?? '-' })
    case IdentityKind.CiToken:
      return interpolate(t.detail.identityCi, { prefix: identity.CiTokenPrefix ?? '' })
    case IdentityKind.User:
      return interpolate(t.detail.identityUser, { name: identity.UserName ?? '' })
    default:
      return null
  }
}

export function IntakeSessionPage() {
  const t = useIntakeText()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const sessionId = Number(useParams().sessionId)
  const poll = useLivePoll<ArtifactSessionDetailDto>((data) => !!data && !isFinal(data.Status))
  const detail = useQuery({
    queryKey: intakeKeys.session(organization.Id, sessionId),
    queryFn: () => intakeApi.session(organization.Id, sessionId),
    enabled: Number.isInteger(sessionId) && sessionId > 0,
    refetchInterval: poll,
  })
  const refresh = () => void queryClient.invalidateQueries({ queryKey: intakeKeys.all })
  const deliver = useMutation({
    mutationFn: () => intakeApi.deliver(organization.Id, sessionId),
    onSuccess: (session) => {
      queryClient.setQueryData(intakeKeys.session(organization.Id, sessionId), session)
      showToast(t.detail.delivered, 'success')
      refresh()
    },
  })
  const remove = useMutation({
    mutationFn: () => intakeApi.deleteSession(organization.Id, sessionId),
    onSuccess: () => {
      showToast(t.detail.deleted, 'success')
      refresh()
    },
  })

  const session = detail.data
  if (detail.isLoading) {
    return (
      <IntakeShell>
        <CardSkeleton />
      </IntakeShell>
    )
  }
  if (detail.isError || !session) {
    return (
      <IntakeShell>
        <ScreenState error={detail.error} onRetry={() => void detail.refetch()} />
      </IntakeShell>
    )
  }

  const view = statusView(session.Status, t)
  const store = storeOf(session.Platform)
  const releaseTest = can(OrgPermission.ReleaseTest)
  const rule = session.AppliedRule
  const target = rule && rule.Target !== DeliveryTarget.None ? (rule.TargetName ?? rule.TargetId ?? '') : t.detail.deliverNoRule
  const checks = session.Checks ?? []
  const okChecks = checks.filter((check) => check.Status !== CheckStatus.Missing).length
  const ref = session.SourceRef
  const identity = identityText(session, t)

  const askDeliver = async () => {
    if (
      await confirm({
        title: interpolate(t.detail.deliverTitle, { file: session.FileName ?? '' }),
        message: interpolate(t.detail.deliverBody, { target }),
        confirmLabel: t.detail.deliver,
      })
    ) {
      deliver.mutate()
    }
  }
  const askDelete = async () => {
    if (
      await confirm({
        title: interpolate(t.detail.deleteTitle, { file: session.FileName ?? '' }),
        message: t.detail.deleteBody,
        danger: true,
        confirmLabel: t.detail.delete,
      })
    ) {
      remove.mutate()
    }
  }

  return (
    <IntakeShell>
      <div className="grid gap-3">
        <Link className="lnk inline-flex items-center gap-1 text-[13px]" to="/intake/sessions">
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
          {t.detail.back}
        </Link>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="grid gap-1">
            <span className="flex flex-wrap items-center gap-2">
              <b>{session.AppName}</b>
              <StoreTag store={store} small />
              <span className="text-xs text-fg3">{platformLabel(session.Platform, t)}</span>
              <Badge dot tone={view.tone}>
                {view.label}
              </Badge>
            </span>
            <h2 className="mono break-all text-[18px] font-semibold">{session.FileName}</h2>
            <span className="text-[13px] text-fg2">
              {interpolate(t.detail.meta, { id: session.Id, size: format.bytes(session.Size), source: sourceText(session, t) })}
              {' · '}
              {format.dateTime(session.StartDate)}
            </span>
          </div>
          <span className="flex flex-wrap gap-2">
            {session.StoreJobId ? (
              <Link className="btn" to={`/activity/jobs/${session.StoreJobId}`}>
                {t.detail.openJob}
              </Link>
            ) : null}
            {session.Status === ArtifactSessionStatus.Duplicate && session.OriginalSessionId ? (
              <Link className="btn" to={`/intake/sessions/${session.OriginalSessionId}`}>
                {t.detail.openOriginal}
              </Link>
            ) : null}
            {releaseTest && session.Status === ArtifactSessionStatus.Held && !session.PackageDeleted ? (
              <Button variant="primary" loading={deliver.isPending} onClick={() => void askDeliver()}>
                <Send aria-hidden />
                {t.detail.deliver}
              </Button>
            ) : null}
            {releaseTest && isFinal(session.Status) && session.Status !== ArtifactSessionStatus.Duplicate && !session.PackageDeleted ? (
              <Button variant="danger-outline" loading={remove.isPending} onClick={() => void askDelete()}>
                <Trash2 aria-hidden />
                {t.detail.delete}
              </Button>
            ) : null}
          </span>
        </div>

        {session.Status === ArtifactSessionStatus.Held ? (
          <Callout tone="warning" title={t.detail.heldTitle}>
            {session.HoldReason ?? session.StatusDescription}
          </Callout>
        ) : session.Status === ArtifactSessionStatus.Rejected ? (
          <Callout tone="danger" title={t.detail.rejectedTitle}>
            {session.StatusDescription}
          </Callout>
        ) : session.Status === ArtifactSessionStatus.Failed ? (
          <Callout tone="danger" title={t.detail.failedTitle}>
            {session.StatusDescription}
          </Callout>
        ) : session.Status === ArtifactSessionStatus.Duplicate ? (
          <Callout tone="info" title={t.detail.duplicateTitle}>
            {interpolate(t.detail.duplicateBody, { id: session.OriginalSessionId ?? '-' })}
          </Callout>
        ) : (
          <LiveNote />
        )}
        {session.PackageDeleted ? <Callout>{t.detail.packageDeleted}</Callout> : null}

        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,340px)]">
          <div className="grid content-start gap-4">
            <section className="card grid gap-3 p-4" aria-label={t.detail.steps}>
              <h3 className="text-[14px] font-semibold">{t.detail.steps}</h3>
              <Timeline
                items={(session.Steps ?? []).map((step) => ({
                  id: step.Kind,
                  tone: stepView(step.Status, t).tone,
                  title: <StepItem step={step} />,
                }))}
              />
            </section>
            {session.Package ? (
              <section className="card grid gap-2 p-4" aria-label={t.detail.package}>
                <h3 className="text-[14px] font-semibold">{t.detail.package}</h3>
                <dl className="grid gap-2 text-[13px] sm:grid-cols-2">
                  <Row label={t.detail.identifier}>
                    <span className="mono">{session.Package.Identifier ?? '-'}</span>
                  </Row>
                  <Row label={t.detail.version}>
                    {[session.Package.VersionName, session.Package.VersionCode ? `(${session.Package.VersionCode})` : null]
                      .filter(Boolean)
                      .join(' ') || '-'}
                  </Row>
                  {session.Package.MinOs ? <Row label={t.detail.minOs}>{session.Package.MinOs}</Row> : null}
                  {session.Package.TargetSdk ? <Row label={t.detail.targetSdk}>{session.Package.TargetSdk}</Row> : null}
                  {session.Package.XcodeVersion || session.Package.SdkVersion ? (
                    <Row label={t.detail.xcode}>
                      {[session.Package.XcodeVersion, session.Package.SdkVersion].filter(Boolean).join(' · ')}
                    </Row>
                  ) : null}
                  {session.Package.Signature ? (
                    <Row label={t.detail.signature}>
                      <span className="mono break-all">{session.Package.Signature}</span>
                    </Row>
                  ) : null}
                  {session.Package.UsesNonExemptEncryption !== null && session.Package.UsesNonExemptEncryption !== undefined ? (
                    <Row label={t.detail.encryption}>{session.Package.UsesNonExemptEncryption ? t.detail.yes : t.detail.no}</Row>
                  ) : null}
                </dl>
              </section>
            ) : null}
            {checks.length > 0 ? (
              <section className="card grid gap-2 p-4">
                <h3 className="text-[14px] font-semibold">{interpolate(t.detail.checks, { ok: okChecks, total: checks.length })}</h3>
                <Checklist
                  items={checks.map((check, index) => ({
                    id: `${check.Key ?? index}`,
                    state: checkState(check.Status),
                    title: check.Title ?? check.Key ?? '',
                    detail: [check.Message, check.RuleId].filter(Boolean).join(' · '),
                    status: '',
                  }))}
                />
              </section>
            ) : null}
            {rule ? (
              <section className="card grid gap-1 p-4">
                <h3 className="text-[14px] font-semibold">{t.detail.applied}</h3>
                <span className="text-[13px]">
                  {platformLabel(rule.Platform, t)} → {rule.Target === DeliveryTarget.None ? t.rules.none : target}
                  {rule.RolloutFraction ? ` · %${Math.round(rule.RolloutFraction * 100)}` : ''}
                  {rule.Override ? ` (${t.detail.override})` : ''}
                </span>
                {rule.RuleUpdatedByUserName ? (
                  <span className="text-xs text-fg3">
                    {interpolate(t.detail.ruleBy, { name: rule.RuleUpdatedByUserName, date: format.date(rule.RuleUpdatedDate) })}
                  </span>
                ) : null}
              </section>
            ) : null}
          </div>

          <div className="grid content-start gap-4">
            <section className="card grid gap-2 p-4" aria-label={t.detail.sourceTitle}>
              <h3 className="text-[14px] font-semibold">{t.detail.sourceTitle}</h3>
              <dl className="grid gap-2 text-[13px]">
                <Row label={t.sessions.source}>{sourceText(session, t)}</Row>
                {ref?.RunUrl ? (
                  <Row label={t.detail.run}>
                    <a className="lnk" href={ref.RunUrl} target="_blank" rel="noreferrer">
                      {t.detail.openRun}
                    </a>
                  </Row>
                ) : null}
                {ref?.Repository ? <Row label={t.detail.repository}>{ref.Repository}</Row> : null}
                {ref?.Branch || ref?.Commit ? (
                  <Row label={t.detail.branch}>
                    <span className="mono">{[ref?.Branch, ref?.Commit?.slice(0, 7)].filter(Boolean).join(' · ')}</span>
                  </Row>
                ) : null}
                {ref?.Actor ? <Row label={t.detail.actor}>{ref.Actor}</Row> : null}
                {identity ? <Row label={t.detail.identity}>{identity}</Row> : null}
                <Row label={t.detail.sha}>
                  <span className="mono break-all">{session.Sha256}</span>
                </Row>
              </dl>
            </section>
            <section className="card grid gap-2 p-4" aria-label={t.detail.ciTitle}>
              <span className="flex items-center justify-between gap-2">
                <h3 className="text-[14px] font-semibold">{t.detail.ciTitle}</h3>
                {session.CiResult ? (
                  <Badge small tone={session.CiResult.ExitCode === 0 ? 'success' : 'danger'}>
                    {interpolate(t.detail.exit, { code: session.CiResult.ExitCode })}
                  </Badge>
                ) : (
                  <Badge small tone="info">
                    {t.detail.ciPending}
                  </Badge>
                )}
              </span>
              {session.CiResult?.Summary ? <pre className="code whitespace-pre-wrap break-all">{session.CiResult.Summary}</pre> : null}
            </section>
          </div>
        </div>
      </div>
    </IntakeShell>
  )
}
