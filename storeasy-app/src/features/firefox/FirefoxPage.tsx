import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, Package, RefreshCw } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Timeline } from '@/components/ui/Progress'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, Input, Textarea } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { apiErrorMessage } from '@/lib/api/http'
import { StoreKind } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { firefoxApi } from './api'
import { useFirefox } from './i18n/useFirefox'
import { firefoxKeys } from './keys'
import { addonStatusKey, addonStatusTone, compareVersions, fileStatusKey, fileStatusTone, parseIds } from './rules'
import {
  FIREFOX_FIELDS,
  FirefoxAddonStatus,
  FirefoxChangeKind,
  FirefoxVersionChannel,
  type FirefoxAddonDto,
  type FirefoxChangeInput,
  type FirefoxField,
  type FirefoxListingValue,
} from './types'

// The Firefox Add-ons screen of an app (one page): the add-on's status, users and rating, its listing per locale, the .xpi
// packages intake received with the version form, the versions with their review status, previews and icon, and the
// review history. Each write is a change plan shown in the shared plan dialog; approving it calls the AMO API
// (ChangePlanKind.FirefoxChange).

/** Plans a Firefox change (always a dry run) and shows the saved plan in the shared plan dialog. */
function useFirefoxPlan(context: WorkspaceContext) {
  const queryClient = useQueryClient()
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const make = useMutation({
    mutationFn: (change: FirefoxChangeInput) => firefoxApi.change(context.organizationId, context.record.StoreAppId, change),
    meta: { silentError: true },
    onSuccess: (saved) => {
      setError(null)
      setPlan(saved)
    },
    onError: (failure) => setError(apiErrorMessage(failure, '')),
  })
  const element = (
    <>
      {error ? <FormErrors title={error} messages={[]} /> : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} onDecided={() => void queryClient.invalidateQueries({ queryKey: firefoxKeys.all })} />
    </>
  )
  return { make: (change: FirefoxChangeInput) => make.mutate(change), pending: make.isPending, element }
}

