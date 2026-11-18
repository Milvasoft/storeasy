import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Plus, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { SecretBox } from '@/components/ui/Code'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, Checkbox, EmptyState, Field, Input, RadioGroup, TableSkeleton } from '@/components/ui/primitives'
import { OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { addDays, todayDateOnly } from '@/lib/utils/date'
import { ShownOnceDialog } from '@/features/tokens/ShownOnce'
import { intakeApi, intakeKeys } from './api'
import { useIntakeText } from './i18n'
import { IntakeShell, useIntakeApps } from './IntakeShell'
import { ARTIFACTS_WRITE_SCOPE, CI_TOKEN_EXPIRY_DAYS, CI_TOKEN_NAME_MAX, type CiTokenCreatedDto, type CiTokenDto } from './types'

// Design 13.8-13.12: organization CI tokens (sci_) for CI without OIDC, scoped to apps and artifacts:write, with an
// expiry. The value shows once; the list keeps the prefix. Creating and revoking need ManageConnections.

const DAY = 24 * 60 * 60 * 1000
const SOON_DAYS = 30

function CreateDialog({
  onClose,
  onCreated,
}: {
  onClose: () => void
  onCreated: (token: CiTokenCreatedDto, name: string, apps: string) => void
}) {
  const t = useIntakeText()
  const base = useT()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const apps = useIntakeApps()
  const [name, setName] = useState('')
  const [appIds, setAppIds] = useState<number[]>([])
  const [allApps, setAllApps] = useState(false)
  const [days, setDays] = useState<number>(90)
  const create = useMutation({
    mutationFn: () =>
      intakeApi.createCiToken(organization.Id, {
        Name: name.trim(),
        AppIds: allApps ? [] : appIds,
        AllApps: allApps,
        Scopes: [ARTIFACTS_WRITE_SCOPE],
        ExpiryDays: days,
      }),
    onSuccess: (token) => {
      void queryClient.invalidateQueries({ queryKey: intakeKeys.all })
      const names = allApps
        ? t.ciTokens.allApps
        : (apps.data ?? [])
            .filter((app) => appIds.includes(app.Id))
            .map((app) => app.Name)
            .join(', ')
      onCreated(token, name.trim(), names)
    },
  })
  const ready = name.trim().length > 0 && (allApps || appIds.length > 0)
  return (
    <Modal
      open
      onClose={onClose}
      title={t.ciTokenForm.title}
      description={t.ciTokenForm.body}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={create.isPending} disabled={!ready} onClick={() => create.mutate()}>
            {t.ciTokenForm.submit}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <Field label={t.ciTokenForm.name} hint={t.ciTokenForm.nameHint} counter={{ value: name.length, max: CI_TOKEN_NAME_MAX }}>
          <Input value={name} maxLength={CI_TOKEN_NAME_MAX} mono onChange={(event) => setName(event.target.value)} />
        </Field>
        <fieldset className="grid gap-1.5">
          <legend className="text-[13px] font-medium">{t.ciTokenForm.apps}</legend>
          <Checkbox checked={allApps} onChange={setAllApps}>
            {t.ciTokenForm.allApps}
          </Checkbox>
          {!allApps
            ? (apps.data ?? []).map((app) => (
                <Checkbox
                  key={app.Id}
                  checked={appIds.includes(app.Id)}
                  onChange={(checked) => setAppIds(checked ? [...appIds, app.Id] : appIds.filter((id) => id !== app.Id))}
                >
                  {app.Name}
                </Checkbox>
              ))
            : null}
          <span className="text-xs text-fg3">{t.ciTokenForm.appsHint}</span>
        </fieldset>
        <div className="grid gap-0.5">
          <span className="text-[13px] font-medium">{t.ciTokenForm.scope}</span>
          <span className="text-[13px]">
            <code className="mono">{ARTIFACTS_WRITE_SCOPE}</code> · {t.ciTokenForm.scopeHint}
          </span>
        </div>
        <RadioGroup<number>
          name="ci-token-expiry"
          label={t.ciTokenForm.expiry}
          value={days}
          options={CI_TOKEN_EXPIRY_DAYS.map((count) => ({
            value: count,
            label: count === 365 ? t.ciTokenForm.year : interpolate(t.ciTokenForm.days, { count }),
          }))}
          onChange={setDays}
        />
        <span className="text-xs text-fg3">{interpolate(t.ciTokenForm.endsOn, { date: format.date(addDays(todayDateOnly(), days)) })}</span>
      </div>
    </Modal>
  )
}

export function IntakeCiTokensPage() {
  const t = useIntakeText()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const manage = can(OrgPermission.ManageConnections)
  const [creating, setCreating] = useState(false)
  const [created, setCreated] = useState<{ token: CiTokenCreatedDto; name: string; apps: string } | null>(null)
  const tokens = useQuery({ queryKey: intakeKeys.ciTokens(organization.Id), queryFn: () => intakeApi.ciTokens(organization.Id) })
  const revoke = useMutation({
    mutationFn: (id: number) => intakeApi.revokeCiToken(organization.Id, id),
    onSuccess: () => {
      showToast(t.ciTokens.revoked, 'success')
      void queryClient.invalidateQueries({ queryKey: intakeKeys.all })
    },
  })

  const askRevoke = async (token: CiTokenDto) => {
    const name = token.Name ?? `#${token.Id}`
    const last = token.LastUseDate
      ? ` ${interpolate(t.ciTokens.revokeLast, {
          when: format.relative(token.LastUseDate),
          source: token.LastUseSource ? ` · ${token.LastUseSource}` : '',
        })}`
      : ''
    if (
      await confirm({
        title: interpolate(t.ciTokens.revokeTitle, { name }),
        message: `${t.ciTokens.revokeBody}${last}`,
        danger: true,
        confirmLabel: t.ciTokens.revoke,
      })
    ) {
      revoke.mutate(token.Id)
    }
  }

  const columns: Column<CiTokenDto>[] = [
    { key: 'name', header: t.ciTokens.name, role: 'main', cell: (token) => <b className="mono">{token.Name}</b> },
    {
      key: 'apps',
      header: t.ciTokens.apps,
      cell: (token) => (token.AllApps ? t.ciTokens.allApps : (token.AppNames ?? []).join(', ') || '-'),
    },
    {
      key: 'scope',
      header: t.ciTokens.scope,
      role: 'hidePhone',
      cell: (token) => <code className="mono">{(token.Scopes ?? []).join(', ')}</code>,
    },
    { key: 'prefix', header: t.ciTokens.prefix, cell: (token) => <code className="mono">{token.Prefix ?? ''}…</code> },
    {
      key: 'expiry',
      header: t.ciTokens.expiry,
      cell: (token) => {
        if (token.IsExpired) {
          return (
            <Badge small tone="danger">
              {interpolate(t.ciTokens.expired, { date: format.date(token.ExpiryDate) })}
            </Badge>
          )
        }
        const days = Math.ceil((new Date(token.ExpiryDate).getTime() - Date.now()) / DAY)
        return days <= SOON_DAYS ? (
          <Badge small tone="warning">
            {interpolate(t.ciTokens.soon, { date: format.date(token.ExpiryDate), days })}
          </Badge>
        ) : (
          format.date(token.ExpiryDate)
        )
      },
    },
    {
      key: 'last',
      header: t.ciTokens.lastUse,
      role: 'hidePhone',
      cell: (token) =>
        token.LastUseDate ? [format.relative(token.LastUseDate), token.LastUseSource].filter(Boolean).join(' · ') : t.ciTokens.never,
    },
    {
      key: 'actions',
      header: '',
      role: 'action',
      cell: (token) =>
        manage ? (
          <Button
            size="sm"
            variant="danger-outline"
            aria-label={interpolate(t.ciTokens.revokeOf, { name: token.Name ?? `#${token.Id}` })}
            onClick={() => void askRevoke(token)}
          >
            <Trash2 aria-hidden />
            {t.ciTokens.revoke}
          </Button>
        ) : null,
    },
  ]

  const rows = tokens.data ?? []
  return (
    <IntakeShell
      actions={
        <Button disabled={!manage} onClick={() => setCreating(true)}>
          <Plus aria-hidden />
          {t.ciTokens.create}
        </Button>
      }
    >
      <Callout tone="info">{t.ciTokens.intro}</Callout>
      {!manage ? <Callout tone="warning">{t.ciTokens.forbidden}</Callout> : null}
      {tokens.isLoading ? (
        <TableSkeleton columns={[t.ciTokens.name, t.ciTokens.apps, t.ciTokens.prefix, t.ciTokens.expiry]} label={base.common.loading} />
      ) : tokens.isError ? (
        <ScreenState error={tokens.error} onRetry={() => void tokens.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={<KeyRound aria-hidden />} title={t.ciTokens.emptyTitle} description={t.ciTokens.emptyBody} />
      ) : (
        <>
          <DataTable columns={columns} rows={rows} rowKey={(token) => token.Id} caption={t.ciTokens.caption} />
          <p className="text-xs text-fg3">{t.ciTokens.expiredNote}</p>
        </>
      )}
      {creating ? (
        <CreateDialog
          onClose={() => setCreating(false)}
          onCreated={(token, name, apps) => {
            setCreating(false)
            setCreated({ token, name, apps })
          }}
        />
      ) : null}
      {created ? (
        <ShownOnceDialog
          title={interpolate(t.ciTokenForm.createdTitle, { name: created.name })}
          description={interpolate(t.ciTokenForm.createdMeta, { apps: created.apps, date: format.date(created.token.ExpiryDate) })}
          warning={t.ciTokenForm.warning}
          ackLabel={t.ciTokenForm.ack}
          onClose={() => setCreated(null)}
        >
          <div className="grid gap-1">
            <b className="text-[13px]">{t.ciTokenForm.value}</b>
            <SecretBox value={created.token.Token ?? ''} />
          </div>
        </ShownOnceDialog>
      ) : null}
    </IntakeShell>
  )
}
