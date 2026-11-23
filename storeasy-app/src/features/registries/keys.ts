// Query keys of the package registries screens.
export const registriesKeys = {
  all: ['registries'] as const,
  packages: (organizationId: number) => ['registries', 'packages', organizationId] as const,
  package: (organizationId: number, trackedPackageId: number) => ['registries', 'package', organizationId, trackedPackageId] as const,
  setup: (organizationId: number, trackedPackageId: number) => ['registries', 'setup', organizationId, trackedPackageId] as const,
  reminders: (organizationId: number) => ['registries', 'reminders', organizationId] as const,
  downloads: (organizationId: number, trackedPackageId: number) => ['registries', 'downloads', organizationId, trackedPackageId] as const,
  listing: (organizationId: number, trackedPackageId: number) => ['registries', 'listing', organizationId, trackedPackageId] as const,
  groups: (organizationId: number) => ['registries', 'groups', organizationId] as const,
  connections: (organizationId: number) => ['registries', 'connections', organizationId] as const,
  release: (organizationId: number, scope: string) => ['registries', 'release', organizationId, scope] as const,
  plans: (organizationId: number, scope: string) => ['registries', 'plans', organizationId, scope] as const,
}