function AddonCard({ context, data }: { context: WorkspaceContext; data: FirefoxAddonDto }) {
  const t = useFirefox()
  const format = useFormat()
  const { watchJobs } = useActivity()
  const sync = useMutation({
    mutationFn: () => firefoxApi.sync(context.organizationId, data.StoreAppId),
    onSuccess: (jobId) => {
      watchJobs([jobId])
      showToast(t.addon.syncQueued, 'success', data.Guid ?? '')
    },
  })
  return (
    <article className="card" aria-label={t.addon.title}>
      <div className="card-h flex-wrap">
        {data.IconUrl ? <img src={data.IconUrl} alt="" className="h-8 w-8 rounded" /> : null}
        <h2 className="h3 min-w-0 flex-1 truncate">{data.AppName ?? data.Guid}</h2>
        {data.StoreUrl ? (
          <a className="lnk inline-flex items-center gap-1 text-[13px]" href={data.StoreUrl} target="_blank" rel="noopener noreferrer">
            <ExternalLink className="h-3.5 w-3.5" aria-hidden />
            {t.addon.openStore}
          </a>
        ) : null}
        {data.DeveloperHubUrl ? (
          <a className="lnk inline-flex items-center gap-1 text-[13px]" href={data.DeveloperHubUrl} target="_blank" rel="noopener noreferrer">
            <ExternalLink className="h-3.5 w-3.5" aria-hidden />
            {t.addon.openHub}
          </a>
        ) : null}
        <Button size="sm" loading={sync.isPending} onClick={() => sync.mutate()}>
          <RefreshCw aria-hidden />
          {t.addon.sync}
        </Button>
      </div>
      <div className="card-b grid gap-3">
        {data.Status === FirefoxAddonStatus.Disabled ? <Callout tone="danger">{t.addon.mozillaDisabled}</Callout> : null}
        {data.IsDisabled ? <Callout tone="warning">{t.addon.developerDisabled}</Callout> : null}
        {data.SyncError ? <Callout tone="danger">{interpolate(t.addon.syncError, { error: data.SyncError })}</Callout> : null}
        {data.ReadError ? <Callout tone="warning">{interpolate(t.addon.readError, { error: data.ReadError })}</Callout> : null}
        {!data.SyncedDate ? <Callout tone="info">{t.addon.notSynced}</Callout> : null}
        <dl className="kv text-[13px]">
          <dt>{t.addon.status}</dt>
          <dd>
            <Badge tone={addonStatusTone(data.Status)} dot>
              {t.status[addonStatusKey(data.Status)]}
            </Badge>
          </dd>
          <dt>{t.addon.listed}</dt>
          <dd className="mono">{data.ListedVersion ?? '-'}</dd>
          <dt>{t.addon.latest}</dt>
          <dd>
            {data.LatestVersion ? (
              <span className="inline-flex flex-wrap items-center gap-2">
                <span className="mono">{data.LatestVersion}</span>
                <Badge tone={fileStatusTone(data.LatestFileStatus)} dot>
                  {t.file[fileStatusKey(data.LatestFileStatus)]}
                </Badge>
                <span className="text-fg3">{data.LatestChannel === FirefoxVersionChannel.Unlisted ? t.channel.unlisted : t.channel.listed}</span>
              </span>
            ) : (
              '-'
            )}
          </dd>
          <dt>{t.addon.rating}</dt>
          <dd>{data.Rating != null ? interpolate(t.addon.ratingValue, { rating: data.Rating.toFixed(1), count: data.RatingCount ?? 0 }) : '-'}</dd>
          <dt>{t.addon.users}</dt>
          <dd>{data.AverageDailyUsers != null ? format.number(data.AverageDailyUsers) : '-'}</dd>
          <dt>{t.addon.downloads}</dt>
          <dd>{data.WeeklyDownloads != null ? format.number(data.WeeklyDownloads) : '-'}</dd>
          <dt>{t.addon.guid}</dt>
          <dd className="mono">{data.Guid ?? '-'}</dd>
          <dt>{t.addon.synced}</dt>
          <dd>{data.SyncedDate ? format.dateTime(data.SyncedDate) : '-'}</dd>
        </dl>
        <p className="m-0 text-xs text-fg3">{t.addon.statsNote}</p>
      </div>
    </article>
  )
}

