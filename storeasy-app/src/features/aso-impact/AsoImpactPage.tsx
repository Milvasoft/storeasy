import { useId, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { BarChart3, History } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { CardSkeleton, EmptyState, Input, PageHeader } from '@/components/ui/primitives'
import { useFormat, useI18n } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useMetricFeeds } from '@/features/store-metrics/api'
import { useStoreMetricsText } from '@/features/store-metrics/i18n'
import { formatMetric, metricLabel, SOURCES, sourceLabel } from '@/features/store-metrics/labels'
import { linePaths, niceMax } from '@/features/store-metrics/MetricChart'
import type { MetricKey, TrafficSource } from '@/features/store-metrics/types'
import { useImpactEvents, useImpactReport } from './api'
import { ChangeArrow } from './ChangeArrow'
import { useImpactText, type ImpactDictionary } from './i18n'
import { eventLink, eventTitle, formatRank, IMPACT_METRICS, signalView, statusView } from './labels'
import {
  ImpactDateSource,
  ImpactMetric,
  ImpactSignal,
  ImpactStatus,
  type AsoImpactDayDto,
  type AsoImpactEventDto,
  type AsoImpactKeywordDto,
  type AsoImpactMetricDto,
  type AsoImpactRanksDto,
  type AsoImpactReportDto,
} from './types'

// The Impact part of the Store performance section: the app's changes as a timeline and, for the chosen one, the before
// and after cards, a paired chart, keyword rank changes and the confounding warning. Read only.
//
// Why a part of Store performance and not a tab of its own: the app already has fifteen tabs, the numbers compared are
// the store performance metrics (no metrics, no impact, so the empty state points to that tab's setup), and the chart
// markers of that tab open this view. The chip row under the tab ("Overview", "Impact") is the pattern the listing,
// media and release sections already use.

export const WINDOW_OPTIONS = [7, 14, 28] as const
export const GAP_OPTIONS = [0, 1, 2, 3, 5, 7] as const
const DEFAULT_WINDOW = 14
const DEFAULT_GAP = 2

function Timeline({
  events,
  selected,
  onSelect,
  t,
}: {
  events: AsoImpactEventDto[]
  selected: string | null
  onSelect: (id: string) => void
  t: ImpactDictionary
}) {
  const format = useFormat()
  return (
    <section className="card" aria-label={t.timeline.title}>
      <div className="card-h">
        <h2 className="h3">{t.timeline.title}</h2>
      </div>
      {events.length === 0 ? (
        <div className="card-b text-[13px] text-fg2">{t.timeline.empty}</div>
      ) : (
        <ol className="m-0 grid list-none gap-0 p-0">
          {events.map((event) => {
            const on = event.Id === selected
            const awaiting = event.DateSource === ImpactDateSource.AwaitingRelease
            return (
              <li key={event.Id} className="border-b border-border last:border-b-0">
                <button
                  type="button"
                  className={cn('grid w-full gap-1 px-4 py-2.5 text-left hover:bg-bg2', on && 'bg-bg2')}
                  aria-pressed={on}
                  onClick={() => onSelect(event.Id)}
                >
                  <span className="flex flex-wrap items-center gap-2 text-xs text-fg3">
                    <time
                      dateTime={event.Date}
                      title={event.DateSource === ImpactDateSource.StoreLive ? t.timeline.live : t.timeline.applied}
                    >
                      {format.date(event.Date)}
                    </time>
                    <StoreTag store={event.Store} small />
                    {awaiting ? (
                      <Badge small tone="warning">
                        {t.timeline.awaiting}
                      </Badge>
                    ) : null}
                  </span>
                  <span className="text-[13px] font-medium">{eventTitle(event, t)}</span>
                  {event.Fields.length > 0 ? (
                    <span className="truncate text-xs text-fg3">{interpolate(t.timeline.fields, { list: event.Fields.join(', ') })}</span>
                  ) : null}
                </button>
              </li>
            )
          })}
        </ol>
      )}
    </section>
  )
}

