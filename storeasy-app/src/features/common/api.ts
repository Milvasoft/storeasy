import { apiGet, apiPatch, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type { StoreKind } from '@/lib/api/types'
import type {
  AgeRatingDto,
  AppOverviewDto,
  AppProfileInput,
  AssistedStepDetailDto,
  AssistedStepHandOffDto,
  AssistedStepListDto,
  AssistedStepStatus,
  BuildLinkDto,
  CategoryDto,
  ChangePlanDto,
  ChangePlanListDto,
  ChangePlanStatus,
  ComplianceItemDto,
  ListingChangeDto,
  ListingDetailsDto,
  ListingDto,
  MediaKind,
  MediaSetListDto,
  PlayTestersDto,
  PricingDto,
  ReadinessReportDto,
  ReleaseNote,
  ReleaseOverviewDto,
  ReleasePlanKind,
  ReleaseType,
  ReviewInfoDto,
  RolloutAction,
  RuleCatalogInfoDto,
  RuleDetailDto,
  RuleListDto,
  RuleStatus,
  SavedAnswerKind,
  SavedAnswerSetDto,
  StoreOptionDto,
  TerritoryDto,
  ListingSuggestionDto,
} from './types'

// The phase 2 endpoints (storeasy-api controllers: Apps, Rules, Compliance, plans, Listings, Screenshots, Previews,
// Categories, age-rating, Pricing, review-info, Builds, Releases, assisted-steps, Assets). Every store write is a change
// plan: the Plan* calls send DryRun true and answer the plan, which is approved through plansApi.approve.
// Kept next to the screens until the endpoints.ts split; the phase 2 contract test reads this file.

// ---- Apps --------------------------------------------------------------------------------------------------------------
export const appContentApi = {
  // Replaces the whole catalog profile; a null field means "not known".
  updateProfile: (organizationId: number, appId: number, profile: AppProfileInput) =>
    apiPut<number>('/Apps/profile', { OrganizationId: organizationId, AppId: appId, ...profile }),

  readiness: (organizationId: number, appId: number, store: StoreKind) =>
    apiGet<ReadinessReportDto>('/Apps/readiness', { OrganizationId: organizationId, AppId: appId, Store: store }),

  // Refresh checks every store live first (rate limited); without it the last stored checks come back.
  overview: (organizationId: number, refresh = false) =>
    apiGet<AppOverviewDto[]>('/Apps/overview', { OrganizationId: organizationId, Refresh: refresh }),

  compliance: (organizationId: number, appId: number) =>
    apiGet<ComplianceItemDto[]>('/Apps/compliance', { OrganizationId: organizationId, AppId: appId }),
}

export const complianceApi = {
  organization: (organizationId: number, query: { From?: string | null; To?: string | null; OpenOnly?: boolean } = {}) =>
    apiGet<ComplianceItemDto[]>('/Compliance', { OrganizationId: organizationId, ...query }),
}

// ---- Rules catalog -----------------------------------------------------------------------------------------------------
export interface RuleListFilter {
  Store?: StoreKind | null
  Category?: string | null
  Status?: RuleStatus | null
  Search?: string | null
}

export const rulesApi = {
  catalog: (organizationId: number) => apiGet<RuleCatalogInfoDto>('/Rules/catalog', { OrganizationId: organizationId }),

  list: (organizationId: number, filter: RuleListFilter) =>
    apiPatch<RuleListDto[]>('/Rules', { OrganizationId: organizationId, ...filter }),

  detail: (organizationId: number, ruleId: string) =>
    apiGet<RuleDetailDto>('/Rules/rule', { OrganizationId: organizationId, RuleId: ruleId }),
}

// ---- Change plans ------------------------------------------------------------------------------------------------------
export const plansApi = {
  // Status null lists every plan; the API defaults to Pending (the approval queue).
  list: (organizationId: number, request: ListRequest, status: ChangePlanStatus | null) =>
    apiPatchList<ChangePlanListDto>('/plans', { OrganizationId: organizationId, Status: status, ...request }),

  detail: (organizationId: number, planId: number) =>
    apiGet<ChangePlanDto>('/plans/plan', { OrganizationId: organizationId, PlanId: planId }),

  // 409 when the store changed since the plan was made, 410 when it expired; Confirm is the typed text of store-bound plans.
  approve: (organizationId: number, planId: number, confirm?: string | null) =>
    apiPost<ChangePlanDto>(
      '/plans/approve',
      { OrganizationId: organizationId, PlanId: planId, Confirm: confirm ?? null },
      { idempotent: true },
    ),

  reject: (organizationId: number, planId: number, reason: string) =>
    apiPost<ChangePlanDto>('/plans/reject', { OrganizationId: organizationId, PlanId: planId, Reason: reason }),

  discard: (organizationId: number, planId: number) =>
    apiPost<ChangePlanDto>('/plans/discard', { OrganizationId: organizationId, PlanId: planId }),
}

// ---- Listings ----------------------------------------------------------------------------------------------------------
export const listingsApi = {
  get: (organizationId: number, storeAppId: number, locale?: string | null) =>
    apiGet<ListingDto>('/Listings', { OrganizationId: organizationId, StoreAppId: storeAppId, Locale: locale ?? null }),

  details: (organizationId: number, storeAppId: number) =>
    apiGet<ListingDetailsDto>('/Listings/details', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  plan: (organizationId: number, storeAppId: number, versionId: string | null, changes: ListingChangeDto[]) =>
    apiPost<ChangePlanDto>('/Listings/plan', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      VersionId: versionId,
      Changes: changes,
      DryRun: true,
    }),

  // An AI suggestion for one listing text with the organization's key (editors). Nothing is saved or planned; audited
  // with the tokens and the cost. 409 AiKeyNotSet; 400 AiSuggestionNotSupported / AiSuggestionLocaleNotFound.
  suggest: (organizationId: number, storeAppId: number, versionId: string | null, locale: string, field: string, instruction?: string | null) =>
    apiPost<ListingSuggestionDto>('/Listings/suggest', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      VersionId: versionId,
      Locale: locale,
      Field: field,
      Instruction: instruction?.trim() || null,
    }),
}

