import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import { Badge, type BadgeTone } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { Button, PageHeader, RadioGroup, Skeleton } from '@/components/ui/primitives'
import { APP_VERSION, SOURCE_URL } from '@/config'
import { rulesApi } from '@/features/common/api'
import { contentKeys } from '@/features/common/keys'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { platformApi, platformKeys } from './api'
import { PlatformGate } from './PlatformCommon'
import { usePlatformText, type PlatformDictionary } from './i18n'
import type { ReadinessReport } from './types'

// Design 17.24: the installation's health on one screen. What the API has today: the anonymous liveness and readiness
// probes (database, Redis) and the rules catalog version. The planned system/status and system/settings endpoints
// (versions, storage, mail, SignalR, job queue, registration mode) do not exist yet; their cards say so.

const REFRESH_MS = 30_000

export function healthView(status: string | undefined, t: PlatformDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case 'Healthy':
      return { label: t.system.healthy, tone: 'success' }
    case 'Degraded':
      return { label: t.system.degraded, tone: 'warning' }
    case 'Unhealthy':
      return { label: t.system.unhealthy, tone: 'danger' }
    default:
      return { label: t.system.unreachable, tone: 'danger' }
  }
}

function checkName(key: string, t: PlatformDictionary): string {
  const lower = key.toLowerCase()
  return (t.system.checkNames as Record<string, string>)[lower] ?? key
}

function ProbeCard({ title, path, loading, children }: { title: string; path: string; loading: boolean; children: ReactNode }) {
  return (
    <section className="card grid content-start gap-1.5 p-4" aria-label={title}>
      <span className="flex items-center gap-2">
        <h2 className="mr-auto text-[15px] font-semibold">{title}</h2>
        {loading ? <Skeleton className="h-5 w-16" /> : children}
      </span>
      <span className="mono text-xs text-fg3">{path}</span>
    </section>
  )
}

