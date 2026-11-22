// Query keys of the Firefox Add-ons screen. They start with 'content' like the phase 2 keys, so an approved change plan
// (PlanActions invalidates contentKeys.all) refreshes this screen too; firefoxKeys.all refreshes only it.
export const firefoxKeys = {
  all: ['content', 'firefox'] as const,
  addon: (organizationId: number, storeAppId: number) => ['content', 'firefox', 'addon', organizationId, storeAppId] as const,
}
