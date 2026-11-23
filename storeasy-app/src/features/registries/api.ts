import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api/http'
import type {
  PackageAlertDto,
  PackageDownloadsDto,
  PackageGroupDto,
  PackageGroupSuggestionDto,
  PackageListingDto,
  PackageRegistry,
  SetupInput,
  TokenReminderDto,
  TokenReminderInput,
  TrackedPackageAddedDto,
  TrackedPackageDto,
  TrackedPackageSummaryDto,
  TrustedPublishingDto,
} from './types'

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

  // The trusted publishing wizard: the saved setup, or a preview of `preview` without saving.
  setup: (organizationId: number, trackedPackageId: number, preview?: SetupInput | null) =>
    apiGet<TrustedPublishingDto>('/registries/setup', {
      OrganizationId: organizationId,
      TrackedPackageId: trackedPackageId,
      ...(preview?.Provider
        ? {
            Provider: preview.Provider,
            Repository: preview.Repository,
            Workflow: preview.Workflow,
            Environment: preview.Environment,
            RegistryUser: preview.RegistryUser,
          }
        : {}),
    }),

  saveSetup: (organizationId: number, trackedPackageId: number, setup: SetupInput) =>
    apiPut<TrustedPublishingDto>('/registries/setup', { OrganizationId: organizationId, TrackedPackageId: trackedPackageId, ...setup }),

  reminders: (organizationId: number) => apiGet<TokenReminderDto[]>('/registries/reminders', { OrganizationId: organizationId }),

  saveReminder: (organizationId: number, reminder: TokenReminderInput) =>
    apiPost<number>('/registries/reminder', { OrganizationId: organizationId, ...reminder }),

  downloads: (organizationId: number, trackedPackageId: number) =>
    apiGet<PackageDownloadsDto>('/registries/downloads', { OrganizationId: organizationId, TrackedPackageId: trackedPackageId }),

  listing: (organizationId: number, trackedPackageId: number) =>
    apiGet<PackageListingDto>('/registries/listing', { OrganizationId: organizationId, TrackedPackageId: trackedPackageId }),

  groups: (organizationId: number) => apiGet<PackageGroupDto[]>('/registries/groups', { OrganizationId: organizationId }),

  groupSuggestions: (organizationId: number) =>
    apiGet<PackageGroupSuggestionDto[]>('/registries/groups/suggestions', { OrganizationId: organizationId }),

  saveGroup: (organizationId: number, id: number | null, name: string, trackedPackageIds: number[]) =>
    apiPost<number>('/registries/group', { OrganizationId: organizationId, Id: id, Name: name, TrackedPackageIds: trackedPackageIds }),

  removeGroup: (organizationId: number, id: number) =>
    apiDelete<number>('/registries/group', undefined, { OrganizationId: organizationId, Id: id }),

  removeReminder: (organizationId: number, id: number) =>
    apiDelete<number>('/registries/reminder', undefined, { OrganizationId: organizationId, Id: id }),
}
