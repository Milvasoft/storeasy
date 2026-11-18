import { useEffect, useMemo, useRef } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ArrowLeft, ListChecks, ListX, MoreHorizontal } from 'lucide-react'
import { progressText } from '@/components/activity/Hat'
import { Badge, StoreTag, Tag } from '@/components/ui/Badge'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Menu } from '@/components/ui/Menu'
import { Progress } from '@/components/ui/Progress'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, EmptyState, IconButton, PageHeader, TableSkeleton } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import type { JobView } from '@/lib/activity/hatState'
import { jobsApi } from '@/lib/api/endpoints'
import { FilterType, SortType, type FilterCriteria, type ListRequest } from '@/lib/api/list'
import {
  ActionChannel,
  OrgPermission,
  StepDirection,
  StoreJobStatus,
  StoreJobStepStatus,
  StoreKind,
  type StoreJobDetailDto,
  type StoreJobListDto,
  type StoreJobStepDto,
} from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import type { Dictionary } from '@/lib/i18n/en'
import { formatDuration, isJobActive, jobDuration, jobKindLabel, jobStatusView, stepStatusLabel } from '@/lib/jobs/labels'
import { useCancelJob } from '@/lib/jobs/useCancelJob'
import { channelLabel } from '@/lib/org/auditLabels'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { cn } from '@/lib/utils/cn'
import { ActivityTabs, useNow } from '@/pages/ActivityPage'

const DEFAULT_PAGE_SIZE = 20
const ALL = 'all'
const STATUSES = Object.values(StoreJobStatus)
const CHANNELS = Object.values(ActionChannel)

function numberParam(value: string | null): number | null {
  const parsed = Number(value)
  return value && Number.isInteger(parsed) ? parsed : null
}

/** The job list request from the address: page, size and the enum filters (EqualTo, 08 section 3), newest first. */
export function jobsRequest(params: URLSearchParams): ListRequest {
  const criterias: FilterCriteria[] = []
  for (const [param, field] of [
    ['status', 'Status'],
    ['store', 'Store'],
    ['channel', 'Channel'],
  ] as const) {
    const value = numberParam(params.get(param))
    if (value !== null) {
      criterias.push({ FilterBy: field, Value: value, Type: FilterType.EqualTo })
    }
  }
  return {
    PageNumber: numberParam(params.get('page')) ?? 1,
    RowCount: numberParam(params.get('size')) ?? DEFAULT_PAGE_SIZE,
    Sorting: { SortBy: 'Id', Type: SortType.Desc },
    ...(criterias.length > 0 ? { Filtering: { Criterias: criterias } } : {}),
  }
}

// The list row merged with what the live stream reported after it (status, retry); the order stays as loaded (11.4).
function liveStatus(job: StoreJobListDto, live: JobView | undefined): { status: StoreJobStatus; retrying: boolean } {
  if (live && live.updatedAt > (job.EndDate ?? job.StartDate ?? job.CreationDate ?? '')) {
    return { status: live.status, retrying: live.retrying }
  }
  return { status: job.Status, retrying: job.Status === StoreJobStatus.Queued && job.Attempt > 0 && !!job.Error }
}

export function JobStatusBadge({
  status,
  retrying,
  stopping,
  t,
}: {
  status: StoreJobStatus
  retrying?: boolean
  stopping?: boolean
  t: Dictionary
}) {
  const view = jobStatusView(status, t, { retrying, stopping })
  return (
    <Badge tone={view.tone} dot={view.dot} small>
      {view.label}
    </Badge>
  )
}

function JobActions({ job, active }: { job: StoreJobListDto; active: boolean }) {
  const t = useT()
  const navigate = useNavigate()
  const { can } = useOrganization()
  const { cancel } = useCancelJob()
  return (
    <span onClick={(event) => event.stopPropagation()}>
      <Menu
        label={t.jobs.actions}
        trigger={
          <IconButton label={t.jobs.actions} ghost size="sm">
            <MoreHorizontal aria-hidden />
          </IconButton>
        }
        entries={[
          { key: 'open', label: t.jobs.open, onSelect: () => navigate(`/activity/jobs/${job.Id}`) },
          ...(active && can(OrgPermission.EditContent) && !job.CancelRequested
            ? [{ key: 'cancel', label: t.jobs.cancel, danger: true, onSelect: () => void cancel(job.Id) }]
            : []),
        ]}
      />
    </span>
  )
}

