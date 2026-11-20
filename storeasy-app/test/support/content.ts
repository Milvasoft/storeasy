import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { ActionChannel, BuildProcessingState, OrgPermission, StoreKind } from '@/lib/api/types'
import {
  AssistedStepStatus,
  ChangeAction,
  ChangePlanKind,
  ChangePlanStatus,
  ComplianceStatus,
  MediaKind,
  MediaSlotFill,
  ReadinessStatus,
  RuleApplicability,
  RuleStatus,
  SavedAnswerKind,
  type AgeRatingDto,
  type AppOverviewDto,
  type AssistedStepDetailDto,
  type AssistedStepListDto,
  type BuildLinkDto,
  type CategoryDto,
  type ChangePlanDto,
  type ComplianceItemDto,
  type ListingDto,
  type ListingDetailsDto,
  type MediaSetListDto,
  type PlayTestersDto,
  type PricingDto,
  type ReadinessReportDto,
  type ReleaseOverviewDto,
  type ReviewInfoDto,
  type RuleCatalogInfoDto,
  type RuleDetailDto,
  type RuleListDto,
  type SavedAnswerSetDto,
  type StoreOptionDto,
} from '@/features/common/types'
import { make } from './factories'
import { endpoint, envelope, listEnvelope } from './handlers'
import { server } from './server'

// The phase 2 fake backend: DTO builders typed with the screens' own contract, the data the handlers answer with, and
// the handlers themselves (one per endpoint of src/features/common/api.ts; contentContract.test.ts checks the pairing).
// A test file calls setupContentApi() once; its beforeEach resets the data and registers the handlers.

// ---- Builders ----------------------------------------------------------------------------------------------------------
export function changePlan(overrides: Partial<ChangePlanDto> = {}): ChangePlanDto {
  return make<ChangePlanDto>(
    {
      PlanId: 7001,
      Kind: ChangePlanKind.Listing,
      Title: 'de-DE anahtar kelimeler',
      AppId: 101,
      AppName: 'Pusula',
      StoreAppId: 201,
      Store: StoreKind.Apple,
      Source: ActionChannel.Web,
      CreatedByUserName: 'deniz@kuzeylab.dev',
      RequiredPermission: OrgPermission.EditContent,
      ConfirmRequired: false,
      ItemCount: 1,
      WarningCount: 1,
      Status: ChangePlanStatus.Pending,
      CreationDate: '2026-11-18T09:00:00Z',
      ExpiryDate: '2026-11-18T09:30:00Z',
      VersionId: 'v-24',
      VersionString: '2.4',
      Items: [
        { Group: 'Version', Locale: 'de-DE', Field: 'keywords', Before: 'karte,wandern', After: 'karte,gps', Action: ChangeAction.Update },
      ],
      Warnings: [{ Key: 'KeywordRepeated', Message: '"gps" en-US anahtar kelimelerinde de var.', Field: 'keywords', Locale: 'de-DE' }],
      Calls: [{ Method: 'PATCH', Path: 'appStoreVersionLocalizations', Target: 'loc-1' }],
      Files: [],
      CatalogVersion: '1.3',
    },
    overrides,
  )
}

export function readinessReport(overrides: Partial<ReadinessReportDto> = {}): ReadinessReportDto {
  return make<ReadinessReportDto>(
    {
      AppId: 101,
      StoreAppId: 201,
      Store: StoreKind.Apple,
      VersionId: 'v-24',
      VersionString: '2.4',
      CatalogVersion: '1.3',
      CheckedDate: '2026-11-18T09:00:00Z',
      CanSubmit: false,
      Ok: 1,
      Missing: 1,
      Warning: 0,
      Assisted: 1,
      Unknown: 0,
      Items: [
        { Key: 'texts.tr', Group: 'Texts', Status: ReadinessStatus.Ok, Title: 'Sürüm metinleri', Locale: 'tr' },
        {
          Key: 'screenshots.en-US',
          Group: 'Media',
          Status: ReadinessStatus.Missing,
          Title: 'Ekran görüntüleri',
          Message: 'iPhone 6,9" seti boş.',
          RuleId: 'apple.screenshots.iphone.69',
          Locale: 'en-US',
          FixRoute: 'listing/screenshots?locale=en-US',
          Slots: [
            { ClassKey: 'iphone-69', Required: true, FilledBy: MediaSlotFill.None, Count: 0 },
            { ClassKey: 'iphone-65', Required: false, FilledBy: MediaSlotFill.Fallback, FallbackFromClassKey: 'iphone-69', Count: 0 },
          ],
        },
        {
          Key: 'assisted.privacy',
          Group: 'Assisted',
          Status: ReadinessStatus.Assisted,
          Title: 'App Privacy etiketleri',
          FixRoute: 'assisted',
        },
      ],
    },
    overrides,
  )
}

