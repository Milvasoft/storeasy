import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { BookOpen, SearchX } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, Chip, EmptyState, Input, PageHeader, SpinnerCenter, TableSkeleton } from '@/components/ui/primitives'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { rulesApi, type RuleListFilter } from '@/features/common/api'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { applicabilityLabel, ruleStatusView } from '@/features/common/labels'
import { RuleApplicability, RuleStatus, type RuleDetailDto, type RuleListDto } from '@/features/common/types'
import { ALL_STORES } from '@/lib/stores/channels'

// Design 17.1-17.2: the loaded rules catalog with its version, filters by store, category and status, a search, and a
// rule's detail in a drawer (/rules/:ruleId) with its sources, conflicts and this organization's matching apps.

const SEARCH_DELAY_MS = 400
const CATEGORIES = [
  'screenshots',
  'app-previews',
  'graphics',
  'trailers',
  'listing-limits',
  'review',
  'age-rating',
  'privacy',
  'policy',
  'compliance',
  'sdk',
  'target-api',
  'packages',
  'testflight',
  'api-access',
]

function RuleDetail({ ruleId, onClose }: { ruleId: string; onClose: () => void }) {
  const t = useP2()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const rule = useQuery({ queryKey: contentKeys.rule(organization.Id, ruleId), queryFn: () => rulesApi.detail(organization.Id, ruleId) })
  const data = rule.data
  return (
    <Modal
      open
      variant="drawer"
      size="lg"
      onClose={onClose}
      title={data?.Title ?? ruleId}
      description={<span className="mono">{ruleId}</span>}
      footer={
        <Button variant="ghost" onClick={onClose}>
          {t.rules.close}
        </Button>
      }
    >
      {rule.isLoading ? <SpinnerCenter label={t.rules.title} /> : null}
      {rule.isError ? <ScreenState error={rule.error} onRetry={() => void rule.refetch()} /> : null}
      {data ? <RuleBody rule={data} format={format} /> : null}
    </Modal>
  )
}

