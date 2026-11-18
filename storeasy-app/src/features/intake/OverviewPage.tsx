import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CloudUpload } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { CardSkeleton, EmptyState } from '@/components/ui/primitives'
import { connectionsApi } from '@/lib/api/endpoints'
import { OrgPermission } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { intakeApi, intakeKeys } from './api'
import { useIntakeText } from './i18n'
import { IntakeShell, useLivePoll } from './IntakeShell'
import { isFinal, ruleText, sourceLabel, sourceText, statusView } from './labels'
import { ArtifactPullSourceKind, ArtifactSessionStatus, OidcProvider, type ArtifactAppSummaryDto, type ArtifactCountsDto } from './types'

// Design 13.1-13.2: counters from artifacts/counts (today by source, the last 7 days by status, the held ones of the API's
// default 30 days), the last package of every app with its delivery rules, and the ways in with how many rules or sources
// each has. The first open leads to the GitHub Action setup.

const DAY = 24 * 60 * 60 * 1000

function statusCount(counts: ArtifactCountsDto | undefined, status: ArtifactSessionStatus): number {
  return counts?.ByStatus?.find((item) => item.Status === status)?.Count ?? 0
}

/** The period starts, fixed when the page opens so the query keys stay stable. */
function periodStarts(): { today: string; week: string } {
  const startOfDay = new Date()
  startOfDay.setHours(0, 0, 0, 0)
  return { today: startOfDay.toISOString(), week: new Date(Date.now() - 7 * DAY).toISOString() }
}

