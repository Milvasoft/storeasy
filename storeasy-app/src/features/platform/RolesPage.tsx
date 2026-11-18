import { useMemo, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, SearchX, Shield, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, Input, PageHeader, Switch, TableSkeleton } from '@/components/ui/primitives'
import { ApiError, apiErrorMessage, apiStatus } from '@/lib/api/http'
import { FilterType, SortType, type ListRequest } from '@/lib/api/list'
import { useGlobalPermissions } from '@/lib/auth/globalPermissions'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { platformApi, platformKeys } from './api'
import { AuditCard, MAX_ROWS, PlatformGate, PlatformPermission, SystemRecordBadge } from './PlatformCommon'
import { usePlatformText, type PlatformDictionary } from './i18n'
import { isSeedRecord, updated, type PermissionListDto, type RoleDetailDto, type RoleListDto } from './types'

// Design 17.16-17.19: global roles are a name, a permission list and members. Permissions come from the code catalog
// (PermissionCatalog); App.SuperAdmin is not in the list but the separate, confirmed "Full access" switch.

const DEFAULT_PAGE_SIZE = 20
const SUPER_GROUP = 'App'
const SUPER_NAME = 'SuperAdmin'
const PERMISSION_REQUEST: ListRequest = { PageNumber: 1, RowCount: MAX_ROWS }

export interface PermissionGroup {
  key: string
  label: string
  permissions: PermissionListDto[]
}

export function isSuperPermission(permission: PermissionListDto): boolean {
  return permission.PermissionGroup === SUPER_GROUP && permission.Name === SUPER_NAME
}

/** The catalog grouped by PermissionGroup, catalog order kept; the full access permission is left out. */
export function groupPermissions(permissions: PermissionListDto[]): PermissionGroup[] {
  const groups = new Map<string, PermissionGroup>()
  for (const permission of permissions) {
    if (isSuperPermission(permission)) {
      continue
    }
    const group = groups.get(permission.PermissionGroup) ?? {
      key: permission.PermissionGroup,
      label: permission.PermissionGroupDescription || permission.PermissionGroup,
      permissions: [],
    }
    group.permissions.push(permission)
    groups.set(permission.PermissionGroup, group)
  }
  return [...groups.values()]
}

export function actionLabel(name: string, t: PlatformDictionary): string {
  return (t.roles.actions as Record<string, string>)[name] ?? name
}

function rolesRequest(params: URLSearchParams): ListRequest {
  const term = params.get('q')?.trim()
  const page = Number(params.get('page'))
  const size = Number(params.get('size'))
  return {
    PageNumber: Number.isInteger(page) && page > 0 ? page : 1,
    RowCount: Number.isInteger(size) && size > 0 ? size : DEFAULT_PAGE_SIZE,
    Sorting: { SortBy: 'Name', Type: SortType.Asc },
    ...(term ? { Filtering: { Criterias: [{ FilterBy: 'Name', Value: term, Type: FilterType.Contains }] } } : {}),
  }
}

function usePermissionCatalog(enabled: boolean) {
  return useQuery({
    queryKey: platformKeys.permissions(),
    queryFn: () => platformApi.permissions(PERMISSION_REQUEST),
    enabled,
    staleTime: 5 * 60_000,
  })
}

// ---- Form -----------------------------------------------------------------------------------------------------------

