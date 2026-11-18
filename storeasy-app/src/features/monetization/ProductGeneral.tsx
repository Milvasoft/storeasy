import { useState } from 'react'
import { AssetUpload } from '@/components/ui/AssetUpload'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Button, Checkbox, Chip, Field, Input, Switch, Textarea } from '@/components/ui/primitives'
import { AssetPurpose } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { PlanDialog } from '@/features/common/PlanView'
import { charCount } from '@/features/common/text'
import type { ChangePlanDto } from '@/features/common/types'
import { useMoneyT } from './i18n'
import { durationLabel, productKindLabel } from './labels'
import { planFor, type PriceContext } from './PricesPanel'
import { usePlanRequest } from './shared'
import { MonetizationChangeKind, StoreProductKind, type MonetizationChangeDto, type ProductDetailDto } from './types'

// Design 10.3 and 10.11: the fields of an App Store subscription or In-App Purchase. There is no save button: the
// changes collect under "Review changes" and go into one plan (design 10.17).

const NAME_MAX = 30
const DESCRIPTION_MAX = 45
const NOTE_MAX = 4000
const REFERENCE_MAX = 64

interface LocaleDraft {
  Name: string
  Description: string
}

interface Draft {
  referenceName?: string
  familySharable?: boolean
  reviewNote?: string
  localizations: Record<string, LocaleDraft>
  screenshot?: { assetId: number; fileName: string }
  availability?: { territories: string[]; inNew: boolean }
}

const EMPTY: Draft = { localizations: {} }

/** The changes of a draft as the planner reads them (MonetizationChangeDto per Kind). */
export function draftChanges(product: ProductDetailDto, draft: Draft): MonetizationChangeDto[] {
  const own = { ProductId: product.ProductId, SubscriptionId: product.Kind === StoreProductKind.Subscription ? product.Id : null }
  const changes: MonetizationChangeDto[] = []
  const update: MonetizationChangeDto = { Kind: MonetizationChangeKind.UpdateProduct, ...own }
  let updated = false
  if (draft.referenceName !== undefined && draft.referenceName.trim() !== (product.ReferenceName ?? '')) {
    update.ReferenceName = draft.referenceName.trim()
    updated = true
  }
  if (draft.familySharable !== undefined && draft.familySharable !== !!product.FamilySharable) {
    update.FamilySharable = draft.familySharable
    updated = true
  }
  if (draft.reviewNote !== undefined && draft.reviewNote !== (product.ReviewNote ?? '')) {
    update.ReviewNote = draft.reviewNote
    updated = true
  }
  if (updated) {
    changes.push(update)
  }
  for (const [locale, value] of Object.entries(draft.localizations)) {
    const current = product.Localizations?.find((item) => item.Locale === locale)
    if (value.Name !== (current?.Name ?? '') || value.Description !== (current?.Description ?? '')) {
      changes.push({
        Kind: MonetizationChangeKind.ProductLocalization,
        ...own,
        Locale: locale,
        Name: value.Name.trim(),
        Description: value.Description.trim() || null,
      })
    }
  }
  if (draft.screenshot) {
    changes.push({ Kind: MonetizationChangeKind.ReviewScreenshot, ...own, AssetId: draft.screenshot.assetId })
  }
  if (draft.availability) {
    changes.push({
      Kind: MonetizationChangeKind.Availability,
      ...own,
      Territories: draft.availability.territories,
      AvailableInNewTerritories: draft.availability.inNew,
    })
  }
  return changes
}

