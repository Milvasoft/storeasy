import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ListX, ScrollText } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { DateRangePicker, type DateRange } from '@/components/ui/DatePicker'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, EmptyState, PageHeader, TableSkeleton } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { FilterType, SortType, type FilterCriteria, type ListRequest } from '@/lib/api/list'
import { StoreKind, type ActionChannel, type AuditAction, type StoreAuditLogDto } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { AUDIT_ACTIONS, auditActionLabel, channelLabel, CHANNELS, parseDetails } from '@/lib/org/auditLabels'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { dayEndIso, dayStartIso, todayDateOnly } from '@/lib/utils/date'

const DEFAULT_PAGE_SIZE = 20
const ALL = 'all'

function numberParam(value: string | null): number | null {
  const parsed = Number(value)
  return value && Number.isInteger(parsed) ? parsed : null
}

/** Builds the ListRequest from the address: page, size, enum filters (EqualTo only, 08 section 3) and the date range. */
export function auditRequest(params: URLSearchParams): ListRequest {
  const criterias: FilterCriteria[] = []
  const channel = numberParam(params.get('channel'))
  const action = numberParam(params.get('action'))
  const store = numberParam(params.get('store'))
  const from = params.get('from')
  const to = params.get('to')
  const user = params.get('user')?.trim()
  if (channel !== null) {
    criterias.push({ FilterBy: 'Channel', Value: channel, Type: FilterType.EqualTo })
  }
  if (action !== null) {
    criterias.push({ FilterBy: 'Action', Value: action, Type: FilterType.EqualTo })
  }
  if (store !== null) {
    criterias.push({ FilterBy: 'Store', Value: store, Type: FilterType.EqualTo })
  }
  if (from && to) {
    // Day boundaries go out as UTC moments (design 19.6).
    criterias.push({ FilterBy: 'Date', Value: dayStartIso(from), OtherValue: dayEndIso(to), Type: FilterType.Between })
  }
  if (user) {
    criterias.push({ FilterBy: 'UserName', Value: user, Type: FilterType.Contains })
  }
  return {
    PageNumber: numberParam(params.get('page')) ?? 1,
    RowCount: numberParam(params.get('size')) ?? DEFAULT_PAGE_SIZE,
    Sorting: { SortBy: 'Date', Type: SortType.Desc },
    ...(criterias.length > 0 ? { Filtering: { Criterias: criterias } } : {}),
  }
}

function AuditDetail({ log, onClose }: { log: StoreAuditLogDto | null; onClose: () => void }) {
  const t = useT()
  const format = useFormat()
  const fields = parseDetails(log?.Details)
  return (
    <Modal
      open={!!log}
      onClose={onClose}
      variant="drawer"
      title={log ? auditActionLabel(log.Action, t) : undefined}
      description={log ? format.dateTime(log.Date) : undefined}
    >
      {log ? (
        <div className="grid gap-5">
          <dl className="kv">
            <dt>{t.audit.user}</dt>
            <dd>{log.UserName ?? '-'}</dd>
            <dt>{t.audit.channel}</dt>
            <dd>{channelLabel(log.Channel, t)}</dd>
            <dt>{t.audit.store}</dt>
            <dd>{log.Store ? <StoreTag store={log.Store} /> : '-'}</dd>
            <dt>{t.audit.target}</dt>
            <dd className="mono text-[13px]">{log.TargetId ?? '-'}</dd>
            <dt>{t.audit.summary}</dt>
            <dd>{log.Summary ?? '-'}</dd>
          </dl>
          {log.Details ? (
            <section className="grid gap-2">
              <h3 className="h3">{t.audit.details}</h3>
              {fields ? (
                <dl className="kv rounded-md border border-border p-3">
                  {fields.map(([key, value]) => (
                    <div key={key} className="contents">
                      <dt className="mono text-xs">{key}</dt>
                      <dd className="mono text-[12.5px]">{value}</dd>
                    </div>
                  ))}
                </dl>
              ) : (
                <pre className="code whitespace-pre-wrap">{log.Details}</pre>
              )}
            </section>
          ) : null}
        </div>
      ) : null}
    </Modal>
  )
}

/**
 * The organization's audit log (design 15.15-15.19): newest first, filters in the address so a view can be shared,
 * a row opens its detail drawer (?id=), the previous page stays on screen while the next one loads (18.4).
 */