/** The job list (design 11.4): newest first, filters in the address, running rows updated in place by the stream. */
export function JobsPage({ appId }: { appId?: number } = {}) {
  const t = useT()
  const format = useFormat()
  const navigate = useNavigate()
  const organization = useCurrentOrganization()
  const { state, appName } = useActivity()
  const [params, setParams] = useSearchParams()
  const request = useMemo(() => jobsRequest(params), [params])

  const jobs = useQuery({
    queryKey: queryKeys.jobs.list(organization.Id, request, appId),
    queryFn: () => jobsApi.list(organization.Id, request, appId),
    placeholderData: keepPreviousData,
  })
  const rows = jobs.data?.Data ?? []
  const running = rows.some((row) => isJobActive(liveStatus(row, state.jobs[row.Id]).status))
  const now = useNow(running)

  const update = (changes: Record<string, string | null>, resetPage = true) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value === null || value === '') {
        next.delete(key)
      } else {
        next.set(key, value)
      }
    }
    if (resetPage) {
      next.delete('page')
    }
    setParams(next, { replace: true })
  }
  const clear = () => update({ status: null, store: null, channel: null })
  const filtered = ['status', 'store', 'channel'].some((key) => params.get(key))

  const columns: Column<StoreJobListDto>[] = [
    {
      key: 'job',
      header: t.jobs.job,
      role: 'main',
      cell: (job) => (
        <span className="grid min-w-0">
          <b className="truncate">{[appName(job.AppId), jobKindLabel(job.Kind, t)].filter(Boolean).join(' · ')}</b>
          <span className="mono truncate text-[11.5px] text-fg3">
            {job.Summary ?? job.Error ?? interpolate(t.jobs.jobNumber, { id: job.Id })}
          </span>
        </span>
      ),
    },
    { key: 'store', header: t.jobs.store, cell: (job) => (job.Store ? <StoreTag store={job.Store} small /> : '-') },
    { key: 'channel', header: t.jobs.channel, cell: (job) => <Tag>{channelLabel(job.Channel, t)}</Tag> },
    { key: 'requestedBy', header: t.jobs.requestedBy, role: 'hidePhone', cell: (job) => job.RequestedByUserName ?? t.jobs.system },
    {
      key: 'status',
      header: t.jobs.status,
      cell: (job) => {
        const live = liveStatus(job, state.jobs[job.Id])
        return (
          <JobStatusBadge t={t} status={live.status} retrying={live.retrying} stopping={job.CancelRequested && isJobActive(live.status)} />
        )
      },
    },
    {
      key: 'attempt',
      header: t.jobs.attempt,
      role: 'hidePhone',
      cell: (job) => (
        <span className="tabular-nums">{interpolate(t.jobs.attemptOf, { attempt: Math.max(1, job.Attempt), max: job.MaxAttempts })}</span>
      ),
    },
    {
      key: 'duration',
      header: t.jobs.duration,
      cell: (job) => {
        const ms = jobDuration(job.StartDate, job.EndDate, now)
        return <span className="tabular-nums">{ms === null ? '-' : formatDuration(ms, t)}</span>
      },
    },
    {
      key: 'started',
      header: t.jobs.started,
      cell: (job) => <span className="whitespace-nowrap tabular-nums">{format.dateTime(job.StartDate ?? job.CreationDate)}</span>,
    },
    {
      key: 'actions',
      header: t.jobs.actions,
      role: 'action',
      className: 'w-10',
      cell: (job) => <JobActions job={job} active={isJobActive(liveStatus(job, state.jobs[job.Id]).status)} />,
    },
  ]

  const option = (value: number, label: string) => ({ value: String(value), label })

  const content = (
    <>
      <div className="toolbar">
        <span className="w-[calc(50%-4px)] sm:w-44">
          <Select
            aria-label={t.jobs.status}
            value={params.get('status') ?? ALL}
            options={[
              { value: ALL, label: t.jobs.allStatuses },
              ...STATUSES.map((status) => option(status, jobStatusView(status, t).label)),
            ]}
            onChange={(value) => update({ status: value === ALL ? null : value })}
          />
        </span>
        <span className="w-[calc(50%-4px)] sm:w-44">
          <Select
            aria-label={t.jobs.store}
            value={params.get('store') ?? ALL}
            options={[
              { value: ALL, label: t.activity.allStores },
              option(StoreKind.Apple, t.enums.store.apple),
              option(StoreKind.Google, t.enums.store.google),
              option(StoreKind.Microsoft, t.enums.store.microsoft),
            ]}
            onChange={(value) => update({ store: value === ALL ? null : value })}
          />
        </span>
        <span className="w-[calc(50%-4px)] sm:w-36">
          <Select
            aria-label={t.jobs.channel}
            value={params.get('channel') ?? ALL}
            options={[{ value: ALL, label: t.common.all }, ...CHANNELS.map((channel) => option(channel, channelLabel(channel, t)))]}
            onChange={(value) => update({ channel: value === ALL ? null : value })}
          />
        </span>
        {filtered ? (
          <Button size="sm" variant="ghost" onClick={clear}>
            {t.common.clearFilters}
          </Button>
        ) : null}
      </div>
      {jobs.isLoading ? (
        <TableSkeleton columns={[t.jobs.job, t.jobs.store, t.jobs.status, t.jobs.duration, t.jobs.started]} label={t.common.loading} />
      ) : jobs.isError ? (
        <ScreenState error={jobs.error} onRetry={() => void jobs.refetch()} />
      ) : rows.length === 0 ? (
        filtered ? (
          <EmptyState
            icon={<ListX aria-hidden />}
            title={t.jobs.noResults}
            description={t.jobs.noResultsBody}
            action={<Button onClick={clear}>{t.common.clearFilters}</Button>}
          />
        ) : (
          <EmptyState icon={<ListChecks aria-hidden />} title={t.jobs.emptyTitle} description={t.jobs.emptyBody} />
        )
      ) : (
        <>
          <DataTable
            caption={t.jobs.listLabel}
            columns={columns}
            rows={rows}
            rowKey={(job) => job.Id}
            busy={jobs.isFetching}
            onRowClick={(job) => navigate(`/activity/jobs/${job.Id}`)}
          />
          <Pagination
            page={jobs.data?.CurrentPageNumber ?? request.PageNumber}
            pageCount={jobs.data?.TotalPageCount ?? 1}
            total={jobs.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            loadingNext={jobs.isFetching && jobs.isPlaceholderData}
            onPage={(page) => update({ page: String(page) }, false)}
            onPageSize={(size) => update({ size: String(size) })}
          />
        </>
      )}
    </>
  )

  if (appId !== undefined) {
    return content
  }
  return (
    <>
      <PageHeader title={t.activity.title} subtitle={t.activity.subtitle} />
      <ActivityTabs />
      {content}
    </>
  )
}

