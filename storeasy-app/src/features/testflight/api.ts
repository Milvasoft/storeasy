import { apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type {
  BetaBuildDto,
  BetaGroupDto,
  BetaGroupInput,
  BetaLocalizationDto,
  BetaLocalizationInput,
  BetaTesterDto,
  BetaTesterInvite,
} from './types'

// storeasy-api TestFlightController. Reads come from storeasy's copy (refreshed by the sync job); every write is a store
// job and answers its id.
export const testFlightApi = {
  builds: (organizationId: number, storeAppId: number) =>
    apiGet<BetaBuildDto[]>('/TestFlight/builds', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  groups: (organizationId: number, storeAppId: number) =>
    apiGet<BetaGroupDto[]>('/TestFlight/groups', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  testers: (organizationId: number, storeAppId: number, groupId: number | null, request: ListRequest) =>
    apiPatchList<BetaTesterDto>('/TestFlight/testers', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      GroupId: groupId,
      ...request,
    }),

  localizations: (organizationId: number, storeAppId: number) =>
    apiGet<BetaLocalizationDto[]>('/TestFlight/localizations', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  sync: (organizationId: number, storeAppId: number) =>
    apiPost<number>('/TestFlight/sync', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  createGroup: (organizationId: number, storeAppId: number, input: BetaGroupInput) =>
    apiPost<number>('/TestFlight/group', { OrganizationId: organizationId, StoreAppId: storeAppId, ...input }, { idempotent: true }),

  updateGroup: (
    organizationId: number,
    groupId: number,
    input: {
      Name: string | null
      PublicLinkEnabled: boolean | null
      PublicLinkLimit: number | null
      PublicLinkLimitEnabled: boolean | null
    },
  ) => apiPut<number>('/TestFlight/group', { OrganizationId: organizationId, GroupId: groupId, ...input }),

  updateGroupTesters: (organizationId: number, groupId: number, add: BetaTesterInvite[], remove: number[]) =>
    apiPut<number>('/TestFlight/group/testers', { OrganizationId: organizationId, GroupId: groupId, Add: add, Remove: remove }),

  updateGroupBuilds: (organizationId: number, groupId: number, addBuildIds: number[], removeBuildIds: number[]) =>
    apiPut<number>('/TestFlight/group/builds', {
      OrganizationId: organizationId,
      GroupId: groupId,
      AddBuildIds: addBuildIds,
      RemoveBuildIds: removeBuildIds,
    }),

  updateWhatToTest: (organizationId: number, buildId: number, locale: string, whatsNew: string) =>
    apiPut<number>('/TestFlight/build/what-to-test', {
      OrganizationId: organizationId,
      BuildId: buildId,
      Locale: locale,
      WhatsNew: whatsNew,
    }),

  updateLocalization: (organizationId: number, storeAppId: number, input: BetaLocalizationInput) =>
    apiPut<number>('/TestFlight/localizations', { OrganizationId: organizationId, StoreAppId: storeAppId, ...input }),

  // The typed "version (build)" must match (400 BetaReviewConfirmRequired); 400 BuildExpired / BuildNotReady.
  submitBetaReview: (organizationId: number, buildId: number, confirmVersion: string) =>
    apiPost<number>(
      '/TestFlight/build/beta-review',
      { OrganizationId: organizationId, BuildId: buildId, Confirm: true, ConfirmVersion: confirmVersion },
      { idempotent: true },
    ),
}

export const testFlightKeys = {
  all: ['testflight'] as const,
  builds: (organizationId: number, storeAppId: number) => ['testflight', 'builds', organizationId, storeAppId] as const,
  groups: (organizationId: number, storeAppId: number) => ['testflight', 'groups', organizationId, storeAppId] as const,
  testers: (organizationId: number, storeAppId: number, groupId: number | null, request: ListRequest) =>
    ['testflight', 'testers', organizationId, storeAppId, groupId, request] as const,
  localizations: (organizationId: number, storeAppId: number) => ['testflight', 'localizations', organizationId, storeAppId] as const,
}
