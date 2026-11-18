// Reviews contract (storeasy-api Storeasy.Application FeedbackDtos ReviewListDto, SavedReplyDto, Storeasy.Domain
// ReviewReplyState and JsonModels LocalizedText). Keys are PascalCase after pascalizeKeys, enums numeric, null
// properties left out by the API, so every reference typed field is optional.
import type { IsoDateTime, StoreKind } from '@/lib/api/types'

export const ReviewReplyState = { None: 0, Sending: 1, Published: 2, Failed: 3 } as const
export type ReviewReplyState = (typeof ReviewReplyState)[keyof typeof ReviewReplyState]

// Storeasy.Domain JsonModels LocalizedText (saved reply variants, TestFlight "what to test").
export interface LocalizedText {
  Locale?: string
  Text?: string
}

export interface ReviewListDto {
  Id: number
  AppId: number
  AppName?: string
  Store: StoreKind
  StoreAppId: number
  ExternalReviewId?: string
  Rating: number
  Title?: string
  Body?: string
  AuthorName?: string
  Locale?: string
  Territory?: string
  AppVersion?: string
  Device?: string
  ReviewDate: IsoDateTime
  IsRead: boolean
  ReplyBody?: string
  ReplyState: ReviewReplyState
  ReplyDate?: IsoDateTime
  ReplyByUserName?: string
  ReplyError?: string
  ReplyJobId?: number
  // ReviewRules.ReplyMaxLength of the review's store: Google Play 350, App Store 5970.
  ReplyMaxLength: number
  DraftBody?: string
}

export interface SavedReplyDto {
  Id: number
  Name?: string
  Variants?: LocalizedText[]
  // Empty: offered in every app.
  AppIds?: number[]
  UseCount: number
  UpdatedDate?: IsoDateTime
  UpdatedByUserName?: string
}

// GetReviewListQuery filters (a ListRequest besides).
export interface ReviewFilter {
  AppId?: number | null
  Store?: StoreKind | null
  MinRating?: number | null
  MaxRating?: number | null
  Locale?: string | null
  HasReply?: boolean | null
  IsRead?: boolean | null
  Search?: string | null
}

export interface SavedReplyInput {
  Name: string
  Variants: LocalizedText[]
  AppIds: number[]
}
