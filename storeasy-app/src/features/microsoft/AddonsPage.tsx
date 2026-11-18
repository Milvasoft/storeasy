import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Plus, ShoppingBag } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, EmptyState, Field, Input, PageHeader, SpinnerCenter, Textarea } from '@/components/ui/primitives'
import { interpolate } from '@/lib/i18n/interpolate'
import { charCount } from '@/features/common/text'
import { msApi } from './api'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import { addonTypeLabel, reasonLabel } from './labels'
import { MsSubmitDialog, MsWorkspace, SubmitButton, useMsPlan, type MsContext, type SubmitTarget } from './MsWorkspace'
import { MS_LIMITS } from './rules'
import {
  MsAddonType,
  MsChangeKind,
  MsSubmissionTarget,
  type MsAddonContentDto,
  type MsAddonDto,
  type MsAddonInputDto,
  type MsAddonListingDto,
} from './types'

// Design 10.15-10.16: the add-ons of an MSIX app. Store-managed consumables and products with mandatory updates are
// refused by the API (409), so they are marked assisted from the start and never planned; the rest are created and edited
// through plans and sent to certification like the app.

export const LIFETIMES = [
  'Forever',
  'OneDay',
  'ThreeDays',
  'FiveDays',
  'OneWeek',
  'TwoWeeks',
  'OneMonth',
  'TwoMonths',
  'ThreeMonths',
  'SixMonths',
  'OneYear',
]

function CreateAddonDialog({ open, onClose, onPlan }: { open: boolean; onClose: () => void; onPlan: (addon: MsAddonInputDto) => void }) {
  const t = useMs()
  const [productId, setProductId] = useState('')
  const [type, setType] = useState<MsAddonType>(MsAddonType.Durable)
  const [error, setError] = useState<string | null>(null)

  const close = () => {
    setProductId('')
    setType(MsAddonType.Durable)
    setError(null)
    onClose()
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = productId.trim() ? null : t.addons.productIdRequired
    setError(problem)
    if (!problem) {
      onPlan({ ProductId: productId.trim(), ProductType: type })
      close()
    }
  }

  return (
    <Modal
      open={open}
      onClose={close}
      title={t.addons.createTitle}
      size="sm"
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {t.submit.close}
          </Button>
          <Button type="submit" form="ms-create-addon" variant="primary">
            {t.planMake}
          </Button>
        </>
      }
    >
      <form id="ms-create-addon" className="grid gap-4" noValidate onSubmit={submit}>
        <Field label={t.addons.productId} hint={t.addons.productIdHint} error={error}>
          <Input mono value={productId} onChange={(event) => setProductId(event.target.value)} />
        </Field>
        <Field label={t.addons.productType}>
          <Select
            aria-label={t.addons.productType}
            value={type}
            options={[
              { value: MsAddonType.Durable, label: t.enums.addonType.durable },
              { value: MsAddonType.Consumable, label: t.enums.addonType.consumable },
            ]}
            onChange={setType}
          />
        </Field>
      </form>
    </Modal>
  )
}

/** The add-on content change: every listing (the API sets what it gets), the tier and lifetime when they changed. */
export function addonContentChange(
  listings: MsAddonListingDto[],
  priceId: string,
  lifetime: string,
  original: { listings: MsAddonListingDto[]; priceId: string; lifetime: string },
): MsAddonContentDto | null {
  const listingsChanged = JSON.stringify(listings) !== JSON.stringify(original.listings)
  const change: MsAddonContentDto = {
    Listings: listingsChanged ? listings : [],
    PriceId: priceId !== original.priceId ? priceId.trim() || null : null,
    Lifetime: lifetime !== original.lifetime ? lifetime || null : null,
  }
  return listingsChanged || change.PriceId !== null || change.Lifetime !== null ? change : null
}

