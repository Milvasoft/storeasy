import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { AssistedStepStatus, ChangePlanKind, type ChangePlanDto } from '@/features/common/types'
import {
  OfferKind,
  OfferMode,
  PriceSource,
  ProductState,
  StoreProductKind,
  type ConvertedPricesDto,
  type IapListDto,
  type PricePointDto,
  type ProductDetailDto,
  type ProductPriceDto,
  type SandboxTesterDto,
  type ServerNotificationsDto,
  type SubscriptionGroupsDto,
  type SubscriptionOffersDto,
} from '@/features/monetization/types'
import { changePlan, contentHandlers } from './content'
import { make } from './factories'
import { endpoint, envelope, listEnvelope } from './handlers'
import { server } from './server'

// The phase 7 fake backend: DTO builders typed with src/features/monetization/types.ts (the shapes of storeasy-api
// MonetizationDtos), the data the handlers answer with, and one handler per endpoint of src/features/monetization/api.ts
// (monetizationContract.test.ts checks the pairing). A test file calls setupMonetizationApi() once.

// ---- Builders ----------------------------------------------------------------------------------------------------------
export function subscriptionDetail(overrides: Partial<ProductDetailDto> = {}): ProductDetailDto {
  return make<ProductDetailDto>(
    {
      Id: 3101,
      StoreAppId: 201,
      Store: StoreKind.Apple,
      ExternalId: '6740001001',
      Kind: StoreProductKind.Subscription,
      ProductId: 'dev.kuzeylab.pusula.plus.monthly',
      ReferenceName: 'Pusula Plus Aylık',
      GroupId: 3001,
      Duration: 'ONE_MONTH',
      Level: 2,
      FamilySharable: false,
      State: ProductState.MissingMetadata,
      StoreState: 'MISSING_METADATA',
      Localizations: [
        { Id: 'l-1', Locale: 'tr', Name: 'Pusula Plus Aylık', Description: 'Çevrimdışı haritalar.', State: 'PREPARE_FOR_SUBMISSION' },
      ],
      Availability: { Id: 'av-1', Territories: ['TUR', 'USA', 'DEU'], AvailableInNewTerritories: true },
      TerritoryCount: 3,
      BasePrice: { Territory: 'TUR', Currency: 'TRY', Amount: '49.99', Source: PriceSource.Store },
      ReviewScreenshot: null,
      ReviewNote: 'Ayarlar > Pusula Plus.',
      SubmitWithVersion: true,
      Plans: [],
      OfferCount: 1,
      SyncedDate: '2026-11-18T09:20:00Z',
    },
    overrides,
  )
}

export function productPrice(overrides: Partial<ProductPriceDto> = {}): ProductPriceDto {
  return make<ProductPriceDto>(
    {
      Territory: 'TUR',
      TerritoryName: 'Türkiye',
      Currency: 'TRY',
      CustomerPrice: '49.99',
      Proceeds: '34.99',
      PricePointId: 'pp-tur-49',
      Source: PriceSource.Store,
      IsBase: true,
      StartDate: null,
      HasPendingChange: false,
    },
    overrides,
  )
}

export function iapItem(overrides: Partial<IapListDto> = {}): IapListDto {
  return make<IapListDto>(
    {
      Id: 4101,
      Store: StoreKind.Apple,
      ProductId: 'dev.kuzeylab.pusula.theme.night',
      ReferenceName: 'Gece teması',
      Kind: StoreProductKind.NonConsumable,
      State: ProductState.ReadyToSubmit,
      StoreState: 'READY_TO_SUBMIT',
      BasePrice: { Territory: 'TUR', Currency: 'TRY', Amount: '29.99', Source: PriceSource.Store },
      HasReviewScreenshot: true,
      PurchaseOptions: [],
    },
    overrides,
  )
}

/** A monetization plan as the dry run answers it. */
export function moneyPlan(overrides: Partial<ChangePlanDto> = {}): ChangePlanDto {
  return changePlan({
    PlanId: 7701,
    Kind: ChangePlanKind.Monetization,
    Title: 'Subscriptions and In-App Purchases: 1 changes',
    StoreAppId: 201,
    VersionId: undefined,
    VersionString: undefined,
    Items: [{ Group: 'Product', Locale: 'tr', Field: 'name', Before: 'Pusula Plus Aylık', After: 'Pusula Plus', Action: 2 }],
    Warnings: [],
    Calls: [{ Method: 'PATCH', Path: '/v2/subscriptionLocalizations', Target: 'dev.kuzeylab.pusula.plus.monthly tr' }],
    ...overrides,
  })
}

// ---- Data --------------------------------------------------------------------------------------------------------------
export interface MoneyState {
  groups: SubscriptionGroupsDto
  playGroups: SubscriptionGroupsDto
  subscription: ProductDetailDto
  prices: ProductPriceDto[]
  pricePoints: PricePointDto[]
  offers: SubscriptionOffersDto
  converted: ConvertedPricesDto
  iaps: IapListDto[]
  iap: ProductDetailDto
  notifications: ServerNotificationsDto
  playNotifications: ServerNotificationsDto
  testers: SandboxTesterDto[]
  plan: ChangePlanDto
}