function ListingCard({ context, data }: { context: WorkspaceContext; data: FirefoxAddonDto }) {
  const t = useFirefox()
  const plan = useFirefoxPlan(context)
  const listing = data.Listing ?? []
  const [field, setField] = useState<FirefoxField>('Summary')
  const [locale, setLocale] = useState(data.DefaultLocale ?? 'en-US')
  const [value, setValue] = useState('')
  const [pending, setPending] = useState<FirefoxListingValue[]>([])
  const [categories, setCategories] = useState((data.Categories ?? []).join(', '))

  const add = (event: FormEvent) => {
    event.preventDefault()
    const entry = { Field: field, Locale: locale.trim(), Value: value.trim() || null }
    setPending((current) => [...current.filter((item) => !(item.Field === entry.Field && item.Locale === entry.Locale)), entry])
    setValue('')
  }
  const nextCategories = categories
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
  const categoriesChanged = nextCategories.slice().sort().join(',') !== (data.Categories ?? []).slice().sort().join(',')

  return (
    <article className="card" aria-label={t.listing.title}>
      <div className="card-h">
        <h2 className="h3">{t.listing.title}</h2>
      </div>
      <div className="card-b grid gap-3">
        <p className="m-0 text-[13px] text-fg2">{t.listing.body}</p>
        {listing.length === 0 ? (
          <p className="m-0 text-[13px] text-fg3">{t.listing.empty}</p>
        ) : (
          <dl className="kv text-[13px]">
            {listing.map((item) => (
              <div key={`${item.Field}-${item.Locale}`} className="contents">
                <dt>
                  {t.listing.fields[item.Field as FirefoxField] ?? item.Field} <span className="mono text-fg3">{item.Locale}</span>
                </dt>
                <dd className="whitespace-pre-wrap">{item.Value}</dd>
              </div>
            ))}
          </dl>
        )}
        <form className="grid gap-3" onSubmit={add} aria-label={t.listing.add}>
          <div className="grid gap-3 sm:grid-cols-[1fr_8rem]">
            <Field label={t.listing.field}>
              <Select<FirefoxField>
                aria-label={t.listing.field}
                value={field}
                options={FIREFOX_FIELDS.map((item) => ({ value: item, label: t.listing.fields[item] }))}
                onChange={setField}
              />
            </Field>
            <Field label={t.listing.locale}>
              <Input mono value={locale} maxLength={20} onChange={(event) => setLocale(event.target.value)} />
            </Field>
          </div>
          <Field label={t.listing.value} hint={t.listing.valueHint} optional>
            <Textarea rows={field === 'Description' ? 6 : 2} value={value} onChange={(event) => setValue(event.target.value)} />
          </Field>
          <span>
            <Button type="submit" size="sm" disabled={!context.editor || !locale.trim()}>
              {t.listing.add}
            </Button>
          </span>
        </form>
        {pending.length > 0 ? (
          <ul className="m-0 grid list-none gap-1 p-0 text-[13px]" aria-label={t.listing.pending}>
            {pending.map((item) => (
              <li key={`${item.Field}-${item.Locale}`} className="flex flex-wrap items-center gap-2">
                <Badge small>{t.listing.fields[item.Field as FirefoxField]}</Badge>
                <span className="mono">{item.Locale}</span>
                <span className="min-w-0 flex-1 truncate">{item.Value ?? '-'}</span>
                <Button size="sm" variant="ghost" onClick={() => setPending((current) => current.filter((entry) => entry !== item))}>
                  {t.listing.remove}
                </Button>
              </li>
            ))}
          </ul>
        ) : null}
        <Field label={t.listing.categories} hint={t.listing.categoriesHint}>
          <Input mono value={categories} maxLength={300} onChange={(event) => setCategories(event.target.value)} />
        </Field>
        {plan.element}
        <span>
          <Button
            variant="primary"
            loading={plan.pending}
            disabled={!context.editor || (pending.length === 0 && !categoriesChanged)}
            onClick={() => plan.make({ Kind: FirefoxChangeKind.Listing, Listing: pending, Categories: categoriesChanged ? nextCategories : null })}
          >
            {t.listing.plan}
          </Button>
        </span>
      </div>
    </article>
  )
}