function KpiCard({ metric, report, t }: { metric: AsoImpactMetricDto; report: AsoImpactReportDto; t: ImpactDictionary }) {
  const { lang } = useI18n()
  const store = useStoreMetricsText()
  const key = IMPACT_METRICS.find((item) => item.metric === metric.Metric)?.key ?? 'Downloads'
  const rate = metric.Metric === ImpactMetric.ConversionRate
  const signal = signalView(report.Conversion.Signal, t)
  const label = metricLabel(key, store)
  return (
    <section className="card" aria-label={label}>
      <div className="card-b grid gap-1">
        <span className="text-xs text-fg2">
          {label}
          {rate ? null : ` (${t.kpi.perDay})`}
        </span>
        <span className="flex flex-wrap items-baseline gap-2">
          <span className="text-sm tabular-nums text-fg3" title={t.kpi.before}>
            {formatMetric(key, metric.Before, lang)}
          </span>
          <span aria-hidden className="text-fg3">
            →
          </span>
          <b className="text-2xl tabular-nums" title={t.kpi.after}>
            {formatMetric(key, metric.After, lang)}
          </b>
        </span>
        <span className="sr-only">
          {t.kpi.before}: {formatMetric(key, metric.Before, lang)}, {t.kpi.after}: {formatMetric(key, metric.After, lang)}
        </span>
        <ChangeArrow change={metric.RelativeChange} />
        {rate && metric.AbsoluteChange != null ? (
          <span className="text-xs text-fg3">
            {interpolate(t.kpi.points, {
              value: new Intl.NumberFormat(lang === 'tr' ? 'tr-TR' : 'en-US', {
                maximumFractionDigits: 2,
                signDisplay: 'exceptZero',
              }).format(metric.AbsoluteChange * 100),
            })}
          </span>
        ) : null}
        {rate ? (
          <span className="flex flex-wrap items-center gap-1.5" title={t.signal.hint}>
            <Badge small tone={signal.tone}>
              {signal.label}
            </Badge>
            {report.Conversion.PValue != null && report.Conversion.Signal !== ImpactSignal.InsufficientData ? (
              <span className="text-xs tabular-nums text-fg3">
                {interpolate(t.signal.pValue, { value: report.Conversion.PValue.toFixed(3) })}
              </span>
            ) : null}
          </span>
        ) : null}
      </div>
    </section>
  )
}

