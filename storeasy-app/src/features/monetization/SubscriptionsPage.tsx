import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, CircleDollarSign, Plus } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, IconButton, Input, PageHeader, Textarea } from '@/components/ui/primitives'
import { StoreKind } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { subscriptionsApi } from './api'
import { useMoneyT } from './i18n'
import { moneyKeys } from './keys'
import { basePriceText, durationLabel, SUBSCRIPTION_PERIODS } from './labels'
import { PlayBasePlans } from './PlayPanels'
import { APPLE_AND_GOOGLE, SourceTag, StateBadge, SyncButton, usePlanRequest } from './shared'
import { MonetizationChangeKind, type SubscriptionGroupDto, type SubscriptionGroupsDto, type SubscriptionSummaryDto } from './types'

// Design 10.1, 10.2 and 10.12: the App Store subscription groups in level order with their localizations, the empty
// state, and the Play subscriptions with their base plans and offers. Every write is a change plan.

export function SubscriptionsPage() {
  return (
    <AppWorkspace section="money" sub="subscriptions" stores={APPLE_AND_GOOGLE}>
      {(context) => <Subscriptions context={context} />}
    </AppWorkspace>
  )
}

function Subscriptions({ context }: { context: WorkspaceContext }) {
  const t = useMoneyT()
  const format = useFormat()
  const { organizationId, record } = context
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const groups = useQuery({
    queryKey: moneyKeys.groups(organizationId, record.StoreAppId),
    queryFn: () => subscriptionsApi.groups(organizationId, record.StoreAppId),
  })
  if (groups.isLoading) {
    return <CardSkeleton count={2} />
  }
  if (groups.isError || !groups.data) {
    return <ScreenState error={groups.error} onRetry={() => void groups.refetch()} />
  }
  const data = groups.data
  const synced = data.SyncedDate ? interpolate(t.sync.last, { date: format.dateTime(data.SyncedDate) }) : t.sync.never
  return (
    <>
      {record.Store === StoreKind.Google ? (
        <PlaySubscriptions context={context} data={data} synced={synced} onPlanned={setPlan} />
      ) : (
        <AppleGroups context={context} data={data} synced={synced} onPlanned={setPlan} />
      )}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} />
    </>
  )
}

interface ListProps {
  context: WorkspaceContext
  data: SubscriptionGroupsDto
  synced: string
  onPlanned: (plan: ChangePlanDto) => void
}

// ---- App Store -----------------------------------------------------------------------------------------------------------
function AppleGroups({ context, data, synced, onPlanned }: ListProps) {
  const t = useMoneyT()
  const { app, organizationId, record, editor, storeQuery } = context
  const [creatingGroup, setCreatingGroup] = useState(false)
  const groups = data.Groups ?? []
  const subscriptions = groups.flatMap((group) => group.Subscriptions ?? [])
  const noScreenshot = subscriptions.filter((item) => !item.HasReviewScreenshot)
  const noLocalization = groups.filter((group) => (group.Localizations ?? []).length === 0)
  return (
    <>
      <PageHeader
        title={t.subscriptions.title}
        subtitle={synced}
        actions={
          <>
            <SyncButton organizationId={organizationId} storeAppId={record.StoreAppId} editor={editor} />
            {editor && groups.length > 0 ? (
              <Button onClick={() => setCreatingGroup(true)}>
                <Plus aria-hidden />
                {t.subscriptions.createGroup}
              </Button>
            ) : null}
          </>
        }
      />
      {noScreenshot.length > 0 || noLocalization.length > 0 ? (
        <Callout tone="warning" title={t.attention.title} className="mb-4">
          <ul className="m-0 grid list-disc gap-1 pl-4">
            {noScreenshot.map((item) => (
              <li key={`s-${item.Id}`}>
                {interpolate(t.attention.screenshot, { name: item.ReferenceName ?? item.ProductId ?? '' })}{' '}
                <Link className="lnk" to={`/apps/${app.Id}/money/subscriptions/${item.Id}${storeQuery}#review`}>
                  {t.attention.upload}
                </Link>
              </li>
            ))}
            {noLocalization.map((group) => (
              <li key={`g-${group.Id}`}>
                {interpolate(t.attention.groupLocalization, { name: group.ReferenceName ?? '' })}{' '}
                <a className="lnk" href={`#group-${group.Id}`}>
                  {t.attention.add}
                </a>
              </li>
            ))}
          </ul>
          <p className="m-0 mt-1 text-xs">{t.attention.readiness}</p>
        </Callout>
      ) : null}
      {groups.length === 0 ? (
        <EmptyState
          icon={<CircleDollarSign aria-hidden />}
          title={interpolate(t.subscriptions.emptyTitle, { app: app.Name })}
          description={t.subscriptions.emptyBody}
          action={
            editor ? (
              <Button variant="primary" onClick={() => setCreatingGroup(true)}>
                {t.subscriptions.createGroupFirst}
              </Button>
            ) : (
              <span className="text-xs text-fg3">{t.common.editorOnly}</span>
            )
          }
        >
          <p className="text-xs text-fg3">
            {t.subscriptions.oneTimeHint}{' '}
            <Link className="lnk" to={`/apps/${app.Id}/money/iap${storeQuery}`}>
              {t.subscriptions.oneTimeLink}
            </Link>
          </p>
        </EmptyState>
      ) : (
        <div className="grid gap-4">
          {groups.map((group) => (
            <GroupCard key={group.Id} context={context} group={group} onPlanned={onPlanned} />
          ))}
        </div>
      )}
      {creatingGroup ? (
        <CreateGroupModal context={context} onClose={() => setCreatingGroup(false)} onPlanned={onPlanned} />
      ) : null}
    </>
  )
}

