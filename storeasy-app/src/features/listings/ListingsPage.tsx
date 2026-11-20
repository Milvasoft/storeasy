import { useMemo, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Sparkles } from 'lucide-react'
import { useAiSettings } from '@/features/ai/api'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Field, Input, PageHeader, Textarea } from '@/components/ui/primitives'
import { StoreKind } from '@/lib/api/types'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { listingsApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import type { P2Dictionary } from '@/features/common/i18n/en'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { fieldLabel } from '@/features/common/labels'
import { PlanDialog } from '@/features/common/PlanView'
import { byteCount, charCount, overLimit } from '@/features/common/text'
import {
  ChangePlanStatus,
  type ChangePlanDto,
  type ListingChangeDto,
  type ListingFieldDto,
  type ListingSuggestionDto,
} from '@/features/common/types'

// Design 7.1-7.3: listing texts per language with the catalog's limits counted live (characters as the stores count
// them, Apple keywords in bytes), a draft kept across languages, and "Plan" making a change plan from the draft.

const LONG_FIELDS = ['description', 'fullDescription', 'whatsNew', 'promotionalText', 'keywords', 'eula']
// Fields of the version that stay editable when the version is locked (Apple: promotional text changes without review).
const ALWAYS_EDITABLE = ['promotionalText']

export type Draft = Record<string, string>

// The listing texts that get AI suggestions (the API's ListingSuggestions.Fields): links, contacts and the EULA are facts.
export const SUGGESTABLE_FIELDS: Partial<Record<StoreKind, readonly string[]>> = {
  [StoreKind.Apple]: ['name', 'subtitle', 'description', 'keywords', 'promotionalText', 'whatsNew'],
  [StoreKind.Google]: ['title', 'shortDescription', 'fullDescription'],
}

export function draftKey(locale: string | null | undefined, field: string): string {
  return `${locale ?? ''}|${field}`
}

/** The draft as plan changes: only values that differ from the store's. */
export function draftChanges(draft: Draft, fields: ListingFieldDto[]): ListingChangeDto[] {
  const changes: ListingChangeDto[] = []
  for (const [key, value] of Object.entries(draft)) {
    const [locale, field] = key.split('|')
    const original = fields.find((item) => item.Key === field && (item.Locale ?? '') === locale)
    if (original && (original.Value ?? '') === value) {
      continue
    }
    changes.push({ Locale: locale || null, Field: field, Value: value === '' ? null : value })
  }
  return changes
}

/** "12 / 30 characters · 40 / 100 bytes" for the limits the catalog gives. */
export function counterText(value: string, field: Pick<ListingFieldDto, 'MaxLength' | 'MaxBytes'>, t: P2Dictionary): string | null {
  const parts: string[] = []
  if (field.MaxLength) {
    parts.push(interpolate(t.listing.chars, { count: charCount(value), max: field.MaxLength }))
  }
  if (field.MaxBytes) {
    parts.push(interpolate(t.listing.bytes, { count: byteCount(value), max: field.MaxBytes }))
  }
  return parts.length > 0 ? parts.join(' · ') : null
}

export function ListingField({
  field,
  value,
  changed,
  disabled,
  onChange,
  onRevert,
}: {
  field: ListingFieldDto
  value: string
  changed: boolean
  disabled: boolean
  onChange: (value: string) => void
  onRevert: () => void
}) {
  const t = useP2()
  const label = fieldLabel(field.Key, t)
  const counter = counterText(value, field, t)
  const over = overLimit(value, field.MaxLength, field.MaxBytes)
  const long = LONG_FIELDS.includes(field.Key)
  return (
    <div className="grid gap-1">
      {changed || field.Required ? (
        <div className="flex items-center gap-2 text-xs">
          {field.Required ? <span className="text-fg3">{t.listing.required}</span> : null}
          {changed ? (
            <>
              <Badge small tone="accent">
                {t.listing.changed}
              </Badge>
              <button type="button" className="lnk" onClick={onRevert} aria-label={`${t.listing.revert}: ${label}`}>
                {t.listing.revert}
              </button>
            </>
          ) : null}
        </div>
      ) : null}
      <Field
        label={label}
        error={over ? `${interpolate(t.listing.overLimit, { field: label })} ${counter ?? ''}` : null}
        hint={
          <>
            {counter}
            {field.Key === 'keywords' && field.MaxBytes ? <span className="block">{t.listing.keywordsHint}</span> : null}
          </>
        }
      >
        {long ? (
          <Textarea
            value={value}
            rows={field.Key === 'keywords' ? 2 : 5}
            disabled={disabled}
            onChange={(event) => onChange(event.target.value)}
          />
        ) : (
          <Input value={value} disabled={disabled} onChange={(event) => onChange(event.target.value)} />
        )}
      </Field>
    </div>
  )
}

/**
 * "Suggest" under a listing text: asks the organization's AI for a value, shows it with its reason to edit, and puts it
 * in the draft. Nothing is saved: the draft becomes a change plan as any other edit.
 */
export function ListingSuggestion({
  storeAppId,
  versionId,
  locale,
  field,
  onApply,
}: {
  storeAppId: number
  versionId: string | null
  locale: string
  field: ListingFieldDto
  onApply: (value: string) => void
}) {
  const t = useP2()
  const organization = useCurrentOrganization()
  const [suggestion, setSuggestion] = useState<ListingSuggestionDto | null>(null)
  const [value, setValue] = useState('')
  const [note, setNote] = useState('')
  const label = fieldLabel(field.Key, t)
  const suggest = useMutation({
    mutationFn: () => listingsApi.suggest(organization.Id, storeAppId, versionId, locale, field.Key, note),
    onSuccess: (made) => {
      setSuggestion(made)
      setValue(made.Value)
    },
  })
  const limits = { MaxLength: suggestion?.MaxLength ?? field.MaxLength, MaxBytes: suggestion?.MaxBytes ?? field.MaxBytes }
  const over = overLimit(value, limits.MaxLength, limits.MaxBytes)

  if (!suggestion) {
    return (
      <div>
        <Button
          size="sm"
          variant="ghost"
          loading={suggest.isPending}
          aria-label={interpolate(t.listing.suggestLabel, { field: label })}
          onClick={() => suggest.mutate()}
        >
          <Sparkles aria-hidden />
          {suggest.isPending ? t.listing.suggesting : t.listing.suggest}
        </Button>
      </div>
    )
  }
  return (
    <section className="card" aria-label={interpolate(t.listing.suggestionTitle, { field: label })}>
      <div className="card-b grid gap-3">
        <div className="flex items-center gap-2">
          <Sparkles aria-hidden className="h-4 w-4" />
          <b className="text-[13.5px]">{interpolate(t.listing.suggestionTitle, { field: label })}</b>
        </div>
        {suggestion.Reason ? <p className="m-0 text-[13px]">{suggestion.Reason}</p> : null}
        <Field
          label={label}
          error={over ? interpolate(t.listing.overLimit, { field: label }) : null}
          hint={
            <>
              {counterText(value, limits, t)}
              {suggestion.Shortened ? <span className="block">{t.listing.suggestionShortened}</span> : null}
            </>
          }
        >
          <Textarea value={value} rows={LONG_FIELDS.includes(field.Key) ? 5 : 2} onChange={(event) => setValue(event.target.value)} />
        </Field>
        <Field label={t.listing.suggestionNote}>
          <Input
            value={note}
            maxLength={500}
            placeholder={t.listing.suggestionNotePlaceholder}
            onChange={(event) => setNote(event.target.value)}
          />
        </Field>
        <p className="m-0 text-xs text-fg3">
          {t.listing.suggestionHint} {interpolate(t.listing.suggestionModel, { model: suggestion.Model })}
        </p>
        <div className="flex flex-wrap gap-2">
          <Button
            size="sm"
            variant="primary"
            disabled={over || value.trim().length === 0}
            onClick={() => {
              onApply(value.trim())
              setSuggestion(null)
            }}
          >
            {t.listing.useSuggestion}
          </Button>
          <Button size="sm" loading={suggest.isPending} onClick={() => suggest.mutate()}>
            {t.listing.suggestAgain}
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setSuggestion(null)}>
            {t.listing.closeSuggestion}
          </Button>
        </div>
      </div>
    </section>
  )
}

