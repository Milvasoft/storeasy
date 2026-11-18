import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Badge } from '@/components/ui/Badge'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Modal } from '@/components/ui/Modal'
import { Button, Chip, Field, Input, Textarea } from '@/components/ui/primitives'
import { interpolate } from '@/lib/i18n/interpolate'
import { PlanDialog } from '@/features/common/PlanView'
import { charCount } from '@/features/common/text'
import type { ChangePlanDto } from '@/features/common/types'
import { iapApi } from './api'
import { useMoneyT } from './i18n'
import { moneyKeys } from './keys'
import { basePriceText, priceText } from './labels'
import { planFor, type PriceContext } from './PricesPanel'
import { SourceTag, StateBadge, usePlanRequest } from './shared'
import { MonetizationChangeKind, ProductState, StoreProductKind, type BasePlanDto, type ProductPriceDto } from './types'

// Design 10.12 and 10.14: the Play listings of a subscription (title and benefits) or one-time product (title and
// description), and the purchase options of a one-time product with their regional prices.

const TITLE_MAX = 55
const DESCRIPTION_MAX = 200

interface ListingDraft {
  Name: string
  Text: string
}

export function PlayListings({ context }: { context: PriceContext }) {
  const t = useMoneyT()
  const { organizationId, product, owner, editor } = context
  const subscription = product.Kind === StoreProductKind.Subscription
  const [drafts, setDrafts] = useState<Record<string, ListingDraft>>({})
  const [locale, setLocale] = useState(product.Localizations?.[0]?.Locale ?? '')
  const [newLocale, setNewLocale] = useState('')
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const locales = [...new Set([...(product.Localizations ?? []).map((item) => item.Locale ?? ''), ...Object.keys(drafts)])].filter(Boolean)
  const stored = product.Localizations?.find((item) => item.Locale === locale)
  const value: ListingDraft = drafts[locale] ?? {
    Name: stored?.Name ?? '',
    Text: subscription ? (stored?.Benefits ?? []).join('\n') : (stored?.Description ?? ''),
  }
  const changed = Object.keys(drafts)
  const tooLong = charCount(value.Name) > TITLE_MAX || (!subscription && charCount(value.Text) > DESCRIPTION_MAX)
  const { mutation, errorBox } = usePlanRequest(
    () =>
      planFor(owner, organizationId, product.StoreAppId, [
        {
          Kind: subscription ? MonetizationChangeKind.PlaySubscription : MonetizationChangeKind.OneTimeProduct,
          ProductId: product.ProductId,
          Localizations: Object.entries(drafts).map(([code, draft]) =>
            subscription
              ? {
                  Locale: code,
                  Name: draft.Name.trim(),
                  Benefits: draft.Text.split('\n')
                    .map((line) => line.trim())
                    .filter(Boolean),
                }
              : { Locale: code, Name: draft.Name.trim(), Description: draft.Text.trim() },
          ),
        },
      ]),
    setPlan,
  )
  const set = (patch: Partial<ListingDraft>) => setDrafts((current) => ({ ...current, [locale]: { ...value, ...patch } }))
  return (
    <section className="card" aria-label={t.play.listingsTitle}>
      <div className="card-h flex-wrap">
        <h3 className="h3">{t.play.listingsTitle}</h3>
        <div className="toolbar" role="group" aria-label={t.common.locale}>
          {locales.map((item) => (
            <Chip key={item} selected={item === locale} onClick={() => setLocale(item)}>
              {item}
            </Chip>
          ))}
        </div>
      </div>
      <div className="card-b grid gap-4">
        {locale ? (
          <>
            <Field label={t.play.title} counter={{ value: charCount(value.Name), max: TITLE_MAX }}>
              <Input value={value.Name} disabled={!editor} onChange={(event) => set({ Name: event.target.value })} />
            </Field>
            {subscription ? (
              <Field label={t.play.benefits} hint={t.play.benefitsHint} optional>
                <Textarea rows={4} value={value.Text} disabled={!editor} onChange={(event) => set({ Text: event.target.value })} />
              </Field>
            ) : (
              <Field label={t.detail.description} counter={{ value: charCount(value.Text), max: DESCRIPTION_MAX }}>
                <Textarea rows={3} value={value.Text} disabled={!editor} onChange={(event) => set({ Text: event.target.value })} />
              </Field>
            )}
          </>
        ) : (
          <p className="m-0 text-[13px] text-fg3">{t.detail.noLocalization}</p>
        )}
        {editor ? (
          <>
            <div className="flex flex-wrap items-end gap-2">
              <Field label={t.detail.newLocale}>
                <Input mono className="w-32" value={newLocale} onChange={(event) => setNewLocale(event.target.value)} />
              </Field>
              <Button
                disabled={!newLocale.trim() || locales.includes(newLocale.trim())}
                onClick={() => {
                  const code = newLocale.trim()
                  setDrafts((current) => ({ ...current, [code]: { Name: '', Text: '' } }))
                  setLocale(code)
                  setNewLocale('')
                }}
              >
                {t.detail.addLocale}
              </Button>
            </div>
            {errorBox}
            <div>
              <Button
                variant="primary"
                disabled={changed.length === 0 || tooLong}
                loading={mutation.isPending}
                onClick={() => mutation.mutate()}
              >
                {interpolate(t.detail.reviewChanges, { count: changed.length })}
              </Button>
            </div>
          </>
        ) : null}
      </div>
      <PlanDialog plan={plan} onClose={() => setPlan(null)} onDecided={() => setDrafts({})} />
    </section>
  )
}

