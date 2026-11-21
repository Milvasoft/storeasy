import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import type {
  AsoKeywordCoverageDto,
  AsoKeywordRanksDto,
  AsoKeywordResearchDto,
  AsoKeywordSeedsDto,
  AsoTrackedKeywordDto,
} from '@/features/aso/types'
import { make } from './factories'
import { endpoint, envelope, errorResponse } from './handlers'
import { server } from './server'

// The ASO keywords fake backend: builders, the data the handlers answer with, and one handler per endpoint of
// src/features/aso/api.ts (asoContract.test.ts checks the pairing).

export function trackedKeyword(overrides: Partial<AsoTrackedKeywordDto> = {}): AsoTrackedKeywordDto {
  return make<AsoTrackedKeywordDto>(
    {
      Id: 501,
      StoreAppId: 201,
      Store: StoreKind.Apple,
      Keyword: 'pusula',
      Country: 'tr',
      TrackCompetitors: true,
      CompetitorIds: ['3001', '3002'],
      TrackedDate: '2026-11-01T09:00:00Z',
      LastCheckedDate: '2026-11-21T08:20:00Z',
      CurrentRank: 8,
      Change1: 4,
      Change7: -2,
      Change30: null,
      Sparkline: [...Array.from({ length: 27 }, () => null), 14, 12, 8],
      Depth: 200,
      Popularity: 62,
      Difficulty: 41,
      Opportunity: 61,
    },
    overrides,
  )
}

export function researchItem(overrides: Partial<AsoKeywordResearchDto> = {}): AsoKeywordResearchDto {
  return make<AsoKeywordResearchDto>(
    {
      Keyword: 'kıble pusulası',
      Store: StoreKind.Apple,
      Country: 'tr',
      Popularity: 71,
      PopularitySource: 'apple-search-hints',
      Difficulty: 35,
      Opportunity: 68,
      CompetingApps: 184,
      CurrentRank: null,
      RankChecked: true,
      Depth: 200,
      Tracked: false,
      TopCompetitors: [
        { Rank: 1, Id: '3001', Name: 'Kıble Bul', Developer: 'Dev', Rating: 4.7, RatingCount: 52000, IconUrl: null, IsOwnApp: false },
        { Rank: 2, Id: '3002', Name: 'Namaz Vakti', Developer: 'Dev', Rating: 4.5, RatingCount: 8000, IconUrl: null, IsOwnApp: false },
      ],
      RelatedTerms: ['kıble pusulası ücretsiz'],
      PopularityDate: '2026-11-21T10:00:00Z',
      ResultsDate: '2026-11-21T10:00:00Z',
    },
    overrides,
  )
}

export function keywordRanks(overrides: Partial<AsoKeywordRanksDto> = {}): AsoKeywordRanksDto {
  return make<AsoKeywordRanksDto>(
    {
      Keyword: trackedKeyword(),
      From: '2026-11-15',
      To: '2026-11-21',
      Points: [
        { Date: '2026-11-15', Checked: false, Rank: null },
        { Date: '2026-11-16', Checked: true, Rank: null },
        { Date: '2026-11-17', Checked: true, Rank: 30 },
        { Date: '2026-11-18', Checked: true, Rank: 20 },
        { Date: '2026-11-19', Checked: true, Rank: 14 },
        { Date: '2026-11-20', Checked: true, Rank: 12 },
        { Date: '2026-11-21', Checked: true, Rank: 8 },
      ],
      Competitors: [{ Id: '3001', Name: 'Kıble Bul', Points: [{ Date: '2026-11-21', Checked: true, Rank: 1 }] }],
      TopCompetitors: [
        { Rank: 1, Id: '3001', Name: 'Kıble Bul', Developer: 'Dev', Rating: 4.7, RatingCount: 52000, IconUrl: null, IsOwnApp: false },
        { Rank: 8, Id: '201', Name: 'Pusula', Developer: 'Kuzey Lab', Rating: 4.2, RatingCount: 310, IconUrl: null, IsOwnApp: true },
      ],
      ResultsDate: '2026-11-21T08:20:00Z',
    },
    overrides,
  )
}

