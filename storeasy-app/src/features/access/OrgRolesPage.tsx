import { useMemo, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Lock, Plus, Search, Shield } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { foldSearch } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, Field, Input, PageHeader, Textarea } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission, OrgRole, type OrgRoleDto } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { accessOf, canDefine, choiceValue, CUSTOM_ASSIGNABLE } from '@/lib/org/roles'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { useAccessText, type AccessDictionary } from './i18n'
import { MembersTabs } from './MembersTabs'
import { actorAccess, choiceLabel, orgRoleKeys, PERMISSION_COUNT, PERMISSION_GROUPS, permissionName, useOrgRoles } from './roles'

// Design 15.13-15.14 and phase 8: the organization's roles. The four seeded roles are locked and read only; custom roles
// are created, renamed, given permissions and deleted here. A custom role never holds ManageOrganization nor a permission
// its author lacks (OrgRoleRules.CanDefine); a role somebody holds cannot be deleted (409 OrganizationRoleInUse).

export const ROLE_NAME_MAX = 50
export const ROLE_DESCRIPTION_MAX = 200

interface Draft {
  name: string
  description: string
  permissions: OrgPermission[]
}

const NEW = 'new'

function draftOf(role: OrgRoleDto | null): Draft {
  return {
    name: role?.Name ?? '',
    description: role?.Description ?? '',
    permissions: role ? [...role.Permissions] : [OrgPermission.View],
  }
}

function sameSet(a: readonly OrgPermission[], b: readonly OrgPermission[]): boolean {
  return a.length === b.length && a.every((item) => b.includes(item))
}

function PermissionList({
  selected,
  editable,
  held,
  onToggle,
  t,
}: {
  selected: readonly OrgPermission[]
  editable: boolean
  held: (permission: OrgPermission) => boolean
  onToggle: (permission: OrgPermission) => void
  t: AccessDictionary
}) {
  const [query, setQuery] = useState('')
  const [onlySelected, setOnlySelected] = useState(false)
  const needle = foldSearch(query.trim())
  const matches = (permission: OrgPermission) => {
    const name = permissionName(permission)
    const text = foldSearch(`${t.roles.permission[name]} ${name} ${t.roles.permissionBody[name]}`)
    return (!needle || text.includes(needle)) && (!onlySelected || selected.includes(permission))
  }
  const groups = PERMISSION_GROUPS.map((group) => ({ ...group, visible: group.permissions.filter(matches) })).filter(
    (group) => group.visible.length > 0,
  )
  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <span className="inp-wrap w-full max-w-xs">
          <Search aria-hidden />
          <input
            value={query}
            aria-label={t.roles.search}
            placeholder={t.roles.search}
            onChange={(event) => setQuery(event.target.value)}
          />
        </span>
        <Checkbox checked={onlySelected} onChange={setOnlySelected}>
          {t.roles.onlySelected}
        </Checkbox>
        <span className="ml-auto text-xs text-fg3">{interpolate(t.roles.selectedCount, { count: selected.length })}</span>
      </div>
      {groups.length === 0 ? <p className="m-0 text-[13px] text-fg3">{interpolate(t.roles.noMatch, { term: query.trim() })}</p> : null}
      {groups.map((group) => (
        <fieldset key={group.key} className="grid gap-1 rounded-md border border-border p-3">
          <legend className="flex w-full items-center gap-2 px-1 text-[13px] font-semibold">
            {t.roles.groups[group.key]}
            <span className="text-xs font-normal text-fg3">
              {interpolate(t.roles.groupCount, {
                selected: group.permissions.filter((permission) => selected.includes(permission)).length,
                total: group.permissions.length,
              })}
            </span>
          </legend>
          {group.visible.map((permission) => {
            const name = permissionName(permission)
            const ownerOnly = !CUSTOM_ASSIGNABLE.includes(permission)
            const always = permission === OrgPermission.View
            const notHeld = !held(permission)
            const reason = !editable ? null : always ? t.roles.viewAlways : ownerOnly ? t.roles.ownerOnly : notHeld ? t.roles.notHeld : null
            return (
              <div key={permission} className="grid gap-0.5 py-1">
                <Checkbox
                  checked={selected.includes(permission)}
                  disabled={!editable || always || ownerOnly || notHeld}
                  onChange={() => onToggle(permission)}
                >
                  <b className="text-fg">{t.roles.permission[name]}</b> <span className="mono text-xs text-fg3">{name}</span>
                </Checkbox>
                <span className="pl-6 text-xs text-fg3">{t.roles.permissionBody[name]}</span>
                {reason ? <span className="pl-6 text-xs text-fg3">{reason}</span> : null}
              </div>
            )
          })}
        </fieldset>
      ))}
    </div>
  )
}

