import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { normalizeSpec } from '@/features/studio/spec'
import {
  MockupDeviceFamily,
  MockupFrameKind,
  MockupRequirement,
  type MockupCaptionDto,
  type MockupCaptionsSavedDto,
  type MockupCaptionSuggestionDto,
  type MockupClassDto,
  type MockupExportManifest,
  type MockupExportPlanDto,
  type MockupFrameDto,
  type MockupPreviewDto,
  type MockupStarterTemplateDto,
  type MockupTemplateDetailDto,
  type MockupTemplateListDto,
} from '@/features/studio/types'
import { StoreKind } from '@/lib/api/types'
import { make } from './factories'
import { endpoint, envelope, listEnvelope } from './handlers'
import { server } from './server'

// The studio's fake backend (MockupsController): DTO builders, the state the handlers read, and the handlers.

export function mockupClass(overrides: Partial<MockupClassDto> = {}): MockupClassDto {
  return make<MockupClassDto>(
    {
      ClassKey: 'apple.screenshots.iphone-dynamic-island-medium',
      Store: StoreKind.Apple,
      Label: 'iPhone DI medium',
      LabelTr: 'iPhone DI orta',
      Width: 1206,
      Height: 2622,
      Family: MockupDeviceFamily.Phone,
      Status: MockupRequirement.Required,
      RequiredFrom: null,
      MaxCount: 10,
    },
    overrides,
  )
}

export function frame(overrides: Partial<MockupFrameDto> = {}): MockupFrameDto {
  return make<MockupFrameDto>(
    {
      Kind: MockupFrameKind.Phone,
      CornerRadius: 0.13,
      Bezel: 0.025,
      Cutouts: ['island', 'notch', 'hole', 'none'],
      Classes: [
        mockupClass(),
        mockupClass({
          ClassKey: 'google.screenshots.phone',
          Store: StoreKind.Google,
          Label: 'Phone',
          LabelTr: 'Telefon',
          Width: 1080,
          Height: 1920,
        }),
      ],
    },
    overrides,
  )
}

export function caption(overrides: Partial<MockupCaptionDto> = {}): MockupCaptionDto {
  return make<MockupCaptionDto>(
    { Locale: 'tr', SlotOrder: 1, Title: 'Hedeflerin tek bakışta', Subtitle: 'Bütün rotan tek ekranda' },
    overrides,
  )
}

export function templateListItem(overrides: Partial<MockupTemplateListDto> = {}): MockupTemplateListDto {
  return make<MockupTemplateListDto>(
    {
      Id: 12,
      AppId: 101,
      AppName: 'Pusula',
      Name: 'Mağaza seti 2026',
      PrimaryLocale: 'tr',
      Locales: ['tr', 'en-US'],
      SlotCount: 3,
      StarterKey: 'classic',
      Version: 4,
      CreationDate: '2026-11-01T09:00:00Z',
      LastModificationDate: '2026-11-18T09:00:00Z',
      LastModifierUserName: 'ece@kuzeylab.dev',
      LastExportJobId: 8812,
      LastExportDate: '2026-11-12T14:21:00Z',
      LastExportImageCount: 162,
    },
    overrides,
  )
}

export function templateDetail(overrides: Partial<MockupTemplateDetailDto> = {}): MockupTemplateDetailDto {
  const spec = normalizeSpec(null)
  return make<MockupTemplateDetailDto>(
    {
      ...templateListItem(),
      Spec: { ...spec, Canvas: { ClassKey: 'apple.screenshots.iphone-dynamic-island-medium' } },
      Slots: [{ Order: 1, Family: MockupDeviceFamily.Phone, Locale: null, AssetId: 901 }],
      Captions: [
        caption(),
        caption({ SlotOrder: 2, Title: 'Her adımı işaretle', Subtitle: 'Küçük adımlar' }),
        caption({ Locale: 'en-US', Title: 'Your goals at a glance', Subtitle: 'Your whole route' }),
      ],
    },
    overrides,
  )
}

