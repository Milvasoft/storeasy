import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ArrowRight } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, Checkbox, Chip, Field, Input, TableSkeleton } from '@/components/ui/primitives'
import type { ListRequest } from '@/lib/api/list'
import { OrgPermission } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { iapApi, subscriptionsApi } from './api'
import { useMoneyT } from './i18n'
import { moneyKeys } from './keys'
import { basePriceText, isOnSale, priceText } from './labels'
import {
  OnSaleNote,
  PricePointSelect,
  SourceTag,
  StartDateField,
  SubscriberChoiceField,
  usePlanRequest,
  type PriceOwner,
  type SubscriberChoice,
} from './shared'
import {
  MonetizationChangeKind,
  PriceSource,
  StoreProductKind,
  type MonetizationChangeDto,
  type PriceFilter,
  type ProductDetailDto,
  type ProductPriceDto,
} from './types'

// Design 10.4-10.6 and 10.11: the territory prices of an App Store subscription or In-App Purchase, searched and
// filtered, a territory changed by hand from its price points, the base price with the store's equalization, and the
// price change of a product on sale with the choice for existing subscribers.

const PAGE_SIZE = 20

type View = 'all' | 'manual' | 'scheduled'

function filterOf(view: View, search: string): PriceFilter {
  return {
    TerritoryName: search.trim() || null,
    Source: view === 'manual' ? PriceSource.Manual : null,
    HasPendingChange: view === 'scheduled' ? true : null,
  }
}

export interface PriceContext {
  organizationId: number
  product: ProductDetailDto
  owner: PriceOwner
  editor: boolean
  can: (permission: OrgPermission) => boolean
}

/** The plan call of the product's controller. */
export function planFor(owner: PriceOwner, organizationId: number, storeAppId: number, changes: MonetizationChangeDto[]) {
  return owner === 'subscription'
    ? subscriptionsApi.plan(organizationId, storeAppId, changes)
    : iapApi.plan(organizationId, storeAppId, changes)
}

/** Whether a price change of this product reaches existing subscribers (an App Store subscription on sale). */
export function needsSubscriberChoice(product: ProductDetailDto): boolean {
  return isOnSale(product.State) && product.Kind === StoreProductKind.Subscription
}

