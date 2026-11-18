import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, ShieldCheck, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, EmptyState, Field, Input, RadioGroup, TableSkeleton, Textarea } from '@/components/ui/primitives'
import { OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { intakeApi, intakeKeys } from './api'
import { claimText, decodeJwtPayload, expectedClaims } from './claims'
import { useIntakeText } from './i18n'
import { IntakeShell, useIntakeApps } from './IntakeShell'
import { exchangeView, providerLabel } from './labels'
import { OidcExchangeResult, OidcProvider, type CreateTrustedPublisherInput, type TrustedPublisherDto } from './types'

// Design 13.4-13.7: trust rules per app (GitHub, Azure DevOps, GitLab), the claims a matching CI token carries, and the
// diagnosis of the last exchange with a dry run against a pasted token. The token is decoded in the browser for a
// preview; the check itself runs on the server, which issues nothing and stores nothing.

function repositoryOf(rule: TrustedPublisherDto): string {
  return rule.Provider === OidcProvider.AzureDevOps
    ? [rule.Organization, rule.Project].filter(Boolean).join(' / ') + (rule.ServiceConnection ? ` · ${rule.ServiceConnection}` : '')
    : (rule.Repository ?? '-')
}

function PublisherDialog({ onClose, initialAppId }: { onClose: () => void; initialAppId?: number | null }) {
  const t = useIntakeText()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const apps = useIntakeApps()
  const [input, setInput] = useState<CreateTrustedPublisherInput>({
    AppId: initialAppId ?? 0,
    Provider: OidcProvider.GitHub,
    Repository: '',
    Branch: 'main',
    Environment: '',
    Workflow: '',
    Organization: '',
    Project: '',
    ServiceConnection: '',
    Issuer: '',
  })
  const set = (changes: Partial<CreateTrustedPublisherInput>) => setInput((current) => ({ ...current, ...changes }))
  const create = useMutation({
    mutationFn: () => {
      const clean = (value?: string | null) => (value?.trim() ? value.trim() : null)
      const azure = input.Provider === OidcProvider.AzureDevOps
      return intakeApi.createPublisher(organization.Id, {
        AppId: input.AppId,
        Provider: input.Provider,
        Repository: azure ? null : clean(input.Repository),
        Branch: azure ? null : clean(input.Branch),
        Environment: azure ? null : clean(input.Environment),
        Workflow: input.Provider === OidcProvider.GitHub ? clean(input.Workflow) : null,
        Organization: azure ? clean(input.Organization) : null,
        Project: azure ? clean(input.Project) : null,
        ServiceConnection: azure ? clean(input.ServiceConnection) : null,
        Issuer: input.Provider === OidcProvider.GitLab ? clean(input.Issuer) : null,
      })
    },
    onSuccess: () => {
      showToast(t.publisherForm.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: intakeKeys.all })
      onClose()
    },
  })
  const azure = input.Provider === OidcProvider.AzureDevOps
  const ready =
    input.AppId > 0 &&
    (azure
      ? !!input.Organization?.trim() && !!input.Project?.trim() && !!input.ServiceConnection?.trim()
      : !!input.Repository?.trim().includes('/') && (!!input.Branch?.trim() || !!input.Environment?.trim()))
  const claims = expectedClaims(input)

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={t.publisherForm.title}
      description={t.publisherForm.body}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={create.isPending} disabled={!ready} onClick={() => create.mutate()}>
            {t.publisherForm.save}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <RadioGroup<OidcProvider>
          name="publisher-provider"
          label={t.publisherForm.provider}
          value={input.Provider}
          options={[OidcProvider.GitHub, OidcProvider.AzureDevOps, OidcProvider.GitLab].map((provider) => ({
            value: provider,
            label: providerLabel(provider, t),
          }))}
          onChange={(provider) => set({ Provider: provider })}
        />
        <Field label={t.publisherForm.app}>
          <Select<number>
            value={input.AppId || null}
            placeholder={base.common.select}
            options={(apps.data ?? []).map((app) => ({ value: app.Id, label: app.Name }))}
            onChange={(appId) => set({ AppId: appId })}
          />
        </Field>
        {azure ? (
          <>
            <Field label={t.publisherForm.organization}>
              <Input value={input.Organization ?? ''} maxLength={100} onChange={(event) => set({ Organization: event.target.value })} />
            </Field>
            <Field label={t.publisherForm.project}>
              <Input value={input.Project ?? ''} maxLength={100} onChange={(event) => set({ Project: event.target.value })} />
            </Field>
            <Field label={t.publisherForm.serviceConnection}>
              <Input
                value={input.ServiceConnection ?? ''}
                maxLength={200}
                onChange={(event) => set({ ServiceConnection: event.target.value })}
              />
            </Field>
            <Callout tone="warning" title={t.publisherForm.azureBranch}>
              {t.publisherForm.azureNote}
            </Callout>
          </>
        ) : (
          <>
            <Field
              label={input.Provider === OidcProvider.GitLab ? t.publisherForm.projectPath : t.publisherForm.repository}
              hint={input.Provider === OidcProvider.GitLab ? t.publisherForm.projectPathHint : t.publisherForm.repositoryHint}
            >
              <Input value={input.Repository ?? ''} maxLength={200} mono onChange={(event) => set({ Repository: event.target.value })} />
            </Field>
            <Field label={t.publisherForm.branch} hint={t.publisherForm.branchHint}>
              <Input value={input.Branch ?? ''} maxLength={200} mono onChange={(event) => set({ Branch: event.target.value })} />
            </Field>
            <Field label={t.publisherForm.environment} hint={t.publisherForm.environmentHint} optional>
              <Input value={input.Environment ?? ''} maxLength={100} onChange={(event) => set({ Environment: event.target.value })} />
            </Field>
            {input.Provider === OidcProvider.GitHub ? (
              <Field label={t.publisherForm.workflow} hint={t.publisherForm.workflowHint} optional>
                <Input value={input.Workflow ?? ''} maxLength={300} mono onChange={(event) => set({ Workflow: event.target.value })} />
              </Field>
            ) : (
              <Field label={t.publisherForm.issuer} hint={t.publisherForm.issuerHint} optional>
                <Input value={input.Issuer ?? ''} maxLength={300} mono onChange={(event) => set({ Issuer: event.target.value })} />
              </Field>
            )}
          </>
        )}
        <div className="grid gap-1">
          <b className="text-[13px]">{t.publisherForm.expected}</b>
          <pre className="code whitespace-pre-wrap break-all" aria-label={t.publisherForm.expected}>
            {claims.map(([name, value]) => `${name}  ${value}`).join('\n')}
          </pre>
          <span className="text-xs text-fg3">{t.publisherForm.after}</span>
        </div>
      </div>
    </Modal>
  )
}

