import { useMemo, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { BarChart3, RefreshCw, TrendingDown, TrendingUp } from 'lucide-react'
import { Badge, StoreTag, storeName } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DateRangePicker, type DateRange } from '@/components/ui/DatePicker'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, EmptyState, Field, Input, PageHeader } from '@/components/ui/primitives'
import { ApiError, apiErrorMessage } from '@/lib/api/http'
import { OrgPermission, StoreKind, type DateOnly } from '@/lib/api/types'
import { useFormat, useI18n } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { addDays, todayDateOnly } from '@/lib/utils/date'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useLiveRefresh } from '@/features/notifications/live'
import { storeMetricsApi, storeMetricsKeys, useMetricFeeds, useMetricsSummary, useStoreMetrics } from './api'
import { useStoreMetricsText, type StoreMetricsDictionary } from './i18n'
import {
  countryName,
  feedStatusView,
  formatChange,
  formatMetric,
  formatPercent,
  METRIC_KEYS,
  metricLabel,
  metricValue,
  SOURCES,
  sourceLabel,
} from './labels'
import { MetricChart } from './MetricChart'
import {
  MetricFeedStatus,
  type MetricFilter,
  type MetricKey,
  type MetricRange,
  type StoreMetricBreakdownDto,
  type StoreMetricChangesDto,
  type StoreMetricFeedDto,
  type StoreMetricValuesDto,
  type TrafficSource,
} from './types'

// Store page performance (ASO conversion analytics): the app tab with filters, KPI tiles, a daily chart, breakdowns and
// the data sources of each store, and the organization summary. Everything is read from storeasy's daily rows; the only
// store write is enabling the App Store analytics reports (confirmed, ManageConnections).

const LIVE_TYPES = ['job.succeeded', 'job.failed'] as const
const PRESET_DAYS = [7, 30, 90] as const

type RangeChoice = { days: number } | { custom: DateRange }

function toRange(choice: RangeChoice): MetricRange {
  return 'days' in choice ? { Days: choice.days } : { From: choice.custom.from, To: choice.custom.to }
}

/** 7 / 30 / 90 days, or a custom period (ending at the latest yesterday). */
function RangePicker({ value, onChange }: { value: RangeChoice; onChange: (value: RangeChoice) => void }) {
  const t = useStoreMetricsText()
  return (
    <div className="toolbar" role="group" aria-label={t.range.label}>
      {PRESET_DAYS.map((days) => (
        <button
          key={days}
          type="button"
          className={cn('chip', 'days' in value && value.days === days && 'on')}
          aria-pressed={'days' in value && value.days === days}
          onClick={() => onChange({ days })}
        >
          {interpolate(t.range.days, { days })}
        </button>
      ))}
      <DateRangePicker
        label={t.range.customLabel}
        value={'custom' in value ? value.custom : null}
        max={addDays(todayDateOnly(), -1)}
        onChange={(range) => onChange(range ? { custom: range } : { days: 30 })}
      />
    </div>
  )
}

function ChangeBadge({ change }: { change: number | null | undefined }) {
  const t = useStoreMetricsText()
  const { lang } = useI18n()
  const text = formatChange(change, lang)
  if (text == null || change == null) {
    return <span className="text-xs text-fg3">{t.change.none}</span>
  }
  const up = change > 0
  const label =
    change === 0 ? t.change.flat : interpolate(up ? t.change.up : t.change.down, { value: formatPercent(Math.abs(change), lang) })
  return (
    <span
      className={cn('inline-flex items-center gap-1 text-xs tabular-nums', up ? 'text-success' : change < 0 ? 'text-danger' : 'text-fg3')}
      title={label}
    >
      {up ? <TrendingUp className="h-3.5 w-3.5" aria-hidden /> : change < 0 ? <TrendingDown className="h-3.5 w-3.5" aria-hidden /> : null}
      <span aria-hidden>{text}</span>
      <span className="sr-only">{label}</span>
    </span>
  )
}

