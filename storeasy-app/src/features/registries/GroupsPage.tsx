import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState, StateView } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Checkbox, Field, Input, PageHeader } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission } from '@/lib/api/types'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { registriesApi } from './api'
import { useRegistries } from './i18n/useRegistries'
import { registriesKeys } from './keys'
import { registryLabel } from './rules'
import type { PackageGroupDto, PackageGroupMemberDto } from './types'

// Package groups (docs/package-registries.md): packages released together, side by side, with who is behind the group's
// highest version; groups suggested from a shared source repository. storeasy data only, nothing changes on a registry.

interface GroupDraft {
  id: number | null
  name: string
  ids: number[]
}

function GroupDialog({ draft, onClose }: { draft: GroupDraft; onClose: () => void }) {
  const t = useRegistries()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<GroupDraft>(draft)
  const [error, setError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)
  const packages = useQuery({
    queryKey: registriesKeys.packages(organization.Id),
    queryFn: () => registriesApi.packages(organization.Id),
  })

  const save = useMutation({
    mutationFn: () => registriesApi.saveGroup(organization.Id, form.id, form.name.trim(), form.ids),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.groups.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
      onClose()
    },
    onError: (failure) => setServer(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = form.name.trim() ? null : t.groups.nameRequired
    setError(problem)
    setServer(null)
    if (!problem) {
      save.mutate()
    }
  }

  const toggle = (id: number, checked: boolean) =>
    setForm({ ...form, ids: checked ? [...form.ids, id] : form.ids.filter((candidate) => candidate !== id) })

  return (
    <Modal
      open
      onClose={onClose}
      title={form.id ? t.groups.edit : t.groups.add}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button type="submit" form="package-group-form" variant="primary" loading={save.isPending}>
            {t.groups.save}
          </Button>
        </>
      }
    >
      <form id="package-group-form" className="grid gap-4" onSubmit={submit} noValidate>
        {server ? <FormErrors title={server} /> : null}
        <Field label={t.groups.name} error={error}>
          <Input value={form.name} maxLength={100} onChange={(event) => setForm({ ...form, name: event.target.value })} />
        </Field>
        <fieldset className="grid max-h-[320px] gap-1 overflow-auto">
          <legend className="text-fg3">{t.groups.members}</legend>
          {(packages.data ?? []).map((item) => (
            <Checkbox key={item.Id} checked={form.ids.includes(item.Id)} onChange={(checked) => toggle(item.Id, checked)}>
              {`${item.Name} (${registryLabel(item.Registry)})`}
            </Checkbox>
          ))}
        </fieldset>
      </form>
    </Modal>
  )
}

