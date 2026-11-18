import { useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { History, ListX, RefreshCw } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { CodeBlock } from '@/components/ui/Code'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { presetRange } from '@/components/ui/DatePicker'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, Chip, EmptyState, PageHeader, TableSkeleton } from '@/components/ui/primitives'
import { FilterType, SortType, type FilterCriteria, type ListRequest } from '@/lib/api/list'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { dayEndIso, dayStartIso } from '@/lib/utils/date'
import { platformApi, platformKeys } from './api'
import { PlatformGate, PlatformPermission } from './PlatformCommon'
import { usePlatformText, type PlatformDictionary } from './i18n'
import { USER_ACTIVITIES, UserActivity, type ActivityLogListDto } from './types'

// Design 17.20: platform administration's own log (organizations' store writes are in their audit log). Read only.

const DEFAULT_PAGE_SIZE = 20
const ALL = 'all'
const RANGES = ['today', 'last7', 'last30'] as const
type RangeKey = (typeof RANGES)[number]

export function activityLabel(activity: UserActivity, t: PlatformDictionary): string {
  return t.logs.activities[activity] ?? String(activity)
}

function numberParam(value: string | null): number | null {
  const parsed = Number(value)
  return value && Number.isInteger(parsed) ? parsed : null
}

/** The list request from the address: date range (Between), action (EqualTo), user (Contains), related record (EqualTo). */
export function activityLogsRequest(params: URLSearchParams): ListRequest {
  const criterias: FilterCriteria[] = []
  const range = params.get('range') as RangeKey | null
  if (range && RANGES.includes(range)) {
    const { from, to } = presetRange(range)
    criterias.push({ FilterBy: 'ActivityDate', Value: dayStartIso(from), OtherValue: dayEndIso(to), Type: FilterType.Between })
  }
  const activity = numberParam(params.get('activity'))
  if (activity !== null) {
    criterias.push({ FilterBy: 'Activity', Value: activity, Type: FilterType.EqualTo })
  }
  const user = params.get('user')?.trim()
  if (user) {
    criterias.push({ FilterBy: 'UserName', Value: user, Type: FilterType.Contains })
  }
  const related = params.get('related')?.trim()
  if (related) {
    criterias.push({ FilterBy: 'RelatedId', Value: related, Type: FilterType.EqualTo })
  }
  return {
    PageNumber: numberParam(params.get('page')) ?? 1,
    RowCount: numberParam(params.get('size')) ?? DEFAULT_PAGE_SIZE,
    Sorting: { SortBy: 'ActivityDate', Type: SortType.Desc },
    ...(criterias.length > 0 ? { Filtering: { Criterias: criterias } } : {}),
  }
}

/** The related record as a link, from one mapping table (design 17.20); deleted records stay plain text. */
export function RelatedRecord({ log }: { log: ActivityLogListDto }) {
  const t = usePlatformText()
  const id = log.RelatedId
  const link = (to: string, label: string): ReactNode => (
    <Link className="lnk" to={to} onClick={(event) => event.stopPropagation()}>
      {label}
    </Link>
  )
  switch (log.Activity) {
    case UserActivity.CreateUser:
    case UserActivity.UpdateUser:
      return id ? link(`/admin/users/${id}`, interpolate(t.logs.userLink, { id })) : <>-</>
    case UserActivity.CreateRole:
    case UserActivity.UpdateRole:
      return id ? link(`/admin/roles/${id}`, interpolate(t.logs.roleLink, { id })) : <>-</>
    case UserActivity.ApproveRuleProposal:
    case UserActivity.RejectRuleProposal:
      return id ? link(`/admin/rules/proposals/${id}`, interpolate(t.logs.proposalLink, { id })) : <>-</>
    case UserActivity.UpdateFeatureFlag:
      return link('/admin/feature-flags', t.logs.flagsLink)
    case UserActivity.UpdateLanguages:
      return link('/admin/languages', t.logs.languagesLink)
    default:
      return <span className="text-fg3">{id ? interpolate(t.logs.deletedRecord, { id }) : '-'}</span>
  }
}

function LogDetail({ log, onClose }: { log: ActivityLogListDto; onClose: () => void }) {
  const t = usePlatformText()
  const format = useFormat()
  return (
    <Modal open onClose={onClose} variant="drawer" title={activityLabel(log.Activity, t)} description={interpolate(t.logs.detailTitle, { id: log.Id })}>
      <div className="grid gap-4">
        <dl className="kv">
          <dt>{t.logs.date}</dt>
          <dd>{format.dateTime(log.ActivityDate)}</dd>
          <dt>{t.logs.user}</dt>
          <dd>{log.UserName ?? '-'}</dd>
          <dt>{t.logs.activity}</dt>
          <dd>{log.ActivityDescription || activityLabel(log.Activity, t)}</dd>
          <dt>{t.logs.related}</dt>
          <dd>
            <RelatedRecord log={log} />
          </dd>
        </dl>
        {log.RelatedData ? <CodeBlock label={t.logs.data}>{log.RelatedData}</CodeBlock> : <p className="text-[13px] text-fg3">{t.logs.noData}</p>}
      </div>
    </Modal>
  )
}

