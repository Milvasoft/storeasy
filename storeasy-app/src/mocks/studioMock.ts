import { delay, http, HttpResponse, type HttpHandler } from 'msw'
import {
  MockupDeviceFamily,
  MockupFrameKind,
  MockupRequirement,
  type MockupCaptionDto,
  type MockupClassDto,
  type MockupExportManifest,
  type MockupExportPlanDto,
  type MockupFrameDto,
  type MockupSpec,
  type MockupTemplateDetailDto,
} from '@/features/studio/types'
import { defaultSpec } from '@/features/studio/spec'
import { ActionChannel, OrgPermission, StoreJobStatus, StoreJobStepStatus, StepDirection, StoreKind } from '@/lib/api/types'

// The dev mock's studio (design 12 sample data): templates, frames, previews drawn as SVG, export plans and a finished
// export. Same paths and envelopes as storeasy-api MockupsController. In memory; a reload starts over.

const BASE = '*/api/v1.0'
const EXPORT_JOB_ID = 88_120

function envelope<T>(data: T) {
  return HttpResponse.json({ isSuccess: true, statusCode: 200, messages: [], data })
}

function refuse(key: string, message: string, status: number) {
  return HttpResponse.json({ isSuccess: false, statusCode: status, messages: [{ key, message, type: 3 }], data: null }, { status })
}

async function body<T>(request: Request): Promise<T> {
  return (await request.json()) as T
}

function cls(
  ClassKey: string,
  Store: StoreKind,
  Label: string,
  LabelTr: string,
  Width: number,
  Height: number,
  Family: MockupDeviceFamily,
  Status: MockupRequirement,
): MockupClassDto {
  return { ClassKey, Store, Label, LabelTr, Width, Height, Family, Status, RequiredFrom: null, MaxCount: 10 }
}

const frames: MockupFrameDto[] = [
  {
    Kind: MockupFrameKind.Phone,
    CornerRadius: 0.13,
    Bezel: 0.025,
    Cutouts: ['island', 'notch', 'hole', 'none'],
    Classes: [
      cls(
        'apple.screenshots.iphone-dynamic-island-medium',
        StoreKind.Apple,
        'iPhone DI medium',
        'iPhone DI orta',
        1206,
        2622,
        MockupDeviceFamily.Phone,
        MockupRequirement.Required,
      ),
      cls(
        'apple.screenshots.iphone-dynamic-island-large',
        StoreKind.Apple,
        'iPhone DI large',
        'iPhone DI büyük',
        1320,
        2868,
        MockupDeviceFamily.Phone,
        MockupRequirement.Recommended,
      ),
      cls(
        'google.screenshots.phone',
        StoreKind.Google,
        'Phone',
        'Telefon',
        1080,
        1920,
        MockupDeviceFamily.Phone,
        MockupRequirement.Required,
      ),
    ],
  },
  {
    Kind: MockupFrameKind.Tablet,
    CornerRadius: 0.05,
    Bezel: 0.03,
    Cutouts: ['hole', 'none'],
    Classes: [
      cls(
        'apple.screenshots.ipad-13',
        StoreKind.Apple,
        'iPad 13"',
        'iPad 13"',
        2064,
        2752,
        MockupDeviceFamily.Tablet,
        MockupRequirement.Required,
      ),
    ],
  },
  {
    Kind: MockupFrameKind.FoldableInner,
    CornerRadius: 0.07,
    Bezel: 0.035,
    Cutouts: ['hole'],
    Classes: [
      cls(
        'apple.screenshots.iphone-duo-inner',
        StoreKind.Apple,
        'iPhone Duo inner display',
        'iPhone Duo iç ekran',
        2007,
        2853,
        MockupDeviceFamily.Tablet,
        MockupRequirement.Optional,
      ),
    ],
  },
  {
    Kind: MockupFrameKind.Desktop,
    CornerRadius: 0.01,
    Bezel: 0,
    Cutouts: [],
    Classes: [
      cls(
        'microsoft.screenshots.desktop',
        StoreKind.Microsoft,
        'Desktop',
        'Masaüstü',
        1920,
        1080,
        MockupDeviceFamily.Desktop,
        MockupRequirement.Required,
      ),
    ],
  },
]

function spec(color: string): MockupSpec {
  const base = defaultSpec()
  return {
    ...base,
    Canvas: { ClassKey: 'apple.screenshots.iphone-dynamic-island-medium' },
    Background: { Kind: 'gradient', Colors: [color, '#0F4C45'], Angle: 160, AssetId: null },
  }
}

