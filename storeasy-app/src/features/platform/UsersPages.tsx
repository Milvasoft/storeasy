import { useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Pencil, Plus, SearchX, Trash2, Users } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Chip, EmptyState, IconButton, PageHeader, TableSkeleton } from '@/components/ui/primitives'
import { FilterType, SortType, type FilterCriteria, type ListRequest } from '@/lib/api/list'
import { UserType } from '@/lib/api/types'
import { useAuth } from '@/lib/auth/AuthContext'
import { useGlobalPermissions } from '@/lib/auth/globalPermissions'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { platformApi, platformKeys } from './api'
import { activityLabel } from './ActivityLogsPage'
import { AuditCard, PlatformGate, PlatformPermission, SystemRecordBadge, userTypeLabel } from './PlatformCommon'
import { UserFormModal } from './UserForm'
import { usePlatformText } from './i18n'
import { isSeedRecord, updated, UserActivity, type UserDetailDto, type UserListDto } from './types'

// Design 17.9-17.15: the installation's accounts (Manager and AppUser together). The user name is the e-mail (K11).

const DEFAULT_PAGE_SIZE = 20
const ALL = 'all'
type SearchField = 'UserName' | 'Name'

function numberParam(value: string | null): number | null {
  const parsed = Number(value)
  return value && Number.isInteger(parsed) ? parsed : null
}

/** The list request from the address: one search box on a chosen field (Contains), the type (EqualTo), locked ones. */
export function usersRequest(params: URLSearchParams, now: Date = new Date()): ListRequest {
  const criterias: FilterCriteria[] = []
  const term = params.get('q')?.trim()
  const field: SearchField = params.get('field') === 'Name' ? 'Name' : 'UserName'
  const type = numberParam(params.get('type'))
  if (term) {
    criterias.push({ FilterBy: field, Value: term, Type: FilterType.Contains })
  }
  if (type !== null) {
    criterias.push({ FilterBy: 'UserType', Value: type, Type: FilterType.EqualTo })
  }
  if (params.get('locked') === '1') {
    criterias.push({ FilterBy: 'LockoutEnd', Value: now.toISOString(), Type: FilterType.GreaterThan })
  }
  return {
    PageNumber: numberParam(params.get('page')) ?? 1,
    RowCount: numberParam(params.get('size')) ?? DEFAULT_PAGE_SIZE,
    Sorting: { SortBy: 'UserName', Type: SortType.Asc },
    ...(criterias.length > 0 ? { Filtering: { Criterias: criterias } } : {}),
  }
}

function fullName(user: { Name?: string | null; Surname?: string | null }): string {
  return [user.Name, user.Surname].filter(Boolean).join(' ') || '-'
}

function LockBadge({ user }: { user: UserListDto }) {
  const t = usePlatformText()
  const format = useFormat()
  if (!user.IsLocked) {
    return <span className="text-fg3">-</span>
  }
  return (
    <span className="grid gap-0.5">
      <Badge tone="warning" small dot>
        {t.users.locked}
      </Badge>
      <span className="text-xs text-fg3">{interpolate(t.users.lockedUntil, { time: format.time(user.LockoutEnd) })}</span>
    </span>
  )
}

/** Delete with the confirmation of design 17.15; resolves true when the user was deleted. */
function useDeleteUser() {
  const t = usePlatformText()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const remove = useMutation({
    mutationFn: (userId: number) => platformApi.deleteUser(userId),
    onSuccess: () => {
      showToast(t.users.deleted, 'success')
      void queryClient.invalidateQueries({ queryKey: platformKeys.all })
    },
  })
  return async (user: { Id: number; UserName: string }): Promise<boolean> => {
    const ok = await confirm({
      title: t.users.remove.title,
      message: interpolate(t.users.remove.body, { name: user.UserName }),
      confirmLabel: t.users.remove.confirm,
      danger: true,
    })
    if (!ok) {
      return false
    }
    try {
      await remove.mutateAsync(user.Id)
      return true
    } catch {
      return false
    }
  }
}

