import type { StoreKind } from '@/lib/api/types'

// The ASO keyword contract (storeasy-api Dtos/AsoDtos). PascalCase like the API's JSON.

export interface AsoCompetitorDto {
  Rank: number
  Id: string
  Name: string | null
  Developer: string | null
  Rating: number | null
  RatingCount: number | null
  IconUrl: string | null
  IsOwnApp: boolean
}

export interface AsoKeywordResearchDto {
  Keyword: string
  Store: StoreKind
  Country: string
  // Estimates 0 to 100 from the store's public data; null when the source did not answer.
  Popularity: number | null
  PopularitySource: string | null
  Difficulty: number | null
  Opportunity: number | null
  CompetingApps: number | null
  // 1 based; null with RankChecked true means "not in the first Depth results".
  CurrentRank: number | null
  RankChecked: boolean
  Depth: number
  Tracked: boolean
  TopCompetitors: AsoCompetitorDto[]
  RelatedTerms: string[]
  PopularityDate: string | null
  ResultsDate: string | null
}

export type AsoSeedSource = 'name' | 'subtitle' | 'keywords' | 'description' | 'competitors' | 'suggestions' | 'ai'

export interface AsoKeywordSeedDto {
  Term: string
  Source: AsoSeedSource
  Cluster: string | null
  Tracked: boolean
}

export interface AsoKeywordSeedsDto {
  Store: StoreKind
  Country: string
  Locale: string | null
  Seeds: AsoKeywordSeedDto[]
  Warning: string | null
  Model: string | null
}

export interface AsoTrackedKeywordDto {
  Id: number
  StoreAppId: number
  Store: StoreKind
  Keyword: string
  Country: string
  TrackCompetitors: boolean
  CompetitorIds: string[]
  TrackedDate: string
  LastCheckedDate: string | null
  CurrentRank: number | null
  // Positive: moved up.
  Change1: number | null
  Change7: number | null
  Change30: number | null
  // The last 30 days, oldest first.
  Sparkline: Array<number | null>
  Depth: number
  Popularity: number | null
  Difficulty: number | null
  Opportunity: number | null
}

export interface AsoRankPointDto {
  Date: string
  Checked: boolean
  Rank: number | null
}

export interface AsoCompetitorSeriesDto {
  Id: string
  Name: string | null
  Points: AsoRankPointDto[]
}

export interface AsoKeywordRanksDto {
  Keyword: AsoTrackedKeywordDto
  From: string
  To: string
  Points: AsoRankPointDto[]
  Competitors: AsoCompetitorSeriesDto[]
  TopCompetitors: AsoCompetitorDto[]
  ResultsDate: string | null
}

export interface AsoKeywordCoverageItemDto {
  Keyword: string
  Covered: boolean
  MissingWords: string[]
  Fields: Array<'name' | 'subtitle' | 'keywords'>
}

export interface AsoKeywordCoverageDto {
  Locale: string
  Name: string | null
  Subtitle: string | null
  Keywords: string | null
  KeywordsLength: number
  Remaining: number
  CharactersNeeded: number
  Items: AsoKeywordCoverageItemDto[]
  RedundantWords: string[]
  MissingWords: string[]
}

export interface TrackKeywordsInput {
  Country: string
  Keywords: string[]
  TrackCompetitors: boolean
  CompetitorIds?: string[]
}
