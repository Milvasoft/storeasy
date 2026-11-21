import { HttpResponse } from 'msw'
import { beforeEach } from 'vitest'
import { ChangePlanKind, ChangePlanStatus, MediaKind } from '@/features/common/types'
import {
  AsoExperimentKind,
  AsoExperimentOperation,
  AsoExperimentStatus,
  AsoStoreSupport,
  type AsoExperimentDto,
  type AsoExperimentExportDto,
  type AsoExperimentOverviewDto,
  type AsoExperimentSummaryDto,
} from '@/features/experiments/types'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { changePlan } from './content'
import { make } from './factories'
import { endpoint, envelope, errorResponse } from './handlers'
import { server } from './server'

// The fake AsoExperimentsController: builders, the data its handlers answer with and the handlers (one per endpoint of
// src/features/experiments/api.ts; experimentsContract.test.ts checks the pairing). A test file calls
// setupExperimentsApi() once; its beforeEach resets the data and registers the handlers.

export function experiment(overrides: Partial<AsoExperimentDto> = {}): AsoExperimentDto {
  return make<AsoExperimentDto>(
    {
      Id: 501,
      AppId: 101,
      StoreAppId: 201,
      Store: StoreKind.Apple,
      Kind: AsoExperimentKind.ProductPageOptimization,
      Name: 'Yeni simge testi',
      Status: AsoExperimentStatus.Prepared,
      StoreState: 'PREPARE_FOR_SUBMISSION',
      TrafficProportion: 30,
      VariantCount: 2,
      StartDate: null,
      EndDate: null,
      WinnerVariantKey: null,
      Url: null,
      UpdatedDate: '2026-11-20T10:00:00Z',
      ExternalId: 'exp-1',
      Variants: [
        { Key: 'A', Name: 'Turuncu simge', AppIconName: 'OrangeIcon', ExternalId: 't-1', Locales: [{ Locale: 'tr', Media: [] }] },
        {
          Key: 'B',
          Name: 'Yeni ekranlar',
          AppIconName: null,
          ExternalId: 't-2',
          Locales: [{ Locale: 'tr', Media: [{ Kind: MediaKind.Screenshot, ClassKey: 'APP_IPHONE_67', AssetIds: [9101, 9102] }] }],
        },
      ],
      ResultSummary: null,
      ResultUrl: null,
      Note: null,
      ReviewRequired: true,
      ChecklistDone: [],
      Assisted: null,
      Timeline: [
        { Date: '2026-11-20T09:00:00Z', Kind: 'created', Title: 'Yeni simge testi', PlanId: null, PlanStatus: null },
        { Date: '2026-11-20T10:00:00Z', Kind: 'plan', Title: 'Mağazaya yaz', PlanId: 7101, PlanStatus: ChangePlanStatus.Applied },
      ],
      ConsoleUrl: 'https://appstoreconnect.apple.com/apps/1234567890/distribution',
      CreatedByUserName: 'deniz@kuzeylab.dev',
      SyncedDate: null,
      Operations: [AsoExperimentOperation.Push, AsoExperimentOperation.Start],
    },
    overrides,
  )
}

export function playExperiment(overrides: Partial<AsoExperimentDto> = {}): AsoExperimentDto {
  return experiment({
    Id: 601,
    StoreAppId: 202,
    Store: StoreKind.Google,
    Kind: AsoExperimentKind.PlayListingExperiment,
    Name: 'Kısa açıklama denemesi',
    Status: AsoExperimentStatus.Draft,
    StoreState: null,
    TrafficProportion: null,
    ExternalId: null,
    ReviewRequired: null,
    ConsoleUrl: null,
    Variants: [{ Key: 'A', Name: 'Kısa ve net', Locales: [{ Locale: 'tr', ShortDescription: 'Rotanı bul, yolunu kaybetme.', Media: [] }] }],
    Assisted: {
      Url: 'https://play.google.com/console/developers/app/store-listing-experiments',
      Steps: ['Deneyler sayfasını aç', 'Varyantları yükle', 'Deneyi başlat'],
      Pitfalls: ['Aynı anda tek bir varsayılan giriş deneyi çalışabilir.'],
    },
    Operations: [],
    ...overrides,
  })
}

export function summaryOf(item: AsoExperimentDto): AsoExperimentSummaryDto {
  return {
    Id: item.Id,
    AppId: item.AppId,
    StoreAppId: item.StoreAppId,
    Store: item.Store,
    Kind: item.Kind,
    Name: item.Name,
    Status: item.Status,
    StoreState: item.StoreState,
    TrafficProportion: item.TrafficProportion,
    VariantCount: item.Variants.length,
    StartDate: item.StartDate,
    EndDate: item.EndDate,
    WinnerVariantKey: item.WinnerVariantKey,
    Url: item.Url,
    UpdatedDate: item.UpdatedDate,
  }
}

interface ExperimentsState {
  experiments: AsoExperimentDto[]
  capabilities: AsoExperimentOverviewDto['Capabilities']
  exportFiles: AsoExperimentExportDto
  nextId: number
}

