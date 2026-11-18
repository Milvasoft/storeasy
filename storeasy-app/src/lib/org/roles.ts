import { OrgRole } from '@/lib/api/types'
import type { Dictionary } from '@/lib/i18n/en'

// The client copy of Storeasy.Domain OrgRoleRules, used only to shape the UI (which options are offered, which rows
// have actions). The API applies the same rules on every request and answers 403 / 400 when they are broken.

export const ROLES: readonly OrgRole[] = [OrgRole.Owner, OrgRole.Admin, OrgRole.Editor, OrgRole.Viewer]

export function roleLabel(role: OrgRole, t: Dictionary): string {
  switch (role) {
    case OrgRole.Owner:
      return t.enums.role.owner
    case OrgRole.Admin:
      return t.enums.role.admin
    case OrgRole.Editor:
      return t.enums.role.editor
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
    default:
      return t.enums.roleDescription.viewer
  }
}

// OrgRoleRules.CanAssign: an owner assigns any role; an admin neither touches an owner nor makes one.
export function canAssign(actor: OrgRole, current: OrgRole | null, next: OrgRole): boolean {
  if (actor === OrgRole.Owner) {
    return true
  }
  if (actor !== OrgRole.Admin) {
    return false
  }
  return current !== OrgRole.Owner && next !== OrgRole.Owner
}

// OrgRoleRules.CanRemove (anyone may remove their own membership: leaving).
export function canRemove(actor: OrgRole, member: OrgRole): boolean {
  return actor === OrgRole.Owner || (actor === OrgRole.Admin && member !== OrgRole.Owner)
}