export function PricesPanel({ context }: { context: PriceContext }) {
  const t = useMoneyT()
  const format = useFormat()
  const { organizationId, product, owner, editor } = context
  const [view, setView] = useState<View>('all')
  const [search, setSearch] = useState('')
  const [request, setRequest] = useState<ListRequest>({ PageNumber: 1, RowCount: PAGE_SIZE })
  const [editing, setEditing] = useState<ProductPriceDto | null>(null)
  const [basing, setBasing] = useState(false)
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const filter = filterOf(view, search)
  const prices = useQuery({
    queryKey: moneyKeys.prices(organizationId, owner, product.Id, request, filter),
    queryFn: () =>
      owner === 'subscription'
        ? subscriptionsApi.prices(organizationId, product.Id, request, filter)
        : iapApi.prices(organizationId, product.Id, request, filter),
    placeholderData: keepPreviousData,
  })

  const choose = (next: View) => {
    setView(next)
    setRequest((current) => ({ ...current, PageNumber: 1 }))
  }

  const columns: Column<ProductPriceDto>[] = [
    {
      key: 'territory',
      header: t.price.territory,
      role: 'main',
      cell: (row) => (
        <span className="flex flex-wrap items-center gap-2">
          <b>{row.TerritoryName ?? row.Territory}</b>
          <span className="mono text-xs text-fg3">{row.Territory}</span>
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
      cell: (row) =>
        row.HasPendingChange ? (
          <span className="inline-flex flex-wrap items-center gap-1.5 tabular-nums">
            {priceText(row.CustomerPrice, row.Currency)}
            <ArrowRight className="h-3.5 w-3.5" aria-label={t.price.becomes} />
            <b>{priceText(row.PendingCustomerPrice, row.Currency)}</b>
            <Badge tone="info" small>
              {t.price.scheduled}
            </Badge>
          </span>
        ) : (
          <span className="tabular-nums">{priceText(row.CustomerPrice, row.Currency)}</span>
        ),
    },
    { key: 'source', header: t.price.source, cell: (row) => <SourceTag source={row.Source} /> },
    {
      key: 'proceeds',
      header: t.price.proceedsColumn,
      role: 'hidePhone',
      cell: (row) => <span className="tabular-nums">{priceText(row.Proceeds, row.Currency)}</span>,
    },
    {
      key: 'start',
      header: t.price.start,
      cell: (row) => (row.PendingStartDate ?? row.StartDate ? format.date(row.PendingStartDate ?? row.StartDate) : '-'),
    },
    {
      key: 'action',
      header: t.price.actions,
      role: 'action',
      cell: (row) =>
        editor && !row.IsBase && row.Territory ? (
          <Button size="sm" variant="ghost" onClick={() => setEditing(row)}>
            {interpolate(t.price.change, { territory: row.TerritoryName ?? row.Territory })}
          </Button>
        ) : null,
    },
  ]

  const rows = prices.data?.Data ?? []
  return (
    <div className="grid gap-4">
      <section className="card" aria-label={t.price.baseTitle}>
        <div className="card-b flex flex-wrap items-center gap-3">
          <div className="grid gap-0.5">
            <span className="text-xs text-fg3">{t.price.baseTitle}</span>
            <span className="flex flex-wrap items-center gap-2">
              <b className="text-lg tabular-nums">{basePriceText(product.BasePrice)}</b>
              {product.BasePrice ? (
                <>
                  <span className="mono text-[13px] text-fg2">{product.BasePrice.Territory}</span>
                  <SourceTag source={product.BasePrice.Source} />
                </>
              ) : (
                <span className="text-[13px] text-fg3">{t.price.noPrice}</span>
              )}
            </span>
            <span className="text-xs text-fg3">{interpolate(t.price.territoryCount, { count: product.TerritoryCount })}</span>
          </div>
          {editor ? (
            <Button className="ml-auto" onClick={() => setBasing(true)}>
              {t.price.changeBase}
            </Button>
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
            setRequest((current) => ({ ...current, PageNumber: 1 }))
          }}
        />
        <div className="toolbar" role="group" aria-label={t.price.filter}>
          <Chip selected={view === 'all'} onClick={() => choose('all')}>
            {t.price.all}
          </Chip>
          <Chip selected={view === 'manual'} onClick={() => choose('manual')}>
            {t.price.manual}
          </Chip>
          <Chip selected={view === 'scheduled'} onClick={() => choose('scheduled')}>
            {t.price.scheduled}
          </Chip>
        </div>
      </div>
      {prices.isLoading ? (
        <TableSkeleton columns={[t.price.territory, t.price.price, t.price.source]} label={t.price.title} />
      ) : prices.isError ? (
        <ScreenState error={prices.error} onRetry={() => void prices.refetch()} />
      ) : rows.length === 0 ? (
        <p className="text-[13px] text-fg3">{t.price.empty}</p>
      ) : (
        <>
          <DataTable columns={columns} rows={rows} rowKey={(row) => row.Territory ?? ''} caption={t.price.title} busy={prices.isFetching} />
          <Pagination
            page={prices.data?.CurrentPageNumber ?? request.PageNumber}
            pageCount={prices.data?.TotalPageCount ?? 1}
            total={prices.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            onPage={(page) => setRequest((current) => ({ ...current, PageNumber: page }))}
            onPageSize={(size) => setRequest({ PageNumber: 1, RowCount: size })}
          />
        </>
      )}
      {editing ? (
        <TerritoryPriceModal context={context} row={editing} onClose={() => setEditing(null)} onPlanned={setPlan} />
      ) : null}
      {basing ? <BasePriceModal context={context} onClose={() => setBasing(false)} onPlanned={setPlan} /> : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} />
    </div>
  )
}

function preserveOf(choice: SubscriberChoice): boolean | null {
  return choice === null ? null : choice === 'keep'
}

interface PriceModalProps {
  context: PriceContext
  onClose: () => void
  onPlanned: (plan: ChangePlanDto) => void
}

/** Design 10.5: one territory by hand, from that territory's price points, from a start day. */
function TerritoryPriceModal({ context, row, onClose, onPlanned }: PriceModalProps & { row: ProductPriceDto }) {
  const t = useMoneyT()
  const { organizationId, product, owner, can } = context
  const [pricePoint, setPricePoint] = useState<{ id: string; text: string } | null>(null)
  const [start, setStart] = useState<string | null>(null)
  const [choice, setChoice] = useState<SubscriberChoice>(null)
  const [choiceError, setChoiceError] = useState<string | null>(null)
  const askChoice = needsSubscriberChoice(product)
  const onSale = isOnSale(product.State)
  const territory = row.Territory ?? ''
  const { mutation, errorBox } = usePlanRequest(
    () =>
      planFor(owner, organizationId, product.StoreAppId, [
        {
          Kind: MonetizationChangeKind.TerritoryPrice,
          ProductId: product.ProductId,
          Territory: territory,
          PricePointId: pricePoint?.id ?? null,
          StartDate: start,
          PreserveCurrentPrice: askChoice ? preserveOf(choice) : null,
        },
      ]),
    (made) => {
      onPlanned(made)
      onClose()
    },
  )
  const submit = () => {
    if (askChoice && choice === null) {
      setChoiceError(t.price.choiceRequired)
      return
    }
    mutation.mutate()
  }
  const name = row.TerritoryName ?? territory
  return (
    <Modal
      open
      variant="drawer"
      onClose={onClose}
      title={interpolate(t.price.territoryTitle, { territory: name })}
      description={`${product.ReferenceName ?? product.ProductId ?? ''} · ${row.Currency ?? ''}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button variant="primary" disabled={!pricePoint} loading={mutation.isPending} onClick={submit}>
            {t.plan.add}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        <dl className="kv">
          <dt>{t.price.current}</dt>
          <dd>
            <span className="tabular-nums">{priceText(row.CustomerPrice, row.Currency)}</span> <SourceTag source={row.Source} />
          </dd>
        </dl>
        <PricePointSelect
          organizationId={organizationId}
          owner={owner}
          id={product.Id}
          territory={territory}
          value={pricePoint?.id ?? null}
          label={t.price.newPrice}
          onChange={(id, text) => setPricePoint({ id, text })}
        />
        <StartDateField label={t.price.start} value={start} onChange={setStart} />
        <p className="m-0 text-xs text-fg3">{t.price.manualNote}</p>
        {askChoice ? (
          <SubscriberChoiceField
            value={choice}
            error={choiceError}
            onChange={(next) => {
              setChoice(next)
              setChoiceError(null)
            }}
          />
        ) : null}
        {onSale ? <OnSaleNote canApprove={can(OrgPermission.ReleaseProduction)} /> : null}
      </div>
    </Modal>
  )
}

/** Design 10.6: the base territory's price point; the store equalizes the other territories unless they were set by hand. */
function BasePriceModal({ context, onClose, onPlanned }: PriceModalProps) {
  const t = useMoneyT()
  const { organizationId, product, owner, can } = context
  const [territory, setTerritory] = useState(product.BasePrice?.Territory ?? '')
  const [pricePoint, setPricePoint] = useState<{ id: string; text: string } | null>(null)
  const [equalize, setEqualize] = useState(true)
  const [start, setStart] = useState<string | null>(null)
  const [choice, setChoice] = useState<SubscriberChoice>(null)
  const [choiceError, setChoiceError] = useState<string | null>(null)
  const askChoice = needsSubscriberChoice(product)
  const onSale = isOnSale(product.State)
  const { mutation, errorBox } = usePlanRequest(
    () =>
      planFor(owner, organizationId, product.StoreAppId, [
        {
          Kind: MonetizationChangeKind.BasePrice,
          ProductId: product.ProductId,
          Territory: territory.trim().toUpperCase(),
          PricePointId: pricePoint?.id ?? null,
          StartDate: start,
          Equalize: equalize,
          PreserveCurrentPrice: askChoice ? preserveOf(choice) : null,
        },
      ]),
    (made) => {
      onPlanned(made)
      onClose()
    },
  )
  const submit = () => {
    if (askChoice && choice === null) {
      setChoiceError(t.price.choiceRequired)
      return
    }
    mutation.mutate()
  }
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={interpolate(t.price.baseChangeTitle, { name: product.ReferenceName ?? product.ProductId ?? '' })}
      description={interpolate(t.price.baseNow, { price: basePriceText(product.BasePrice) })}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button variant="primary" disabled={!pricePoint} loading={mutation.isPending} onClick={submit}>
            {t.plan.review}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t.price.baseTerritory} hint={t.price.territoryHint}>
            <Input
              mono
              maxLength={3}
              value={territory}
              onChange={(event) => {
                setTerritory(event.target.value.toUpperCase())
                setPricePoint(null)
              }}
            />
          </Field>
          <PricePointSelect
            organizationId={organizationId}
            owner={owner}
            id={product.Id}
            territory={territory}
            value={pricePoint?.id ?? null}
            label={t.price.newPrice}
            onChange={(id, text) => setPricePoint({ id, text })}
          />
        </div>
        {owner === 'subscription' ? (
          <Checkbox checked={equalize} onChange={setEqualize}>
            {t.price.equalize}
          </Checkbox>
        ) : null}
        <StartDateField label={t.price.start} value={start} onChange={setStart} />
        {askChoice ? (
          <SubscriberChoiceField
            value={choice}
            error={choiceError}
            onChange={(next) => {
              setChoice(next)
              setChoiceError(null)
            }}
          />
        ) : null}
        {onSale ? <OnSaleNote canApprove={can(OrgPermission.ReleaseProduction)} /> : null}
      </div>
    </Modal>
  )
}