function GroupCard({
  context,
  group,
  onPlanned,
}: {
  context: WorkspaceContext
  group: SubscriptionGroupDto
  onPlanned: (plan: ChangePlanDto) => void
}) {
  const t = useMoneyT()
  const { app, organizationId, record, editor, storeQuery } = context
  const synced = [...(group.Subscriptions ?? [])].sort((a, b) => (a.Level ?? 99) - (b.Level ?? 99))
  // The level order being edited; null while it is the store's order.
  const [order, setOrder] = useState<SubscriptionSummaryDto[] | null>(null)
  const [adding, setAdding] = useState(false)
  const [localizing, setLocalizing] = useState<{ locale: string; name: string; customAppName: string } | null>(null)
  const rows = order ?? synced
  const moved = rows.filter((item, index) => item.Level !== index + 1)
  const levels = usePlanRequest(
    () =>
      subscriptionsApi.plan(
        organizationId,
        record.StoreAppId,
        moved.map((item) => ({ Kind: MonetizationChangeKind.Level, ProductId: item.ProductId, Level: rows.indexOf(item) + 1 })),
      ),
    (made) => {
      setOrder(null)
      onPlanned(made)
    },
  )
  const move = (index: number, by: number) => {
    const next = [...rows]
    const [item] = next.splice(index, 1)
    next.splice(index + by, 0, item)
    setOrder(next)
  }
  return (
    <section className="card" id={`group-${group.Id}`} aria-label={group.ReferenceName ?? ''}>
      <div className="card-h flex-wrap">
        <h2 className="h3">{group.ReferenceName}</h2>
        <span className="mono text-xs text-fg3">{interpolate(t.subscriptions.groupId, { id: group.ExternalId ?? '-' })}</span>
        {editor ? (
          <Button size="sm" className="ml-auto" onClick={() => setAdding(true)}>
            <Plus aria-hidden />
            {t.subscriptions.addSubscription}
          </Button>
        ) : null}
      </div>
      <div className="card-b grid gap-3">
        {rows.length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.subscriptions.groupEmpty}</p> : null}
        <ol className="m-0 grid list-none gap-2 p-0" aria-label={t.subscriptions.levels}>
          {rows.map((item, index) => (
            <li key={item.Id} className="flex flex-wrap items-center gap-3 rounded-lg border border-border p-2.5">
              <Badge small>{interpolate(t.subscriptions.level, { level: index + 1 })}</Badge>
              <span className="grid min-w-0 flex-1 gap-0.5">
                <Link className="lnk font-semibold" to={`/apps/${app.Id}/money/subscriptions/${item.Id}${storeQuery}`}>
                  {item.ReferenceName ?? item.ProductId}
                </Link>
                <span className="mono text-xs text-fg3">
                  {item.ProductId} · {durationLabel(item.Duration, t)}
                </span>
              </span>
              <span className="flex items-center gap-1.5 tabular-nums">
                {basePriceText(item.BasePrice)}
                {item.BasePrice ? <SourceTag source={item.BasePrice.Source} /> : null}
              </span>
              <StateBadge state={item.State} />
              {!item.HasReviewScreenshot ? (
                <Badge tone="danger" small>
                  {t.attention.noScreenshot}
                </Badge>
              ) : null}
              {editor ? (
                <span className="flex gap-1">
                  <IconButton
                    label={interpolate(t.subscriptions.up, { name: item.ReferenceName ?? '' })}
                    disabled={index === 0}
                    onClick={() => move(index, -1)}
                  >
                    <ArrowUp aria-hidden />
                  </IconButton>
                  <IconButton
                    label={interpolate(t.subscriptions.down, { name: item.ReferenceName ?? '' })}
                    disabled={index === rows.length - 1}
                    onClick={() => move(index, 1)}
                  >
                    <ArrowDown aria-hidden />
                  </IconButton>
                </span>
              ) : null}
            </li>
          ))}
        </ol>
        <p className="m-0 text-xs text-fg3">{t.subscriptions.levelHint}</p>
        {levels.errorBox}
        {order && moved.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            <Button variant="primary" loading={levels.mutation.isPending} onClick={() => levels.mutation.mutate()}>
              {interpolate(t.subscriptions.reviewOrder, { count: moved.length })}
            </Button>
            <Button variant="ghost" onClick={() => setOrder(null)}>
              {t.common.cancel}
            </Button>
          </div>
        ) : null}
        <div className="grid gap-2">
          <h3 className="h3 m-0">{t.subscriptions.localizations}</h3>
          {(group.Localizations ?? []).length === 0 ? (
            <p className="m-0 text-[13px] text-fg3">{t.subscriptions.noLocalization}</p>
          ) : (
            <ul className="m-0 grid list-none gap-1 p-0 text-[13px]">
              {(group.Localizations ?? []).map((localization) => (
                <li key={localization.Locale} className="flex flex-wrap items-center gap-2">
                  <span className="mono">{localization.Locale}</span>
                  <b>{localization.Name}</b>
                  <span className="text-fg3">
                    {localization.CustomAppName
                      ? interpolate(t.subscriptions.appName, { name: localization.CustomAppName })
                      : t.subscriptions.appNameDefault}
                  </span>
                  {localization.State ? <span className="mono text-[11px] text-fg3">{localization.State}</span> : null}
                  {editor ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() =>
                        setLocalizing({
                          locale: localization.Locale ?? '',
                          name: localization.Name ?? '',
                          customAppName: localization.CustomAppName ?? '',
                        })
                      }
                    >
                      {interpolate(t.common.editLocale, { locale: localization.Locale ?? '' })}
                    </Button>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
          {editor ? (
            <div>
              <Button size="sm" onClick={() => setLocalizing({ locale: '', name: '', customAppName: '' })}>
                {t.subscriptions.addLocalization}
              </Button>
            </div>
          ) : null}
        </div>
      </div>
      {adding ? (
        <CreateSubscriptionModal
          context={context}
          group={group}
          nextLevel={synced.length + 1}
          onClose={() => setAdding(false)}
          onPlanned={onPlanned}
        />
      ) : null}
      {localizing ? (
        <GroupLocalizationModal
          context={context}
          group={group}
          initial={localizing}
          onClose={() => setLocalizing(null)}
          onPlanned={onPlanned}
        />
      ) : null}
    </section>
  )
}

interface ModalProps {
  context: WorkspaceContext
  onClose: () => void
  onPlanned: (plan: ChangePlanDto) => void
}

function CreateGroupModal({ context, onClose, onPlanned }: ModalProps) {
  const t = useMoneyT()
  const { organizationId, record } = context
  const [name, setName] = useState('')
  const [locale, setLocale] = useState('tr')
  const [displayName, setDisplayName] = useState('')
  const [appName, setAppName] = useState('')
  const [touched, setTouched] = useState(false)
  const { mutation, errorBox } = usePlanRequest(
    () =>
      subscriptionsApi.plan(organizationId, record.StoreAppId, [
        {
          Kind: MonetizationChangeKind.CreateGroup,
          ReferenceName: name.trim(),
          Localizations: displayName.trim()
            ? [{ Locale: locale.trim(), Name: displayName.trim(), CustomAppName: appName.trim() || null }]
            : [],
        },
      ]),
    (made) => {
      onPlanned(made)
      onClose()
    },
  )
  const nameError = touched && !name.trim() ? t.common.required : null
  return (
    <Modal
      open
      onClose={onClose}
      title={t.subscriptions.createGroup}
      description={t.subscriptions.createGroupBody}
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
              if (name.trim()) {
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
        <Field label={t.subscriptions.groupName} hint={t.common.referenceHint} error={nameError}>
          <Input value={name} maxLength={64} onChange={(event) => setName(event.target.value)} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label={t.common.locale}>
            <Input mono value={locale} onChange={(event) => setLocale(event.target.value)} />
          </Field>
          <Field label={t.subscriptions.displayName}>
            <Input value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
          </Field>
          <Field label={t.subscriptions.customAppName} optional>
            <Input value={appName} onChange={(event) => setAppName(event.target.value)} />
          </Field>
        </div>
      </div>
    </Modal>
  )
}

function CreateSubscriptionModal({
  context,
  group,
  nextLevel,
  onClose,
  onPlanned,
}: ModalProps & { group: SubscriptionGroupDto; nextLevel: number }) {
  const t = useMoneyT()
  const { organizationId, record } = context
  const [productId, setProductId] = useState('')
  const [name, setName] = useState('')
  const [duration, setDuration] = useState<string>('ONE_MONTH')
  const [family, setFamily] = useState(false)
  const [touched, setTouched] = useState(false)
  const { mutation, errorBox } = usePlanRequest(
    () =>
      subscriptionsApi.plan(organizationId, record.StoreAppId, [
        {
          Kind: MonetizationChangeKind.CreateSubscription,
          GroupId: group.Id,
          GroupReferenceName: group.ReferenceName,
          ProductId: productId.trim(),
          ReferenceName: name.trim(),
          Duration: duration,
          Level: nextLevel,
          FamilySharable: family,
        },
      ]),
    (made) => {
      onPlanned(made)
      onClose()
    },
  )
  const valid = productId.trim() && name.trim()
  return (
    <Modal
      open
      onClose={onClose}
      title={t.subscriptions.addSubscription}
      description={group.ReferenceName ?? undefined}
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
        <Field label={t.common.productId} hint={t.common.productIdHint} error={touched && !productId.trim() ? t.common.required : null}>
          <Input mono value={productId} onChange={(event) => setProductId(event.target.value)} />
        </Field>
        <Field label={t.common.referenceName} hint={t.common.referenceHint} error={touched && !name.trim() ? t.common.required : null}>
          <Input value={name} maxLength={64} onChange={(event) => setName(event.target.value)} />
        </Field>
        <Field label={t.subscriptions.duration}>
          <Select
            value={duration}
            options={SUBSCRIPTION_PERIODS.map((period) => ({ value: period, label: durationLabel(period, t) }))}
            onChange={setDuration}
          />
        </Field>
        <Checkbox checked={family} onChange={setFamily}>
          {t.detail.familySharing}
        </Checkbox>
        {family ? <p className="m-0 text-xs text-fg3">{t.detail.familyLocked}</p> : null}
      </div>
    </Modal>
  )
}

function GroupLocalizationModal({
  context,
  group,
  initial,
  onClose,
  onPlanned,
}: ModalProps & { group: SubscriptionGroupDto; initial: { locale: string; name: string; customAppName: string } }) {
  const t = useMoneyT()
  const { organizationId, record } = context
  const [locale, setLocale] = useState(initial.locale)
  const [name, setName] = useState(initial.name)
  const [appName, setAppName] = useState(initial.customAppName)
  const { mutation, errorBox } = usePlanRequest(
    () =>
      subscriptionsApi.plan(organizationId, record.StoreAppId, [
        {
          Kind: MonetizationChangeKind.GroupLocalization,
          GroupId: group.Id,
          GroupReferenceName: group.ReferenceName,
          Locale: locale.trim(),
          Name: name.trim(),
          CustomAppName: appName.trim() || null,
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
      title={t.subscriptions.localizationTitle}
      description={group.ReferenceName ?? undefined}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button variant="primary" disabled={!locale.trim() || !name.trim()} loading={mutation.isPending} onClick={() => mutation.mutate()}>
            {t.plan.add}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        <Field label={t.common.locale}>
          <Input mono value={locale} disabled={!!initial.locale} onChange={(event) => setLocale(event.target.value)} />
        </Field>
        <Field label={t.subscriptions.displayName}>
          <Input value={name} onChange={(event) => setName(event.target.value)} />
        </Field>
        <Field label={t.subscriptions.customAppName} hint={t.subscriptions.customAppNameHint} optional>
          <Input value={appName} onChange={(event) => setAppName(event.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

// ---- Google Play ---------------------------------------------------------------------------------------------------------
function PlaySubscriptions({ context, data, synced, onPlanned }: ListProps) {
  const t = useMoneyT()
  const { app, organizationId, record, editor, storeQuery } = context
  const [adding, setAdding] = useState(false)
  const subscriptions = data.PlaySubscriptions ?? []
  return (
    <>
      <PageHeader
        title={t.subscriptions.title}
        subtitle={synced}
        actions={
          <>
            <SyncButton organizationId={organizationId} storeAppId={record.StoreAppId} editor={editor} />
            {editor ? (
              <Button onClick={() => setAdding(true)}>
                <Plus aria-hidden />
                {t.play.addSubscription}
              </Button>
            ) : null}
          </>
        }
      />
      {subscriptions.length === 0 ? (
        <EmptyState icon={<CircleDollarSign aria-hidden />} title={interpolate(t.play.emptyTitle, { app: app.Name })} description={t.play.emptyBody} />
      ) : (
        <div className="grid gap-4">
          {subscriptions.map((subscription) => (
            <section key={subscription.Id} className="card" aria-label={subscription.ProductId ?? ''}>
              <div className="card-h flex-wrap">
                <h2 className="h3">{subscription.Listings?.[0]?.Name ?? subscription.ProductId}</h2>
                <span className="mono text-xs text-fg3">{subscription.ProductId}</span>
                <StateBadge state={subscription.State} />
                <Link className="lnk ml-auto text-[13px]" to={`/apps/${app.Id}/money/subscriptions/${subscription.Id}${storeQuery}`}>
                  {t.play.listings}
                </Link>
              </div>
              <div className="card-b">
                <PlayBasePlans context={context} subscription={subscription} onPlanned={onPlanned} />
              </div>
            </section>
          ))}
          <p className="m-0 text-xs text-fg3">{t.play.noDelete}</p>
        </div>
      )}
      {adding ? <PlaySubscriptionModal context={context} onClose={() => setAdding(false)} onPlanned={onPlanned} /> : null}
    </>
  )
}

function PlaySubscriptionModal({ context, onClose, onPlanned }: ModalProps) {
  const t = useMoneyT()
  const { organizationId, record } = context
  const [productId, setProductId] = useState('')
  const [locale, setLocale] = useState('tr-TR')
  const [title, setTitle] = useState('')
  const [benefits, setBenefits] = useState('')
  const { mutation, errorBox } = usePlanRequest(
    () =>
      subscriptionsApi.plan(organizationId, record.StoreAppId, [
        {
          Kind: MonetizationChangeKind.PlaySubscription,
          ProductId: productId.trim(),
          Localizations: [
            {
              Locale: locale.trim(),
              Name: title.trim(),
              Benefits: benefits
                .split('\n')
                .map((line) => line.trim())
                .filter(Boolean),
            },
          ],
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
      title={t.play.addSubscription}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button
            variant="primary"
            disabled={!productId.trim() || !title.trim() || !locale.trim()}
            loading={mutation.isPending}
            onClick={() => mutation.mutate()}
          >
            {t.plan.add}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        {errorBox}
        <Field label={t.common.productId} hint={t.play.productIdHint}>
          <Input mono value={productId} onChange={(event) => setProductId(event.target.value)} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label={t.common.locale}>
            <Input mono value={locale} onChange={(event) => setLocale(event.target.value)} />
          </Field>
          <div className="sm:col-span-2">
            <Field label={t.play.title} counter={{ value: [...title].length, max: 55 }}>
              <Input value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
          </div>
        </div>
        <Field label={t.play.benefits} hint={t.play.benefitsHint} optional>
          <Textarea rows={3} value={benefits} onChange={(event) => setBenefits(event.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}
