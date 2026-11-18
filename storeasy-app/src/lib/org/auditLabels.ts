import { ActionChannel, AuditAction } from '@/lib/api/types'
import type { Dictionary } from '@/lib/i18n/en'

// Enum labels of the audit log (t.enums.*), translated in one place.

export const AUDIT_ACTIONS: readonly AuditAction[] = Object.values(AuditAction)
export const CHANNELS: readonly ActionChannel[] = Object.values(ActionChannel)

export function auditActionLabel(action: AuditAction, t: Dictionary): string {
  switch (action) {
    case AuditAction.OrganizationCreated:
      return t.enums.auditAction.organizationCreated
    case AuditAction.OrganizationUpdated:
      return t.enums.auditAction.organizationUpdated
    case AuditAction.MemberInvited:
      return t.enums.auditAction.memberInvited
    case AuditAction.InvitationRevoked:
      return t.enums.auditAction.invitationRevoked
    case AuditAction.MemberJoined:
      return t.enums.auditAction.memberJoined
    case AuditAction.MemberRoleChanged:
      return t.enums.auditAction.memberRoleChanged
    case AuditAction.MemberRemoved:
      return t.enums.auditAction.memberRemoved
    case AuditAction.ConnectionAdded:
      return t.enums.auditAction.connectionAdded
    case AuditAction.ConnectionUpdated:
      return t.enums.auditAction.connectionUpdated
    case AuditAction.ConnectionRemoved:
      return t.enums.auditAction.connectionRemoved
    case AuditAction.ConnectionVerified:
      return t.enums.auditAction.connectionVerified
    default:
      return String(action)
  }
}

export function channelLabel(channel: ActionChannel, t: Dictionary): string {
  switch (channel) {
    case ActionChannel.Web:
      return t.enums.channel.web
    case ActionChannel.Mcp:
      return t.enums.channel.mcp
    case ActionChannel.Api:
      return t.enums.channel.api
    default:
      return t.enums.channel.system
  }
}

// Details is free text today; when it is a flat JSON object its fields are shown as a list (design 15.16).
export function parseDetails(details: string | null | undefined): Array<[string, string]> | null {
  if (!details) {
    return null
  }
  try {
    const value: unknown = JSON.parse(details)
    if (!value || typeof value !== 'object' || Array.isArray(value)) {
      return null
    }
    return Object.entries(value as Record<string, unknown>).map(([key, item]) => [
      key,
      typeof item === 'string' ? item : JSON.stringify(item),
    ])
  } catch {
    return null
  }
}
