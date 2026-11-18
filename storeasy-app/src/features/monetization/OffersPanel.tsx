import { useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, Field, IconButton, Input, RadioGroup } from '@/components/ui/primitives'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { todayDateOnly } from '@/lib/utils/date'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { subscriptionsApi } from './api'
import { useMoneyT } from './i18n'
import { moneyKeys } from './keys'
import { durationLabel, OFFER_DURATIONS, offerModeLabel } from './labels'
import type { PriceContext } from './PricesPanel'
import { PricePointSelect, usePlanRequest } from './shared'
import { MonetizationChangeKind, OfferMode, type MonetizationChangeDto, type ProductOffer } from './types'

// Design 10.7-10.8: introductory, promotional, offer code and win-back offers of an App Store subscription. Offer prices
// come from the store's price points too; one-time codes are never stored, only their counts are shown.

type OfferKindKey = 'introductory' | 'promotional' | 'offerCode' | 'winBack'

const CHANGE_KIND: Record<OfferKindKey, MonetizationChangeKind> = {
  introductory: MonetizationChangeKind.IntroductoryOffer,
  promotional: MonetizationChangeKind.PromotionalOffer,
  offerCode: MonetizationChangeKind.OfferCode,
  winBack: MonetizationChangeKind.WinBackOffer,
}

const ELIGIBILITY = ['NEW', 'EXISTING', 'EXPIRED'] as const

export function OffersPanel({ context }: { context: PriceContext }) {
  const t = useMoneyT()
  const format = useFormat()
  const { organizationId, product, editor } = context
  const [adding, setAdding] = useState<OfferKindKey | null>(null)
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const offers = useQuery({
    queryKey: moneyKeys.offers(organizationId, product.Id),
    queryFn: () => subscriptionsApi.offers(organizationId, product.Id),
  })
  // An offer code is never deleted; it is deactivated by its reference name.
  const stop = usePlanRequest(
    (referenceName: string) =>
      subscriptionsApi.plan(organizationId, product.StoreAppId, [
        { Kind: MonetizationChangeKind.OfferCode, ProductId: product.ProductId, SubscriptionId: product.Id, ReferenceName: referenceName, Active: false },
      ]),
    setPlan,
  )
  if (offers.isLoading) {
    return <CardSkeleton count={3} />
  }
  if (offers.isError || !offers.data) {
    return <ScreenState error={offers.error} onRetry={() => void offers.refetch()} />
  }
  const data = offers.data
  const dates = (offer: ProductOffer) =>
    `${offer.StartDate ? format.date(offer.StartDate) : t.offers.now} - ${offer.EndDate ? format.date(offer.EndDate) : t.offers.noEnd}`
  const terms = (offer: ProductOffer) =>
    `${offerModeLabel(offer.Mode, t)} · ${durationLabel(offer.Duration, t)}${offer.Periods && offer.Periods > 1 ? ` x ${offer.Periods}` : ''}`

  return (
    <div className="grid gap-4">
      {stop.errorBox}
      <OfferSection title={t.offers.introductory} note={t.offers.introductoryNote} onAdd={editor ? () => setAdding('introductory') : undefined}>
        {data.Introductory.map((offer, index) => (
          <OfferRow key={offer.Id ?? index} title={terms(offer)} state={offer.State}>
            {interpolate(t.offers.territories, { count: offer.Territories?.length ?? 0 })} · {dates(offer)}
          </OfferRow>
        ))}
      </OfferSection>
      <OfferSection title={t.offers.promotional} note={t.offers.promotionalNote} onAdd={editor ? () => setAdding('promotional') : undefined}>
        {data.Promotional.map((offer, index) => (
          <OfferRow key={offer.Id ?? index} title={offer.Name ?? offer.Code ?? '-'} state={offer.State}>
            <span className="mono">{offer.Code}</span> · {terms(offer)} · {interpolate(t.offers.prices, { count: offer.PriceCount ?? 0 })}
          </OfferRow>
        ))}
      </OfferSection>
      <OfferSection title={t.offers.offerCodes} note={t.offers.codesNote} onAdd={editor ? () => setAdding('offerCode') : undefined}>
        {data.OfferCodes.map((offer, index) => (
          <OfferRow
            key={offer.Id ?? index}
            title={offer.Name ?? '-'}
            state={offer.State}
            action={
              editor && offer.Name && (offer.State ?? '').toUpperCase() !== 'FALSE' ? (
                <Button
                  size="sm"
                  variant="ghost"
                  loading={stop.mutation.isPending && stop.mutation.variables === offer.Name}
                  onClick={() => stop.mutation.mutate(offer.Name ?? '')}
                >
                  {interpolate(t.offers.deactivate, { name: offer.Name })}
                </Button>
              ) : null
            }
          >
            {terms(offer)} · {(offer.Eligibility ?? []).join(', ') || '-'}
            <br />
            {offer.Code ? interpolate(t.offers.customCode, { code: offer.Code, count: offer.CustomCodeCount ?? 0 }) : null}
            {offer.Code && offer.OneTimeCodeCount ? ' · ' : null}
            {offer.OneTimeCodeCount ? interpolate(t.offers.oneTimeCodes, { count: offer.OneTimeCodeCount }) : null}
          </OfferRow>
        ))}
      </OfferSection>
      <OfferSection title={t.offers.winBack} note={t.offers.winBackNote} onAdd={editor ? () => setAdding('winBack') : undefined}>
        {data.WinBack.map((offer, index) => (
          <OfferRow key={offer.Id ?? index} title={offer.Name ?? offer.Code ?? '-'} state={offer.State}>
            <span className="mono">{offer.Code}</span> · {terms(offer)} · {dates(offer)}
          </OfferRow>
        ))}
      </OfferSection>
      {adding ? <OfferModal context={context} kind={adding} onClose={() => setAdding(null)} onPlanned={setPlan} /> : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} />
    </div>
  )
}

