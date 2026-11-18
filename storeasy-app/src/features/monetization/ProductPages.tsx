import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ArrowLeft, PackagePlus, Plus } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Tabs } from '@/components/ui/Tabs'
import {
  Button,
  CardSkeleton,
  Chip,
  EmptyState,
  Field,
  Input,
  PageHeader,
  RadioGroup,
  TableSkeleton,
  Textarea,
} from '@/components/ui/primitives'
import type { ListRequest } from '@/lib/api/list'
import { StoreKind } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { iapApi, subscriptionsApi } from './api'
import { useMoneyT } from './i18n'
import { moneyKeys } from './keys'
import { basePriceText, productKindLabel } from './labels'
import { OffersPanel } from './OffersPanel'
import { PlayBasePlans } from './PlayPanels'
import { PlayListings, PurchaseOptions } from './PlayListings'
import { PricesPanel, type PriceContext } from './PricesPanel'
import { ProductGeneral } from './ProductGeneral'
import { APPLE_AND_GOOGLE, ProductHeading, SourceTag, StateBadge, SyncButton, usePlanRequest } from './shared'
import { MonetizationChangeKind, StoreProductKind, type IapFilter, type IapListDto, type ProductDetailDto } from './types'

// Design 10.3-10.11 and 10.14: one subscription with its general, price and offer tabs; the In-App Purchases (Play
// one-time products) list, the create dialog and one product.

export type SubscriptionTab = 'general' | 'prices' | 'offers'
export type IapTab = 'general' | 'prices'

function BackLink({ to, label }: { to: string; label: string }) {
  return (
    <Link className="lnk mb-2 inline-flex items-center gap-1 text-[13px]" to={to}>
      <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
      {label}
    </Link>
  )
}

function useProduct(context: WorkspaceContext, owner: 'subscription' | 'iap', id: number) {
  const { organizationId } = context
  return useQuery({
    queryKey: owner === 'subscription' ? moneyKeys.subscription(organizationId, id) : moneyKeys.iap(organizationId, id),
    queryFn: () => (owner === 'subscription' ? subscriptionsApi.detail(organizationId, id) : iapApi.detail(organizationId, id)),
    enabled: Number.isInteger(id) && id > 0,
  })
}

function priceContext(context: WorkspaceContext, product: ProductDetailDto, owner: 'subscription' | 'iap'): PriceContext {
  return { organizationId: context.organizationId, product, owner, editor: context.editor, can: context.can }
}

// ---- Subscription --------------------------------------------------------------------------------------------------------
export function SubscriptionDetailPage({ tab }: { tab: SubscriptionTab }) {
  return (
    <AppWorkspace section="money" sub="subscriptions" stores={APPLE_AND_GOOGLE}>
      {(context) => <SubscriptionDetail context={context} tab={tab} />}
    </AppWorkspace>
  )
}

function SubscriptionDetail({ context, tab }: { context: WorkspaceContext; tab: SubscriptionTab }) {
  const t = useMoneyT()
  const { app, storeQuery } = context
  const { subscriptionId: rawId = '' } = useParams()
  const id = Number(rawId)
  const detail = useProduct(context, 'subscription', id)
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  if (detail.isLoading) {
    return <CardSkeleton count={3} />
  }
  if (detail.isError || !detail.data) {
    return <ScreenState error={detail.error} onRetry={() => void detail.refetch()} />
  }
  const product = detail.data
  const base = `/apps/${app.Id}/money/subscriptions/${product.Id}`
  const prices = priceContext(context, product, 'subscription')
  const back = <BackLink to={`/apps/${app.Id}/money/subscriptions${storeQuery}`} label={t.subscriptions.title} />
  if (product.Store === StoreKind.Google) {
    return (
      <div className="grid gap-4">
        <div>
          {back}
          <ProductHeading
            name={product.Localizations?.[0]?.Name ?? product.ProductId ?? ''}
            productId={product.ProductId}
            store={product.Store}
            state={product.State}
            storeState={product.StoreState}
          />
        </div>
        <PlayListings context={prices} />
        <section className="card" aria-label={t.play.basePlans}>
          <div className="card-h">
            <h3 className="h3">{t.play.basePlans}</h3>
          </div>
          <div className="card-b">
            <PlayBasePlans
              context={context}
              subscription={{ Id: product.Id, ProductId: product.ProductId, BasePlans: product.Plans }}
              onPlanned={setPlan}
            />
          </div>
        </section>
        <PlanDialog plan={plan} onClose={() => setPlan(null)} />
      </div>
    )
  }
  return (
    <div>
      {back}
      <ProductHeading
        name={product.ReferenceName ?? product.ProductId ?? ''}
        productId={product.ProductId}
        store={product.Store}
        state={product.State}
        storeState={product.StoreState}
      />
      <Tabs
        label={t.detail.tabs}
        items={[
          { value: 'general', label: t.detail.general, to: `${base}${storeQuery}` },
          { value: 'prices', label: t.detail.prices, to: `${base}/prices${storeQuery}` },
          { value: 'offers', label: t.detail.offers, count: product.OfferCount, to: `${base}/offers${storeQuery}` },
        ]}
      />
      <div className="mt-4">
        {tab === 'prices' ? (
          <PricesPanel context={prices} />
        ) : tab === 'offers' ? (
          <OffersPanel context={prices} />
        ) : (
          <ProductGeneral context={prices} />
        )}
      </div>
    </div>
  )
}