function initialMoney(): MoneyState {
  return {
    groups: {
      StoreAppId: 201,
      Store: StoreKind.Apple,
      SyncedDate: '2026-11-18T09:20:00Z',
      Groups: [
        {
          Id: 3001,
          ExternalId: '21058734',
          ReferenceName: 'Pusula Plus',
          Localizations: [{ Id: 'gl-1', Locale: 'tr', Name: 'Pusula Plus', CustomAppName: null, State: 'PREPARE_FOR_SUBMISSION' }],
          Subscriptions: [
            {
              Id: 3101,
              ProductId: 'dev.kuzeylab.pusula.plus.monthly',
              ReferenceName: 'Pusula Plus Aylık',
              Level: 2,
              Duration: 'ONE_MONTH',
              State: ProductState.MissingMetadata,
              BasePrice: { Territory: 'TUR', Currency: 'TRY', Amount: '49.99', Source: PriceSource.Store },
              HasReviewScreenshot: false,
            },
            {
              Id: 3102,
              ProductId: 'dev.kuzeylab.pusula.plus.yearly',
              ReferenceName: 'Pusula Plus Yıllık',
              Level: 1,
              Duration: 'ONE_YEAR',
              State: ProductState.Approved,
              BasePrice: { Territory: 'TUR', Currency: 'TRY', Amount: '399.99', Source: PriceSource.Store },
              HasReviewScreenshot: true,
            },
          ],
        },
      ],
      PlaySubscriptions: [],
    },
    playGroups: {
      StoreAppId: 202,
      Store: StoreKind.Google,
      SyncedDate: '2026-11-18T09:20:00Z',
      Groups: [],
      PlaySubscriptions: [
        {
          Id: 5101,
          ProductId: 'pusula_premium',
          State: ProductState.Active,
          Listings: [{ Locale: 'tr-TR', Name: 'Pusula Premium', Benefits: ['Çevrimdışı haritalar'] }],
          BasePlans: [
            {
              BasePlanId: 'aylik',
              Type: 'AutoRenewing',
              BillingPeriod: 'P1M',
              State: ProductState.Active,
              BasePrice: { Territory: 'TR', Currency: 'TRY', Amount: '54.99', Source: PriceSource.Manual },
              RegionCount: 2,
              Offers: [
                {
                  Kind: OfferKind.PlayOffer,
                  Id: 'ilk-hafta',
                  Code: 'ilk-hafta',
                  BasePlanId: 'aylik',
                  Phases: ['FreeTrial P1W x1'],
                  Eligibility: ['NewCustomers'],
                  State: 'ACTIVE',
                },
              ],
            },
            {
              BasePlanId: 'prepaid-1ay',
              Type: 'Prepaid',
              BillingPeriod: 'P1M',
              State: ProductState.Draft,
              BasePrice: null,
              RegionCount: 0,
              Offers: [],
            },
          ],
        },
      ],
    },
    subscription: subscriptionDetail(),
    prices: [
      productPrice(),
      productPrice({ Territory: 'USA', TerritoryName: 'ABD', Currency: 'USD', CustomerPrice: '1.49', Proceeds: '1.04', IsBase: false }),
      productPrice({
        Territory: 'DEU',
        TerritoryName: 'Almanya',
        Currency: 'EUR',
        CustomerPrice: '1.99',
        Proceeds: '1.17',
        Source: PriceSource.Manual,
        IsBase: false,
        HasPendingChange: true,
        PendingCustomerPrice: '2.49',
        PendingStartDate: '2026-12-01',
      }),
    ],
    pricePoints: [
      { Id: 'pp-deu-199', Territory: 'DEU', Currency: 'EUR', CustomerPrice: '1.99', Proceeds: '1.17' },
      { Id: 'pp-deu-249', Territory: 'DEU', Currency: 'EUR', CustomerPrice: '2.49', Proceeds: '1.47' },
    ],
    offers: {
      SubscriptionId: 3101,
      Introductory: [
        {
          Kind: OfferKind.Introductory,
          Id: 'io-1',
          Mode: OfferMode.FreeTrial,
          Duration: 'ONE_WEEK',
          Periods: 1,
          Territories: ['TUR', 'USA'],
          State: 'ACTIVE',
        },
      ],
      Promotional: [],
      OfferCodes: [
        {
          Kind: OfferKind.OfferCode,
          Id: 'oc-1',
          Name: 'Lansman',
          Code: 'PUSULA2027',
          Mode: OfferMode.FreeTrial,
          Duration: 'ONE_MONTH',
          Periods: 1,
          Eligibility: ['NEW'],
          CustomCodeCount: 500,
          OneTimeCodeCount: 1000,
          State: 'True',
        },
      ],
      WinBack: [],
      PlayOffers: [],
    },
    converted: {
      Prices: [
        { RegionCode: 'TR', Currency: 'TRY', Amount: '59.99' },
        { RegionCode: 'US', Currency: 'USD', Amount: '1.99' },
        { RegionCode: 'IN', Currency: 'INR', Amount: '119' },
      ],
      OtherRegionsUsd: '1.99',
      OtherRegionsEur: '1.99',
      RegionsVersion: '2022/02',
      ConvertedDate: '2026-11-18T11:31:00Z',
    },
    iaps: [
      iapItem(),
      iapItem({
        Id: 4102,
        ProductId: 'dev.kuzeylab.pusula.goal.5',
        ReferenceName: 'Ek hedef hakkı',
        Kind: StoreProductKind.Consumable,
        State: ProductState.MissingMetadata,
        HasReviewScreenshot: false,
      }),
    ],
    iap: subscriptionDetail({
      Id: 4101,
      Kind: StoreProductKind.NonConsumable,
      ProductId: 'dev.kuzeylab.pusula.theme.night',
      ReferenceName: 'Gece teması',
      GroupId: null,
      Duration: null,
      Level: null,
      State: ProductState.ReadyToSubmit,
      StoreState: 'READY_TO_SUBMIT',
      ReviewScreenshot: { Id: 'rs-1', FileName: 'tema-gece.png', Width: 1290, Height: 2796, Checksum: 'abc', State: 'COMPLETE' },
      SubmitWithVersion: false,
      OfferCount: 0,
    }),
    notifications: {
      StoreAppId: 201,
      Store: StoreKind.Apple,
      Url: 'https://api.kuzeylab.dev/apple/notifications',
      Version: 'V2',
      SandboxUrl: null,
      SandboxVersion: null,
      Assisted: false,
    },
    playNotifications: {
      StoreAppId: 202,
      Store: StoreKind.Google,
      Assisted: true,
      AssistedStepKey: 'google.rtdn',
      AssistedStepId: 5530,
      AssistedStepStatus: AssistedStepStatus.Todo,
    },
    testers: [
      {
        Id: 'st-1',
        FirstName: 'Deniz',
        LastName: 'Arslan',
        AccountName: 'sandbox.deniz@kuzeylab.dev',
        Territory: 'TUR',
        InterruptPurchases: false,
        SubscriptionRenewalRate: 'MONTHLY_RENEWAL_EVERY_ONE_HOUR',
        ApplePayCompatible: true,
      },
    ],
    plan: moneyPlan(),
  }
}