function OfferSection({ title, note, onAdd, children }: { title: string; note: string; onAdd?: () => void; children: ReactNode[] }) {
  const t = useMoneyT()
  return (
    <section className="card" aria-label={title}>
      <div className="card-h">
        <h3 className="h3">{title}</h3>
        <Badge small>{children.length}</Badge>
        {onAdd ? (
          <Button size="sm" className="ml-auto" onClick={onAdd}>
            <Plus aria-hidden />
            {interpolate(t.offers.add, { kind: title })}
          </Button>
        ) : null}
      </div>
      <div className="card-b grid gap-2">
        {children.length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.offers.none}</p> : <ul className="m-0 grid list-none gap-2 p-0">{children}</ul>}
        <p className="m-0 text-xs text-fg3">{note}</p>
      </div>
    </section>
  )
}

function OfferRow({ title, state, action, children }: { title: string; state?: string | null; action?: ReactNode; children: ReactNode }) {
  return (
    <li className="flex flex-wrap items-start gap-2 rounded-lg border border-border p-2.5 text-[13px]">
      <span className="grid min-w-0 flex-1 gap-0.5">
        <b>{title}</b>
        <span className="text-fg2">{children}</span>
      </span>
      {state ? (
        <Badge small tone={state.toUpperCase() === 'ACTIVE' || state.toUpperCase() === 'TRUE' ? 'success' : 'neutral'}>
          {state}
        </Badge>
      ) : null}
      {action}
    </li>
  )
}

interface PriceRow {
  key: number
  territory: string
  pricePointId: string | null
}

let rowKey = 0

function PriceRows({ context, rows, onChange }: { context: PriceContext; rows: PriceRow[]; onChange: (rows: PriceRow[]) => void }) {
  const t = useMoneyT()
  const { organizationId, product } = context
  const set = (key: number, patch: Partial<PriceRow>) => onChange(rows.map((row) => (row.key === key ? { ...row, ...patch } : row)))
  return (
    <fieldset className="grid gap-2">
      <legend className="mb-1 text-[13px] font-semibold">{t.offers.priceRows}</legend>
      {rows.map((row, index) => (
        <div key={row.key} className="grid items-end gap-2 sm:grid-cols-[120px_minmax(0,1fr)_auto]">
          <Field label={interpolate(t.offers.territoryN, { n: index + 1 })}>
            <Input
              mono
              maxLength={3}
              value={row.territory}
              onChange={(event) => set(row.key, { territory: event.target.value.toUpperCase(), pricePointId: null })}
            />
          </Field>
          <PricePointSelect
            organizationId={organizationId}
            owner="subscription"
            id={product.Id}
            territory={row.territory}
            value={row.pricePointId}
            label={interpolate(t.offers.priceN, { n: index + 1 })}
            onChange={(id) => set(row.key, { pricePointId: id })}
          />
          <IconButton
            label={interpolate(t.offers.removeRow, { n: index + 1 })}
            disabled={rows.length === 1}
            onClick={() => onChange(rows.filter((item) => item.key !== row.key))}
          >
            <Trash2 aria-hidden />
          </IconButton>
        </div>
      ))}
      <div>
        <Button size="sm" variant="ghost" onClick={() => onChange([...rows, { key: ++rowKey, territory: '', pricePointId: null }])}>
          <Plus aria-hidden />
          {t.offers.addRow}
        </Button>
      </div>
    </fieldset>
  )
}

