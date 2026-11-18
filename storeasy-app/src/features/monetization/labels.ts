import type { BadgeTone } from '@/components/ui/Badge'
import type { MoneyDictionary } from './i18n'
import { OfferMode, PriceSource, ProductState, StoreProductKind, type BasePriceDto } from './types'

// Enum labels and tones of the phase 7 screens, one switch per enum.

export interface StateView {
  label: string
  tone: BadgeTone
}

export function productStateView(state: ProductState, t: MoneyDictionary): StateView {
  const labels = t.enums.state
  switch (state) {
    case ProductState.Draft:
      return { label: labels.draft, tone: 'neutral' }
    case ProductState.MissingMetadata:
      return { label: labels.missingMetadata, tone: 'warning' }
    case ProductState.ReadyToSubmit:
      return { label: labels.readyToSubmit, tone: 'info' }
    case ProductState.WaitingForReview:
      return { label: labels.waitingForReview, tone: 'info' }
    case ProductState.InReview:
      return { label: labels.inReview, tone: 'info' }
    case ProductState.ActionNeeded:
      return { label: labels.actionNeeded, tone: 'warning' }
    case ProductState.Approved:
      return { label: labels.approved, tone: 'success' }
    case ProductState.Active:
      return { label: labels.active, tone: 'success' }
    case ProductState.Inactive:
      return { label: labels.inactive, tone: 'neutral' }
    case ProductState.Rejected:
      return { label: labels.rejected, tone: 'danger' }
    default:
      return { label: labels.removed, tone: 'neutral' }
  }
}

/** Whether the store sells the product now (MonetizationRules.IsOnSale): its price changes need ReleaseProduction. */
export function isOnSale(state: ProductState): boolean {
  return state === ProductState.Approved || state === ProductState.Active
}

export function productKindLabel(kind: StoreProductKind, t: MoneyDictionary): string {
  const labels = t.enums.kind
  switch (kind) {
    case StoreProductKind.Subscription:
      return labels.subscription
    case StoreProductKind.Consumable:
      return labels.consumable
    case StoreProductKind.NonConsumable:
      return labels.nonConsumable
    case StoreProductKind.NonRenewingSubscription:
      return labels.nonRenewing
    default:
      return labels.oneTime
  }
}

export function priceSourceLabel(source: PriceSource, t: MoneyDictionary): string {
  switch (source) {
    case PriceSource.Manual:
      return t.enums.source.manual
    case PriceSource.Converted:
      return t.enums.source.converted
    default:
      return t.enums.source.store
  }
}

export function offerModeLabel(mode: OfferMode | null | undefined, t: MoneyDictionary): string {
  switch (mode) {
    case OfferMode.FreeTrial:
      return t.enums.mode.freeTrial
    case OfferMode.PayAsYouGo:
      return t.enums.mode.payAsYouGo
    case OfferMode.PayUpFront:
      return t.enums.mode.payUpFront
    default:
      return '-'
  }
}

// App Store subscription periods and offer durations (MonetizationRules.SubscriptionPeriods, OfferDurations).
export const SUBSCRIPTION_PERIODS = ['ONE_WEEK', 'ONE_MONTH', 'TWO_MONTHS', 'THREE_MONTHS', 'SIX_MONTHS', 'ONE_YEAR'] as const
export const OFFER_DURATIONS = [
  'THREE_DAYS',
  'ONE_WEEK',
  'TWO_WEEKS',
  'ONE_MONTH',
  'TWO_MONTHS',
  'THREE_MONTHS',
  'SIX_MONTHS',
  'ONE_YEAR',
] as const

/** An App Store duration ("ONE_MONTH") or a Play ISO 8601 period ("P1M") in words; unknown values as they are. */
export function durationLabel(duration: string | null | undefined, t: MoneyDictionary): string {
  if (!duration) {
    return '-'
  }
  const known = (t.enums.duration as Record<string, string>)[duration]
  if (known) {
    return known
  }
  const iso = /^P(\d+)([YMWD])$/.exec(duration)
  if (!iso) {
    return duration
  }
  const units: Record<string, string> = { Y: t.enums.iso.year, M: t.enums.iso.month, W: t.enums.iso.week, D: t.enums.iso.day }
  return `${iso[1]} ${units[iso[2]]}`
}

/** "49.99 TRY": the amount exactly as the store sent it, never recalculated. */
export function priceText(amount: string | null | undefined, currency: string | null | undefined): string {
  if (!amount) {
    return '-'
  }
  return currency ? `${amount} ${currency}` : amount
}

export function basePriceText(price: BasePriceDto | null | undefined): string {
  return price ? priceText(price.Amount, price.Currency) : '-'
}

// Sandbox renewal rates of App Store Connect (sandboxTesters subscriptionRenewalRate).
export const RENEWAL_RATES = [
  'MONTHLY_RENEWAL_EVERY_ONE_HOUR',
  'MONTHLY_RENEWAL_EVERY_THIRTY_MINUTES',
  'MONTHLY_RENEWAL_EVERY_FIFTEEN_MINUTES',
  'MONTHLY_RENEWAL_EVERY_FIVE_MINUTES',
  'MONTHLY_RENEWAL_EVERY_THREE_MINUTES',
] as const

export function renewalRateLabel(rate: string | null | undefined, t: MoneyDictionary): string {
  return rate ? ((t.enums.renewalRate as Record<string, string>)[rate] ?? rate) : '-'
}
