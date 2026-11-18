import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { ArrowLeft, Plus, SearchX } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState, StateView } from '@/components/ui/ScreenState'
import { foldSearch, Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Chip, Field, Input, PageHeader, RadioGroup } from '@/components/ui/primitives'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { subscriptionsApi } from './api'
import { useMoneyT, type MoneyDictionary } from './i18n'
import { moneyKeys } from './keys'
import { basePriceText, durationLabel, isOnSale, priceText } from './labels'
import { OnSaleNote, SourceTag, StateBadge, SubscriberChoiceField, usePlanRequest, type SubscriberChoice } from './shared'
import {
  MonetizationChangeKind,
  PriceSource,
  ProductState,
  type BasePlanDto,
  type ConvertedPricesDto,
  type PlaySubscriptionDto,
  type PriceInputDto,
  type ProductOffer,
} from './types'

// Design 10.12, 10.13 and 10.20: Play base plans and their offers (activate, deactivate, add), and the regional prices of
// a base plan from Play's own price conversion, edited by hand before they go into a plan.

export const PLAY_PERIODS = ['P1W', 'P1M', 'P3M', 'P6M', 'P1Y'] as const
const PLAY_OFFER_DURATIONS = ['P3D', 'P1W', 'P2W', 'P1M', 'P2M', 'P3M', 'P6M', 'P1Y'] as const
const PLAY_ELIGIBILITY = ['NewCustomers', 'Upgrade', 'DeveloperDetermined'] as const

function planTypeLabel(type: string | null | undefined, t: MoneyDictionary): string {
  switch (type) {
    case 'AutoRenewing':
      return t.play.autoRenewing
    case 'Prepaid':
      return t.play.prepaid
    case 'Installments':
      return t.play.installments
    default:
      return type ?? '-'
  }
}

function offerActive(offer: ProductOffer): boolean {
  return (offer.State ?? '').toUpperCase() === 'ACTIVE'
}