function Texts({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const { record, organizationId, editor } = context
  const [search, setSearch] = useSearchParams()
  const requested = search.get('locale')
  const [draft, setDraft] = useState<Draft>({})
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const aiSettings = useAiSettings(organizationId)
  // Suggestions need the organization's AI key; without it the button is not shown.
  const canSuggest = editor && aiSettings.data?.HasApiKey === true
  const suggestable = SUGGESTABLE_FIELDS[context.store] ?? []

  const listing = useQuery({
    queryKey: contentKeys.listing(organizationId, record.StoreAppId, requested),
    queryFn: () => listingsApi.get(organizationId, record.StoreAppId, requested),
  })
  const data = listing.data
  const locales = data?.Locales ?? []
  const locale = requested ?? locales.find((item) => item.IsPrimary)?.Locale ?? locales[0]?.Locale ?? null
  const allFields = useMemo(() => (data?.Fields ?? []).filter((field) => field.Group !== 'Details'), [data])
  const fields = allFields.filter((field) => !field.Locale || field.Locale === locale)
  const groups = [...new Set(fields.map((field) => field.Group ?? '-'))]
  const changes = draftChanges(draft, allFields)
  const blocked = Object.entries(draft).some(([key, value]) => {
    const [fieldLocale, name] = key.split('|')
    const field = allFields.find((item) => item.Key === name && (item.Locale ?? '') === fieldLocale)
    return field ? overLimit(value, field.MaxLength, field.MaxBytes) : false
  })

  const makePlan = useMutation({
    mutationFn: () => listingsApi.plan(organizationId, record.StoreAppId, data?.VersionId ?? null, changes),
    onSuccess: (made) => setPlan(made),
  })

  if (listing.isLoading) {
    return <CardSkeleton />
  }
  if (listing.isError || !data) {
    return <ScreenState error={listing.error} onRetry={() => void listing.refetch()} />
  }

  const pick = (next: string) => {
    const params = new URLSearchParams(search)
    params.set('locale', next)
    setSearch(params, { replace: true })
  }
  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (changes.length > 0 && !blocked) {
      makePlan.mutate()
    }
  }

  return (
    <form onSubmit={submit} noValidate>
      <PageHeader
        title={t.listing.title}
        subtitle={data.VersionString ? interpolate(t.listing.version, { version: data.VersionString }) : undefined}
        badge={data.VersionState ? <Badge small>{data.VersionState}</Badge> : undefined}
      />
      {!data.VersionEditable && context.store === StoreKind.Apple ? (
        <Callout tone="warning" className="mb-3">
          {interpolate(t.listing.locked, { version: data.VersionString ?? '-', state: data.VersionState ?? '-' })}
        </Callout>
      ) : null}
      <div className="grid gap-4 md:grid-cols-[200px_1fr]">
        <nav aria-label={t.listing.locales} className="grid content-start gap-1">
          {locales.length === 0 ? <p className="text-[13px] text-fg3">{t.listing.noLocales}</p> : null}
          {locales.map((item) => (
            <button
              key={item.Locale}
              type="button"
              className={cn('chip justify-between', item.Locale === locale && 'on')}
              aria-pressed={item.Locale === locale}
              onClick={() => pick(item.Locale)}
            >
              <span className="mono">{item.Locale}</span>
              <span className="text-xs text-fg3">
                {item.IsPrimary ? `${t.listing.primary} · ` : ''}
                {interpolate(t.listing.filled, { filled: item.FilledCount, required: item.RequiredCount })}
              </span>
            </button>
          ))}
        </nav>
        <div className="grid content-start gap-4">
          {groups.map((group) => (
            <section key={group} className="card">
              <div className="card-h">
                <h2 className="h3">{(t.listing.group as Record<string, string>)[group] ?? group}</h2>
              </div>
              <div className="card-b grid gap-4">
                {fields
                  .filter((field) => (field.Group ?? '-') === group)
                  .map((field) => {
                    const key = draftKey(field.Locale, field.Key)
                    const value = draft[key] ?? field.Value ?? ''
                    const locked = !data.VersionEditable && field.Group === 'Version' && !ALWAYS_EDITABLE.includes(field.Key)
                    return (
                      <div key={key} className="grid gap-2">
                        <ListingField
                          field={field}
                          value={value}
                          changed={key in draft && draft[key] !== (field.Value ?? '')}
                          disabled={!editor || locked}
                          onChange={(next) => setDraft((current) => ({ ...current, [key]: next }))}
                          onRevert={() =>
                            setDraft((current) => {
                              const rest = { ...current }
                              delete rest[key]
                              return rest
                            })
                          }
                        />
                        {canSuggest && !locked && field.Locale && suggestable.includes(field.Key) ? (
                          <ListingSuggestion
                            storeAppId={record.StoreAppId}
                            versionId={data.VersionId ?? null}
                            locale={field.Locale}
                            field={field}
                            onApply={(next) => setDraft((current) => ({ ...current, [key]: next }))}
                          />
                        ) : null}
                      </div>
                    )
                  })}
              </div>
            </section>
          ))}
        </div>
      </div>
      {editor && changes.length > 0 ? (
        <div className="card sticky bottom-3 mt-4">
          <div className="card-b flex flex-wrap items-center gap-3">
            <span className="min-w-0 flex-1 text-[13px]">
              <b>{interpolate(t.listing.draft, { count: changes.length })}</b>
              {blocked ? <span className="err-t block">{t.listing.blocked}</span> : null}
            </span>
            <Button variant="ghost" onClick={() => setDraft({})}>
              {t.listing.discardDraft}
            </Button>
            <Button type="submit" variant="primary" disabled={blocked} loading={makePlan.isPending}>
              {t.plan.make}
            </Button>
          </div>
        </div>
      ) : null}
      <PlanDialog
        plan={plan}
        onClose={() => setPlan(null)}
        onDecided={(decided) => {
          if (decided?.Status === ChangePlanStatus.Approved || decided?.Status === ChangePlanStatus.Applied) {
            setDraft({})
          }
        }}
      />
    </form>
  )
}

