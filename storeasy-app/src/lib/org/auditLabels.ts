import { ActionChannel, AuditAction } from '@/lib/api/types'
import type { Dictionary } from '@/lib/i18n/en'

// Enum labels of the audit log (t.enums.*), translated in one place.

export const AUDIT_ACTIONS: readonly AuditAction[] = Object.values(AuditAction)

const AUDIT_ACTION_NAMES: ReadonlyMap<AuditAction, string> = new Map(
  Object.entries(AuditAction).map(([name, value]) => [value, name] as [AuditAction, string]),
)
export const CHANNELS: readonly ActionChannel[] = Object.values(ActionChannel)

export function auditActionLabel(action: AuditAction, t: Dictionary): string {
  const name = AUDIT_ACTION_NAMES.get(action)
  const key = name ? ((name[0].toLowerCase() + name.slice(1)) as keyof Dictionary['enums']['auditAction']) : undefined
  return (key && t.enums.auditAction[key]) || String(action)
}

export function channelLabel(channel: ActionChannel, t: Dictionary): string {
  switch (channel) {
    case ActionChannel.Web:
      return t.enums.channel.web
    case ActionChannel.Mcp:
      return t.enums.channel.mcp
    case ActionChannel.Api:
      return t.enums.channel.api
    case ActionChannel.Ci:
      return t.enums.channel.ci
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
