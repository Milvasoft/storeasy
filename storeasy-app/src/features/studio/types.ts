import type { DateOnly, IsoDateTime, StoreKind } from '@/lib/api/types'

// The studio's backend contract (storeasy-api docs/studio.md, MockupsController, Storeasy.Application Dtos/MockupDtos,
// Storeasy.Domain JsonModels/MockupSpec and MockupExportManifest). Keys are PascalCase (pascalizeKeys), enums numeric.

// ---- Enums (Storeasy.Domain.Enums) ----

export const MockupDeviceFamily = { Phone: 1, Tablet: 2, Desktop: 3 } as const
export type MockupDeviceFamily = (typeof MockupDeviceFamily)[keyof typeof MockupDeviceFamily]

export const MockupFrameKind = { None: 1, Phone: 2, Tablet: 3, FoldableOuter: 4, FoldableInner: 5, Desktop: 6 } as const
export type MockupFrameKind = (typeof MockupFrameKind)[keyof typeof MockupFrameKind]

export const MockupRequirement = { Required: 1, Recommended: 2, Optional: 3 } as const
export type MockupRequirement = (typeof MockupRequirement)[keyof typeof MockupRequirement]

/** StoreJobKind of an export job (the shared StoreJobKind lists only the kinds the shell knows). */
export const MOCKUP_EXPORT_JOB_KIND = 60

// ---- Template spec (MockupSpec, schemaVersion 1) ----
// The spec's string fields are the schema's lower case tokens; lengths are fractions of the canvas (see docs/studio.md).

export type BackgroundKind = 'solid' | 'gradient' | 'image'
export type FrameSpecKind = 'auto' | 'none' | 'phone' | 'tablet' | 'foldable' | 'desktop'
export type FrameCutout = 'island' | 'notch' | 'hole' | 'none'
export type TextFont = 'inter' | 'noto-sans'
export type TextPosition = 'top' | 'bottom'
export type TextAlignment = 'center' | 'start' | 'end'
export type LayerType = 'shape' | 'badge'
export type ShapeKind = 'rect' | 'round-rect' | 'ellipse'

export interface MockupCanvasSpec {
  ClassKey?: string | null
}

export interface MockupBackgroundSpec {
  Kind: BackgroundKind | string
  Colors?: string[] | null
  Angle: number
  AssetId?: number | null
}

export interface MockupFrameSpec {
  Kind: FrameSpecKind | string
  Cutout?: FrameCutout | string | null
  Color?: string | null
  CornerRadius?: number | null
  Bezel?: number | null
  Scale: number
  OffsetX: number
  OffsetY: number
  Rotation: number
  Shadow: boolean
}

export interface MockupTextStyleSpec {
  Size: number
  Weight: number
  MaxLines: number
  AutoFit: boolean
  MinSize: number
  LineHeight: number
  Color?: string | null
}

export interface MockupTextSpec {
  Font: TextFont | string
  Color?: string | null
  Position: TextPosition | string
  Alignment: TextAlignment | string
  Margin: number
  Gap: number
  Uppercase: boolean
  Title?: MockupTextStyleSpec | null
  Subtitle?: MockupTextStyleSpec | null
}

export interface MockupLayerSpec {
  Type: LayerType | string
  Shape?: ShapeKind | string | null
  X: number
  Y: number
  Width: number
  Height: number
  Radius: number
  Color?: string | null
  Opacity: number
  Above: boolean
  /** Badge text per locale; "*" is every locale. Keys are locale tags, never pascalized (see normalizeSpec). */
  Texts?: Record<string, string> | null
  TextSize: number
  TextColor?: string | null
}

export interface MockupSpec {
  SchemaVersion: number
  Canvas?: MockupCanvasSpec | null
  Background?: MockupBackgroundSpec | null
  Frame?: MockupFrameSpec | null
  Text?: MockupTextSpec | null
  Layers?: MockupLayerSpec[] | null
}

// ---- Templates ----

export interface MockupTemplateListDto {
  Id: number
  AppId: number
  AppName?: string | null
  Name: string
  PrimaryLocale: string
  Locales?: string[] | null
  SlotCount: number
  StarterKey?: string | null
  /** Concurrency version: sent back with an update. */
  Version: number
  CreationDate?: IsoDateTime | null
  LastModificationDate?: IsoDateTime | null
  LastModifierUserName?: string | null
  LastExportJobId?: number | null
  LastExportDate?: IsoDateTime | null
  LastExportImageCount?: number | null
}

export interface MockupSlotDto {
  Order: number
  Family: MockupDeviceFamily
  /** Null: the screenshot is used for every locale. */
  Locale?: string | null
  AssetId: number
}