function SubmitCard({ context, data }: { context: WorkspaceContext; data: FirefoxAddonDto }) {
  const t = useFirefox()
  const format = useFormat()
  const plan = useFirefoxPlan(context)
  const packages = data.Packages ?? []
  const candidates = packages.filter(
    (item) => !item.Blocked && !item.PackageDeleted && (!data.LatestVersion || compareVersions(item.Version, data.LatestVersion) > 0),
  )
  const [sessionId, setSessionId] = useState<number>(candidates[0]?.SessionId ?? 0)
  const [channel, setChannel] = useState<FirefoxVersionChannel>(FirefoxVersionChannel.Listed)
  const [notes, setNotes] = useState('')
  const [approvalNotes, setApprovalNotes] = useState('')
  const [license, setLicense] = useState('')
  const [source, setSource] = useState('')
  const locale = data.DefaultLocale ?? 'en-US'

  const submit = (event: FormEvent) => {
    event.preventDefault()
    plan.make({
      Kind: FirefoxChangeKind.Version,
      SessionId: sessionId || null,
      Channel: channel,
      ReleaseNotes: notes.trim() ? [{ Locale: locale, Value: notes.trim() }] : [],
      ApprovalNotes: approvalNotes.trim() || null,
      License: license.trim() || null,
      SourceAssetId: parseIds(source)[0] ?? null,
    })
  }

  return (
    <article className="card" aria-label={t.packages.title}>
      <div className="card-h">
        <h2 className="h3">{t.packages.title}</h2>
      </div>
      <div className="card-b grid gap-3">
        <p className="m-0 text-[13px] text-fg2">{t.packages.body}</p>
        {packages.length === 0 ? (
          <EmptyState icon={<Package aria-hidden />} title={t.packages.empty} />
        ) : (
          <ul className="m-0 grid list-none gap-2 p-0">
            {packages.map((item) => (
              <li key={item.SessionId} className="grid gap-1 text-[13px]">
                <span className="flex flex-wrap items-center gap-2">
                  <b className="mono">{item.FileName}</b>
                  <span className="mono">{item.Version ?? '-'}</span>
                  {item.ManifestVersion ? <Badge small>{interpolate(t.packages.manifest, { version: item.ManifestVersion })}</Badge> : null}
                  {item.PackageDeleted ? <Badge small>{t.packages.deleted}</Badge> : null}
                  {item.Blocked ? (
                    <Badge small tone="danger">
                      {t.packages.blocked}
                    </Badge>
                  ) : null}
                  <span className="text-fg3">{format.dateTime(item.StartDate)}</span>
                </span>
                {(item.Issues ?? []).length > 0 ? <span className="text-xs text-fg3">{(item.Issues ?? []).join(' ')}</span> : null}
              </li>
            ))}
          </ul>
        )}
        <form className="grid gap-3" onSubmit={submit} aria-label={t.submit.title}>
          <h3 className="h3">{t.submit.title}</h3>
          <p className="m-0 text-[13px] text-fg2">{t.submit.body}</p>
          {candidates.length === 0 ? <Callout tone="info">{t.submit.noPackage}</Callout> : null}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t.submit.package}>
              <Select
                aria-label={t.submit.package}
                value={sessionId}
                options={candidates.map((item) => ({ value: item.SessionId, label: `${item.Version ?? '-'} · ${item.FileName}` }))}
                onChange={setSessionId}
              />
            </Field>
            <Field label={t.submit.channel}>
              <Select<FirefoxVersionChannel>
                aria-label={t.submit.channel}
                value={channel}
                options={[
                  { value: FirefoxVersionChannel.Listed, label: t.channel.listed },
                  { value: FirefoxVersionChannel.Unlisted, label: t.channel.unlisted },
                ]}
                onChange={setChannel}
              />
            </Field>
          </div>
          <Field label={t.submit.notes} optional>
            <Textarea rows={3} value={notes} maxLength={3000} onChange={(event) => setNotes(event.target.value)} />
          </Field>
          <Field label={t.submit.approvalNotes} hint={t.submit.approvalNotesHint} optional>
            <Textarea rows={3} value={approvalNotes} maxLength={3000} onChange={(event) => setApprovalNotes(event.target.value)} />
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t.submit.license} hint={t.submit.licenseHint} optional>
              <Input mono value={license} maxLength={100} onChange={(event) => setLicense(event.target.value)} />
            </Field>
            <Field label={t.submit.source} hint={t.submit.sourceHint} optional>
              <Input inputMode="numeric" value={source} maxLength={20} onChange={(event) => setSource(event.target.value)} />
            </Field>
          </div>
          {plan.element}
          <span>
            <Button type="submit" variant="primary" loading={plan.pending} disabled={!context.editor || !sessionId}>
              {t.submit.plan}
            </Button>
          </span>
        </form>
      </div>
    </article>
  )
}