function RoleEditor({
  role,
  canManage,
  held,
  onSaved,
  onCancel,
}: {
  role: OrgRoleDto | null
  canManage: boolean
  held: (permission: OrgPermission) => boolean
  onSaved: (id: number) => void
  onCancel: () => void
}) {
  const t = useAccessText()
  const base = useT()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const organization = useCurrentOrganization()
  const [draft, setDraft] = useState<Draft>(() => draftOf(role))
  const [nameError, setNameError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)
  const locked = !!role?.IsLocked
  const editable = canManage && !locked
  const creating = role === null
  const changed =
    creating ||
    draft.name.trim() !== role.Name ||
    draft.description.trim() !== (role.Description ?? '') ||
    !sameSet(draft.permissions, role.Permissions)

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: orgRoleKeys.list(organization.Id) })
    void queryClient.invalidateQueries({ queryKey: queryKeys.members.all })
  }

  const save = useMutation({
    mutationFn: () => {
      const input = {
        Name: draft.name.trim(),
        Description: draft.description.trim() || null,
        Permissions: [...draft.permissions].sort((a, b) => a - b),
      }
      return creating
        ? organizationsApi.createRole(organization.Id, input)
        : organizationsApi.updateRole(organization.Id, role.Id ?? 0, input)
    },
    meta: { silentError: true },
    onSuccess: (id) => {
      showToast(interpolate(creating ? t.roles.created : t.roles.updated, { name: draft.name.trim() }), 'success')
      setServer(null)
      refresh()
      onSaved(creating ? id : (role.Id ?? id))
    },
    onError: (error) => setServer(apiErrorMessage(error, base.common.error)),
  })

  const remove = useMutation({
    mutationFn: (target: OrgRoleDto) => organizationsApi.deleteRole(organization.Id, target.Id ?? 0),
    onSuccess: (_id, target) => {
      showToast(interpolate(t.roles.deleted, { name: target.Name }), 'success')
      refresh()
      onCancel()
    },
  })

  const askDelete = async (target: OrgRoleDto) => {
    if (target.MemberCount + target.InvitationCount > 0) {
      // The API answers 409 OrganizationRoleInUse; the dialog says so first and offers nothing to confirm.
      await confirm({
        title: interpolate(t.roles.deleteBlockedTitle, { name: target.Name }),
        message: interpolate(t.roles.deleteInUse, { name: target.Name, members: target.MemberCount, invitations: target.InvitationCount }),
        confirmLabel: base.common.ok,
        cancelLabel: base.common.close,
      })
      return
    }
    if (
      await confirm({
        title: interpolate(t.roles.deleteTitle, { name: target.Name }),
        message: t.roles.deleteBody,
        confirmLabel: t.roles.delete,
        danger: true,
      })
    ) {
      remove.mutate(target)
    }
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const name = draft.name.trim()
    const problem = !name
      ? t.roles.nameRequired
      : name.length > ROLE_NAME_MAX
        ? interpolate(t.roles.nameTooLong, { max: ROLE_NAME_MAX })
        : null
    setNameError(problem)
    if (problem || !changed) {
      return
    }
    save.mutate()
  }

  const toggle = (permission: OrgPermission) =>
    setDraft((current) => ({
      ...current,
      permissions: current.permissions.includes(permission)
        ? current.permissions.filter((item) => item !== permission)
        : [...current.permissions, permission],
    }))

  const title = role ? choiceLabel(role, base) : t.roles.newRole
  return (
    <form className="card" onSubmit={submit} noValidate aria-label={title}>
      <div className="card-h flex-wrap">
        <h2 className="h3">{title}</h2>
        {locked ? (
          <Badge small>
            <Lock className="h-3 w-3" aria-hidden />
            {t.roles.locked}
          </Badge>
        ) : (
          <Badge small tone="accent">
            {t.roles.customRole}
          </Badge>
        )}
        {editable && !creating && changed ? (
          <Badge small tone="warning">
            {t.roles.unsaved}
          </Badge>
        ) : null}
      </div>
      <div className="card-b grid gap-4">
        {server ? <FormErrors title={server} /> : null}
        {locked ? <p className="m-0 text-[13px] text-fg2">{t.roles.lockedNote}</p> : null}
        {editable ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t.roles.name} error={nameError} counter={{ value: draft.name.length, max: ROLE_NAME_MAX }}>
              <Input value={draft.name} maxLength={ROLE_NAME_MAX} onChange={(event) => setDraft({ ...draft, name: event.target.value })} />
            </Field>
            <Field label={t.roles.description} optional counter={{ value: draft.description.length, max: ROLE_DESCRIPTION_MAX }}>
              <Textarea
                rows={1}
                value={draft.description}
                maxLength={ROLE_DESCRIPTION_MAX}
                onChange={(event) => setDraft({ ...draft, description: event.target.value })}
              />
            </Field>
          </div>
        ) : role?.Description ? (
          <p className="m-0 text-[13px] text-fg2">{role.Description}</p>
        ) : null}
        <PermissionList selected={draft.permissions} editable={editable} held={held} onToggle={toggle} t={t} />
      </div>
      {editable ? (
        <div className="card-f flex-wrap justify-end">
          {!creating && role ? (
            <Button variant="ghost" className="mr-auto" loading={remove.isPending} onClick={() => void askDelete(role)}>
              {t.roles.delete}
            </Button>
          ) : null}
          <Button
            variant="ghost"
            disabled={!creating && !changed}
            onClick={() => {
              if (creating) {
                onCancel()
              } else {
                setDraft(draftOf(role))
                setNameError(null)
                setServer(null)
              }
            }}
          >
            {t.roles.cancel}
          </Button>
          <Button type="submit" variant="primary" loading={save.isPending} disabled={!changed}>
            {creating ? t.roles.create : t.roles.save}
          </Button>
        </div>
      ) : null}
    </form>
  )
}

