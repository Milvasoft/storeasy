import type { ListRequest } from '@/lib/api/list'

// Every query key of the app. Shape: [resource, scope, ...args]; invalidate by prefix (queryKeys.members.all drops every
// members query). Organization scoped data carries the organization id right after the scope.
export const queryKeys = {
  account: {
    detail: () => ['account', 'detail'] as const,
  },
  organizations: {
    all: ['organizations'] as const,
    mine: () => ['organizations', 'mine'] as const,
  },
  members: {
    all: ['members'] as const,
    list: (organizationId: number) => ['members', 'list', organizationId] as const,
  },
  connections: {
    all: ['connections'] as const,
    list: (organizationId: number) => ['connections', 'list', organizationId] as const,
  },
  auditLogs: {
    all: ['audit-logs'] as const,
    list: (organizationId: number, request: ListRequest) => ['audit-logs', 'list', organizationId, request] as const,
  },
}