const captions: MockupCaptionDto[] = [
  { Locale: 'tr', SlotOrder: 1, Title: 'Hedeflerin tek bakışta', Subtitle: 'Bütün rotan tek ekranda' },
  { Locale: 'tr', SlotOrder: 2, Title: 'Her adımı işaretle', Subtitle: 'Küçük adımlar, düzenli ilerleme' },
  { Locale: 'tr', SlotOrder: 3, Title: 'İlerlemeni gör', Subtitle: 'Haftalık grafikler kendiliğinden' },
  { Locale: 'en-US', SlotOrder: 1, Title: 'Your goals at a glance', Subtitle: 'Your whole route on one screen' },
  { Locale: 'en-US', SlotOrder: 2, Title: 'Check off every step', Subtitle: 'Small steps, steady progress' },
]

const now = Date.now()
const templates: MockupTemplateDetailDto[] = [
  {
    Id: 12,
    AppId: 101,
    AppName: 'Pusula',
    Name: 'Mağaza seti 2026',
    PrimaryLocale: 'tr',
    Locales: ['tr', 'en-US'],
    SlotCount: 4,
    StarterKey: 'classic',
    Version: 4,
    CreationDate: new Date(now - 9 * 86_400_000).toISOString(),
    LastModificationDate: new Date(now - 3_600_000).toISOString(),
    LastModifierUserName: 'ece@kuzeylab.dev',
    LastExportJobId: EXPORT_JOB_ID,
    LastExportDate: new Date(now - 6 * 86_400_000).toISOString(),
    LastExportImageCount: 24,
    Spec: spec('#14B8A6'),
    Slots: [
      { Order: 1, Family: MockupDeviceFamily.Phone, Locale: null, AssetId: 9001 },
      { Order: 2, Family: MockupDeviceFamily.Phone, Locale: null, AssetId: 9002 },
    ],
    Captions: captions,
  },
]
let nextId = 40