function VersionsCard({ context, data }: { context: WorkspaceContext; data: FirefoxAddonDto }) {
  const t = useFirefox()
  const format = useFormat()
  const plan = useFirefoxPlan(context)
  const versions = data.Versions ?? []
  const locale = data.DefaultLocale ?? 'en-US'
  const [editing, setEditing] = useState<string | null>(null)
  const [notes, setNotes] = useState('')
  return (
    <article className="card" aria-label={t.versions.title}>
      <div className="card-h">
        <h2 className="h3">{t.versions.title}</h2>
      </div>
      <div className="card-b grid gap-3">
        {plan.element}
        {versions.length === 0 ? (
          <p className="m-0 text-[13px] text-fg3">{t.versions.empty}</p>
        ) : (
          <ul className="m-0 grid list-none gap-2 p-0">
            {versions.map((version) => (
              <li key={version.Id} className="grid gap-1 text-[13px]">
                <span className="flex flex-wrap items-center gap-2">
                  <b className="mono">{version.Version}</b>
                  <Badge small tone={fileStatusTone(version.FileStatus)} dot>
                    {t.file[fileStatusKey(version.FileStatus)]}
                  </Badge>
                  <span className="text-fg3">{version.Channel === FirefoxVersionChannel.Unlisted ? t.channel.unlisted : t.channel.listed}</span>
                  {version.License ? <span className="mono text-fg3">{version.License}</span> : null}
                  {version.HasSource ? <Badge small>{t.versions.source}</Badge> : null}
                  {version.Created ? <span className="text-fg3">{format.dateTime(version.Created)}</span> : null}
                  <Button
                    size="sm"
                    variant="ghost"
                    disabled={!context.editor}
                    onClick={() => {
                      setEditing(version.Version)
                      setNotes((version.ReleaseNotes ?? []).find((note) => note.Locale === locale)?.Value ?? '')
                    }}
                  >
                    {interpolate(t.versions.notesTitle, { version: version.Version })}
                  </Button>
                </span>
                {(version.ReleaseNotes ?? []).length > 0 ? (
                  <span className="whitespace-pre-wrap text-xs text-fg2">{(version.ReleaseNotes ?? [])[0].Value}</span>
                ) : null}
              </li>
            ))}
          </ul>
        )}
        {editing ? (
          <form
            className="grid gap-2"
            aria-label={interpolate(t.versions.notesTitle, { version: editing })}
            onSubmit={(event) => {
              event.preventDefault()
              plan.make({ Kind: FirefoxChangeKind.ReleaseNotes, Version: editing, ReleaseNotes: [{ Locale: locale, Value: notes.trim() || null }] })
            }}
          >
            <Field label={interpolate(t.versions.notesTitle, { version: editing })}>
              <Textarea rows={3} value={notes} maxLength={3000} onChange={(event) => setNotes(event.target.value)} />
            </Field>
            <span>
              <Button type="submit" size="sm" loading={plan.pending} disabled={!context.editor}>
                {t.versions.notesPlan}
              </Button>
            </span>
          </form>
        ) : null}
      </div>
    </article>
  )
}

function MediaCard({ context, data }: { context: WorkspaceContext; data: FirefoxAddonDto }) {
  const t = useFirefox()
  const plan = useFirefoxPlan(context)
  const previews = data.Previews ?? []
  const [removing, setRemoving] = useState<number[]>([])
  const [addIds, setAddIds] = useState('')
  const [iconId, setIconId] = useState('')
  return (
    <article className="card" aria-label={t.media.title}>
      <div className="card-h">
        <h2 className="h3">{t.media.title}</h2>
      </div>
      <div className="card-b grid gap-3">
        <p className="m-0 text-[13px] text-fg2">{t.media.body}</p>
        {plan.element}
        {previews.length === 0 ? (
          <p className="m-0 text-[13px] text-fg3">{t.media.empty}</p>
        ) : (
          <ul className="m-0 grid list-none grid-cols-2 gap-2 p-0 sm:grid-cols-4">
            {previews.map((preview) => (
              <li key={preview.Id} className="grid gap-1">
                {preview.ThumbnailUrl || preview.ImageUrl ? (
                  <img src={preview.ThumbnailUrl ?? preview.ImageUrl ?? ''} alt={preview.Caption ?? ''} className="w-full rounded" />
                ) : null}
                <Checkbox
                  checked={removing.includes(preview.Id)}
                  onChange={(checked) =>
                    setRemoving((current) => (checked ? [...current, preview.Id] : current.filter((id) => id !== preview.Id)))
                  }
                >
                  {t.media.remove}
                </Checkbox>
              </li>
            ))}
          </ul>
        )}
        {removing.length > 0 ? (
          <span>
            <Button
              size="sm"
              disabled={!context.editor}
              loading={plan.pending}
              onClick={() => plan.make({ Kind: FirefoxChangeKind.Previews, DeletePreviewIds: removing })}
            >
              {t.media.removeAll}
            </Button>
          </span>
        ) : null}
        <form
          className="grid gap-2"
          aria-label={t.media.addPlan}
          onSubmit={(event) => {
            event.preventDefault()
            plan.make({ Kind: FirefoxChangeKind.Previews, AddPreviewAssetIds: parseIds(addIds) })
          }}
        >
          <Field label={t.media.addIds} hint={t.media.addIdsHint}>
            <Input mono value={addIds} maxLength={200} onChange={(event) => setAddIds(event.target.value)} />
          </Field>
          <span>
            <Button type="submit" size="sm" disabled={!context.editor || parseIds(addIds).length === 0}>
              {t.media.addPlan}
            </Button>
          </span>
        </form>
        <form
          className="grid gap-2"
          aria-label={t.media.iconPlan}
          onSubmit={(event) => {
            event.preventDefault()
            plan.make({ Kind: FirefoxChangeKind.Icon, IconAssetId: parseIds(iconId)[0] ?? null })
          }}
        >
          <Field label={t.media.iconId}>
            <Input inputMode="numeric" value={iconId} maxLength={20} onChange={(event) => setIconId(event.target.value)} />
          </Field>
          <span>
            <Button type="submit" size="sm" disabled={!context.editor || parseIds(iconId).length === 0}>
              {t.media.iconPlan}
            </Button>
          </span>
        </form>
      </div>
    </article>
  )
}

