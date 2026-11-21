import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Plus, RotateCcw, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { SecretBox } from '@/components/ui/Code'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Tabs } from '@/components/ui/Tabs'
import { Button, EmptyState, Field, Input, PageHeader, RadioGroup, Switch, TableSkeleton } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgRole } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { roleLabel } from '@/lib/org/roles'
import { showToast } from '@/lib/toast/toast'
import { addDays, todayDateOnly } from '@/lib/utils/date'
import { claudeCodeRemote } from '@/features/mcp/snippets'
import { tokenKeys, tokensApi } from './api'
import { useTokensText } from './i18n'
import { CopyButton, ShownOnceDialog } from './ShownOnce'
import { PAT_EXPIRY_DAYS, PAT_NAME_MAX, type PersonalAccessTokenCreatedDto, type PersonalAccessTokenDto } from './types'

// Design 16.11-16.14: the caller's personal access tokens (sea_) of every organization. The value shows once, in the
// dialog right after creating; afterwards only the prefix. Revoking asks first and works at once.

/** The developer section's heading and tabs, shared by the token list and the MCP setup page. */
export function DeveloperHeader({ count }: { count?: number }) {
  const t = useTokensText()
  return (
    <>
      <PageHeader
        tour="developer"
        title={t.title}
        subtitle={t.subtitle}
        actions={
          <Link className="btn pri" to="/developer?create=1">
            <Plus aria-hidden />
            {t.create}
          </Link>
        }
      />
      <Tabs
        label={t.tabs.label}
        items={[
          { value: 'tokens', label: t.tabs.tokens, to: '/developer', count },
          { value: 'setup', label: t.tabs.setup, to: '/developer/setup' },
        ]}
      />
    </>
  )
}

interface FormSeed {
  name: string
  organizationId: number | null
  readOnly: boolean
}

function CreateTokenDialog({
  seed,
  onClose,
  onCreated,
}: {
  seed: FormSeed
  onClose: () => void
  onCreated: (created: PersonalAccessTokenCreatedDto, input: { name: string; organization: string; readOnly: boolean }) => void
}) {
  const t = useTokensText()
  const base = useT()
  const format = useFormat()
  const queryClient = useQueryClient()
  const { organizations, current } = useOrganization()
  const [name, setName] = useState(seed.name)
  const [organizationId, setOrganizationId] = useState<number | null>(seed.organizationId ?? current?.Id ?? null)
  const [readOnly, setReadOnly] = useState(seed.readOnly)
  const [days, setDays] = useState<number>(90)
  const organization = organizations.find((item) => item.Id === organizationId) ?? null
  const viewer = organization?.Role === OrgRole.Viewer
  const effectiveReadOnly = viewer || readOnly
  const create = useMutation({
    mutationFn: () =>
      tokensApi.create({ OrganizationId: organizationId ?? 0, Name: name.trim(), ReadOnly: effectiveReadOnly, ExpiryDays: days }),
    meta: { silentError: true },
    onSuccess: (created) => {
      void queryClient.invalidateQueries({ queryKey: tokenKeys.all })
      onCreated(created, { name: name.trim(), organization: organization?.Name ?? '', readOnly: effectiveReadOnly })
    },
  })
  const ready = name.trim().length > 0 && name.trim().length <= PAT_NAME_MAX && organization !== null
  return (
    <Modal
      open
      onClose={onClose}
      title={t.form.title}
      description={t.form.body}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={create.isPending} disabled={!ready} onClick={() => create.mutate()}>
            {viewer ? t.form.submitReadOnly : t.form.submit}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        {create.isError ? (
          <Callout tone="danger" title={t.form.failed}>
            {apiErrorMessage(create.error, base.common.error)}
          </Callout>
        ) : null}
        <Field label={t.form.name} counter={{ value: name.length, max: PAT_NAME_MAX }}>
          <Input value={name} maxLength={PAT_NAME_MAX} onChange={(event) => setName(event.target.value)} />
        </Field>
        <Field label={t.form.organization}>
          <Select<number>
            value={organizationId}
            options={organizations.map((item) => ({
              value: item.Id,
              label: interpolate(t.form.organizationOption, { name: item.Name, role: roleLabel(item.Role, base) }),
            }))}
            onChange={setOrganizationId}
          />
        </Field>
        <div className="flex items-start justify-between gap-3">
          <span className="grid gap-0.5">
            <span className="text-[13.5px] font-medium">{t.form.readOnly}</span>
            <span className="text-xs text-fg3">{viewer ? t.form.viewerLocked : t.form.readOnlyHint}</span>
          </span>
          <Switch checked={effectiveReadOnly} disabled={viewer} onChange={setReadOnly} label={t.form.readOnly} />
        </div>
        <RadioGroup<number>
          name="token-expiry"
          label={t.form.expiry}
          value={days}
          options={PAT_EXPIRY_DAYS.map((count) => ({ value: count, label: interpolate(t.form.days, { count }) }))}
          onChange={setDays}
        />
        <span className="text-xs text-fg3">{interpolate(t.form.endsOn, { date: format.date(addDays(todayDateOnly(), days)) })}</span>
        <Callout tone="info">{t.form.limit}</Callout>
      </div>
    </Modal>
  )
}

