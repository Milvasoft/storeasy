import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LogOut, MoreHorizontal, Search, UserMinus, UserPlus, Users } from 'lucide-react'
import { InviteDialog } from '@/components/members/InviteDialog'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Menu } from '@/components/ui/Menu'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Avatar, Button, EmptyState, IconButton, PageHeader, TableSkeleton } from '@/components/ui/primitives'
import { foldSearch } from '@/components/ui/Select'
import { organizationsApi } from '@/lib/api/endpoints'
import { OrgPermission, OrgRole, type OrganizationInvitationDto, type OrganizationMemberDto, type RoleChoice } from '@/lib/api/types'
import { displayName, initialsOf, useAuth } from '@/lib/auth/AuthContext'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { canGrant, canRemoveMember, choiceValue, parseChoice, roleLabel, seededAccess } from '@/lib/org/roles'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { useAccessText } from '@/features/access/i18n'
import { MembersTabs } from '@/features/access/MembersTabs'
import { accessOfHolder, actorAccess, choiceLabel, orgRoleKeys, roleChoices, roleOptions, useOrgRoles } from '@/features/access/roles'

function memberName(member: OrganizationMemberDto): string {
  return displayName({ name: member.Name, surname: member.Surname, email: member.Email, userName: member.Email ?? '' })
}

/**
 * Members and invitations (design 15.1-15.11). The list is not paged (organizations are small); search runs here. Role
 * changes and removals follow the API's rules (OrgRoleRules); the controls only reflect them, the API decides.
 */