/** /members/roles. */
export function OrgRolesPage() {
  const t = useAccessText()
  const base = useT()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const [params, setParams] = useSearchParams()
  const roles = useOrgRoles(organization.Id)
  const actor = actorAccess(organization)
  const canManage = can(OrgPermission.ManageMembers)
  const held = (permission: OrgPermission) => canDefine(actor, [permission])

  const list = useMemo(() => roles.data ?? [], [roles.data])
  const seeded = list.filter((role) => role.IsLocked)
  const custom = list.filter((role) => !role.IsLocked)
  const selectedKey = params.get('role') ?? choiceValue(OrgRole.Owner)
  const selected = selectedKey === NEW ? null : (list.find((role) => choiceValue(role.Role, role.Id) === selectedKey) ?? list[0] ?? null)

  const select = (key: string | null) => {
    const next = new URLSearchParams(params)
    if (key) {
      next.set('role', key)
    } else {
      next.delete('role')
    }
    setParams(next, { replace: true })
  }

  const roleButton = (role: OrgRoleDto) => {
    const key = choiceValue(role.Role, role.Id)
    const active = selected !== null && selectedKey !== NEW && choiceValue(selected.Role, selected.Id) === key
    const count = accessOf(role).permissions.length
    return (
      <li key={key}>
        <button
          type="button"
          className={cn('lst-i w-full text-left', active && 'bg-bg2')}
          aria-current={active ? 'true' : undefined}
          onClick={() => select(key)}
        >
          {role.IsLocked ? (
            <Lock className="h-4 w-4 flex-none text-fg3" aria-hidden />
          ) : (
            <Shield className="h-4 w-4 flex-none text-fg3" aria-hidden />
          )}
          <span className="min-w-0 flex-1">
            <b className="block truncate">{choiceLabel(role, base)}</b>
            <span className="block text-xs text-fg3">
              {interpolate(t.roles.summary, { count, total: PERMISSION_COUNT, members: role.MemberCount })}
              {role.InvitationCount > 0 ? ` · ${interpolate(t.roles.pendingInvites, { count: role.InvitationCount })}` : ''}
            </span>
          </span>
        </button>
      </li>
    )
  }

  return (
    <>
      <PageHeader
        title={t.roles.title}
        subtitle={t.roles.subtitle}
        actions={
          canManage ? (
            <Button variant="primary" onClick={() => select(NEW)}>
              <Plus aria-hidden />
              {t.roles.newRole}
            </Button>
          ) : undefined
        }
      />
      <MembersTabs />
      {roles.isLoading ? (
        <CardSkeleton count={2} />
      ) : roles.isError ? (
        <ScreenState error={roles.error} onRetry={() => void roles.refetch()} />
      ) : (
        <div className="mt-4 grid gap-4 lg:grid-cols-[minmax(0,320px)_minmax(0,1fr)]">
          <div className="grid content-start gap-4">
            {!canManage ? <Callout tone="info">{t.roles.readOnly}</Callout> : null}
            <section className="card" aria-label={t.roles.seeded}>
              <div className="card-h">
                <h2 className="h3">{t.roles.seeded}</h2>
              </div>
              <ul className="m-0 list-none p-0">{seeded.map(roleButton)}</ul>
            </section>
            <section className="card" aria-label={t.roles.custom}>
              <div className="card-h">
                <h2 className="h3">{t.roles.custom}</h2>
              </div>
              {custom.length === 0 ? (
                <p className="card-b m-0 text-[13px] text-fg3">{t.roles.noCustom}</p>
              ) : (
                <ul className="m-0 list-none p-0">{custom.map(roleButton)}</ul>
              )}
            </section>
          </div>
          {selectedKey === NEW && canManage ? (
            <RoleEditor
              key={NEW}
              role={null}
              canManage={canManage}
              held={held}
              onSaved={(id) => select(choiceValue(OrgRole.Custom, id))}
              onCancel={() => select(null)}
            />
          ) : selected ? (
            <RoleEditor
              key={`${choiceValue(selected.Role, selected.Id)}-${selected.Name}-${selected.Permissions.join('.')}`}
              role={selected}
              canManage={canManage}
              held={held}
              onSaved={(id) => select(choiceValue(OrgRole.Custom, id))}
              onCancel={() => select(null)}
            />
          ) : null}
        </div>
      )}
    </>
  )
}
