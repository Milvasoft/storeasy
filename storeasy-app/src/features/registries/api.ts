import { apiDelete, apiGet, apiPost } from '@/lib/api/http'
import type { PackageAlertDto, PackageRegistry, TrackedPackageAddedDto, TrackedPackageDto, TrackedPackageSummaryDto } from './types'

// The package registries endpoints (storeasy-api RegistriesController, route prefix registries). Reads answer storeasy's
// rows; add checks the name on the registry and queues the first sync; sync queues a job. The whole module answers 503
// while the Registries feature flag is off. registriesContract.test.ts checks every endpoint has a handler in
// test/support/registries.ts.

export const registriesApi = {
  packages: (organizationId: number) => apiGet<TrackedPackageSummaryDto[]>('/registries/packages', { OrganizationId: organizationId }),

  package: (organizationId: number, trackedPackageId: number) =>
    apiGet<TrackedPackageDto>('/registries/package', { OrganizationId: organizationId, TrackedPackageId: trackedPackageId }),

  alerts: (organizationId: number, includeResolved = false) =>
    apiGet<PackageAlertDto[]>('/registries/alerts', { OrganizationId: organizationId, IncludeResolved: includeResolved }),

  add: (organizationId: number, registry: PackageRegistry, name: string, appId: number | null) =>
    apiPost<TrackedPackageAddedDto>('/registries/package', {
      OrganizationId: organizationId,
      Registry: registry,
      Name: name,
      AppId: appId,
    }),

  remove: (organizationId: number, trackedPackageId: number) =>
    apiDelete<number>('/registries/package', undefined, { OrganizationId: organizationId, TrackedPackageId: trackedPackageId }),

  sync: (organizationId: number, trackedPackageId: number | null) =>
    apiPost<number>('/registries/sync', { OrganizationId: organizationId, TrackedPackageId: trackedPackageId }),
}