function DiagnosisDrawer({ ruleId, onClose }: { ruleId: number; onClose: () => void }) {
  const t = useIntakeText()
  const base = useT()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const detail = useQuery({
    queryKey: intakeKeys.publisher(organization.Id, ruleId),
    queryFn: () => intakeApi.publisher(organization.Id, ruleId),
  })
  const [token, setToken] = useState('')
  const payload = useMemo(() => (token.trim() ? decodeJwtPayload(token) : null), [token])
  const test = useMutation({ mutationFn: () => intakeApi.testPublisher(organization.Id, ruleId, token.trim()) })
  const rule = detail.data
  const last = rule?.LastClaims ?? null
  const expiry = typeof payload?.exp === 'number' ? new Date(payload.exp * 1000).toISOString() : null

  return (
    <Modal
      open
      onClose={onClose}
      variant="drawer"
      size="lg"
      title={
        rule
          ? interpolate(t.diagnosis.title, { app: rule.AppName ?? `#${rule.AppId}`, provider: providerLabel(rule.Provider, t) })
          : t.publishers.diagnose
      }
      footer={
        <Button variant="ghost" onClick={onClose}>
          {base.common.close}
        </Button>
      }
    >
      {detail.isLoading ? (
        <CardSkeleton count={1} />
      ) : detail.isError || !rule ? (
        <ScreenState error={detail.error} onRetry={() => void detail.refetch()} />
      ) : (
        <div className="grid gap-4 text-[13px]">
          {rule.LastExchangeDate && rule.LastExchangeResult ? (
            <span className="flex flex-wrap items-center gap-2">
              <Badge small tone={exchangeView(rule.LastExchangeResult, t).tone}>
                {exchangeView(rule.LastExchangeResult, t).label}
              </Badge>
              <span>{format.dateTime(rule.LastExchangeDate)}</span>
              {rule.LastExchangeRunUrl ? (
                <a className="lnk" href={rule.LastExchangeRunUrl} target="_blank" rel="noreferrer">
                  {t.diagnosis.openRun}
                </a>
              ) : null}
            </span>
          ) : (
            <p className="text-fg3">{t.diagnosis.noExchange}</p>
          )}
          <table className="w-full text-left text-[12.5px]">
            <thead>
              <tr className="text-fg3">
                <th className="py-1 pr-2 font-medium">{t.diagnosis.claim}</th>
                <th className="py-1 pr-2 font-medium">{t.diagnosis.expected}</th>
                {last ? <th className="py-1 font-medium">{t.diagnosis.actual}</th> : null}
              </tr>
            </thead>
            <tbody className="mono">
              {Object.entries(rule.ExpectedClaims ?? {}).map(([claim, expected]) => {
                const actual = last?.find((item) => item.Name === claim)?.Value
                return (
                  <tr key={claim} className="border-t border-border">
                    <td className="py-1 pr-2">{claim}</td>
                    <td className="break-all py-1 pr-2">{expected}</td>
                    {last ? <td className="break-all py-1">{actual ?? '-'}</td> : null}
                  </tr>
                )
              })}
            </tbody>
          </table>
          {rule.LastExchangeResult && rule.LastExchangeResult !== OidcExchangeResult.Succeeded ? (
            <Callout tone="info">{t.diagnosis.ciAnswer}</Callout>
          ) : null}

          <section className="grid gap-2" aria-label={t.diagnosis.tryTitle}>
            <b>{t.diagnosis.tryTitle}</b>
            <Field label={t.diagnosis.tokenLabel} hint={t.diagnosis.tryHint}>
              <Textarea
                mono
                rows={3}
                value={token}
                onChange={(event) => {
                  setToken(event.target.value)
                  test.reset()
                }}
              />
            </Field>
            {token.trim() && !payload ? <span className="err-t">{t.diagnosis.notJwt}</span> : null}
            {payload ? (
              <div className="grid gap-1">
                <b className="text-xs text-fg3">{t.diagnosis.preview}</b>
                <pre className="code whitespace-pre-wrap break-all" aria-label={t.diagnosis.preview}>
                  {Object.entries(payload)
                    .map(([name, value]) => `${name}  ${claimText(value)}`)
                    .join('\n')}
                </pre>
                {expiry ? (
                  <span className="text-xs text-fg3">
                    {new Date(expiry).getTime() < Date.now()
                      ? interpolate(t.diagnosis.expired, { date: format.dateTime(expiry) })
                      : interpolate(t.diagnosis.expires, { date: format.dateTime(expiry) })}
                  </span>
                ) : null}
              </div>
            ) : null}
            <Button className="justify-self-start" loading={test.isPending} disabled={!payload} onClick={() => test.mutate()}>
              {t.diagnosis.run}
            </Button>
            {test.data ? (
              <div className="grid gap-2" role="status">
                <span className="flex flex-wrap gap-2">
                  <Badge small tone={test.data.Valid ? 'success' : 'danger'}>
                    {test.data.Valid ? t.diagnosis.valid : t.diagnosis.invalid}
                  </Badge>
                  {test.data.Valid ? (
                    <Badge small tone={test.data.Matched ? 'success' : 'danger'}>
                      {test.data.Matched ? t.diagnosis.matched : t.diagnosis.mismatch}
                    </Badge>
                  ) : null}
                </span>
                {test.data.Error ? <Callout tone="danger">{test.data.Error}</Callout> : null}
                {(test.data.Claims ?? []).length > 0 ? (
                  <table className="w-full text-left text-[12.5px]">
                    <thead>
                      <tr className="text-fg3">
                        <th className="py-1 pr-2 font-medium">{t.diagnosis.claim}</th>
                        <th className="py-1 pr-2 font-medium">{t.diagnosis.expected}</th>
                        <th className="py-1 font-medium">{t.diagnosis.actual}</th>
                      </tr>
                    </thead>
                    <tbody className="mono">
                      {(test.data.Claims ?? []).map((claim) => (
                        <tr key={claim.Claim} className={claim.Matches ? 'border-t border-border' : 'border-t border-border text-danger'}>
                          <td className="py-1 pr-2">{claim.Claim}</td>
                          <td className="break-all py-1 pr-2">{claim.Expected}</td>
                          <td className="break-all py-1">{claim.Actual ?? '-'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                ) : null}
              </div>
            ) : null}
          </section>
        </div>
      )}
    </Modal>
  )
}

export function IntakePublishersPage() {
  const t = useIntakeText()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const manage = can(OrgPermission.ManageConnections)
  const [params, setParams] = useSearchParams()
  const [adding, setAdding] = useState(false)
  const publishers = useQuery({ queryKey: intakeKeys.publishers(organization.Id), queryFn: () => intakeApi.publishers(organization.Id) })
  const remove = useMutation({
    mutationFn: (id: number) => intakeApi.deletePublisher(organization.Id, id),
    onSuccess: () => {
      showToast(t.publishers.deleted, 'success')
      void queryClient.invalidateQueries({ queryKey: intakeKeys.all })
    },
  })
  const openId = Number(params.get('rule')) || null
  const open = (id: number | null) => {
    const next = new URLSearchParams(params)
    if (id) {
      next.set('rule', String(id))
    } else {
      next.delete('rule')
    }
    setParams(next, { replace: true })
  }

  const columns: Column<TrustedPublisherDto>[] = [
    { key: 'app', header: t.publishers.app, role: 'main', cell: (rule) => <b>{rule.AppName ?? `#${rule.AppId}`}</b> },
    { key: 'provider', header: t.publishers.provider, cell: (rule) => providerLabel(rule.Provider, t) },
    { key: 'repository', header: t.publishers.repository, cell: (rule) => <span className="mono break-all">{repositoryOf(rule)}</span> },
    {
      key: 'branch',
      header: t.publishers.branch,
      cell: (rule) =>
        rule.Provider === OidcProvider.AzureDevOps ? (
          <span className="text-fg3">{t.publishers.notInToken}</span>
        ) : (
          <span className="mono">{rule.Branch ?? '-'}</span>
        ),
    },
    { key: 'environment', header: t.publishers.environment, role: 'hidePhone', cell: (rule) => rule.Environment ?? '-' },
    {
      key: 'last',
      header: t.publishers.lastExchange,
      cell: (rule) =>
        rule.LastExchangeDate && rule.LastExchangeResult ? (
          <span className="flex flex-wrap items-center gap-1.5">
            <Badge small tone={exchangeView(rule.LastExchangeResult, t).tone}>
              {exchangeView(rule.LastExchangeResult, t).label}
            </Badge>
            <span className="text-xs text-fg3">{format.dateTime(rule.LastExchangeDate)}</span>
          </span>
        ) : (
          '-'
        ),
    },
    {
      key: 'actions',
      header: '',
      role: 'action',
      cell: (rule) => {
        const name = rule.AppName ?? `#${rule.AppId}`
        return manage ? (
          <span className="flex justify-end gap-1.5">
            <Button size="sm" aria-label={interpolate(t.publishers.diagnoseOf, { name })} onClick={() => open(rule.Id)}>
              {t.publishers.diagnose}
            </Button>
            <Button
              size="sm"
              variant="danger-outline"
              aria-label={interpolate(t.publishers.deleteOf, { name })}
              onClick={async () => {
                if (
                  await confirm({
                    title: t.publishers.deleteTitle,
                    message: t.publishers.deleteBody,
                    danger: true,
                    confirmLabel: t.publishers.delete,
                  })
                ) {
                  remove.mutate(rule.Id)
                }
              }}
            >
              <Trash2 aria-hidden />
            </Button>
          </span>
        ) : null
      },
    },
  ]

  const rows = publishers.data ?? []
  return (
    <IntakeShell
      actions={
        manage ? (
          <Button onClick={() => setAdding(true)}>
            <Plus aria-hidden />
            {t.publishers.add}
          </Button>
        ) : null
      }
    >
      <Callout tone="info" title={t.publishers.flow}>
        {t.publishers.flowNote}
      </Callout>
      {!manage ? <p className="text-xs text-fg3">{t.manageOnly}</p> : null}
      {publishers.isLoading ? (
        <TableSkeleton columns={[t.publishers.app, t.publishers.provider, t.publishers.repository]} label={base.common.loading} />
      ) : publishers.isError ? (
        <ScreenState error={publishers.error} onRetry={() => void publishers.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState
          icon={<ShieldCheck aria-hidden />}
          title={t.publishers.emptyTitle}
          description={t.publishers.emptyBody}
          action={
            manage ? (
              <Button variant="primary" onClick={() => setAdding(true)}>
                {t.publishers.add}
              </Button>
            ) : undefined
          }
        />
      ) : (
        <DataTable columns={columns} rows={rows} rowKey={(rule) => rule.Id} caption={t.publishers.caption} />
      )}
      {adding ? <PublisherDialog onClose={() => setAdding(false)} initialAppId={Number(params.get('app')) || null} /> : null}
      {openId && manage ? <DiagnosisDrawer ruleId={openId} onClose={() => open(null)} /> : null}
    </IntakeShell>
  )
}