export function complianceItem(overrides: Partial<ComplianceItemDto> = {}): ComplianceItemDto {
  return make<ComplianceItemDto>(
    {
      AppId: 101,
      AppName: 'Pusula',
      RuleId: 'apple.age-rating.2026-questions',
      Store: StoreKind.Apple,
      Title: 'Yaş derecelendirmesi yeni soruları',
      Summary: 'Paylaşılan konum soruları cevaplanmalı.',
      DueDate: '2027-01-12',
      DaysLeft: 55,
      Status: ComplianceStatus.Missing,
      RuleStatus: RuleStatus.Upcoming,
      Applicability: RuleApplicability.Applies,
      MatchReason: ['iphone'],
      SourceUrl: 'https://developer.apple.com/news/',
      CheckedDate: '2026-11-18',
    },
    overrides,
  )
}

export function listing(overrides: Partial<ListingDto> = {}): ListingDto {
  return make<ListingDto>(
    {
      StoreAppId: 201,
      Store: StoreKind.Apple,
      VersionId: 'v-24',
      VersionString: '2.4',
      VersionState: 'PREPARE_FOR_SUBMISSION',
      VersionEditable: true,
      CatalogVersion: '1.3',
      Locales: [
        { Locale: 'tr', IsPrimary: true, FilledCount: 4, RequiredCount: 4 },
        { Locale: 'de-DE', IsPrimary: false, FilledCount: 3, RequiredCount: 4 },
      ],
      Fields: [
        { Key: 'name', Group: 'AppInfo', Locale: 'tr', Value: 'Pusula', MaxLength: 30, Required: true },
        { Key: 'keywords', Group: 'Version', Locale: 'tr', Value: 'harita,rota', MaxLength: 100, MaxBytes: 100, Required: true },
        { Key: 'description', Group: 'Version', Locale: 'tr', Value: 'Yürüyüş haritası.', MaxLength: 4000, Required: true },
      ],
    },
    overrides,
  )
}

export function mediaSets(overrides: Partial<MediaSetListDto> = {}): MediaSetListDto {
  return make<MediaSetListDto>(
    {
      StoreAppId: 201,
      Store: StoreKind.Apple,
      Kind: MediaKind.Screenshot,
      Locale: 'tr',
      VersionId: 'v-24',
      CatalogVersion: '1.3',
      Classes: [
        {
          ClassKey: 'iphone-69',
          Title: 'iPhone 6,9"',
          Sizes: [{ Width: 1320, Height: 2868 }],
          Required: true,
          Applicability: RuleApplicability.Applies,
          Fill: MediaSlotFill.Own,
          Count: 2,
          Max: 10,
          Items: [
            { Id: 's1', Position: 1, FileName: '01-harita.png', Width: 1320, Height: 2868 },
            { Id: 's2', Position: 2, FileName: '02-rota.png', Width: 1320, Height: 2868 },
          ],
        },
        {
          ClassKey: 'iphone-65',
          Title: 'iPhone 6,5"',
          Required: false,
          Applicability: RuleApplicability.Applies,
          Fill: MediaSlotFill.Fallback,
          FallbackFromClassKey: 'iphone-69',
          Count: 0,
          Max: 10,
          Items: [],
        },
        {
          ClassKey: 'ipad-13',
          Title: 'iPad 13"',
          Required: true,
          Applicability: RuleApplicability.Applies,
          Fill: MediaSlotFill.None,
          Count: 0,
          Max: 10,
          Items: [],
        },
      ],
    },
    overrides,
  )
}