// ---- Screenshots and previews ------------------------------------------------------------------------------------------
export interface MediaPlanInput {
  Locale: string
  ClassKey: string
  Order: string[]
  Deletes: string[]
  AssetIds: number[]
}

function mediaBase(kind: MediaKind): '/Screenshots' | '/Previews' {
  return kind === 2 ? '/Previews' : '/Screenshots'
}

export const mediaApi = {
  screenshotSets: (organizationId: number, storeAppId: number, locale: string | null) =>
    apiGet<MediaSetListDto>('/Screenshots/sets', { OrganizationId: organizationId, StoreAppId: storeAppId, Locale: locale }),

  previewSets: (organizationId: number, storeAppId: number, locale: string | null) =>
    apiGet<MediaSetListDto>('/Previews/sets', { OrganizationId: organizationId, StoreAppId: storeAppId, Locale: locale }),

  planScreenshots: (organizationId: number, storeAppId: number, input: MediaPlanInput) =>
    apiPost<ChangePlanDto>('/Screenshots/plan', { OrganizationId: organizationId, StoreAppId: storeAppId, ...input, DryRun: true }),

  planPreviews: (organizationId: number, storeAppId: number, input: MediaPlanInput) =>
    apiPost<ChangePlanDto>('/Previews/plan', { OrganizationId: organizationId, StoreAppId: storeAppId, ...input, DryRun: true }),

  sets: (kind: MediaKind, organizationId: number, storeAppId: number, locale: string | null) =>
    mediaBase(kind) === '/Previews'
      ? mediaApi.previewSets(organizationId, storeAppId, locale)
      : mediaApi.screenshotSets(organizationId, storeAppId, locale),

  plan: (kind: MediaKind, organizationId: number, storeAppId: number, input: MediaPlanInput) =>
    mediaBase(kind) === '/Previews'
      ? mediaApi.planPreviews(organizationId, storeAppId, input)
      : mediaApi.planScreenshots(organizationId, storeAppId, input),
}