function initialExperiments(): ExperimentsState {
  return {
    experiments: [
      experiment(),
      experiment({
        Id: 502,
        Name: 'Sonbahar kampanyası',
        Kind: AsoExperimentKind.CustomProductPage,
        Status: AsoExperimentStatus.Running,
        TrafficProportion: null,
        StartDate: '2026-11-01T00:00:00Z',
        Url: 'https://apps.apple.com/app/id1234567890?ppid=abc',
      }),
    ],
    capabilities: [
      {
        StoreAppId: 201,
        Store: StoreKind.Apple,
        Kinds: [
          { Kind: AsoExperimentKind.ProductPageOptimization, Support: AsoStoreSupport.Api },
          { Kind: AsoExperimentKind.CustomProductPage, Support: AsoStoreSupport.Api },
        ],
      },
      {
        StoreAppId: 202,
        Store: StoreKind.Google,
        Kinds: [
          { Kind: AsoExperimentKind.PlayListingExperiment, Support: AsoStoreSupport.Assisted },
          { Kind: AsoExperimentKind.PlayCustomListing, Support: AsoStoreSupport.Assisted },
        ],
      },
      { StoreAppId: 203, Store: StoreKind.Microsoft, Kinds: [] },
    ],
    exportFiles: {
      Files: [
        {
          VariantKey: 'A',
          Locale: 'tr',
          ClassKey: 'phoneScreenshots',
          AssetId: 9201,
          FileName: '01.png',
          Url: 'https://storage.test/bucket/01.png',
        },
      ],
      Texts: [{ VariantKey: 'A', Locale: 'tr', Field: 'shortDescription', Value: 'Rotanı bul, yolunu kaybetme.' }],
    },
    nextId: 900,
  }
}

export const experimentsState: ExperimentsState = initialExperiments()

function json(body: unknown) {
  return HttpResponse.json(body as Record<string, unknown>)
}

function find(id: string | null): AsoExperimentDto | undefined {
  return experimentsState.experiments.find((item) => String(item.Id) === id)
}

function notFound() {
  return errorResponse('AsoExperimentNotFound', 'Deney bulunamadı.', 404)
}

export const experimentHandlers = [
  endpoint('get', '/aso-experiments', ({ url }) =>
    json(
      envelope({
        Experiments: experimentsState.experiments.filter((item) => String(item.AppId) === url.searchParams.get('AppId')).map(summaryOf),
        Capabilities: experimentsState.capabilities,
      }),
    ),
  ),
  endpoint('get', '/aso-experiments/experiment', ({ url }) => {
    const item = find(url.searchParams.get('Id'))
    return item ? json(envelope(item)) : notFound()
  }),
  endpoint('post', '/aso-experiments/experiment', ({ body }) => {
    const input = body as Partial<AsoExperimentDto> & { Id?: number | null }
    const existing = input.Id ? find(String(input.Id)) : undefined
    const saved = experiment({
      ...(existing ?? {}),
      ...input,
      Id: existing?.Id ?? experimentsState.nextId++,
      Status: existing?.Status ?? AsoExperimentStatus.Draft,
      ExternalId: existing?.ExternalId ?? null,
      Timeline: existing?.Timeline ?? [],
    } as Partial<AsoExperimentDto>)
    experimentsState.experiments = [saved, ...experimentsState.experiments.filter((item) => item.Id !== saved.Id)]
    return json(envelope(saved))
  }),
  endpoint('delete', '/aso-experiments/experiment', ({ url }) => {
    experimentsState.experiments = experimentsState.experiments.filter((item) => String(item.Id) !== url.searchParams.get('Id'))
    return json(envelope(true))
  }),
  endpoint('post', '/aso-experiments/experiment/plan', ({ body }) => {
    const input = body as { Id: number; Operation: number }
    const confirmed = input.Operation !== AsoExperimentOperation.Push
    return json(
      envelope(
        changePlan({
          PlanId: 7201,
          Kind: ChangePlanKind.AsoExperiment,
          Title: 'Yeni simge testi: mağazaya yaz',
          RequiredPermission: confirmed ? OrgPermission.ReleaseProduction : OrgPermission.EditContent,
          ConfirmRequired: confirmed,
          Items: [],
          Warnings: [],
        }),
      ),
    )
  }),
  endpoint('post', '/aso-experiments/experiment/result', ({ body }) => {
    const input = body as Partial<AsoExperimentDto> & { Id: number }
    const item = find(String(input.Id))
    if (!item) {
      return notFound()
    }
    const saved = { ...item, ...Object.fromEntries(Object.entries(input).filter(([, value]) => value !== null)) } as AsoExperimentDto
    experimentsState.experiments = experimentsState.experiments.map((other) => (other.Id === saved.Id ? saved : other))
    return json(envelope(saved))
  }),
  endpoint('post', '/aso-experiments/experiment/checklist', ({ body }) => {
    const input = body as { Id: number; DoneItems: number[] }
    const item = find(String(input.Id))
    if (!item) {
      return notFound()
    }
    const saved = { ...item, ChecklistDone: input.DoneItems }
    experimentsState.experiments = experimentsState.experiments.map((other) => (other.Id === saved.Id ? saved : other))
    return json(envelope(saved))
  }),
  endpoint('get', '/aso-experiments/experiment/export', () => json(envelope(experimentsState.exportFiles))),
  endpoint('post', '/aso-experiments/sync', () => json(envelope(48500))),
]

export function setupExperimentsApi(): void {
  beforeEach(() => {
    Object.assign(experimentsState, initialExperiments())
    server.use(...experimentHandlers)
  })
}