export function starter(overrides: Partial<MockupStarterTemplateDto> = {}): MockupStarterTemplateDto {
  return make<MockupStarterTemplateDto>(
    {
      Key: 'classic',
      Name: 'Classic',
      NameTr: 'Klasik',
      Description: 'Caption on top',
      DescriptionTr: 'Başlık üstte',
      SlotCount: 6,
      SampleLanguages: ['tr', 'en-US'],
      Spec: normalizeSpec(null),
    },
    overrides,
  )
}

export function exportPlan(overrides: Partial<MockupExportPlanDto> = {}): MockupExportPlanDto {
  return make<MockupExportPlanDto>(
    {
      TemplateId: 12,
      CatalogVersion: '1.0.1',
      Locales: ['tr', 'en-US'],
      Items: [
        {
          ClassKey: 'apple.screenshots.iphone-dynamic-island-medium',
          Store: StoreKind.Apple,
          Label: 'iPhone DI medium',
          LabelTr: 'iPhone DI orta',
          Width: 1206,
          Height: 2622,
          Frame: MockupFrameKind.Phone,
          Status: MockupRequirement.Required,
          RequiredFrom: null,
          Selected: true,
          PerLocaleCount: 3,
          Note: 'Every iPhone app needs at least one image.',
        },
        {
          ClassKey: 'apple.screenshots.iphone-duo-inner',
          Store: StoreKind.Apple,
          Label: 'iPhone Duo inner display',
          LabelTr: 'iPhone Duo iç ekran',
          Width: 2007,
          Height: 2853,
          Frame: MockupFrameKind.FoldableInner,
          Status: MockupRequirement.Recommended,
          RequiredFrom: '2027-04-01',
          Selected: false,
          PerLocaleCount: 3,
          Note: null,
        },
        {
          ClassKey: 'google.screenshots.phone',
          Store: StoreKind.Google,
          Label: 'Phone',
          LabelTr: 'Telefon',
          Width: 1080,
          Height: 1920,
          Frame: MockupFrameKind.Phone,
          Status: MockupRequirement.Required,
          RequiredFrom: null,
          Selected: true,
          PerLocaleCount: 3,
          Note: null,
        },
      ],
      Rejected: [
        {
          ClassKey: 'apple.screenshots.iphone-dynamic-island-medium',
          Width: 1320,
          Height: 2868,
          Reason: 'The 6.1"/6.3" slot accepts only 1206x2622 and 1179x2556.',
        },
      ],
      Total: 12,
      Warnings: [],
    },
    overrides,
  )
}

export function manifest(overrides: Partial<MockupExportManifest> = {}): MockupExportManifest {
  const images = (start: number) =>
    [1, 2, 3].map((order) => ({ SlotOrder: order, AssetId: start + order, Size: 900_000, Sha256: null, Md5: null, ZipPath: null }))
  return make<MockupExportManifest>(
    {
      SchemaVersion: 1,
      JobId: 8812,
      TemplateId: 12,
      AppId: 101,
      TemplateVersion: 4,
      CatalogVersion: '1.0.1',
      CreatedDate: '2026-11-18T14:21:00Z',
      ZipAssetId: 991,
      ImageCount: 9,
      TotalBytes: 8_100_000,
      Sets: [
        {
          Store: StoreKind.Apple,
          ClassKey: 'apple.screenshots.iphone-dynamic-island-medium',
          StoreClassKey: 'APP_IPHONE_61',
          Locale: 'tr',
          Width: 1206,
          Height: 2622,
          // Out of order on purpose: the plan sends them in display order.
          Images: [...images(900)].reverse(),
        },
        {
          Store: StoreKind.Google,
          ClassKey: 'google.screenshots.phone',
          StoreClassKey: 'phoneScreenshots',
          Locale: 'tr',
          Width: 1080,
          Height: 1920,
          Images: images(910),
        },
        {
          Store: StoreKind.Apple,
          ClassKey: 'apple.screenshots.iphone-duo-inner',
          StoreClassKey: null,
          Locale: 'tr',
          Width: 2007,
          Height: 2853,
          Images: images(920),
        },
      ],
      Failures: [{ ClassKey: 'apple.screenshots.ipad-13', Locale: 'tr', SlotOrder: 3, Code: 'AssetTooSmall', Message: 'Needs 139%.' }],
      Warnings: [],
    },
    overrides,
  )
}