// ---- In-App Purchases ----------------------------------------------------------------------------------------------------
const PAGE_SIZE = 20
const APPLE_KINDS = [StoreProductKind.Consumable, StoreProductKind.NonConsumable, StoreProductKind.NonRenewingSubscription] as const

export function IapListPage() {
  return (
    <AppWorkspace section="money" sub="iap" stores={APPLE_AND_GOOGLE}>
      {(context) => <IapList context={context} />}
    </AppWorkspace>
  )
}

function IapList({ context }: { context: WorkspaceContext }) {
  const t = useMoneyT()
  const { app, organizationId, record, editor, storeQuery } = context
  const play = record.Store === StoreKind.Google
  const [request, setRequest] = useState<ListRequest>({ PageNumber: 1, RowCount: PAGE_SIZE })
  const [filter, setFilter] = useState<IapFilter>({})
  const [creating, setCreating] = useState(false)
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const list = useQuery({
    queryKey: moneyKeys.iaps(organizationId, record.StoreAppId, request, filter),
    queryFn: () => iapApi.list(organizationId, record.StoreAppId, request, filter),
    placeholderData: keepPreviousData,
  })
  const rows = list.data?.Data ?? []
  const detailOf = (row: IapListDto) => `/apps/${app.Id}/money/iap/${row.Id}${storeQuery}`
  const setFilterTo = (next: IapFilter) => {
    setFilter(next)
    setRequest((current) => ({ ...current, PageNumber: 1 }))
  }

  const columns: Column<IapListDto>[] = [
    {
      key: 'product',
      header: t.iap.product,
      role: 'main',
      cell: (row) => (
        <span className="grid gap-0.5">
          <Link className="lnk font-semibold" to={detailOf(row)}>
            {row.ReferenceName ?? row.ProductId}
          </Link>
          <span className="mono text-xs text-fg3">{row.ProductId}</span>
        </span>
      ),
    },
    play
      ? {
          key: 'options',
          header: t.iap.options,
          cell: (row) => (row.PurchaseOptions ?? []).map((option) => option.BasePlanId).join(', ') || '-',
        }
      : { key: 'kind', header: t.iap.kind, cell: (row) => productKindLabel(row.Kind, t) },
    {
      key: 'price',
      header: t.iap.basePrice,
      cell: (row) =>
        row.BasePrice ? (
          <span className="flex items-center gap-1.5 tabular-nums">
            {basePriceText(row.BasePrice)}
            <SourceTag source={row.BasePrice.Source} />
          </span>
        ) : (
          <span className="text-fg3">{t.price.noPrice}</span>
        ),
    },
    {
      key: 'state',
      header: t.iap.state,
      cell: (row) => (
        <span className="flex flex-wrap items-center gap-1.5">
          <StateBadge state={row.State} />
          {!play && !row.HasReviewScreenshot ? (
            <Link className="lnk text-xs" to={`${detailOf(row)}#review`}>
              {t.attention.noScreenshot}
            </Link>
          ) : null}
        </span>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title={t.iap.title}
        subtitle={play ? t.iap.playSubtitle : t.iap.subtitle}
        actions={
          <>
            <SyncButton organizationId={organizationId} storeAppId={record.StoreAppId} editor={editor} />
            {editor ? (
              <Button onClick={() => setCreating(true)}>
                <Plus aria-hidden />
                {t.iap.create}
              </Button>
            ) : null}
          </>
        }
      />
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <Input
          className="max-w-xs"
          aria-label={t.iap.search}
          placeholder={t.iap.search}
          value={filter.Search ?? ''}
          onChange={(event) => setFilterTo({ ...filter, Search: event.target.value || null })}
        />
        {!play ? (
          <div className="toolbar" role="group" aria-label={t.iap.kind}>
            <Chip selected={!filter.Kind} onClick={() => setFilterTo({ ...filter, Kind: null })}>
              {interpolate(t.iap.all, { count: list.data?.TotalDataCount ?? rows.length })}
            </Chip>
            {APPLE_KINDS.map((kind) => (
              <Chip key={kind} selected={filter.Kind === kind} onClick={() => setFilterTo({ ...filter, Kind: kind })}>
                {productKindLabel(kind, t)}
              </Chip>
            ))}
          </div>
        ) : null}
      </div>
      {list.isLoading ? (
        <TableSkeleton columns={[t.iap.product, t.iap.kind, t.iap.basePrice, t.iap.state]} label={t.iap.title} />
      ) : list.isError ? (
        <ScreenState error={list.error} onRetry={() => void list.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState
          icon={<PackagePlus aria-hidden />}
          title={filter.Search || filter.Kind ? t.iap.noMatch : interpolate(t.iap.emptyTitle, { app: app.Name })}
          description={t.iap.emptyBody}
        />
      ) : (
        <>
          <DataTable columns={columns} rows={rows} rowKey={(row) => row.Id} caption={t.iap.title} busy={list.isFetching} />
          <Pagination
            page={list.data?.CurrentPageNumber ?? request.PageNumber}
            pageCount={list.data?.TotalPageCount ?? 1}
            total={list.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            onPage={(page) => setRequest((current) => ({ ...current, PageNumber: page }))}
            onPageSize={(size) => setRequest({ PageNumber: 1, RowCount: size })}
          />
        </>
      )}
      {play ? <p className="mt-3 text-xs text-fg3">{t.play.consumableNote}</p> : null}
      {creating ? <CreateIapModal context={context} onClose={() => setCreating(false)} onPlanned={setPlan} /> : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} />
    </>
  )
}

/** Design 10.10 and 10.19: kind and product id are asked together, since neither changes once created. */
function CreateIapModal({
  context,
  onClose,
  onPlanned,
}: {
  context: WorkspaceContext
  onClose: () => void
  onPlanned: (plan: ChangePlanDto) => void
}) {
  const t = useMoneyT()
  const { app, organizationId, record } = context
  const play = record.Store === StoreKind.Google
  const [kind, setKind] = useState<StoreProductKind>(StoreProductKind.NonConsumable)
  const [productId, setProductId] = useState('')
  const [name, setName] = useState('')
  const [locale, setLocale] = useState('tr-TR')
  const [description, setDescription] = useState('')
  const [price, setPrice] = useState({ region: 'TR', currency: 'TRY', amount: '' })
  const [touched, setTouched] = useState(false)
  const { mutation, errorBox, error } = usePlanRequest(
    () =>
      iapApi.plan(
        organizationId,
        record.StoreAppId,
        play
          ? [
              {
                Kind: MonetizationChangeKind.OneTimeProduct,
                ProductId: productId.trim(),
                PurchaseOptionId: 'buy',
                Localizations: [{ Locale: locale.trim(), Name: name.trim(), Description: description.trim() }],
                Prices: [{ Territory: price.region, Currency: price.currency, Amount: price.amount.trim() }],
              },
            ]
          : [{ Kind: MonetizationChangeKind.CreateIap, ProductKind: kind, ProductId: productId.trim(), ReferenceName: name.trim() }],
      ),
    (made) => {
      onPlanned(made)
      onClose()
    },
  )
  const nameError = touched && !name.trim() ? t.common.required : null
  const idError = touched && !productId.trim() ? t.common.required : error ? t.iap.idRefused : null
  const valid = productId.trim() && name.trim() && (!play || (description.trim() && price.amount.trim()))
  return (
    <Modal
      open
      onClose={onClose}
      title={t.iap.createTitle}
      description={`${app.Name} · ${play ? 'Google Play' : 'App Store'}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button
            variant="primary"
            loading={mutation.isPending}
            onClick={() => {
              setTouched(true)
              if (valid) {
                mutation.mutate()
              }
            }}
          >
            {t.plan.add}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        {!play ? (
          <RadioGroup<StoreProductKind>
            name="kind"
            label={t.iap.kind}
            value={kind}
            options={APPLE_KINDS.map((value) => ({
              value,
              label: productKindLabel(value, t),
              description: (t.iap.kindHint as Record<string, string>)[String(value)],
            }))}
            onChange={setKind}
          />
        ) : null}
        <Field label={t.common.productId} hint={play ? t.play.productIdHint : t.common.productIdHint} error={idError}>
          <Input mono value={productId} onChange={(event) => setProductId(event.target.value)} />
        </Field>
        {play ? (
          <>
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label={t.common.locale}>
                <Input mono value={locale} onChange={(event) => setLocale(event.target.value)} />
              </Field>
              <div className="sm:col-span-2">
                <Field label={t.play.title} error={nameError}>
                  <Input value={name} onChange={(event) => setName(event.target.value)} />
                </Field>
              </div>
            </div>
            <Field label={t.detail.description}>
              <Textarea rows={2} value={description} onChange={(event) => setDescription(event.target.value)} />
            </Field>
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label={t.play.region}>
                <Input
                  mono
                  maxLength={2}
                  value={price.region}
                  onChange={(event) => setPrice({ ...price, region: event.target.value.toUpperCase() })}
                />
              </Field>
              <Field label={t.play.currency}>
                <Input
                  mono
                  maxLength={3}
                  value={price.currency}
                  onChange={(event) => setPrice({ ...price, currency: event.target.value.toUpperCase() })}
                />
              </Field>
              <Field label={t.play.amount} hint={t.play.amountHint}>
                <Input
                  inputMode="decimal"
                  value={price.amount}
                  onChange={(event) => setPrice({ ...price, amount: event.target.value.replace(',', '.') })}
                />
              </Field>
            </div>
          </>
        ) : (
          <Field label={t.common.referenceName} hint={t.common.referenceHint} error={nameError}>
            <Input value={name} maxLength={64} onChange={(event) => setName(event.target.value)} />
          </Field>
        )}
      </div>
    </Modal>
  )
}

export function IapDetailPage({ tab }: { tab: IapTab }) {
  return (
    <AppWorkspace section="money" sub="iap" stores={APPLE_AND_GOOGLE}>
      {(context) => <IapDetail context={context} tab={tab} />}
    </AppWorkspace>
  )
}

function IapDetail({ context, tab }: { context: WorkspaceContext; tab: IapTab }) {
  const t = useMoneyT()
  const { app, storeQuery } = context
  const { iapId: rawId = '' } = useParams()
  const id = Number(rawId)
  const format = useFormat()
  const detail = useProduct(context, 'iap', id)
  if (detail.isLoading) {
    return <CardSkeleton count={3} />
  }
  if (detail.isError || !detail.data) {
    return <ScreenState error={detail.error} onRetry={() => void detail.refetch()} />
  }
  const product = detail.data
  const prices = priceContext(context, product, 'iap')
  const base = `/apps/${app.Id}/money/iap/${product.Id}`
  const heading = (
    <>
      <BackLink to={`/apps/${app.Id}/money/iap${storeQuery}`} label={t.iap.title} />
      <ProductHeading
        name={product.ReferenceName ?? product.Localizations?.[0]?.Name ?? product.ProductId ?? ''}
        productId={product.ProductId}
        store={product.Store}
        state={product.State}
        storeState={product.StoreState}
        extra={<Badge small>{productKindLabel(product.Kind, t)}</Badge>}
      />
      <p className="mb-3 mt-0 text-xs text-fg3">{interpolate(t.sync.last, { date: format.dateTime(product.SyncedDate) })}</p>
    </>
  )
  if (product.Store === StoreKind.Google) {
    return (
      <div className="grid gap-4">
        <div>{heading}</div>
        <PlayListings context={prices} />
        <PurchaseOptions context={prices} />
      </div>
    )
  }
  return (
    <div>
      {heading}
      <Tabs
        label={t.detail.tabs}
        items={[
          { value: 'general', label: t.detail.general, to: `${base}${storeQuery}` },
          { value: 'prices', label: t.detail.prices, to: `${base}/prices${storeQuery}` },
        ]}
      />
      <div className="mt-4">{tab === 'prices' ? <PricesPanel context={prices} /> : <ProductGeneral context={prices} />}</div>
    </div>
  )
}
