import { useMemo, useState, type FormEvent, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { foldSearch, Select } from '@/components/ui/Select'
import {
  Button,
  CardSkeleton,
  Checkbox,
  Field,
  Input,
  PageHeader,
  PasswordInput,
  RadioGroup,
  Switch,
  Textarea,
} from '@/components/ui/primitives'
import { StoreKind } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { metadataApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { PlanDialog } from '@/features/common/PlanView'
import type { AgeRatingAnswerDto, ChangePlanDto, ReviewInfoDto, TerritoryDto } from '@/features/common/types'

// Design 7.19-7.21 and 8.23: categories and rights, age rating (new descriptors without an API are assisted steps),
// price and territories with Apple's price points, and the App Review information. Every save is a change plan.

function usePlanState() {
  return useState<ChangePlanDto | null>(null)
}

function SaveBar({ editor, disabled, loading, children }: { editor: boolean; disabled?: boolean; loading: boolean; children?: ReactNode }) {
  const t = useP2()
  if (!editor) {
    return null
  }
  return (
    <div className="flex flex-wrap items-center gap-3">
      <Button type="submit" variant="primary" disabled={disabled} loading={loading}>
        {t.plan.make}
      </Button>
      {children}
    </div>
  )
}

// ---- Categories -----------------------------------------------------------------------------------------------------------
const RIGHTS = ['DOES_NOT_USE_THIRD_PARTY_CONTENT', 'USES_THIRD_PARTY_CONTENT'] as const

function Categories({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const { record, organizationId, editor } = context
  const [plan, setPlan] = usePlanState()
  const [draft, setDraft] = useState<{ primary?: string; secondary?: string; copyright?: string; rights?: string }>({})
  const categories = useQuery({
    queryKey: contentKeys.categories(organizationId, record.StoreAppId),
    queryFn: () => metadataApi.categories(organizationId, record.StoreAppId),
  })
  const makePlan = useMutation({
    mutationFn: () =>
      metadataApi.planCategories(organizationId, record.StoreAppId, {
        Primary: draft.primary ?? null,
        Secondary: draft.secondary === undefined ? null : draft.secondary,
        Copyright: draft.copyright === undefined ? null : draft.copyright.trim(),
        ContentRightsDeclaration: draft.rights ?? null,
      }),
    onSuccess: setPlan,
  })
  if (categories.isLoading) {
    return <CardSkeleton />
  }
  if (categories.isError || !categories.data) {
    return <ScreenState error={categories.error} onRetry={() => void categories.refetch()} />
  }
  const data = categories.data
  const primary = draft.primary ?? data.Primary ?? null
  const secondary = draft.secondary ?? data.Secondary ?? ''
  const options = (data.Options ?? []).map((option) => ({ value: option.Key, label: option.Title ?? option.Key }))
  const sameError = secondary && primary === secondary ? t.category.sameAsPrimary : null
  const changed = Object.keys(draft).length > 0
  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!sameError && changed) {
      makePlan.mutate()
    }
  }
  return (
    <form className="grid max-w-xl gap-4" noValidate onSubmit={submit}>
      <PageHeader title={t.category.title} />
      <Field label={t.category.primary}>
        <Select
          value={primary}
          options={options}
          searchable
          disabled={!editor}
          onChange={(value) => setDraft((d) => ({ ...d, primary: value }))}
        />
      </Field>
      <Field label={t.category.secondary} error={sameError} optional>
        <Select
          value={secondary}
          options={[{ value: '', label: t.category.secondaryNone }, ...options]}
          searchable
          disabled={!editor}
          onChange={(value) => setDraft((d) => ({ ...d, secondary: value }))}
        />
      </Field>
      <Field label={t.category.copyright} hint={t.category.copyrightHint}>
        <Input
          value={draft.copyright ?? data.Copyright ?? ''}
          disabled={!editor}
          onChange={(event) => setDraft((d) => ({ ...d, copyright: event.target.value }))}
        />
      </Field>
      <RadioGroup
        name="content-rights"
        label={t.category.rights}
        value={draft.rights ?? data.ContentRightsDeclaration ?? ''}
        onChange={(value) => setDraft((d) => ({ ...d, rights: value }))}
        options={RIGHTS.map((value) => ({
          value,
          label: value === RIGHTS[0] ? t.category.rightsNone : t.category.rightsUses,
          disabled: !editor,
        }))}
      />
      <SaveBar editor={editor} disabled={!changed || !!sameError} loading={makePlan.isPending} />
      <PlanDialog plan={plan} onClose={() => setPlan(null)} onDecided={() => setDraft({})} />
    </form>
  )
}

export function CategoriesPage() {
  return (
    <AppWorkspace section="listing" sub="category">
      {(context) => <Categories context={context} />}
    </AppWorkspace>
  )
}

// ---- Age rating -----------------------------------------------------------------------------------------------------------
const LEVELS = ['NONE', 'INFREQUENT_OR_MILD', 'FREQUENT_OR_INTENSE'] as const

/** The JSON value of an answer: "true"/"false" go as booleans, the rest as strings (PlanAgeRatingCommand.Answers). */
export function answerValue(value: string): string | boolean {
  return value === 'true' ? true : value === 'false' ? false : value
}

function AgeRating({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const format = useFormat()
  const { record, organizationId, editor, app, storeQuery } = context
  const [plan, setPlan] = usePlanState()
  const [draft, setDraft] = useState<Record<string, string>>({})
  const rating = useQuery({
    queryKey: contentKeys.ageRating(organizationId, record.StoreAppId),
    queryFn: () => metadataApi.ageRating(organizationId, record.StoreAppId),
  })
  const changes = useMemo(() => {
    const answers = rating.data?.Answers ?? []
    return Object.fromEntries(
      Object.entries(draft)
        .filter(([key, value]) => answers.find((answer) => answer.Key === key)?.Value !== value)
        .map(([key, value]) => [key, answerValue(value)]),
    )
  }, [draft, rating.data])
  const makePlan = useMutation({
    mutationFn: () => metadataApi.planAgeRating(organizationId, record.StoreAppId, changes),
    onSuccess: setPlan,
  })
  if (rating.isLoading) {
    return <CardSkeleton />
  }
  if (rating.isError || !rating.data) {
    return <ScreenState error={rating.error} onRetry={() => void rating.refetch()} />
  }
  const levelLabel = (value: string) =>
    value === 'NONE' ? t.ageRating.none : value === 'INFREQUENT_OR_MILD' ? t.ageRating.mild : t.ageRating.intense
  const row = (answer: AgeRatingAnswerDto) => {
    const value = draft[answer.Key] ?? answer.Value ?? ''
    const boolean = value === 'true' || value === 'false'
    const set = (next: string) => setDraft((current) => ({ ...current, [answer.Key]: next }))
    return (
      <li key={answer.Key} className="ck-i items-center">
        <div className="min-w-0 flex-1">
          <span className="mono text-[13px]">{answer.Key}</span>
          <span className="ml-2 inline-flex flex-wrap gap-1">
            {answer.IsNew ? (
              <Badge small tone="accent">
                {t.ageRating.isNew}
              </Badge>
            ) : null}
            {answer.DueDate ? (
              <Badge small tone="warning">
                {interpolate(t.ageRating.due, { date: format.date(answer.DueDate) })}
              </Badge>
            ) : null}
          </span>
        </div>
        {answer.Assisted ? (
          <Link className="lnk text-[13px]" to={`/apps/${app.Id}/assisted${storeQuery}`}>
            {t.ageRating.assisted}
          </Link>
        ) : boolean ? (
          <span className="flex items-center gap-2 text-[13px]">
            <Switch
              checked={value === 'true'}
              label={answer.Key}
              disabled={!editor}
              onChange={(checked) => set(checked ? 'true' : 'false')}
            />
            {value === 'true' ? t.ageRating.yes : t.ageRating.no}
          </span>
        ) : (
          <div className="w-56">
            <Select
              aria-label={answer.Key}
              value={value || null}
              disabled={!editor}
              options={[...new Set([...LEVELS, ...(value ? [value] : [])])].map((level) => ({ value: level, label: levelLabel(level) }))}
              onChange={set}
            />
          </div>
        )}
      </li>
    )
  }
  const answers = rating.data.Answers ?? []
  const assisted = answers.filter((answer) => answer.Assisted)
  return (
    <form
      className="grid max-w-3xl gap-4"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        if (Object.keys(changes).length > 0) {
          makePlan.mutate()
        }
      }}
    >
      <PageHeader title={t.ageRating.title} subtitle={t.ageRating.subtitle} />
      {assisted.length > 0 ? <Callout tone="info">{t.ageRating.assistedNote}</Callout> : null}
      <ul className="card m-0 grid list-none px-4 py-1">{answers.map(row)}</ul>
      <SaveBar editor={editor} disabled={Object.keys(changes).length === 0} loading={makePlan.isPending} />
      <PlanDialog plan={plan} onClose={() => setPlan(null)} onDecided={() => setDraft({})} />
    </form>
  )
}

