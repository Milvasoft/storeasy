import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { FileText } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, EmptyState, Field, Input, PageHeader, Textarea } from '@/components/ui/primitives'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { charCount } from '@/features/common/text'
import { msApi } from './api'
import type { MsDictionary } from './i18n/en'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import { MsWorkspace, useMsPlan, type MsContext } from './MsWorkspace'
import {
  MS_LIST_LIMIT,
  MS_TEXT_LIMIT,
  linesOf,
  listingChange,
  listingDraftOf,
  listingProblems,
  type MsListField,
  type MsListingDraft,
  type MsTextField,
} from './rules'
import { MsChangeKind, MsProductKind, type MsListingDto } from './types'

// Design 7.4: listing texts per language with the Microsoft Store limits counted live (description 10,000, short
// description 1,000, what's new 1,500, 20 features, 7 search terms). Planning sends only the changed fields of the changed
// languages; the plan goes into a draft submission once approved.

const TEXT_FIELDS: MsTextField[] = ['title', 'shortDescription', 'description', 'whatsNew']
const LIST_FIELDS: MsListField[] = ['features', 'searchTerms']

function textLabel(field: MsTextField | MsListField, t: MsDictionary): string {
  switch (field) {
    case 'title':
      return t.listings.titleField
    case 'shortDescription':
      return t.listings.shortDescription
    case 'description':
      return t.listings.description
    case 'whatsNew':
      return t.listings.whatsNew
    case 'features':
      return t.listings.features
    default:
      return t.listings.searchTerms
  }
}

function Listings({ context, listings }: { context: MsContext; listings: MsListingDto[] }) {
  const t = useMs()
  const { product, editable } = context
  const [locale, setLocale] = useState(listings[0]?.Locale ?? '')
  const [drafts, setDrafts] = useState<Record<string, MsListingDraft>>({})
  const plan = useMsPlan(context, () => setDrafts({}))
  const titleEditable = product.Kind === MsProductKind.Msix

  const originals = Object.fromEntries(listings.map((listing) => [listing.Locale, listingDraftOf(listing)]))
  const current = drafts[locale] ?? originals[locale] ?? listingDraftOf(undefined)
  const changes = listings
    .map((listing) =>
      drafts[listing.Locale] ? listingChange(listing.Locale, drafts[listing.Locale], originals[listing.Locale], titleEditable) : null,
    )
    .filter((change): change is MsListingDto => change !== null)
  const problems = Object.values(drafts).flatMap((draft) => listingProblems(draft))
  const localProblems = listingProblems(current)

  const update = (field: keyof MsListingDraft, value: string) =>
    setDrafts((all) => ({ ...all, [locale]: { ...(all[locale] ?? originals[locale]), [field]: value } }))

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (changes.length > 0 && problems.length === 0) {
      plan.make({ Kind: MsChangeKind.Listings, Listings: changes })
    }
  }

  return (
    <form noValidate onSubmit={submit}>
      <PageHeader title={t.listings.title} subtitle={t.listings.lead} />
      {plan.element}
      <div className="grid gap-4 md:grid-cols-[200px_1fr]">
        <nav className="grid content-start gap-1" aria-label={t.listings.languages}>
          {listings.map((listing) => {
            const changed =
              !!drafts[listing.Locale] && listingChange(listing.Locale, drafts[listing.Locale], originals[listing.Locale], titleEditable)
            return (
              <button
                key={listing.Locale}
                type="button"
                className={cn('chip justify-between', listing.Locale === locale && 'on')}
                aria-pressed={listing.Locale === locale}
                onClick={() => setLocale(listing.Locale)}
              >
                <span className="mono">{listing.Locale}</span>
                {changed ? (
                  <Badge small tone="accent">
                    {t.listings.changed}
                  </Badge>
                ) : null}
              </button>
            )
          })}
        </nav>
        <section className="card" aria-label={locale}>
          <div className="card-b grid gap-4">
            {TEXT_FIELDS.map((field) => {
              const value = current[field]
              const label = textLabel(field, t)
              const over = localProblems.includes(field)
              const readOnly = field === 'title' && !titleEditable
              const long = field === 'description' || field === 'whatsNew' || field === 'shortDescription'
              return (
                <Field
                  key={field}
                  label={label}
                  counter={{ value: charCount(value), max: MS_TEXT_LIMIT[field] }}
                  error={over ? interpolate(t.listings.overLimit, { field: label }) : null}
                  hint={readOnly ? t.listings.titleReadOnly : undefined}
                >
                  {long ? (
                    <Textarea
                      value={value}
                      rows={field === 'description' ? 8 : 3}
                      disabled={!editable}
                      onChange={(event) => update(field, event.target.value)}
                    />
                  ) : (
                    <Input value={value} disabled={!editable || readOnly} onChange={(event) => update(field, event.target.value)} />
                  )}
                </Field>
              )
            })}
            {LIST_FIELDS.map((field) => {
              const value = current[field]
              const label = textLabel(field, t)
              const limit = MS_LIST_LIMIT[field]
              const over = localProblems.includes(field)
              const hint = interpolate(field === 'features' ? t.listings.featuresHint : t.listings.searchTermsHint, {
                count: limit.count,
                length: limit.length,
              })
              return (
                <Field
                  key={field}
                  label={label}
                  counter={{ value: linesOf(value).length, max: limit.count }}
                  error={over ? `${interpolate(t.listings.overLimit, { field: label })} ${hint}` : null}
                  hint={hint}
                >
                  <Textarea
                    value={value}
                    rows={field === 'features' ? 6 : 4}
                    disabled={!editable}
                    onChange={(event) => update(field, event.target.value)}
                  />
                </Field>
              )
            })}
          </div>
        </section>
      </div>
      {editable && changes.length > 0 ? (
        <div className="card sticky bottom-3 mt-4">
          <div className="card-b flex flex-wrap items-center gap-3">
            <span className="min-w-0 flex-1 text-[13px]">
              <b>{interpolate(t.listings.draft, { count: changes.length })}</b>
              {problems.length > 0 ? <span className="err-t block">{t.listings.blocked}</span> : null}
            </span>
            <Button variant="ghost" onClick={() => setDrafts({})}>
              {t.listings.discardDraft}
            </Button>
            <Button type="submit" variant="primary" disabled={problems.length > 0} loading={plan.pending}>
              {t.planMake}
            </Button>
          </div>
        </div>
      ) : null}
    </form>
  )
}

function ListingsScreen({ context }: { context: MsContext }) {
  const t = useMs()
  const { organizationId, record } = context
  const content = useQuery({
    queryKey: msKeys.content(organizationId, record.StoreAppId, null, null),
    queryFn: () => msApi.content(organizationId, record.StoreAppId),
  })
  if (content.isLoading) {
    return <CardSkeleton />
  }
  if (content.isError || !content.data) {
    return <ScreenState error={content.error} onRetry={() => void content.refetch()} />
  }
  const listings = content.data.Listings ?? []
  if (listings.length === 0) {
    return <EmptyState icon={<FileText aria-hidden />} title={t.listings.noListings} />
  }
  return <Listings context={context} listings={listings} />
}

export function MsListingsPage() {
  return <MsWorkspace sub="listings">{(context) => <ListingsScreen context={context} />}</MsWorkspace>
}
