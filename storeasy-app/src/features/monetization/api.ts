import { apiGet, apiPatchList, apiPost } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type { ChangePlanDto } from '@/features/common/types'
import type {
  ConvertedPricesDto,
  IapFilter,
  IapListDto,
  MonetizationChangeDto,
  PriceFilter,
  PricePointDto,
  ProductDetailDto,
  ProductPriceDto,
  SandboxTesterDto,
  ServerNotificationsDto,
  SubscriptionGroupsDto,
  SubscriptionOffersDto,
} from './types'

// The phase 7 endpoints (storeasy-api SubscriptionsController, IapController, PurchaseSettingsController). Reads show the
// last sync; every write is a change plan made as a dry run and approved through plansApi.approve, like every other plan.
// monetizationContract.test.ts pairs every call here with a test handler.

export const subscriptionsApi = {
  groups: (organizationId: number, storeAppId: number) =>
    apiGet<SubscriptionGroupsDto>('/Subscriptions/groups', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  detail: (organizationId: number, subscriptionId: number) =>
    apiGet<ProductDetailDto>('/Subscriptions/subscription', { OrganizationId: organizationId, SubscriptionId: subscriptionId }),

  prices: (organizationId: number, subscriptionId: number, request: ListRequest, filter: PriceFilter) =>
    apiPatchList<ProductPriceDto>('/Subscriptions/prices', {
      OrganizationId: organizationId,
      SubscriptionId: subscriptionId,
      ...filter,
      ...request,
    }),

  pricePoints: (organizationId: number, subscriptionId: number, territory: string) =>
    apiGet<PricePointDto[]>('/Subscriptions/price-points', {
      OrganizationId: organizationId,
      SubscriptionId: subscriptionId,
      Territory: territory,
    }),

  offers: (organizationId: number, subscriptionId: number) =>
    apiGet<SubscriptionOffersDto>('/Subscriptions/offers', { OrganizationId: organizationId, SubscriptionId: subscriptionId }),

  // Play's own conversion of one price to every region; nothing is saved.
  convert: (organizationId: number, storeAppId: number, currency: string, amount: string) =>
    apiPost<ConvertedPricesDto>('/Subscriptions/prices/convert', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      Currency: currency,
      Amount: amount,
    }),

  plan: (organizationId: number, storeAppId: number, changes: MonetizationChangeDto[]) =>
    apiPost<ChangePlanDto>('/Subscriptions/plan', { OrganizationId: organizationId, StoreAppId: storeAppId, Changes: changes, DryRun: true }),

  // Queues a sync of the subscriptions and In-App Purchases; answers the job id.
  sync: (organizationId: number, storeAppId: number) =>
    apiPost<number>('/Subscriptions/sync', { OrganizationId: organizationId, StoreAppId: storeAppId }, { idempotent: true }),
}

export const iapApi = {
  list: (organizationId: number, storeAppId: number, request: ListRequest, filter: IapFilter) =>
    apiPatchList<IapListDto>('/Iap', { OrganizationId: organizationId, StoreAppId: storeAppId, ...filter, ...request }),

  detail: (organizationId: number, iapId: number) =>
    apiGet<ProductDetailDto>('/Iap/product', { OrganizationId: organizationId, IapId: iapId }),

  prices: (organizationId: number, iapId: number, request: ListRequest, filter: PriceFilter) =>
    apiPatchList<ProductPriceDto>('/Iap/prices', { OrganizationId: organizationId, IapId: iapId, ...filter, ...request }),

  pricePoints: (organizationId: number, iapId: number, territory: string) =>
    apiGet<PricePointDto[]>('/Iap/price-points', { OrganizationId: organizationId, IapId: iapId, Territory: territory }),

  plan: (organizationId: number, storeAppId: number, changes: MonetizationChangeDto[]) =>
    apiPost<ChangePlanDto>('/Iap/plan', { OrganizationId: organizationId, StoreAppId: storeAppId, Changes: changes, DryRun: true }),
}

export const purchaseSettingsApi = {
  serverNotifications: (organizationId: number, storeAppId: number) =>
    apiGet<ServerNotificationsDto>('/PurchaseSettings/server-notifications', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  // Null keeps an address as it is; ManageWebhooks (Owner, Admin) plans it.
  planServerNotifications: (
    organizationId: number,
    storeAppId: number,
    input: { Url: string | null; SandboxUrl: string | null; Version: string | null },
  ) =>
    apiPost<ChangePlanDto>('/PurchaseSettings/server-notifications/plan', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      ...input,
      DryRun: true,
    }),

  sandboxTesters: (organizationId: number, storeAppId: number) =>
    apiGet<SandboxTesterDto[]>('/PurchaseSettings/sandbox-testers', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  planSandboxTesters: (organizationId: number, storeAppId: number, changes: MonetizationChangeDto[]) =>
    apiPost<ChangePlanDto>('/PurchaseSettings/sandbox-testers/plan', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      Changes: changes,
      DryRun: true,
    }),
}
