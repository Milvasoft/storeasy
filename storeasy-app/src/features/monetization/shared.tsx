import { useState, type ReactNode } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import { Badge, StoreTag, Tag } from '@/components/ui/Badge'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Select } from '@/components/ui/Select'
import { Button, Field, Input, RadioGroup } from '@/components/ui/primitives'
import { ApiError, apiErrorMessage } from '@/lib/api/http'
import { StoreKind } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { todayDateOnly } from '@/lib/utils/date'
import { showToast } from '@/lib/toast/toast'
import type { ChangePlanDto } from '@/features/common/types'
import { iapApi, subscriptionsApi } from './api'
import { useMoneyT } from './i18n'
import { moneyKeys } from './keys'
import { priceSourceLabel, priceText, productStateView } from './labels'
import type { PriceSource, ProductState } from './types'

// Building blocks of the phase 7 screens: state and price badges, the price point picker (prices only ever come from the
// store's price points), the existing subscribers choice of a price change and the plan request of a form.

// The stores whose subscriptions and In-App Purchases storeasy manages here (Microsoft add-ons live with the Microsoft screens).
export const APPLE_AND_GOOGLE = [StoreKind.Apple, StoreKind.Google]

export function StateBadge({ state, storeState }: { state: ProductState; storeState?: string | null }) {
  const t = useMoneyT()
  const view = productStateView(state, t)
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <Badge tone={view.tone} dot small>
        {view.label}
      </Badge>
      {storeState ? <span className="mono text-[11px] text-fg3">{storeState}</span> : null}
    </span>
  )
}

export function SourceTag({ source }: { source: PriceSource }) {
  const t = useMoneyT()
  return <Tag>{priceSourceLabel(source, t)}</Tag>
}

/** A product heading: reference name, product id, store and state. */
export function ProductHeading({
  name,
  productId,
  store,
  state,
  storeState,
  extra,
}: {
  name: string
  productId?: string | null
  store: StoreKind
  state: ProductState
  storeState?: string | null
  extra?: ReactNode
}) {
  return (
    <div className="mb-3 grid gap-1">
      <div className="flex flex-wrap items-center gap-2">
        <StoreTag store={store} small />
        <StateBadge state={state} storeState={storeState} />
        {extra}
      </div>
      <h2 className="h2 m-0">{name}</h2>
      {productId ? <span className="mono text-[13px] text-fg2">{productId}</span> : null}
    </div>
  )
}

export type PriceOwner = 'subscription' | 'iap'

/** The store's price points of one territory; a price is picked from them, never typed. */
export function PricePointSelect({
  organizationId,
  owner,
  id,
  territory,
  value,
  onChange,
  label,
  disabled,
}: {
  organizationId: number
  owner: PriceOwner
  id: number
  territory: string
  value: string | null
  onChange: (pricePointId: string, text: string) => void
  label: string
  disabled?: boolean
}) {
  const t = useMoneyT()
  const code = territory.trim().toUpperCase()
  const ready = /^[A-Z]{3}$/.test(code)
  const points = useQuery({
    queryKey: moneyKeys.pricePoints(organizationId, owner, id, code),
    queryFn: () =>
      owner === 'subscription' ? subscriptionsApi.pricePoints(organizationId, id, code) : iapApi.pricePoints(organizationId, id, code),
    enabled: ready,
    staleTime: 5 * 60_000,
  })
  const options = (points.data ?? [])
    .filter((point) => point.Id)
    .map((point) => ({
      value: point.Id as string,
      label: priceText(point.CustomerPrice, point.Currency),
      description: point.Proceeds ? interpolate(t.price.proceeds, { amount: priceText(point.Proceeds, point.Currency) }) : undefined,
    }))
  return (
    <Field label={label} hint={points.isError ? t.price.pointsFailed : t.price.pointsHint}>
      <Select
        value={value}
        searchable
        disabled={disabled || !ready || points.isLoading}
        placeholder={points.isLoading ? t.price.pointsLoading : t.price.pointsPick}
        options={options}
        onChange={(picked) => onChange(picked, options.find((option) => option.value === picked)?.label ?? picked)}
      />
    </Field>
  )
}