function KpiTiles({ totals, changes }: { totals: StoreMetricValuesDto; changes: StoreMetricChangesDto }) {
  const t = useStoreMetricsText()
  const { lang } = useI18n()
  const hints: Record<MetricKey, string> = {
    Impressions: t.metricHint.impressions,
    PageViews: t.metricHint.pageViews,
    Downloads: t.metricHint.downloads,
    ConversionRate: t.metricHint.conversionRate,
  }
  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      {METRIC_KEYS.map((metric) => {
        const value = metricValue(totals, metric)
        return (
          <section key={metric} className="card" aria-label={metricLabel(metric, t)}>
            <div className="card-b grid gap-1">
              <span className="text-xs text-fg2" title={hints[metric]}>
                {metricLabel(metric, t)}
              </span>
              <b className="text-2xl tabular-nums">{value == null ? t.notReported : formatMetric(metric, value, lang)}</b>
              {value == null ? (
                <span className="text-xs text-fg3">{hints[metric]}</span>
              ) : (
                <ChangeBadge change={metricValue(changes, metric)} />
              )}
            </div>
          </section>
        )
      })}
    </div>
  )
}

function BreakdownTable({
  title,
  rows,
  name,
}: {
  title: string
  rows: StoreMetricBreakdownDto[]
  name: (row: StoreMetricBreakdownDto) => string
}) {
  const t = useStoreMetricsText()
  const { lang } = useI18n()
  return (
    <section className="card" aria-label={title}>
      <div className="card-h">
        <h2 className="h3">{title}</h2>
      </div>
      {rows.length === 0 ? (
        <div className="card-b text-[13px] text-fg2">{t.breakdown.empty}</div>
      ) : (
        <div className="tbl-w">
          <table className="tbl">
            <thead>
              <tr>
                <th scope="col">{t.breakdown.name}</th>
                <th scope="col" className="text-right">
                  {t.metric.pageViews}
                </th>
                <th scope="col" className="text-right">
                  {t.metric.downloads}
                </th>
                <th scope="col" className="text-right">
                  {t.metric.conversionRate}
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => {
                const label = name(row)
                return (
                  <tr key={label}>
                    <th scope="row" className="font-normal">
                      {label}
                    </th>
                    <td className="text-right tabular-nums">{formatMetric('PageViews', row.Totals.PageViews, lang)}</td>
                    <td className="text-right tabular-nums">{formatMetric('Downloads', row.Totals.Downloads, lang)}</td>
                    <td className="text-right tabular-nums">{formatMetric('ConversionRate', row.Totals.ConversionRate, lang)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function BucketForm({ feed, organizationId, canManage }: { feed: StoreMetricFeedDto; organizationId: number; canManage: boolean }) {
  const t = useStoreMetricsText()
  const { t: base } = useI18n()
  const queryClient = useQueryClient()
  const [bucket, setBucket] = useState(feed.ReportBucket ?? '')
  const [error, setError] = useState<string | null>(null)
  const save = useMutation({
    mutationFn: () => storeMetricsApi.setBucket(organizationId, feed.ConnectionId ?? 0, bucket),
    meta: { silentError: true },
    onSuccess: (stored) => {
      setError(null)
      setBucket(stored ?? '')
      showToast(stored ? t.google.saved : t.google.cleared, 'success')
      void queryClient.invalidateQueries({ queryKey: storeMetricsKeys.all })
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  if (!canManage) {
    return feed.ReportBucket ? (
      <span className="mono text-xs text-fg2">{feed.ReportBucket}</span>
    ) : (
      <span className="text-xs text-fg3">{t.google.noPermission}</span>
    )
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    save.mutate()
  }

  return (
    <form className="grid gap-2" onSubmit={submit}>
      <Field label={t.google.bucket} hint={t.google.bucketHint} error={error ?? undefined}>
        <div className="flex flex-wrap gap-2">
          <Input
            mono
            className="min-w-[240px] flex-1"
            value={bucket}
            placeholder={t.google.bucketPlaceholder}
            aria-label={t.google.bucket}
            onChange={(event) => setBucket(event.target.value)}
          />
          <Button type="submit" loading={save.isPending} disabled={(feed.ReportBucket ?? '') === bucket.trim()}>
            {t.google.save}
          </Button>
        </div>
      </Field>
    </form>
  )
}

function FeedHint({ feed, t }: { feed: StoreMetricFeedDto; t: StoreMetricsDictionary }) {
  if (feed.ConnectionId == null) {
    return <p className="m-0 text-xs text-fg3">{t.feeds.noConnection}</p>
  }
  let text: string | null = null
  if (feed.Status === MetricFeedStatus.AccessDenied) {
    text =
      feed.Store === StoreKind.Apple
        ? t.apple.accessDenied
        : feed.Store === StoreKind.Google
          ? t.google.accessDenied
          : t.microsoft.accessDenied
  } else if (feed.Status === MetricFeedStatus.Failed) {
    text = t.failedHint
  } else if (feed.Store === StoreKind.Apple) {
    text =
      feed.Status === MetricFeedStatus.NotEnabled ? t.apple.notEnabled : feed.Status === MetricFeedStatus.Waiting ? t.apple.waiting : null
  } else if (feed.Store === StoreKind.Google) {
    text = feed.Status === MetricFeedStatus.NeedsSetup ? t.google.needsSetup : null
  } else if (feed.Status === MetricFeedStatus.Waiting) {
    text = t.microsoft.waiting
  }
  const tone = feed.Status === MetricFeedStatus.AccessDenied || feed.Status === MetricFeedStatus.Failed ? 'warning' : 'info'
  return text ? (
    <Callout tone={tone}>
      {text}
      {feed.LastError ? <span className="mono mt-1 block text-xs text-fg3">{feed.LastError}</span> : null}
    </Callout>
  ) : null
}

function FeedRow({
  feed,
  organizationId,
  can,
}: {
  feed: StoreMetricFeedDto
  organizationId: number
  can: (permission: OrgPermission) => boolean
}) {
  const t = useStoreMetricsText()
  const { t: base } = useI18n()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const canManage = can(OrgPermission.ManageConnections)
  const status = feedStatusView(feed.Status, t)
  const enable = useMutation({
    mutationFn: () => storeMetricsApi.enableApple(organizationId, feed.StoreAppId),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.apple.started, 'success')
      void queryClient.invalidateQueries({ queryKey: storeMetricsKeys.all })
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  const askEnable = async () => {
    const ok = await confirm({ title: t.apple.confirmTitle, message: t.apple.confirmBody, confirmLabel: t.apple.confirmLabel })
    if (ok) {
      enable.mutate()
    }
  }

  return (
    <li className="grid gap-2 border-b border-border py-3 last:border-b-0">
      <div className="flex flex-wrap items-center gap-2">
        <StoreTag store={feed.Store} connectionId={feed.ConnectionId} small />
        {feed.ConnectionName ? <span className="text-xs text-fg3">{feed.ConnectionName}</span> : null}
        <Badge small tone={status.tone}>
          {status.label}
        </Badge>
        <span className="ml-auto text-xs text-fg3">
          {feed.LastFetchedDate ? interpolate(t.feeds.lastFetched, { time: format.relative(feed.LastFetchedDate) }) : t.feeds.neverFetched}
          {feed.LatestDataDate ? ` · ${interpolate(t.feeds.dataUntil, { date: format.date(feed.LatestDataDate) })}` : null}
        </span>
      </div>
      {feed.EnabledByUserName && feed.EnabledDate ? (
        <span className="text-xs text-fg3">
          {interpolate(t.feeds.enabledBy, { user: feed.EnabledByUserName, date: format.date(feed.EnabledDate) })}
        </span>
      ) : null}
      <FeedHint feed={feed} t={t} />
      {feed.Store === StoreKind.Apple && feed.CanEnable ? (
        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="primary"
            size="sm"
            loading={enable.isPending}
            disabled={!canManage}
            title={!canManage ? t.apple.noPermission : undefined}
            onClick={() => void askEnable()}
          >
            {enable.isPending ? t.apple.enabling : t.apple.enable}
          </Button>
          {!canManage ? <span className="text-xs text-fg3">{t.apple.noPermission}</span> : null}
        </div>
      ) : null}
      {feed.Store === StoreKind.Google && feed.ConnectionId != null ? (
        <BucketForm feed={feed} organizationId={organizationId} canManage={canManage} />
      ) : null}
    </li>
  )
}

function Feeds({
  feeds,
  organizationId,
  can,
}: {
  feeds: StoreMetricFeedDto[]
  organizationId: number
  can: (permission: OrgPermission) => boolean
}) {
  const t = useStoreMetricsText()
  return (
    <section className="card" aria-label={t.feeds.title}>
      <div className="card-h">
        <h2 className="h3">{t.feeds.title}</h2>
      </div>
      <ul className="card-b m-0 grid list-none p-0 px-4">
        {feeds.map((feed) => (
          <FeedRow key={feed.StoreAppId} feed={feed} organizationId={organizationId} can={can} />
        ))}
      </ul>
    </section>
  )
}

function StoreMetrics({ context }: { context: WorkspaceContext }) {
  const t = useStoreMetricsText()
  const { t: base, lang } = useI18n()
  const format = useFormat()
  const queryClient = useQueryClient()
  const { app, organizationId, can } = context
  const [range, setRange] = useState<RangeChoice>({ days: 30 })
  const [store, setStore] = useState<StoreKind | null>(null)
  const [country, setCountry] = useState<string | null>(null)
  const [source, setSource] = useState<TrafficSource | null>(null)
  const [chartMetric, setChartMetric] = useState<MetricKey>('Downloads')
  useLiveRefresh(LIVE_TYPES, storeMetricsKeys.all)

  const filter: MetricFilter = useMemo(
    () => ({ ...toRange(range), Store: store, Country: country, Source: source }),
    [range, store, country, source],
  )
  const report = useStoreMetrics(organizationId, app.Id, filter)
  const feeds = useMetricFeeds(organizationId, app.Id)
  const editor = can(OrgPermission.EditContent)

  const sync = useMutation({
    mutationFn: () => storeMetricsApi.sync(organizationId, app.Id),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.fetchStarted, 'success')
      void queryClient.invalidateQueries({ queryKey: storeMetricsKeys.feeds(organizationId, app.Id) })
    },
    onError: (failure) =>
      showToast(
        failure instanceof ApiError && failure.keys.includes('StoreAppNotFound')
          ? t.fetchNoRecord
          : apiErrorMessage(failure, base.common.error),
        'error',
      ),
  })

  const stores = useMemo(() => [...new Set((feeds.data ?? []).map((feed) => feed.Store))].sort(), [feeds.data])
  const data = report.data
  const filtered = store != null || country != null || source != null || !('days' in range)
  const countryOptions = useMemo(() => {
    const codes = new Set((data?.Countries ?? []).map((row) => row.Country ?? ''))
    if (country) {
      codes.add(country)
    }
    return [
      { value: '', label: t.filters.allCountries },
      ...[...codes].filter(Boolean).map((code) => ({ value: code, label: countryName(code, lang, t) })),
    ]
  }, [data?.Countries, country, lang, t])

  const chartPoints = (data?.Days ?? []).map((day) => ({ date: day.Date, value: metricValue(day, chartMetric) }))

  return (
    <>
      <PageHeader
        title={t.title}
        subtitle={t.subtitle}
        actions={
          <Button
            loading={sync.isPending}
            disabled={!editor}
            title={!editor ? t.fetchNoPermission : undefined}
            onClick={() => sync.mutate()}
          >
            <RefreshCw aria-hidden />
            {sync.isPending ? t.fetching : t.fetchNow}
          </Button>
        }
      />
      <p className="mb-3 mt-0 text-xs text-fg3">{t.delayNote}</p>

      <div className="mb-3 flex flex-wrap items-center gap-2">
        <RangePicker
          value={range}
          onChange={(next) => {
            setRange(next)
          }}
        />
        {stores.length > 1 ? (
          <div className="toolbar" role="group" aria-label={t.filters.store}>
            <button type="button" className={cn('chip', store == null && 'on')} aria-pressed={store == null} onClick={() => setStore(null)}>
              {t.filters.allStores}
            </button>
            {stores.map((item) => (
              <button
                key={item}
                type="button"
                className={cn('chip', store === item && 'on')}
                aria-pressed={store === item}
                onClick={() => setStore(item)}
              >
                {storeName(item, base.enums.store)}
              </button>
            ))}
          </div>
        ) : null}
        <Select<string>
          className="w-[190px]"
          aria-label={t.filters.country}
          value={country ?? ''}
          searchable
          options={countryOptions}
          onChange={(value) => setCountry(value || null)}
        />
        <Select<number>
          className="w-[190px]"
          aria-label={t.filters.source}
          value={source ?? -1}
          options={[{ value: -1, label: t.filters.allSources }, ...SOURCES.map((item) => ({ value: item, label: sourceLabel(item, t) }))]}
          onChange={(value) => setSource(value === -1 ? null : (value as TrafficSource))}
        />
      </div>

      {report.isLoading ? (
        <CardSkeleton />
      ) : report.isError || !data ? (
        <ScreenState error={report.error} onRetry={() => void report.refetch()} />
      ) : (
        <div className="grid gap-4">
          <p className="m-0 text-xs text-fg3">
            {interpolate(t.range.showing, {
              from: format.date(data.From),
              to: format.date(data.To),
              previousFrom: format.date(data.PreviousFrom),
              previousTo: format.date(data.PreviousTo),
            })}
          </p>
          {data.Days.length === 0 ? (
            <EmptyState
              icon={<BarChart3 aria-hidden />}
              title={filtered ? t.empty.filtered : t.empty.title}
              description={filtered ? undefined : t.empty.body}
            />
          ) : (
            <>
              <KpiTiles totals={data.Totals} changes={data.Changes} />
              <section className="card" aria-label={t.chart.title}>
                <div className="card-h flex flex-wrap items-center gap-2">
                  <h2 className="h3">{t.chart.title}</h2>
                  <div className="toolbar ml-auto" role="group" aria-label={t.chart.pick}>
                    {METRIC_KEYS.map((metric) => (
                      <button
                        key={metric}
                        type="button"
                        className={cn('chip', chartMetric === metric && 'on')}
                        aria-pressed={chartMetric === metric}
                        onClick={() => setChartMetric(metric)}
                      >
                        {metricLabel(metric, t)}
                      </button>
                    ))}
                  </div>
                </div>
                <div className="card-b">
                  {chartPoints.some((point) => point.value != null) ? (
                    <MetricChart
                      points={chartPoints}
                      label={interpolate(t.chart.label, {
                        metric: metricLabel(chartMetric, t),
                        from: format.date(data.From),
                        to: format.date(data.To),
                      })}
                      formatValue={(value) => formatMetric(chartMetric, value, lang)}
                      formatDate={(date: DateOnly) => format.date(date)}
                    />
                  ) : (
                    <p className="m-0 text-[13px] text-fg2">{t.chart.empty}</p>
                  )}
                </div>
              </section>
              <div className="grid gap-4 lg:grid-cols-2">
                <div className="grid content-start gap-2">
                  <BreakdownTable title={t.breakdown.sources} rows={data.Sources} name={(row) => sourceLabel(row.Source, t)} />
                  {country && stores.includes(StoreKind.Google) ? (
                    <p className="m-0 text-xs text-fg3">{t.breakdown.playSourceNote}</p>
                  ) : null}
                </div>
                <BreakdownTable title={t.breakdown.countries} rows={data.Countries} name={(row) => countryName(row.Country, lang, t)} />
              </div>
              {data.Stores.length > 1 ? (
                <BreakdownTable
                  title={t.breakdown.stores}
                  rows={data.Stores}
                  name={(row) => (row.Store == null ? '-' : storeName(row.Store, base.enums.store))}
                />
              ) : null}
            </>
          )}
        </div>
      )}

      <div className="mt-4">
        {feeds.isLoading ? (
          <CardSkeleton count={1} />
        ) : feeds.isError || !feeds.data ? (
          <ScreenState error={feeds.error} onRetry={() => void feeds.refetch()} />
        ) : (
          <Feeds feeds={feeds.data} organizationId={organizationId} can={can} />
        )}
      </div>
    </>
  )
}

/** The app's Store performance tab. */
export function StoreMetricsPage() {
  return (
    <AppWorkspace section="storePerformance" appWide>
      {(context) => <StoreMetrics context={context} />}
    </AppWorkspace>
  )
}

/** Every app of the organization over one period. */
export function OrgStoreMetricsPage() {
  const t = useStoreMetricsText()
  const { lang } = useI18n()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const [range, setRange] = useState<RangeChoice>({ days: 30 })
  const summary = useMetricsSummary(organization.Id, toRange(range))
  const queryClient = useQueryClient()
  useLiveRefresh(LIVE_TYPES, storeMetricsKeys.all)
  const sync = useMutation({
    mutationFn: () => storeMetricsApi.sync(organization.Id, null),
    meta: { silentError: true },
    onSuccess: () => showToast(t.fetchStarted, 'success'),
    onError: () => showToast(t.fetchNoRecord, 'error'),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: storeMetricsKeys.all }),
  })
  const data = summary.data

  return (
    <>
      <PageHeader
        title={t.orgTitle}
        subtitle={t.orgSubtitle}
        actions={
          <Button
            loading={sync.isPending}
            disabled={!can(OrgPermission.EditContent)}
            title={!can(OrgPermission.EditContent) ? t.fetchNoPermission : undefined}
            onClick={() => sync.mutate()}
          >
            <RefreshCw aria-hidden />
            {t.fetchNow}
          </Button>
        }
      />
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <RangePicker value={range} onChange={setRange} />
      </div>
      {summary.isLoading ? (
        <CardSkeleton />
      ) : summary.isError || !data ? (
        <ScreenState error={summary.error} onRetry={() => void summary.refetch()} />
      ) : data.Apps.length === 0 ? (
        <EmptyState icon={<BarChart3 aria-hidden />} title={t.empty.title} description={t.summary.empty} />
      ) : (
        <div className="grid gap-4">
          <p className="m-0 text-xs text-fg3">
            {format.date(data.From)} → {format.date(data.To)}
          </p>
          <KpiTiles totals={data.Totals} changes={data.Changes} />
          <section className="card" aria-label={t.summary.total}>
            <div className="tbl-w">
              <table className="tbl">
                <thead>
                  <tr>
                    <th scope="col">{t.summary.app}</th>
                    {METRIC_KEYS.map((metric) => (
                      <th key={metric} scope="col" className="text-right">
                        {metricLabel(metric, t)}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {data.Apps.map((item) => (
                    <tr key={item.AppId}>
                      <th scope="row" className="font-normal">
                        <Link
                          className="lnk"
                          to={`/apps/${item.AppId}/store-performance`}
                          aria-label={interpolate(t.summary.open, { app: item.AppName })}
                        >
                          {item.AppName}
                        </Link>
                      </th>
                      {METRIC_KEYS.map((metric) => (
                        <td key={metric} className="text-right">
                          <span className="grid justify-items-end gap-0.5">
                            <span className="tabular-nums">{formatMetric(metric, metricValue(item.Totals, metric), lang)}</span>
                            {metricValue(item.Totals, metric) != null ? <ChangeBadge change={metricValue(item.Changes, metric)} /> : null}
                          </span>
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </div>
      )}
    </>
  )
}
