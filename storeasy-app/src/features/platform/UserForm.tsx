import { useMemo, useRef, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Lock } from 'lucide-react'
import { PasswordPolicy } from '@/components/auth/PasswordPolicy'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Button, Checkbox, Field, Input, PasswordInput, RadioGroup, Switch } from '@/components/ui/primitives'
import { apiErrorMessage, ApiError } from '@/lib/api/http'
import { SortType } from '@/lib/api/list'
import { UserType } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { emailError, passwordError } from '@/lib/validation'
import { platformApi, platformKeys } from './api'
import { MAX_ROWS } from './PlatformCommon'
import { usePlatformText, type PlatformDictionary } from './i18n'
import { updated, USER_NAME_MAX, type UpdateUserCommand, type UserDetailDto } from './types'

const ROLE_REQUEST = { PageNumber: 1, RowCount: MAX_ROWS, Sorting: { SortBy: 'Name', Type: SortType.Asc } }

interface Draft {
  type: UserType
  email: string
  name: string
  surname: string
  password: string
  roleIds: number[]
  locked: boolean
}

function initialDraft(user: UserDetailDto | null): Draft {
  return {
    type: user?.UserType ?? UserType.Manager,
    email: user?.UserName ?? '',
    name: user?.Name ?? '',
    surname: user?.Surname ?? '',
    password: '',
    roleIds: (user?.Roles ?? []).map((role) => role.Id),
    locked: user?.IsLocked ?? false,
  }
}

function sameIds(a: number[], b: number[]): boolean {
  return a.length === b.length && a.every((id) => b.includes(id))
}

/** The fields an edit sends: only what changed (UpdateProperty), the role list as a whole (design 17.13). */
export function userUpdateCommand(user: UserDetailDto, draft: Draft): UpdateUserCommand {
  const before = initialDraft(user)
  const command: UpdateUserCommand = { Id: user.Id }
  if (draft.name.trim() !== before.name) {
    command.Name = updated(draft.name.trim() || null)
  }
  if (draft.surname.trim() !== before.surname) {
    command.Surname = updated(draft.surname.trim() || null)
  }
  if (!sameIds(draft.roleIds, before.roleIds)) {
    command.RoleIdList = updated(draft.roleIds)
  }
  if (draft.password) {
    command.NewPassword = updated(draft.password)
  }
  if (draft.locked !== before.locked) {
    command.Lockout = updated(draft.locked)
  }
  return command
}

/** The session-ending changes of an edit, one line each (design 17.14); empty when the save needs no confirmation. */
export function sessionChanges(
  command: UpdateUserCommand,
  user: UserDetailDto,
  roleName: (id: number) => string,
  t: PlatformDictionary,
): string[] {
  const lines: string[] = []
  if (command.RoleIdList) {
    const before = (user.Roles ?? []).map((role) => role.Id)
    const added = command.RoleIdList.Value.filter((id) => !before.includes(id)).map(roleName)
    const removed = before.filter((id) => !command.RoleIdList!.Value.includes(id)).map(roleName)
    const parts = [
      added.length ? interpolate(t.users.sessions.rolesAdded, { names: added.join(', ') }) : null,
      removed.length ? interpolate(t.users.sessions.rolesRemoved, { names: removed.join(', ') }) : null,
    ].filter(Boolean)
    lines.push(interpolate(t.users.sessions.roles, { change: parts.join(', ') }))
  }
  if (command.NewPassword) {
    lines.push(t.users.sessions.password)
  }
  if (command.Lockout?.Value) {
    lines.push(t.users.sessions.lock)
  }
  return lines
}

