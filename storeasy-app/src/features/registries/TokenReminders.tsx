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
import { Button, Field, Input, TableSkeleton } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { registriesApi } from './api'
import { useRegistries } from './i18n/useRegistries'
import type { RegistriesDictionary } from './i18n/en'
import { registriesKeys } from './keys'
import { REGISTRIES, looksLikeToken, registryLabel } from './rules'
import { PackageRegistry, type TokenReminderDto, type TokenReminderInput } from './types'

// Token expiry reminders (docs/package-registries.md): a name, the registry, where the token is used and its expiry date;
// never the token. storeasy warns 14 and 3 days before and on the expiry date.

const EMPTY: TokenReminderInput = { Id: null, Registry: PackageRegistry.Npm, Label: '', UsedIn: '', ExpiryDate: '' }

function stageBadge(reminder: TokenReminderDto, t: RegistriesDictionary) {
  if (reminder.Stage >= 3) {
    return (
      <Badge small tone="danger">
        {reminder.DaysLeft === 0 ? t.reminders.expiresToday : t.reminders.expired}
      </Badge>
    )
  }
  return (
    <Badge small tone={reminder.Stage === 2 ? 'danger' : reminder.Stage === 1 ? 'warning' : 'neutral'}>
      {interpolate(t.reminders.daysLeft, { days: reminder.DaysLeft })}
    </Badge>
  )
}

function ReminderDialog({ value, onClose }: { value: TokenReminderInput | null; onClose: () => void }) {
  const t = useRegistries()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<TokenReminderInput>(value ?? EMPTY)
  const [errors, setErrors] = useState<{ label?: string; usedIn?: string; expiry?: string }>({})
  const [server, setServer] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => registriesApi.saveReminder(organization.Id, { ...form, Label: form.Label.trim(), UsedIn: form.UsedIn.trim() }),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.reminders.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
      onClose()
    },
    onError: (failure) => setServer(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const next = {
      label: !form.Label.trim() ? t.reminders.labelRequired : looksLikeToken(form.Label) ? t.reminders.tokenRefused : undefined,
      usedIn: looksLikeToken(form.UsedIn) ? t.reminders.tokenRefused : undefined,
      expiry: form.ExpiryDate ? undefined : t.reminders.expiryRequired,
    }
    setErrors(next)
    setServer(null)
    if (!next.label && !next.usedIn && !next.expiry) {
      save.mutate()
    }
  }

  return (
    <Modal
      open={value !== null}
      onClose={onClose}
      title={form.Id ? t.reminders.edit : t.reminders.add}
      description={t.reminders.intro}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button type="submit" form="token-reminder-form" variant="primary" loading={save.isPending}>
            {t.reminders.save}
          </Button>
        </>
      }
    >
      <form id="token-reminder-form" className="grid gap-4" onSubmit={submit} noValidate>
        {server ? <FormErrors title={server} /> : null}
        <Field label={t.reminders.registry}>
          <Select
            aria-label={t.reminders.registry}
            value={form.Registry}
            options={REGISTRIES.map((registry) => ({ value: registry, label: registryLabel(registry) }))}
            onChange={(registry) => setForm({ ...form, Registry: registry })}
          />
        </Field>
        <Field label={t.reminders.label} hint={t.reminders.labelHint} error={errors.label}>
          <Input
            value={form.Label}
            maxLength={100}
            autoComplete="off"
            onChange={(event) => setForm({ ...form, Label: event.target.value })}
          />
        </Field>
        <Field label={t.reminders.usedIn} hint={t.reminders.usedInHint} error={errors.usedIn} optional>
          <Input
            value={form.UsedIn}
            maxLength={200}
            autoComplete="off"
            onChange={(event) => setForm({ ...form, UsedIn: event.target.value })}
          />
        </Field>
        <Field label={t.reminders.expiry} error={errors.expiry}>
          <Input type="date" value={form.ExpiryDate} onChange={(event) => setForm({ ...form, ExpiryDate: event.target.value })} />
        </Field>
      </form>
    </Modal>
  )
}

/** The token reminders of the organization, with add, change and remove for editors. */
export function TokenReminders({ canEdit }: { canEdit: boolean }) {
  const t = useRegistries()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<TokenReminderInput | null>(null)

  const reminders = useQuery({
    queryKey: registriesKeys.reminders(organization.Id),
    queryFn: () => registriesApi.reminders(organization.Id),
  })

  const remove = useMutation({
    mutationFn: (id: number) => registriesApi.removeReminder(organization.Id, id),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.reminders.removed, 'success')
      void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  const askRemove = async (reminder: TokenReminderDto) => {
    if (
      await confirm({
        title: interpolate(t.reminders.removeTitle, { label: reminder.Label }),
        confirmLabel: t.reminders.remove,
        danger: true,
      })
    ) {
      remove.mutate(reminder.Id)
    }
  }

  const columns: Column<TokenReminderDto>[] = [
    { key: 'label', header: t.reminders.label, role: 'main', cell: (row) => row.Label },
    { key: 'registry', header: t.reminders.registry, cell: (row) => registryLabel(row.Registry) },
    { key: 'usedIn', header: t.reminders.usedIn, role: 'hidePhone', cell: (row) => row.UsedIn ?? '-' },
    {
      key: 'expiry',
      header: t.reminders.expiry,
      cell: (row) => (
        <span className="inline-flex flex-wrap items-center gap-1.5">
          {format.date(row.ExpiryDate)}
          {stageBadge(row, t)}
          {row.Stage > 0 && row.TrustedPublishingAvailable ? <span className="text-fg3">{t.reminders.moveHint}</span> : null}
        </span>
      ),
    },
    {
      key: 'actions',
      header: '',
      role: 'action',
      cell: (row) =>
        canEdit ? (
          <span className="inline-flex gap-1">
            <Button
              variant="ghost"
              onClick={() =>
                setEditing({
                  Id: row.Id,
                  Registry: row.Registry,
                  Label: row.Label,
                  UsedIn: row.UsedIn ?? '',
                  ExpiryDate: row.ExpiryDate.slice(0, 10),
                })
              }
            >
              {t.reminders.edit}
            </Button>
            <Button variant="ghost" aria-label={`${t.reminders.remove} ${row.Label}`} onClick={() => void askRemove(row)}>
              <Trash2 aria-hidden />
            </Button>
          </span>
        ) : null,
    },
  ]

  let body
  if (reminders.isPending) {
    body = <TableSkeleton columns={[t.reminders.label, t.reminders.registry, t.reminders.expiry]} label={t.reminders.caption} />
  } else if (reminders.isError) {
    body = <ScreenState error={reminders.error} onRetry={() => void reminders.refetch()} />
  } else if (reminders.data.length === 0) {
    body = <p className="text-fg3">{t.reminders.empty}</p>
  } else {
    body = <DataTable caption={t.reminders.caption} columns={columns} rows={reminders.data} rowKey={(row) => row.Id} />
  }

  return (
    <section aria-label={t.reminders.title} className="mt-6 grid gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="mr-auto text-[15px] font-semibold">{t.reminders.title}</h2>
        {canEdit ? (
          <Button variant="outline" onClick={() => setEditing(EMPTY)}>
            <Plus aria-hidden />
            {t.reminders.add}
          </Button>
        ) : null}
      </div>
      <p className="text-fg2">{t.reminders.intro}</p>
      {body}
      {editing ? <ReminderDialog value={editing} onClose={() => setEditing(null)} /> : null}
    </section>
  )
}