function EditAddonDialog({
  addon,
  context,
  onClose,
  onPlan,
}: {
  addon: MsAddonDto | null
  context: MsContext
  onClose: () => void
  onPlan: (addon: MsAddonDto, content: MsAddonContentDto) => void
}) {
  const t = useMs()
  const { organizationId, record } = context
  const content = useQuery({
    queryKey: msKeys.content(organizationId, record.StoreAppId, null, addon?.StoreId ?? null),
    queryFn: () => msApi.content(organizationId, record.StoreAppId, { AddonStoreId: addon?.StoreId ?? null }),
    enabled: !!addon,
  })
  return (
    <Modal
      open={!!addon}
      onClose={onClose}
      size="lg"
      title={interpolate(t.addons.editTitle, { name: addon?.Title ?? addon?.ProductId ?? '' })}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.submit.close}
          </Button>
          <Button type="submit" form="ms-edit-addon" variant="primary" disabled={!content.data}>
            {t.planMake}
          </Button>
        </>
      }
    >
      {content.isLoading ? <SpinnerCenter label={t.addons.editTitle} /> : null}
      {content.isError ? <ScreenState error={content.error} onRetry={() => void content.refetch()} /> : null}
      {addon && content.data ? (
        <AddonContentForm
          key={addon.StoreId}
          listings={content.data.AddonListings ?? []}
          priceId={content.data.Pricing?.PriceId ?? addon.PriceId ?? ''}
          lifetime={content.data.AddonLifetime ?? addon.Lifetime ?? ''}
          onPlan={(change) => {
            onPlan(addon, change)
            onClose()
          }}
        />
      ) : null}
    </Modal>
  )
}

function AddonContentForm({
  listings: initial,
  priceId: initialPrice,
  lifetime: initialLifetime,
  onPlan,
}: {
  listings: MsAddonListingDto[]
  priceId: string
  lifetime: string
  onPlan: (change: MsAddonContentDto) => void
}) {
  const t = useMs()
  const [listings, setListings] = useState(
    initial.map((listing) => ({ ...listing, Title: listing.Title ?? '', Description: listing.Description ?? '' })),
  )
  const [priceId, setPriceId] = useState(initialPrice)
  const [lifetime, setLifetime] = useState(initialLifetime)
  const [locale, setLocale] = useState('')
  const original = {
    listings: initial.map((listing) => ({ ...listing, Title: listing.Title ?? '', Description: listing.Description ?? '' })),
    priceId: initialPrice,
    lifetime: initialLifetime,
  }
  const over = listings.some(
    (listing) => charCount(listing.Title ?? '') > MS_LIMITS.addonTitle || charCount(listing.Description ?? '') > MS_LIMITS.addonDescription,
  )
  const change = addonContentChange(listings, priceId, lifetime, original)
  const update = (index: number, patch: { Title?: string; Description?: string }) =>
    setListings((list) => list.map((listing, at) => (at === index ? { ...listing, ...patch } : listing)))

  return (
    <form
      id="ms-edit-addon"
      className="grid gap-4"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        if (change && !over) {
          onPlan(change)
        }
      }}
    >
      {listings.length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.addons.noContent}</p> : null}
      {listings.map((listing, index) => (
        <fieldset key={listing.Locale} className="grid gap-3 rounded-md border border-border p-3">
          <legend className="mono px-1 text-[13px]">{listing.Locale}</legend>
          <Field
            label={`${t.addons.titleField} · ${listing.Locale}`}
            counter={{ value: charCount(listing.Title ?? ''), max: MS_LIMITS.addonTitle }}
          >
            <Input value={listing.Title ?? ''} onChange={(event) => update(index, { Title: event.target.value })} />
          </Field>
          <Field
            label={`${t.addons.description} · ${listing.Locale}`}
            counter={{ value: charCount(listing.Description ?? ''), max: MS_LIMITS.addonDescription }}
          >
            <Textarea rows={3} value={listing.Description ?? ''} onChange={(event) => update(index, { Description: event.target.value })} />
          </Field>
        </fieldset>
      ))}
      <div className="flex items-end gap-2">
        <span className="flex-1">
          <Field label={t.addons.addLocale}>
            <Input mono value={locale} onChange={(event) => setLocale(event.target.value)} />
          </Field>
        </span>
        <Button
          disabled={!locale.trim() || listings.some((listing) => listing.Locale === locale.trim().toLowerCase())}
          onClick={() => {
            setListings((list) => [...list, { Locale: locale.trim().toLowerCase(), Title: '', Description: '' }])
            setLocale('')
          }}
        >
          {t.addons.addLanguage}
        </Button>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t.addons.priceId} hint={t.pricing.priceHint}>
          <Input mono value={priceId} onChange={(event) => setPriceId(event.target.value)} />
        </Field>
        <Field label={t.addons.lifetime}>
          <Select
            aria-label={t.addons.lifetime}
            value={lifetime || null}
            options={LIFETIMES.map((value) => ({ value, label: value }))}
            onChange={setLifetime}
          />
        </Field>
      </div>
      {!change ? <p className="m-0 text-xs text-fg3">{t.pricing.noChanges}</p> : null}
    </form>
  )
}

