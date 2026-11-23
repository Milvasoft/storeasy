// Query keys of the package registries screens.
export const registriesKeys = {
  all: ['registries'] as const,
  packages: (organizationId: number) => ['registries', 'packages', organizationId] as const,
  package: (organizationId: number, trackedPackageId: number) => ['registries', 'package', organizationId, trackedPackageId] as const,
  setup: (organizationId: number, trackedPackageId: number) => ['registries', 'setup', organizationId, trackedPackageId] as const,
  reminders: (organizationId: number) => ['registries', 'reminders', organizationId] as const,
}