// ---- Categories, age rating, pricing, review info ----------------------------------------------------------------------
export interface CategoriesInput {
  Primary: string | null
  Secondary: string | null
  Copyright: string | null
  ContentRightsDeclaration: string | null
}

export interface PricingInput {
  BaseTerritory: string | null
  PricePointId: string | null
  Territories: TerritoryDto[]
}

export interface ReviewInfoInput {
  VersionId: string | null
  ContactFirstName: string | null
  ContactLastName: string | null
  ContactEmail: string | null
  ContactPhone: string | null
  DemoAccountRequired: boolean | null
  DemoAccountName: string | null
  // Write only: null keeps the sealed password, it is never read back.
  DemoPassword: string | null
  Notes: string | null
}

export const metadataApi = {
  categories: (organizationId: number, storeAppId: number) =>
    apiGet<CategoryDto>('/Categories', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  planCategories: (organizationId: number, storeAppId: number, input: CategoriesInput) =>
    apiPut<ChangePlanDto>('/Categories', { OrganizationId: organizationId, StoreAppId: storeAppId, ...input, DryRun: true }),

  ageRating: (organizationId: number, storeAppId: number) =>
    apiGet<AgeRatingDto>('/age-rating', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  // Answers keep their JSON type: "NONE" style values as strings, flags as booleans.
  planAgeRating: (organizationId: number, storeAppId: number, answers: Record<string, string | boolean>) =>
    apiPut<ChangePlanDto>('/age-rating', { OrganizationId: organizationId, StoreAppId: storeAppId, Answers: answers, DryRun: true }),

  pricing: (organizationId: number, storeAppId: number) =>
    apiGet<PricingDto>('/Pricing', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  pricePoints: (organizationId: number, storeAppId: number, territory: string) =>
    apiGet<StoreOptionDto[]>('/Pricing/price-points', { OrganizationId: organizationId, StoreAppId: storeAppId, Territory: territory }),

  planPricing: (organizationId: number, storeAppId: number, input: PricingInput) =>
    apiPut<ChangePlanDto>('/Pricing', { OrganizationId: organizationId, StoreAppId: storeAppId, ...input, DryRun: true }),

  reviewInfo: (organizationId: number, storeAppId: number, versionId?: string | null) =>
    apiGet<ReviewInfoDto>('/review-info', { OrganizationId: organizationId, StoreAppId: storeAppId, VersionId: versionId ?? null }),

  planReviewInfo: (organizationId: number, storeAppId: number, input: ReviewInfoInput) =>
    apiPut<ChangePlanDto>('/review-info', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      ...input,
      UseSavedAnswers: false,
      DryRun: true,
    }),
}

// ---- Builds and releases -----------------------------------------------------------------------------------------------
export interface ReleasePlanInput {
  Kind: ReleasePlanKind
  VersionId?: string | null
  ReleaseType?: ReleaseType | null
  EarliestReleaseDate?: string | null
  PhasedRelease?: boolean | null
  Track?: string | null
  Name?: string | null
  VersionCodes?: string[]
  Status?: 'completed' | 'draft' | 'inProgress'
  UserFraction?: number | null
  ReleaseNotes?: ReleaseNote[]
  Countries?: string[] | null
  IncludeRestOfWorld?: boolean | null
}

export interface RolloutInput {
  VersionId?: string | null
  Track?: string | null
  Action: RolloutAction
  // 0-100, SetPercentage only.
  Percentage?: number | null
  Reason?: string | null
}

export const buildsApi = {
  list: (organizationId: number, storeAppId: number, request: ListRequest) =>
    apiPatchList<BuildLinkDto>('/Builds', { OrganizationId: organizationId, StoreAppId: storeAppId, ...request }),

  planLink: (organizationId: number, storeAppId: number, versionId: string, buildId: number, usesNonExemptEncryption: boolean | null) =>
    apiPost<ChangePlanDto>('/Builds/link', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      VersionId: versionId,
      BuildId: buildId,
      UsesNonExemptEncryption: usesNonExemptEncryption,
      DryRun: true,
    }),

  planExportCompliance: (organizationId: number, storeAppId: number, versionId: string, usesNonExemptEncryption: boolean) =>
    apiPut<ChangePlanDto>('/Builds/export-compliance', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      VersionId: versionId,
      UsesNonExemptEncryption: usesNonExemptEncryption,
      DryRun: true,
    }),
}