function HistoryCard({ data }: { data: FirefoxAddonDto }) {
  const t = useFirefox()
  const format = useFormat()
  const history = data.StatusHistory ?? []
  return (
    <article className="card" aria-label={t.history.title}>
      <div className="card-h">
        <h2 className="h3">{t.history.title}</h2>
      </div>
      <div className="card-b">
        {history.length === 0 ? (
          <p className="m-0 text-[13px] text-fg3">{t.history.empty}</p>
        ) : (
          <Timeline
            items={history.map((change, index) => ({
              id: `${change.Subject}-${change.Date}-${index}`,
              tone: change.State === 'Disabled' || change.State === 'Rejected' ? 'dng' : index === 0 ? 'now' : 'ok',
              title: `${change.Subject === 'addon' ? t.history.addon : t.history.version}: ${change.State} ${change.Version ?? ''}`.trim(),
              meta: format.dateTime(change.Date),
            }))}
          />
        )}
      </div>
    </article>
  )
}

function FirefoxBody({ context }: { context: WorkspaceContext }) {
  const t = useFirefox()
  const data = useQuery({
    queryKey: firefoxKeys.addon(context.organizationId, context.record.StoreAppId),
    queryFn: () => firefoxApi.addon(context.organizationId, context.record.StoreAppId),
  })
  if (data.isLoading) {
    return <CardSkeleton />
  }
  if (data.isError || !data.data) {
    return <ScreenState error={data.error} onRetry={() => void data.refetch()} />
  }
  const addon = data.data
  return (
    <div className="grid gap-4">
      {!context.editor ? <Callout tone="info">{t.readOnly}</Callout> : null}
      <AddonCard context={context} data={addon} />
      <ListingCard key={`listing-${(addon.Listing ?? []).length}-${(addon.Categories ?? []).join(',')}`} context={context} data={addon} />
      <SubmitCard key={`submit-${(addon.Packages ?? []).length}-${addon.LatestVersion ?? ''}`} context={context} data={addon} />
      <VersionsCard context={context} data={addon} />
      <MediaCard context={context} data={addon} />
      <HistoryCard data={addon} />
    </div>
  )
}

/** The Firefox Add-ons screen of an app: the app workspace on its Firefox record. */
export function FirefoxPage() {
  return (
    <AppWorkspace section="firefox" stores={[StoreKind.Firefox]}>
      {(context) => <FirefoxBody context={context} />}
    </AppWorkspace>
  )
}