export interface MockupCaptionDto {
  Locale: string
  SlotOrder: number
  Title?: string | null
  Subtitle?: string | null
}

export interface MockupTemplateDetailDto extends MockupTemplateListDto {
  Spec: MockupSpec
  Slots?: MockupSlotDto[] | null
  Captions?: MockupCaptionDto[] | null
}

export interface MockupStarterTemplateDto {
  Key: string
  Name: string
  NameTr?: string | null
  Description?: string | null
  DescriptionTr?: string | null
  SlotCount: number
  SampleLanguages?: string[] | null
  Spec: MockupSpec
}

// ---- Warnings, captions, previews ----

export interface MockupWarningDto {
  /** CaptionOverflow, CaptionFallback, FamilyFallback, AspectMismatch, AlphaFlattened, FontMissing, AssetTooSmall... */
  Code: string
  Message?: string | null
  ClassKey?: string | null
  Locale?: string | null
  SlotOrder?: number | null
  /** "title" or "subtitle" for caption warnings. */
  Field?: string | null
  Lines?: number | null
  MaxLines?: number | null
}

export interface MockupCaptionsSavedDto {
  Saved: number
  MeasuredClassKey?: string | null
  Warnings?: MockupWarningDto[] | null
}

export interface MockupCaptionSuggestionItemDto {
  SlotOrder: number
  /** "title" or "subtitle". */
  Field: string
  Before?: string | null
  After: string
  Reason?: string | null
}

export interface MockupCaptionSuggestionDto {
  TargetLocale: string
  Items?: MockupCaptionSuggestionItemDto[] | null
}

export interface MockupPreviewImageDto {
  SlotOrder: number
  Width: number
  Height: number
  /** data:image/png;base64 URL; null when the image failed. */
  Url?: string | null
  Warnings?: MockupWarningDto[] | null
}

export interface MockupPreviewDto {
  ClassKey?: string | null
  Locale?: string | null
  Images?: MockupPreviewImageDto[] | null
  DurationMs: number
}

// ---- Frames ----

export interface MockupClassDto {
  ClassKey: string
  Store: StoreKind
  Label: string
  LabelTr?: string | null
  Width: number
  Height: number
  Family: MockupDeviceFamily
  Status: MockupRequirement
  RequiredFrom?: DateOnly | null
  MaxCount: number
}

export interface MockupFrameDto {
  Kind: MockupFrameKind
  CornerRadius: number
  Bezel: number
  Cutouts?: string[] | null
  Classes?: MockupClassDto[] | null
}

// ---- Export ----

export interface MockupExportTarget {
  ClassKey: string
  Width: number
  Height: number
}

export interface MockupExportPlanItemDto {
  ClassKey: string
  Store: StoreKind
  Label: string
  LabelTr?: string | null
  Width: number
  Height: number
  Frame: MockupFrameKind
  Status: MockupRequirement
  RequiredFrom?: DateOnly | null
  Selected: boolean
  PerLocaleCount: number
  Note?: string | null
}

export interface MockupExportRejectedDto {
  ClassKey: string
  Width: number
  Height: number
  Reason?: string | null
}

export interface MockupExportPlanDto {
  TemplateId: number
  CatalogVersion?: string | null
  Locales?: string[] | null
  Items?: MockupExportPlanItemDto[] | null
  Rejected?: MockupExportRejectedDto[] | null
  Total: number
  Warnings?: MockupWarningDto[] | null
}

export interface MockupExportImage {
  SlotOrder: number
  AssetId: number
  Size: number
  Sha256?: string | null
  Md5?: string | null
  ZipPath?: string | null
}

export interface MockupExportSet {
  Store: StoreKind
  ClassKey: string
  /** The store's own key (MediaClasses); null where the store API has none yet (iPhone Duo, Microsoft Store). */
  StoreClassKey?: string | null
  Locale: string
  Width: number
  Height: number
  Images?: MockupExportImage[] | null
}

export interface MockupExportIssue {
  ClassKey?: string | null
  Locale?: string | null
  SlotOrder: number
  Code: string
  Message?: string | null
}

export interface MockupExportManifest {
  SchemaVersion: number
  JobId: number
  TemplateId: number
  AppId: number
  TemplateVersion: number
  CatalogVersion?: string | null
  CreatedDate: IsoDateTime
  ZipAssetId?: number | null
  ImageCount: number
  TotalBytes: number
  Sets?: MockupExportSet[] | null
  Failures?: MockupExportIssue[] | null
  Warnings?: MockupExportIssue[] | null
}
