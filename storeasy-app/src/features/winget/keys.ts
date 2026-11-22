// Query keys of the winget screens. They start with 'content' like the phase 2 keys, so an approved change plan
// (PlanActions invalidates contentKeys.all) refreshes these screens too; wingetKeys.all refreshes only them.
export const wingetKeys = {
  all: ['content', 'winget'] as const,
  package: (organizationId: number, storeAppId: number) => ['content', 'winget', 'package', organizationId, storeAppId] as const,
  manifest: (organizationId: number, storeAppId: number, version: string | null) =>
    ['content', 'winget', 'manifest', organizationId, storeAppId, version] as const,
}