function RoleFormModal({ role, onClose }: { role: RoleDetailDto | null; onClose: () => void }) {
  const t = usePlatformText()
  const base = useT()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const global = useGlobalPermissions()
  const errorsRef = useRef<HTMLDivElement>(null)
  const canReadCatalog = global.has(PlatformPermission.PermissionList)
  const catalog = usePermissionCatalog(canReadCatalog)
  const all = useMemo(() => catalog.data?.Data ?? [], [catalog.data])
  const superPermission = all.find(isSuperPermission)
  const initialIds = useMemo(() => (role?.Permissions ?? []).map((permission) => permission.Id), [role])
  const [name, setName] = useState(role?.Name ?? '')
  const [ids, setIds] = useState<number[]>(initialIds)
  const [search, setSearch] = useState('')
  const [onlySelected, setOnlySelected] = useState(false)
  const [touched, setTouched] = useState(false)
  const superOn = superPermission ? ids.includes(superPermission.Id) : false
  const groups = useMemo(() => groupPermissions(all), [all])
  const needle = search.trim().toLocaleLowerCase()
  const visibleGroups = groups
    .map((group) => ({
      ...group,
      visible: group.permissions.filter(
        (permission) =>
          (!onlySelected || ids.includes(permission.Id)) &&
          (!needle ||
            [group.label, group.key, permission.Name, actionLabel(permission.Name, t), permission.Description ?? '']
              .join(' ')
              .toLocaleLowerCase()
              .includes(needle)),
      ),
    }))
    .filter((group) => group.visible.length > 0)

  const nameChanged = name.trim() !== (role?.Name ?? '')
  const idsChanged = ids.length !== initialIds.length || ids.some((id) => !initialIds.includes(id))
  const dirty = nameChanged || idsChanged
  const nameError = name.trim() ? null : t.roles.form.nameRequired

  const save = useMutation({
    meta: { silentError: true },
    mutationFn: () =>
      role
        ? platformApi.updateRole({
            Id: role.Id,
            ...(nameChanged ? { Name: updated(name.trim()) } : {}),
            ...(idsChanged ? { PermissionIdList: updated(ids) } : {}),
          })
        : platformApi.createRole({ Name: name.trim(), PermissionIdList: ids }),
    onSuccess: (id) => {
      showToast(role ? t.roles.saved : t.roles.created, 'success')
      void queryClient.invalidateQueries({ queryKey: platformKeys.all })
      onClose()
      if (!role) {
        navigate(`/admin/roles/${id}`)
      }
    },
    onError: () => {
      requestAnimationFrame(() => errorsRef.current?.focus())
    },
  })

  const submit = (event?: FormEvent) => {
    event?.preventDefault()
    setTouched(true)
    if (nameError || (role && !dirty)) {
      return
    }
    save.mutate()
  }

  const close = async () => {
    if (dirty && !save.isPending) {
      const ok = await confirm({
        title: t.common.unsavedClose,
        message: t.common.unsavedCloseBody,
        confirmLabel: t.common.discard,
        cancelLabel: t.common.keepEditing,
        danger: true,
      })
      if (!ok) {
        return
      }
    }
    onClose()
  }

  const toggleSuper = async (value: boolean) => {
    if (!superPermission) {
      return
    }
    if (value) {
      const ok = await confirm({
        title: t.roles.superConfirm.title,
        message: interpolate(t.roles.superConfirm.body, { name: name.trim() || t.roles.form.createTitle }),
        confirmLabel: t.roles.superConfirm.confirm,
        danger: true,
      })
      if (!ok) {
        return
      }
      setIds((current) => [...current, superPermission.Id])
    } else {
      setIds((current) => current.filter((id) => id !== superPermission.Id))
    }
  }

  const toggleGroup = (group: PermissionGroup, value: boolean) => {
    const groupIds = group.permissions.map((permission) => permission.Id)
    setIds((current) => (value ? [...new Set([...current, ...groupIds])] : current.filter((id) => !groupIds.includes(id))))
  }

  const selectedCount = ids.filter((id) => id !== superPermission?.Id).length
  const failure = save.error
  const failureMessages =
    failure instanceof ApiError && failure.messages.length > 0 ? failure.messages : [apiErrorMessage(failure, base.common.error)]

  return (
    <Modal
      open
      size="lg"
      onClose={() => void close()}
      title={role ? t.roles.form.editTitle : t.roles.form.createTitle}
      description={role ? `${role.Name} · #${role.Id}` : undefined}
      footer={
        <>
          <Button variant="ghost" onClick={() => void close()}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={save.isPending} disabled={(touched && !!nameError) || (!!role && !dirty)} onClick={() => submit()}>
            {base.common.save}
          </Button>
        </>
      }
    >
      <form className="grid gap-3" onSubmit={submit} noValidate>
        {failure ? <FormErrors ref={errorsRef} title={role ? t.common.saveFailed : t.roles.form.createFailed} messages={failureMessages} /> : null}
        <Field label={t.roles.form.name} error={touched ? nameError : null}>
          <Input value={name} onChange={(event) => setName(event.target.value)} />
        </Field>
        {!canReadCatalog ? (
          <Callout tone="info">{t.roles.form.catalogForbidden}</Callout>
        ) : catalog.isLoading ? (
          <CardSkeleton count={2} />
        ) : catalog.isError ? (
          apiStatus(catalog.error) === 403 ? (
            <Callout tone="info">{t.roles.form.catalogForbidden}</Callout>
          ) : (
            <Callout tone="danger" title={t.roles.form.catalogError}>
              <Button size="sm" onClick={() => void catalog.refetch()}>
                {t.roles.form.retry}
              </Button>
            </Callout>
          )
        ) : (
          <>
            {superPermission ? (
              <div className="card flex items-start gap-3 p-3">
                <Switch checked={superOn} onChange={(value) => void toggleSuper(value)} label={t.roles.form.superTitle} />
                <span className="grid gap-0.5">
                  <b className="text-[13.5px]">{t.roles.form.superTitle}</b>
                  <span className="text-xs text-fg3">{t.roles.form.superBody}</span>
                  <span className="mono text-xs text-fg3">App.SuperAdmin</span>
                </span>
              </div>
            ) : null}
            <div className="toolbar !mb-0">
              <span className="w-full sm:w-56">
                <input
                  className="inp"
                  type="search"
                  aria-label={t.roles.form.search}
                  placeholder={t.roles.form.search}
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </span>
              <Checkbox checked={onlySelected} onChange={setOnlySelected}>
                {t.roles.form.onlySelected}
              </Checkbox>
              <span className="ml-auto text-xs text-fg3">{interpolate(t.roles.form.selected, { count: selectedCount })}</span>
            </div>
            {superOn ? <p className="text-xs text-warningText">{t.roles.form.superInactive}</p> : null}
            {visibleGroups.length === 0 ? (
              <p className="text-[13px] text-fg3">{t.roles.form.noMatch}</p>
            ) : (
              <div className="grid gap-2">
                {visibleGroups.map((group) => {
                  const chosen = group.permissions.filter((permission) => ids.includes(permission.Id)).length
                  return (
                    <fieldset key={group.key} className="card grid gap-2 p-3 sm:grid-cols-[minmax(0,14rem)_minmax(0,1fr)]" disabled={superOn}>
                      <legend className="sr-only">{group.label}</legend>
                      <div className="grid content-start gap-1">
                        <b className="text-[13.5px]">{group.label}</b>
                        <span className="mono text-xs text-fg3">{group.key}</span>
                        <span className="text-xs text-fg3">
                          {interpolate(t.roles.form.groupSelected, { selected: chosen, total: group.permissions.length })}
                        </span>
                        <Checkbox
                          checked={chosen === group.permissions.length}
                          indeterminate={chosen > 0 && chosen < group.permissions.length}
                          disabled={superOn}
                          onChange={(value) => toggleGroup(group, value)}
                        >
                          <span className="sr-only">{group.label}: </span>
                          {t.roles.form.selectAll}
                        </Checkbox>
                      </div>
                      <div className="grid gap-1.5">
                        {group.visible.map((permission) => (
                          <Checkbox
                            key={permission.Id}
                            checked={ids.includes(permission.Id)}
                            disabled={superOn}
                            className="items-start"
                            onChange={(value) =>
                              setIds((current) => (value ? [...current, permission.Id] : current.filter((id) => id !== permission.Id)))
                            }
                          >
                            <span className="grid gap-0.5">
                              <span className="font-medium text-fg">{actionLabel(permission.Name, t)}</span>
                              {permission.Description ? <span className="text-xs text-fg3">{permission.Description}</span> : null}
                              <span className="mono text-[11px] text-fg3">{`${permission.PermissionGroup}.${permission.Name}`}</span>
                            </span>
                          </Checkbox>
                        ))}
                      </div>
                    </fieldset>
                  )
                })}
              </div>
            )}
          </>
        )}
        <p className="text-xs text-fg3">{t.roles.form.note}</p>
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}

// ---- Detail drawer --------------------------------------------------------------------------------------------------

function RoleDrawer({ roleId, onClose, onEdit }: { roleId: number; onClose: () => void; onEdit: (role: RoleDetailDto) => void }) {
  const t = usePlatformText()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const global = useGlobalPermissions()
  const detail = useQuery({ queryKey: platformKeys.role(roleId), queryFn: () => platformApi.role(roleId) })
  const catalog = usePermissionCatalog(global.has(PlatformPermission.PermissionList))
  const remove = useMutation({
    mutationFn: () => platformApi.deleteRole(roleId),
    onSuccess: () => {
      showToast(t.roles.deleted, 'success')
      void queryClient.invalidateQueries({ queryKey: platformKeys.all })
      onClose()
    },
  })
  const role = detail.data
  const seed = isSeedRecord(roleId)
  const permissionIds = (role?.Permissions ?? []).map((permission) => permission.Id)
  const all = catalog.data?.Data ?? []
  const superPermission = all.find(isSuperPermission)
  const fullAccess = seed || (superPermission ? permissionIds.includes(superPermission.Id) : false)
  const groups = groupPermissions(all.filter((permission) => permissionIds.includes(permission.Id)))
  const uncatalogued = (role?.Permissions ?? []).filter((permission) => !all.some((item) => item.Id === permission.Id))

  const deleteRole = async () => {
    if (!role) {
      return
    }
    const ok = await confirm({
      title: t.roles.remove.title,
      message: interpolate(t.roles.remove.body, { name: role.Name }),
      confirmLabel: t.roles.remove.confirm,
      danger: true,
    })
    if (ok) {
      remove.mutate()
    }
  }

  return (
    <Modal
      open
      variant="drawer"
      onClose={onClose}
      title={role?.Name ?? `#${roleId}`}
      description={role ? `#${role.Id} · ${interpolate(t.roles.permissionCount, { count: role.Permissions?.length ?? 0 })}` : undefined}
      footer={
        role && !seed ? (
          <>
            {global.has(PlatformPermission.RoleDelete) ? (
              <Button variant="danger-outline" loading={remove.isPending} onClick={() => void deleteRole()}>
                <Trash2 aria-hidden />
                {t.common.delete}
              </Button>
            ) : null}
            {global.has(PlatformPermission.RoleUpdate) ? (
              <Button variant="primary" onClick={() => onEdit(role)}>
                <Pencil aria-hidden />
                {t.common.edit}
              </Button>
            ) : null}
          </>
        ) : undefined
      }
    >
      {detail.isLoading ? (
        <CardSkeleton count={2} />
      ) : detail.isError || !role ? (
        <ScreenState error={detail.error} onRetry={() => void detail.refetch()} />
      ) : (
        <div className="grid gap-4">
          {seed ? (
            <span className="flex flex-wrap gap-1.5">
              <SystemRecordBadge />
              <Badge small>{t.common.readOnly}</Badge>
            </span>
          ) : null}
          {fullAccess ? (
            <Callout tone="warning" title={t.roles.fullAccess}>
              {t.roles.form.superBody}
            </Callout>
          ) : null}
          {groups.length === 0 && uncatalogued.length === 0 && !fullAccess ? (
            <p className="text-[13px] text-fg3">{t.roles.noPermissions}</p>
          ) : (
            <div className="grid gap-3">
              {groups.map((group) => (
                <div key={group.key} className="grid gap-1">
                  <b className="text-[13.5px]">{group.label}</b>
                  <span className="flex flex-wrap gap-1.5">
                    {group.permissions.map((permission) => (
                      <Badge key={permission.Id} small>
                        {actionLabel(permission.Name, t)}
                      </Badge>
                    ))}
                  </span>
                </div>
              ))}
              {uncatalogued.filter((permission) => !(fullAccess && permission.Name === SUPER_NAME)).length > 0 ? (
                <span className="flex flex-wrap gap-1.5">
                  {uncatalogued
                    .filter((permission) => !(fullAccess && permission.Name === SUPER_NAME))
                    .map((permission) => (
                      <Badge key={permission.Id} small>
                        {actionLabel(permission.Name ?? '', t)}
                      </Badge>
                    ))}
                </span>
              ) : null}
            </div>
          )}
          <section className="grid gap-1">
            <h3 className="text-[13.5px] font-semibold">{t.roles.users}</h3>
            {(role.Users ?? []).length === 0 ? (
              <p className="text-[13px] text-fg3">{t.roles.noUsers}</p>
            ) : (
              <ul className="grid gap-1 text-[13px]">
                {(role.Users ?? []).map((user) => (
                  <li key={user.Id}>
                    {global.has(PlatformPermission.UserDetail) ? (
                      <Link className="lnk" to={`/admin/users/${user.Id}`}>
                        {user.Name ?? `#${user.Id}`}
                      </Link>
                    ) : (
                      (user.Name ?? `#${user.Id}`)
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>
          <AuditCard audit={role.AuditInfo} />
        </div>
      )}
    </Modal>
  )
}

// ---- List -----------------------------------------------------------------------------------------------------------

function RolesList() {
  const t = usePlatformText()
  const base = useT()
  const navigate = useNavigate()
  const global = useGlobalPermissions()
  const { roleId: rawRoleId } = useParams()
  const [params, setParams] = useSearchParams()
  const [draft, setDraft] = useState(params.get('q') ?? '')
  const [form, setForm] = useState<{ role: RoleDetailDto | null } | null>(null)
  const request = rolesRequest(params)
  const roles = useQuery({ queryKey: platformKeys.roles(request), queryFn: () => platformApi.roles(request), placeholderData: keepPreviousData })
  const openRoleId = Number(rawRoleId)
  const canDetail = global.has(PlatformPermission.RoleDetail)

  const update = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value) {
        next.set(key, value)
      } else {
        next.delete(key)
      }
    }
    if (!('page' in changes)) {
      next.delete('page')
    }
    setParams(next, { replace: true })
  }
  const term = params.get('q')?.trim() ?? ''
  const columns: Column<RoleListDto>[] = [
    {
      key: 'name',
      header: t.roles.name,
      role: 'main',
      cell: (role) => (
        <span className="flex flex-wrap items-center gap-1.5">
          <b>{role.Name}</b>
          <span className="text-xs text-fg3">#{role.Id}</span>
          {isSeedRecord(role.Id) ? (
            <>
              <SystemRecordBadge />
              <Badge small tone="warning">
                {t.roles.fullAccess}
              </Badge>
              <Badge small>{t.common.readOnly}</Badge>
            </>
          ) : null}
        </span>
      ),
    },
  ]
  const rows = roles.data?.Data ?? []

  return (
    <>
      <PageHeader
        title={t.roles.title}
        subtitle={t.roles.subtitle}
        actions={
          global.has(PlatformPermission.RoleCreate) ? (
            <Button variant="primary" onClick={() => setForm({ role: null })}>
              <Plus aria-hidden />
              {t.roles.newRole}
            </Button>
          ) : null
        }
      />
      <div className="toolbar">
        <form
          className="w-full sm:w-64"
          role="search"
          onSubmit={(event) => {
            event.preventDefault()
            update({ q: draft.trim() || null })
          }}
        >
          <input
            className="inp"
            type="search"
            aria-label={t.roles.search}
            placeholder={t.roles.search}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
          />
        </form>
      </div>
      {roles.isLoading ? (
        <TableSkeleton columns={[t.roles.name]} label={base.common.loading} />
      ) : roles.isError ? (
        <ScreenState error={roles.error} onRetry={() => void roles.refetch()} />
      ) : rows.length === 0 ? (
        term ? (
          <EmptyState
            icon={<SearchX aria-hidden />}
            title={interpolate(t.roles.noResults, { term })}
            action={
              <Button
                onClick={() => {
                  setDraft('')
                  update({ q: null })
                }}
              >
                {base.common.clearSearch}
              </Button>
            }
          />
        ) : (
          <EmptyState icon={<Shield aria-hidden />} title={t.roles.empty} description={t.roles.emptyBody} />
        )
      ) : (
        <>
          <DataTable
            caption={t.roles.title}
            columns={columns}
            rows={rows}
            rowKey={(role) => role.Id}
            busy={roles.isFetching}
            onRowClick={canDetail ? (role) => navigate({ pathname: `/admin/roles/${role.Id}`, search: params.toString() }) : undefined}
          />
          <Pagination
            page={request.PageNumber}
            pageCount={roles.data?.TotalPageCount ?? 1}
            total={roles.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            onPage={(page) => update({ page: String(page) })}
            onPageSize={(size) => update({ size: String(size) })}
          />
        </>
      )}
      {canDetail && Number.isInteger(openRoleId) && openRoleId > 0 && !form ? (
        <RoleDrawer roleId={openRoleId} onClose={() => navigate({ pathname: '/admin/roles', search: params.toString() })} onEdit={(role) => setForm({ role })} />
      ) : null}
      {form ? <RoleFormModal role={form.role} onClose={() => setForm(null)} /> : null}
    </>
  )
}

/** /admin/roles and /admin/roles/:roleId (the role opens in a drawer over the list). */
export function RolesPage() {
  return (
    <PlatformGate permission={PlatformPermission.RoleList}>
      <RolesList />
    </PlatformGate>
  )
}
