import { apiGet, apiPost } from '@/lib/api/http'
import type { ChangePlanDto } from '@/features/common/types'
import type { ChromeChangeInput, ChromeItemAddedDto, ChromeItemDto } from './types'

// The Chrome Web Store endpoints (storeasy-api ChromeController, route prefix chrome). item reads storeasy's row and
// the extension packages intake received; every write is a change plan: change is always a dry run and the saved plan is
// approved through plansApi.approve (ChangePlanKind.ChromeChange). chromeContract.test.ts reads this file and checks that
// every endpoint has a handler in test/support/chrome.ts.

export const chromeApi = {
  // The Chrome Web Store API cannot list a publisher's items: an item is added by its extension id.
  addItem: (organizationId: number, connectionId: number, itemId: string, appId: number | null, name: string | null) =>
    apiPost<ChromeItemAddedDto>(
      '/chrome/item',
      { OrganizationId: organizationId, ConnectionId: connectionId, ItemId: itemId, AppId: appId, Name: name },
      { idempotent: true },
    ),

  item: (organizationId: number, storeAppId: number) =>
    apiGet<ChromeItemDto>('/chrome/item', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  change: (organizationId: number, storeAppId: number, change: ChromeChangeInput) =>
    apiPost<ChangePlanDto>('/chrome/change', { OrganizationId: organizationId, StoreAppId: storeAppId, ...change, DryRun: true }),

  sync: (organizationId: number, storeAppId: number) =>
    apiPost<number>('/chrome/sync', { OrganizationId: organizationId, StoreAppId: storeAppId }),
}