export function AuditPage() {
  const t = useT()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const [params, setParams] = useSearchParams()
  const request = useMemo(() => auditRequest(params), [params])
  const [userDraft, setUserDraft] = useState(params.get('user') ?? '')

  const logs = useQuery({
    queryKey: queryKeys.auditLogs.list(organization.Id, request),
    queryFn: () => organizationsApi.auditLogs(organization.Id, request),
    placeholderData: keepPreviousData,
  })

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

  const rows = logs.data?.Data ?? []
  const selectedId = numberParam(params.get('id'))
  const selected = rows.find((row) => row.Id === selectedId) ?? null
  const filtered = ['channel', 'action', 'store', 'from', 'user'].some((key) => params.get(key))
  const range: DateRange | null = params.get('from') && params.get('to') ? { from: params.get('from')!, to: params.get('to')! } : null

  const columns: Column<StoreAuditLogDto>[] = [
    {
      key: 'date',
      header: t.audit.date,
      cell: (log) => <span className="whitespace-nowrap tabular-nums">{format.dateTime(log.Date)}</span>,
    },
    { key: 'action', header: t.audit.action, role: 'main', cell: (log) => <b>{auditActionLabel(log.Action, t)}</b> },
    { key: 'user', header: t.audit.user, cell: (log) => log.UserName ?? '-' },
    { key: 'channel', header: t.audit.channel, cell: (log) => <Badge small>{channelLabel(log.Channel, t)}</Badge> },
    { key: 'summary', header: t.audit.summary, role: 'hidePhone', cell: (log) => <span className="text-fg2">{log.Summary ?? '-'}</span> },
  ]

  const enumOptions = <T extends number>(values: readonly T[], label: (value: T) => string) => [
    { value: ALL, label: t.common.all },
    ...values.map((value) => ({ value: String(value), label: label(value) })),
  ]

  return (
    <>
      <PageHeader tour="audit" title={t.audit.title} subtitle={t.audit.subtitle} />
      <div className="toolbar">
        <span className="w-full sm:w-56">
          <form
            onSubmit={(event) => {
              event.preventDefault()
              update({ user: userDraft.trim() || null })
            }}
          >
            <input
              className="inp"
              type="search"
              aria-label={t.audit.searchUser}
              placeholder={t.audit.searchUser}
              value={userDraft}
              onChange={(event) => setUserDraft(event.target.value)}
            />
          </form>
        </span>
        <span className="w-[calc(50%-4px)] sm:w-40">
          <Select
            aria-label={t.audit.channel}
            value={params.get('channel') ?? ALL}
            options={enumOptions(CHANNELS, (value) => channelLabel(value as ActionChannel, t))}
            onChange={(value) => update({ channel: value === ALL ? null : value })}
          />
        </span>
        <span className="w-[calc(50%-4px)] sm:w-48">
          <Select
            aria-label={t.audit.action}
            searchable
            value={params.get('action') ?? ALL}
            options={enumOptions(AUDIT_ACTIONS, (value) => auditActionLabel(value as AuditAction, t))}
            onChange={(value) => update({ action: value === ALL ? null : value })}
          />
        </span>
        <span className="w-[calc(50%-4px)] sm:w-40">
          <Select
            aria-label={t.audit.store}
            value={params.get('store') ?? ALL}
            options={[
              { value: ALL, label: t.common.all },
              { value: String(StoreKind.Apple), label: t.enums.store.apple },
              { value: String(StoreKind.Google), label: t.enums.store.google },
            ]}
            onChange={(value) => update({ store: value === ALL ? null : value })}
          />
        </span>
        <DateRangePicker
          label={t.audit.date}
          value={range}
          max={todayDateOnly()}
          onChange={(value) => update({ from: value?.from ?? null, to: value?.to ?? null })}
        />
        {filtered ? (
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              setUserDraft('')
              update({ channel: null, action: null, store: null, from: null, to: null, user: null })
            }}
          >
            {t.common.clearFilters}
          </Button>
        ) : null}
      </div>
      {logs.isLoading ? (
        <TableSkeleton columns={[t.audit.date, t.audit.action, t.audit.user, t.audit.channel, t.audit.summary]} label={t.common.loading} />
      ) : logs.isError ? (
        <ScreenState error={logs.error} onRetry={() => void logs.refetch()} />
      ) : rows.length === 0 ? (
        filtered ? (
          <EmptyState
            icon={<ListX aria-hidden />}
            title={t.audit.noResults}
            description={t.audit.noResultsBody}
            action={
              <Button onClick={() => update({ channel: null, action: null, store: null, from: null, to: null, user: null })}>
                {t.common.clearFilters}
              </Button>
            }
          />
        ) : (
          <EmptyState icon={<ScrollText aria-hidden />} title={t.audit.emptyTitle} description={t.audit.emptyBody} />
        )
      ) : (
        <>
          <DataTable
            caption={t.audit.title}
            columns={columns}
            rows={rows}
            rowKey={(log) => log.Id}
            busy={logs.isFetching}
            onRowClick={(log) => update({ id: String(log.Id) }, false)}
          />
          <Pagination
            page={logs.data?.CurrentPageNumber ?? request.PageNumber}
            pageCount={logs.data?.TotalPageCount ?? 1}
            total={logs.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            loadingNext={logs.isFetching && logs.isPlaceholderData}
            onPage={(page) => update({ page: String(page) }, false)}
            onPageSize={(size) => update({ size: String(size) })}
          />
        </>
      )}
      <AuditDetail log={selected} onClose={() => update({ id: null }, false)} />
    </>
  )
}