function numberOrNull(value: string): number | null {
  const parsed = Number(value)
  return value.trim() && Number.isFinite(parsed) ? parsed : null
}

/** Design 10.8 for every offer kind: the fields the planner reads for that kind (MonetizationChangeDto). */
function OfferModal({
  context,
  kind,
  onClose,
  onPlanned,
}: {
  context: PriceContext
  kind: OfferKindKey
  onClose: () => void
  onPlanned: (plan: ChangePlanDto) => void
}) {
  const t = useMoneyT()
  const { organizationId, product } = context
  const [mode, setMode] = useState<OfferMode>(OfferMode.FreeTrial)
  const [duration, setDuration] = useState<string>('ONE_WEEK')
  const [periods, setPeriods] = useState('1')
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [rows, setRows] = useState<PriceRow[]>([{ key: ++rowKey, territory: product.BasePrice?.Territory ?? '', pricePointId: null }])
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [eligibility, setEligibility] = useState<string[]>(['NEW'])
  const [customCode, setCustomCode] = useState('')
  const [customCount, setCustomCount] = useState('')
  const [expiry, setExpiry] = useState('')
  const [oneTime, setOneTime] = useState('')
  const [paidMonths, setPaidMonths] = useState('1')
  const [sinceMin, setSinceMin] = useState('1')
  const [sinceMax, setSinceMax] = useState('12')
  const [waitMonths, setWaitMonths] = useState('')
  const [priority, setPriority] = useState<string>('NORMAL')

  const intro = kind === 'introductory'
  const needsPrices = !intro || mode !== OfferMode.FreeTrial
  const payAsYouGo = mode === OfferMode.PayAsYouGo
  const prices = rows
    .filter((row) => row.territory.trim() && row.pricePointId)
    .map((row) => ({ Territory: row.territory.trim(), PricePointId: row.pricePointId }))

  const change = (): MonetizationChangeDto => {
    const base: MonetizationChangeDto = {
      Kind: CHANGE_KIND[kind],
      ProductId: product.ProductId,
      SubscriptionId: product.Id,
      Mode: mode,
      Duration: payAsYouGo ? product.Duration : duration,
      Periods: payAsYouGo ? (numberOrNull(periods) ?? 1) : 1,
      Prices: needsPrices ? prices : null,
    }
    switch (kind) {
      case 'introductory':
        return { ...base, StartDate: start || null, EndDate: end || null }
      case 'promotional':
        return { ...base, ReferenceName: name.trim(), Code: code.trim() }
      case 'offerCode':
        return {
          ...base,
          ReferenceName: name.trim(),
          Eligibility: eligibility,
          Code: customCode.trim() || null,
          CustomCodeCount: customCode.trim() ? numberOrNull(customCount) : null,
          CodeExpiryDate: customCode.trim() ? expiry || null : null,
          OneTimeCodeCount: numberOrNull(oneTime),
        }
      default:
        return {
          ...base,
          ReferenceName: name.trim(),
          Code: code.trim(),
          PaidMonths: numberOrNull(paidMonths),
          SinceMinMonths: numberOrNull(sinceMin),
          SinceMaxMonths: numberOrNull(sinceMax),
          WaitMonths: numberOrNull(waitMonths),
          Priority: priority,
          StartDate: start || null,
          EndDate: end || null,
        }
    }
  }

  const { mutation, errorBox } = usePlanRequest(
    () => subscriptionsApi.plan(organizationId, product.StoreAppId, [change()]),
    (made) => {
      onPlanned(made)
      onClose()
    },
  )
  const missing =
    (needsPrices && prices.length === 0) ||
    (!intro && !name.trim()) ||
    ((kind === 'promotional' || kind === 'winBack') && !code.trim()) ||
    (kind === 'offerCode' && eligibility.length === 0) ||
    (kind === 'winBack' && !start)
  const title = { introductory: t.offers.introductory, promotional: t.offers.promotional, offerCode: t.offers.offerCodes, winBack: t.offers.winBack }[kind]

  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title={interpolate(t.offers.add, { kind: title })}
      description={product.ReferenceName ?? product.ProductId ?? undefined}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button variant="primary" disabled={missing} loading={mutation.isPending} onClick={() => mutation.mutate()}>
            {t.plan.add}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        {!intro ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t.common.referenceName}>
              <Input value={name} onChange={(event) => setName(event.target.value)} />
            </Field>
            {kind !== 'offerCode' ? (
              <Field label={t.offers.code} hint={t.offers.codeHint}>
                <Input mono value={code} onChange={(event) => setCode(event.target.value)} />
              </Field>
            ) : null}
          </div>
        ) : null}
        <RadioGroup<OfferMode>
          name="mode"
          label={t.offers.mode}
          value={mode}
          options={[
            { value: OfferMode.FreeTrial, label: t.enums.mode.freeTrial, description: t.offers.freeTrialHint },
            { value: OfferMode.PayAsYouGo, label: t.enums.mode.payAsYouGo, description: t.offers.payAsYouGoHint },
            { value: OfferMode.PayUpFront, label: t.enums.mode.payUpFront, description: t.offers.payUpFrontHint },
          ]}
          onChange={setMode}
        />
        {payAsYouGo ? (
          <Field label={t.offers.periods} hint={interpolate(t.offers.periodsHint, { period: durationLabel(product.Duration, t) })}>
            <Input type="number" min={1} value={periods} onChange={(event) => setPeriods(event.target.value)} />
          </Field>
        ) : (
          <Field label={t.offers.duration}>
            <Select value={duration} options={OFFER_DURATIONS.map((value) => ({ value, label: durationLabel(value, t) }))} onChange={setDuration} />
          </Field>
        )}
        {needsPrices ? (
          <PriceRows context={context} rows={rows} onChange={setRows} />
        ) : (
          <p className="m-0 text-[13px] text-fg2">{t.offers.allTerritories}</p>
        )}
        {kind === 'offerCode' ? (
          <>
            <fieldset className="grid gap-1">
              <legend className="mb-1 text-[13px] font-semibold">{t.offers.eligibility}</legend>
              {ELIGIBILITY.map((value) => (
                <Checkbox
                  key={value}
                  checked={eligibility.includes(value)}
                  onChange={(on) => setEligibility((list) => (on ? [...list, value] : list.filter((item) => item !== value)))}
                >
                  {(t.offers.eligibilityOf as Record<string, string>)[value]}
                </Checkbox>
              ))}
            </fieldset>
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label={t.offers.customCodeLabel} optional>
                <Input mono value={customCode} onChange={(event) => setCustomCode(event.target.value.toUpperCase())} />
              </Field>
              <Field label={t.offers.customCount}>
                <Input type="number" min={1} value={customCount} disabled={!customCode.trim()} onChange={(event) => setCustomCount(event.target.value)} />
              </Field>
              <Field label={t.offers.expiry}>
                <Input type="date" min={todayDateOnly()} value={expiry} disabled={!customCode.trim()} onChange={(event) => setExpiry(event.target.value)} />
              </Field>
            </div>
            <Field label={t.offers.oneTimeCount} hint={t.offers.oneTimeHint} optional>
              <Input type="number" min={0} max={25000} value={oneTime} onChange={(event) => setOneTime(event.target.value)} />
            </Field>
          </>
        ) : null}
        {kind === 'winBack' ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t.offers.paidMonths}>
              <Input type="number" min={1} max={60} value={paidMonths} onChange={(event) => setPaidMonths(event.target.value)} />
            </Field>
            <Field label={t.offers.waitMonths} optional>
              <Input type="number" min={0} value={waitMonths} onChange={(event) => setWaitMonths(event.target.value)} />
            </Field>
            <Field label={t.offers.sinceMin}>
              <Input type="number" min={0} value={sinceMin} onChange={(event) => setSinceMin(event.target.value)} />
            </Field>
            <Field label={t.offers.sinceMax}>
              <Input type="number" min={0} value={sinceMax} onChange={(event) => setSinceMax(event.target.value)} />
            </Field>
            <Field label={t.offers.priority}>
              <Select
                value={priority}
                options={[
                  { value: 'NORMAL', label: t.offers.priorityNormal },
                  { value: 'HIGH', label: t.offers.priorityHigh },
                ]}
                onChange={setPriority}
              />
            </Field>
          </div>
        ) : null}
        {kind === 'introductory' || kind === 'winBack' ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t.offers.startDate} optional={kind === 'introductory'} hint={kind === 'winBack' ? t.offers.winBackStart : undefined}>
              <Input type="date" min={todayDateOnly()} value={start} onChange={(event) => setStart(event.target.value)} />
            </Field>
            <Field label={t.offers.endDate} optional>
              <Input type="date" min={start || todayDateOnly()} value={end} onChange={(event) => setEnd(event.target.value)} />
            </Field>
          </div>
        ) : null}
        {intro ? <p className="m-0 text-xs text-fg3">{t.offers.introEligibility}</p> : null}
      </div>
    </Modal>
  )
}