export function IntakeOverviewPage() {
  const t = useIntakeText()
  const format = useFormat()
  const navigate = useNavigate()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const manage = can(OrgPermission.ManageConnections)
  const poll = useLivePoll<ArtifactAppSummaryDto[]>((data) =>
    (data ?? []).some((row) => row.LastSession && !isFinal(row.LastSession.Status)),
  )
  const summary = useQuery({
    queryKey: intakeKeys.summary(organization.Id),
    queryFn: () => intakeApi.summary(organization.Id),
    refetchInterval: poll,
  })
  const [starts] = useState(periodStarts)
  const today = useQuery({
    queryKey: intakeKeys.counts(organization.Id, starts.today),
    queryFn: () => intakeApi.counts(organization.Id, { From: starts.today }),
  })
  const week = useQuery({
    queryKey: intakeKeys.counts(organization.Id, starts.week),
    queryFn: () => intakeApi.counts(organization.Id, { From: starts.week }),
  })
  const month = useQuery({
    queryKey: intakeKeys.counts(organization.Id, 'default'),
    queryFn: () => intakeApi.counts(organization.Id),
  })
  const publishers = useQuery({ queryKey: intakeKeys.publishers(organization.Id), queryFn: () => intakeApi.publishers(organization.Id) })
  const sources = useQuery({ queryKey: intakeKeys.sources(organization.Id), queryFn: () => intakeApi.sources(organization.Id) })
  const connections = useQuery({
    queryKey: queryKeys.connections.list(organization.Id),
    queryFn: () => connectionsApi.list(organization.Id),
    enabled: manage,
  })

  const counters = {
    today: today.data?.Total ?? 0,
    todaySources: (today.data?.BySource ?? [])
      .filter((item) => item.Count > 0)
      .map((item) => interpolate(t.overview.bySource, { source: sourceLabel(item.Source, t), count: item.Count }))
      .join(' · '),
    delivered: statusCount(week.data, ArtifactSessionStatus.Delivered),
    rejected: statusCount(week.data, ArtifactSessionStatus.Rejected),
    held: statusCount(month.data, ArtifactSessionStatus.Held),
  }

  const countOf = (provider: OidcProvider) => (publishers.data ?? []).filter((rule) => rule.Provider === provider).length
  const sourcesOf = (kind: ArtifactPullSourceKind) => (sources.data ?? []).filter((source) => source.Kind === kind).length
  const ways: Array<{ key: string; title: string; body: string; meta?: string; to: string; recommended?: boolean }> = [
    {
      key: 'github',
      title: 'GitHub Action',
      body: t.overview.github,
      meta: interpolate(t.overview.rulesCount, { count: countOf(OidcProvider.GitHub) }),
      to: '/intake/setup?path=github',
      recommended: true,
    },
    {
      key: 'azure',
      title: 'Azure DevOps',
      body: t.overview.azure,
      meta: interpolate(t.overview.rulesCount, { count: countOf(OidcProvider.AzureDevOps) }),
      to: '/intake/setup?path=azure',
    },
    { key: 'cli', title: t.source.cli, body: t.overview.cli, to: '/intake/setup?path=cli' },
    { key: 'rest', title: t.source.rest, body: t.overview.rest, to: '/intake/setup?path=rest' },
    {
      key: 'eas',
      title: t.source.eas,
      body: t.overview.eas,
      meta: interpolate(t.overview.sourcesCount, { count: sourcesOf(ArtifactPullSourceKind.Eas) }),
      to: '/intake/sources',
    },
    {
      key: 'webhook',
      title: t.source.webhook,
      body: t.overview.webhook,
      meta: interpolate(t.overview.sourcesCount, { count: sourcesOf(ArtifactPullSourceKind.Webhook) }),
      to: '/intake/sources',
    },
  ]

  const columns: Column<ArtifactAppSummaryDto>[] = [
    { key: 'app', header: t.overview.app, role: 'main', cell: (row) => <b>{row.AppName ?? `#${row.AppId}`}</b> },
    {
      key: 'last',
      header: t.overview.lastPackage,
      cell: (row) =>
        row.LastSession ? (
          <Link className="lnk mono break-all" to={`/intake/sessions/${row.LastSession.Id}`}>
            {row.LastSession.FileName}
          </Link>
        ) : (
          <span className="text-fg3">{t.overview.noPackage}</span>
        ),
    },
    { key: 'source', header: t.overview.source, cell: (row) => (row.LastSession ? sourceText(row.LastSession, t) : '-') },
    {
      key: 'status',
      header: t.overview.status,
      cell: (row) => {
        if (!row.LastSession) {
          return '-'
        }
        const view = statusView(row.LastSession.Status, t)
        return (
          <Badge small dot tone={view.tone}>
            {view.label}
          </Badge>
        )
      },
    },
    {
      key: 'rule',
      header: t.overview.rule,
      role: 'hidePhone',
      cell: (row) =>
        (row.Rules ?? []).length > 0 ? (
          <span className="grid gap-0.5">
            {(row.Rules ?? []).map((rule) => (
              <span key={rule.Platform}>{ruleText(rule, t)}</span>
            ))}
          </span>
        ) : (
          <span className="text-fg3">{t.overview.noRule}</span>
        ),
    },
  ]

  const rows = summary.data ?? []
  const empty = summary.isSuccess && rows.every((row) => !row.LastSession)

  return (
    <IntakeShell>
      {summary.isLoading ? (
        <CardSkeleton />
      ) : summary.isError ? (
        <ScreenState error={summary.error} onRetry={() => void summary.refetch()} />
      ) : (
        <>
          {empty ? (
            <EmptyState
              icon={<CloudUpload aria-hidden />}
              title={t.overview.emptyTitle}
              description={t.overview.emptyBody}
              action={
                manage ? (
                  <Link className="btn pri" to="/intake/setup?path=github">
                    {t.overview.emptyAction}
                  </Link>
                ) : (
                  <span className="text-[13px] text-fg3">{t.overview.askAdmin}</span>
                )
              }
            >
              {manage && connections.data?.length === 0 ? (
                <Callout tone="warning">
                  {t.overview.noConnection}{' '}
                  <Link className="lnk" to="/connections">
                    {t.overview.connect}
                  </Link>
                </Callout>
              ) : null}
            </EmptyState>
          ) : (
            <>
              <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
                {[
                  { label: t.overview.today, value: counters.today, detail: counters.todaySources },
                  { label: t.overview.delivered, value: counters.delivered },
                  { label: t.overview.rejected, value: counters.rejected },
                  { label: t.overview.held, value: counters.held },
                ].map((item) => (
                  <div key={item.label} className="card grid gap-1 p-4">
                    <span className="text-xs text-fg3">{item.label}</span>
                    <b className="text-[22px] tabular-nums">{format.number(item.value)}</b>
                    {item.detail ? <span className="text-xs text-fg3">{item.detail}</span> : null}
                  </div>
                ))}
              </div>
              <section className="grid gap-2">
                <span className="flex items-center justify-between gap-2">
                  <h2 className="text-[15px] font-semibold">{t.overview.lastTitle}</h2>
                  <Link className="lnk text-[13px]" to="/intake/sessions">
                    {t.overview.allSessions}
                  </Link>
                </span>
                <DataTable
                  columns={columns}
                  rows={rows}
                  rowKey={(row) => row.AppId}
                  caption={t.overview.lastTitle}
                  onRowClick={(row) => (row.LastSession ? navigate(`/intake/sessions/${row.LastSession.Id}`) : undefined)}
                />
              </section>
            </>
          )}
          <section className="grid gap-2">
            <span className="flex flex-wrap items-baseline justify-between gap-2">
              <h2 className="text-[15px] font-semibold">{t.overview.waysTitle}</h2>
              <span className="text-xs text-fg3">{t.overview.waysHint}</span>
            </span>
            <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {ways.map((way) => (
                <li key={way.key} className="card grid content-start gap-1.5 p-4">
                  <span className="flex items-center gap-2">
                    <b className="text-[14px]">{way.title}</b>
                    {way.recommended ? (
                      <Badge small tone="accent">
                        {t.overview.recommended}
                      </Badge>
                    ) : null}
                  </span>
                  <span className="text-[13px] text-fg2">{way.body}</span>
                  <span className="flex items-center justify-between gap-2 text-xs text-fg3">
                    <span>{way.meta}</span>
                    <Link className="lnk" to={way.to} aria-label={`${t.overview.open} · ${way.title}`}>
                      {t.overview.open}
                    </Link>
                  </span>
                </li>
              ))}
            </ul>
          </section>
        </>
      )}
    </IntakeShell>
  )
}