export function ruleListItem(overrides: Partial<RuleListDto> = {}): RuleListDto {
  return make<RuleListDto>(
    {
      Id: 'apple.screenshots.duo',
      Store: StoreKind.Apple,
      Category: 'screenshots',
      Title: 'iPhone Duo ekran görüntüleri',
      Level: 'required',
      Status: RuleStatus.Upcoming,
      EffectiveDate: '2027-04-01',
      Verified: true,
      SourceUrl: 'https://developer.apple.com/help/app-store-connect/',
      CheckedDate: '2026-11-18',
      AffectedAppCount: 2,
    },
    overrides,
  )
}

export function ruleDetail(overrides: Partial<RuleDetailDto> = {}): RuleDetailDto {
  return make<RuleDetailDto>(
    {
      ...ruleListItem(),
      Description: 'Duo dış ve iç ekran setleri.',
      FallbackChain: ['iphone-69'],
      AppliesWhen: { DeviceFamilies: ['iphone'], Build: { SdkName: 'iOS', SdkVersionAtLeast: '27.1' } },
      Params: { Sizes: [{ Width: 1398, Height: 2034 }] },
      Sources: [
        { Url: 'https://developer.apple.com/help/app-store-connect/', Title: 'Screenshot specifications', CheckedAt: '2026-11-18' },
      ],
      Conflicts: [{ Description: 'Sürüm notu bütün gönderimler diyor.', CatalogChoice: 'SDK koşulu' }],
      CatalogVersion: '1.3',
      AffectedApps: [
        { AppId: 101, Name: 'Pusula', Applicability: RuleApplicability.Unknown, Reasons: ['iphone'], UnknownKeys: ['build.sdk'] },
      ],
    },
    overrides,
  )
}

export function assistedStep(overrides: Partial<AssistedStepListDto> = {}): AssistedStepListDto {
  return make<AssistedStepListDto>(
    {
      Id: 5521,
      AppId: 101,
      Key: 'apple.privacy.labels',
      RuleId: 'apple.privacy.labels',
      Store: StoreKind.Apple,
      Title: 'App Privacy etiketlerini gir',
      Status: AssistedStepStatus.Todo,
    },
    overrides,
  )
}

export function assistedStepDetail(overrides: Partial<AssistedStepDetailDto> = {}): AssistedStepDetailDto {
  return make<AssistedStepDetailDto>(
    {
      ...assistedStep(),
      StoreUrl: 'https://appstoreconnect.apple.com/apps/6740056789/privacy',
      Steps: [
        { Order: 1, Text: 'App Privacy sayfasını aç', Done: true },
        { Order: 2, Text: 'Veri türlerini işaretle', Done: false },
      ],
      Pitfalls: ['Her türden sonra Save.'],
      AnswerKeys: [SavedAnswerKind.PrivacyLabels],
      Answers: {},
    },
    overrides,
  )
}

// ---- State -------------------------------------------------------------------------------------------------------------
export interface ContentState {
  readiness: ReadinessReportDto
  overview: AppOverviewDto[]
  appCompliance: ComplianceItemDto[]
  orgCompliance: ComplianceItemDto[]
  catalog: RuleCatalogInfoDto
  rules: RuleListDto[]
  rule: RuleDetailDto
  plans: ChangePlanDto[]
  plan: ChangePlanDto
  listing: ListingDto
  details: ListingDetailsDto
  media: MediaSetListDto
  categories: CategoryDto
  ageRating: AgeRatingDto
  pricing: PricingDto
  pricePoints: StoreOptionDto[]
  reviewInfo: ReviewInfoDto
  releases: ReleaseOverviewDto
  testers: PlayTestersDto
  builds: BuildLinkDto[]
  answers: SavedAnswerSetDto
  assisted: AssistedStepListDto[]
  assistedStep: AssistedStepDetailDto
}