export function coverage(overrides: Partial<AsoKeywordCoverageDto> = {}): AsoKeywordCoverageDto {
  return make<AsoKeywordCoverageDto>(
    {
      Locale: 'tr',
      Name: 'Pusula: Kıble Bulucu',
      Subtitle: 'GPS ve harita',
      Keywords: 'pusula,kompas,yön bulma,harita',
      KeywordsLength: 30,
      Remaining: 70,
      CharactersNeeded: 5,
      Items: [
        { Keyword: 'pusula', Covered: true, MissingWords: [], Fields: ['name', 'keywords'] },
        { Keyword: 'kıble yönü', Covered: false, MissingWords: ['yönü'], Fields: ['name'] },
      ],
      RedundantWords: ['pusula', 'harita'],
      MissingWords: ['yönü'],
    },
    overrides,
  )
}

interface AsoState {
  tracked: AsoTrackedKeywordDto[]
  research: AsoKeywordResearchDto[]
  seeds: AsoKeywordSeedsDto
  ideas: AsoKeywordSeedsDto
  ranks: AsoKeywordRanksDto
  coverage: AsoKeywordCoverageDto[]
}

function initialAso(): AsoState {
  return {
    tracked: [trackedKeyword()],
    research: [researchItem()],
    seeds: {
      Store: StoreKind.Apple,
      Country: 'tr',
      Locale: 'tr',
      Seeds: [
        { Term: 'pusula', Source: 'name', Cluster: null, Tracked: true },
        { Term: 'kıble pusulası', Source: 'suggestions', Cluster: null, Tracked: false },
      ],
      Warning: null,
      Model: null,
    },
    ideas: {
      Store: StoreKind.Apple,
      Country: 'tr',
      Locale: 'tr',
      Seeds: [{ Term: 'yön bulucu', Source: 'ai', Cluster: 'Yön', Tracked: false }],
      Warning: null,
      Model: 'claude-haiku-5-5',
    },
    ranks: keywordRanks(),
    coverage: [coverage()],
  }
}

export const asoState: AsoState = initialAso()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

export const asoHandlers: HttpHandler[] = [
  endpoint('get', '/aso-keywords', () => json(envelope(asoState.tracked))),
  endpoint('post', '/aso-keywords/research', ({ body }) => {
    const input = body as { Keywords: string[] }
    return json(
      envelope(asoState.research.filter((item) => input.Keywords.some((keyword) => keyword.toLocaleLowerCase('tr') === item.Keyword))),
    )
  }),
  endpoint('get', '/aso-keywords/seeds', () => json(envelope(asoState.seeds))),
  endpoint('post', '/aso-keywords/seeds/suggest', () => json(envelope(asoState.ideas))),
  endpoint('post', '/aso-keywords/track', ({ body }) => {
    const input = body as { Keywords: string[]; Country: string }
    if (asoState.tracked.length + input.Keywords.length > 3) {
      return errorResponse('AsoKeywordLimitReached', 'Organizasyonunuz en fazla 3 anahtar kelime takip edebilir.', 409)
    }
    asoState.tracked = [
      ...asoState.tracked,
      ...input.Keywords.map((keyword, index) =>
        trackedKeyword({
          Id: 600 + index,
          Keyword: keyword,
          Country: input.Country.toLowerCase(),
          LastCheckedDate: null,
          CurrentRank: null,
          Sparkline: [],
        }),
      ),
    ]
    return json(envelope(asoState.tracked))
  }),
  endpoint('post', '/aso-keywords/untrack', ({ body }) => {
    const ids = (body as { Ids: number[] }).Ids
    asoState.tracked = asoState.tracked.filter((item) => !ids.includes(item.Id))
    return json(envelope(ids.length))
  }),
  endpoint('get', '/aso-keywords/ranks', () => json(envelope(asoState.ranks))),
  endpoint('get', '/aso-keywords/coverage', () => json(envelope(asoState.coverage))),
]

/** Resets the ASO data and answers its endpoints for every test of the calling file. */
export function setupAsoApi(): void {
  beforeEach(() => {
    Object.assign(asoState, initialAso())
    server.use(...asoHandlers)
  })
}
