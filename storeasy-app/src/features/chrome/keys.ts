// Query keys of the Chrome Web Store screen. They start with 'content' like the phase 2 keys, so an approved change plan
// (PlanActions invalidates contentKeys.all) refreshes this screen too; chromeKeys.all refreshes only it.
export const chromeKeys = {
  all: ['content', 'chrome'] as const,
  item: (organizationId: number, storeAppId: number) => ['content', 'chrome', 'item', organizationId, storeAppId] as const,
}