export function ProductGeneral({ context }: { context: PriceContext }) {
  const t = useMoneyT()
  const confirm = useConfirm()
  const { organizationId, product, owner, editor } = context
  const [draft, setDraft] = useState<Draft>(EMPTY)
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const [locale, setLocale] = useState(product.Localizations?.[0]?.Locale ?? '')
  const [newLocale, setNewLocale] = useState('')
  const [territories, setTerritories] = useState(false)
  const changes = draftChanges(product, draft)
  const { mutation, errorBox } = usePlanRequest(() => planFor(owner, organizationId, product.StoreAppId, changes), setPlan)

  const locales = [
    ...new Set([...(product.Localizations ?? []).map((item) => item.Locale ?? ''), ...Object.keys(draft.localizations)]),
  ].filter(Boolean)
  const stored = product.Localizations?.find((item) => item.Locale === locale)
  const localeValue: LocaleDraft = draft.localizations[locale] ?? { Name: stored?.Name ?? '', Description: stored?.Description ?? '' }
  const setLocaleValue = (patch: Partial<LocaleDraft>) =>
    setDraft((current) => ({ ...current, localizations: { ...current.localizations, [locale]: { ...localeValue, ...patch } } }))

  const family = draft.familySharable ?? !!product.FamilySharable
  const isConsumable = product.Kind === StoreProductKind.Consumable
  const referenceName = draft.referenceName ?? product.ReferenceName ?? ''
  const reviewNote = draft.reviewNote ?? product.ReviewNote ?? ''
  const availability = draft.availability ?? {
    territories: product.Availability?.Territories ?? [],
    inNew: product.Availability?.AvailableInNewTerritories ?? true,
  }
  const tooLong = charCount(localeValue.Name) > NAME_MAX || charCount(localeValue.Description) > DESCRIPTION_MAX

  const turnFamilyOn = async (on: boolean) => {
    if (!on) {
      setDraft((current) => ({ ...current, familySharable: false }))
      return
    }
    const ok = await confirm({ title: t.detail.familyConfirmTitle, message: t.detail.familyLocked, confirmLabel: t.detail.familyConfirm })
    if (ok) {
      setDraft((current) => ({ ...current, familySharable: true }))
    }
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_280px]">
      <div className="grid min-w-0 gap-4">
        <section className="card" aria-label={t.detail.general}>
          <div className="card-h">
            <h3 className="h3">{t.detail.general}</h3>
          </div>
          <div className="card-b grid gap-4">
            <Field label={t.common.productId} hint={t.detail.productIdLocked}>
              <Input mono value={product.ProductId ?? ''} readOnly />
            </Field>
            <Field label={t.common.referenceName} hint={t.common.referenceHint} counter={{ value: referenceName.length, max: REFERENCE_MAX }}>
              <Input
                value={referenceName}
                disabled={!editor}
                onChange={(event) => setDraft((current) => ({ ...current, referenceName: event.target.value }))}
              />
            </Field>
            {product.Kind === StoreProductKind.Subscription ? (
              <dl className="kv">
                <dt>{t.subscriptions.duration}</dt>
                <dd>{durationLabel(product.Duration, t)}</dd>
              </dl>
            ) : (
              <dl className="kv">
                <dt>{t.iap.kind}</dt>
                <dd>{productKindLabel(product.Kind, t)}</dd>
              </dl>
            )}
            {!isConsumable ? (
              <div className="flex items-start gap-3">
                <Switch
                  checked={family}
                  label={t.detail.familySharing}
                  disabled={!editor || !!product.FamilySharable}
                  onChange={(on) => void turnFamilyOn(on)}
                />
                <span className="grid gap-0.5 text-[13px]">
                  <b>{t.detail.familySharing}</b>
                  <span className="text-xs text-fg3">{t.detail.familyLocked}</span>
                </span>
              </div>
            ) : null}
            <div className="flex flex-wrap items-center gap-3 text-[13px]">
              <span>
                <b>{t.detail.territories}</b>{' '}
                {interpolate(t.detail.territoryCount, { count: availability.territories.length || product.TerritoryCount })}
                {availability.inNew ? ` · ${t.detail.newTerritories}` : ''}
              </span>
              {editor ? (
                <Button size="sm" variant="ghost" onClick={() => setTerritories(true)}>
                  {t.detail.editTerritories}
                </Button>
              ) : null}
            </div>
          </div>
        </section>

        <section className="card" aria-label={t.detail.localizations}>
          <div className="card-h flex-wrap">
            <h3 className="h3">{t.detail.localizations}</h3>
            <div className="toolbar" role="group" aria-label={t.common.locale}>
              {locales.map((item) => (
                <Chip key={item} selected={item === locale} onClick={() => setLocale(item)}>
                  {item}
                </Chip>
              ))}
            </div>
          </div>
          <div className="card-b grid gap-4">
            {locales.length === 0 ? <Callout tone="warning">{t.detail.noLocalization}</Callout> : null}
            {locale ? (
              <>
                <Field label={t.detail.displayName} counter={{ value: charCount(localeValue.Name), max: NAME_MAX }}>
                  <Input value={localeValue.Name} disabled={!editor} onChange={(event) => setLocaleValue({ Name: event.target.value })} />
                </Field>
                <Field label={t.detail.description} counter={{ value: charCount(localeValue.Description), max: DESCRIPTION_MAX }}>
                  <Textarea
                    rows={2}
                    value={localeValue.Description}
                    disabled={!editor}
                    onChange={(event) => setLocaleValue({ Description: event.target.value })}
                  />
                </Field>
              </>
            ) : null}
            {editor ? (
              <div className="flex flex-wrap items-end gap-2">
                <Field label={t.detail.newLocale}>
                  <Input mono className="w-32" value={newLocale} onChange={(event) => setNewLocale(event.target.value)} />
                </Field>
                <Button
                  disabled={!newLocale.trim() || locales.includes(newLocale.trim())}
                  onClick={() => {
                    const code = newLocale.trim()
                    setDraft((current) => ({ ...current, localizations: { ...current.localizations, [code]: { Name: '', Description: '' } } }))
                    setLocale(code)
                    setNewLocale('')
                  }}
                >
                  {t.detail.addLocale}
                </Button>
              </div>
            ) : null}
          </div>
        </section>

        <section className="card" id="review" aria-label={t.detail.review}>
          <div className="card-h">
            <h3 className="h3">{t.detail.review}</h3>
            <Badge tone={product.ReviewScreenshot || draft.screenshot ? 'success' : 'danger'} small>
              {product.ReviewScreenshot || draft.screenshot ? t.detail.reviewOk : t.detail.reviewRequired}
            </Badge>
          </div>
          <div className="card-b grid gap-4">
            <div className="grid gap-1.5">
              <span className="text-[13px] font-semibold">{t.detail.screenshot}</span>
              {product.ReviewScreenshot ? (
                <p className="m-0 text-[13px]">
                  <span className="mono">{product.ReviewScreenshot.FileName}</span>
                  {product.ReviewScreenshot.Width && product.ReviewScreenshot.Height ? (
                    <span className="text-fg3">{` · ${product.ReviewScreenshot.Width}x${product.ReviewScreenshot.Height}`}</span>
                  ) : null}
                </p>
              ) : (
                <p className="m-0 text-[13px] text-danger">{t.detail.screenshotMissing}</p>
              )}
              {editor ? (
                <AssetUpload
                  purpose={AssetPurpose.Screenshot}
                  accept={['.png', '.jpg', '.jpeg']}
                  label={product.ReviewScreenshot ? t.detail.replaceScreenshot : t.detail.uploadScreenshot}
                  hint={t.detail.screenshotHint}
                  onUploaded={(asset) => setDraft((current) => ({ ...current, screenshot: { assetId: asset.Id, fileName: asset.FileName } }))}
                />
              ) : null}
              <span className="text-xs text-fg3">{t.detail.readinessRule}</span>
            </div>
            <Field label={t.detail.reviewNote} counter={{ value: reviewNote.length, max: NOTE_MAX }} optional>
              <Textarea
                rows={3}
                value={reviewNote}
                disabled={!editor}
                onChange={(event) => setDraft((current) => ({ ...current, reviewNote: event.target.value }))}
              />
            </Field>
          </div>
        </section>
      </div>

      <aside className="grid content-start gap-4">
        <section className="card" aria-label={t.detail.status}>
          <div className="card-h">
            <h3 className="h3">{t.detail.status}</h3>
          </div>
          <div className="card-b grid gap-2 text-[13px]">
            <span className="mono text-xs text-fg3">{product.StoreState ?? '-'}</span>
            {product.SubmitWithVersion ? <Callout tone="info">{t.detail.firstWithVersion}</Callout> : null}
          </div>
        </section>
        {owner === 'subscription' ? (
          <section className="card" aria-label={t.detail.future}>
            <div className="card-h">
              <h3 className="h3">{t.detail.future}</h3>
              <Badge small>{t.detail.toVerify}</Badge>
            </div>
            <div className="card-b grid gap-2 text-[13px] text-fg3">
              <span>
                <b className="text-fg2">{t.detail.retention}</b> · {product.RetentionMessage ?? '-'}
              </span>
              <span>
                <b className="text-fg2">{t.detail.multiSeat}</b> · {product.MultiSeatStatus ?? '-'}
              </span>
              <span className="text-xs">{t.detail.futureNote}</span>
            </div>
          </section>
        ) : null}
      </aside>

      {editor ? (
        <div className="grid gap-2 lg:col-span-2">
          {errorBox}
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="primary"
              disabled={changes.length === 0 || tooLong}
              loading={mutation.isPending}
              onClick={() => mutation.mutate()}
            >
              {interpolate(t.detail.reviewChanges, { count: changes.length })}
            </Button>
            {changes.length > 0 ? (
              <Button variant="ghost" onClick={() => setDraft(EMPTY)}>
                {t.detail.discard}
              </Button>
            ) : null}
            {tooLong ? <span className="err-t">{t.detail.tooLong}</span> : null}
          </div>
        </div>
      ) : (
        <p className="m-0 text-xs text-fg3 lg:col-span-2">{t.detail.viewerNote}</p>
      )}
      {territories ? (
        <AvailabilityModal
          value={availability}
          onClose={() => setTerritories(false)}
          onSave={(next) => {
            setDraft((current) => ({ ...current, availability: next }))
            setTerritories(false)
          }}
        />
      ) : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} onDecided={() => setDraft(EMPTY)} />
    </div>
  )
}

