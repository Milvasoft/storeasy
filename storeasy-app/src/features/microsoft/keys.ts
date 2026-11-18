// Query keys of the Microsoft Store screens. Every key starts with 'content' like the phase 2 keys, so an approved change
// plan (PlanActions invalidates contentKeys.all) refreshes these screens too; msKeys.all refreshes only them.
export const msKeys = {
  all: ['content', 'ms'] as const,
  product: (organizationId: number, storeAppId: number) => ['content', 'ms', 'product', organizationId, storeAppId] as const,
  content: (organizationId: number, storeAppId: number, flightId: string | null, addonStoreId: string | null) =>
    ['content', 'ms', 'store-content', organizationId, storeAppId, flightId, addonStoreId] as const,
  submissions: (organizationId: number, storeAppId: number) => ['content', 'ms', 'submissions', organizationId, storeAppId] as const,
  flights: (organizationId: number, storeAppId: number) => ['content', 'ms', 'flights', organizationId, storeAppId] as const,
  addons: (organizationId: number, storeAppId: number) => ['content', 'ms', 'addons', organizationId, storeAppId] as const,
  packages: (organizationId: number, storeAppId: number) => ['content', 'ms', 'packages', organizationId, storeAppId] as const,
  // Not under 'content': connections are organization settings, refreshed with the connection list.
  expiring: (organizationId: number) => ['connections', 'ms-expiring', organizationId] as const,
}
