import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Tabs } from '@/components/ui/Tabs'
import { Button, CardSkeleton, Field, Input } from '@/components/ui/primitives'
import { OrgPermission } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { Snippet } from '@/features/tokens/ShownOnce'
import { intakeApi, intakeKeys } from './api'
import { useIntakeText } from './i18n'
import { IntakeShell, useIntakeApps } from './IntakeShell'
import { azurePipeline, cliCommand, githubWorkflow, restSteps } from './snippets'
import { OidcProvider } from './types'

// Design 13.21-13.22: copyable setup snippets per way in, filled with the chosen app's id (client side, no endpoint),
// with a check that the app has a trust rule for the OIDC ways. And the intake settings (retention, size limit).

type SetupPath = 'github' | 'azure' | 'cli' | 'rest' | 'eas' | 'webhook'
const PATHS: SetupPath[] = ['github', 'azure', 'cli', 'rest', 'eas', 'webhook']

export function IntakeSetupPage() {
  const t = useIntakeText()
  const organization = useCurrentOrganization()
  const apps = useIntakeApps()
  const publishers = useQuery({ queryKey: intakeKeys.publishers(organization.Id), queryFn: () => intakeApi.publishers(organization.Id) })
  const [params, setParams] = useSearchParams()
  const path: SetupPath = PATHS.includes(params.get('path') as SetupPath) ? (params.get('path') as SetupPath) : 'github'
  const appId = Number(params.get('app')) || apps.data?.[0]?.Id || null
  const update = (changes: Record<string, string>) => setParams({ ...Object.fromEntries(params), ...changes }, { replace: true })
  const provider = path === 'github' ? OidcProvider.GitHub : path === 'azure' ? OidcProvider.AzureDevOps : null
  const rule = provider ? (publishers.data ?? []).find((item) => item.AppId === appId && item.Provider === provider) : undefined
  const copy = (name: string) => interpolate(t.setup.copy, { name })

  return (
    <IntakeShell>
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t.setup.app}>
          <Select<number>
            className="min-w-[200px]"
            value={appId}
            options={(apps.data ?? []).map((app) => ({
              value: app.Id,
              label: `${app.Name} · ${interpolate(t.setup.appId, { id: app.Id })}`,
            }))}
            onChange={(value) => update({ app: String(value) })}
          />
        </Field>
      </div>
      <Tabs<SetupPath>
        label={t.setup.paths.label}
        value={path}
        onChange={(value) => update({ path: value })}
        items={PATHS.map((value) => ({ value, label: t.setup.paths[value] }))}
      />
      {apps.isLoading ? (
        <CardSkeleton count={1} />
      ) : appId === null ? (
        <p className="text-[13px] text-fg3">{t.setup.pickApp}</p>
      ) : (
        <section className="card grid gap-4 p-4">
          {provider && publishers.isSuccess ? (
            rule ? (
              <Callout tone="success">
                {interpolate(t.setup.ruleReady, {
                  detail: [rule.AppName, rule.Repository ?? rule.ServiceConnection, rule.Branch, rule.Environment]
                    .filter(Boolean)
                    .join(' · '),
                })}
              </Callout>
            ) : (
              <Callout tone="warning">
                {t.setup.ruleMissing}{' '}
                <Link className="lnk" to={`/intake/trusted-publishers?app=${appId}`}>
                  {t.setup.addRule}
                </Link>
              </Callout>
            )
          ) : null}
          {path === 'github' ? (
            <Snippet
              title=".github/workflows/release.yml"
              code={githubWorkflow(appId, rule?.Environment)}
              copyLabel={copy('release.yml')}
            />
          ) : path === 'azure' ? (
            <Snippet
              title="azure-pipelines.yml"
              code={azurePipeline(appId, rule?.ServiceConnection, rule?.Environment)}
              copyLabel={copy('azure-pipelines.yml')}
              hint={t.setup.azureHint}
            />
          ) : path === 'cli' ? (
            <Snippet title="CLI" code={cliCommand(appId)} copyLabel={copy('CLI')} hint={t.setup.cliHint} />
          ) : path === 'rest' ? (
            <Snippet title="REST" code={restSteps(appId)} copyLabel={copy('REST')} hint={t.setup.restHint} />
          ) : (
            <p className="text-[13px] text-fg2">
              {path === 'eas' ? t.setup.easHint : t.setup.webhookHint}{' '}
              <Link className="lnk" to="/intake/sources">
                {t.setup.openSources}
              </Link>
            </p>
          )}
          <div className="grid gap-1 text-[13px]">
            <b>{t.setup.exitCodes}</b>
            <ul className="grid gap-0.5">
              {(
                [
                  [0, t.setup.exit0],
                  [2, t.setup.exit2],
                  [3, t.setup.exit3],
                  [4, t.setup.exit4],
                  [5, t.setup.exit5],
                ] as const
              ).map(([code, text]) => (
                <li key={code}>
                  <code className="mono">{code}</code> {text}
                </li>
              ))}
            </ul>
          </div>
        </section>
      )}
    </IntakeShell>
  )
}

const GB = 1024 * 1024 * 1024
const MAX_GB = 4

export function IntakeSettingsPage() {
  const t = useIntakeText()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const queryClient = useQueryClient()
  const manage = can(OrgPermission.ManageConnections)
  const settings = useQuery({ queryKey: intakeKeys.settings(organization.Id), queryFn: () => intakeApi.settings(organization.Id) })
  const [days, setDays] = useState('')
  const [size, setSize] = useState('')
  useEffect(() => {
    if (settings.data) {
      setDays(String(settings.data.ArtifactRetentionDays))
      setSize(String(Math.round((settings.data.MaxArtifactBytes / GB) * 100) / 100))
    }
  }, [settings.data])
  const save = useMutation({
    mutationFn: () =>
      intakeApi.saveSettings(organization.Id, { ArtifactRetentionDays: Number(days), MaxArtifactBytes: Math.round(Number(size) * GB) }),
    onSuccess: () => {
      showToast(t.settings.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: intakeKeys.settings(organization.Id) })
    },
  })
  const daysValid = Number.isInteger(Number(days)) && Number(days) >= 1 && Number(days) <= 365
  const sizeValid = Number(size) > 0 && Number(size) <= MAX_GB

  return (
    <IntakeShell>
      {settings.isLoading ? (
        <CardSkeleton count={1} />
      ) : settings.isError ? (
        <ScreenState error={settings.error} onRetry={() => void settings.refetch()} />
      ) : (
        <section className="card grid max-w-[520px] gap-3 p-4" aria-label={t.settings.title}>
          <h2 className="text-[15px] font-semibold">{t.settings.title}</h2>
          <Field label={t.settings.retention} hint={t.settings.retentionHint} error={days && !daysValid ? t.settings.invalid : null}>
            <Input type="number" min={1} max={365} value={days} disabled={!manage} onChange={(event) => setDays(event.target.value)} />
          </Field>
          <Field label={t.settings.maxSize} hint={t.settings.maxSizeHint} error={size && !sizeValid ? t.settings.invalid : null}>
            <Input
              type="number"
              min={0.01}
              max={MAX_GB}
              step={0.5}
              value={size}
              disabled={!manage}
              onChange={(event) => setSize(event.target.value)}
            />
          </Field>
          {manage ? (
            <Button
              className="justify-self-start"
              variant="primary"
              loading={save.isPending}
              disabled={!daysValid || !sizeValid}
              onClick={() => save.mutate()}
            >
              {t.settings.save}
            </Button>
          ) : (
            <span className="text-xs text-fg3">{t.manageOnly}</span>
          )}
        </section>
      )}
    </IntakeShell>
  )
}