export function ListingsPage() {
  return (
    <AppWorkspace section="listing" sub="texts">
      {(context) => <Texts context={context} />}
    </AppWorkspace>
  )
}

// ---- Play app details (design 7.23) ------------------------------------------------------------------------------------
const DETAIL_FIELDS = ['contactEmail', 'contactPhone', 'contactWebsite', 'defaultLanguage'] as const

function Details({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const { record, organizationId, editor } = context
  const [draft, setDraft] = useState<Record<string, string>>({})
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const details = useQuery({
    queryKey: contentKeys.details(organizationId, record.StoreAppId),
    queryFn: () => listingsApi.details(organizationId, record.StoreAppId),
  })
  const current: Record<string, string> = {
    contactEmail: details.data?.ContactEmail ?? '',
    contactPhone: details.data?.ContactPhone ?? '',
    contactWebsite: details.data?.ContactWebsite ?? '',
    defaultLanguage: details.data?.DefaultLanguage ?? '',
  }
  const changes: ListingChangeDto[] = DETAIL_FIELDS.filter((field) => field in draft && draft[field] !== current[field]).map((field) => ({
    Locale: null,
    Field: field,
    Value: draft[field].trim() || null,
  }))
  const makePlan = useMutation({
    mutationFn: () => listingsApi.plan(organizationId, record.StoreAppId, null, changes),
    onSuccess: (made) => setPlan(made),
  })

  if (details.isLoading) {
    return <CardSkeleton />
  }
  if (details.isError) {
    return <ScreenState error={details.error} onRetry={() => void details.refetch()} />
  }
  return (
    <form
      className="grid max-w-xl gap-4"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        if (changes.length > 0) {
          makePlan.mutate()
        }
      }}
    >
      <PageHeader title={t.listing.detailsTitle} subtitle={t.listing.detailsHint} />
      {DETAIL_FIELDS.map((field) => (
        <Field key={field} label={fieldLabel(field, t)} hint={field === 'defaultLanguage' ? t.listing.defaultLanguageHint : undefined}>
          <Input
            value={draft[field] ?? current[field]}
            disabled={!editor}
            type={field === 'contactEmail' ? 'email' : field === 'contactPhone' ? 'tel' : field === 'contactWebsite' ? 'url' : 'text'}
            onChange={(event) => setDraft((value) => ({ ...value, [field]: event.target.value }))}
          />
        </Field>
      ))}
      {editor ? (
        <div>
          <Button type="submit" variant="primary" disabled={changes.length === 0} loading={makePlan.isPending}>
            {t.plan.make}
          </Button>
        </div>
      ) : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} onDecided={() => setDraft({})} />
    </form>
  )
}

export function ListingDetailsPage() {
  return (
    <AppWorkspace section="listing" sub="details">
      {(context) => <Details context={context} />}
    </AppWorkspace>
  )
}