function CreatedDialog({
  created,
  name,
  organization,
  readOnly,
  onClose,
}: {
  created: PersonalAccessTokenCreatedDto
  name: string
  organization: string
  readOnly: boolean
  onClose: () => void
}) {
  const t = useTokensText()
  const format = useFormat()
  const value = created.Token ?? ''
  const command = claudeCodeRemote(value)
  return (
    <ShownOnceDialog
      title={interpolate(t.created.title, { name })}
      description={interpolate(t.created.meta, {
        organization,
        access: readOnly ? t.list.readOnly : t.list.readWrite,
        date: format.date(created.ExpiryDate),
      })}
      warning={t.created.warning}
      ackLabel={t.created.ack}
      onClose={onClose}
    >
      <div className="grid gap-1">
        <b className="text-[13px]">{t.created.value}</b>
        <SecretBox value={value} />
      </div>
      <div className="grid gap-1.5">
        <b className="text-[13px]">{t.created.claudeCode}</b>
        {/* The command carries the value: it goes to the clipboard, the screen shows the prefix only. */}
        <pre className="code whitespace-pre-wrap break-all">{claudeCodeRemote(`${created.Prefix ?? 'sea_'}…`)}</pre>
        <span className="flex flex-wrap items-center gap-2">
          <CopyButton value={command} label={t.created.copyCommand}>
            {t.created.copyCommand}
          </CopyButton>
          <Link className="lnk text-[13px]" to="/developer/setup">
            {t.created.otherClients}
          </Link>
        </span>
      </div>
    </ShownOnceDialog>
  )
}