function AvailabilityModal({
  value,
  onClose,
  onSave,
}: {
  value: { territories: string[]; inNew: boolean }
  onClose: () => void
  onSave: (value: { territories: string[]; inNew: boolean }) => void
}) {
  const t = useMoneyT()
  const [text, setText] = useState(value.territories.join(', '))
  const [inNew, setInNew] = useState(value.inNew)
  const codes = [
    ...new Set(
      text
        .split(/[\s,]+/)
        .map((code) => code.trim().toUpperCase())
        .filter(Boolean),
    ),
  ]
  const invalid = codes.filter((code) => !/^[A-Z]{3}$/.test(code))
  return (
    <Modal
      open
      onClose={onClose}
      title={t.detail.editTerritories}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button variant="primary" disabled={codes.length === 0 || invalid.length > 0} onClick={() => onSave({ territories: codes, inNew })}>
            {t.common.done}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        <Field
          label={t.detail.territoryCodes}
          hint={interpolate(t.detail.territoryCount, { count: codes.length })}
          error={invalid.length > 0 ? interpolate(t.detail.territoryInvalid, { codes: invalid.join(', ') }) : null}
        >
          <Textarea mono rows={5} value={text} onChange={(event) => setText(event.target.value)} />
        </Field>
        <Checkbox checked={inNew} onChange={setInNew}>
          {t.detail.newTerritoriesAuto}
        </Checkbox>
      </div>
    </Modal>
  )
}