/** Start of a price or offer: right away (null) or a future day. */
export function StartDateField({
  value,
  onChange,
  label,
  disabled,
}: {
  value: string | null
  onChange: (value: string | null) => void
  label: string
  disabled?: boolean
}) {
  const t = useMoneyT()
  const [later, setLater] = useState(value !== null)
  return (
    <div className="grid gap-2">
      <RadioGroup
        name={`start-${label}`}
        label={label}
        value={later ? 'later' : 'now'}
        options={[
          { value: 'now', label: t.price.startNow },
          { value: 'later', label: t.price.startLater },
        ]}
        onChange={(choice) => {
          setLater(choice === 'later')
          onChange(choice === 'later' ? todayDateOnly() : null)
        }}
      />
      {later ? (
        <Field label={t.price.startDate} hint={t.price.startDateHint}>
          <Input
            type="date"
            min={todayDateOnly()}
            value={value ?? ''}
            disabled={disabled}
            onChange={(event) => onChange(event.target.value || null)}
          />
        </Field>
      ) : null}
    </div>
  )
}

export type SubscriberChoice = 'keep' | 'apply' | null

/**
 * What happens to existing subscribers when the price of a product on sale changes (design 10.6). The plan cannot be
 * made without a choice; applying the new price to them needs the typed confirmation the plan carries.
 */
export function SubscriberChoiceField({
  value,
  onChange,
  play,
  error,
}: {
  value: SubscriberChoice
  onChange: (value: 'keep' | 'apply') => void
  play?: boolean
  error?: string | null
}) {
  const t = useMoneyT()
  return (
    <section className="card" aria-label={t.price.subscribers}>
      <div className="card-h">
        <h3 className="h3">{t.price.subscribers}</h3>
        <Badge tone="warning" small>
          {t.price.required}
        </Badge>
      </div>
      <div className="card-b grid gap-3">
        <RadioGroup<string>
          name="subscribers"
          label={t.price.subscribers}
          value={value ?? ''}
          options={[
            { value: 'keep', label: t.price.keep, description: play ? t.price.keepPlay : t.price.keepHint },
            { value: 'apply', label: t.price.apply, description: play ? t.price.applyPlay : t.price.applyHint },
          ]}
          onChange={(choice) => onChange(choice === 'apply' ? 'apply' : 'keep')}
        />
        {error ? <span className="err-t">{error}</span> : null}
        <Callout tone="warning">{play ? t.price.consentPlay : t.price.consent}</Callout>
      </div>
    </section>
  )
}

/** The permission note of a price change of a product on sale (ReleaseProduction: Owner, Admin). */
export function OnSaleNote({ canApprove }: { canApprove: boolean }) {
  const t = useMoneyT()
  return <p className="m-0 text-xs text-fg3">{canApprove ? t.price.onSale : t.price.onSaleNeeds}</p>
}

/**
 * One plan request of a form: the dry run's answer opens the plan dialog, a refusal is shown in the form (the server's
 * own message, e.g. a product id already used in the account) and the form keeps its values.
 */
export function usePlanRequest<V = void>(request: (variables: V) => Promise<ChangePlanDto>, onPlanned: (plan: ChangePlanDto) => void) {
  const t = useMoneyT()
  const [error, setError] = useState<string | null>(null)
  // The server's message keys of the refusal: field keys such as "Prices[IN].Amount" mark the rows they name.
  const [keys, setKeys] = useState<string[]>([])
  const mutation = useMutation({
    mutationFn: request,
    meta: { silentError: true },
    onMutate: () => {
      setError(null)
      setKeys([])
    },
    onSuccess: onPlanned,
    onError: (failure) => {
      setError(apiErrorMessage(failure, t.plan.failed))
      setKeys(failure instanceof ApiError ? failure.keys : [])
    },
  })
  const errorBox = error ? <FormErrors title={t.plan.failedTitle} messages={[error]} /> : null
  return { mutation, error, keys, errorBox }
}

/** Queues a sync of the store's subscriptions and In-App Purchases. */
export function SyncButton({ organizationId, storeAppId, editor }: { organizationId: number; storeAppId: number; editor: boolean }) {
  const t = useMoneyT()
  const sync = useMutation({
    mutationFn: () => subscriptionsApi.sync(organizationId, storeAppId),
    onSuccess: (jobId) => showToast(t.sync.queued, 'success', interpolate(t.sync.job, { id: jobId })),
  })
  if (!editor) {
    return null
  }
  return (
    <Button size="sm" variant="ghost" loading={sync.isPending} onClick={() => sync.mutate()}>
      <RefreshCw aria-hidden />
      {t.sync.action}
    </Button>
  )
}