export interface StudioState {
  templates: MockupTemplateListDto[]
  template: MockupTemplateDetailDto
  frames: MockupFrameDto[]
  starters: MockupStarterTemplateDto[]
  plan: MockupExportPlanDto
  manifest: MockupExportManifest
  captionsSaved: MockupCaptionsSavedDto
  suggestion: MockupCaptionSuggestionDto
  /** The version PUT template answers. */
  nextVersion: number
  exportJobId: number
}

function initialStudio(): StudioState {
  return {
    templates: [
      templateListItem(),
      templateListItem({ Id: 13, AppId: 102, AppName: 'Kumbara', Name: 'İlk gönderim', LastExportDate: null, LastExportJobId: null }),
    ],
    template: templateDetail(),
    frames: [
      frame(),
      frame({
        Kind: MockupFrameKind.Desktop,
        CornerRadius: 0.01,
        Bezel: 0,
        Cutouts: [],
        Classes: [
          mockupClass({
            ClassKey: 'microsoft.screenshots.desktop',
            Store: StoreKind.Microsoft,
            Label: 'Desktop',
            LabelTr: 'Masaüstü',
            Width: 1920,
            Height: 1080,
            Family: MockupDeviceFamily.Desktop,
          }),
        ],
      }),
    ],
    starters: [starter()],
    plan: exportPlan(),
    manifest: manifest(),
    captionsSaved: { Saved: 1, MeasuredClassKey: 'google.screenshots.phone', Warnings: [] },
    suggestion: { TargetLocale: 'en-US', Items: [] },
    nextVersion: 5,
    exportJobId: 8812,
  }
}

export const studioState: StudioState = initialStudio()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

function previewOf(body: { SlotOrders?: number[] | null; Locale?: string | null; ClassKey?: string | null }): MockupPreviewDto {
  return {
    ClassKey: body.ClassKey ?? null,
    Locale: body.Locale ?? null,
    Images: (body.SlotOrders ?? [1]).map((order) => ({
      SlotOrder: order,
      Width: 180,
      Height: 390,
      Url: `data:image/png;base64,slot${order}`,
      Warnings: [],
    })),
    DurationMs: 380,
  }
}

export const studioHandlers: HttpHandler[] = [
  endpoint('patch', '/Mockups/templates', () => json(listEnvelope(studioState.templates))),
  endpoint('get', '/Mockups/template', () => json(envelope(studioState.template))),
  endpoint('post', '/Mockups/template', () => json(envelope(40))),
  endpoint('put', '/Mockups/template', () => json(envelope(studioState.nextVersion))),
  endpoint('delete', '/Mockups/template', () => json(envelope(12))),
  endpoint('put', '/Mockups/captions', () => json(envelope(studioState.captionsSaved))),
  endpoint('post', '/Mockups/captions/suggest', () => json(envelope(studioState.suggestion))),
  endpoint('get', '/Mockups/frames', () => json(envelope(studioState.frames))),
  endpoint('get', '/Mockups/starter-templates', () => json(envelope(studioState.starters))),
  endpoint('post', '/Mockups/render-preview', ({ body }) => json(envelope(previewOf(body as Parameters<typeof previewOf>[0])))),
  endpoint('post', '/Mockups/export/plan', () => json(envelope(studioState.plan))),
  endpoint('post', '/Mockups/export', () => json(envelope(studioState.exportJobId))),
  endpoint('get', '/Mockups/export/manifest', () => json(envelope(studioState.manifest))),
  endpoint('get', '/Mockups/export/zip', () =>
    json(envelope({ Url: 'https://storage.test/bucket/mockups-8812.zip', ExpiryDate: '2026-11-18T15:00:00Z' })),
  ),
]

/** Resets the studio state and answers its endpoints for every test of the file. */
export function setupStudioApi(): void {
  beforeEach(() => {
    Object.assign(studioState, initialStudio())
    server.use(...studioHandlers)
  })
}