export const releasesApi = {
  overview: (organizationId: number, storeAppId: number) =>
    apiGet<ReleaseOverviewDto>('/Releases', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  plan: (organizationId: number, storeAppId: number, input: ReleasePlanInput) =>
    apiPost<ChangePlanDto>('/Releases/plan', { OrganizationId: organizationId, StoreAppId: storeAppId, ...input, DryRun: true }),

  planRollout: (organizationId: number, storeAppId: number, input: RolloutInput) =>
    apiPut<ChangePlanDto>('/Releases/rollout', { OrganizationId: organizationId, StoreAppId: storeAppId, ...input, DryRun: true }),

  // Makes the plan and approves it in one call; the typed "app version" text must match (400 PlanConfirmMismatch).
  submitForReview: (organizationId: number, storeAppId: number, versionId: string, confirm: string) =>
    apiPost<ChangePlanDto>(
      '/Releases/submit-for-review',
      { OrganizationId: organizationId, StoreAppId: storeAppId, VersionId: versionId, Confirm: confirm },
      { idempotent: true },
    ),

  testers: (organizationId: number, storeAppId: number, track: string) =>
    apiGet<PlayTestersDto>('/Releases/testers', { OrganizationId: organizationId, StoreAppId: storeAppId, Track: track }),

  planTesters: (organizationId: number, storeAppId: number, track: string, googleGroups: string[]) =>
    apiPut<ChangePlanDto>('/Releases/testers', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      Track: track,
      GoogleGroups: googleGroups,
      DryRun: true,
    }),
}

// ---- Saved answers -----------------------------------------------------------------------------------------------------
export const answersApi = {
  get: (organizationId: number, appId: number) =>
    apiGet<SavedAnswerSetDto>('/Apps/answers', { OrganizationId: organizationId, AppId: appId }),

  // DemoPassword null leaves the sealed password as it is; it never comes back from the API.
  update: (
    organizationId: number,
    appId: number,
    key: SavedAnswerKind,
    answers: unknown,
    demoPassword: string | null,
    removeDemoLogin = false,
  ) =>
    apiPut<number>('/Apps/answers', {
      OrganizationId: organizationId,
      AppId: appId,
      Key: key,
      Answers: answers,
      DemoPassword: demoPassword,
      RemoveDemoAccountLogin: removeDemoLogin,
    }),

  copy: (organizationId: number, sourceAppId: number, targetAppId: number, keys: SavedAnswerKind[]) =>
    apiPost<number>('/Apps/answers/copy', {
      OrganizationId: organizationId,
      SourceAppId: sourceAppId,
      TargetAppId: targetAppId,
      Keys: keys,
    }),

  planDataSafety: (organizationId: number, appId: number) =>
    apiPost<ChangePlanDto>('/Apps/answers/data-safety', { OrganizationId: organizationId, AppId: appId, DryRun: true }),
}

// ---- Assisted steps ----------------------------------------------------------------------------------------------------
export const assistedApi = {
  list: (organizationId: number, filter: { AppId?: number | null; Store?: StoreKind | null; Status?: AssistedStepStatus | null }) =>
    apiPatch<AssistedStepListDto[]>('/assisted-steps', { OrganizationId: organizationId, ...filter }),

  detail: (organizationId: number, stepId: number) =>
    apiGet<AssistedStepDetailDto>('/assisted-steps/step', { OrganizationId: organizationId, StepId: stepId }),

  handOff: (organizationId: number, stepId: number) =>
    apiPost<AssistedStepHandOffDto>('/assisted-steps/step/handoff', { OrganizationId: organizationId, StepId: stepId }),

  complete: (
    organizationId: number,
    stepId: number,
    input: { Status: AssistedStepStatus; DoneItems: number[]; EvidenceAssetId: number | null; Note: string | null },
  ) => apiPut<number>('/assisted-steps/step/complete', { OrganizationId: organizationId, StepId: stepId, ...input }),
}