function preview(order: number, title: string, color: string, width: number, height: number): string {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}"><rect width="100%" height="100%" fill="${color}"/><text x="50%" y="12%" fill="#fff" font-family="sans-serif" font-size="${Math.round(width / 14)}" text-anchor="middle">${title.replace(/[<&]/g, '')}</text><rect x="${width * 0.16}" y="${height * 0.25}" width="${width * 0.68}" height="${height * 0.7}" rx="${width * 0.08}" fill="#1C1C22"/><text x="50%" y="62%" fill="#fff" font-family="sans-serif" font-size="${Math.round(width / 8)}" text-anchor="middle">${order}</text></svg>`
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`
}

function planOf(template: MockupTemplateDetailDto, locales: string[] | null, classKeys: string[] | null): MockupExportPlanDto {
  const all = frames.flatMap((frame) => (frame.Classes ?? []).map((item) => ({ item, frame: frame.Kind })))
  const chosenLocales = locales ?? template.Locales ?? []
  const items = all.map(({ item, frame }) => ({
    ClassKey: item.ClassKey,
    Store: item.Store,
    Label: item.Label,
    LabelTr: item.LabelTr,
    Width: item.Width,
    Height: item.Height,
    Frame: frame,
    Status: item.Status,
    RequiredFrom: null,
    Selected: classKeys ? classKeys.includes(item.ClassKey) : item.Status !== MockupRequirement.Optional,
    PerLocaleCount: template.SlotCount,
    Note: null,
  }))
  return {
    TemplateId: template.Id,
    CatalogVersion: '1.0.1',
    Locales: chosenLocales,
    Items: items,
    Rejected: [
      {
        ClassKey: 'apple.screenshots.iphone-dynamic-island-medium',
        Width: 1320,
        Height: 2868,
        Reason: 'The 6.1"/6.3" slot accepts only 1206x2622 and 1179x2556.',
      },
    ],
    Total: items.filter((item) => item.Selected).length * chosenLocales.length * template.SlotCount,
    Warnings: [],
  }
}

function manifest(template: MockupTemplateDetailDto): MockupExportManifest {
  const locales = template.Locales ?? []
  const sets = frames
    .flatMap((frame) => frame.Classes ?? [])
    .filter((item) => item.Status !== MockupRequirement.Optional)
    .flatMap((item) =>
      locales.map((locale) => ({
        Store: item.Store,
        ClassKey: item.ClassKey,
        StoreClassKey: item.Store === StoreKind.Microsoft ? null : (item.ClassKey.split('.').pop()?.toUpperCase() ?? null),
        Locale: locale,
        Width: item.Width,
        Height: item.Height,
        Images: Array.from({ length: template.SlotCount }, (_, index) => ({
          SlotOrder: index + 1,
          AssetId: 70_000 + index,
          Size: 900_000,
          Sha256: null,
          Md5: null,
          ZipPath: `${item.ClassKey}/${locale}/0${index + 1}.png`,
        })),
      })),
    )
  return {
    SchemaVersion: 1,
    JobId: EXPORT_JOB_ID,
    TemplateId: template.Id,
    AppId: template.AppId,
    TemplateVersion: template.Version,
    CatalogVersion: '1.0.1',
    CreatedDate: new Date(now - 6 * 86_400_000).toISOString(),
    ZipAssetId: 991,
    ImageCount: sets.reduce((sum, set) => sum + set.Images.length, 0),
    TotalBytes: 21_600_000,
    Sets: sets,
    Failures: [],
    Warnings: [],
  }
}

function find(request: Request, key = 'TemplateId'): MockupTemplateDetailDto | undefined {
  return templates.find((item) => item.Id === Number(new URL(request.url).searchParams.get(key)))
}

export const studioMockHandlers: HttpHandler[] = [
  http.patch(`${BASE}/Mockups/templates`, () => {
    const rows = templates.map(({ Spec: _spec, Slots: _slots, Captions: _captions, ...row }) => row)
    return HttpResponse.json({
      isSuccess: true,
      statusCode: 200,
      messages: [],
      data: rows,
      currentPageNumber: 1,
      totalPageCount: 1,
      totalDataCount: rows.length,
    })
  }),
  http.get(`${BASE}/Mockups/template`, ({ request }) => {
    const template = find(request)
    return template ? envelope(template) : refuse('MockupTemplateNotFound', 'Şablon bulunamadı.', 404)
  }),
  http.post(`${BASE}/Mockups/template`, async ({ request }) => {
    const input = await body<{ AppId: number; Name: string; Locales: string[]; PrimaryLocale: string; CopyFromTemplateId?: number | null }>(
      request,
    )
    const source = templates.find((item) => item.Id === input.CopyFromTemplateId)
    const id = nextId++
    templates.push({
      ...(source ?? templates[0]),
      Id: id,
      AppId: input.AppId,
      Name: input.Name,
      Locales: input.Locales,
      PrimaryLocale: input.PrimaryLocale,
      Version: 1,
      LastExportJobId: null,
      LastExportDate: null,
      LastExportImageCount: null,
      Captions: source ? source.Captions : [],
      Slots: source ? source.Slots : [],
    })
    return envelope(id)
  }),
  http.put(`${BASE}/Mockups/template`, async ({ request }) => {
    const input = await body<MockupTemplateDetailDto & { TemplateId: number }>(request)
    const template = templates.find((item) => item.Id === input.TemplateId)
    if (!template) {
      return refuse('MockupTemplateNotFound', 'Şablon bulunamadı.', 404)
    }
    if (template.Version !== input.Version) {
      return refuse('MockupTemplateChanged', 'Şablon siz açtıktan sonra değişti.', 409)
    }
    Object.assign(template, {
      Name: input.Name,
      Spec: input.Spec,
      Locales: input.Locales,
      PrimaryLocale: input.PrimaryLocale,
      SlotCount: input.SlotCount,
      Slots: input.Slots,
      Version: template.Version + 1,
    })
    return envelope(template.Version)
  }),
  http.delete(`${BASE}/Mockups/template`, ({ request }) => {
    const template = find(request)
    templates.splice(templates.indexOf(template!), 1)
    return envelope(template?.Id ?? 0)
  }),
  http.put(`${BASE}/Mockups/captions`, async ({ request }) => {
    const input = await body<{ TemplateId: number; Items: MockupCaptionDto[] }>(request)
    const template = templates.find((item) => item.Id === input.TemplateId)
    const warnings = input.Items.filter((item) => (item.Title ?? '').length > 30).map((item) => ({
      Code: 'CaptionOverflow',
      Message: 'Title needs 3 lines.',
      ClassKey: 'google.screenshots.phone',
      Locale: item.Locale,
      SlotOrder: item.SlotOrder,
      Field: 'title',
      Lines: 3,
      MaxLines: 2,
    }))
    if (template) {
      const rest = (template.Captions ?? []).filter(
        (caption) => !input.Items.some((item) => item.Locale === caption.Locale && item.SlotOrder === caption.SlotOrder),
      )
      template.Captions = [...rest, ...input.Items.filter((item) => item.Title || item.Subtitle)]
    }
    return envelope({ saved: input.Items.length, measuredClassKey: 'google.screenshots.phone', warnings })
  }),
  http.post(`${BASE}/Mockups/captions/suggest`, () => refuse('FeatureDisabled', 'Yapay zeka önerisi bu kurulumda kapalı.', 503)),
  http.get(`${BASE}/Mockups/frames`, () => envelope(frames)),
  http.get(`${BASE}/Mockups/starter-templates`, () =>
    envelope([
      {
        key: 'classic',
        name: 'Classic',
        nameTr: 'Klasik',
        description: 'Caption on top, phone below',
        descriptionTr: 'Başlık üstte, telefon altta',
        slotCount: 6,
        sampleLanguages: ['tr', 'en-US'],
        spec: defaultSpec(),
      },
    ]),
  ),
  http.post(`${BASE}/Mockups/render-preview`, async ({ request }) => {
    await delay(380)
    const input = await body<{ TemplateId: number; Locale?: string; SlotOrders?: number[]; Spec?: MockupSpec }>(request)
    const template = templates.find((item) => item.Id === input.TemplateId)
    const color = input.Spec?.Background?.Colors?.[0] ?? '#4636FF'
    return envelope({
      classKey: input.Spec?.Canvas?.ClassKey ?? null,
      locale: input.Locale,
      images: (input.SlotOrders ?? [1, 2, 3]).map((order) => {
        const caption = template?.Captions?.find((item) => item.Locale === input.Locale && item.SlotOrder === order)
        return { slotOrder: order, width: 300, height: 652, url: preview(order, caption?.Title ?? '', color, 300, 652), warnings: [] }
      }),
      durationMs: 380,
    })
  }),
  http.post(`${BASE}/Mockups/export/plan`, async ({ request }) => {
    const input = await body<{ TemplateId: number; Locales?: string[] | null; ClassKeys?: string[] | null }>(request)
    const template = templates.find((item) => item.Id === input.TemplateId)
    return template
      ? envelope(planOf(template, input.Locales ?? null, input.ClassKeys ?? null))
      : refuse('MockupTemplateNotFound', 'Şablon bulunamadı.', 404)
  }),
  http.post(`${BASE}/Mockups/export`, () => envelope(EXPORT_JOB_ID)),
  http.get(`${BASE}/Mockups/export/manifest`, () => envelope(manifest(templates[0]))),
  http.get(`${BASE}/Mockups/export/zip`, () => envelope({ url: 'about:blank', expiryDate: new Date(now + 600_000).toISOString() })),
  http.post(`${BASE}/Screenshots/plan`, async ({ request }) => {
    const input = await body<{ StoreAppId: number; Locale: string; ClassKey: string; AssetIds: number[] }>(request)
    return envelope({
      planId: 5000 + input.AssetIds.length + input.Locale.length,
      kind: 6,
      title: `Screenshots ${input.ClassKey} ${input.Locale}`,
      appId: 101,
      appName: 'Pusula',
      storeAppId: input.StoreAppId,
      store: StoreKind.Apple,
      source: ActionChannel.Web,
      createdByUserName: 'deniz@kuzeylab.dev',
      requiredPermission: OrgPermission.EditContent,
      confirmRequired: false,
      itemCount: input.AssetIds.length,
      warningCount: 0,
      status: 1,
      expiryDate: new Date(Date.now() + 3_600_000).toISOString(),
    })
  }),
  // The export job: finished, with one render step per class. Other job ids fall through to the apps mock.
  http.get(`${BASE}/StoreJobs/job`, ({ request }) => {
    if (Number(new URL(request.url).searchParams.get('JobId')) !== EXPORT_JOB_ID) {
      return undefined
    }
    return envelope({
      id: EXPORT_JOB_ID,
      kind: 60,
      status: StoreJobStatus.Succeeded,
      store: null,
      appId: 101,
      channel: ActionChannel.Web,
      requestedByUserName: 'deniz@kuzeylab.dev',
      attempt: 1,
      maxAttempts: 3,
      cancelRequested: false,
      summary: 'Mockup export',
      steps: [
        {
          order: 1,
          attempt: 1,
          name: 'render.size',
          direction: StepDirection.Internal,
          status: StoreJobStepStatus.Succeeded,
          progress: 8,
          total: 8,
          message: 'iPhone DI medium',
          startDate: new Date(now).toISOString(),
        },
      ],
    })
  }),
]
