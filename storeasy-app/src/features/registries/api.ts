import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api/http'
import type {
  PackageAlertDto,
  PackageDownloadsDto,
  PackageGroupDto,
  PackageGroupSuggestionDto,
  PackageListingDto,
  PackageRegistry,
  RegistryConnectionDto,
  RegistryConnectionInput,
  RegistryPlanDto,
  RegistryPlanKind,
  ReleaseScope,
  ReleaseSettings,
  ReleaseSettingsDto,
  ReleaseSuggestionDto,
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
// while the Registries feature flag is off. Releases and registry operations are plans: nothing is written until a plan is
// approved with its typed confirmation. registriesContract.test.ts checks every endpoint has a handler in
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

  connections: (organizationId: number) => apiGet<RegistryConnectionDto[]>('/registries/connections', { OrganizationId: organizationId }),

  // The secret goes to storeasy once and is sealed there; it is never sent back.
  saveConnection: (organizationId: number, connection: RegistryConnectionInput) =>
    apiPost<number>('/registries/connection', {
      OrganizationId: organizationId,
      Kind: connection.Kind,
      Secret: connection.Secret,
      ExpiryDate: connection.ExpiryDate || null,
    }),

  removeConnection: (organizationId: number, id: number) =>
    apiDelete<number>('/registries/connection', undefined, { OrganizationId: organizationId, Id: id }),

  release: (organizationId: number, scope: ReleaseScope, prerelease?: string) =>
    apiGet<ReleaseSettingsDto>('/registries/release', {
      OrganizationId: organizationId,
      ...scope,
      ...(prerelease ? { Prerelease: prerelease } : {}),
    }),

  // Saves the settings, or clears them with null.
  saveRelease: (organizationId: number, scope: ReleaseScope, settings: ReleaseSettings | null) =>
    apiPut<number>('/registries/release', { OrganizationId: organizationId, ...scope, Settings: settings }),

  detectRelease: (organizationId: number, scope: ReleaseScope, repository?: string) =>
    apiGet<ReleaseSuggestionDto[]>('/registries/release/detect', {
      OrganizationId: organizationId,
      ...scope,
      ...(repository ? { Repository: repository } : {}),
    }),

  planRelease: (organizationId: number, scope: ReleaseScope, version: string) =>
    apiPost<RegistryPlanDto>('/registries/release/plan', { OrganizationId: organizationId, ...scope, Version: version }),

  planOperation: (organizationId: number, trackedPackageId: number, kind: RegistryPlanKind, version: string, argument?: string) =>
    apiPost<RegistryPlanDto>('/registries/operation/plan', {
      OrganizationId: organizationId,
      TrackedPackageId: trackedPackageId,
      Kind: kind,
      Version: version,
      Argument: argument || null,
    }),

  plans: (organizationId: number, scope: ReleaseScope) =>
    apiGet<RegistryPlanDto[]>('/registries/plans', { OrganizationId: organizationId, ...scope }),

  plan: (organizationId: number, planId: number) =>
    apiGet<RegistryPlanDto>('/registries/plan', { OrganizationId: organizationId, PlanId: planId }),

  approvePlan: (organizationId: number, planId: number, confirm: string) =>
    apiPost<RegistryPlanDto>('/registries/plan/approve', { OrganizationId: organizationId, PlanId: planId, Confirm: confirm }),

  rejectPlan: (organizationId: number, planId: number) =>
    apiPost<RegistryPlanDto>('/registries/plan/reject', { OrganizationId: organizationId, PlanId: planId }),
}
