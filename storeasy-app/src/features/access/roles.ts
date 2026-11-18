import { useQuery } from '@tanstack/react-query'
import { organizationsApi } from '@/lib/api/endpoints'
import { OrgPermission, OrgRole, type MyOrganizationDto, type OrgRoleDto } from '@/lib/api/types'
import type { Dictionary } from '@/lib/i18n/en'
import { accessOf, canGrant, choiceValue, roleDescription, roleLabel, type Access } from '@/lib/org/roles'
import type { AccessDictionary } from './i18n'

// Organization roles as the members, invitation and roles screens use them (GET organizations/roles).

export type PermissionName = keyof typeof OrgPermission

/** The vertical permission groups of design 15.13 (never a matrix that grows sideways). */
export const PERMISSION_GROUPS: Array<{ key: keyof AccessDictionary['roles']['groups']; permissions: OrgPermission[] }> = [
  { key: 'view', permissions: [OrgPermission.View] },
  { key: 'content', permissions: [OrgPermission.EditContent, OrgPermission.ReplyReviews] },
  { key: 'release', permissions: [OrgPermission.ReleaseTest, OrgPermission.ReleaseProduction, OrgPermission.SubmitForReview] },
  { key: 'delivery', permissions: [OrgPermission.ManageConnections, OrgPermission.ManageWebhooks] },
  { key: 'organization', permissions: [OrgPermission.ManageMembers, OrgPermission.ManageOrganization] },
]

export const PERMISSION_COUNT = Object.keys(OrgPermission).length

export function permissionName(permission: OrgPermission): PermissionName {
  return (Object.keys(OrgPermission) as PermissionName[]).find((name) => OrgPermission[name] === permission) ?? 'View'
}

export const orgRoleKeys = {
  all: ['org-roles'] as const,
  list: (organizationId: number) => ['org-roles', organizationId] as const,
  settings: (organizationId: number) => ['org-settings', organizationId] as const,
}

export function useOrgRoles(organizationId: number, enabled = true) {
  return useQuery({
    queryKey: orgRoleKeys.list(organizationId),
    queryFn: () => organizationsApi.roles(organizationId),
    enabled,
  })
}

/** The caller's own access in the organization (role and the permissions it gives). */
export function actorAccess(organization: Pick<MyOrganizationDto, 'Role' | 'Permissions'>): Access {
  return { role: organization.Role, permissions: organization.Permissions }
}

/** The roles a choice can name: the seeded ones (from the list, else the fixed four) and the custom ones. */
export function roleChoices(roles: OrgRoleDto[] | undefined): OrgRoleDto[] {
  if (roles && roles.length > 0) {
    return roles
  }
  return [OrgRole.Owner, OrgRole.Admin, OrgRole.Editor, OrgRole.Viewer].map((role) => ({
    Id: null,
    Role: role,
    Name: '',
    Permissions: [...accessOf({ Role: role, Permissions: [] }).permissions],
    MemberCount: 0,
    InvitationCount: 0,
    IsLocked: true,
  }))
}

export function choiceLabel(role: OrgRoleDto, t: Dictionary): string {
  return role.Role === OrgRole.Custom ? role.Name : roleLabel(role.Role, t)
}

export interface RoleOption {
  value: string
  label: string
  description: string
  disabled: boolean
}

/**
 * Select options for giving a role: each one disabled when the actor could not grant it (OrgRoleRules.CanAssign) or,
 * for invitations, when it is Owner (ownership comes by transfer).
 */
export function roleOptions(
  roles: OrgRoleDto[] | undefined,
  actor: Access,
  current: Access | null,
  t: Dictionary,
  options: { ownerAllowed: boolean },
): RoleOption[] {
  return roleChoices(roles).map((role) => ({
    value: choiceValue(role.Role, role.Id),
    label: choiceLabel(role, t),
    description: role.Role === OrgRole.Custom ? role.Description || roleDescription(OrgRole.Custom, t) : roleDescription(role.Role, t),
    disabled: (!options.ownerAllowed && role.Role === OrgRole.Owner) || !canGrant(actor, current, accessOf(role)),
  }))
}

/** The access of a member or invitation, read from the role list (custom roles carry their own set). */
export function accessOfHolder(holder: { Role: OrgRole; CustomRoleId?: number | null }, roles: OrgRoleDto[] | undefined): Access {
  if (holder.Role === OrgRole.Custom) {
    const role = roles?.find((item) => item.Role === OrgRole.Custom && item.Id === holder.CustomRoleId)
    return { role: OrgRole.Custom, permissions: role?.Permissions ?? [OrgPermission.View] }
  }
  return accessOf({ Role: holder.Role, Permissions: [] })
}