export const moneyState: MoneyState = initialMoney()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)
const planAnswer =
  (overrides: Partial<ChangePlanDto> = {}) =>
  () =>
    json(envelope({ ...moneyState.plan, ...overrides }))

// ---- Handlers ----------------------------------------------------------------------------------------------------------
export const monetizationHandlers: HttpHandler[] = [
  endpoint('get', '/Subscriptions/groups', ({ url }) =>
    json(envelope(url.searchParams.get('StoreAppId') === '202' ? moneyState.playGroups : moneyState.groups)),
  ),
  endpoint('get', '/Subscriptions/subscription', () => json(envelope(moneyState.subscription))),
  endpoint('patch', '/Subscriptions/prices', () => json(listEnvelope(moneyState.prices))),
  endpoint('get', '/Subscriptions/price-points', () => json(envelope(moneyState.pricePoints))),
  endpoint('get', '/Subscriptions/offers', () => json(envelope(moneyState.offers))),
  endpoint('post', '/Subscriptions/prices/convert', () => json(envelope(moneyState.converted))),
  endpoint('post', '/Subscriptions/plan', planAnswer()),
  endpoint('post', '/Subscriptions/sync', () => json(envelope(48900))),

  endpoint('patch', '/Iap', () => json(listEnvelope(moneyState.iaps))),
  endpoint('get', '/Iap/product', () => json(envelope(moneyState.iap))),
  endpoint('patch', '/Iap/prices', () => json(listEnvelope(moneyState.prices))),
  endpoint('get', '/Iap/price-points', () => json(envelope(moneyState.pricePoints))),
  endpoint('post', '/Iap/plan', planAnswer()),

  endpoint('get', '/PurchaseSettings/server-notifications', ({ url }) =>
    json(envelope(url.searchParams.get('StoreAppId') === '202' ? moneyState.playNotifications : moneyState.notifications)),
  ),
  endpoint(
    'post',
    '/PurchaseSettings/server-notifications/plan',
    planAnswer({
      Kind: ChangePlanKind.ServerNotifications,
      Title: 'Server notifications: 1 changes',
      RequiredPermission: OrgPermission.ManageWebhooks,
    }),
  ),
  endpoint('get', '/PurchaseSettings/sandbox-testers', () => json(envelope(moneyState.testers))),
  endpoint(
    'post',
    '/PurchaseSettings/sandbox-testers/plan',
    planAnswer({ Kind: ChangePlanKind.SandboxTesters, Title: 'Sandbox testers: 1 changes' }),
  ),
]

/** Resets the phase 7 data and answers its endpoints (and the phase 2 ones: plans, assets) for every test of the file. */
export function setupMonetizationApi(): void {
  beforeEach(() => {
    Object.assign(moneyState, initialMoney())
    server.use(...contentHandlers, ...monetizationHandlers)
  })
}
