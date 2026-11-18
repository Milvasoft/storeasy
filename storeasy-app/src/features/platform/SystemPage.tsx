import { useState, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import { Badge, type BadgeTone } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Callout } from '@/components/ui/Feedback'
import { Button, PageHeader, RadioGroup, Skeleton } from '@/components/ui/primitives'
import { APP_VERSION, SOURCE_URL } from '@/config'
import { RegistrationMode } from '@/lib/api/types'
import { useGlobalPermissions } from '@/lib/auth/globalPermissions'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { usePublicSettings } from '@/features/access/publicSettings'
import { platformApi, platformKeys } from './api'
import { PlatformGate, PlatformPermission } from './PlatformCommon'
import { usePlatformText, type PlatformDictionary } from './i18n'
import type { DependencyStatusDto, ReadinessReport, SystemSettingsDto } from './types'

// Design 17.24-17.25: the installation's health on one screen. The anonymous liveness and readiness probes, the instance
// status (system/status: dependencies, job queues, optional features, build) and the registration mode, which a manager
// with SystemManagement.Settings changes after a confirmation (system/settings/registration-mode).

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

/** A dependency of system/status as a badge: not configured, healthy or not. */
export function dependencyView(dependency: DependencyStatusDto | undefined, t: PlatformDictionary): { label: string; tone?: BadgeTone } {
  if (!dependency?.Configured) {
    return { label: t.system.notConfigured }
  }
  if (dependency.Healthy === false) {
    return { label: t.system.unhealthy, tone: 'danger' }
  }
  return dependency.Healthy ? { label: t.system.healthy, tone: 'success' } : { label: t.system.unreachable, tone: 'warning' }
}

export function modeLabel(mode: RegistrationMode, t: PlatformDictionary): string {
  return mode === RegistrationMode.Open
    ? t.system.modes.open
    : mode === RegistrationMode.InviteOnly
      ? t.system.modes.inviteOnly
      : t.system.modes.closed
}