/** The purchase options of a Play one-time product: state, regional prices and a price change. */
export function PurchaseOptions({ context }: { context: PriceContext }) {
  const t = useMoneyT()
  const { organizationId, product, editor } = context
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const [pricing, setPricing] = useState<BasePlanDto | null>(null)
  const options = product.Plans ?? []
  const toggle = usePlanRequest(
    (option: { id: string; active: boolean }) =>
      iapApi.plan(organizationId, product.StoreAppId, [
        { Kind: MonetizationChangeKind.OneTimeProduct, ProductId: product.ProductId, PurchaseOptionId: option.id, Active: option.active },
      ]),
    setPlan,
  )
  return (
    <div className="grid gap-4">
      {toggle.errorBox}
      {options.length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.play.noOptions}</p> : null}
      {options.map((option) => {
        const id = option.BasePlanId ?? ''
        const active = option.State === ProductState.Active
        return (
          <section key={id} className="card" aria-label={id}>
            <div className="card-h flex-wrap">
              <h3 className="h3">{id}</h3>
              <span className="text-[13px] text-fg2">{option.Type === 'Rent' ? t.play.rent : t.play.buy}</span>
              <StateBadge state={option.State} />
              <span className="tabular-nums text-[13px]">{basePriceText(option.BasePrice)}</span>
              {editor ? (
                <span className="ml-auto flex gap-2">
                  <Button size="sm" onClick={() => setPricing(option)}>
                    {t.play.changePrice}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    loading={toggle.mutation.isPending && toggle.mutation.variables?.id === id}
                    onClick={() => toggle.mutation.mutate({ id, active: !active })}
                  >
                    {interpolate(active ? t.play.deactivate : t.play.activate, { name: id })}
                  </Button>
                </span>
              ) : null}
            </div>
            <div className="card-b">
              <OptionPrices organizationId={organizationId} iapId={product.Id} optionId={id} />
            </div>
          </section>
        )
      })}
      <p className="m-0 text-xs text-fg3">{t.play.consumableNote}</p>
      {pricing ? <OptionPriceModal context={context} option={pricing} onClose={() => setPricing(null)} onPlanned={setPlan} /> : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} />
    </div>
  )
}

function OptionPrices({ organizationId, iapId, optionId }: { organizationId: number; iapId: number; optionId: string }) {
  const t = useMoneyT()
  const request = { PageNumber: 1, RowCount: 500 }
  const filter = { PlanId: optionId }
  const prices = useQuery({
    queryKey: moneyKeys.prices(organizationId, 'iap', iapId, request, filter),
    queryFn: () => iapApi.prices(organizationId, iapId, request, filter),
    placeholderData: keepPreviousData,
  })
  const rows = prices.data?.Data ?? []
  const columns: Column<ProductPriceDto>[] = [
    {
      key: 'region',
      header: t.price.territory,
      role: 'main',
      cell: (row) => (
        <span className="flex items-center gap-2">
          <span className="mono">{row.Territory}</span>
          {row.IsBase ? (
            <Badge tone="accent" small>
              {t.price.base}
            </Badge>
          ) : null}
        </span>
      ),
    },
    {
      key: 'price',
      header: t.price.price,
      cell: (row) => <span className="tabular-nums">{priceText(row.CustomerPrice, row.Currency)}</span>,
    },
    { key: 'source', header: t.price.source, cell: (row) => <SourceTag source={row.Source} /> },
  ]
  if (prices.isLoading) {
    return <p className="m-0 text-[13px] text-fg3">{t.price.loading}</p>
  }
  return rows.length === 0 ? (
    <p className="m-0 text-[13px] text-fg3">{t.price.noPrice}</p>
  ) : (
    <DataTable
      columns={columns}
      rows={rows}
      rowKey={(row) => row.Territory ?? ''}
      caption={interpolate(t.play.pricesOf, { name: optionId })}
    />
  )
}

function OptionPriceModal({
  context,
  option,
  onClose,
  onPlanned,
}: {
  context: PriceContext
  option: BasePlanDto
  onClose: () => void
  onPlanned: (plan: ChangePlanDto) => void
}) {
  const t = useMoneyT()
  const { organizationId, product } = context
  const [region, setRegion] = useState(option.BasePrice?.Territory ?? 'TR')
  const [currency, setCurrency] = useState(option.BasePrice?.Currency ?? 'TRY')
  const [amount, setAmount] = useState('')
  const { mutation, errorBox } = usePlanRequest(
    () =>
      iapApi.plan(organizationId, product.StoreAppId, [
        {
          Kind: MonetizationChangeKind.OneTimeProduct,
          ProductId: product.ProductId,
          PurchaseOptionId: option.BasePlanId,
          Prices: [{ Territory: region.trim(), Currency: currency.trim(), Amount: amount.trim() }],
        },
      ]),
    (made) => {
      onPlanned(made)
      onClose()
    },
  )
  return (
    <Modal
      open
      onClose={onClose}
      title={interpolate(t.play.priceTitle, { name: option.BasePlanId ?? '' })}
      description={product.ProductId ?? undefined}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button variant="primary" disabled={!amount.trim()} loading={mutation.isPending} onClick={() => mutation.mutate()}>
            {t.plan.add}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label={t.play.region}>
            <Input mono maxLength={2} value={region} onChange={(event) => setRegion(event.target.value.toUpperCase())} />
          </Field>
          <Field label={t.play.currency}>
            <Input mono maxLength={3} value={currency} onChange={(event) => setCurrency(event.target.value.toUpperCase())} />
          </Field>
          <Field label={t.play.amount} hint={t.play.amountHint}>
            <Input inputMode="decimal" value={amount} onChange={(event) => setAmount(event.target.value.replace(',', '.'))} />
          </Field>
        </div>
        <p className="m-0 text-xs text-fg3">{t.play.priceNote}</p>
      </div>
    </Modal>
  )
}