/** The base plans of one Play subscription with their offers and state actions. */
export function PlayBasePlans({
  context,
  subscription,
  onPlanned,
}: {
  context: WorkspaceContext
  subscription: Pick<PlaySubscriptionDto, 'Id' | 'ProductId' | 'BasePlans'>
  onPlanned: (plan: ChangePlanDto) => void
}) {
  const t = useMoneyT()
  const { app, organizationId, record, editor, storeQuery } = context
  const [addingPlan, setAddingPlan] = useState(false)
  const [offerFor, setOfferFor] = useState<BasePlanDto | null>(null)
  const toggle = useMutation({
    mutationFn: (change: { kind: 'plan' | 'offer'; basePlanId: string; code?: string; active: boolean }) =>
      subscriptionsApi.plan(organizationId, record.StoreAppId, [
        change.kind === 'plan'
          ? {
              Kind: MonetizationChangeKind.BasePlanState,
              ProductId: subscription.ProductId,
              BasePlanId: change.basePlanId,
              Active: change.active,
            }
          : {
              Kind: MonetizationChangeKind.PlayOfferState,
              ProductId: subscription.ProductId,
              BasePlanId: change.basePlanId,
              Code: change.code,
              Active: change.active,
            },
      ]),
    onSuccess: onPlanned,
  })
  const plans = subscription.BasePlans ?? []
  return (
    <div className="grid gap-3">
      {plans.length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.play.noBasePlans}</p> : null}
      <ul className="m-0 grid list-none gap-2 p-0" aria-label={t.play.basePlans}>
        {plans.map((plan) => {
          const id = plan.BasePlanId ?? ''
          const active = plan.State === ProductState.Active
          return (
            <li key={id} className="grid gap-2 rounded-lg border border-border p-2.5">
              <div className="flex flex-wrap items-center gap-3">
                <span className="grid min-w-0 flex-1 gap-0.5">
                  <b>{durationLabel(plan.BillingPeriod, t)}</b>
                  <span className="mono text-xs text-fg3">
                    {id} · {plan.BillingPeriod ?? '-'} · {planTypeLabel(plan.Type, t)}
                  </span>
                </span>
                <span className="flex items-center gap-1.5 tabular-nums">
                  {plan.BasePrice ? (
                    <>
                      {basePriceText(plan.BasePrice)}
                      <SourceTag source={plan.BasePrice.Source} />
                    </>
                  ) : (
                    <span className="text-fg3">{t.price.noPrice}</span>
                  )}
                </span>
                <StateBadge state={plan.State} />
                <Link className="lnk text-[13px]" to={`/apps/${app.Id}/money/subscriptions/${subscription.Id}/base-plans/${id}${storeQuery}`}>
                  {interpolate(t.play.regionPrices, { count: plan.RegionCount })}
                </Link>
                {editor ? (
                  <Button
                    size="sm"
                    variant={active ? 'ghost' : 'outline'}
                    loading={toggle.isPending && toggle.variables?.basePlanId === id && toggle.variables.kind === 'plan'}
                    onClick={() => toggle.mutate({ kind: 'plan', basePlanId: id, active: !active })}
                  >
                    {interpolate(active ? t.play.deactivate : t.play.activate, { name: id })}
                  </Button>
                ) : null}
              </div>
              {(plan.Offers ?? []).length > 0 ? (
                <ul className="m-0 grid list-none gap-1 border-l-2 border-border pl-3" aria-label={interpolate(t.play.offersOf, { name: id })}>
                  {(plan.Offers ?? []).map((offer) => {
                    const code = offer.Code ?? offer.Id ?? ''
                    const on = offerActive(offer)
                    return (
                      <li key={code} className="flex flex-wrap items-center gap-2 text-[13px]">
                        <span className="mono">{code}</span>
                        <span className="text-fg2">{(offer.Phases ?? []).join(' + ') || '-'}</span>
                        <span className="text-fg3">{(offer.Eligibility ?? []).join(', ')}</span>
                        <Badge tone={on ? 'success' : 'neutral'} small>
                          {on ? t.enums.state.active : offer.State ? t.enums.state.inactive : t.enums.state.draft}
                        </Badge>
                        {editor ? (
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => toggle.mutate({ kind: 'offer', basePlanId: id, code, active: !on })}
                          >
                            {interpolate(on ? t.play.deactivate : t.play.activate, { name: code })}
                          </Button>
                        ) : null}
                      </li>
                    )
                  })}
                </ul>
              ) : null}
              {editor && plan.Type === 'AutoRenewing' ? (
                <div>
                  <Button size="sm" variant="ghost" onClick={() => setOfferFor(plan)}>
                    <Plus aria-hidden />
                    {interpolate(t.play.addOffer, { name: id })}
                  </Button>
                </div>
              ) : null}
            </li>
          )
        })}
      </ul>
      {editor ? (
        <div>
          <Button size="sm" onClick={() => setAddingPlan(true)}>
            <Plus aria-hidden />
            {t.play.addBasePlan}
          </Button>
        </div>
      ) : null}
      {addingPlan ? (
        <BasePlanModal context={context} productId={subscription.ProductId ?? ''} onClose={() => setAddingPlan(false)} onPlanned={onPlanned} />
      ) : null}
      {offerFor ? (
        <PlayOfferModal
          context={context}
          productId={subscription.ProductId ?? ''}
          plan={offerFor}
          onClose={() => setOfferFor(null)}
          onPlanned={onPlanned}
        />
      ) : null}
    </div>
  )
}

interface PlayModalProps {
  context: WorkspaceContext
  productId: string
  onClose: () => void
  onPlanned: (plan: ChangePlanDto) => void
}

function PriceFields({
  price,
  onChange,
}: {
  price: { region: string; currency: string; amount: string }
  onChange: (price: { region: string; currency: string; amount: string }) => void
}) {
  const t = useMoneyT()
  return (
    <div className="grid gap-4 sm:grid-cols-3">
      <Field label={t.play.region}>
        <Input mono maxLength={2} value={price.region} onChange={(event) => onChange({ ...price, region: event.target.value.toUpperCase() })} />
      </Field>
      <Field label={t.play.currency}>
        <Input mono maxLength={3} value={price.currency} onChange={(event) => onChange({ ...price, currency: event.target.value.toUpperCase() })} />
      </Field>
      <Field label={t.play.amount} hint={t.play.amountHint}>
        <Input inputMode="decimal" value={price.amount} onChange={(event) => onChange({ ...price, amount: event.target.value.replace(',', '.') })} />
      </Field>
    </div>
  )
}