/** Day by day: the before window as a dashed line, the after window as the accent line, both on day 1..N. */
export function PairedChart({
  before,
  after,
  metric,
  label,
  formatValue,
  t,
}: {
  before: AsoImpactDayDto[]
  after: AsoImpactDayDto[]
  metric: MetricKey
  label: string
  formatValue: (value: number) => string
  t: ImpactDictionary
}) {
  const titleId = useId()
  const width = 640
  const height = 180
  const pad = { top: 10, right: 10, bottom: 22, left: 52 }
  const value = (day: AsoImpactDayDto): number | null => day[metric] ?? null
  const length = Math.max(before.length, after.length)
  const values = [...before, ...after].map(value).filter((item): item is number => item != null)
  const top = niceMax(Math.max(0, ...values))
  const innerWidth = width - pad.left - pad.right
  const innerHeight = height - pad.top - pad.bottom
  const x = (index: number) => pad.left + (length > 1 ? (index * innerWidth) / (length - 1) : innerWidth / 2)
  const y = (item: number) => pad.top + innerHeight - (item / top) * innerHeight
  const line = (days: AsoImpactDayDto[]) =>
    linePaths(days.map((day, index) => ({ x: x(index), y: value(day) == null ? null : y(value(day) as number) })))

  return (
    <figure className="m-0 grid gap-2">
      <svg viewBox={`0 0 ${width} ${height}`} className="h-auto w-full" role="img" aria-labelledby={titleId}>
        <title id={titleId}>{label}</title>
        {[0, 0.5, 1].map((share) => (
          <g key={share}>
            <line
              x1={pad.left}
              x2={width - pad.right}
              y1={y(top * share)}
              y2={y(top * share)}
              className="stroke-border"
              strokeDasharray={share === 0 ? undefined : '3 4'}
            />
            <text x={pad.left - 8} y={y(top * share) + 4} textAnchor="end" className="fill-fg3 text-[11px]">
              {formatValue(top * share)}
            </text>
          </g>
        ))}
        {line(before).map((path) => (
          <path key={`b${path}`} d={path} fill="none" className="stroke-fg3" strokeWidth={2} strokeDasharray="5 4" data-series="before" />
        ))}
        {line(after).map((path) => (
          <path key={`a${path}`} d={path} fill="none" className="stroke-accent" strokeWidth={2.5} data-series="after" />
        ))}
        <text x={pad.left} y={height - 6} className="fill-fg3 text-[11px]">
          {interpolate(t.chart.day, { n: 1 })}
        </text>
        <text x={width - pad.right} y={height - 6} textAnchor="end" className="fill-fg3 text-[11px]">
          {interpolate(t.chart.day, { n: length })}
        </text>
      </svg>
      <ul className="m-0 flex list-none flex-wrap gap-3 p-0 text-xs">
        <li className="inline-flex items-center gap-1 text-fg3">
          <span aria-hidden>┅</span> {t.chart.before}
        </li>
        <li className="inline-flex items-center gap-1 text-accent">
          <span aria-hidden>━</span> {t.chart.after}
        </li>
      </ul>
      <table className="sr-only">
        <caption>{label}</caption>
        <thead>
          <tr>
            <th scope="col">#</th>
            <th scope="col">{t.chart.before}</th>
            <th scope="col">{t.chart.after}</th>
          </tr>
        </thead>
        <tbody>
          {Array.from({ length }, (_, index) => (
            <tr key={index}>
              <th scope="row">{interpolate(t.chart.day, { n: index + 1 })}</th>
              <td>{before[index] && value(before[index]) != null ? formatValue(value(before[index]) as number) : '-'}</td>
              <td>{after[index] && value(after[index]) != null ? formatValue(value(after[index]) as number) : '-'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}

function RankChange({ keyword, t }: { keyword: AsoImpactKeywordDto; t: ImpactDictionary }) {
  const { lang } = useI18n()
  if (keyword.Change == null) {
    if (keyword.Before == null && keyword.After != null && keyword.BeforeCheckedDays > 0) {
      return <span className="text-xs text-success">{t.ranks.entered}</span>
    }
    if (keyword.Before != null && keyword.After == null && keyword.AfterCheckedDays > 0) {
      return <span className="text-xs text-danger">{t.ranks.dropped}</span>
    }
    return <span className="text-xs text-fg3">-</span>
  }
  if (Math.abs(keyword.Change) < 0.05) {
    return <span className="text-xs text-fg3">{t.kpi.flat}</span>
  }
  const better = keyword.Change < 0
  return (
    <span className={cn('text-xs tabular-nums', better ? 'text-success' : 'text-danger')}>
      {interpolate(better ? t.ranks.up : t.ranks.down, { value: formatRank(Math.abs(keyword.Change), lang) })}
    </span>
  )
}

function Ranks({ ranks, context, t }: { ranks: AsoImpactRanksDto; context: WorkspaceContext; t: ImpactDictionary }) {
  const { lang } = useI18n()
  return (
    <section className="card" aria-label={t.ranks.title}>
      <div className="card-h grid gap-1">
        <h2 className="h3">{t.ranks.title}</h2>
        {ranks.KeywordCount > 0 ? (
          <span className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-fg3">
            {ranks.Compared > 0 ? (
              <span>
                {interpolate(t.ranks.summary, {
                  count: ranks.Compared,
                  before: formatRank(ranks.BeforeAverage, lang),
                  after: formatRank(ranks.AfterAverage, lang),
                })}
              </span>
            ) : null}
            <span>{interpolate(t.ranks.top10, { before: ranks.BeforeTop10, after: ranks.AfterTop10 })}</span>
            <span>{interpolate(t.ranks.moves, { improved: ranks.Improved, declined: ranks.Declined })}</span>
          </span>
        ) : null}
      </div>
      {ranks.KeywordCount === 0 ? (
        <div className="card-b text-[13px] text-fg2">
          {t.ranks.none}{' '}
          <Link className="lnk" to={`/apps/${context.app.Id}/keywords`}>
            {t.ranks.noneLink}
          </Link>
        </div>
      ) : (
        <div className="tbl-w">
          <table className="tbl">
            <thead>
              <tr>
                <th scope="col">{t.ranks.keyword}</th>
                <th scope="col">{t.ranks.country}</th>
                <th scope="col" className="text-right">
                  {t.ranks.before}
                </th>
                <th scope="col" className="text-right">
                  {t.ranks.after}
                </th>
                <th scope="col" className="text-right">
                  {t.ranks.change}
                </th>
              </tr>
            </thead>
            <tbody>
              {ranks.Keywords.map((keyword) => (
                <tr key={`${keyword.Keyword}-${keyword.Country}`}>
                  <th scope="row" className="font-normal">
                    {keyword.Keyword}
                  </th>
                  <td>{keyword.Country.toUpperCase()}</td>
                  <td className="text-right tabular-nums">
                    {keyword.Before == null ? (
                      <span className="text-fg3">{t.ranks.notRanked}</span>
                    ) : (
                      `#${formatRank(keyword.Before, lang)}`
                    )}
                  </td>
                  <td className="text-right tabular-nums">
                    {keyword.After == null ? <span className="text-fg3">{t.ranks.notRanked}</span> : `#${formatRank(keyword.After, lang)}`}
                  </td>
                  <td className="text-right">
                    <RankChange keyword={keyword} t={t} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function EventList({ events, onSelect, t }: { events: AsoImpactEventDto[]; onSelect: (id: string) => void; t: ImpactDictionary }) {
  const format = useFormat()
  return (
    <ul className="m-0 mt-1 grid list-none gap-1 p-0">
      {events.map((event) => (
        <li key={event.Id}>
          <button type="button" className="lnk text-left" onClick={() => onSelect(event.Id)}>
            {format.date(event.Date)}: {eventTitle(event, t)}
          </button>
        </li>
      ))}
    </ul>
  )
}

function Report({
  report,
  context,
  onSelect,
  t,
}: {
  report: AsoImpactReportDto
  context: WorkspaceContext
  onSelect: (id: string) => void
  t: ImpactDictionary
}) {
  const format = useFormat()
  const { lang } = useI18n()
  const store = useStoreMetricsText()
  const [chartMetric, setChartMetric] = useState<MetricKey>('Downloads')
  const status = statusView(report.Status, t)
  const link = eventLink(report.Event)
  const metrics = IMPACT_METRICS.map((item) => report.Metrics.find((metric) => metric.Metric === item.metric)).filter(
    (metric): metric is AsoImpactMetricDto => metric != null && (metric.Before != null || metric.After != null),
  )

  return (
    <div className="grid gap-4">
      <section className="card" aria-label={eventTitle(report.Event, t)}>
        <div className="card-b grid gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <StoreTag store={report.Event.Store} small />
            <h2 className="h3 m-0">{eventTitle(report.Event, t)}</h2>
            <Badge small tone={status.tone}>
              {status.label}
            </Badge>
            {link ? (
              <Link className="lnk ml-auto text-xs" to={link}>
                {report.Event.PlanId != null ? t.links.plan : t.links.version}
              </Link>
            ) : null}
          </div>
          {report.Event.Title ? <span className="text-xs text-fg2">{report.Event.Title}</span> : null}
          <span className="text-xs text-fg3">
            {interpolate(t.windows, {
              beforeFrom: format.date(report.Before.From),
              beforeTo: format.date(report.Before.To),
              afterFrom: format.date(report.After.From),
              afterTo: format.date(report.After.To),
            })}
          </span>
          <span className="text-xs text-fg3">{report.WeekdayMatched ? t.weekday.matched : report.WholeWeeks ? t.weekday.whole : null}</span>
        </div>
      </section>

      {report.Status === ImpactStatus.Collecting ? (
        <Callout tone="info">{interpolate(t.status.collectingBody, { date: format.date(report.ReadyDate) })}</Callout>
      ) : null}
      {report.Status === ImpactStatus.AwaitingRelease ? <Callout tone="warning">{t.status.awaitingBody}</Callout> : null}
      {report.Status === ImpactStatus.NoData ? (
        <Callout tone="neutral">
          {t.status.noDataBody}{' '}
          <Link className="lnk" to={`/apps/${context.app.Id}/store-performance`}>
            {t.empty.metricsLink}
          </Link>
        </Callout>
      ) : null}

      {report.Confounders.length > 0 ? (
        <Callout tone="warning" title={t.confounded.title}>
          {t.confounded.body}
          <EventList events={report.Confounders} onSelect={onSelect} t={t} />
        </Callout>
      ) : null}
      {report.Related.length > 0 ? (
        <p className="m-0 text-xs text-fg3">
          {interpolate(t.related, { list: report.Related.map((event) => eventTitle(event, t)).join(', ') })}
        </p>
      ) : null}

      {metrics.length > 0 && report.Status !== ImpactStatus.AwaitingRelease ? (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            {metrics.map((metric) => (
              <KpiCard key={metric.Metric} metric={metric} report={report} t={t} />
            ))}
          </div>
          <section className="card" aria-label={t.chart.title}>
            <div className="card-h flex flex-wrap items-center gap-2">
              <h2 className="h3">{t.chart.title}</h2>
              <div className="toolbar ml-auto" role="group" aria-label={store.chart.pick}>
                {metrics.map((metric) => {
                  const key = IMPACT_METRICS.find((item) => item.metric === metric.Metric)?.key ?? 'Downloads'
                  return (
                    <button
                      key={key}
                      type="button"
                      className={cn('chip', chartMetric === key && 'on')}
                      aria-pressed={chartMetric === key}
                      onClick={() => setChartMetric(key)}
                    >
                      {metricLabel(key, store)}
                    </button>
                  )
                })}
              </div>
            </div>
            <div className="card-b">
              <PairedChart
                before={report.BeforeDays}
                after={report.AfterDays}
                metric={chartMetric}
                label={interpolate(t.chart.label, { metric: metricLabel(chartMetric, store), days: report.WindowDays })}
                formatValue={(value) => formatMetric(chartMetric, value, lang)}
                t={t}
              />
            </div>
          </section>
        </>
      ) : null}

      {report.Ranks ? <Ranks ranks={report.Ranks} context={context} t={t} /> : null}
    </div>
  )
}

function Impact({ context }: { context: WorkspaceContext }) {
  const t = useImpactText()
  const store = useStoreMetricsText()
  const { app, organizationId } = context
  const [search, setSearch] = useSearchParams()
  const [windowDays, setWindowDays] = useState<number>(DEFAULT_WINDOW)
  const [gapDays, setGapDays] = useState<number>(DEFAULT_GAP)
  const [country, setCountry] = useState('')
  const [source, setSource] = useState<TrafficSource | null>(null)
  const events = useImpactEvents(organizationId, app.Id)
  const feeds = useMetricFeeds(organizationId, app.Id)
  const requested = search.get('event')
  const selected = requested ?? events.data?.find((event) => event.DateSource !== ImpactDateSource.AwaitingRelease)?.Id ?? null
  const countryCode = /^[a-z]{2}$/i.test(country.trim()) ? country.trim().toUpperCase() : null
  const options = useMemo(
    () => ({ WindowDays: windowDays, GapDays: gapDays, Country: countryCode, Source: source }),
    [windowDays, gapDays, countryCode, source],
  )
  const report = useImpactReport(organizationId, app.Id, selected, options)
  const noMetrics = feeds.data != null && !feeds.data.some((feed) => feed.LatestDataDate != null)

  const select = (id: string) => {
    const next = new URLSearchParams(search)
    next.set('event', id)
    setSearch(next, { replace: true })
  }

  return (
    <>
      <PageHeader title={t.title} subtitle={t.subtitle} />
      {noMetrics ? (
        <div className="mb-4">
          <EmptyState
            icon={<BarChart3 aria-hidden />}
            title={t.empty.metricsTitle}
            description={t.empty.metricsBody}
            action={
              <Link className="btn" to={`/apps/${app.Id}/store-performance`}>
                {t.empty.metricsLink}
              </Link>
            }
          />
        </div>
      ) : null}
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <div className="toolbar" role="group" aria-label={t.options.window}>
          {WINDOW_OPTIONS.map((days) => (
            <button
              key={days}
              type="button"
              className={cn('chip', windowDays === days && 'on')}
              aria-pressed={windowDays === days}
              onClick={() => setWindowDays(days)}
            >
              {interpolate(t.options.windowDays, { days })}
            </button>
          ))}
        </div>
        <Select<number>
          className="w-[160px]"
          aria-label={t.options.gap}
          value={gapDays}
          options={GAP_OPTIONS.map((days) => ({ value: days, label: interpolate(t.options.gapDays, { days }) }))}
          onChange={(value) => setGapDays(value)}
        />
        <Input
          className="w-[110px]"
          aria-label={t.options.country}
          placeholder={t.options.countryPlaceholder}
          maxLength={2}
          value={country}
          onChange={(event) => setCountry(event.target.value)}
        />
        <Select<number>
          className="w-[190px]"
          aria-label={t.options.source}
          value={source ?? -1}
          options={[
            { value: -1, label: t.options.allSources },
            ...SOURCES.map((item) => ({ value: item, label: sourceLabel(item, store) })),
          ]}
          onChange={(value) => setSource(value === -1 ? null : (value as TrafficSource))}
        />
      </div>
      <p className="mb-3 mt-0 text-xs text-fg3">{t.options.gapHint}</p>

      {events.isLoading ? (
        <CardSkeleton />
      ) : events.isError || !events.data ? (
        <ScreenState error={events.error} onRetry={() => void events.refetch()} />
      ) : (
        <div className="grid items-start gap-4 lg:grid-cols-[minmax(240px,1fr)_2.4fr]">
          <Timeline events={events.data} selected={selected} onSelect={select} t={t} />
          <div>
            {selected == null ? (
              <EmptyState icon={<History aria-hidden />} title={t.timeline.pick} />
            ) : report.isLoading ? (
              <CardSkeleton />
            ) : report.isError || !report.data ? (
              <ScreenState error={report.error} onRetry={() => void report.refetch()} />
            ) : (
              <Report report={report.data} context={context} onSelect={select} t={t} />
            )}
          </div>
        </div>
      )}
    </>
  )
}

/** The app's Store performance, Impact part. */
export function AsoImpactPage() {
  return (
    <AppWorkspace section="storePerformance" sub="impact" appWide>
      {(context) => <Impact context={context} />}
    </AppWorkspace>
  )
}