function initialContent(): ContentState {
  return {
    readiness: readinessReport(),
    overview: [
      {
        AppId: 101,
        Name: 'Pusula',
        Stores: [
          {
            StoreAppId: 201,
            Store: StoreKind.Apple,
            CheckedDate: '2026-11-18T09:00:00Z',
            Ok: 10,
            Missing: 2,
            Warning: 0,
            Assisted: 1,
            Unknown: 0,
          },
        ],
        NextDeadline: { Date: '2027-01-12', RuleId: 'apple.age-rating.2026-questions', Title: 'Yaş derecelendirmesi' },
      },
    ],
    appCompliance: [complianceItem()],
    orgCompliance: [
      complianceItem(),
      complianceItem({
        AppId: 102,
        AppName: 'Defter',
        RuleId: 'ms.connection.secret-expiry',
        Store: StoreKind.Microsoft,
        Title: 'Entra sırrı',
        DueDate: '2026-12-10',
        DaysLeft: 22,
      }),
    ],
    catalog: { Version: '1.3', PublishedDate: '2026-11-18T00:00:00Z', RuleCount: 42 },
    rules: [
      ruleListItem(),
      ruleListItem({
        Id: 'google.build.target-api',
        Store: StoreKind.Google,
        Category: 'target-api',
        Title: 'Hedef API 36',
        Status: RuleStatus.InForce,
        AffectedAppCount: 0,
      }),
    ],
    rule: ruleDetail(),
    plans: [changePlan()],
    plan: changePlan(),
    listing: listing(),
    details: { StoreAppId: 202, ContactEmail: 'destek@kuzeylab.dev', DefaultLanguage: 'tr-TR' },
    media: mediaSets(),
    categories: {
      StoreAppId: 201,
      Primary: 'NAVIGATION',
      Secondary: 'TRAVEL',
      Options: [
        { Key: 'NAVIGATION', Title: 'Navigation' },
        { Key: 'TRAVEL', Title: 'Travel' },
        { Key: 'SPORTS', Title: 'Sports' },
      ],
      Copyright: '2026 Kuzey Lab',
      ContentRightsDeclaration: 'DOES_NOT_USE_THIRD_PARTY_CONTENT',
    },
    ageRating: {
      StoreAppId: 201,
      Answers: [
        { Key: 'gambling', Value: 'false', IsNew: false, Assisted: false },
        { Key: 'violenceCartoonOrFantasy', Value: 'NONE', IsNew: false, Assisted: false },
        { Key: 'sharedLocation', IsNew: true, Assisted: true, DueDate: '2027-01-12' },
      ],
      AssistedRuleIds: ['apple.age-rating.2026-questions'],
    },
    pricing: {
      StoreAppId: 201,
      BaseTerritory: 'TUR',
      PricePointId: 'pp-0',
      Schedule: [{ Territory: 'TUR', PricePointId: 'pp-0', CustomerPrice: '0.00' }],
      Territories: [
        { Code: 'TUR', Available: true },
        { Code: 'DEU', Available: true },
        { Code: 'CHN', Available: false },
      ],
      AvailableInNewTerritories: true,
    },
    pricePoints: [
      { Key: 'pp-0', Title: 'Ücretsiz' },
      { Key: 'pp-49', Title: '49,99 TL' },
    ],
    reviewInfo: {
      StoreAppId: 201,
      VersionId: 'v-24',
      ContactFirstName: 'Deniz',
      ContactLastName: 'Arslan',
      ContactEmail: 'deniz@kuzeylab.dev',
      ContactPhone: '+905550102030',
      DemoAccountRequired: true,
      DemoAccountName: 'demo@kuzeylab.dev',
      DemoPasswordSet: true,
      StorePasswordSet: true,
      Notes: 'Demo hesapla giriş yapın.',
      StoreDiffers: false,
    },
    releases: {
      StoreAppId: 201,
      Store: StoreKind.Apple,
      Versions: [
        {
          VersionId: 'v-24',
          VersionString: '2.4',
          Platform: 'IOS',
          State: 'PREPARE_FOR_SUBMISSION',
          ReleaseType: 'MANUAL',
        },
        {
          VersionId: 'v-23',
          VersionString: '2.3',
          Platform: 'IOS',
          State: 'READY_FOR_DISTRIBUTION',
          ReleaseType: 'AFTER_APPROVAL',
          BuildNumber: '412',
          PhasedRelease: { State: 'ACTIVE', CurrentDayNumber: 3 },
        },
      ],
    },
    testers: { StoreAppId: 202, Track: 'internal', GoogleGroups: ['beta@kuzeylab.dev'] },
    builds: [
      {
        Id: 9001,
        StoreAppId: 201,
        Store: StoreKind.Apple,
        Version: '2.4',
        BuildNumber: '412',
        ProcessingState: BuildProcessingState.Valid,
        UsesNonExemptEncryption: null,
      },
      {
        Id: 9002,
        StoreAppId: 201,
        Store: StoreKind.Apple,
        Version: '2.4',
        BuildNumber: '413',
        ProcessingState: BuildProcessingState.Processing,
      },
    ],
    answers: {
      AppId: 101,
      DemoPasswordSet: true,
      Sections: [
        {
          Key: SavedAnswerKind.ReviewLogin,
          UpdatedDate: '2026-11-03T10:00:00Z',
          UpdatedByUserName: 'deniz@kuzeylab.dev',
          Answers: {
            ContactFirstName: 'Deniz',
            ContactEmail: 'deniz@kuzeylab.dev',
            DemoAccountRequired: true,
            DemoAccountName: 'demo@kuzeylab.dev',
          },
        },
      ],
    },
    assisted: [
      assistedStep(),
      assistedStep({
        Id: 5522,
        Key: 'apple.agreements.paid',
        Title: 'Paid Apps Agreement',
        Status: AssistedStepStatus.Done,
        LastDoneDate: '2026-11-03T10:00:00Z',
        LastDoneByUserName: 'deniz@kuzeylab.dev',
        Channel: ActionChannel.Web,
      }),
    ],
    assistedStep: assistedStepDetail(),
  }
}