function UsersList() {
  const t = usePlatformText()
  const base = useT()
  const navigate = useNavigate()
  const { user: me } = useAuth()
  const global = useGlobalPermissions()
  const [params, setParams] = useSearchParams()
  const [draft, setDraft] = useState(params.get('q') ?? '')
  const [form, setForm] = useState<{ user: UserDetailDto | null } | null>(null)
  const [editId, setEditId] = useState<number | null>(null)
  const deleteUser = useDeleteUser()
  const request = usersRequest(params)
  const users = useQuery({
    queryKey: platformKeys.users(request),
    queryFn: () => platformApi.users(request),
    placeholderData: keepPreviousData,
  })
  const editing = useQuery({
    queryKey: platformKeys.user(editId ?? 0),
    queryFn: () => platformApi.user(editId ?? 0),
    enabled: editId !== null,
  })

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

  const field: SearchField = params.get('field') === 'Name' ? 'Name' : 'UserName'
  const term = params.get('q')?.trim() ?? ''
  const typeFilter = params.get('type')
  const lockedOnly = params.get('locked') === '1'
  const filterCount = [typeFilter, lockedOnly ? '1' : null].filter(Boolean).length
  const canEdit = global.has(PlatformPermission.UserUpdate)
  const canDelete = global.has(PlatformPermission.UserDelete)
  const canDetail = global.has(PlatformPermission.UserDetail)

  const columns: Column<UserListDto>[] = [
    {
      key: 'userName',
      header: t.users.userName,
      role: 'main',
      cell: (user) => (
        <span className="grid gap-0.5">
          <span className="flex flex-wrap items-center gap-1.5">
            {canDetail ? (
              <Link className="lnk font-semibold" to={`/admin/users/${user.Id}`} onClick={(event) => event.stopPropagation()}>
                {user.UserName}
              </Link>
            ) : (
              <b>{user.UserName}</b>
            )}
            {isSeedRecord(user.Id) ? <SystemRecordBadge /> : null}
          </span>
          <span className="text-xs text-fg3">
            #{user.Id}
            {user.Id === me?.id ? ` · ${t.common.you}` : ''}
          </span>
        </span>
      ),
    },
    { key: 'name', header: t.users.fullName, cell: (user) => fullName(user) },
    { key: 'type', header: t.users.type, cell: (user) => <Badge small>{userTypeLabel(user.UserType, t)}</Badge> },
    { key: 'lock', header: t.users.lock, cell: (user) => <LockBadge user={user} /> },
    {
      key: 'actions',
      header: t.common.actions,
      role: 'action',
      className: 'w-24 text-right',
      cell: (user) =>
        isSeedRecord(user.Id) ? (
          <span className="text-xs text-fg3">{t.common.readOnly}</span>
        ) : (
          <span className="inline-flex gap-1" onClick={(event) => event.stopPropagation()}>
            {canEdit ? (
              <IconButton label={`${t.common.edit}: ${user.UserName}`} size="sm" ghost onClick={() => setEditId(user.Id)}>
                <Pencil aria-hidden />
              </IconButton>
            ) : null}
            {canDelete ? (
              <IconButton label={`${t.common.delete}: ${user.UserName}`} size="sm" ghost onClick={() => void deleteUser(user)}>
                <Trash2 aria-hidden />
              </IconButton>
            ) : null}
          </span>
        ),
    },
  ]

  const rows = users.data?.Data ?? []
  const typeOptions = [
    { value: ALL, label: base.common.all },
    { value: String(UserType.Manager), label: t.userType.manager },
    { value: String(UserType.AppUser), label: t.userType.appUser },
  ]

  return (
    <>
      <PageHeader
        title={t.users.title}
        subtitle={t.users.subtitle}
        actions={
          global.has(PlatformPermission.UserCreate) ? (
            <Button variant="primary" onClick={() => setForm({ user: null })}>
              <Plus aria-hidden />
              {t.users.newUser}
            </Button>
          ) : null
        }
      />
      <div className="toolbar">
        <span className="w-[calc(50%-4px)] sm:w-36">
          <Select<SearchField>
            aria-label={t.users.searchIn}
            value={field}
            options={[
              { value: 'UserName', label: t.users.searchUserName },
              { value: 'Name', label: t.users.searchName },
            ]}
            onChange={(value) => update({ field: value === 'Name' ? 'Name' : null })}
          />
        </span>
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
            aria-label={field === 'Name' ? t.users.searchNamePlaceholder : t.users.searchPlaceholder}
            placeholder={field === 'Name' ? t.users.searchNamePlaceholder : t.users.searchPlaceholder}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
          />
        </form>
        <span className="w-[calc(50%-4px)] sm:w-40">
          <Select
            aria-label={t.users.type}
            value={typeFilter ?? ALL}
            options={typeOptions}
            onChange={(value) => update({ type: value === ALL ? null : value })}
          />
        </span>
        <Chip selected={lockedOnly} onClick={() => update({ locked: lockedOnly ? null : '1' })}>
          {t.users.lockedOnly}
        </Chip>
        {filterCount > 0 ? (
          <>
            <span className="text-xs text-fg3">{interpolate(t.users.activeFilters, { count: filterCount })}</span>
            <Button size="sm" variant="ghost" onClick={() => update({ type: null, locked: null })}>
              {base.common.clearFilters}
            </Button>
          </>
        ) : null}
      </div>
      {users.isLoading ? (
        <TableSkeleton columns={[t.users.userName, t.users.fullName, t.users.type, t.users.lock]} label={base.common.loading} />
      ) : users.isError ? (
        <ScreenState error={users.error} onRetry={() => void users.refetch()} />
      ) : rows.length === 0 ? (
        term ? (
          <EmptyState
            icon={<SearchX aria-hidden />}
            title={interpolate(t.users.noResults, { term })}
            description={interpolate(t.users.noResultsBody, { field: field === 'Name' ? t.users.searchName : t.users.searchUserName })}
            action={
              <Button
                onClick={() => {
                  setDraft('')
                  update({ q: null })
                }}
              >
                {t.users.clearSearch}
              </Button>
            }
          />
        ) : filterCount > 0 ? (
          <EmptyState
            icon={<SearchX aria-hidden />}
            title={t.users.noFilterResults}
            description={t.users.noFilterResultsBody}
            action={<Button onClick={() => update({ type: null, locked: null })}>{base.common.clearFilters}</Button>}
          />
        ) : (
          <EmptyState icon={<Users aria-hidden />} title={t.users.empty} description={t.users.emptyBody} />
        )
      ) : (
        <>
          <DataTable
            caption={t.users.title}
            columns={columns}
            rows={rows}
            rowKey={(user) => user.Id}
            busy={users.isFetching}
            onRowClick={canDetail ? (user) => navigate(`/admin/users/${user.Id}`) : undefined}
          />
          <Pagination
            page={request.PageNumber}
            pageCount={users.data?.TotalPageCount ?? 1}
            total={users.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            onPage={(page) => update({ page: String(page) })}
            onPageSize={(size) => update({ size: String(size) })}
          />
        </>
      )}
      {form ? <UserFormModal user={form.user} onClose={() => setForm(null)} onSaved={(id) => navigate(`/admin/users/${id}`)} /> : null}
      {editId !== null && editing.data ? (
        <UserFormModal user={editing.data} onClose={() => setEditId(null)} />
      ) : null}
    </>
  )
}

/** /admin/users. */
export function UsersPage() {
  return (
    <PlatformGate permission={PlatformPermission.UserList}>
      <UsersList />
    </PlatformGate>
  )
}

// ---- Detail ---------------------------------------------------------------------------------------------------------

function RecentActivity({ userId }: { userId: number }) {
  const t = usePlatformText()
  const format = useFormat()
  const request: ListRequest = {
    PageNumber: 1,
    RowCount: 5,
    Sorting: { SortBy: 'ActivityDate', Type: SortType.Desc },
    Filtering: {
      Criterias: [
        { FilterBy: 'RelatedId', Value: String(userId), Type: FilterType.EqualTo },
        { FilterBy: 'Activity', Value: UserActivity.UpdateUser, Type: FilterType.EqualTo },
      ],
    },
  }
  const logs = useQuery({ queryKey: platformKeys.activityLogs(request), queryFn: () => platformApi.activityLogs(request) })
  const rows = logs.data?.Data ?? []
  return (
    <section className="card grid gap-2 p-4" aria-label={t.users.detail.recent}>
      <div className="flex items-center gap-2">
        <h2 className="mr-auto text-[15px] font-semibold">{t.users.detail.recent}</h2>
        <Link className="lnk text-[13px]" to={`/admin/activity-logs?related=${userId}`}>
          {t.users.detail.seeAll}
        </Link>
      </div>
      {logs.isLoading ? (
        <CardSkeleton count={1} />
      ) : rows.length === 0 ? (
        <p className="text-[13px] text-fg3">{t.users.detail.noRecent}</p>
      ) : (
        <ul className="grid gap-2">
          {rows.map((log) => (
            <li key={log.Id} className="grid gap-0.5 text-[13px]">
              <b>{activityLabel(log.Activity, t)}</b>
              <span className="text-xs text-fg3">
                {[log.UserName, log.RelatedData, format.dateTime(log.ActivityDate)].filter(Boolean).join(' · ')}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function UserDetail() {
  const t = usePlatformText()
  const format = useFormat()
  const navigate = useNavigate()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const global = useGlobalPermissions()
  const { userId: rawId = '' } = useParams()
  const userId = Number(rawId)
  const [editing, setEditing] = useState(false)
  const deleteUser = useDeleteUser()
  const detail = useQuery({
    queryKey: platformKeys.user(userId),
    queryFn: () => platformApi.user(userId),
    enabled: Number.isInteger(userId) && userId > 0,
  })
  const lock = useMutation({
    mutationFn: (value: boolean) => platformApi.updateUser({ Id: userId, Lockout: updated(value) }),
    onSuccess: (_id, value) => {
      showToast(value ? t.users.lockedDone : t.users.unlocked, 'success')
      void queryClient.invalidateQueries({ queryKey: platformKeys.all })
    },
  })

  const back = (
    <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to="/admin/users">
      <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
      {t.users.title}
    </Link>
  )
  if (detail.isLoading) {
    return <CardSkeleton />
  }
  if (detail.isError || !detail.data) {
    return <ScreenState error={detail.error} onRetry={() => void detail.refetch()} back={back} />
  }
  const user = detail.data
  const seed = isSeedRecord(user.Id)
  const canEdit = !seed && global.has(PlatformPermission.UserUpdate)
  const canDelete = !seed && global.has(PlatformPermission.UserDelete)
  const roles = user.Roles ?? []

  const lockNow = async () => {
    const ok = await confirm({
      title: interpolate(t.users.lockConfirm.title, { name: user.UserName }),
      message: interpolate(t.users.lockConfirm.body, { name: user.UserName }),
      confirmLabel: t.users.lockConfirm.confirm,
      danger: true,
    })
    if (ok) {
      lock.mutate(true)
    }
  }

  return (
    <>
      {back}
      <PageHeader
        title={user.UserName}
        badge={
          <span className="flex flex-wrap items-center gap-1.5">
            <span className="text-fg3">#{user.Id}</span>
            <Badge small>{userTypeLabel(user.UserType, t)}</Badge>
            {user.IsLocked ? (
              <Badge tone="warning" small dot>
                {interpolate(t.users.lockedUntil, { time: format.time(user.LockoutEnd) })}
              </Badge>
            ) : null}
            {seed ? <SystemRecordBadge /> : null}
          </span>
        }
        actions={
          <>
            {canEdit ? (
              <Button onClick={() => setEditing(true)}>
                <Pencil aria-hidden />
                {t.common.edit}
              </Button>
            ) : null}
            {canDelete ? (
              <Button
                variant="danger-outline"
                onClick={() =>
                  void deleteUser(user).then((deleted) => {
                    if (deleted) {
                      navigate('/admin/users')
                    }
                  })
                }
              >
                <Trash2 aria-hidden />
                {t.common.delete}
              </Button>
            ) : null}
          </>
        }
      />
      {seed ? <p className="mb-3 text-[13px] text-fg3">{t.users.detail.systemBody}</p> : null}
      <div className="grid gap-3 lg:grid-cols-2">
        <section className="card grid gap-2 p-4" aria-label={t.users.detail.identity}>
          <h2 className="text-[15px] font-semibold">{t.users.detail.identity}</h2>
          <dl className="kv">
            <dt>{t.users.userName}</dt>
            <dd>{user.UserName}</dd>
            <dt>{t.users.detail.email}</dt>
            <dd>{user.Email || '-'}</dd>
            <dt>{t.users.detail.name}</dt>
            <dd>{user.Name || '-'}</dd>
            <dt>{t.users.detail.surname}</dt>
            <dd>{user.Surname || '-'}</dd>
            <dt>{t.users.type}</dt>
            <dd>{userTypeLabel(user.UserType, t)}</dd>
          </dl>
        </section>
        <section className="card grid content-start gap-3 p-4" aria-label={t.users.detail.security}>
          <h2 className="text-[15px] font-semibold">{t.users.detail.security}</h2>
          <div className="grid gap-1">
            <b className="text-[13.5px]">{user.IsLocked ? t.users.detail.lockedTitle : t.users.lock}</b>
            <p className="text-[13px] text-fg2">
              {user.IsLocked
                ? interpolate(t.users.detail.lockedBody, { time: format.dateTime(user.LockoutEnd) })
                : t.users.detail.notLocked}
            </p>
            {canEdit ? (
              <span>
                {user.IsLocked ? (
                  <Button size="sm" loading={lock.isPending} onClick={() => lock.mutate(false)}>
                    {t.users.detail.unlock}
                  </Button>
                ) : (
                  <Button size="sm" variant="danger-outline" loading={lock.isPending} onClick={() => void lockNow()}>
                    {t.users.detail.lock}
                  </Button>
                )}
              </span>
            ) : null}
          </div>
          <div className="grid gap-1">
            <b className="text-[13.5px]">{t.users.detail.password}</b>
            <p className="text-[13px] text-fg2">{t.users.detail.passwordBody}</p>
            {canEdit ? (
              <span>
                <Button size="sm" onClick={() => setEditing(true)}>
                  {t.users.detail.resetPassword}
                </Button>
              </span>
            ) : null}
          </div>
        </section>
        <section className="card grid content-start gap-2 p-4" aria-label={t.users.detail.roles}>
          <h2 className="text-[15px] font-semibold">{t.users.detail.roles}</h2>
          {roles.length === 0 ? (
            <p className="text-[13px] text-fg3">{t.users.detail.noRoles}</p>
          ) : (
            <span className="flex flex-wrap gap-1.5">
              {roles.map((role) =>
                global.has(PlatformPermission.RoleDetail) ? (
                  <Link key={role.Id} className="bdg sm lnk" to={`/admin/roles/${role.Id}`}>
                    {role.Name ?? `#${role.Id}`}
                  </Link>
                ) : (
                  <Badge key={role.Id} small>
                    {role.Name ?? `#${role.Id}`}
                  </Badge>
                ),
              )}
            </span>
          )}
        </section>
        {global.has(PlatformPermission.ActivityLogList) ? <RecentActivity userId={user.Id} /> : null}
        <AuditCard audit={user.AuditInfo} />
      </div>
      {editing ? <UserFormModal user={user} onClose={() => setEditing(false)} /> : null}
    </>
  )
}

/** /admin/users/:userId. */
export function UserDetailPage() {
  return (
    <PlatformGate permission={PlatformPermission.UserDetail}>
      <UserDetail />
    </PlatformGate>
  )
}