export function AgeRatingPage() {
  return (
    <AppWorkspace section="listing" sub="ageRating">
      {(context) => <AgeRating context={context} />}
    </AppWorkspace>
  )
}

// ---- Pricing --------------------------------------------------------------------------------------------------------------
function Pricing({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const format = useFormat()
  const { record, organizationId, editor } = context
  const [plan, setPlan] = usePlanState()
  const [base, setBase] = useState<string | null>(null)
  const [pricePoint, setPricePoint] = useState<string | null>(null)
  const [territories, setTerritories] = useState<TerritoryDto[] | null>(null)
  const [query, setQuery] = useState('')
  const pricing = useQuery({
    queryKey: contentKeys.pricing(organizationId, record.StoreAppId),
    queryFn: () => metadataApi.pricing(organizationId, record.StoreAppId),
  })
  const baseTerritory = base ?? pricing.data?.BaseTerritory ?? null
  const points = useQuery({
    queryKey: contentKeys.pricePoints(organizationId, record.StoreAppId, baseTerritory ?? ''),
    queryFn: () => metadataApi.pricePoints(organizationId, record.StoreAppId, baseTerritory ?? ''),
    enabled: !!baseTerritory,
  })
  const list = territories ?? pricing.data?.Territories ?? []
  const changed = base !== null || pricePoint !== null || territories !== null
  const makePlan = useMutation({
    mutationFn: () =>
      metadataApi.planPricing(organizationId, record.StoreAppId, {
        BaseTerritory: baseTerritory,
        PricePointId: pricePoint ?? pricing.data?.PricePointId ?? null,
        Territories: list,
      }),
    onSuccess: setPlan,
  })
  if (pricing.isLoading) {
    return <CardSkeleton />
  }
  if (pricing.isError || !pricing.data) {
    return <ScreenState error={pricing.error} onRetry={() => void pricing.refetch()} />
  }
  const data = pricing.data
  const shown = query ? list.filter((item) => foldSearch(item.Code).includes(foldSearch(query))) : list
  const available = list.filter((item) => item.Available).length
  const setAll = (value: boolean) => setTerritories(list.map((item) => ({ ...item, Available: value })))
  return (
    <form
      className="grid max-w-3xl gap-4"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        if (changed) {
          makePlan.mutate()
        }
      }}
    >
      <PageHeader title={t.pricing.title} />
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label={t.pricing.base}>
          <Select
            value={baseTerritory}
            searchable
            disabled={!editor}
            options={list.map((item) => ({ value: item.Code, label: item.Code }))}
            onChange={(value) => {
              setBase(value)
              setPricePoint(null)
            }}
          />
        </Field>
        <Field label={t.pricing.pricePoint} hint={t.pricing.pricePointHint}>
          <Select
            value={pricePoint ?? data.PricePointId ?? null}
            searchable
            disabled={!editor || !points.data}
            options={(points.data ?? []).map((point) => ({
              value: point.Key,
              label: point.Title ?? point.Key,
              description: point.Extra ?? undefined,
            }))}
            onChange={setPricePoint}
          />
        </Field>
      </div>
      {(data.Schedule ?? []).length > 0 ? (
        <section className="card">
          <div className="card-h">
            <h2 className="h3">{t.pricing.schedule}</h2>
          </div>
          <ul className="card-b m-0 grid list-none gap-1 text-[13px]">
            {(data.Schedule ?? []).map((entry, index) => (
              <li key={index}>
                <span className="mono">{entry.Territory}</span> · {entry.CustomerPrice ?? entry.PricePointId}{' '}
                {entry.StartDate ? (
                  <span className="text-fg3">{interpolate(t.pricing.from, { date: format.date(entry.StartDate) })}</span>
                ) : null}
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      <section className="card">
        <div className="card-h">
          <h2 className="h3">{t.pricing.territories}</h2>
          <span className="text-xs text-fg3">{interpolate(t.pricing.selected, { count: available, total: list.length })}</span>
          {editor ? (
            <span className="ml-auto flex gap-2">
              <Button size="sm" variant="ghost" onClick={() => setAll(true)}>
                {t.pricing.selectAll}
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setAll(false)}>
                {t.pricing.selectNone}
              </Button>
            </span>
          ) : null}
        </div>
        <div className="card-b grid gap-3">
          <Input
            aria-label={t.pricing.search}
            placeholder={t.pricing.search}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
          <div className="grid grid-cols-3 gap-2 sm:grid-cols-6">
            {shown.map((item) => (
              <Checkbox
                key={item.Code}
                checked={item.Available}
                disabled={!editor}
                onChange={(checked) =>
                  setTerritories(list.map((value) => (value.Code === item.Code ? { ...value, Available: checked } : value)))
                }
              >
                <span className="mono">{item.Code}</span>
              </Checkbox>
            ))}
          </div>
          {typeof data.AvailableInNewTerritories === 'boolean' ? (
            <p className="m-0 text-xs text-fg3">
              {t.pricing.newTerritories}: <b>{data.AvailableInNewTerritories ? t.ageRating.yes : t.ageRating.no}</b> ·{' '}
              {t.pricing.newTerritoriesNote}
            </p>
          ) : null}
        </div>
      </section>
      <SaveBar editor={editor} disabled={!changed} loading={makePlan.isPending} />
      <PlanDialog
        plan={plan}
        onClose={() => setPlan(null)}
        onDecided={() => {
          setBase(null)
          setPricePoint(null)
          setTerritories(null)
        }}
      />
    </form>
  )
}

export function PricingPage() {
  return (
    <AppWorkspace section="listing" sub="pricing">
      {(context) => <Pricing context={context} />}
    </AppWorkspace>
  )
}

// ---- Review information ---------------------------------------------------------------------------------------------------
type ReviewDraft = Partial<
  Record<'ContactFirstName' | 'ContactLastName' | 'ContactEmail' | 'ContactPhone' | 'DemoAccountName' | 'Notes', string>
> & {
  DemoAccountRequired?: boolean
}

function ReviewInfo({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const { record, organizationId, editor } = context
  const [plan, setPlan] = usePlanState()
  const [draft, setDraft] = useState<ReviewDraft>({})
  // Write only: the password is sent when typed and never read back or shown.
  const [password, setPassword] = useState('')
  const info = useQuery({
    queryKey: contentKeys.reviewInfo(organizationId, record.StoreAppId),
    queryFn: () => metadataApi.reviewInfo(organizationId, record.StoreAppId),
  })
  const makePlan = useMutation({
    mutationFn: (data: ReviewInfoDto) =>
      metadataApi.planReviewInfo(organizationId, record.StoreAppId, {
        VersionId: data.VersionId ?? null,
        ContactFirstName: draft.ContactFirstName?.trim() ?? null,
        ContactLastName: draft.ContactLastName?.trim() ?? null,
        ContactEmail: draft.ContactEmail?.trim() ?? null,
        ContactPhone: draft.ContactPhone?.trim() ?? null,
        DemoAccountRequired: draft.DemoAccountRequired ?? null,
        DemoAccountName: draft.DemoAccountName?.trim() ?? null,
        DemoPassword: password || null,
        Notes: draft.Notes ?? null,
      }),
    onSuccess: (made) => {
      setPassword('')
      setPlan(made)
    },
  })
  if (info.isLoading) {
    return <CardSkeleton />
  }
  if (info.isError || !info.data) {
    return <ScreenState error={info.error} onRetry={() => void info.refetch()} />
  }
  const data = info.data
  const value = (key: keyof Omit<ReviewDraft, 'DemoAccountRequired'>) => draft[key] ?? (data[key] as string | undefined) ?? ''
  const set = (key: keyof Omit<ReviewDraft, 'DemoAccountRequired'>) => (event: { target: { value: string } }) =>
    setDraft((current) => ({ ...current, [key]: event.target.value }))
  const required = draft.DemoAccountRequired ?? data.DemoAccountRequired
  const changed = Object.keys(draft).length > 0 || password.length > 0
  return (
    <form
      className="grid max-w-2xl gap-4"
      noValidate
      autoComplete="off"
      onSubmit={(event) => {
        event.preventDefault()
        if (changed) {
          makePlan.mutate(data)
        }
      }}
    >
      <PageHeader title={t.reviewInfo.title} subtitle={t.reviewInfo.subtitle} />
      {data.StoreDiffers ? <Callout tone="warning">{t.reviewInfo.storeDiffers}</Callout> : null}
      <section className="card">
        <div className="card-h">
          <h2 className="h3">{t.reviewInfo.contact}</h2>
        </div>
        <div className="card-b grid gap-4 sm:grid-cols-2">
          <Field label={t.reviewInfo.firstName}>
            <Input value={value('ContactFirstName')} disabled={!editor} onChange={set('ContactFirstName')} />
          </Field>
          <Field label={t.reviewInfo.lastName}>
            <Input value={value('ContactLastName')} disabled={!editor} onChange={set('ContactLastName')} />
          </Field>
          <Field label={t.reviewInfo.email}>
            <Input type="email" value={value('ContactEmail')} disabled={!editor} onChange={set('ContactEmail')} />
          </Field>
          <Field label={t.reviewInfo.phone}>
            <Input type="tel" value={value('ContactPhone')} disabled={!editor} onChange={set('ContactPhone')} />
          </Field>
        </div>
      </section>
      <section className="card">
        <div className="card-h">
          <h2 className="h3">{t.reviewInfo.demo}</h2>
          <span className="ml-auto flex items-center gap-2 text-[13px]">
            <Switch
              checked={required}
              label={t.reviewInfo.demoRequired}
              disabled={!editor}
              onChange={(checked) => setDraft((current) => ({ ...current, DemoAccountRequired: checked }))}
            />
            {t.reviewInfo.demoRequired}
          </span>
        </div>
        {required ? (
          <div className="card-b grid gap-4 sm:grid-cols-2">
            <Field label={t.reviewInfo.demoName}>
              <Input value={value('DemoAccountName')} disabled={!editor} autoComplete="off" onChange={set('DemoAccountName')} />
            </Field>
            <Field
              label={t.reviewInfo.demoPassword}
              hint={`${data.DemoPasswordSet || data.StorePasswordSet ? t.reviewInfo.demoPasswordSet : t.reviewInfo.demoPasswordNotSet} ${t.reviewInfo.demoPasswordNote}`}
            >
              <PasswordInput
                value={password}
                disabled={!editor}
                autoComplete="new-password"
                onChange={(event) => setPassword(event.target.value)}
              />
            </Field>
          </div>
        ) : null}
      </section>
      <Field label={t.reviewInfo.notes} counter={{ value: value('Notes').length, max: 4000 }} optional>
        <Textarea rows={5} value={value('Notes')} disabled={!editor} onChange={set('Notes')} />
      </Field>
      <SaveBar editor={editor} disabled={!changed} loading={makePlan.isPending} />
      <PlanDialog plan={plan} onClose={() => setPlan(null)} onDecided={() => setDraft({})} />
    </form>
  )
}

export function ReviewInfoPage() {
  return (
    <AppWorkspace section="release" sub="reviewInfo" stores={[StoreKind.Apple]}>
      {(context) => <ReviewInfo context={context} />}
    </AppWorkspace>
  )
}