export function MembersPage() {
  const t = useT()
  const access = useAccessText()
  const format = useFormat()
  const confirm = useConfirm()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const organization = useCurrentOrganization()
  const { can, refetch: refetchOrganizations } = useOrganization()
  const { user } = useAuth()
  const [inviting, setInviting] = useState(false)
  const [query, setQuery] = useState('')
  const manage = can(OrgPermission.ManageMembers)
  const actor = actorAccess(organization)

  const members = useQuery({ queryKey: queryKeys.members.list(organization.Id), queryFn: () => organizationsApi.members(organization.Id) })
  const roles = useOrgRoles(organization.Id, manage)

  const choiceName = (choice: RoleChoice) => {
    const role = roleChoices(roles.data).find((item) => choiceValue(item.Role, item.Id) === choiceValue(choice.role, choice.customRoleId))
    return role ? choiceLabel(role, t) : roleLabel(choice.role, t)
  }

  const changeRole = useMutation({
    mutationFn: ({ member, choice }: { member: OrganizationMemberDto; choice: RoleChoice }) =>
      organizationsApi.changeRole(organization.Id, member.Id, choice),
    onSuccess: (_id, { member, choice }) => {
      showToast(interpolate(t.members.roleChanged, { name: memberName(member), role: choiceName(choice) }), 'success')
      void queryClient.invalidateQueries({ queryKey: queryKeys.members.all })
      void queryClient.invalidateQueries({ queryKey: orgRoleKeys.all })
      if (member.UserId === user?.id) {
        void refetchOrganizations()
      }
    },
  })

  const removeMember = useMutation({
    mutationFn: (member: OrganizationMemberDto) => organizationsApi.removeMember(organization.Id, member.Id),
    onSuccess: (_id, member) => {
      if (member.UserId === user?.id) {
        showToast(interpolate(t.members.left, { name: organization.Name }), 'success')
        void refetchOrganizations()
        navigate(HOME_PATH, { replace: true })
        return
      }
      showToast(interpolate(t.members.removed, { name: memberName(member) }), 'success')
      void queryClient.invalidateQueries({ queryKey: queryKeys.members.all })
    },
  })

  const revoke = useMutation({
    mutationFn: (invitation: OrganizationInvitationDto) => organizationsApi.revokeInvitation(organization.Id, invitation.Id),
    onSuccess: (_id, invitation) => {
      showToast(interpolate(t.members.revoked, { email: invitation.Email }), 'success')
      void queryClient.invalidateQueries({ queryKey: queryKeys.members.all })
    },
  })

  const all = useMemo(() => members.data?.Members ?? [], [members.data])
  const ownerCount = all.filter((member) => member.Role === OrgRole.Owner).length
  const needle = foldSearch(query.trim())
  const rows = needle ? all.filter((member) => foldSearch(`${memberName(member)} ${member.Email ?? ''}`).includes(needle)) : all
  const invitations = members.data?.Invitations ?? null

  const askRole = async (member: OrganizationMemberDto, value: string) => {
    if (value === choiceValue(member.Role, member.CustomRoleId)) {
      return
    }
    const choice = parseChoice(value)
    const self = member.UserId === user?.id
    const ok = await confirm({
      title: interpolate(t.members.roleConfirmTitle, { name: memberName(member) }),
      message: (
        <span className="grid gap-2">
          <span>
            {interpolate(t.members.roleConfirmBody, { from: roleLabel(member.Role, t, member.CustomRoleName), to: choiceName(choice) })}
          </span>
          {self ? <b className="text-warningText">{t.members.selfDemote}</b> : null}
        </span>
      ),
      confirmLabel: t.members.changeRole,
    })
    if (ok) {
      changeRole.mutate({ member, choice })
    }
  }

  const askRemove = async (member: OrganizationMemberDto) => {
    const self = member.UserId === user?.id
    if (self && member.Role === OrgRole.Owner && ownerCount <= 1) {
      // The API refuses with OrganizationLastOwner; the dialog says it first (design 15.11).
      await confirm({
        title: t.members.leaveBlockedTitle,
        message: t.members.leaveBlockedBody,
        confirmLabel: t.common.ok,
        cancelLabel: t.common.close,
      })
      return
    }
    const ok = await confirm({
      title: self
        ? interpolate(t.members.leaveTitle, { name: organization.Name })
        : interpolate(t.members.removeTitle, { name: memberName(member) }),
      message: self ? t.members.leaveBody : t.members.removeBody,
      confirmLabel: self ? t.members.leave : t.members.remove,
      danger: true,
    })
    if (ok) {
      removeMember.mutate(member)
    }
  }

  const columns: Column<OrganizationMemberDto>[] = [
    {
      key: 'member',
      header: t.members.member,
      role: 'main',
      cell: (member) => (
        <span className="flex min-w-0 items-center gap-3">
          <Avatar initials={initialsOf(memberName(member))} />
          <span className="min-w-0">
            <b className="block truncate">
              {memberName(member)} {member.UserId === user?.id ? <span className="font-normal text-fg3">({t.members.you})</span> : null}
            </b>
            <span className="block truncate text-[12.5px] text-fg3">{member.Email}</span>
          </span>
        </span>
      ),
    },
    {
      key: 'role',
      header: t.members.role,
      cell: (member) => {
        // The only owner cannot step down (OrganizationLastOwner, design 15.1).
        const lastOwner = member.Role === OrgRole.Owner && ownerCount <= 1
        const current = accessOfHolder(member, roles.data)
        const editable =
          manage && !lastOwner && canGrant(actor, current, member.Role === OrgRole.Owner ? seededAccess(OrgRole.Admin) : current)
        if (!editable) {
          const reason =
            member.Role === OrgRole.Owner && actor.role !== OrgRole.Owner && manage
              ? t.members.ownerLocked
              : lastOwner && manage
                ? t.members.lastOwner
                : null
          return (
            <span className="grid">
              <span>{roleLabel(member.Role, t, member.CustomRoleName)}</span>
              {reason ? <span className="text-xs text-fg3">{reason}</span> : null}
            </span>
          )
        }
        return (
          <span className="block w-44">
            <Select<string>
              aria-label={interpolate(t.members.roleOf, { name: memberName(member) })}
              value={choiceValue(member.Role, member.CustomRoleId)}
              onChange={(value) => void askRole(member, value)}
              disabled={changeRole.isPending}
              options={roleOptions(roles.data, actor, current, t, { ownerAllowed: true }).map((option) => ({
                ...option,
                disabledReason: option.value === choiceValue(OrgRole.Owner) ? t.members.ownerOnlyOwner : access.roles.notHeld,
              }))}
            />
          </span>
        )
      },
    },
    {
      key: 'joined',
      header: t.members.joined,
      role: 'hidePhone',
      cell: (member) => <span className="tabular-nums text-fg2">{format.date(member.JoinDate)}</span>,
    },
    {
      key: 'actions',
      header: '',
      role: 'action',
      cell: (member) => {
        const self = member.UserId === user?.id
        if (!self && !(manage && canRemoveMember(actor, accessOfHolder(member, roles.data)))) {
          return null
        }
        return (
          <Menu
            label={interpolate(t.members.actionsFor, { name: memberName(member) })}
            trigger={
              <IconButton label={interpolate(t.members.actionsFor, { name: memberName(member) })} size="sm" ghost>
                <MoreHorizontal aria-hidden />
              </IconButton>
            }
            entries={[
              self
                ? {
                    key: 'leave',
                    icon: <LogOut aria-hidden />,
                    label: t.members.leave,
                    danger: true,
                    onSelect: () => void askRemove(member),
                  }
                : {
                    key: 'remove',
                    icon: <UserMinus aria-hidden />,
                    label: t.members.remove,
                    danger: true,
                    onSelect: () => void askRemove(member),
                  },
            ]}
          />
        )
      },
    },
  ]

  const invitationColumns: Column<OrganizationInvitationDto>[] = [
    { key: 'email', header: t.fields.email, role: 'main', cell: (invitation) => <b className="break-all">{invitation.Email}</b> },
    { key: 'role', header: t.members.role, cell: (invitation) => roleLabel(invitation.Role, t, invitation.CustomRoleName) },
    {
      key: 'expiry',
      header: t.members.expires,
      cell: (invitation) => {
        const days = Math.ceil((new Date(invitation.ExpiryDate).getTime() - Date.now()) / 86_400_000)
        return (
          <span className="flex flex-wrap items-center gap-2">
            <span className="tabular-nums">{format.date(invitation.ExpiryDate)}</span>
            {days <= 0 ? (
              <Badge tone="danger">{t.members.expired}</Badge>
            ) : days <= 2 ? (
              <Badge tone="warning">{interpolate(t.members.daysLeft, { count: days })}</Badge>
            ) : null}
          </span>
        )
      },
    },
    {
      key: 'actions',
      header: '',
      role: 'action',
      cell: (invitation) => (
        <Button
          size="sm"
          variant="ghost"
          loading={revoke.isPending && revoke.variables?.Id === invitation.Id}
          onClick={async () => {
            if (
              await confirm({
                title: interpolate(t.members.revokeTitle, { email: invitation.Email }),
                message: t.members.revokeBody,
                confirmLabel: t.members.revoke,
                danger: true,
              })
            ) {
              revoke.mutate(invitation)
            }
          }}
        >
          {t.members.revoke}
        </Button>
      ),
    },
  ]

  const inviteButton = manage ? (
    <Button variant="primary" onClick={() => setInviting(true)}>
      <UserPlus aria-hidden />
      {t.members.invite}
    </Button>
  ) : undefined

  return (
    <>
      <PageHeader
        tour="members"
        title={t.members.title}
        subtitle={
          members.data ? interpolate(t.members.summary, { members: all.length, invitations: invitations?.length ?? 0 }) : t.members.subtitle
        }
        actions={inviteButton}
      />
      <MembersTabs members={members.data ? all.length : undefined} />
      {members.isLoading ? (
        <TableSkeleton columns={[t.members.member, t.members.role, t.members.joined]} rows={4} label={t.common.loading} />
      ) : members.isError ? (
        <ScreenState error={members.error} onRetry={() => void members.refetch()} />
      ) : (
        <div className="grid gap-8">
          <section className="grid gap-3" aria-label={t.members.title}>
            {all.length > 5 ? (
              <span className="inp-wrap max-w-sm">
                <Search aria-hidden />
                <input
                  value={query}
                  aria-label={t.members.search}
                  placeholder={t.members.search}
                  onChange={(event) => setQuery(event.target.value)}
                />
              </span>
            ) : null}
            {rows.length === 0 ? (
              <EmptyState
                icon={<Users aria-hidden />}
                title={interpolate(t.common.noResultsFor, { term: query.trim() })}
                action={<Button onClick={() => setQuery('')}>{t.common.clearSearch}</Button>}
              />
            ) : (
              <DataTable
                caption={t.members.title}
                columns={columns}
                rows={rows}
                rowKey={(member) => member.Id}
                busy={changeRole.isPending || removeMember.isPending}
              />
            )}
          </section>
          {invitations ? (
            <section className="grid gap-3">
              <h2 className="h3">
                {t.members.pending} <span className="mono text-sm font-medium text-fg3">{invitations.length}</span>
              </h2>
              {invitations.length === 0 ? (
                <p className="m-0 text-[13px] text-fg3">{t.members.noPending}</p>
              ) : (
                <DataTable
                  caption={t.members.pending}
                  columns={invitationColumns}
                  rows={invitations}
                  rowKey={(invitation) => invitation.Id}
                />
              )}
            </section>
          ) : null}
        </div>
      )}
      {manage ? (
        <InviteDialog
          open={inviting}
          onClose={() => setInviting(false)}
          organizationId={organization.Id}
          actor={actor}
          roles={roles.data}
        />
      ) : null}
    </>
  )
}
