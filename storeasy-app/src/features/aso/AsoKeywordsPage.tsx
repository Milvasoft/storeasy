import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowLeft, ArrowUp, Minus, Search, Sparkles, Tags } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, Input, PageHeader, Textarea } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { useFormat, useI18n, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { useAiSettings } from '@/features/ai/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useImpactEvents } from '@/features/aso-impact/api'
import { ChartMarkers, type ChartMarker } from '@/features/aso-impact/ChartMarkers'
import { useImpactText } from '@/features/aso-impact/i18n'
import { eventTitle, impactLink } from '@/features/aso-impact/labels'
import { ImpactDateSource } from '@/features/aso-impact/types'
import { asoApi, asoKeys, parseKeywords, useKeywordCoverage, useKeywordRanks, useTrackedKeywords } from './api'
import { useAsoText, type AsoDictionary } from './i18n'
import type {
  AsoCompetitorDto,
  AsoKeywordCoverageDto,
  AsoKeywordResearchDto,
  AsoKeywordSeedDto,
  AsoRankPointDto,
  AsoTrackedKeywordDto,
} from './types'

// The "Keywords" tab of an app: tracked keywords with their daily rank, research from store data (seeds, measuring,
// adding to tracking) and Apple's keywords field helper. Every score is an estimate from public store data.

const STORES = [StoreKind.Apple, StoreKind.Google]
const MAX_RESEARCH = 20
const DAY_OPTIONS = [7, 30, 90, 365]

/** The default country of the research: the UI language's usual storefront. */
export function defaultCountry(lang: string): string {
  return lang === 'tr' ? 'TR' : 'US'
}

/** A rank change as an arrow and a count; positive means the app moved up. */
function Change({ value, t }: { value: number | null; t: AsoDictionary }) {
  if (value == null) {
    return <span className="text-fg3">{t.research.unknown}</span>
  }
  if (value === 0) {
    return (
      <span className="inline-flex items-center gap-0.5 text-fg3" title={t.tracked.same}>
        <Minus className="h-3 w-3" aria-hidden />
        <span className="sr-only">{t.tracked.same}</span>
      </span>
    )
  }
  const up = value > 0
  const label = interpolate(up ? t.tracked.up : t.tracked.down, { count: Math.abs(value) })
  return (
    <span className={cn('inline-flex items-center gap-0.5 tabular-nums', up ? 'text-success' : 'text-danger')} title={label}>
      {up ? <ArrowUp className="h-3 w-3" aria-hidden /> : <ArrowDown className="h-3 w-3" aria-hidden />}
      {Math.abs(value)}
      <span className="sr-only">{label}</span>
    </span>
  )
}

/** Points of a rank line: x per day, y from rank 1 (top) to depth (bottom); a day without a rank breaks the line. */
export function rankPath(ranks: Array<number | null>, width: number, height: number, depth: number): string {
  if (ranks.length === 0) {
    return ''
  }
  const step = ranks.length > 1 ? width / (ranks.length - 1) : 0
  const max = Math.max(2, depth)
  let path = ''
  let pen = false
  ranks.forEach((rank, index) => {
    if (rank == null) {
      pen = false
      return
    }
    const x = Math.round(index * step * 10) / 10
    const y = Math.round(((Math.min(rank, max) - 1) / (max - 1)) * height * 10) / 10
    path += `${pen ? 'L' : 'M'}${x} ${y} `
    pen = true
  })
  return path.trim()
}

function Sparkline({ ranks, depth, label }: { ranks: Array<number | null>; depth: number; label: string }) {
  const known = ranks.filter((rank): rank is number => rank != null)
  // The scale follows the ranks seen, so a keyword moving between 40 and 60 still shows its movement.
  const scale = known.length > 0 ? Math.min(depth, Math.max(...known) + 5) : depth
  return (
    <svg width="96" height="24" viewBox="-2 -2 100 28" role="img" aria-label={label} className="block">
      <path d={rankPath(ranks, 96, 24, scale)} fill="none" stroke="currentColor" strokeWidth="1.5" className="text-accent" />
    </svg>
  )
}

function Score({ value, t }: { value: number | null; t: AsoDictionary }) {
  return value == null ? <span className="text-fg3">{t.research.unknown}</span> : <span className="tabular-nums">{value}</span>
}

function RankCell({ rank, checked, depth, t }: { rank: number | null; checked: boolean; depth: number; t: AsoDictionary }) {
  if (!checked) {
    return <span className="text-xs text-fg3">{t.tracked.waiting}</span>
  }
  return rank == null ? (
    <span className="text-xs text-fg3">{interpolate(t.tracked.notRanked, { depth })}</span>
  ) : (
    <b className="tabular-nums">#{rank}</b>
  )
}

function TrackedCard({ context }: { context: WorkspaceContext }) {
  const t = useAsoText()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const { app, organizationId, store, can } = context
  const tracked = useTrackedKeywords(organizationId, app.Id, store)
  const editor = can(OrgPermission.EditContent)

  const untrack = useMutation({
    mutationFn: (id: number) => asoApi.untrack(organizationId, [id]),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.tracked.untracked, 'success')
      void queryClient.invalidateQueries({ queryKey: asoKeys.all })
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  async function onUntrack(row: AsoTrackedKeywordDto) {
    const ok = await confirm({
      title: t.tracked.untrack,
      message: interpolate(t.tracked.untrackConfirm, { keyword: row.Keyword }),
      confirmLabel: t.tracked.untrack,
      danger: true,
    })
    if (ok) {
      untrack.mutate(row.Id)
    }
  }

  const rows = tracked.data ?? []

  return (
    <section className="card" aria-label={t.tracked.title}>
      <div className="card-h">
        <h2 className="h3">{t.tracked.title}</h2>
      </div>
      {tracked.isLoading ? (
        <div className="card-b">
          <CardSkeleton count={1} />
        </div>
      ) : tracked.isError ? (
        <ScreenState error={tracked.error} onRetry={() => void tracked.refetch()} />
      ) : rows.length === 0 ? (
        <div className="card-b">
          <EmptyState icon={<Tags aria-hidden />} title={t.tracked.empty} />
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="tbl">
            <thead>
              <tr>
                <th>{t.tracked.keyword}</th>
                <th>{t.tracked.rank}</th>
                <th>{t.tracked.change1}</th>
                <th>{t.tracked.change7}</th>
                <th>{t.tracked.change30}</th>
                <th>{t.tracked.trend}</th>
                <th>{t.tracked.popularity}</th>
                <th>{t.tracked.difficulty}</th>
                <th aria-hidden />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.Id}>
                  <td>
                    <Link className="lnk font-medium" to={`/apps/${app.Id}/keywords/${row.Id}${context.storeQuery}`}>
                      {row.Keyword}
                    </Link>
                    <span className="block text-xs text-fg3">
                      <span className="mono">{row.Country.toUpperCase()}</span>
                      {row.LastCheckedDate ? ` · ${interpolate(t.tracked.checked, { date: format.dateTime(row.LastCheckedDate) })}` : ''}
                    </span>
                  </td>
                  <td>
                    <RankCell rank={row.CurrentRank} checked={row.LastCheckedDate != null} depth={row.Depth} t={t} />
                  </td>
                  <td>
                    <Change value={row.Change1} t={t} />
                  </td>
                  <td>
                    <Change value={row.Change7} t={t} />
                  </td>
                  <td>
                    <Change value={row.Change30} t={t} />
                  </td>
                  <td>
                    <Sparkline ranks={row.Sparkline} depth={row.Depth} label={`${row.Keyword}: ${t.tracked.trendLabel}`} />
                  </td>
                  <td>
                    <Score value={row.Popularity} t={t} />
                  </td>
                  <td>
                    <Score value={row.Difficulty} t={t} />
                  </td>
                  <td className="text-right">
                    {editor ? (
                      <Button size="sm" variant="ghost" onClick={() => void onUntrack(row)} disabled={untrack.isPending}>
                        {t.tracked.untrack}
                      </Button>
                    ) : null}
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

function Competitors({ items, t }: { items: AsoCompetitorDto[]; t: AsoDictionary }) {
  return (
    <span className="flex flex-wrap gap-1">
      {items.slice(0, 3).map((item) => (
        <span
          key={item.Id}
          className={cn('inline-flex items-center gap-1 text-xs', item.IsOwnApp && 'font-semibold')}
          title={item.Name ?? item.Id}
        >
          {item.IconUrl ? <img src={item.IconUrl} alt="" width={16} height={16} className="rounded" loading="lazy" /> : null}
          <span className="max-w-[9rem] truncate">{item.IsOwnApp ? t.detail.you : (item.Name ?? item.Id)}</span>
        </span>
      ))}
    </span>
  )
}

function ResearchCard({ context, country, onCountry }: { context: WorkspaceContext; country: string; onCountry: (value: string) => void }) {
  const t = useAsoText()
  const base = useT()
  const format = useFormat()
  const queryClient = useQueryClient()
  const { app, organizationId, store, can } = context
  const editor = can(OrgPermission.EditContent)
  const settings = useAiSettings(organizationId)
  const [text, setText] = useState('')
  const [seeds, setSeeds] = useState<AsoKeywordSeedDto[]>([])
  const [seedWarning, setSeedWarning] = useState<string | null>(null)
  const [results, setResults] = useState<AsoKeywordResearchDto[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [withCompetitors, setWithCompetitors] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const keywords = parseKeywords(text)
  const validCountry = /^[A-Za-z]{2}$/.test(country)

  function addTerms(terms: string[]) {
    const merged = parseKeywords([...keywords, ...terms].join('\n'))
    setText(merged.join('\n'))
  }

  const seedQuery = useMutation({
    mutationFn: () => asoApi.seeds(organizationId, app.Id, store, country),
    meta: { silentError: true },
    onSuccess: (data) => {
      setError(null)
      setSeeds(data.Seeds)
      setSeedWarning(data.Warning)
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const ideas = useMutation({
    mutationFn: () =>
      asoApi.ideas(
        organizationId,
        app.Id,
        store,
        country,
        parseKeywords([...keywords, ...seeds.map((seed) => seed.Term)].join('\n')).slice(0, 60),
      ),
    meta: { silentError: true },
    onSuccess: (data) => {
      setError(null)
      setSeeds((current) => [...current, ...data.Seeds.filter((seed) => !current.some((item) => item.Term === seed.Term))])
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const measure = useMutation({
    mutationFn: () => asoApi.research(organizationId, app.Id, store, country, keywords.slice(0, MAX_RESEARCH)),
    meta: { silentError: true },
    onSuccess: (data) => {
      setError(null)
      setResults(data)
      setSelected(new Set(data.filter((item) => !item.Tracked).map((item) => item.Keyword)))
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const track = useMutation({
    mutationFn: () =>
      asoApi.track(organizationId, app.Id, store, { Country: country, Keywords: [...selected], TrackCompetitors: withCompetitors }),
    meta: { silentError: true },
    onSuccess: () => {
      setError(null)
      setResults((current) => current.map((item) => (selected.has(item.Keyword) ? { ...item, Tracked: true } : item)))
      setSelected(new Set())
      showToast(t.research.trackedToast, 'success')
      void queryClient.invalidateQueries({ queryKey: asoKeys.all })
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  function toggle(keyword: string, on: boolean) {
    setSelected((current) => {
      const next = new Set(current)
      if (on) {
        next.add(keyword)
      } else {
        next.delete(keyword)
      }
      return next
    })
  }

  const measured = results.find((item) => item.ResultsDate ?? item.PopularityDate)
  const freshness = measured ? (measured.ResultsDate ?? measured.PopularityDate) : null
  const hasKey = settings.data?.HasApiKey ?? false

  return (
    <section className="card" aria-label={t.research.title}>
      <div className="card-h">
        <h2 className="h3">{t.research.title}</h2>
      </div>
      <div className="card-b grid gap-3">
        <p className="m-0 text-[13px] text-fg2">{t.research.hint}</p>
        <div className="grid gap-3 md:grid-cols-[120px_minmax(0,1fr)]">
          <Field label={t.country} hint={t.countryHint} error={validCountry ? null : t.countryHint}>
            <Input value={country} maxLength={2} mono onChange={(event) => onCountry(event.target.value.toUpperCase())} />
          </Field>
          <Field label={t.research.keywords}>
            <Textarea rows={4} value={text} placeholder={t.research.placeholder} onChange={(event) => setText(event.target.value)} />
          </Field>
        </div>
        {keywords.length > MAX_RESEARCH ? <p className="m-0 text-xs text-warningText">{t.research.tooMany}</p> : null}
        <div className="flex flex-wrap gap-2">
          <Button
            variant="primary"
            loading={measure.isPending}
            disabled={keywords.length === 0 || !validCountry}
            onClick={() => measure.mutate()}
          >
            <Search aria-hidden />
            {t.research.measure}
          </Button>
          <Button loading={seedQuery.isPending} disabled={!validCountry} onClick={() => seedQuery.mutate()}>
            {t.research.fromListing}
          </Button>
          {editor ? (
            <Button
              loading={ideas.isPending}
              disabled={!hasKey || !validCountry}
              title={!hasKey ? t.research.aiNoKey : undefined}
              onClick={() => ideas.mutate()}
            >
              <Sparkles aria-hidden />
              {t.research.withAi}
            </Button>
          ) : null}
        </div>
        {measure.isPending ? <p className="m-0 text-xs text-fg3">{t.research.measuring}</p> : null}
        {error ? <Callout tone="danger">{error}</Callout> : null}
        {seedWarning ? <Callout tone="warning">{seedWarning}</Callout> : null}
        {seeds.length > 0 ? (
          <div className="grid gap-2" aria-label={t.research.seedsTitle} role="group">
            <div className="flex flex-wrap items-center gap-2">
              <b className="text-[13px]">{t.research.seedsTitle}</b>
              <span className="text-xs text-fg3">{t.research.seedsHint}</span>
              <Button size="sm" variant="ghost" onClick={() => addTerms(seeds.map((seed) => seed.Term))}>
                {t.research.addAll}
              </Button>
            </div>
            <div className="flex flex-wrap gap-1.5">
              {seeds.map((seed) => (
                <button
                  key={seed.Term}
                  type="button"
                  className={cn('chip', keywords.some((keyword) => keyword.toLocaleLowerCase() === seed.Term) && 'on')}
                  title={seed.Cluster ? `${t.research.source[seed.Source]} · ${seed.Cluster}` : t.research.source[seed.Source]}
                  onClick={() => addTerms([seed.Term])}
                >
                  {seed.Term}
                  {seed.Tracked ? <span className="ml-1 text-xs text-fg3">({t.research.alreadyTracked})</span> : null}
                </button>
              ))}
            </div>
          </div>
        ) : null}
      </div>
      {results.length === 0 ? (
        <div className="card-b pt-0 text-[13px] text-fg3">{t.research.empty}</div>
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className="tbl">
              <thead>
                <tr>
                  <th aria-hidden />
                  <th>{t.tracked.keyword}</th>
                  <th>{t.tracked.popularity}</th>
                  <th>{t.tracked.difficulty}</th>
                  <th>{t.research.opportunity}</th>
                  <th>{t.research.competing}</th>
                  <th>{t.research.currentRank}</th>
                  <th>{t.research.topCompetitors}</th>
                </tr>
              </thead>
              <tbody>
                {results.map((item) => (
                  <tr key={item.Keyword}>
                    <td>
                      {item.Tracked ? (
                        <Badge small tone="success">
                          {t.research.alreadyTracked}
                        </Badge>
                      ) : (
                        <Checkbox checked={selected.has(item.Keyword)} disabled={!editor} onChange={(on) => toggle(item.Keyword, on)}>
                          <span className="sr-only">{interpolate(t.research.select, { keyword: item.Keyword })}</span>
                        </Checkbox>
                      )}
                    </td>
                    <td>
                      <b>{item.Keyword}</b>
                      {item.RelatedTerms.length > 0 ? (
                        <span className="block max-w-[16rem] truncate text-xs text-fg3">
                          {t.research.related}: {item.RelatedTerms.slice(0, 4).join(', ')}
                        </span>
                      ) : null}
                    </td>
                    <td>
                      <Score value={item.Popularity} t={t} />
                    </td>
                    <td>
                      <Score value={item.Difficulty} t={t} />
                    </td>
                    <td>
                      <Score value={item.Opportunity} t={t} />
                    </td>
                    <td>
                      <Score value={item.CompetingApps} t={t} />
                    </td>
                    <td>
                      <RankCell rank={item.CurrentRank} checked={item.RankChecked} depth={item.Depth} t={t} />
                    </td>
                    <td>
                      <Competitors items={item.TopCompetitors} t={t} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="card-b flex flex-wrap items-center gap-3">
            {editor ? (
              <>
                <Checkbox checked={withCompetitors} onChange={setWithCompetitors}>
                  {t.research.trackCompetitors}
                </Checkbox>
                <Button variant="primary" loading={track.isPending} disabled={selected.size === 0} onClick={() => track.mutate()}>
                  {t.research.track}
                </Button>
              </>
            ) : null}
            {freshness ? (
              <span className="text-xs text-fg3">{interpolate(t.notes.freshness, { date: format.dateTime(freshness) })}</span>
            ) : null}
          </div>
        </>
      )}
    </section>
  )
}

function CoverageLocale({ item, t }: { item: AsoKeywordCoverageDto; t: AsoDictionary }) {
  return (
    <section className="grid gap-2 border-t border-border py-3 first:border-t-0" aria-label={item.Locale}>
      <div className="flex flex-wrap items-center gap-2">
        <b className="mono">{item.Locale}</b>
        <Badge small tone={item.Remaining < 0 ? 'danger' : item.Remaining < 10 ? 'warning' : 'neutral'}>
          {item.Remaining < 0
            ? interpolate(t.coverage.over, { count: -item.Remaining })
            : interpolate(t.coverage.remaining, { remaining: item.Remaining })}
        </Badge>
        {item.CharactersNeeded > 0 ? (
          <span className="text-xs text-fg3">{interpolate(t.coverage.needed, { count: item.CharactersNeeded })}</span>
        ) : null}
      </div>
      {item.Keywords ? <code className="mono break-all text-xs text-fg2">{item.Keywords}</code> : null}
      <ul className="m-0 grid list-none gap-1 p-0 text-[13px]">
        {item.Items.map((entry) => (
          <li key={entry.Keyword} className="flex flex-wrap items-center gap-2">
            <span>{entry.Keyword}</span>
            {entry.Covered ? (
              <Badge small tone="success">
                {t.coverage.covered}
              </Badge>
            ) : (
              <Badge small tone="warning">
                {interpolate(t.coverage.missing, { words: entry.MissingWords.join(', ') })}
              </Badge>
            )}
            {entry.Fields.length > 0 ? (
              <span className="text-xs text-fg3">{entry.Fields.map((field) => t.coverage.fields[field]).join(', ')}</span>
            ) : null}
          </li>
        ))}
      </ul>
      {item.RedundantWords.length > 0 ? (
        <p className="m-0 text-xs text-fg3" title={t.coverage.redundantHint}>
          {interpolate(t.coverage.redundant, { words: item.RedundantWords.join(', ') })}
        </p>
      ) : null}
    </section>
  )
}

function CoverageCard({ context }: { context: WorkspaceContext }) {
  const t = useAsoText()
  const { app, organizationId } = context
  const coverage = useKeywordCoverage(organizationId, app.Id, true)
  const locales = coverage.data ?? []

  return (
    <section className="card" aria-label={t.coverage.title}>
      <div className="card-h">
        <h2 className="h3">{t.coverage.title}</h2>
      </div>
      <div className="card-b grid gap-1">
        <p className="m-0 text-xs text-fg3">{t.coverage.hint}</p>
        {coverage.isLoading ? (
          <CardSkeleton count={1} />
        ) : coverage.isError ? (
          <ScreenState error={coverage.error} onRetry={() => void coverage.refetch()} />
        ) : locales.every((locale) => locale.Items.length === 0) ? (
          <p className="m-0 text-[13px] text-fg2">{t.coverage.empty}</p>
        ) : (
          locales.map((item) => <CoverageLocale key={item.Locale} item={item} t={t} />)
        )}
      </div>
    </section>
  )
}

function Keywords({ context }: { context: WorkspaceContext }) {
  const t = useAsoText()
  const { lang } = useI18n()
  const [country, setCountry] = useState(defaultCountry(lang))

  return (
    <>
      <PageHeader title={t.title} subtitle={t.subtitle} />
      <div className="mb-3 grid gap-2">
        <Callout tone="info">{t.notes.estimated}</Callout>
        {context.store === StoreKind.Google ? <Callout tone="info">{t.notes.googlePlay}</Callout> : null}
      </div>
      <div className="grid gap-4">
        <TrackedCard context={context} />
        <ResearchCard context={context} country={country} onCountry={setCountry} />
        {context.store === StoreKind.Apple ? <CoverageCard context={context} /> : null}
      </div>
    </>
  )
}

export function AsoKeywordsPage() {
  return (
    <AppWorkspace section="keywords" stores={STORES}>
      {(context) => <Keywords key={context.store} context={context} />}
    </AppWorkspace>
  )
}

// ---- Detail --------------------------------------------------------------------------------------------------------

const SERIES_COLORS = ['text-fg3', 'text-warningText', 'text-info', 'text-danger', 'text-success']

function RankChart({
  points,
  competitors,
  depth,
  t,
  markers = [],
  onMarker,
}: {
  points: AsoRankPointDto[]
  competitors: Array<{ name: string; points: AsoRankPointDto[] }>
  depth: number
  t: AsoDictionary
  // Store page changes and releases of the record (vertical markers that open the change's impact).
  markers?: ChartMarker[]
  onMarker?: (id: string) => void
}) {
  const format = useFormat()
  const width = 640
  const height = 200
  const known = [...points, ...competitors.flatMap((series) => series.points)]
    .filter((point) => point.Rank != null)
    .map((point) => point.Rank as number)
  const scale = known.length > 0 ? Math.min(depth, Math.max(...known) + 5) : depth
  // A checked day without a rank sits on the bottom line; an unchecked day leaves a gap.
  const line = (series: AsoRankPointDto[]) =>
    rankPath(
      series.map((point) => (point.Checked ? (point.Rank ?? scale) : null)),
      width,
      height,
      scale,
    )

  return (
    <figure className="m-0 grid gap-2">
      <svg viewBox={`-8 -8 ${width + 16} ${height + 16}`} role="img" aria-label={t.detail.chart} className="h-auto w-full">
        <line x1={0} y1={height} x2={width} y2={height} stroke="currentColor" className="text-border" strokeDasharray="4 4" />
        {competitors.map((series, index) => (
          <path
            key={series.name}
            d={line(series.points)}
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
            className={SERIES_COLORS[index % SERIES_COLORS.length]}
          />
        ))}
        <path d={line(points)} fill="none" stroke="currentColor" strokeWidth="2.5" className="text-accent" />
        <ChartMarkers
          markers={markers}
          dates={points.map((point) => point.Date)}
          x={(index) => (points.length > 1 ? (index * width) / (points.length - 1) : width / 2)}
          top={0}
          bottom={height}
          onOpen={onMarker}
        />
      </svg>
      <figcaption className="flex flex-wrap justify-between gap-2 text-xs text-fg3">
        <span>{points.length > 0 ? format.date(points[0].Date) : ''}</span>
        <span>#1 ... #{scale}</span>
        <span>{points.length > 0 ? format.date(points[points.length - 1].Date) : ''}</span>
      </figcaption>
      <ul className="m-0 flex list-none flex-wrap gap-3 p-0 text-xs">
        <li className="inline-flex items-center gap-1 text-accent">
          <span aria-hidden>━</span> {t.detail.you}
        </li>
        {competitors.map((series, index) => (
          <li key={series.name} className={cn('inline-flex items-center gap-1', SERIES_COLORS[index % SERIES_COLORS.length])}>
            <span aria-hidden>━</span> {series.name}
          </li>
        ))}
      </ul>
    </figure>
  )
}

function KeywordDetail({ context }: { context: WorkspaceContext }) {
  const t = useAsoText()
  const format = useFormat()
  const { keywordId = '' } = useParams()
  const [days, setDays] = useState(30)
  const ranks = useKeywordRanks(context.organizationId, Number(keywordId), days)
  const data = ranks.data
  const competitors = useMemo(
    () => (data?.Competitors ?? []).map((series) => ({ name: series.Name ?? series.Id, points: series.Points })),
    [data?.Competitors],
  )
  const impactText = useImpactText()
  const navigate = useNavigate()
  const events = useImpactEvents(
    context.organizationId,
    context.app.Id,
    { StoreAppId: data?.Keyword.StoreAppId, From: data?.From, To: data?.To },
    data != null,
  )
  const markers: ChartMarker[] = (events.data ?? []).map((event) => ({
    id: event.Id,
    date: event.Date,
    label: `${format.date(event.Date)}: ${eventTitle(event, impactText)}`,
    pending: event.DateSource === ImpactDateSource.AwaitingRelease,
  }))

  return (
    <>
      <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to={`/apps/${context.app.Id}/keywords${context.storeQuery}`}>
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
        {t.detail.back}
      </Link>
      {ranks.isLoading ? (
        <CardSkeleton />
      ) : ranks.isError || !data ? (
        <ScreenState error={ranks.error} onRetry={() => void ranks.refetch()} />
      ) : (
        <div className="grid gap-4">
          <PageHeader
            title={interpolate(t.detail.title, { keyword: data.Keyword.Keyword, country: data.Keyword.Country.toUpperCase() })}
            actions={
              <div className="toolbar" role="group" aria-label={t.detail.days}>
                {DAY_OPTIONS.map((option) => (
                  <button
                    key={option}
                    type="button"
                    className={cn('chip', option === days && 'on')}
                    aria-pressed={option === days}
                    onClick={() => setDays(option)}
                  >
                    {interpolate(t.detail.daysOption, { count: option })}
                  </button>
                ))}
              </div>
            }
          />
          <section className="card" aria-label={t.detail.chart}>
            <div className="card-h">
              <h2 className="h3">{t.detail.chart}</h2>
              <RankCell rank={data.Keyword.CurrentRank} checked={data.Keyword.LastCheckedDate != null} depth={data.Keyword.Depth} t={t} />
            </div>
            <div className="card-b grid gap-2">
              <span className="text-xs text-fg3">{t.detail.chartHint}</span>
              {data.Points.some((point) => point.Checked) ? (
                <RankChart
                  points={data.Points}
                  competitors={competitors}
                  depth={data.Keyword.Depth}
                  t={t}
                  markers={markers}
                  onMarker={(id) => navigate(impactLink({ AppId: context.app.Id, Id: id }))}
                />
              ) : (
                <p className="m-0 text-[13px] text-fg2">{t.detail.noData}</p>
              )}
            </div>
          </section>
          <section className="card" aria-label={t.detail.topCompetitors}>
            <div className="card-h">
              <h2 className="h3">{t.detail.topCompetitors}</h2>
              {data.ResultsDate ? (
                <span className="text-xs text-fg3">{interpolate(t.notes.freshness, { date: format.dateTime(data.ResultsDate) })}</span>
              ) : null}
            </div>
            {data.TopCompetitors.length === 0 ? (
              <div className="card-b text-[13px] text-fg2">{t.detail.noData}</div>
            ) : (
              <ol className="card-b m-0 grid list-none gap-2 p-0 px-4">
                {data.TopCompetitors.map((item) => (
                  <li key={item.Id} className={cn('flex items-center gap-2 text-[13px]', item.IsOwnApp && 'font-semibold')}>
                    <span className="w-8 text-right tabular-nums text-fg3">#{item.Rank}</span>
                    {item.IconUrl ? <img src={item.IconUrl} alt="" width={24} height={24} className="rounded" loading="lazy" /> : null}
                    <span className="min-w-0 flex-1 truncate">
                      {item.IsOwnApp ? `${item.Name ?? item.Id} (${t.detail.you})` : (item.Name ?? item.Id)}
                    </span>
                    {item.RatingCount != null ? (
                      <span className="text-xs text-fg3">
                        {interpolate(t.detail.rating, {
                          rating: format.number(item.Rating ?? 0, 1),
                          count: format.number(item.RatingCount),
                        })}
                      </span>
                    ) : null}
                  </li>
                ))}
              </ol>
            )}
          </section>
        </div>
      )}
    </>
  )
}

export function AsoKeywordDetailPage() {
  return (
    <AppWorkspace section="keywords" stores={STORES}>
      {(context) => <KeywordDetail key={context.store} context={context} />}
    </AppWorkspace>
  )
}