const STEP_TONE: Record<StoreJobStepStatus, string> = {
  [StoreJobStepStatus.Running]: 'now',
  [StoreJobStepStatus.Succeeded]: 'ok',
  [StoreJobStepStatus.Failed]: 'dng',
  [StoreJobStepStatus.Skipped]: '',
}

function StepItem({ step, showAttempt }: { step: StoreJobStepDto; showAttempt: boolean }) {
  const t = useT()
  const format = useFormat()
  const failed = step.Status === StoreJobStepStatus.Failed
  const direction = failed
    ? { className: 'err', label: t.activity.direction.error }
    : step.Direction === StepDirection.Outbound
      ? { className: 'out', label: t.activity.direction.out }
      : step.Direction === StepDirection.Inbound
        ? { className: '', label: t.activity.direction.in }
        : { className: '', label: t.activity.direction.internal }
  const progress = progressText({ progress: step.Progress ?? null, total: step.Total ?? null, stepName: step.Name }, format)
  return (
    <li className="tl-i">
      <span className={cn('tl-d', STEP_TONE[step.Status])} aria-hidden />
      <div className="tstep">
        <div className="min-w-0">
          <span className={cn('dir', direction.className)}>{direction.label}</span> <b className="mono text-[12.5px]">{step.Name}</b>{' '}
          <span className="text-xs text-fg3">
            {[stepStatusLabel(step.Status, t), showAttempt ? interpolate(t.jobs.attemptStep, { attempt: step.Attempt }) : null]
              .filter(Boolean)
              .join(' · ')}
          </span>
          {step.Message || progress ? <div className="m">{[progress, step.Message].filter(Boolean).join(' · ')}</div> : null}
          {step.Total ? (
            <div className="mt-1.5 max-w-xs">
              <Progress
                small
                value={step.Progress ?? 0}
                max={step.Total}
                tone={failed ? 'dng' : step.Status === StoreJobStepStatus.Succeeded ? 'ok' : undefined}
                label={step.Name}
              />
            </div>
          ) : null}
        </div>
        <time className="mono text-xs text-fg3" dateTime={step.StartDate}>
          {format.time(step.StartDate)}
        </time>
      </div>
    </li>
  )
}

