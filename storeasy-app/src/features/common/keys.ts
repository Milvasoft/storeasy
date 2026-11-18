import type { ListRequest } from '@/lib/api/list'
import type { StoreKind } from '@/lib/api/types'

// Query keys of the phase 2 screens; every key starts with 'content', so an approved plan refreshes all of them at once
// (queryClient.invalidateQueries({ queryKey: contentKeys.all })). Merge into lib/query/keys.ts later.
export const contentKeys = {
  all: ['content'] as const,
  readiness: (organizationId: number, appId: number, store: StoreKind) => ['content', 'readiness', organizationId, appId, store] as const,
  overview: (organizationId: number) => ['content', 'overview', organizationId] as const,
  appCompliance: (organizationId: number, appId: number) => ['content', 'compliance', organizationId, appId] as const,
  orgCompliance: (organizationId: number, openOnly: boolean) => ['content', 'compliance', organizationId, 'org', openOnly] as const,
  rulesCatalog: (organizationId: number) => ['content', 'rules', organizationId, 'catalog'] as const,
  rules: (organizationId: number, filter: object) => ['content', 'rules', organizationId, 'list', filter] as const,
  rule: (organizationId: number, ruleId: string) => ['content', 'rules', organizationId, 'rule', ruleId] as const,
  plans: (organizationId: number, request: ListRequest, status: number | null) =>
    ['content', 'plans', organizationId, request, status] as const,
  plan: (organizationId: number, planId: number) => ['content', 'plan', organizationId, planId] as const,
  listing: (organizationId: number, storeAppId: number, locale: string | null) =>
    ['content', 'listing', organizationId, storeAppId, locale] as const,
  details: (organizationId: number, storeAppId: number) => ['content', 'details', organizationId, storeAppId] as const,
  media: (organizationId: number, storeAppId: number, kind: number, locale: string | null) =>
    ['content', 'media', organizationId, storeAppId, kind, locale] as const,
  categories: (organizationId: number, storeAppId: number) => ['content', 'categories', organizationId, storeAppId] as const,
  ageRating: (organizationId: number, storeAppId: number) => ['content', 'age-rating', organizationId, storeAppId] as const,
  pricing: (organizationId: number, storeAppId: number) => ['content', 'pricing', organizationId, storeAppId] as const,
  pricePoints: (organizationId: number, storeAppId: number, territory: string) =>
    ['content', 'price-points', organizationId, storeAppId, territory] as const,
  reviewInfo: (organizationId: number, storeAppId: number) => ['content', 'review-info', organizationId, storeAppId] as const,
  releases: (organizationId: number, storeAppId: number) => ['content', 'releases', organizationId, storeAppId] as const,
  testers: (organizationId: number, storeAppId: number, track: string) =>
    ['content', 'testers', organizationId, storeAppId, track] as const,
  builds: (organizationId: number, storeAppId: number) => ['content', 'builds', organizationId, storeAppId] as const,
  answers: (organizationId: number, appId: number) => ['content', 'answers', organizationId, appId] as const,
  assisted: (organizationId: number, appId: number) => ['content', 'assisted', organizationId, appId] as const,
  assistedStep: (organizationId: number, stepId: number) => ['content', 'assisted-step', organizationId, stepId] as const,
  otherApps: (organizationId: number) => ['content', 'other-apps', organizationId] as const,
}
