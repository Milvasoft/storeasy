import { apiDelete, apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type { AssetDownloadDto } from '@/lib/api/types'
import { normalizeSpec } from './spec'
import type {
  MockupCaptionDto,
  MockupCaptionsSavedDto,
  MockupCaptionSuggestionDto,
  MockupExportManifest,
  MockupExportPlanDto,
  MockupExportTarget,
  MockupFrameDto,
  MockupPreviewDto,
  MockupSlotDto,
  MockupSpec,
  MockupStarterTemplateDto,
  MockupTemplateDetailDto,
  MockupTemplateListDto,
} from './types'

// MockupsController (/api/v1.0/mockups): read and preview need View, writes and exports EditContent.

export const TEMPLATE_CHANGED_KEY = 'MockupTemplateChanged'
export const TEMPLATE_NAME_EXISTS_KEY = 'MockupTemplateNameExists'

export interface CreateTemplateInput {
  AppId: number
  Name: string
  Locales: string[]
  PrimaryLocale: string
  SlotCount?: number | null
  StarterKey?: string | null
  CopyFromTemplateId?: number | null
  CopyCaptions: boolean
}

export interface UpdateTemplateInput {
  TemplateId: number
  Version: number
  Name: string
  Spec: MockupSpec
  Locales: string[]
  PrimaryLocale: string
  SlotCount: number
  Slots: MockupSlotDto[]
}

export interface PreviewInput {
  TemplateId: number
  Locale?: string | null
  ClassKey?: string | null
  SlotOrders?: number[] | null
  /** The unsaved design; the stored one when null. */
  Spec?: MockupSpec | null
  MaxWidth?: number | null
}

export interface ExportSelection {
  TemplateId: number
  Locales?: string[] | null
  ClassKeys?: string[] | null
  Targets?: MockupExportTarget[] | null
}

export interface SuggestInput {
  TemplateId: number
  TargetLocale: string
  SourceLocales?: string[] | null
  SlotOrders?: number[] | null
}

function withSpec(detail: MockupTemplateDetailDto): MockupTemplateDetailDto {
  return { ...detail, Spec: normalizeSpec(detail.Spec) }
}

export const studioApi = {
  templates: (organizationId: number, request: ListRequest, appId?: number | null) =>
    apiPatchList<MockupTemplateListDto>('/Mockups/templates', { OrganizationId: organizationId, AppId: appId ?? null, ...request }),

  // 404 MockupTemplateNotFound.
  template: (organizationId: number, templateId: number) =>
    apiGet<MockupTemplateDetailDto>('/Mockups/template', { OrganizationId: organizationId, TemplateId: templateId }).then(withSpec),

  // From a starter or a copy of another template. 409 MockupTemplateNameExists. Answers the new id.
  create: (organizationId: number, input: CreateTemplateInput) =>
    apiPost<number>('/Mockups/template', { OrganizationId: organizationId, ...input }, { idempotent: true }),

  // Answers the new version; 409 MockupTemplateChanged when Version is stale; 400 MockupSpecInvalid with the paths.
  update: (organizationId: number, input: UpdateTemplateInput) =>
    apiPut<number>('/Mockups/template', { OrganizationId: organizationId, ...input }),

  remove: (organizationId: number, templateId: number) =>
    apiDelete<number>('/Mockups/template', { OrganizationId: organizationId, TemplateId: templateId }),

  // An empty cell removes it. Answers the captions that overflow in the narrowest exported class.
  saveCaptions: (organizationId: number, templateId: number, items: MockupCaptionDto[]) =>
    apiPut<MockupCaptionsSavedDto>('/Mockups/captions', { OrganizationId: organizationId, TemplateId: templateId, Items: items }),

  // Never saved. 503 when Studio.AiCaptions is off or no translator is configured; 429 over 60 an hour.
  suggestCaptions: (organizationId: number, input: SuggestInput) =>
    apiPost<MockupCaptionSuggestionDto>('/Mockups/captions/suggest', { OrganizationId: organizationId, ...input }),

  frames: (organizationId: number) => apiGet<MockupFrameDto[]>('/Mockups/frames', { OrganizationId: organizationId }),

  starters: (organizationId: number) =>
    apiGet<MockupStarterTemplateDto[]>('/Mockups/starter-templates', { OrganizationId: organizationId }).then((list) =>
      (list ?? []).map((starter) => ({ ...starter, Spec: normalizeSpec(starter.Spec) })),
    ),

  // Up to four slots of one locale and class; 429 over 1200 an hour.
  preview: (organizationId: number, input: PreviewInput) =>
    apiPost<MockupPreviewDto>('/Mockups/render-preview', { OrganizationId: organizationId, ...input }),

  exportPlan: (organizationId: number, selection: ExportSelection) =>
    apiPost<MockupExportPlanDto>('/Mockups/export/plan', { OrganizationId: organizationId, ...selection }),

  // Answers the StoreJob id (Kind MockupExport); progress streams on the activity hub.
  startExport: (organizationId: number, selection: ExportSelection) =>
    apiPost<number>('/Mockups/export', { OrganizationId: organizationId, ...selection }, { idempotent: true }),

  // 409 MockupExportNotReady until the job succeeded.
  manifest: (organizationId: number, jobId: number) =>
    apiGet<MockupExportManifest>('/Mockups/export/manifest', { OrganizationId: organizationId, JobId: jobId }),

  zip: (organizationId: number, jobId: number) =>
    apiGet<AssetDownloadDto>('/Mockups/export/zip', { OrganizationId: organizationId, JobId: jobId }),
}