function RuleBody({ rule, format }: { rule: RuleDetailDto; format: ReturnType<typeof useFormat> }) {
  const t = useP2()
  const status = ruleStatusView(rule.Status, t)
  const conditions = rule.AppliesWhen
    ? Object.entries(rule.AppliesWhen)
        .filter(([, value]) => value && (Array.isArray(value) ? value.length > 0 : true))
        .map(
          ([key, value]) =>
            `${key}: ${Array.isArray(value) ? value.join(', ') : typeof value === 'object' ? JSON.stringify(value) : String(value)}`,
        )
    : []
  const sizes = (rule.Params?.Sizes ?? []).map((size) => `${size.Width}x${size.Height}${size.Note ? ` (${size.Note})` : ''}`)
  return (
    <div className="grid gap-4 text-[13.5px]">
      <div className="flex flex-wrap items-center gap-2">
        <StoreTag store={rule.Store} small />
        {rule.Category ? <Badge small>{rule.Category}</Badge> : null}
        <Badge tone={status.tone} dot>
          {status.label}
        </Badge>
        {!rule.Verified ? <Badge tone="warning">{t.rules.unverified}</Badge> : null}
        {rule.EffectiveDate ? <span>{interpolate(t.rules.effective, { date: format.date(rule.EffectiveDate) })}</span> : null}
        {rule.Deadline ? <span>{interpolate(t.rules.deadline, { date: format.date(rule.Deadline) })}</span> : null}
      </div>
      {rule.Description ? (
        <section>
          <h3 className="h3 mb-1">{t.rules.description}</h3>
          <p className="m-0 text-fg2">{rule.Description}</p>
        </section>
      ) : null}
      {rule.Condition || conditions.length > 0 ? (
        <section>
          <h3 className="h3 mb-1">{t.rules.condition}</h3>
          {rule.Condition ? <p className="m-0 text-fg2">{rule.Condition}</p> : null}
          {conditions.length > 0 ? (
            <ul className="mono m-0 mt-1 grid list-none gap-0.5 p-0 text-xs text-fg2" aria-label={t.rules.appliesWhen}>
              {conditions.map((line) => (
                <li key={line}>{line}</li>
              ))}
            </ul>
          ) : null}
        </section>
      ) : null}
      {sizes.length > 0 ? (
        <section>
          <h3 className="h3 mb-1">{t.rules.values}</h3>
          <p className="mono m-0 text-fg2">{sizes.join(', ')}</p>
        </section>
      ) : null}
      {(rule.FallbackChain ?? []).length > 0 ? (
        <p className="m-0">
          <b>{t.rules.fallback}:</b> <span className="mono">{(rule.FallbackChain ?? []).join(' > ')}</span>
        </p>
      ) : null}
      {(rule.SatisfiedBy ?? []).length > 0 ? (
        <p className="m-0">
          <b>{t.rules.satisfiedBy}:</b> <span className="mono">{(rule.SatisfiedBy ?? []).join(', ')}</span>
        </p>
      ) : null}
      {(rule.Conflicts ?? []).map((conflict, index) => (
        <Callout key={index} tone="warning" title={t.rules.conflict}>
          {conflict.Description}
          {conflict.CatalogChoice ? (
            <span className="block">{interpolate(t.rules.catalogChoice, { choice: conflict.CatalogChoice })}</span>
          ) : null}
        </Callout>
      ))}
      {(rule.Sources ?? []).length > 0 ? (
        <section>
          <h3 className="h3 mb-1">{t.rules.sources}</h3>
          <ul className="m-0 grid list-none gap-2 p-0">
            {(rule.Sources ?? []).map((source) => (
              <li key={source.Url}>
                <a className="lnk" href={source.Url} target="_blank" rel="noreferrer noopener">
                  {source.Title ?? source.Url}
                </a>
                <span className="ml-2 text-xs text-fg3">{interpolate(t.rules.checked, { date: format.date(source.CheckedAt) })}</span>
                {source.Quote ? (
                  <blockquote className="m-0 mt-1 border-l-2 border-border pl-2 text-xs text-fg2">{source.Quote}</blockquote>
                ) : null}
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      {rule.Notes || rule.DateNote ? (
        <section>
          <h3 className="h3 mb-1">{t.rules.notes}</h3>
          {rule.Notes ? <p className="m-0 text-fg2">{rule.Notes}</p> : null}
          {rule.DateNote ? <p className="m-0 text-fg2">{rule.DateNote}</p> : null}
        </section>
      ) : null}
      <section aria-label={t.rules.affectedApps}>
        <h3 className="h3 mb-1">{t.rules.affectedApps}</h3>
        {(rule.AffectedApps ?? []).length === 0 ? <p className="m-0 text-fg3">{t.rules.noAffected}</p> : null}
        <ul className="m-0 grid list-none gap-2 p-0">
          {(rule.AffectedApps ?? []).map((app) => (
            <li key={app.AppId} className="flex flex-wrap items-center gap-2">
              <Link className="lnk font-semibold" to={`/apps/${app.AppId}/compliance`}>
                {app.Name}
              </Link>
              <Badge small tone={app.Applicability === RuleApplicability.Applies ? 'warning' : 'neutral'}>
                {applicabilityLabel(app.Applicability, t)}
              </Badge>
              {(app.Reasons ?? []).length > 0 ? <span className="text-xs text-fg3">{(app.Reasons ?? []).join(', ')}</span> : null}
              {(app.UnknownKeys ?? []).length > 0 ? (
                <span className="text-xs text-fg3">{interpolate(t.rules.unknownKeys, { keys: (app.UnknownKeys ?? []).join(', ') })}</span>
              ) : null}
            </li>
          ))}
        </ul>
      </section>
      <p className="m-0 text-xs text-fg3">{rule.CatalogVersion}</p>
    </div>
  )
}

export function RulesPage() {
  const t = useP2()
  const base = useT()
  const format = useFormat()
  const navigate = useNavigate()
  const organization = useCurrentOrganization()
  const { ruleId } = useParams()
  const [term, setTerm] = useState('')
  const [filter, setFilter] = useState<RuleListFilter>({})

  // The search goes out 400 ms after the last key (design 6.6).
  useEffect(() => {
    const timer = setTimeout(() => setFilter((current) => ({ ...current, Search: term.trim() || null })), SEARCH_DELAY_MS)
    return () => clearTimeout(timer)
  }, [term])

  const catalog = useQuery({ queryKey: contentKeys.rulesCatalog(organization.Id), queryFn: () => rulesApi.catalog(organization.Id) })
  const rules = useQuery({
    queryKey: contentKeys.rules(organization.Id, filter),
    queryFn: () => rulesApi.list(organization.Id, filter),
    placeholderData: keepPreviousData,
  })
  const filtered = !!(filter.Store || filter.Category || filter.Status || filter.Search)

  const columns: Column<RuleListDto>[] = [
    {
      key: 'rule',
      header: t.rules.rule,
      role: 'main',
      cell: (rule) => (
        <span className="grid gap-0.5">
          <span className="flex items-center gap-2">
            <StoreTag store={rule.Store} small codeOnly />
            <Link className="lnk font-semibold" to={`/rules/${encodeURIComponent(rule.Id)}`} onClick={(event) => event.stopPropagation()}>
              {rule.Title ?? rule.Id}
            </Link>
          </span>
          <span className="mono text-xs text-fg3">{rule.Id}</span>
        </span>
      ),
    },
    { key: 'category', header: t.rules.category, role: 'hidePhone', cell: (rule) => rule.Category ?? '-' },
    {
      key: 'when',
      header: t.rules.when,
      cell: (rule) => {
        const status = ruleStatusView(rule.Status, t)
        const date = rule.DueDate ?? rule.Deadline ?? rule.EffectiveDate
        return (
          <span className="grid gap-0.5">
            <span className="tabular-nums">{date ? format.date(date) : '-'}</span>
            <Badge small tone={status.tone}>
              {status.label}
            </Badge>
          </span>
        )
      },
    },
    {
      key: 'source',
      header: t.rules.source,
      role: 'hidePhone',
      cell: (rule) =>
        rule.SourceUrl ? (
          <span className="grid gap-0.5 text-xs">
            <a className="lnk" href={rule.SourceUrl} target="_blank" rel="noreferrer noopener" onClick={(event) => event.stopPropagation()}>
              {new URL(rule.SourceUrl).hostname}
            </a>
            {rule.CheckedDate ? (
              <span className="text-fg3">{interpolate(t.rules.checked, { date: format.date(rule.CheckedDate) })}</span>
            ) : null}
          </span>
        ) : (
          '-'
        ),
    },
    {
      key: 'affected',
      header: t.rules.affected,
      cell: (rule) => (rule.AffectedAppCount > 0 ? interpolate(t.rules.affectedCount, { count: rule.AffectedAppCount }) : '0'),
    },
  ]

  return (
    <>
      <PageHeader
        title={t.rules.title}
        subtitle={t.rules.subtitle}
        badge={
          catalog.data ? (
            <Badge small>
              {interpolate(t.rules.catalog, {
                version: catalog.data.Version,
                date: format.date(catalog.data.PublishedDate),
                count: catalog.data.RuleCount,
              })}
            </Badge>
          ) : undefined
        }
      />
      <div className="toolbar mb-3">
        <div className="min-w-[200px] flex-1">
          <Input aria-label={t.rules.search} placeholder={t.rules.search} value={term} onChange={(event) => setTerm(event.target.value)} />
        </div>
        <div role="group" aria-label={t.workspace.store} className="flex flex-wrap gap-1.5">
          <Chip selected={!filter.Store} onClick={() => setFilter((current) => ({ ...current, Store: null }))}>
            {base.common.all}
          </Chip>
          {ALL_STORES.map((store) => (
            <Chip key={store} selected={filter.Store === store} onClick={() => setFilter((current) => ({ ...current, Store: store }))}>
              <StoreTag store={store} small />
            </Chip>
          ))}
        </div>
        <div className="w-48">
          <Select
            aria-label={t.rules.category}
            placeholder={t.rules.category}
            value={filter.Category ?? ''}
            options={[{ value: '', label: base.common.all }, ...CATEGORIES.map((value) => ({ value, label: value }))]}
            onChange={(value) => setFilter((current) => ({ ...current, Category: value || null }))}
          />
        </div>
        <div className="w-40">
          <Select
            aria-label={t.rules.status}
            value={filter.Status ?? 0}
            options={[
              { value: 0, label: base.common.all },
              { value: RuleStatus.InForce, label: t.enums.ruleStatus.inForce },
              { value: RuleStatus.Upcoming, label: t.enums.ruleStatus.upcoming },
              { value: RuleStatus.Deprecated, label: t.enums.ruleStatus.deprecated },
            ]}
            onChange={(value) => setFilter((current) => ({ ...current, Status: value ? (value as RuleStatus) : null }))}
          />
        </div>
        {filtered ? (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setTerm('')
              setFilter({})
            }}
          >
            {base.common.clearFilters}
          </Button>
        ) : null}
      </div>
      {rules.isLoading ? (
        <TableSkeleton columns={[t.rules.rule, t.rules.when, t.rules.affected]} label={t.rules.title} />
      ) : rules.isError ? (
        <ScreenState error={rules.error} onRetry={() => void rules.refetch()} />
      ) : (rules.data ?? []).length === 0 ? (
        <EmptyState
          icon={filtered ? <SearchX aria-hidden /> : <BookOpen aria-hidden />}
          title={filter.Search ? interpolate(t.rules.emptyTitle, { term: filter.Search }) : t.rules.emptyFilters}
        />
      ) : (
        <DataTable
          caption={t.rules.title}
          columns={columns}
          rows={rules.data ?? []}
          rowKey={(rule) => rule.Id}
          busy={rules.isFetching}
          onRowClick={(rule) => navigate(`/rules/${encodeURIComponent(rule.Id)}`)}
        />
      )}
      <p className="mt-3 text-xs text-fg3">{t.rules.disclaimer}</p>
      {ruleId ? <RuleDetail ruleId={ruleId} onClose={() => navigate('/rules')} /> : null}
    </>
  )
}