function SystemStatus() {
  const t = usePlatformText()
  const format = useFormat()
  const { current } = useOrganization()
  const health = useQuery({
    queryKey: platformKeys.health(),
    queryFn: async () => {
      const [live, ready] = await Promise.allSettled([platformApi.live(), platformApi.ready()])
      return {
        live: live.status === 'fulfilled' ? live.value : null,
        ready: ready.status === 'fulfilled' ? ready.value : null,
      }
    },
    refetchInterval: REFRESH_MS,
  })
  const catalog = useQuery({
    queryKey: contentKeys.rulesCatalog(current?.Id ?? 0),
    queryFn: () => rulesApi.catalog(current?.Id ?? 0),
    enabled: !!current,
  })

  const live = health.data?.live
  const ready = health.data?.ready
  const report: ReadinessReport | null = ready?.body && typeof ready.body === 'object' ? ready.body : null
  const liveOk = !!live && live.status === 200
  const readyView = report ? healthView(report.Status, t) : healthView(undefined, t)
  const checks = Object.entries(report?.Checks ?? {})

  return (
    <>
      <PageHeader
        title={t.system.title}
        subtitle={t.system.subtitle}
        badge={
          report ? (
            <Badge tone={readyView.tone} dot small>
              {readyView.label}
            </Badge>
          ) : null
        }
        actions={
          <Button loading={health.isFetching} onClick={() => void health.refetch()}>
            <RefreshCw aria-hidden />
            {t.system.refresh}
          </Button>
        }
      />
      <p className="mb-3 text-xs text-fg3">
        {[report ? interpolate(t.system.checkedIn, { ms: report.DurationMs }) : null, t.system.autoRefresh].filter(Boolean).join(' · ')}
      </p>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        <ProbeCard title={t.system.live} path="/api/v1.0/healthcheck" loading={health.isLoading}>
          <Badge tone={liveOk ? 'success' : 'danger'} dot small>
            {liveOk ? live.text.replace(/"/g, '') || t.system.healthy : t.system.unreachable}
          </Badge>
        </ProbeCard>
        <ProbeCard title={t.system.ready} path="/api/v1.0/healthcheck/ready" loading={health.isLoading}>
          <span className="flex flex-wrap items-center gap-1.5">
            {ready && ready.status !== 200 && report ? <span className="text-xs text-fg3">{t.system.notReady}</span> : null}
            <Badge tone={readyView.tone} dot small>
              {readyView.label}
            </Badge>
          </span>
        </ProbeCard>
        <section className="card grid content-start gap-2 p-4" aria-label={t.system.checks}>
          <h2 className="text-[15px] font-semibold">{t.system.checks}</h2>
          {health.isLoading ? (
            <Skeleton className="h-5 w-full" />
          ) : !report ? (
            <p className="text-[13px] text-fg3">{t.system.probeFailed}</p>
          ) : checks.length === 0 ? (
            <p className="text-[13px] text-fg3">{t.system.noChecks}</p>
          ) : (
            <ul className="grid gap-1.5">
              {checks.map(([key, check]) => {
                const view = healthView(check.Status, t)
                return (
                  <li key={key} className="flex flex-wrap items-center gap-2 text-[13px]">
                    <b className="mr-auto">{checkName(key, t)}</b>
                    {check.Description ? <span className="text-xs text-fg3">{check.Description}</span> : null}
                    <Badge tone={view.tone} dot small>
                      {view.label}
                    </Badge>
                  </li>
                )
              })}
            </ul>
          )}
        </section>
        <section className="card grid content-start gap-2 p-4" aria-label={t.system.versions}>
          <h2 className="text-[15px] font-semibold">{t.system.versions}</h2>
          <dl className="kv">
            <dt>{t.system.webVersion}</dt>
            <dd className="mono">v{APP_VERSION}</dd>
            <dt>{t.system.apiVersion}</dt>
            <dd className="text-fg3">{t.system.apiVersionGap}</dd>
            <dt>{t.system.catalog}</dt>
            <dd>
              {!current
                ? t.system.catalogNoOrganization
                : catalog.data
                  ? interpolate(t.system.catalogValue, {
                      version: catalog.data.Version,
                      count: catalog.data.RuleCount,
                      date: format.date(catalog.data.PublishedDate),
                    })
                  : catalog.isError
                    ? '-'
                    : '...'}
            </dd>
            <dt>{t.system.license}</dt>
            <dd>
              AGPL-3.0 ·{' '}
              <a className="lnk" href={SOURCE_URL} target="_blank" rel="noopener noreferrer">
                {t.system.sourceCode}
              </a>
            </dd>
          </dl>
        </section>
        <section className="card grid content-start gap-2 p-4" aria-label={t.system.jobs}>
          <h2 className="text-[15px] font-semibold">{t.system.jobs}</h2>
          <p className="text-[13px] text-fg3">{t.system.jobsGap}</p>
        </section>
        <section className="card grid content-start gap-2 p-4 md:col-span-2 xl:col-span-1" aria-label={t.system.registration}>
          <span className="flex items-center gap-2">
            <h2 className="mr-auto text-[15px] font-semibold">{t.system.registration}</h2>
            <span className="mono text-xs text-fg3">{t.system.registrationKey}</span>
          </span>
          <Callout tone="info">{t.system.registrationGap}</Callout>
          <RadioGroup<string>
            name="registration-mode"
            label={t.system.registration}
            value=""
            options={[
              { value: 'Open', label: t.system.modes.open, description: t.system.modes.openBody, disabled: true },
              { value: 'InviteOnly', label: t.system.modes.inviteOnly, description: t.system.modes.inviteOnlyBody, disabled: true },
              { value: 'Closed', label: t.system.modes.closed, description: t.system.modes.closedBody, disabled: true },
            ]}
            onChange={() => undefined}
          />
          <p className="text-xs text-fg3">{t.system.accountsNote}</p>
        </section>
      </div>
    </>
  )
}

/** /admin/system: every platform administrator (no SystemManagement permission group exists yet). */
export function SystemPage() {
  return (
    <PlatformGate>
      <SystemStatus />
    </PlatformGate>
  )
}