/** Design 17.12 (create) and 17.13 (edit): a modal form; 409 and validation messages stay above the form. */
export function UserFormModal({ user, onClose, onSaved }: { user: UserDetailDto | null; onClose: () => void; onSaved?: (id: number) => void }) {
  const t = usePlatformText()
  const base = useT()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const errorsRef = useRef<HTMLDivElement>(null)
  const [draft, setDraft] = useState<Draft>(() => initialDraft(user))
  const [touched, setTouched] = useState(false)
  const editing = !!user
  const roles = useQuery({ queryKey: platformKeys.roles(ROLE_REQUEST), queryFn: () => platformApi.roles(ROLE_REQUEST) })
  const roleOptions = useMemo(() => {
    const known = roles.data?.Data ?? []
    // A role of the user that is not in the first page still shows, so it is not dropped by accident.
    const extra = (user?.Roles ?? []).filter((role) => !known.some((item) => item.Id === role.Id))
    return [...known, ...extra.map((role) => ({ Id: role.Id, Name: role.Name ?? `#${role.Id}` }))]
  }, [roles.data, user])
  const roleName = (id: number) => roleOptions.find((role) => role.Id === id)?.Name ?? `#${id}`

  const command = user ? userUpdateCommand(user, draft) : null
  const changeCount = command ? Object.keys(command).length - 1 : 0
  const dirty = editing ? changeCount > 0 : JSON.stringify(draft) !== JSON.stringify(initialDraft(null))

  const errors = {
    email: editing ? null : emailError(draft.email, base),
    password: editing ? (draft.password ? passwordError(draft.password, base) : null) : passwordError(draft.password, base),
    roles: !editing && draft.roleIds.length === 0 ? t.users.form.rolesRequired : null,
  }
  const invalid = Object.values(errors).some(Boolean)

  const save = useMutation({
    meta: { silentError: true },
    mutationFn: async () => {
      if (user && command) {
        return platformApi.updateUser(command)
      }
      const email = draft.email.trim()
      return platformApi.createUser({
        UserType: draft.type,
        UserName: email,
        Email: email,
        Name: draft.name.trim() || null,
        Surname: draft.surname.trim() || null,
        Password: draft.password,
        RoleIdList: draft.roleIds,
        AllowedNotifications: null,
      })
    },
    onSuccess: (id) => {
      showToast(editing ? t.users.saved : t.users.created, 'success')
      void queryClient.invalidateQueries({ queryKey: platformKeys.all })
      onSaved?.(id)
      onClose()
    },
    onError: () => {
      requestAnimationFrame(() => errorsRef.current?.focus())
    },
  })

  const submit = async (event?: FormEvent) => {
    event?.preventDefault()
    setTouched(true)
    if (invalid || (editing && changeCount === 0)) {
      return
    }
    if (user && command) {
      const lines = sessionChanges(command, user, roleName, t)
      if (lines.length > 0) {
        const ok = await confirm({
          title: interpolate(t.users.sessions.title, { name: user.UserName }),
          message: (
            <div className="grid gap-2">
              <p>{t.users.sessions.intro}</p>
              <ul className="list-disc pl-5">
                {lines.map((line) => (
                  <li key={line}>{line}</li>
                ))}
              </ul>
              <p>{t.users.sessions.body}</p>
            </div>
          ),
          confirmLabel: t.users.sessions.confirm,
          danger: true,
        })
        if (!ok) {
          return
        }
      }
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

  const set = <K extends keyof Draft>(key: K, value: Draft[K]) => setDraft((current) => ({ ...current, [key]: value }))
  const failure = save.error
  const failureMessages = failure instanceof ApiError && failure.messages.length > 0 ? failure.messages : [apiErrorMessage(failure, base.common.error)]
  const rolesChanged = editing && !!command?.RoleIdList

  return (
    <Modal
      open
      onClose={() => void close()}
      title={editing ? t.users.form.editTitle : t.users.form.createTitle}
      description={user ? `${user.UserName} · #${user.Id}` : undefined}
      footer={
        <>
          {editing && changeCount > 0 ? (
            <span className="mr-auto text-xs text-fg3">{interpolate(t.common.changeCount, { count: changeCount })}</span>
          ) : null}
          <Button variant="ghost" onClick={() => void close()}>
            {base.common.cancel}
          </Button>
          <Button
            variant="primary"
            loading={save.isPending}
            disabled={(touched && invalid) || (editing && changeCount === 0)}
            onClick={() => void submit()}
          >
            {base.common.save}
          </Button>
        </>
      }
    >
      <form className="grid gap-3" onSubmit={(event) => void submit(event)} noValidate>
        {failure ? (
          <FormErrors ref={errorsRef} title={editing ? t.common.saveFailed : t.users.form.createFailed} messages={failureMessages} />
        ) : null}
        {editing ? (
          <dl className="kv">
            <dt>{t.users.userName}</dt>
            <dd className="inline-flex items-center gap-1.5">
              <Lock className="h-3.5 w-3.5 text-fg3" aria-label={t.users.form.fixedField} />
              {user.UserName}
            </dd>
            <dt>{t.users.type}</dt>
            <dd className="inline-flex items-center gap-1.5">
              <Lock className="h-3.5 w-3.5 text-fg3" aria-label={t.users.form.fixedField} />
              {user.UserType === UserType.Manager ? t.userType.manager : t.userType.appUser}
            </dd>
          </dl>
        ) : (
          <>
            <RadioGroup<UserType>
              name="user-type"
              label={t.users.form.type}
              value={draft.type}
              options={[
                { value: UserType.Manager, label: t.userType.manager, description: t.userType.managerHint },
                { value: UserType.AppUser, label: t.userType.appUser },
              ]}
              onChange={(value) => set('type', value)}
            />
            <Field label={t.users.form.email} hint={t.users.form.emailHint} error={touched ? errors.email : null}>
              <Input type="email" autoComplete="off" value={draft.email} onChange={(event) => set('email', event.target.value)} />
            </Field>
          </>
        )}
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t.users.form.name} optional counter={{ value: draft.name.length, max: USER_NAME_MAX }}>
            <Input maxLength={USER_NAME_MAX} value={draft.name} onChange={(event) => set('name', event.target.value)} />
          </Field>
          <Field label={t.users.form.surname} optional counter={{ value: draft.surname.length, max: USER_NAME_MAX }}>
            <Input maxLength={USER_NAME_MAX} value={draft.surname} onChange={(event) => set('surname', event.target.value)} />
          </Field>
        </div>
        <fieldset className="grid gap-1.5">
          <legend className="lbl mb-1 text-[13px] font-medium">
            {t.users.form.roles} <span className="text-xs text-fg3">· {t.users.form.rolesHint}</span>
          </legend>
          {roleOptions.length === 0 && !roles.isLoading ? (
            <p className="text-[13px] text-fg3">{roles.isError ? apiErrorMessage(roles.error, base.common.error) : t.users.form.noRoleOptions}</p>
          ) : (
            roleOptions.map((role) => (
              <Checkbox
                key={role.Id}
                checked={draft.roleIds.includes(role.Id)}
                onChange={(checked) =>
                  set('roleIds', checked ? [...draft.roleIds, role.Id] : draft.roleIds.filter((id) => id !== role.Id))
                }
              >
                {role.Name}
              </Checkbox>
            ))
          )}
          {touched && errors.roles ? <span className="err-t">{errors.roles}</span> : null}
          {rolesChanged ? <span className="text-xs text-warningText">{t.users.form.rolesChanged}</span> : null}
        </fieldset>
        <Field
          label={editing ? t.users.form.newPassword : t.users.form.password}
          optional={editing}
          hint={editing ? t.users.form.newPasswordHint : undefined}
          error={touched ? errors.password : null}
        >
          <PasswordInput autoComplete="new-password" value={draft.password} onChange={(event) => set('password', event.target.value)} />
        </Field>
        {!editing || draft.password ? <PasswordPolicy value={draft.password} /> : null}
        {editing ? (
          <div className="flex items-start gap-3">
            <Switch checked={draft.locked} onChange={(value) => set('locked', value)} label={t.users.form.lock} />
            <span className="grid gap-0.5">
              <span className="text-[13.5px] font-medium">{t.users.form.lock}</span>
              <span className="text-xs text-fg3">{t.users.form.lockHint}</span>
            </span>
          </div>
        ) : null}
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