function modeBody(mode: RegistrationMode, t: PlatformDictionary): string {
  return mode === RegistrationMode.Open
    ? t.system.confirmBodies.open
    : mode === RegistrationMode.InviteOnly
      ? t.system.confirmBodies.inviteOnly
      : t.system.confirmBodies.closed
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

function RegistrationForm({ settings, canChange }: { settings: SystemSettingsDto; canChange: boolean }) {
  const t = usePlatformText()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const [mode, setMode] = useState<RegistrationMode>(settings.RegistrationMode)
  const update = useMutation({
    mutationFn: (next: RegistrationMode | null) => platformApi.updateRegistrationMode(next),
    onSuccess: (applied) => {
      showToast(interpolate(t.system.saved, { mode: modeLabel(applied, t) }), 'success')
      void queryClient.invalidateQueries({ queryKey: platformKeys.settings() })
      void queryClient.invalidateQueries({ queryKey: ['system', 'public-settings'] })
    },
  })

  const save = async () => {
    const ok = await confirm({
      title: interpolate(t.system.confirmTitle, { mode: modeLabel(mode, t) }),
      message: modeBody(mode, t),
      confirmLabel: t.system.confirm,
      danger: mode === RegistrationMode.Closed,
    })
    if (ok) {
      update.mutate(mode)
    }
  }

  const reset = async () => {
    const ok = await confirm({
      title: t.system.resetTitle,
      message: interpolate(t.system.resetBody, { mode: modeLabel(settings.ConfiguredRegistrationMode, t) }),
      confirmLabel: t.system.useConfigured,
    })
    if (ok) {
      update.mutate(null)
    }
  }

  const options = [RegistrationMode.Open, RegistrationMode.InviteOnly, RegistrationMode.Closed].map((value) => ({
    value,
    label: value === settings.RegistrationMode ? `${modeLabel(value, t)} · ${t.system.current}` : modeLabel(value, t),
    description:
      value === RegistrationMode.Open
        ? t.system.modes.openBody
        : value === RegistrationMode.InviteOnly
          ? t.system.modes.inviteOnlyBody
          : t.system.modes.closedBody,
    disabled: !canChange || update.isPending,
  }))

  return (
    <>
      <RadioGroup<RegistrationMode>
        name="registration-mode"
        label={t.system.registration}
        value={mode}
        options={options}
        onChange={setMode}
      />
      <p className="text-xs text-fg3">
        {settings.RegistrationModeSaved
          ? interpolate(t.system.registrationSaved, { mode: modeLabel(settings.ConfiguredRegistrationMode, t) })
          : t.system.registrationFromConfig}{' '}
        {t.system.accountsNote}
      </p>
      {canChange ? (
        <span className="flex flex-wrap justify-end gap-2">
          {settings.RegistrationModeSaved ? (
            <Button size="sm" variant="ghost" className="mr-auto" onClick={() => void reset()}>
              {t.system.useConfigured}
            </Button>
          ) : null}
          <Button
            size="sm"
            variant="ghost"
            disabled={mode === settings.RegistrationMode}
            onClick={() => setMode(settings.RegistrationMode)}
          >
            {t.system.cancel}
          </Button>
          <Button
            size="sm"
            variant="primary"
            loading={update.isPending}
            disabled={mode === settings.RegistrationMode}
            onClick={() => void save()}
          >
            {t.system.save}
          </Button>
        </span>
      ) : null}
    </>
  )
}

function RegistrationCard() {
  const t = usePlatformText()
  const global = useGlobalPermissions()
  const canChange = global.has(PlatformPermission.SystemSettings)
  const settings = useQuery({ queryKey: platformKeys.settings(), queryFn: () => platformApi.settings(), enabled: canChange })
  const publicSettings = usePublicSettings()
  // Without the settings permission the anonymous public settings still tell the mode in force.
  const fallback: SystemSettingsDto | null = publicSettings.data
    ? {
        RegistrationMode: publicSettings.data.RegistrationMode,
        RegistrationModeSaved: false,
        ConfiguredRegistrationMode: publicSettings.data.RegistrationMode,
      }
    : null
  const data = canChange ? settings.data : fallback
  return (
    <section className="card grid content-start gap-2 p-4 md:col-span-2 xl:col-span-1" aria-label={t.system.registration}>
      <span className="flex items-center gap-2">
        <h2 className="mr-auto text-[15px] font-semibold">{t.system.registration}</h2>
        <span className="mono text-xs text-fg3">{t.system.registrationKey}</span>
      </span>
      {!canChange ? <Callout tone="info">{t.system.registrationReadOnly}</Callout> : null}
      {data ? (
        <RegistrationForm key={`${data.RegistrationMode}-${String(data.RegistrationModeSaved)}`} settings={data} canChange={canChange} />
      ) : (
        <Skeleton className="h-24 w-full" />
      )}
    </section>
  )
}

function SystemStatus() {
  const t = usePlatformText()
  const format = useFormat()
  const global = useGlobalPermissions()
  const canReadCatalog = global.has(PlatformPermission.RuleCatalogList)
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
  const status = useQuery({ queryKey: platformKeys.status(), queryFn: () => platformApi.status(), refetchInterval: REFRESH_MS })
  const catalog = useQuery({ queryKey: platformKeys.ruleCatalog(), queryFn: () => platformApi.ruleCatalog(), enabled: canReadCatalog })

  const live = health.data?.live
  const ready = health.data?.ready
  const report: ReadinessReport | null = ready?.body && typeof ready.body === 'object' ? ready.body : null
  const liveOk = !!live && live.status === 200
  const readyView = report ? healthView(report.Status, t) : healthView(undefined, t)
  const checks = Object.entries(report?.Checks ?? {})
  const info = status.data
  const services: Array<{ key: keyof PlatformDictionary['system']['serviceNames']; value: DependencyStatusDto | undefined }> = [
    { key: 'database', value: info?.Database },
    { key: 'redis', value: info?.Redis },
    { key: 'storage', value: info?.Storage },
    { key: 'mail', value: info?.Mail },
  ]
  const features: Array<{ key: keyof PlatformDictionary['system']['featureNames']; on: boolean }> = info
    ? [
        { key: 'encryption', on: info.EncryptionKeyConfigured },
        { key: 'google', on: info.GoogleSignInConfigured },
        { key: 'webPush', on: info.WebPushConfigured },
      ]
    : []

  return (
    <>
      <PageHeader
        title={t.system.title}
        subtitle={
          info?.Version
            ? `storeasy ${interpolate(t.system.apiVersionValue, { version: info.Version, commit: info.Commit ?? '-' })}${info.Environment ? ` · ${info.Environment}` : ''}`
            : t.system.subtitle
        }
        badge={
          report ? (
            <Badge tone={readyView.tone} dot small>
              {readyView.label}
            </Badge>
          ) : null
        }
        actions={
          <Button
            loading={health.isFetching || status.isFetching}
            onClick={() => {
              void health.refetch()
              void status.refetch()
            }}
          >
            <RefreshCw aria-hidden />
            {t.system.refresh}
          </Button>
        }
      />
      <p className="mb-3 text-xs text-fg3">
        {[
          report ? interpolate(t.system.checkedIn, { ms: report.DurationMs }) : null,
          info ? interpolate(t.system.statusChecked, { time: format.time(info.CheckedAt) }) : null,
          t.system.autoRefresh,
        ]
          .filter(Boolean)
          .join(' · ')}
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
        <section className="card grid content-start gap-2 p-4" aria-label={t.system.services}>
          <h2 className="text-[15px] font-semibold">{t.system.services}</h2>
          {status.isLoading ? (
            <Skeleton className="h-5 w-full" />
          ) : !info ? (
            <p className="text-[13px] text-fg3">{t.system.probeFailed}</p>
          ) : (
            <ul className="grid gap-1.5">
              {services.map(({ key, value }) => {
                const view = dependencyView(value, t)
                const detail = [value?.Provider, value?.LatencyMs != null ? interpolate(t.system.latency, { ms: value.LatencyMs }) : null]
                  .filter(Boolean)
                  .join(' · ')
                return (
                  <li key={key} className="grid gap-0.5 text-[13px]">
                    <span className="flex flex-wrap items-center gap-2">
                      <b className="mr-auto">{t.system.serviceNames[key]}</b>
                      {detail ? <span className="text-xs text-fg3">{detail}</span> : null}
                      <Badge tone={view.tone} dot small>
                        {view.label}
                      </Badge>
                    </span>
                    {value?.Error ? <span className="err-t text-xs">{value.Error}</span> : null}
                  </li>
                )
              })}
              <li className="flex flex-wrap items-center gap-2 text-[13px]">
                <b className="mr-auto">{t.system.backplane}</b>
                <span className="mono text-xs">{info.SignalRBackplane ?? '-'}</span>
              </li>
            </ul>
          )}
        </section>
        <section className="card grid content-start gap-2 p-4" aria-label={t.system.jobs}>
          <h2 className="text-[15px] font-semibold">{t.system.jobs}</h2>
          {info ? (
            <dl className="kv">
              <dt>{t.system.jobsQueued}</dt>
              <dd className="tabular-nums">{format.number(info.Jobs.Queued)}</dd>
              <dt>{t.system.jobsRunning}</dt>
              <dd className="tabular-nums">{format.number(info.Jobs.Running)}</dd>
              <dt>{t.system.jobsSucceeded}</dt>
              <dd className="tabular-nums">{format.number(info.Jobs.SucceededLastDay)}</dd>
              <dt>{t.system.jobsFailed}</dt>
              <dd className="tabular-nums">{format.number(info.Jobs.FailedLastDay)}</dd>
              <dt>{t.system.jobsOldest}</dt>
              <dd>{info.Jobs.OldestQueuedDate ? format.relative(info.Jobs.OldestQueuedDate) : '-'}</dd>
              <dt>{t.system.pendingEmails}</dt>
              <dd className="tabular-nums">{format.number(info.PendingEmails)}</dd>
            </dl>
          ) : (
            <Skeleton className="h-24 w-full" />
          )}
        </section>
        <section className="card grid content-start gap-2 p-4" aria-label={t.system.features}>
          <h2 className="text-[15px] font-semibold">{t.system.features}</h2>
          <ul className="grid gap-1.5">
            {features.map((feature) => (
              <li key={feature.key} className="flex items-center gap-2 text-[13px]">
                <b className="mr-auto">{t.system.featureNames[feature.key]}</b>
                <Badge small dot tone={feature.on ? 'success' : undefined}>
                  {feature.on ? t.system.configured : t.system.notConfigured}
                </Badge>
              </li>
            ))}
          </ul>
        </section>
        <section className="card grid content-start gap-2 p-4" aria-label={t.system.versions}>
          <h2 className="text-[15px] font-semibold">{t.system.versions}</h2>
          <dl className="kv">
            <dt>{t.system.webVersion}</dt>
            <dd className="mono">v{APP_VERSION}</dd>
            <dt>{t.system.apiVersion}</dt>
            <dd className="mono">
              {info?.Version ? interpolate(t.system.apiVersionValue, { version: info.Version, commit: info.Commit ?? '-' }) : '-'}
            </dd>
            {info?.Environment ? (
              <>
                <dt>{t.system.environment}</dt>
                <dd>{info.Environment}</dd>
              </>
            ) : null}
            <dt>{t.system.catalog}</dt>
            <dd>
              {!canReadCatalog
                ? t.system.catalogNoPermission
                : catalog.data
                  ? interpolate(t.system.catalogValue, {
                      version: catalog.data.Catalog.Version,
                      count: catalog.data.Catalog.RuleCount,
                      date: format.date(catalog.data.Catalog.PublishedDate),
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
        <RegistrationCard />
      </div>
    </>
  )
}

/** /admin/system: SystemManagement.Status; the registration mode needs SystemManagement.Settings to change. */
export function SystemPage() {
  return (
    <PlatformGate permission={PlatformPermission.SystemStatus}>
      <SystemStatus />
    </PlatformGate>
  )
}
