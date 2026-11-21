import { useQuery } from '@tanstack/react-query'
import { apiGet, apiPost } from '@/lib/api/http'
import type { StoreKind } from '@/lib/api/types'
import type {
  AsoKeywordCoverageDto,
  AsoKeywordRanksDto,
  AsoKeywordResearchDto,
  AsoKeywordSeedsDto,
  AsoTrackedKeywordDto,
  TrackKeywordsInput,
} from './types'

// The ASO keyword calls (AsoKeywordsController) and their query hooks. Reads need View; track, untrack and AI ideas need
// EditContent.
export const asoApi = {
  // View. Active keywords of the app in the store, with rank, change and the last 30 days.
  tracked: (organizationId: number, appId: number, store: StoreKind) =>
    apiGet<AsoTrackedKeywordDto[]>('/aso-keywords', { OrganizationId: organizationId, AppId: appId, Store: store }),

  // View. Up to 20 keywords; 503 AsoSourceUnavailable when the store did not answer, 400 AsoStoreNotSupported for
  // Microsoft Store. Store calls are paced, so a first research can take a while.
  research: (organizationId: number, appId: number, store: StoreKind, country: string, keywords: string[]) =>
    apiPost<AsoKeywordResearchDto[]>('/aso-keywords/research', {
      OrganizationId: organizationId,
      AppId: appId,
      Store: store,
      Country: country,
      Keywords: keywords,
    }),

  // View. Candidates from the listing, competitors' names and the store's suggestions.
  seeds: (organizationId: number, appId: number, store: StoreKind, country: string) =>
    apiGet<AsoKeywordSeedsDto>('/aso-keywords/seeds', { OrganizationId: organizationId, AppId: appId, Store: store, Country: country }),

  // EditContent. AI ideas (candidates only); 409 AiKeyNotSet without a key.
  ideas: (organizationId: number, appId: number, store: StoreKind, country: string, seeds: string[]) =>
    apiPost<AsoKeywordSeedsDto>('/aso-keywords/seeds/suggest', {
      OrganizationId: organizationId,
      AppId: appId,
      Store: store,
      Country: country,
      Seeds: seeds,
    }),

  // EditContent. 409 AsoKeywordLimitReached or AsoGooglePlayRanksDisabled.
  track: (organizationId: number, appId: number, store: StoreKind, input: TrackKeywordsInput) =>
    apiPost<AsoTrackedKeywordDto[]>('/aso-keywords/track', { OrganizationId: organizationId, AppId: appId, Store: store, ...input }),

  // EditContent. The history is kept.
  untrack: (organizationId: number, ids: number[]) =>
    apiPost<number>('/aso-keywords/untrack', { OrganizationId: organizationId, Ids: ids }),

  // View.
  ranks: (organizationId: number, id: number, days: number) =>
    apiGet<AsoKeywordRanksDto>('/aso-keywords/ranks', { OrganizationId: organizationId, Id: id, Days: days }),

  // View. App Store only.
  coverage: (organizationId: number, appId: number) =>
    apiGet<AsoKeywordCoverageDto[]>('/aso-keywords/coverage', { OrganizationId: organizationId, AppId: appId }),
}

export const asoKeys = {
  all: ['aso'] as const,
  tracked: (organizationId: number, appId: number, store: StoreKind) => ['aso', 'tracked', organizationId, appId, store] as const,
  ranks: (organizationId: number, id: number, days: number) => ['aso', 'ranks', organizationId, id, days] as const,
  coverage: (organizationId: number, appId: number) => ['aso', 'coverage', organizationId, appId] as const,
}

export function useTrackedKeywords(organizationId: number, appId: number, store: StoreKind, enabled = true) {
  return useQuery({
    queryKey: asoKeys.tracked(organizationId, appId, store),
    queryFn: () => asoApi.tracked(organizationId, appId, store),
    enabled,
  })
}

export function useKeywordRanks(organizationId: number, id: number, days: number) {
  return useQuery({
    queryKey: asoKeys.ranks(organizationId, id, days),
    queryFn: () => asoApi.ranks(organizationId, id, days),
    enabled: id > 0,
  })
}

export function useKeywordCoverage(organizationId: number, appId: number, enabled: boolean) {
  return useQuery({
    queryKey: asoKeys.coverage(organizationId, appId),
    queryFn: () => asoApi.coverage(organizationId, appId),
    enabled,
    staleTime: 60_000,
  })
}

/** The keywords typed in a box: one per line or comma separated, trimmed, without duplicates (case insensitive). */
export function parseKeywords(text: string): string[] {
  const seen = new Set<string>()
  const result: string[] = []
  for (const part of text.split(/[\n,]/)) {
    const keyword = part.trim().replace(/\s+/g, ' ')
    const key = keyword.toLocaleLowerCase()
    if (keyword && !seen.has(key)) {
      seen.add(key)
      result.push(keyword)
    }
  }
  return result
}
