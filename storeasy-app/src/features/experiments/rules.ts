import { OrgPermission, StoreKind } from '@/lib/api/types'
import {
  AsoExperimentKind,
  AsoExperimentOperation,
  AsoExperimentStatus,
  AsoStoreSupport,
  type AsoExperimentVariantDto,
  type AsoStoreCapabilityDto,
} from './types'

// The limits of each experiment kind (the API checks them again) and the pure helpers of the screens.

export interface KindLimits {
  store: StoreKind
  maxVariants: number
  minVariants: number
  traffic: boolean
  appIcon: boolean
  promotionalText: number | null
  shortDescription: number | null
  fullDescription: number | null
}

export const KIND_LIMITS: Record<AsoExperimentKind, KindLimits> = {
  [AsoExperimentKind.ProductPageOptimization]: {
    store: StoreKind.Apple,
    maxVariants: 3,
    minVariants: 1,
    traffic: true,
    appIcon: true,
    promotionalText: null,
    shortDescription: null,
    fullDescription: null,
  },
  [AsoExperimentKind.CustomProductPage]: {
    store: StoreKind.Apple,
    maxVariants: 1,
    minVariants: 1,
    traffic: false,
    appIcon: false,
    promotionalText: 170,
    shortDescription: null,
    fullDescription: null,
  },
  [AsoExperimentKind.PlayListingExperiment]: {
    store: StoreKind.Google,
    maxVariants: 3,
    minVariants: 1,
    traffic: false,
    appIcon: false,
    promotionalText: null,
    shortDescription: 80,
    fullDescription: 4000,
  },
  [AsoExperimentKind.PlayCustomListing]: {
    store: StoreKind.Google,
    maxVariants: 1,
    minVariants: 1,
    traffic: false,
    appIcon: false,
    promotionalText: null,
    shortDescription: 80,
    fullDescription: 4000,
  },
}

export const VARIANT_KEYS = ['A', 'B', 'C'] as const

export const ALL_KINDS: AsoExperimentKind[] = [
  AsoExperimentKind.ProductPageOptimization,
  AsoExperimentKind.CustomProductPage,
  AsoExperimentKind.PlayListingExperiment,
  AsoExperimentKind.PlayCustomListing,
]

/** Whether the kind is done by hand in the store console (Play), not through the API. */
export function isAssistedKind(kind: AsoExperimentKind): boolean {
  return kind === AsoExperimentKind.PlayListingExperiment || kind === AsoExperimentKind.PlayCustomListing
}

/** The kinds a store record supports (API or assisted), in the order the wizard offers them. */
export function supportedKinds(capability: AsoStoreCapabilityDto | undefined): AsoExperimentKind[] {
  return (capability?.Kinds ?? []).filter((item) => item.Support !== AsoStoreSupport.NotSupported).map((item) => item.Kind)
}

/** A new, empty variant with the next free key. */
export function newVariant(existing: AsoExperimentVariantDto[], locales: string[]): AsoExperimentVariantDto {
  const key = VARIANT_KEYS.find((candidate) => !existing.some((variant) => variant.Key === candidate)) ?? String(existing.length + 1)
  return { Key: key, Name: '', AppIconName: null, Locales: locales.map((locale) => ({ Locale: locale, Media: [] })) }
}

export type DraftProblem =
  | { code: 'name' }
  | { code: 'traffic' }
  | { code: 'variantCount'; max: number }
  | { code: 'variantName'; key: string }
  | { code: 'noLocale'; key: string }
  | { code: 'tooLong'; key: string; locale: string; field: 'promotionalText' | 'shortDescription' | 'fullDescription'; max: number }
  | { code: 'empty'; key: string }

export interface DraftInput {
  kind: AsoExperimentKind
  name: string
  traffic: number | null
  variants: AsoExperimentVariantDto[]
}

/** What keeps a draft from being saved; empty when it can be saved. */
export function draftProblems(input: DraftInput): DraftProblem[] {
  const limits = KIND_LIMITS[input.kind]
  const problems: DraftProblem[] = []
  if (!input.name.trim()) {
    problems.push({ code: 'name' })
  }
  if (limits.traffic && (input.traffic == null || !Number.isInteger(input.traffic) || input.traffic < 1 || input.traffic > 100)) {
    problems.push({ code: 'traffic' })
  }
  if (input.variants.length < limits.minVariants || input.variants.length > limits.maxVariants) {
    problems.push({ code: 'variantCount', max: limits.maxVariants })
  }
  for (const variant of input.variants) {
    if (!variant.Name.trim()) {
      problems.push({ code: 'variantName', key: variant.Key })
    }
    if (variant.Locales.length === 0) {
      problems.push({ code: 'noLocale', key: variant.Key })
    }
    let changes = limits.appIcon && !!variant.AppIconName?.trim()
    for (const locale of variant.Locales) {
      for (const field of ['promotionalText', 'shortDescription', 'fullDescription'] as const) {
        const max = limits[field]
        const value = fieldValue(locale, field)
        if (max != null && value.length > 0) {
          changes = true
          if ([...value].length > max) {
            problems.push({ code: 'tooLong', key: variant.Key, locale: locale.Locale, field, max })
          }
        }
      }
      if (locale.Media.some((media) => media.AssetIds.length > 0)) {
        changes = true
      }
    }
    if (!changes && variant.Locales.length > 0) {
      problems.push({ code: 'empty', key: variant.Key })
    }
  }
  return problems
}

function fieldValue(
  locale: { PromotionalText?: string | null; ShortDescription?: string | null; FullDescription?: string | null },
  field: 'promotionalText' | 'shortDescription' | 'fullDescription',
): string {
  switch (field) {
    case 'promotionalText':
      return locale.PromotionalText ?? ''
    case 'shortDescription':
      return locale.ShortDescription ?? ''
    default:
      return locale.FullDescription ?? ''
  }
}

/** Whether the draft can still be edited in storeasy. */
export function isEditable(status: AsoExperimentStatus): boolean {
  return status === AsoExperimentStatus.Draft || status === AsoExperimentStatus.Prepared
}

/** Whether the experiment has ended (no more store writes but the result). */
export function isFinished(status: AsoExperimentStatus): boolean {
  return status === AsoExperimentStatus.Stopped || status === AsoExperimentStatus.Completed
}

/** The permission an operation's plan needs when it is approved (shown on the buttons; the API decides). */
export function operationPermission(operation: AsoExperimentOperation): OrgPermission {
  switch (operation) {
    case AsoExperimentOperation.Push:
      return OrgPermission.EditContent
    case AsoExperimentOperation.Submit:
      return OrgPermission.SubmitForReview
    default:
      return OrgPermission.ReleaseProduction
  }
}

/** The texts of an experiment as a CSV (one row per variant, locale and field), for the Play Console forms. */
export function textsCsv(rows: Array<{ VariantKey: string; Locale: string; Field: string; Value: string }>): string {
  const quote = (value: string) => `"${value.replace(/"/g, '""')}"`
  return ['variant,locale,field,value', ...rows.map((row) => [row.VariantKey, row.Locale, row.Field, row.Value].map(quote).join(','))].join(
    '\n',
  )
}
