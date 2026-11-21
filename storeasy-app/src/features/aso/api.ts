import { useQuery } from '@tanstack/react-query'
import { apiGet, apiPost } from '@/lib/api/http'
import type { StoreKind } from '@/lib/api/types'
import { isJobActive } from '@/lib/jobs/labels'
import type {
  AsoKeywordCoverageDto,
  AsoKeywordRanksDto,
  AsoKeywordResearchJobDto,
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

  // View. Queues the measuring of up to 20 keywords as a store job and answers its id at once (store requests are
  // paced, so it takes minutes). 400 AsoStoreNotSupported for Microsoft Store; an identical research still on the way
  // answers the same id.
  research: (organizationId: number, appId: number, store: StoreKind, country: string, keywords: string[]) =>
    apiPost<number>('/aso-keywords/research', {
      OrganizationId: organizationId,
      AppId: appId,
      Store: store,
      Country: country,
      Keywords: keywords,
    }),

  // View. A research job with its progress and, once it succeeded, the measurements; without a job id the app's latest
  // research in the store (null when there was none).
  researchJob: (organizationId: number, appId: number, store: StoreKind, jobId: number | null) =>
    apiGet<AsoKeywordResearchJobDto | null>('/aso-keywords/research', {
      OrganizationId: organizationId,
      AppId: appId,
      Store: store,
      ...(jobId ? { JobId: jobId } : {}),
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
  research: (organizationId: number, appId: number, store: StoreKind, jobId: number | null) =>
    ['aso', 'research', organizationId, appId, store, jobId ?? 'latest'] as const,
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

/** How often a running research is read again (the live stream also refreshes it on the job's own events). */
export const RESEARCH_POLL_MS = 3000

/** A research job of the app in the store (the latest one without a job id), read again while it runs. */
export function useResearchJob(organizationId: number, appId: number, store: StoreKind, jobId: number | null) {
  return useQuery({
    queryKey: asoKeys.research(organizationId, appId, store, jobId),
    queryFn: () => asoApi.researchJob(organizationId, appId, store, jobId),
    refetchInterval: (query) => {
      const job = query.state.data
      return job && isJobActive(job.Status) ? RESEARCH_POLL_MS : false
    },
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
