import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Field, Input, PageHeader } from '@/components/ui/primitives'
import { splitList } from '@/features/common/text'
import { msApi } from './api'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import { MsWorkspace, useMsPlan, type MsContext } from './MsWorkspace'
import { MsChangeKind, MsProductKind, type MsPricingDto } from './types'

// Design 7.22: price and availability. MSIX apps: base price tier, trial and visibility; the price itself is assisted on
// Pricing Version 2. MSI/EXE products: markets, discoverability, pricing model and free trial. Only changed values are sent;
// a null value stays as it is in the store.

export const TRIAL_PERIODS = ['NoFreeTrial', 'OneDay', 'SevenDays', 'FifteenDays', 'ThirtyDays', 'TrialNeverExpires']
export const VISIBILITIES = ['Public', 'Private', 'Hidden']
export const DISCOVERABILITIES = ['DISCOVERABLE', 'DEEPLINK_ONLY']
export const PRICING_MODELS = ['FREE', 'FREEMIUM', 'SUBSCRIPTION', 'PAID']
export const FREE_TRIALS = ['NO_FREE_TRIAL', 'FREE_TRIAL']

type PricingDraft = Record<'PriceId' | 'TrialPeriod' | 'Visibility' | 'Markets' | 'Discoverability' | 'PricingModel' | 'FreeTrial', string>

function draftOf(pricing: MsPricingDto | null | undefined): PricingDraft {
  return {
    PriceId: pricing?.PriceId ?? '',
    TrialPeriod: pricing?.TrialPeriod ?? '',
    Visibility: pricing?.Visibility ?? '',
    Markets: (pricing?.Markets ?? []).join(', '),
    Discoverability: pricing?.Discoverability ?? '',
    PricingModel: pricing?.PricingModel ?? '',
    FreeTrial: pricing?.FreeTrial ?? '',
  }
}

/** The pricing change: the changed values only (null keeps the store's). Null when nothing changed. */
export function pricingChange(draft: PricingDraft, original: PricingDraft): MsPricingDto | null {
  const pick = (key: keyof PricingDraft) => (draft[key] !== original[key] ? draft[key].trim() || null : null)
  const change: MsPricingDto = {
    PriceId: pick('PriceId'),
    TrialPeriod: pick('TrialPeriod'),
    Visibility: pick('Visibility'),
    Markets: draft.Markets !== original.Markets ? splitList(draft.Markets).map((code) => code.toUpperCase()) : null,
    Discoverability: pick('Discoverability'),
    PricingModel: pick('PricingModel'),
    FreeTrial: pick('FreeTrial'),
  }
  return Object.values(change).some((value) => value !== null) ? change : null
}

function options(values: string[]) {
  return values.map((value) => ({ value, label: value }))
}

function Pricing({ context, pricing, pricingV2 }: { context: MsContext; pricing: MsPricingDto | null | undefined; pricingV2: boolean }) {
  const t = useMs()
  const { product, editable, app } = context
  const original = draftOf(pricing)
  const [draft, setDraft] = useState<PricingDraft>(original)
  const plan = useMsPlan(context)
  const msiExe = product.Kind === MsProductKind.MsiExe
  const change = pricingChange(draft, original)
  const set = (key: keyof PricingDraft) => (value: string) => setDraft((current) => ({ ...current, [key]: value }))

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (change) {
      plan.make({ Kind: MsChangeKind.Pricing, Pricing: change })
    }
  }

  return (
    <form className="grid max-w-2xl gap-4" noValidate onSubmit={submit}>
      <PageHeader title={t.pricing.title} subtitle={t.pricing.lead} />
      {plan.element}
      {msiExe ? (
        <>
          <Field label={t.pricing.markets} hint={t.pricing.marketsHint}>
            <Input mono value={draft.Markets} disabled={!editable} onChange={(event) => set('Markets')(event.target.value)} />
          </Field>
          <Field label={t.pricing.discoverability}>
            <Select
              aria-label={t.pricing.discoverability}
              value={draft.Discoverability || null}
              disabled={!editable}
              options={options(DISCOVERABILITIES)}
              onChange={set('Discoverability')}
            />
          </Field>
          <Field label={t.pricing.pricingModel}>
            <Select
              aria-label={t.pricing.pricingModel}
              value={draft.PricingModel || null}
              disabled={!editable}
              options={options(PRICING_MODELS)}
              onChange={set('PricingModel')}
            />
          </Field>
          <Field label={t.pricing.freeTrial}>
            <Select
              aria-label={t.pricing.freeTrial}
              value={draft.FreeTrial || null}
              disabled={!editable}
              options={options(FREE_TRIALS)}
              onChange={set('FreeTrial')}
            />
          </Field>
        </>
      ) : (
        <>
          {pricingV2 ? (
            <Callout tone="info" title={t.pricing.v2Title}>
              <span className="block">{t.pricing.v2Body}</span>
              <Link className="lnk mt-1 inline-block" to={`/apps/${app.Id}/assisted?store=microsoft`}>
                {t.plan.openAssisted}
              </Link>
            </Callout>
          ) : (
            <Field label={t.pricing.price} hint={t.pricing.priceHint}>
              <Input mono value={draft.PriceId} disabled={!editable} onChange={(event) => set('PriceId')(event.target.value)} />
            </Field>
          )}
          <Field label={t.pricing.trial}>
            <Select
              aria-label={t.pricing.trial}
              value={draft.TrialPeriod || null}
              disabled={!editable}
              options={options(TRIAL_PERIODS)}
              onChange={set('TrialPeriod')}
            />
          </Field>
          <Field label={t.pricing.visibility}>
            <Select
              aria-label={t.pricing.visibility}
              value={draft.Visibility || null}
              disabled={!editable}
              options={options(VISIBILITIES)}
              onChange={set('Visibility')}
            />
          </Field>
        </>
      )}
      {editable ? (
        <div className="flex flex-wrap items-center gap-3">
          <Button type="submit" variant="primary" disabled={!change} loading={plan.pending}>
            {t.planMake}
          </Button>
          {!change ? <span className="text-xs text-fg3">{t.pricing.noChanges}</span> : null}
        </div>
      ) : null}
    </form>
  )
}

function PricingScreen({ context }: { context: MsContext }) {
  const { organizationId, record } = context
  const content = useQuery({
    queryKey: msKeys.content(organizationId, record.StoreAppId, null, null),
    queryFn: () => msApi.content(organizationId, record.StoreAppId),
  })
  if (content.isLoading) {
    return <CardSkeleton />
  }
  if (content.isError || !content.data) {
    return <ScreenState error={content.error} onRetry={() => void content.refetch()} />
  }
  return <Pricing context={context} pricing={content.data.Pricing} pricingV2={content.data.PricingV2 || context.product.PricingV2} />
}

export function MsPricingPage() {
  return <MsWorkspace sub="pricing">{(context) => <PricingScreen context={context} />}</MsWorkspace>
}