function GroupCard({ group, canEdit, onEdit }: { group: PackageGroupDto; canEdit: boolean; onEdit: () => void }) {
  const t = useRegistries()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()

  const remove = useMutation({
    mutationFn: () => registriesApi.removeGroup(organization.Id, group.Id),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.groups.removed, 'success')
      void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  const askRemove = async () => {
    if (
      await confirm({
        title: interpolate(t.groups.removeTitle, { name: group.Name }),
        message: t.groups.removeBody,
        confirmLabel: t.groups.remove,
        danger: true,
      })
    ) {
      remove.mutate()
    }
  }

  const columns: Column<PackageGroupMemberDto>[] = [
    {
      key: 'name',
      header: t.table.package,
      role: 'main',
      cell: (row) => (
        <Link to={`/registries/${row.TrackedPackageId}`} className="mono">
          {row.Name}
        </Link>
      ),
    },
    { key: 'registry', header: t.table.registry, cell: (row) => registryLabel(row.Registry) },
    {
      key: 'latest',
      header: t.table.latest,
      cell: (row) => (
        <span className="inline-flex items-center gap-1.5">
          <span className="mono">{row.LatestVersion ?? '-'}</span>
          {row.Behind ? (
            <Badge small tone="warning">
              {t.groups.behindBadge}
            </Badge>
          ) : null}
        </span>
      ),
    },
    { key: 'published', header: t.table.published, cell: (row) => (row.LatestPublishedDate ? format.date(row.LatestPublishedDate) : '-') },
    {
      key: 'alerts',
      header: t.table.alerts,
      role: 'action',
      cell: (row) =>
        row.OpenAlertCount > 0 ? (
          <Badge small tone="danger">
            {row.OpenAlertCount}
          </Badge>
        ) : (
          <span className="text-fg3">{t.table.none}</span>
        ),
    },
  ]

  return (
    <article className="card" aria-label={group.Name}>
      <div className="card-h flex-wrap">
        <h2 className="h3 mr-auto">{group.Name}</h2>
        <span className="text-fg3">
          {t.groups.highest}: <span className="mono">{group.HighestVersion ?? '-'}</span>
        </span>
        <Badge tone={group.BehindCount > 0 ? 'warning' : 'success'}>
          {group.BehindCount > 0 ? interpolate(t.groups.behind, { count: group.BehindCount }) : t.groups.allCurrent}
        </Badge>
        {canEdit ? (
          <>
            <Button variant="ghost" onClick={onEdit}>
              {t.groups.edit}
            </Button>
            <Button variant="ghost" aria-label={`${t.groups.remove} ${group.Name}`} onClick={() => void askRemove()}>
              <Trash2 aria-hidden />
            </Button>
          </>
        ) : null}
      </div>
      <div className="card-b">
        <DataTable caption={group.Name} columns={columns} rows={group.Members ?? []} rowKey={(row) => row.TrackedPackageId} />
      </div>
    </article>
  )
}

function Groups() {
  const t = useRegistries()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const canEdit = can(OrgPermission.EditContent)
  const [draft, setDraft] = useState<GroupDraft | null>(null)
  const groups = useQuery({ queryKey: registriesKeys.groups(organization.Id), queryFn: () => registriesApi.groups(organization.Id) })
  const suggestions = useQuery({
    queryKey: [...registriesKeys.groups(organization.Id), 'suggestions'],
    queryFn: () => registriesApi.groupSuggestions(organization.Id),
  })

  return (
    <>
      <div className="mb-2">
        <Link to="/registries" className="btn ghost">
          <ArrowLeft aria-hidden />
          {t.groups.back}
        </Link>
      </div>
      <PageHeader
        title={t.groups.title}
        subtitle={t.groups.subtitle}
        actions={
          canEdit ? (
            <Button variant="primary" onClick={() => setDraft({ id: null, name: '', ids: [] })}>
              <Plus aria-hidden />
              {t.groups.add}
            </Button>
          ) : null
        }
      />
      <div className="grid gap-4">
        {groups.isPending ? <CardSkeleton count={2} /> : null}
        {groups.isError ? <ScreenState error={groups.error} onRetry={() => void groups.refetch()} /> : null}
        {groups.data?.length === 0 ? <p className="text-fg3">{t.groups.empty}</p> : null}
        {(groups.data ?? []).map((group) => (
          <GroupCard
            key={group.Id}
            group={group}
            canEdit={canEdit}
            onEdit={() => setDraft({ id: group.Id, name: group.Name, ids: (group.Members ?? []).map((m) => m.TrackedPackageId) })}
          />
        ))}
        {(suggestions.data ?? []).length > 0 ? (
          <section aria-label={t.groups.suggestions} className="grid gap-2">
            <h2 className="text-[15px] font-semibold">{t.groups.suggestions}</h2>
            <p className="text-fg2">{t.groups.suggestionsBody}</p>
            {(suggestions.data ?? []).map((suggestion) => (
              <div key={suggestion.Name} className="card card-b flex flex-wrap items-center gap-2">
                <span className="mono mr-auto">{suggestion.Name}</span>
                <span className="text-fg3">{interpolate(t.groups.packageCount, { count: suggestion.TrackedPackageIds?.length ?? 0 })}</span>
                {canEdit ? (
                  <Button
                    variant="outline"
                    onClick={() => setDraft({ id: null, name: suggestion.Name, ids: suggestion.TrackedPackageIds ?? [] })}
                  >
                    {t.groups.create}
                  </Button>
                ) : null}
              </div>
            ))}
          </section>
        ) : null}
      </div>
      {draft ? <GroupDialog draft={draft} onClose={() => setDraft(null)} /> : null}
    </>
  )
}

/** The package groups page (route /registries/groups). */
export function PackageGroupsPage() {
  const t = useRegistries()
  return useFeature('Registries') ? <Groups /> : <StateView icon={<Trash2 aria-hidden />} title={t.off.title} description={t.off.body} />
}
