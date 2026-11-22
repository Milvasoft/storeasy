import { apiGet, apiPost } from '@/lib/api/http'
import type { ChangePlanDto } from '@/features/common/types'
import type { FirefoxAddonAddedDto, FirefoxAddonDto, FirefoxChangeInput } from './types'

// The Firefox Add-ons endpoints (storeasy-api FirefoxController, route prefix firefox). addon reads storeasy's row, a live
// read of the listing and versions, and the .xpi packages intake received; every write is a change plan: change is always a
// dry run and the saved plan is approved through plansApi.approve (ChangePlanKind.FirefoxChange). firefoxContract.test.ts
// reads this file and checks that every endpoint has a handler in test/support/firefox.ts.

export const firefoxApi = {
  // The connection's sync adds every add-on of the account; this adds one by id, guid or slug (an existing app, a co-author).
  addAddon: (organizationId: number, connectionId: number, addon: string, appId: number | null, name: string | null) =>
    apiPost<FirefoxAddonAddedDto>(
      '/firefox/addon',
      { OrganizationId: organizationId, ConnectionId: connectionId, Addon: addon, AppId: appId, Name: name },
      { idempotent: true },
    ),

  addon: (organizationId: number, storeAppId: number) =>
    apiGet<FirefoxAddonDto>('/firefox/addon', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  change: (organizationId: number, storeAppId: number, change: FirefoxChangeInput) =>
    apiPost<ChangePlanDto>('/firefox/change', { OrganizationId: organizationId, StoreAppId: storeAppId, ...change, DryRun: true }),

  sync: (organizationId: number, storeAppId: number) =>
    apiPost<number>('/firefox/sync', { OrganizationId: organizationId, StoreAppId: storeAppId }),
}