function BasePlanModal({ context, productId, onClose, onPlanned }: PlayModalProps) {
  const t = useMoneyT()
  const { organizationId, record } = context
  const [id, setId] = useState('')
  const [period, setPeriod] = useState<string>('P1M')
  const [renewing, setRenewing] = useState<'auto' | 'prepaid'>('auto')
  const [price, setPrice] = useState({ region: 'TR', currency: 'TRY', amount: '' })
  const { mutation, errorBox } = usePlanRequest(
    () =>
      subscriptionsApi.plan(organizationId, record.StoreAppId, [
        {
          Kind: MonetizationChangeKind.BasePlan,
          ProductId: productId,
          BasePlanId: id.trim(),
          Duration: period,
          AutoRenewing: renewing === 'auto',
          Prices: [{ Territory: price.region, Currency: price.currency, Amount: price.amount.trim() }],
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
      title={t.play.addBasePlan}
      description={productId}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button variant="primary" disabled={!id.trim() || !price.amount.trim()} loading={mutation.isPending} onClick={() => mutation.mutate()}>
            {t.plan.add}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        <Field label={t.play.basePlanId} hint={t.play.planIdHint}>
          <Input mono value={id} onChange={(event) => setId(event.target.value)} />
        </Field>
        <Field label={t.play.billingPeriod}>
          <Select value={period} options={PLAY_PERIODS.map((value) => ({ value, label: durationLabel(value, t) }))} onChange={setPeriod} />
        </Field>
        <RadioGroup
          name="renewing"
          label={t.play.type}
          value={renewing}
          options={[
            { value: 'auto', label: t.play.autoRenewing },
            { value: 'prepaid', label: t.play.prepaid },
          ]}
          onChange={setRenewing}
        />
        <PriceFields price={price} onChange={setPrice} />
        <p className="m-0 text-xs text-fg3">{t.play.draftNote}</p>
      </div>
    </Modal>
  )
}

function PlayOfferModal({ context, productId, plan, onClose, onPlanned }: PlayModalProps & { plan: BasePlanDto }) {
  const t = useMoneyT()
  const { organizationId, record } = context
  const [code, setCode] = useState('')
  const [phase, setPhase] = useState<'free' | 'price'>('free')
  const [duration, setDuration] = useState<string>('P1W')
  const [recurrence, setRecurrence] = useState('1')
  const [price, setPrice] = useState({
    region: plan.BasePrice?.Territory ?? 'TR',
    currency: plan.BasePrice?.Currency ?? 'TRY',
    amount: '',
  })
  const [eligibility, setEligibility] = useState<string>('NewCustomers')
  const { mutation, errorBox } = usePlanRequest(
    () =>
      subscriptionsApi.plan(organizationId, record.StoreAppId, [
        {
          Kind: MonetizationChangeKind.PlayOffer,
          ProductId: productId,
          BasePlanId: plan.BasePlanId,
          Code: code.trim(),
          Eligibility: [eligibility],
          Phases: [
            {
              Duration: duration,
              Recurrence: Math.max(1, Number(recurrence) || 1),
              Free: phase === 'free',
              Prices: phase === 'free' ? null : [{ Territory: price.region, Currency: price.currency, Amount: price.amount.trim() }],
            },
          ],
        },
      ]),
    (made) => {
      onPlanned(made)
      onClose()
    },
  )
  const ready = code.trim() && (phase === 'free' || price.amount.trim())
  return (
    <Modal
      open
      onClose={onClose}
      title={interpolate(t.play.addOffer, { name: plan.BasePlanId ?? '' })}
      description={productId}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button variant="primary" disabled={!ready} loading={mutation.isPending} onClick={() => mutation.mutate()}>
            {t.plan.add}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        <Field label={t.play.offerId} hint={t.play.planIdHint}>
          <Input mono value={code} onChange={(event) => setCode(event.target.value)} />
        </Field>
        <RadioGroup
          name="phase"
          label={t.play.phase}
          value={phase}
          options={[
            { value: 'free', label: t.play.freeTrial },
            { value: 'price', label: t.play.introPrice },
          ]}
          onChange={setPhase}
        />
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t.offers.duration}>
            <Select value={duration} options={PLAY_OFFER_DURATIONS.map((value) => ({ value, label: durationLabel(value, t) }))} onChange={setDuration} />
          </Field>
          <Field label={t.play.recurrence}>
            <Input type="number" min={1} value={recurrence} onChange={(event) => setRecurrence(event.target.value)} />
          </Field>
        </div>
        {phase === 'price' ? <PriceFields price={price} onChange={setPrice} /> : null}
        <Field label={t.offers.eligibility}>
          <Select
            value={eligibility}
            options={PLAY_ELIGIBILITY.map((value) => ({ value, label: (t.play.eligibility as Record<string, string>)[value] }))}
            onChange={setEligibility}
          />
        </Field>
      </div>
    </Modal>
  )
}

// ---- Base plan regional prices -------------------------------------------------------------------------------------------
const PAGE_SIZE = 20
// Every Play region on one page: the table is filtered and paged in the browser.
const ALL_REGIONS = { PageNumber: 1, RowCount: 500 }

interface Row {
  region: string
  currency: string
  current: string | null
  amount: string
  source: PriceSource
  edited: boolean
}

type RowView = 'changed' | 'all' | 'invalid'

function invalidRegions(keys: string[]): Set<string> {
  const regions = new Set<string>()
  for (const key of keys) {
    const match = /^Prices\[([A-Za-z]+)\]/.exec(key)
    if (match) {
      regions.add(match[1].toUpperCase())
    }
  }
  return regions
}

export function BasePlanPricesPage() {
  return (
    <AppWorkspace section="money" sub="subscriptions" stores={[StoreKind.Google]}>
      {(context) => <BasePlanPrices context={context} />}
    </AppWorkspace>
  )
}

function BasePlanPrices({ context }: { context: WorkspaceContext }) {
  const t = useMoneyT()
  const format = useFormat()
  const { app, organizationId, editor, storeQuery, can } = context
  const { subscriptionId: rawId = '', basePlanId = '' } = useParams()
  const subscriptionId = Number(rawId)
  const [base, setBase] = useState<{ region: string; currency: string; amount: string } | null>(null)
  const [converted, setConverted] = useState<ConvertedPricesDto | null>(null)
  const [rows, setRows] = useState<Row[] | null>(null)
  const [other, setOther] = useState<{ usd: string; eur: string }>({ usd: '', eur: '' })
  const [view, setView] = useState<RowView>('changed')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(PAGE_SIZE)
  const [choice, setChoice] = useState<SubscriberChoice>(null)
  const [choiceError, setChoiceError] = useState<string | null>(null)
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)

  const detail = useQuery({
    queryKey: moneyKeys.subscription(organizationId, subscriptionId),
    queryFn: () => subscriptionsApi.detail(organizationId, subscriptionId),
    enabled: Number.isInteger(subscriptionId) && subscriptionId > 0,
  })
  const filter = { PlanId: basePlanId }
  const current = useQuery({
    queryKey: moneyKeys.prices(organizationId, 'subscription', subscriptionId, ALL_REGIONS, filter),
    queryFn: () => subscriptionsApi.prices(organizationId, subscriptionId, ALL_REGIONS, filter),
    enabled: Number.isInteger(subscriptionId) && subscriptionId > 0,
  })
  const product = detail.data
  const basePlan = product?.Plans?.find((item) => item.BasePlanId === basePlanId) ?? null
  const storeAppId = product?.StoreAppId ?? context.record.StoreAppId

  const convert = useMutation({
    mutationFn: (input: { currency: string; amount: string }) => subscriptionsApi.convert(organizationId, storeAppId, input.currency, input.amount),
    onSuccess: (answer) => {
      const known = new Map((current.data?.Data ?? []).map((price) => [(price.Territory ?? '').toUpperCase(), price]))
      const baseRegion = (base?.region ?? '').toUpperCase()
      const next: Row[] = answer.Prices.filter((price) => price.RegionCode).map((price) => {
        const region = (price.RegionCode as string).toUpperCase()
        const before = known.get(region)
        const own = region === baseRegion
        return {
          region,
          currency: price.Currency ?? before?.Currency ?? '',
          current: before?.CustomerPrice ?? null,
          amount: own ? (base?.amount ?? '') : (price.Amount ?? ''),
          source: own ? PriceSource.Manual : PriceSource.Converted,
          edited: own,
        }
      })
      if (baseRegion && !next.some((row) => row.region === baseRegion) && base) {
        const before = known.get(baseRegion)
        next.unshift({
          region: baseRegion,
          currency: base.currency,
          current: before?.CustomerPrice ?? null,
          amount: base.amount,
          source: PriceSource.Manual,
          edited: true,
        })
      }
      setConverted(answer)
      setRows(next)
      setOther({ usd: answer.OtherRegionsUsd ?? '', eur: answer.OtherRegionsEur ?? '' })
      setPage(1)
    },
  })

  const onSale = basePlan ? isOnSale(basePlan.State) : false
  const changes = useMemo(
    () => (rows ?? []).filter((row) => row.amount.trim() && row.amount.trim() !== (row.current ?? '').trim()),
    [rows],
  )
  const request = usePlanRequest(
    () =>
      subscriptionsApi.plan(organizationId, storeAppId, [
        {
          Kind: MonetizationChangeKind.BasePlanPrices,
          SubscriptionId: subscriptionId,
          ProductId: product?.ProductId,
          BasePlanId: basePlanId,
          Prices: changes.map<PriceInputDto>((row) => ({ Territory: row.region, Currency: row.currency, Amount: row.amount.trim() })),
          OtherRegionsUsd: other.usd.trim() || null,
          OtherRegionsEur: other.eur.trim() || null,
          RegionsVersion: converted?.RegionsVersion ?? null,
          PreserveCurrentPrice: onSale ? choice === 'keep' : null,
        },
      ]),
    setPlan,
  )
  const invalid = invalidRegions(request.keys)

  if (detail.isLoading || current.isLoading) {
    return <CardSkeleton count={2} />
  }
  if (detail.isError || !product) {
    return <ScreenState error={detail.error} onRetry={() => void detail.refetch()} />
  }
  if (!basePlan) {
    return (
      <StateView
        icon={<SearchX aria-hidden />}
        title={interpolate(t.play.planMissing, { name: basePlanId })}
        action={
          <Link className="btn" to={`/apps/${app.Id}/money/subscriptions${storeQuery}`}>
            {t.subscriptions.title}
          </Link>
        }
      />
    )
  }

  const start = base ?? {
    region: basePlan.BasePrice?.Territory ?? 'TR',
    currency: basePlan.BasePrice?.Currency ?? 'TRY',
    amount: basePlan.BasePrice?.Amount ?? '',
  }
  const all = rows ?? (current.data?.Data ?? []).map<Row>((price) => ({
    region: (price.Territory ?? '').toUpperCase(),
    currency: price.Currency ?? '',
    current: price.CustomerPrice ?? null,
    amount: price.CustomerPrice ?? '',
    source: price.Source,
    edited: false,
  }))
  const changedSet = new Set(changes.map((row) => row.region))
  const needle = foldSearch(search.trim())
  const listed = all
    .filter((row) => (view === 'changed' && rows ? changedSet.has(row.region) : view === 'invalid' ? invalid.has(row.region) : true))
    .filter((row) => !needle || foldSearch(row.region).includes(needle))
  const shown = listed.slice((page - 1) * pageSize, page * pageSize)

  const setAmount = (region: string, amount: string) =>
    setRows((list) =>
      (list ?? all).map((row) => (row.region === region ? { ...row, amount: amount.replace(',', '.'), source: PriceSource.Manual, edited: true } : row)),
    )

  const submit = () => {
    if (onSale && choice === null) {
      setChoiceError(t.price.choiceRequired)
      return
    }
    request.mutation.mutate()
  }

  const columns: Column<Row>[] = [
    {
      key: 'region',
      header: t.price.territory,
      role: 'main',
      cell: (row) => (
        <span className="flex items-center gap-2">
          <span className="mono">{row.region}</span>
          {row.region === start.region.toUpperCase() ? (
            <Badge tone="accent" small>
              {t.price.base}
            </Badge>
          ) : null}
        </span>
      ),
    },
    { key: 'current', header: t.price.current, cell: (row) => <span className="tabular-nums">{priceText(row.current, row.currency)}</span> },
    {
      key: 'new',
      header: t.price.newPrice,
      cell: (row) =>
        editor && rows ? (
          <span className="grid gap-1">
            <Input
              aria-label={interpolate(t.play.amountOf, { region: row.region })}
              className={invalid.has(row.region) ? 'err' : undefined}
              inputMode="decimal"
              value={row.amount}
              onChange={(event) => setAmount(row.region, event.target.value)}
            />
            {invalid.has(row.region) ? <span className="err-t">{t.play.invalidRow}</span> : null}
          </span>
        ) : (
          <span className="tabular-nums">{priceText(row.amount, row.currency)}</span>
        ),
    },
    { key: 'source', header: t.price.source, cell: (row) => <SourceTag source={row.source} /> },
  ]

  return (
    <div className="grid gap-4">
      <Link className="lnk inline-flex items-center gap-1 text-[13px]" to={`/apps/${app.Id}/money/subscriptions${storeQuery}`}>
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
        {t.subscriptions.title}
      </Link>
      <PageHeader
        title={durationLabel(basePlan.BillingPeriod, t)}
        subtitle={<span className="mono">{`${product.ProductId} · ${basePlanId} · ${basePlan.BillingPeriod ?? '-'}`}</span>}
        badge={<StateBadge state={basePlan.State} />}
        actions={
          editor && rows ? (
            <Button variant="primary" disabled={changes.length === 0} loading={request.mutation.isPending} onClick={submit}>
              {interpolate(t.play.addPrices, { count: changes.length })}
            </Button>
          ) : null
        }
      />
      {request.errorBox}
      {invalid.size > 0 ? (
        <Callout tone="danger" title={interpolate(t.play.invalidTitle, { count: invalid.size })}>
          <Button size="sm" variant="ghost" onClick={() => setView('invalid')}>
            {t.play.showInvalid}
          </Button>
        </Callout>
      ) : null}
      <section className="card" aria-label={t.play.baseTitle}>
        <div className="card-b grid gap-3">
          <PriceFields price={start} onChange={(next) => setBase(next)} />
          {editor ? (
            <div className="flex flex-wrap items-center gap-3">
              <Button
                loading={convert.isPending}
                disabled={!start.amount.trim() || !start.currency.trim()}
                onClick={() => {
                  setBase(start)
                  convert.mutate({ currency: start.currency.trim(), amount: start.amount.trim() })
                }}
              >
                {t.play.convert}
              </Button>
              <span className="text-xs text-fg3">{t.play.convertHint}</span>
            </div>
          ) : null}
          {converted ? (
            <Callout tone="info">
              {interpolate(t.play.converted, { count: converted.Prices.length, date: format.dateTime(converted.ConvertedDate) })}
            </Callout>
          ) : null}
          {rows ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t.play.otherUsd}>
                <Input inputMode="decimal" value={other.usd} disabled={!editor} onChange={(event) => setOther({ ...other, usd: event.target.value })} />
              </Field>
              <Field label={t.play.otherEur}>
                <Input inputMode="decimal" value={other.eur} disabled={!editor} onChange={(event) => setOther({ ...other, eur: event.target.value })} />
              </Field>
            </div>
          ) : null}
        </div>
      </section>
      <div className="flex flex-wrap items-center gap-2">
        <Input
          className="max-w-xs"
          aria-label={t.price.search}
          placeholder={t.price.search}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            setPage(1)
          }}
        />
        <div className="toolbar" role="group" aria-label={t.price.filter}>
          {rows ? (
            <Chip selected={view === 'changed'} onClick={() => setView('changed')}>
              {interpolate(t.play.changed, { count: changes.length })}
            </Chip>
          ) : null}
          <Chip selected={view === 'all' || (!rows && view === 'changed')} onClick={() => setView('all')}>
            {interpolate(t.play.all, { count: all.length })}
          </Chip>
        </div>
      </div>
      {listed.length === 0 ? (
        <p className="text-[13px] text-fg3">{t.price.empty}</p>
      ) : (
        <>
          <DataTable columns={columns} rows={shown} rowKey={(row) => row.region} caption={t.play.regionTable} />
          <Pagination
            page={page}
            pageCount={Math.max(1, Math.ceil(listed.length / pageSize))}
            total={listed.length}
            pageSize={pageSize}
            onPage={setPage}
            onPageSize={(size) => {
              setPageSize(size)
              setPage(1)
            }}
          />
        </>
      )}
      {rows && onSale ? (
        <>
          <SubscriberChoiceField
            play
            value={choice}
            error={choiceError}
            onChange={(next) => {
              setChoice(next)
              setChoiceError(null)
            }}
          />
          <OnSaleNote canApprove={can(OrgPermission.ReleaseProduction)} />
        </>
      ) : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} onDecided={() => setRows(null)} />
    </div>
  )
}
