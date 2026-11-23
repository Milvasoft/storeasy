import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, Field, Input, PasswordInput, TableSkeleton } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { ConnectionStatus } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { registriesApi } from './api'
import { useRegistries } from './i18n/useRegistries'
import type { RegistriesDictionary } from './i18n/en'
import { registriesKeys } from './keys'
import { CONNECTION_KINDS, connectionKindHint, connectionKindLabel } from './rules'
import { RegistryConnectionKind, type RegistryConnectionDto, type RegistryConnectionInput } from './types'

// The credentials of releases and registry operations (docs/package-registries.md): a GitHub token and the registries'
// keys, sealed in storeasy and shown only by their end. The secret is typed once and never comes back.

const EMPTY: RegistryConnectionInput = { Kind: RegistryConnectionKind.GitHub, Secret: '', ExpiryDate: '' }

function statusBadge(status: ConnectionStatus, t: RegistriesDictionary) {
  const labels: Record<ConnectionStatus, string> = {
    [ConnectionStatus.NotVerified]: t.credentials.statusLabel.notVerified,
    [ConnectionStatus.Verified]: t.credentials.statusLabel.verified,
    [ConnectionStatus.Rejected]: t.credentials.statusLabel.rejected,
    [ConnectionStatus.Unreachable]: t.credentials.statusLabel.unreachable,
  }
  const tone = status === ConnectionStatus.Verified ? 'success' : status === ConnectionStatus.NotVerified ? 'neutral' : 'danger'
  return (
    <Badge small tone={tone}>
      {labels[status]}
    </Badge>
  )
}

function CredentialDialog({ onClose }: { onClose: () => void }) {
  const t = useRegistries()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<RegistryConnectionInput>(EMPTY)
  const [error, setError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => registriesApi.saveConnection(organization.Id, { ...form, Secret: form.Secret.trim() }),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.credentials.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
      onClose()
    },
    onError: (failure) => setServer(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setServer(null)
    if (!form.Secret.trim()) {
      setError(t.credentials.secretRequired)
      return
    }
    setError(null)
    save.mutate()
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={t.credentials.add}
      description={t.credentials.intro}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button type="submit" form="registry-credential-form" variant="primary" loading={save.isPending}>
            {t.credentials.save}
          </Button>
        </>
      }
    >
      <form id="registry-credential-form" className="grid gap-4" onSubmit={submit} noValidate>
        {server ? <FormErrors title={server} /> : null}
        <Field label={t.credentials.kind} hint={connectionKindHint(form.Kind, t)}>
          <Select
            aria-label={t.credentials.kind}
            value={form.Kind}
            options={CONNECTION_KINDS.map((kind) => ({ value: kind, label: connectionKindLabel(kind, t) }))}
            onChange={(kind) => setForm({ ...form, Kind: kind })}
          />
        </Field>
        <Field label={t.credentials.secret} error={error ?? undefined}>
          <PasswordInput
            value={form.Secret}
            autoComplete="off"
            maxLength={4096}
            onChange={(event) => setForm({ ...form, Secret: event.target.value })}
          />
        </Field>
        {form.Kind !== RegistryConnectionKind.GitHub ? (
          <Field label={t.credentials.expiry} hint={t.credentials.expiryHint} optional>
            <Input type="date" value={form.ExpiryDate} onChange={(event) => setForm({ ...form, ExpiryDate: event.target.value })} />
          </Field>
        ) : null}
      </form>
    </Modal>
  )
}

/** The release and operation credentials of the organization; adding and removing needs Manage connections. */
export function RegistryCredentials({ canManage }: { canManage: boolean }) {
  const t = useRegistries()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [adding, setAdding] = useState(false)

  const connections = useQuery({
    queryKey: registriesKeys.connections(organization.Id),
    queryFn: () => registriesApi.connections(organization.Id),
  })

  const remove = useMutation({
    mutationFn: (id: number) => registriesApi.removeConnection(organization.Id, id),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.credentials.removed, 'success')
      void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  const askRemove = async (connection: RegistryConnectionDto) => {
    const kind = connectionKindLabel(connection.Kind, t)
    if (
      await confirm({
        title: interpolate(t.credentials.removeTitle, { kind }),
        message: t.credentials.removeBody,
        confirmLabel: t.credentials.remove,
        danger: true,
      })
    ) {
      remove.mutate(connection.Id)
    }
  }

  const columns: Column<RegistryConnectionDto>[] = [
    { key: 'kind', header: t.credentials.kind, role: 'main', cell: (row) => connectionKindLabel(row.Kind, t) },
    { key: 'fingerprint', header: t.credentials.fingerprint, cell: (row) => <span className="mono">{row.Fingerprint}</span> },
    {
      key: 'status',
      header: t.credentials.status,
      cell: (row) => (
        <span className="inline-flex flex-wrap items-center gap-1.5">
          {statusBadge(row.Status, t)}
          {row.LastVerifyError ? <span className="text-fg3">{row.LastVerifyError}</span> : null}
        </span>
      ),
    },
    { key: 'expiry', header: t.credentials.expiry, role: 'hidePhone', cell: (row) => (row.ExpiryDate ? format.date(row.ExpiryDate) : '-') },
    {
      key: 'actions',
      header: '',
      role: 'action',
      cell: (row) =>
        canManage ? (
          <Button
            variant="ghost"
            aria-label={`${t.credentials.remove} ${connectionKindLabel(row.Kind, t)}`}
            onClick={() => void askRemove(row)}
          >
            <Trash2 aria-hidden />
          </Button>
        ) : null,
    },
  ]

  let body
  if (connections.isPending) {
    body = (
      <TableSkeleton columns={[t.credentials.kind, t.credentials.fingerprint, t.credentials.status]} rows={2} label={t.credentials.title} />
    )
  } else if (connections.isError) {
    body = <ScreenState error={connections.error} onRetry={() => void connections.refetch()} />
  } else if (connections.data.length === 0) {
    body = <p className="text-fg3">{t.credentials.empty}</p>
  } else {
    body = <DataTable caption={t.credentials.title} columns={columns} rows={connections.data} rowKey={(row) => row.Id} />
  }

  return (
    <section aria-label={t.credentials.title} className="mt-6 grid gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="mr-auto text-[15px] font-semibold">{t.credentials.title}</h2>
        {canManage ? (
          <Button variant="outline" onClick={() => setAdding(true)}>
            <Plus aria-hidden />
            {t.credentials.add}
          </Button>
        ) : null}
      </div>
      <p className="text-fg2">{t.credentials.intro}</p>
      {!canManage ? <p className="text-fg3">{t.credentials.readOnly}</p> : null}
      {body}
      {adding ? <CredentialDialog onClose={() => setAdding(false)} /> : null}
    </section>
  )
}