function Addons({ context }: { context: MsContext }) {
  const t = useMs()
  const { organizationId, record, editor, editable, product, app } = context
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<MsAddonDto | null>(null)
  const [target, setTarget] = useState<SubmitTarget | null>(null)
  const plan = useMsPlan(context)
  const addons = useQuery({
    queryKey: msKeys.addons(organizationId, record.StoreAppId),
    queryFn: () => msApi.addons(organizationId, record.StoreAppId),
  })
  if (addons.isLoading) {
    return <CardSkeleton />
  }
  if (addons.isError) {
    return <ScreenState error={addons.error} onRetry={() => void addons.refetch()} />
  }
  const list = addons.data ?? []
  const assisted = list.filter((addon) => !addon.ApiWritable).length
  return (
    <>
      <PageHeader
        title={t.addons.title}
        subtitle={t.addons.lead}
        actions={
          editor ? (
            <Button variant="primary" disabled={!editable} loading={plan.pending} onClick={() => setCreating(true)}>
              <Plus aria-hidden />
              {t.addons.create}
            </Button>
          ) : undefined
        }
      />
      {plan.element}
      {assisted > 0 ? (
        <div className="mb-3">
          <Callout tone="warning">
            {interpolate(t.addons.assistedCount, { count: assisted })}{' '}
            <Link className="lnk" to={`/apps/${app.Id}/assisted?store=microsoft`}>
              {t.plan.openAssisted}
            </Link>
          </Callout>
        </div>
      ) : null}
      {list.length === 0 ? (
        <EmptyState icon={<ShoppingBag aria-hidden />} title={t.addons.none} />
      ) : (
        <table className="tbl">
          <thead>
            <tr>
              <th>{t.addons.product}</th>
              <th>{t.addons.type}</th>
              <th>{t.addons.price}</th>
              <th>{t.addons.status}</th>
              <th aria-hidden />
            </tr>
          </thead>
          <tbody>
            {list.map((addon) => {
              const name = addon.Title ?? addon.ProductId ?? addon.StoreId
              return (
                <tr key={addon.StoreId}>
                  <td>
                    <b className="block">{name}</b>
                    <span className="mono text-xs text-fg3">
                      {addon.ProductId} · {addon.StoreId}
                    </span>
                  </td>
                  <td>
                    {addonTypeLabel(addon.Type, t)}
                    {addon.Lifetime ? <span className="block text-xs text-fg3">{addon.Lifetime}</span> : null}
                  </td>
                  <td className="mono">{addon.PriceId ?? t.addons.partnerCenter}</td>
                  <td>
                    <span className="flex flex-wrap gap-1">
                      {addon.LastPublishedSubmissionId ? (
                        <Badge small tone="success">
                          {t.addons.published}
                        </Badge>
                      ) : null}
                      {addon.PendingSubmissionId ? (
                        <Badge small tone="warning">
                          {t.addons.draft}
                        </Badge>
                      ) : null}
                      {!addon.ApiWritable ? (
                        <Badge small tone="info">
                          {t.addons.assisted}
                          {addon.AssistedReason ? ` · ${reasonLabel(addon.AssistedReason, t)}` : ''}
                        </Badge>
                      ) : null}
                    </span>
                  </td>
                  <td className="text-right">
                    {editor && addon.ApiWritable ? (
                      <span className="inline-flex flex-wrap justify-end gap-2">
                        <Button size="sm" disabled={!editable} aria-label={`${t.addons.edit}: ${name}`} onClick={() => setEditing(addon)}>
                          {t.addons.edit}
                        </Button>
                        {addon.PendingSubmissionId ? (
                          <SubmitButton
                            context={context}
                            label={t.addons.submit}
                            onClick={() => setTarget({ Target: MsSubmissionTarget.Addon, AddonStoreId: addon.StoreId, label: name })}
                          />
                        ) : null}
                      </span>
                    ) : null}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}
      <CreateAddonDialog
        open={creating}
        onClose={() => setCreating(false)}
        onPlan={(addon) => plan.make({ Kind: MsChangeKind.CreateAddon, Addon: addon })}
      />
      <EditAddonDialog
        addon={editing}
        context={context}
        onClose={() => setEditing(null)}
        onPlan={(addon, change) => plan.make({ Kind: MsChangeKind.AddonContent, AddonStoreId: addon.StoreId, AddonContent: change })}
      />
      <MsSubmitDialog target={target} onClose={() => setTarget(null)} organizationId={organizationId} product={product} />
    </>
  )
}

export function MsAddonsPage() {
  return <MsWorkspace sub="addons">{(context) => <Addons context={context} />}</MsWorkspace>
}
