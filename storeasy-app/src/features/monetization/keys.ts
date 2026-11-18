import type { ListRequest } from '@/lib/api/list'
import type { IapFilter, PriceFilter } from './types'

// Query keys of the phase 7 screens. They start with 'content' like the other store screens, so an approved plan
// (PlanActions invalidates contentKeys.all) refreshes them too.
export const moneyKeys = {
  all: ['content', 'money'] as const,
  groups: (organizationId: number, storeAppId: number) => ['content', 'money', 'groups', organizationId, storeAppId] as const,
  subscription: (organizationId: number, subscriptionId: number) =>
    ['content', 'money', 'subscription', organizationId, subscriptionId] as const,
  prices: (organizationId: number, owner: 'subscription' | 'iap', id: number, request: ListRequest, filter: PriceFilter) =>
    ['content', 'money', 'prices', organizationId, owner, id, request, filter] as const,
  pricePoints: (organizationId: number, owner: 'subscription' | 'iap', id: number, territory: string) =>
    ['content', 'money', 'price-points', organizationId, owner, id, territory] as const,
  offers: (organizationId: number, subscriptionId: number) => ['content', 'money', 'offers', organizationId, subscriptionId] as const,
  iaps: (organizationId: number, storeAppId: number, request: ListRequest, filter: IapFilter) =>
    ['content', 'money', 'iaps', organizationId, storeAppId, request, filter] as const,
  iap: (organizationId: number, iapId: number) => ['content', 'money', 'iap', organizationId, iapId] as const,
  serverNotifications: (organizationId: number, storeAppId: number) =>
    ['content', 'money', 'server-notifications', organizationId, storeAppId] as const,
  sandboxTesters: (organizationId: number, storeAppId: number) =>
    ['content', 'money', 'sandbox-testers', organizationId, storeAppId] as const,
}