function ActivityLogs() {
  const t = usePlatformText()
  const base = useT()
  const format = useFormat()
  const [params, setParams] = useSearchParams()
  const [userDraft, setUserDraft] = useState(params.get('user') ?? '')
  const [relatedDraft, setRelatedDraft] = useState(params.get('related') ?? '')
  const [selected, setSelected] = useState<ActivityLogListDto | null>(null)
  const request = activityLogsRequest(params)
  const logs = useQuery({
    queryKey: platformKeys.activityLogs(request),
    queryFn: () => platformApi.activityLogs(request),
    placeholderData: keepPreviousData,
  })

  const update = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value) {
        next.set(key, value)
      } else {
        next.delete(key)
      }
    }
    if (!('page' in changes)) {
      next.delete('page')
    }
    setParams(next, { replace: true })
  }
  const clear = () => {
    setUserDraft('')
    setRelatedDraft('')
    update({ range: null, activity: null, user: null, related: null })
  }
  const range = params.get('range')
  const filtered = ['range', 'activity', 'user', 'related'].some((key) => params.get(key))

  const columns: Column<ActivityLogListDto>[] = [
    {
      key: 'date',
      header: t.logs.date,
      cell: (log) => <span className="whitespace-nowrap tabular-nums">{format.dateTime(log.ActivityDate)}</span>,
    },
    { key: 'user', header: t.logs.user, cell: (log) => log.UserName ?? '-' },
    {
      key: 'activity',
      header: t.logs.activity,
      role: 'main',
      cell: (log) => <Badge small>{activityLabel(log.Activity, t)}</Badge>,
    },
    { key: 'related', header: t.logs.related, cell: (log) => <RelatedRecord log={log} /> },
    {
      key: 'data',
      header: t.logs.data,
      role: 'hidePhone',
      cell: (log) => <span className="line-clamp-1 break-all text-fg2">{log.RelatedData || '-'}</span>,
    },
  ]
  const rows = logs.data?.Data ?? []

  return (
    <>
      <PageHeader
        title={t.logs.title}
        subtitle={t.logs.subtitle}
        actions={
          <Button loading={logs.isFetching && !logs.isLoading} onClick={() => void logs.refetch()}>
            <RefreshCw aria-hidden />
            {t.common.refresh}
          </Button>
        }
      />
      <div className="toolbar">
        <span className="flex flex-wrap gap-1.5" role="group" aria-label={t.logs.date}>
          <Chip selected={!range} onClick={() => update({ range: null })}>
            {t.logs.allTime}
          </Chip>
          {RANGES.map((key) => (
            <Chip key={key} selected={range === key} onClick={() => update({ range: key })}>
              {key === 'today' ? t.logs.today : key === 'last7' ? t.logs.last7 : t.logs.last30}
            </Chip>
          ))}
        </span>
        <span className="w-full sm:w-56">
          <Select
            aria-label={t.logs.activity}
            searchable
            value={params.get('activity') ?? ALL}
            options={[
              { value: ALL, label: base.common.all },
              ...USER_ACTIVITIES.map((value) => ({ value: String(value), label: activityLabel(value, t) })),
            ]}
            onChange={(value) => update({ activity: value === ALL ? null : value })}
          />
        </span>
        <form
          className="w-[calc(50%-4px)] sm:w-52"
          role="search"
          onSubmit={(event) => {
            event.preventDefault()
            update({ user: userDraft.trim() || null })
          }}
        >
          <input
            className="inp"
            type="search"
            aria-label={t.logs.searchUser}
            placeholder={t.logs.searchUser}
            value={userDraft}
            onChange={(event) => setUserDraft(event.target.value)}
          />
        </form>
        <form
          className="w-[calc(50%-4px)] sm:w-32"
          onSubmit={(event) => {
            event.preventDefault()
            update({ related: relatedDraft.trim() || null })
          }}
        >
          <input
            className="inp"
            type="search"
            aria-label={t.logs.related}
            placeholder={t.logs.relatedPlaceholder}
            value={relatedDraft}
            onChange={(event) => setRelatedDraft(event.target.value)}
          />
        </form>
        {filtered ? (
          <Button size="sm" variant="ghost" onClick={clear}>
            {base.common.clearFilters}
          </Button>
        ) : null}
      </div>
      {logs.isLoading ? (
        <TableSkeleton columns={[t.logs.date, t.logs.user, t.logs.activity, t.logs.related, t.logs.data]} label={base.common.loading} />
      ) : logs.isError ? (
        <ScreenState error={logs.error} onRetry={() => void logs.refetch()} />
      ) : rows.length === 0 ? (
        filtered ? (
          <EmptyState
            icon={<ListX aria-hidden />}
            title={t.logs.noResults}
            description={t.logs.noResultsBody}
            action={<Button onClick={clear}>{base.common.clearFilters}</Button>}
          />
        ) : (
          <EmptyState icon={<History aria-hidden />} title={t.logs.empty} description={t.logs.emptyBody} />
        )
      ) : (
        <>
          <DataTable
            caption={t.logs.title}
            columns={columns}
            rows={rows}
            rowKey={(log) => log.Id}
            busy={logs.isFetching}
            onRowClick={setSelected}
          />
          <Pagination
            page={request.PageNumber}
            pageCount={logs.data?.TotalPageCount ?? 1}
            total={logs.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            onPage={(page) => update({ page: String(page) })}
            onPageSize={(size) => update({ size: String(size) })}
          />
        </>
      )}
      <p className="mt-3 text-xs text-fg3">{t.logs.retention}</p>
      {selected ? <LogDetail log={selected} onClose={() => setSelected(null)} /> : null}
    </>
  )
}

/** /admin/activity-logs. */
export function ActivityLogsPage() {
  return (
    <PlatformGate permission={PlatformPermission.ActivityLogList}>
      <ActivityLogs />
    </PlatformGate>
  )
}
