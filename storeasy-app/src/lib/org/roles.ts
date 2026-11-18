import { OrgPermission, OrgRole, type OrgRoleDto } from '@/lib/api/types'
import type { Dictionary } from '@/lib/i18n/en'

// The client copy of Storeasy.Domain OrgRoleRules, used only to shape the UI (which options are offered, which rows
// have actions). The API applies the same rules on every request and answers 403 / 400 when they are broken.

export const ROLES: readonly OrgRole[] = [OrgRole.Owner, OrgRole.Admin, OrgRole.Editor, OrgRole.Viewer]

const ALL = Object.values(OrgPermission) as OrgPermission[]

/** The seeded roles' permission sets (OrgRoleRules matrix); a custom role's set comes from its OrgRoleDto. */
export const SEEDED_PERMISSIONS: Record<Exclude<OrgRole, typeof OrgRole.Custom>, readonly OrgPermission[]> = {
  [OrgRole.Owner]: ALL,
  [OrgRole.Admin]: ALL.filter((permission) => permission !== OrgPermission.ManageOrganization),
  [OrgRole.Editor]: [OrgPermission.View, OrgPermission.EditContent, OrgPermission.ReleaseTest, OrgPermission.ReplyReviews],
  [OrgRole.Viewer]: [OrgPermission.View],
}

/** Permissions a custom role may hold (OrgRoleRules.CustomAssignable): all but the owner only ManageOrganization. */
export const CUSTOM_ASSIGNABLE: readonly OrgPermission[] = ALL.filter((permission) => permission !== OrgPermission.ManageOrganization)

/** A member's (or a role's) access: its role and the permissions it gives. */
export interface Access {
  role: OrgRole
  permissions: readonly OrgPermission[]
}

export function seededAccess(role: OrgRole): Access {
  return { role, permissions: role === OrgRole.Custom ? [OrgPermission.View] : SEEDED_PERMISSIONS[role] }
}

/** The access an organization role gives (seeded or custom). */
export function accessOf(role: Pick<OrgRoleDto, 'Role' | 'Permissions'>): Access {
  return { role: role.Role, permissions: role.Role === OrgRole.Custom ? role.Permissions : seededAccess(role.Role).permissions }
}

function within(inner: Access, outer: Access): boolean {
  return inner.permissions.every((permission) => outer.permissions.includes(permission))
}

/**
 * OrgRoleRules.CanAssign: an owner assigns anything; a member manager neither touches an owner nor makes one, and both the
 * member's current access and the new one must lie within the manager's own permissions.
 */
export function canGrant(actor: Access, current: Access | null, target: Access): boolean {
  if (actor.role === OrgRole.Owner) {
    return true
  }
  if (!actor.permissions.includes(OrgPermission.ManageMembers) || current?.role === OrgRole.Owner || target.role === OrgRole.Owner) {
    return false
  }
  return (current === null || within(current, actor)) && within(target, actor)
}

/** OrgRoleRules.CanRemove (anyone may remove their own membership: leaving). */
export function canRemoveMember(actor: Access, member: Access): boolean {
  if (actor.role === OrgRole.Owner) {
    return true
  }
  return actor.permissions.includes(OrgPermission.ManageMembers) && member.role !== OrgRole.Owner && within(member, actor)
}

/** OrgRoleRules.CanDefine: a custom role never holds a permission its author lacks. */
export function canDefine(actor: Access, permissions: readonly OrgPermission[]): boolean {
  return (
    actor.permissions.includes(OrgPermission.ManageMembers) && permissions.every((permission) => actor.permissions.includes(permission))
  )
}

// Seeded roles only: the role matrix stands in for the permissions.
export function canAssign(actor: OrgRole, current: OrgRole | null, next: OrgRole): boolean {
  return canGrant(seededAccess(actor), current === null ? null : seededAccess(current), seededAccess(next))
}

export function canRemove(actor: OrgRole, member: OrgRole): boolean {
  return canRemoveMember(seededAccess(actor), seededAccess(member))
}

export function roleLabel(role: OrgRole, t: Dictionary, customName?: string | null): string {
  switch (role) {
    case OrgRole.Owner:
      return t.enums.role.owner
    case OrgRole.Admin:
      return t.enums.role.admin
    case OrgRole.Editor:
      return t.enums.role.editor
    case OrgRole.Custom:
      return customName || t.enums.role.custom
    default:
      return t.enums.role.viewer
  }
}

// What a role can do, in one line (design 19.3: every role option carries its short description).
export function roleDescription(role: OrgRole, t: Dictionary): string {
  switch (role) {
    case OrgRole.Owner:
      return t.enums.roleDescription.owner
    case OrgRole.Admin:
      return t.enums.roleDescription.admin
    case OrgRole.Editor:
      return t.enums.roleDescription.editor
    case OrgRole.Custom:
      return t.enums.roleDescription.custom
    default:
      return t.enums.roleDescription.viewer
  }
}

/** A select value for a role choice: "r2" for a seeded role, "c14" for custom role 14. */
export function choiceValue(role: OrgRole, customRoleId?: number | null): string {
  return role === OrgRole.Custom ? `c${customRoleId ?? 0}` : `r${role}`
}

export function parseChoice(value: string): { role: OrgRole; customRoleId: number | null } {
  const id = Number(value.slice(1))
  return value.startsWith('c') ? { role: OrgRole.Custom, customRoleId: id } : { role: id as OrgRole, customRoleId: null }
}
