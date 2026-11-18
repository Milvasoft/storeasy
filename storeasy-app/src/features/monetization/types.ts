import type { DateOnly, IsoDateTime, StoreKind } from '@/lib/api/types'
import type { AssistedStepStatus } from '@/features/common/types'

// The phase 7 contract (storeasy-api Dtos/MonetizationDtos, Domain/JsonModels Product*, Domain/Enums), typed field by
// field. Enums travel as numbers; reference fields can be null on the wire.

// ---- Enums ----

export const ProductState = {
  Draft: 1,
  MissingMetadata: 2,
  ReadyToSubmit: 3,
  WaitingForReview: 4,
  InReview: 5,
  ActionNeeded: 6,
  Approved: 7,
  Active: 8,
  Inactive: 9,
  Rejected: 10,
  Removed: 11,
} as const
export type ProductState = (typeof ProductState)[keyof typeof ProductState]

export const PriceSource = { Store: 1, Manual: 2, Converted: 3 } as const
export type PriceSource = (typeof PriceSource)[keyof typeof PriceSource]

export const StoreProductKind = {
  Subscription: 1,
  Consumable: 2,
  NonConsumable: 3,
  NonRenewingSubscription: 4,
  OneTimeProduct: 5,
} as const
export type StoreProductKind = (typeof StoreProductKind)[keyof typeof StoreProductKind]

export const OfferMode = { FreeTrial: 1, PayAsYouGo: 2, PayUpFront: 3 } as const
export type OfferMode = (typeof OfferMode)[keyof typeof OfferMode]

export const OfferKind = { Introductory: 1, Promotional: 2, OfferCode: 3, WinBack: 4, PlayOffer: 5 } as const
export type OfferKind = (typeof OfferKind)[keyof typeof OfferKind]

export const MonetizationChangeKind = {
  CreateGroup: 1,
  UpdateGroup: 2,
  GroupLocalization: 3,
  CreateSubscription: 4,
  UpdateProduct: 5,
  ProductLocalization: 6,
  Level: 7,
  BasePrice: 8,
  TerritoryPrice: 9,
  Availability: 10,
  IntroductoryOffer: 11,
  PromotionalOffer: 12,
  OfferCode: 13,
  WinBackOffer: 14,
  ReviewScreenshot: 15,
  CreateIap: 16,
  PlaySubscription: 17,
  BasePlan: 18,
  BasePlanState: 19,
  BasePlanPrices: 20,
  PlayOffer: 21,
  PlayOfferState: 22,
  OneTimeProduct: 23,
  ServerNotifications: 24,
  SandboxTester: 25,
} as const
export type MonetizationChangeKind = (typeof MonetizationChangeKind)[keyof typeof MonetizationChangeKind]

// ---- Json models ----

export interface ProductLocalization {
  Id?: string | null
  Locale?: string | null
  Name?: string | null
  Description?: string | null
  CustomAppName?: string | null
  Benefits?: string[] | null
  State?: string | null
}

export interface ProductAvailability {
  Id?: string | null
  Territories?: string[] | null
  AvailableInNewTerritories?: boolean | null
}

export interface ProductScreenshot {
  Id?: string | null
  FileName?: string | null
  Width?: number | null
  Height?: number | null
  Checksum?: string | null
  State?: string | null
}

export interface ProductOffer {
  Kind: OfferKind
  Id?: string | null
  Name?: string | null
  Code?: string | null
  Mode?: OfferMode | null
  Duration?: string | null
  Periods?: number | null
  Territories?: string[] | null
  PriceCount?: number | null
  StartDate?: DateOnly | null
  EndDate?: DateOnly | null
  State?: string | null
  Eligibility?: string[] | null
  BasePlanId?: string | null
  CustomCodeCount?: number | null
  OneTimeCodeCount?: number | null
  Phases?: string[] | null
}

// ---- DTOs ----

export interface BasePriceDto {
  Territory?: string | null
  Currency?: string | null
  Amount?: string | null
  Source: PriceSource
}

export interface BasePlanDto {
  BasePlanId?: string | null
  Type?: string | null
  BillingPeriod?: string | null
  State: ProductState
  BasePrice?: BasePriceDto | null
  RegionCount: number
  Offers?: ProductOffer[] | null
}

export interface SubscriptionSummaryDto {
  Id: number
  ProductId?: string | null
  ReferenceName?: string | null
  Level?: number | null
  Duration?: string | null
  State: ProductState
  StoreState?: string | null
  BasePrice?: BasePriceDto | null
  HasReviewScreenshot: boolean
}

export interface SubscriptionGroupDto {
  Id: number
  ExternalId?: string | null
  ReferenceName?: string | null
  Localizations?: ProductLocalization[] | null
  Subscriptions?: SubscriptionSummaryDto[] | null
}

export interface PlaySubscriptionDto {
  Id: number
  ProductId?: string | null
  State: ProductState
  Listings?: ProductLocalization[] | null
  BasePlans?: BasePlanDto[] | null
}

export interface SubscriptionGroupsDto {
  StoreAppId: number
  Store: StoreKind
  SyncedDate?: IsoDateTime | null
  Groups: SubscriptionGroupDto[]
  PlaySubscriptions: PlaySubscriptionDto[]
}

export interface ProductDetailDto {
  Id: number
  StoreAppId: number
  Store: StoreKind
  ExternalId?: string | null
  Kind: StoreProductKind
  ProductId?: string | null
  ReferenceName?: string | null
  GroupId?: number | null
  Duration?: string | null
  Level?: number | null
  FamilySharable?: boolean | null
  State: ProductState
  StoreState?: string | null
  Localizations?: ProductLocalization[] | null
  Availability?: ProductAvailability | null
  TerritoryCount: number
  BasePrice?: BasePriceDto | null
  ReviewScreenshot?: ProductScreenshot | null
  ReviewNote?: string | null
  SubmitWithVersion: boolean
  RetentionMessage?: string | null
  MultiSeatStatus?: string | null
  Plans?: BasePlanDto[] | null
  OfferCount: number
  SyncedDate: IsoDateTime
}