function JsonBlock({ label, value }: { label: string; value: string }) {
  let text = value
  try {
    text = JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    /* not JSON: shown as it is */
  }
  return (
    <section className="grid gap-1.5">
      <h3 className="h3">{label}</h3>
      <pre className="code whitespace-pre-wrap">{text}</pre>
    </section>
  )
}

/** One job (design 11.5, 11.6): status, timing and its steps across attempts; a running job can be cancelled. */
export function JobDetailPage() {
  const t = useT()
  const format = useFormat()
  const { jobId: rawId = '' } = useParams()
  const jobId = Number(rawId)
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const { state, appName } = useActivity()
  const { cancel, pending } = useCancelJob()
  const live = state.jobs[jobId]

  const job = useQuery({
    queryKey: queryKeys.jobs.detail(organization.Id, jobId),
    queryFn: () => jobsApi.detail(organization.Id, jobId),
    enabled: Number.isInteger(jobId) && jobId > 0,
    // While the job runs, the stream says when something changed; the steps are read again on each change.
    refetchInterval: (query) => (query.state.data && isJobActive(query.state.data.Status) ? 15_000 : false),
  })
  const data: StoreJobDetailDto | undefined = job.data
  const liveKey = live ? `${live.status}-${live.stepOrder}-${live.stepStatus}-${live.updatedAt}` : null
  useRefetchOnChange(liveKey, job.refetch)
  const now = useNow(!!data && isJobActive(data.Status))

  const back = (
    <Link className="btn inv" to="/activity/jobs">
      {t.jobs.back}
    </Link>
  )

  if (job.isLoading || (!data && !job.isError)) {
    return <TableSkeleton columns={[t.jobs.steps]} rows={5} label={t.common.loading} />
  }
  if (job.isError || !data) {
    return <ScreenState error={job.error} onRetry={() => void job.refetch()} back={back} />
  }

  const status = live && live.updatedAt > (data.EndDate ?? data.StartDate ?? data.CreationDate ?? '') ? live.status : data.Status
  const active = isJobActive(status)
  const steps = data.Steps ?? []
  const attempts = new Set(steps.map((step) => step.Attempt)).size
  const duration = jobDuration(data.StartDate, data.EndDate, now)
  const title = [appName(data.AppId), jobKindLabel(data.Kind, t)].filter(Boolean).join(' · ')

  return (
    <>
      <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to="/activity/jobs">
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
        {t.jobs.back}
      </Link>
      <PageHeader
        title={title}
        badge={
          <>
            {data.Store ? <StoreTag store={data.Store} small /> : null}
            <JobStatusBadge t={t} status={status} retrying={live?.retrying} stopping={data.CancelRequested && active} />
          </>
        }
        subtitle={
          <span className="mono text-xs">
            {[duration !== null ? formatDuration(duration, t) : null, interpolate(t.jobs.jobNumber, { id: data.Id })]
              .filter(Boolean)
              .join(' · ')}
          </span>
        }
        actions={
          active && can(OrgPermission.EditContent) && !data.CancelRequested ? (
            <Button variant="danger-outline" loading={pending} onClick={() => void cancel(data.Id)}>
              {t.jobs.cancel}
            </Button>
          ) : undefined
        }
      />
      <div className="split">
        <section className="card">
          <div className="card-h">
            <h2 className="h3">{t.jobs.steps}</h2>
            <span className="mono ml-auto text-xs text-fg3">{interpolate(t.jobs.stepCount, { count: steps.length })}</span>
          </div>
          <div className="card-b">
            {steps.length === 0 ? (
              <p className="m-0 text-[13px] text-fg3">{t.jobs.noSteps}</p>
            ) : (
              <ol className="m-0 grid list-none p-0">
                {steps.map((step) => (
                  <StepItem key={`${step.Attempt}-${step.Order}`} step={step} showAttempt={attempts > 1} />
                ))}
              </ol>
            )}
          </div>
        </section>
        <aside className="grid gap-4">
          {data.Error && status !== StoreJobStatus.Succeeded ? (
            <Callout tone="danger" title={t.jobs.error}>
              <span className="mono text-xs">{data.Error}</span>
            </Callout>
          ) : null}
          <section className="card">
            <div className="card-h">
              <h2 className="h3">{t.jobs.details}</h2>
            </div>
            <dl className="kv card-b">
              <dt>{t.jobs.requestedBy}</dt>
              <dd>{data.RequestedByUserName ?? t.jobs.system}</dd>
              <dt>{t.jobs.channel}</dt>
              <dd>{channelLabel(data.Channel, t)}</dd>
              <dt>{t.jobs.attempt}</dt>
              <dd className="tabular-nums">
                {interpolate(t.jobs.attemptOf, { attempt: Math.max(1, data.Attempt), max: data.MaxAttempts })}
              </dd>
              {data.AppId ? (
                <>
                  <dt>{t.jobs.app}</dt>
                  <dd>
                    <Link className="lnk" to={`/apps/${data.AppId}`}>
                      {appName(data.AppId) ?? `#${data.AppId}`}
                    </Link>
                  </dd>
                </>
              ) : null}
              <dt>{t.jobs.queuedAt}</dt>
              <dd className="tabular-nums">{format.dateTime(data.CreationDate)}</dd>
              <dt>{t.jobs.started}</dt>
              <dd className="tabular-nums">{format.dateTime(data.StartDate)}</dd>
              {data.EndDate ? (
                <>
                  <dt>{t.jobs.finishedAt}</dt>
                  <dd className="tabular-nums">{format.dateTime(data.EndDate)}</dd>
                </>
              ) : status === StoreJobStatus.Queued && data.NextAttemptDate ? (
                <>
                  <dt>{t.jobs.nextAttempt}</dt>
                  <dd className="tabular-nums">{format.dateTime(data.NextAttemptDate)}</dd>
                </>
              ) : null}
              {data.Summary ? (
                <>
                  <dt>{t.jobs.summary}</dt>
                  <dd>{data.Summary}</dd>
                </>
              ) : null}
            </dl>
          </section>
          {data.Payload ? <JsonBlock label={t.jobs.payload} value={data.Payload} /> : null}
          {data.Result ? <JsonBlock label={t.jobs.resultData} value={data.Result} /> : null}
        </aside>
      </div>
    </>
  )
}

// Reads the job again whenever the live stream reports a change of it (a new step, a new state).
function useRefetchOnChange(key: string | null, refetch: () => unknown) {
  const previous = useRef(key)
  useEffect(() => {
    if (key !== previous.current) {
      previous.current = key
      if (key !== null) {
        void refetch()
      }
    }
  }, [key, refetch])
}