export const contentState: ContentState = initialContent()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)
const planAnswer =
  (overrides: Partial<ChangePlanDto> = {}) =>
  () =>
    json(envelope({ ...contentState.plan, ...overrides }))

// ---- Handlers ----------------------------------------------------------------------------------------------------------
export const contentHandlers: HttpHandler[] = [
  endpoint('put', '/Apps/profile', () => json(envelope(101))),
  endpoint('get', '/Apps/readiness', () => json(envelope(contentState.readiness))),
  endpoint('get', '/Apps/overview', () => json(envelope(contentState.overview))),
  endpoint('get', '/Apps/compliance', () => json(envelope(contentState.appCompliance))),
  endpoint('get', '/Compliance', () => json(envelope(contentState.orgCompliance))),

  endpoint('get', '/Rules/catalog', () => json(envelope(contentState.catalog))),
  endpoint('patch', '/Rules', () => json(envelope(contentState.rules))),
  endpoint('get', '/Rules/rule', () => json(envelope(contentState.rule))),

  endpoint('patch', '/plans', () => json(listEnvelope(contentState.plans))),
  endpoint('get', '/plans/plan', () => json(envelope(contentState.plan))),
  endpoint('post', '/plans/approve', planAnswer({ Status: ChangePlanStatus.Approved, JobId: 48500 })),
  endpoint('post', '/plans/reject', planAnswer({ Status: ChangePlanStatus.Rejected })),
  endpoint('post', '/plans/discard', planAnswer({ Status: ChangePlanStatus.Discarded })),

  // The API answers the asked language's fields; the sample's fields move to that language.
  endpoint('get', '/Listings', ({ url }) => {
    const locale = url.searchParams.get('Locale')
    const fields = (contentState.listing.Fields ?? []).map((field) => (locale && field.Locale ? { ...field, Locale: locale } : field))
    return json(envelope({ ...contentState.listing, Fields: fields }))
  }),
  endpoint('get', '/Listings/details', () => json(envelope(contentState.details))),
  endpoint('post', '/Categories/suggest', ({ body }) =>
    json(
      envelope({
        StoreAppId: (body as { StoreAppId: number }).StoreAppId,
        CurrentPrimary: 'NAVIGATION',
        CurrentSecondary: 'TRAVEL',
        Primary: 'TRAVEL',
        Secondary: 'NAVIGATION',
        Reason: 'Uygulama önce bir seyahat aracı.',
        Model: 'claude-haiku-5-5',
      }),
    ),
  ),
  endpoint('post', '/Listings/plan', planAnswer()),
  endpoint('post', '/Listings/suggest', ({ body }) => {
    const request = body as { StoreAppId: number; Locale: string; Field: string }
    return json(
      envelope({
        StoreAppId: request.StoreAppId,
        Locale: request.Locale,
        Field: request.Field,
        Before: null,
        Value: 'Pusula: Rota ve Harita',
        Reason: 'Ana faydayı adın yanına ekler.',
        Shortened: false,
        MaxLength: 30,
        MaxBytes: null,
        Model: 'claude-haiku-5-5',
      }),
    )
  }),

  endpoint('get', '/Screenshots/sets', () => json(envelope(contentState.media))),
  endpoint('get', '/Previews/sets', () => json(envelope({ ...contentState.media, Kind: MediaKind.Preview }))),
  endpoint('post', '/Screenshots/plan', planAnswer({ Kind: ChangePlanKind.Screenshots, Title: 'tr · iPhone 6,9"' })),
  endpoint('post', '/Previews/plan', planAnswer({ Kind: ChangePlanKind.Previews })),

  endpoint('get', '/Categories', () => json(envelope(contentState.categories))),
  endpoint('put', '/Categories', planAnswer({ Kind: ChangePlanKind.Categories })),
  endpoint('get', '/age-rating', () => json(envelope(contentState.ageRating))),
  endpoint('put', '/age-rating', planAnswer({ Kind: ChangePlanKind.AgeRating })),
  endpoint('get', '/Pricing', () => json(envelope(contentState.pricing))),
  endpoint('get', '/Pricing/price-points', () => json(envelope(contentState.pricePoints))),
  endpoint('put', '/Pricing', planAnswer({ Kind: ChangePlanKind.Pricing })),
  endpoint('get', '/review-info', () => json(envelope(contentState.reviewInfo))),
  endpoint('put', '/review-info', planAnswer({ Kind: ChangePlanKind.ReviewInfo })),

  endpoint('patch', '/Builds', () => json(listEnvelope(contentState.builds))),
  endpoint('post', '/Builds/link', planAnswer({ Kind: ChangePlanKind.BuildLink })),
  endpoint('put', '/Builds/export-compliance', planAnswer({ Kind: ChangePlanKind.ExportCompliance })),

  endpoint('get', '/Releases', () => json(envelope(contentState.releases))),
  endpoint('post', '/Releases/plan', planAnswer({ Kind: ChangePlanKind.TrackRelease })),
  endpoint('put', '/Releases/rollout', planAnswer({ Kind: ChangePlanKind.Rollout })),
  endpoint('post', '/Releases/submit-for-review', planAnswer({ Kind: ChangePlanKind.SubmitForReview, Status: ChangePlanStatus.Approved })),
  endpoint('get', '/Releases/testers', () => json(envelope(contentState.testers))),
  endpoint('put', '/Releases/testers', planAnswer({ Kind: ChangePlanKind.Testers })),

  endpoint('get', '/Apps/answers', () => json(envelope(contentState.answers))),
  endpoint('put', '/Apps/answers', () => json(envelope(31))),
  endpoint('post', '/Apps/answers/copy', () => json(envelope(3))),
  endpoint('post', '/Apps/answers/data-safety', planAnswer({ Kind: ChangePlanKind.DataSafety })),

  endpoint('patch', '/assisted-steps', () => json(envelope(contentState.assisted))),
  endpoint('get', '/assisted-steps/step', () => json(envelope(contentState.assistedStep))),
  endpoint('post', '/assisted-steps/step/handoff', () =>
    json(
      envelope({
        StepId: contentState.assistedStep.Id,
        Key: contentState.assistedStep.Key,
        Store: StoreKind.Apple,
        AppName: 'Pusula',
        Title: contentState.assistedStep.Title,
        Prompt: 'App Privacy etiketlerini kayıtlı cevaplarla doldur.',
        McpTool: 'assisted_step_get',
      }),
    ),
  ),
  endpoint('put', '/assisted-steps/step/complete', () => json(envelope(contentState.assistedStep.Id))),
]

/** Resets the phase 2 data and answers its endpoints for every test of the calling file. */
export function setupContentApi(): void {
  beforeEach(() => {
    Object.assign(contentState, initialContent())
    server.use(...contentHandlers)
  })
}