export interface ProductPriceDto {
  Territory?: string | null
  TerritoryName?: string | null
  Currency?: string | null
  CustomerPrice?: string | null
  Proceeds?: string | null
  PricePointId?: string | null
  Source: PriceSource
  IsBase: boolean
  StartDate?: DateOnly | null
  HasPendingChange: boolean
  PendingCustomerPrice?: string | null
  PendingStartDate?: DateOnly | null
}

export interface PricePointDto {
  Id?: string | null
  Territory?: string | null
  Currency?: string | null
  CustomerPrice?: string | null
  Proceeds?: string | null
}

export interface SubscriptionOffersDto {
  SubscriptionId: number
  Introductory: ProductOffer[]
  Promotional: ProductOffer[]
  OfferCodes: ProductOffer[]
  WinBack: ProductOffer[]
  PlayOffers: ProductOffer[]
}

export interface ConvertedPriceDto {
  RegionCode?: string | null
  Currency?: string | null
  Amount?: string | null
}

export interface ConvertedPricesDto {
  Prices: ConvertedPriceDto[]
  OtherRegionsUsd?: string | null
  OtherRegionsEur?: string | null
  RegionsVersion?: string | null
  ConvertedDate: IsoDateTime
}

export interface IapListDto {
  Id: number
  Store: StoreKind
  ProductId?: string | null
  ReferenceName?: string | null
  Kind: StoreProductKind
  State: ProductState
  StoreState?: string | null
  BasePrice?: BasePriceDto | null
  HasReviewScreenshot: boolean
  PurchaseOptions?: BasePlanDto[] | null
}

export interface ServerNotificationsDto {
  StoreAppId: number
  Store: StoreKind
  Url?: string | null
  Version?: string | null
  SandboxUrl?: string | null
  SandboxVersion?: string | null
  Assisted: boolean
  AssistedStepKey?: string | null
  AssistedStepId?: number | null
  AssistedStepStatus?: AssistedStepStatus | null
}

export interface SandboxTesterDto {
  Id?: string | null
  FirstName?: string | null
  LastName?: string | null
  AccountName?: string | null
  Territory?: string | null
  InterruptPurchases?: boolean | null
  SubscriptionRenewalRate?: string | null
  ApplePayCompatible?: boolean | null
}

// ---- Inputs ----

export interface LocalizationInputDto {
  Locale: string
  Name?: string | null
  Description?: string | null
  CustomAppName?: string | null
  Benefits?: string[] | null
}

export interface PriceInputDto {
  Territory: string
  PricePointId?: string | null
  Currency?: string | null
  Amount?: string | null
  CustomerPrice?: string | null
}

export interface OfferPhaseInputDto {
  Duration: string
  Recurrence: number
  Free: boolean
  Prices?: PriceInputDto[] | null
}

// MonetizationChangeDto: which fields are read depends on Kind; the planner fills the store ids itself.
export interface MonetizationChangeDto {
  Kind: MonetizationChangeKind
  SubscriptionId?: number | null
  ProductId?: string | null
  ProductKind?: StoreProductKind | null
  GroupId?: number | null
  GroupReferenceName?: string | null
  ReferenceName?: string | null
  Locale?: string | null
  Name?: string | null
  Description?: string | null
  CustomAppName?: string | null
  Benefits?: string[] | null
  Localizations?: LocalizationInputDto[] | null
  Duration?: string | null
  Level?: number | null
  FamilySharable?: boolean | null
  ReviewNote?: string | null
  MultiSeatStatus?: string | null
  Territory?: string | null
  PricePointId?: string | null
  StartDate?: DateOnly | null
  EndDate?: DateOnly | null
  PreserveCurrentPrice?: boolean | null
  Equalize?: boolean
  Prices?: PriceInputDto[] | null
  Territories?: string[] | null
  AvailableInNewTerritories?: boolean | null
  Mode?: OfferMode | null
  Periods?: number | null
  Code?: string | null
  Eligibility?: string[] | null
  OfferEligibility?: string | null
  AutoRenew?: boolean | null
  CustomCodeCount?: number | null
  OneTimeCodeCount?: number | null
  CodeExpiryDate?: DateOnly | null
  Active?: boolean | null
  PaidMonths?: number | null
  SinceMinMonths?: number | null
  SinceMaxMonths?: number | null
  WaitMonths?: number | null
  Priority?: string | null
  AssetId?: number | null
  BasePlanId?: string | null
  AutoRenewing?: boolean | null
  Phases?: OfferPhaseInputDto[] | null
  OtherRegionsUsd?: string | null
  OtherRegionsEur?: string | null
  PurchaseOptionId?: string | null
  Url?: string | null
  SandboxUrl?: string | null
  NotificationVersion?: string | null
  TesterId?: string | null
  InterruptPurchases?: boolean | null
  RenewalRate?: string | null
  ClearPurchaseHistory?: boolean | null
  RegionsVersion?: string | null
}

// GetSubscriptionPricesQuery / GetIapPricesQuery filters (beside the ListRequest).
export interface PriceFilter {
  PlanId?: string | null
  TerritoryName?: string | null
  Source?: PriceSource | null
  HasPendingChange?: boolean | null
}

// GetIapListQuery filters.
export interface IapFilter {
  Search?: string | null
  Kind?: StoreProductKind | null
  State?: ProductState | null
}