export function TokensPage() {
  const t = useTokensText()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()
  const [seed, setSeed] = useState<FormSeed | null>(null)
  const [created, setCreated] = useState<{
    value: PersonalAccessTokenCreatedDto
    name: string
    organization: string
    readOnly: boolean
  } | null>(null)
  const tokens = useQuery({ queryKey: tokenKeys.list(), queryFn: tokensApi.list })
  const revoke = useMutation({
    mutationFn: (tokenId: number) => tokensApi.revoke(tokenId),
    onSuccess: () => {
      showToast(t.revoke.done, 'success')
      void queryClient.invalidateQueries({ queryKey: tokenKeys.all })
    },
  })

  const formSeed: FormSeed | null = seed ?? (params.get('create') ? { name: '', organizationId: null, readOnly: false } : null)
  const closeForm = () => {
    setSeed(null)
    if (params.get('create')) {
      const next = new URLSearchParams(params)
      next.delete('create')
      setParams(next, { replace: true })
    }
  }

  const rows = useMemo(() => tokens.data ?? [], [tokens.data])

  const askRevoke = async (token: PersonalAccessTokenDto) => {
    const name = token.Name ?? `#${token.Id}`
    const ok = await confirm({
      title: interpolate(t.revoke.title, { name }),
      message: (
        <>
          {interpolate(t.revoke.body, { prefix: `${token.Prefix ?? ''}…` })}
          {token.LastUseDate ? ` ${interpolate(t.revoke.lastUse, { when: format.relative(token.LastUseDate) })}` : ''}
        </>
      ),
      danger: true,
      confirmLabel: t.revoke.confirm,
    })
    if (ok) {
      revoke.mutate(token.Id)
    }
  }

  const columns: Column<PersonalAccessTokenDto>[] = [
    {
      key: 'name',
      header: t.list.name,
      role: 'main',
      cell: (token) => <b className={token.IsExpired ? 'text-fg3' : undefined}>{token.Name ?? `#${token.Id}`}</b>,
    },
    { key: 'organization', header: t.list.organization, cell: (token) => token.OrganizationName ?? '-' },
    {
      key: 'access',
      header: t.list.access,
      cell: (token) => (
        <Badge small tone={token.ReadOnly ? 'neutral' : 'accent'}>
          {token.ReadOnly ? t.list.readOnly : t.list.readWrite}
        </Badge>
      ),
    },
    { key: 'prefix', header: t.list.prefix, cell: (token) => <code className="mono">{token.Prefix ?? ''}…</code> },
    {
      key: 'expiry',
      header: t.list.expiry,
      cell: (token) =>
        token.IsExpired ? (
          <Badge small tone="danger">
            {interpolate(t.list.expired, { date: format.date(token.ExpiryDate) })}
          </Badge>
        ) : (
          format.date(token.ExpiryDate)
        ),
    },
    {
      key: 'lastUse',
      header: t.list.lastUse,
      role: 'hidePhone',
      cell: (token) =>
        token.LastUseDate ? [format.relative(token.LastUseDate), token.LastClientName].filter(Boolean).join(' · ') : t.list.never,
    },
    { key: 'created', header: t.list.created, role: 'hidePhone', cell: (token) => format.date(token.CreationDate) },
    {
      key: 'actions',
      header: t.list.actions,
      role: 'action',
      cell: (token) => {
        const name = token.Name ?? `#${token.Id}`
        return (
          <span className="flex justify-end gap-1.5">
            {token.IsExpired ? (
              <Button
                size="sm"
                aria-label={interpolate(t.list.renewOf, { name })}
                onClick={() => setSeed({ name, organizationId: token.OrganizationId, readOnly: token.ReadOnly })}
              >
                <RotateCcw aria-hidden />
                {t.list.renew}
              </Button>
            ) : null}
            <Button
              size="sm"
              variant="danger-outline"
              aria-label={interpolate(t.list.revokeOf, { name })}
              onClick={() => void askRevoke(token)}
            >
              <Trash2 aria-hidden />
              {t.list.revoke}
            </Button>
          </span>
        )
      },
    },
  ]

  return (
    <div className="grid gap-4">
      <DeveloperHeader count={tokens.data?.length} />
      {tokens.isLoading ? (
        <TableSkeleton columns={[t.list.name, t.list.organization, t.list.prefix, t.list.expiry]} label={base.common.loading} />
      ) : tokens.isError ? (
        <ScreenState error={tokens.error} onRetry={() => void tokens.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState
          icon={<KeyRound aria-hidden />}
          title={t.list.emptyTitle}
          description={t.list.emptyBody}
          action={
            <Button variant="primary" onClick={() => setSeed({ name: '', organizationId: null, readOnly: true })}>
              <Plus aria-hidden />
              {t.create}
            </Button>
          }
        />
      ) : (
        <>
          <DataTable columns={columns} rows={rows} rowKey={(token) => token.Id} caption={t.list.caption} />
          <p className="text-xs text-fg3">{t.list.footer}</p>
        </>
      )}
      {formSeed ? (
        <CreateTokenDialog
          key={`${formSeed.name}-${formSeed.organizationId ?? 0}`}
          seed={formSeed}
          onClose={closeForm}
          onCreated={(value, input) => {
            closeForm()
            setCreated({ value, ...input })
          }}
        />
      ) : null}
      {created ? (
        <CreatedDialog
          created={created.value}
          name={created.name}
          organization={created.organization}
          readOnly={created.readOnly}
          onClose={() => setCreated(null)}
        />
      ) : null}
    </div>
  )
}
