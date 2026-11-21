import { useMemo, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, GitPullRequest, Plus, RefreshCw, Trash2 } from 'lucide-react'
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
import { wingetApi } from './api'
import { useWinget } from './i18n/useWinget'
import { wingetKeys } from './keys'
import { ARCHITECTURES, INSTALLER_TYPES, LISTING_FIELDS, SCOPES, installersFromSources, needsAction, stateKey, stateTone } from './rules'
import {
  WingetChangeKind,
  type WingetChangeInput,
  type WingetInstallerInputDto,
  type WingetManifestDto,
  type WingetPackageDto,
  type WingetPullRequestDto,
} from './types'

// The winget screen of an app (one page): the package as winget-pkgs has it, its pull requests and their review state,
// the manifests of a version with storeasy's checks, and the two writes, a new version and locale text edits. Each write
// is a change plan shown in the shared plan dialog; approving it opens the pull request (ChangePlanKind.WingetChange).

/** Plans a winget change (always a dry run) and shows the saved plan in the shared plan dialog. */
function useWingetPlan(context: WorkspaceContext) {
  const queryClient = useQueryClient()
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const make = useMutation({
    mutationFn: (change: WingetChangeInput) => wingetApi.change(context.organizationId, context.record.StoreAppId, change),
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
      <PlanDialog
        plan={plan}
        onClose={() => setPlan(null)}
        onDecided={() => void queryClient.invalidateQueries({ queryKey: wingetKeys.all })}
      />
    </>
  )
  return { make: (change: WingetChangeInput) => make.mutate(change), pending: make.isPending, element }
}

function PackageCard({ context, data }: { context: WorkspaceContext; data: WingetPackageDto }) {
  const t = useWinget()
  const format = useFormat()
  const { watchJobs } = useActivity()
  const sync = useMutation({
    mutationFn: () => wingetApi.sync(context.organizationId, data.StoreAppId),
    onSuccess: (jobId) => {
      watchJobs([jobId])
      showToast(t.package.syncQueued, 'success', data.PackageIdentifier)
    },
  })
  return (
    <article className="card" aria-label={t.package.title}>
      <div className="card-h flex-wrap">
        <h2 className="h3 min-w-0 flex-1 truncate mono">{data.PackageIdentifier}</h2>
        <a className="lnk inline-flex items-center gap-1 text-[13px]" href={data.PackagePageUrl} target="_blank" rel="noopener noreferrer">
          <ExternalLink className="h-3.5 w-3.5" aria-hidden />
          {t.package.openFolder}
        </a>
        <Button size="sm" loading={sync.isPending} onClick={() => sync.mutate()}>
          <RefreshCw aria-hidden />
          {t.package.sync}
        </Button>
      </div>
      <div className="card-b grid gap-3">
        {!data.IsPublished ? <Callout tone="info">{t.package.notPublished}</Callout> : null}
        {data.SyncError ? <Callout tone="danger">{interpolate(t.package.syncError, { error: data.SyncError })}</Callout> : null}
        <dl className="kv text-[13px]">
          <dt>{t.package.name}</dt>
          <dd>{data.PackageName ?? '-'}</dd>
          <dt>{t.package.publisher}</dt>
          <dd>{data.Publisher ?? '-'}</dd>
          <dt>{t.package.latest}</dt>
          <dd className="mono">{data.LatestVersion ?? '-'}</dd>
          <dt>{t.package.versions}</dt>
          <dd className="mono">{(data.Versions ?? []).join(', ') || '-'}</dd>
          <dt>{t.package.locales}</dt>
          <dd>{(data.Locales ?? []).join(', ') || '-'}</dd>
          <dt>{t.package.schema}</dt>
          <dd className="mono">
            {data.ManifestVersion ?? '-'} · {interpolate(t.package.schemaWritten, { version: data.WrittenManifestVersion })}
          </dd>
          <dt>{t.package.fork}</dt>
          <dd className="mono">{data.Fork ?? '-'}</dd>
          <dt>{t.package.synced}</dt>
          <dd>{data.SyncedDate ? format.dateTime(data.SyncedDate) : '-'}</dd>
        </dl>
        <p className="m-0 text-xs text-fg3">{t.package.noStats}</p>
      </div>
    </article>
  )
}

function PullRequestCard({ pull }: { pull: WingetPullRequestDto }) {
  const t = useWinget()
  const format = useFormat()
  const comment = (pull.Comments ?? [])[0]
  const history = pull.StatusHistory ?? []
  return (
    <article className="card" aria-label={interpolate(t.pulls.number, { number: pull.Number })}>
      <div className="card-h flex-wrap">
        <GitPullRequest className="h-4 w-4" aria-hidden />
        <h3 className="h3 min-w-0 flex-1 truncate">
          {interpolate(t.pulls.number, { number: pull.Number })} {pull.Title ?? ''}
        </h3>
        <Badge tone={stateTone(pull.State)} dot>
          {t.state[stateKey(pull.State)]}
        </Badge>
      </div>
      <div className="card-b grid gap-3">
        {needsAction(pull.State) ? <Callout tone="warning">{t.pulls.needsAction}</Callout> : null}
        <dl className="kv text-[13px]">
          <dt>{t.pulls.version}</dt>
          <dd className="mono">{pull.PackageVersion ?? '-'}</dd>
          <dt>{t.pulls.opened}</dt>
          <dd>{pull.OpenedDate ? format.dateTime(pull.OpenedDate) : '-'}</dd>
          <dt>{t.pulls.labels}</dt>
          <dd className="flex flex-wrap gap-1">
            {(pull.Labels ?? []).length > 0
              ? (pull.Labels ?? []).map((label) => (
                  <Badge key={label} small>
                    {label}
                  </Badge>
                ))
              : '-'}
          </dd>
          <dt>{t.pulls.checks}</dt>
          <dd>
            {(pull.Checks ?? []).map((check) => `${check.Name ?? '?'}: ${check.Conclusion ?? check.Status ?? '-'}`).join(', ') || '-'}
          </dd>
        </dl>
        <div className="flex flex-wrap gap-2">
          <Badge small>{pull.CreatedByStoreasy ? t.pulls.byStoreasy : t.pulls.outside}</Badge>
          {pull.Url ? (
            <a className="lnk inline-flex items-center gap-1 text-[13px]" href={pull.Url} target="_blank" rel="noopener noreferrer">
              <ExternalLink className="h-3.5 w-3.5" aria-hidden />
              {t.pulls.open}
            </a>
          ) : null}
        </div>
        {comment ? (
          <section aria-label={t.pulls.comments}>
            <h4 className="lbl mb-1">{t.pulls.comments}</h4>
            <p className="m-0 text-[13px] text-fg2">
              <b>{comment.Author}</b> · {format.dateTime(comment.Date)}: {comment.Excerpt}
            </p>
          </section>
        ) : null}
        {history.length > 1 ? (
          <Timeline
            items={history.map((change, index) => ({
              id: `${change.State}-${index}`,
              tone: index === history.length - 1 ? (needsAction(change.State) ? 'dng' : 'now') : 'ok',
              title: t.state[stateKey(change.State)],
              meta: format.dateTime(change.Date),
            }))}
          />
        ) : null}
      </div>
    </article>
  )
}

function ManifestCard({ context, data }: { context: WorkspaceContext; data: WingetPackageDto }) {
  const t = useWinget()
  const [version, setVersion] = useState<string | null>(data.LatestVersion ?? null)
  const manifest = useQuery({
    queryKey: wingetKeys.manifest(context.organizationId, data.StoreAppId, version),
    queryFn: () => wingetApi.manifest(context.organizationId, data.StoreAppId, version),
    enabled: data.IsPublished,
  })
  if (!data.IsPublished) {
    return null
  }
  const body = manifest.data
  const issues = body?.Issues ?? []
  return (
    <article className="card" aria-label={t.manifest.title}>
      <div className="card-h flex-wrap">
        <h2 className="h3 min-w-0 flex-1">{t.manifest.title}</h2>
        <Select
          aria-label={t.manifest.version}
          value={version}
          options={(data.Versions ?? []).map((item) => ({ value: item, label: item }))}
          onChange={setVersion}
        />
      </div>
      <div className="card-b grid gap-4">
        {manifest.isLoading ? <CardSkeleton count={1} /> : null}
        {manifest.isError ? <ScreenState error={manifest.error} onRetry={() => void manifest.refetch()} /> : null}
        {body ? (
          <>
            {issues.length === 0 ? (
              <Callout tone="success">{t.manifest.noIssues}</Callout>
            ) : (
              <Callout tone={issues.some((issue) => issue.Blocking) ? 'danger' : 'warning'} title={t.manifest.issues}>
                <ul className="m-0 grid list-none gap-1 p-0">
                  {issues.map((issue, index) => (
                    <li key={`${issue.Key}-${index}`}>
                      <Badge small tone={issue.Blocking ? 'danger' : 'warning'}>
                        {issue.Blocking ? t.manifest.blocking : t.manifest.warning}
                      </Badge>{' '}
                      {issue.Locale ? <span className="mono">{issue.Locale} · </span> : null}
                      {issue.Message}
                    </li>
                  ))}
                </ul>
              </Callout>
            )}
            <section aria-label={t.manifest.listing}>
              <h3 className="lbl mb-1">{t.manifest.listing}</h3>
              {(body.Locales ?? []).map((texts) => (
                <dl key={texts.Locale} className="kv mb-2 text-[13px]">
                  <dt className="mono">{texts.Locale}</dt>
                  <dd />
                  {Object.entries(texts.Fields ?? {}).map(([field, value]) => (
                    <div key={field} className="contents">
                      <dt>{field}</dt>
                      <dd className="whitespace-pre-wrap">{value}</dd>
                    </div>
                  ))}
                </dl>
              ))}
            </section>
            <section aria-label={t.manifest.installers}>
              <h3 className="lbl mb-1">{t.manifest.installers}</h3>
              <ul className="m-0 grid list-none gap-1 p-0 text-[13px]">
                {(body.Installers ?? []).map((installer, index) => (
                  <li key={`${installer.Url}-${index}`} className="mono break-all">
                    {[installer.Architecture, installer.Scope, installer.Type].filter(Boolean).join(' · ')} · {installer.Url} ·{' '}
                    {installer.Sha256}
                  </li>
                ))}
              </ul>
            </section>
            <details>
              <summary className="lbl cursor-pointer">{t.manifest.files}</summary>
              {(body.Files ?? []).map((file) => (
                <div key={file.FileName} className="mt-2">
                  <h4 className="mono text-[12.5px]">{file.FileName}</h4>
                  <pre className="code max-h-80 overflow-auto text-[12px]">{file.Content}</pre>
                </div>
              ))}
            </details>
          </>
        ) : null}
      </div>
    </article>
  )
}

const EMPTY_INSTALLER: WingetInstallerInputDto = {
  Url: '',
  Architecture: 'x64',
  Type: null,
  Scope: null,
  Sha256: null,
  SilentSwitches: null,
}

function NewVersionCard({ context, data }: { context: WorkspaceContext; data: WingetPackageDto }) {
  const t = useWinget()
  const plan = useWingetPlan(context)
  const sources = data.InstallerSources ?? []
  const [version, setVersion] = useState('')
  const [fromMicrosoft, setFromMicrosoft] = useState(false)
  const [installers, setInstallers] = useState<WingetInstallerInputDto[]>([{ ...EMPTY_INSTALLER }])
  const [notes, setNotes] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const locale = data.DefaultLocale ?? 'en-US'
  const given = installers.filter((installer) => installer.Url.trim())
  const errors = {
    version: version.trim() ? null : t.newVersion.needVersion,
    installers: given.length > 0 || (fromMicrosoft && sources.length > 0) ? null : t.newVersion.needInstaller,
  }
  const set = (index: number, patch: Partial<WingetInstallerInputDto>) =>
    setInstallers((current) => current.map((installer, at) => (at === index ? { ...installer, ...patch } : installer)))

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setSubmitted(true)
    if (errors.version || errors.installers) {
      return
    }
    plan.make({
      Kind: WingetChangeKind.NewVersion,
      Version: version.trim(),
      FromMicrosoftStore: fromMicrosoft,
      Installers: given.map((installer) => ({
        ...installer,
        Url: installer.Url.trim(),
        Scope: installer.Scope || null,
        Type: installer.Type || null,
        Sha256: installer.Sha256?.trim() || null,
        SilentSwitches: installer.SilentSwitches?.trim() || null,
        ProductCode: installer.ProductCode?.trim() || null,
      })),
      Locales: notes.trim() ? [{ Locale: locale, Fields: { ReleaseNotes: notes.trim() } }] : [],
    })
  }

  return (
    <article className="card" aria-label={t.newVersion.title}>
      <div className="card-h">
        <h2 className="h3">{t.newVersion.title}</h2>
      </div>
      <form className="card-b grid gap-4" onSubmit={submit} noValidate>
        <p className="m-0 text-[13px] text-fg2">{t.newVersion.body}</p>
        <Field label={t.newVersion.version} hint={t.newVersion.versionHint} error={submitted ? errors.version : null}>
          <Input mono value={version} maxLength={128} onChange={(event) => setVersion(event.target.value)} />
        </Field>
        {sources.length > 0 ? (
          <div className="grid gap-1">
            <Checkbox checked={fromMicrosoft} onChange={setFromMicrosoft}>
              {t.newVersion.fromMicrosoft}
            </Checkbox>
            <span className="hint">{t.newVersion.fromMicrosoftHint}</span>
          </div>
        ) : null}
        {fromMicrosoft ? (
          <ul className="m-0 grid list-none gap-1 p-0 text-[13px]">
            {installersFromSources(sources).map((installer, index) => (
              <li key={`${installer.Url}-${installer.Architecture}-${index}`} className="mono break-all">
                {installer.Architecture} · {installer.Type} · {installer.Url}
              </li>
            ))}
          </ul>
        ) : null}
        {installers.map((installer, index) => (
          <fieldset key={index} className="grid gap-3 rounded-sm border border-border p-3">
            <legend className="lbl px-1">{interpolate(t.newVersion.installer, { index: index + 1 })}</legend>
            <Field label={t.newVersion.url} hint={t.newVersion.urlHint}>
              <Input mono value={installer.Url} maxLength={2048} onChange={(event) => set(index, { Url: event.target.value })} />
            </Field>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field label={t.newVersion.architecture}>
                <Select
                  aria-label={t.newVersion.architecture}
                  value={installer.Architecture}
                  options={ARCHITECTURES.map((value) => ({ value, label: value }))}
                  onChange={(value) => set(index, { Architecture: value })}
                />
              </Field>
              <Field label={t.newVersion.type}>
                <Select
                  aria-label={t.newVersion.type}
                  value={installer.Type ?? ''}
                  options={[{ value: '', label: '-' }, ...INSTALLER_TYPES.map((value) => ({ value, label: value }))]}
                  onChange={(value) => set(index, { Type: value || null })}
                />
              </Field>
              <Field label={t.newVersion.scope}>
                <Select
                  aria-label={t.newVersion.scope}
                  value={installer.Scope ?? ''}
                  options={SCOPES.map((value) => ({ value, label: value || t.newVersion.scopeNone }))}
                  onChange={(value) => set(index, { Scope: value || null })}
                />
              </Field>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label={t.newVersion.silent} hint={t.newVersion.silentHint}>
                <Input
                  mono
                  value={installer.SilentSwitches ?? ''}
                  maxLength={512}
                  onChange={(event) => set(index, { SilentSwitches: event.target.value })}
                />
              </Field>
              <Field label={t.newVersion.sha} hint={t.newVersion.shaHint}>
                <Input
                  mono
                  value={installer.Sha256 ?? ''}
                  maxLength={64}
                  onChange={(event) => set(index, { Sha256: event.target.value })}
                />
              </Field>
            </div>
            <Field label={t.newVersion.productCode}>
              <Input
                mono
                value={installer.ProductCode ?? ''}
                maxLength={255}
                onChange={(event) => set(index, { ProductCode: event.target.value })}
              />
            </Field>
            {installers.length > 1 ? (
              <span>
                <Button size="sm" variant="ghost" onClick={() => setInstallers((current) => current.filter((_, at) => at !== index))}>
                  <Trash2 aria-hidden />
                  {t.newVersion.remove}
                </Button>
              </span>
            ) : null}
          </fieldset>
        ))}
        {submitted && errors.installers ? <Callout tone="danger">{errors.installers}</Callout> : null}
        <span>
          <Button size="sm" onClick={() => setInstallers((current) => [...current, { ...EMPTY_INSTALLER }])}>
            <Plus aria-hidden />
            {t.newVersion.addInstaller}
          </Button>
        </span>
        <Field label={interpolate(t.newVersion.releaseNotes, { locale })} optional>
          <Textarea value={notes} rows={4} maxLength={10000} onChange={(event) => setNotes(event.target.value)} />
        </Field>
        {plan.element}
        <span>
          <Button type="submit" variant="primary" loading={plan.pending} disabled={!context.editor}>
            {t.newVersion.plan}
          </Button>
        </span>
      </form>
    </article>
  )
}

function ListingCard({ context, data, manifest }: { context: WorkspaceContext; data: WingetPackageDto; manifest: WingetManifestDto }) {
  const t = useWinget()
  const plan = useWingetPlan(context)
  const locales = useMemo(() => (manifest.Locales ?? []).map((texts) => texts.Locale), [manifest])
  const textsOf = (tag: string) => (manifest.Locales ?? []).find((texts) => texts.Locale.toLowerCase() === tag.toLowerCase())
  const [locale, setLocale] = useState(locales[0] ?? data.DefaultLocale ?? 'en-US')
  const [newLocale, setNewLocale] = useState('')
  const [values, setValues] = useState<Record<string, Record<string, string>>>({})
  const [tags, setTags] = useState<Record<string, string>>({})
  const active = newLocale.trim() || locale
  const current = { ...(textsOf(active)?.Fields ?? {}), ...(values[active] ?? {}) }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const edits = Object.entries(values).map(([tag, fields]) => ({
      Locale: tag,
      Fields: fields,
      Tags:
        tags[tag] !== undefined
          ? tags[tag]
              .split(',')
              .map((item) => item.trim())
              .filter(Boolean)
          : null,
    }))
    for (const [tag, text] of Object.entries(tags)) {
      if (!values[tag]) {
        edits.push({
          Locale: tag,
          Fields: {},
          Tags: text
            .split(',')
            .map((item) => item.trim())
            .filter(Boolean),
        })
      }
    }
    plan.make({ Kind: WingetChangeKind.Listing, Version: manifest.Version ?? null, Locales: edits })
  }

  return (
    <article className="card" aria-label={t.listing.title}>
      <div className="card-h">
        <h2 className="h3">{t.listing.title}</h2>
      </div>
      <form className="card-b grid gap-4" onSubmit={submit} noValidate>
        <p className="m-0 text-[13px] text-fg2">{t.listing.body}</p>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t.listing.locale}>
            <Select
              aria-label={t.listing.locale}
              value={locale}
              options={locales.map((value) => ({ value, label: value }))}
              onChange={setLocale}
            />
          </Field>
          <Field label={t.listing.addLocale} optional>
            <Input mono value={newLocale} maxLength={20} onChange={(event) => setNewLocale(event.target.value)} />
          </Field>
        </div>
        {LISTING_FIELDS.map((field) => (
          <Field key={field.name} label={field.name} optional>
            {'multiline' in field && field.multiline ? (
              <Textarea
                rows={4}
                maxLength={field.max}
                value={current[field.name] ?? ''}
                onChange={(event) =>
                  setValues((all) => ({ ...all, [active]: { ...(all[active] ?? {}), [field.name]: event.target.value } }))
                }
              />
            ) : (
              <Input
                maxLength={field.max}
                value={current[field.name] ?? ''}
                onChange={(event) =>
                  setValues((all) => ({ ...all, [active]: { ...(all[active] ?? {}), [field.name]: event.target.value } }))
                }
              />
            )}
          </Field>
        ))}
        <Field label={t.listing.tags} optional>
          <Input
            value={tags[active] ?? (textsOf(active)?.Tags ?? []).join(', ')}
            onChange={(event) => setTags((all) => ({ ...all, [active]: event.target.value }))}
          />
        </Field>
        {plan.element}
        <span>
          <Button type="submit" variant="primary" loading={plan.pending} disabled={!context.editor}>
            {t.listing.plan}
          </Button>
        </span>
      </form>
    </article>
  )
}

function WingetBody({ context }: { context: WorkspaceContext }) {
  const t = useWinget()
  const data = useQuery({
    queryKey: wingetKeys.package(context.organizationId, context.record.StoreAppId),
    queryFn: () => wingetApi.package(context.organizationId, context.record.StoreAppId),
  })
  const latest = useQuery({
    queryKey: wingetKeys.manifest(context.organizationId, context.record.StoreAppId, data.data?.LatestVersion ?? null),
    queryFn: () => wingetApi.manifest(context.organizationId, context.record.StoreAppId, data.data?.LatestVersion ?? null),
    enabled: !!data.data?.IsPublished,
  })
  if (data.isLoading) {
    return <CardSkeleton />
  }
  if (data.isError || !data.data) {
    return <ScreenState error={data.error} onRetry={() => void data.refetch()} />
  }
  const pulls = data.data.PullRequests ?? []
  return (
    <div className="grid gap-4">
      {!context.editor ? <Callout tone="info">{t.readOnly}</Callout> : null}
      <PackageCard context={context} data={data.data} />
      <section aria-label={t.pulls.title} className="grid gap-3">
        <h2 className="h3">{t.pulls.title}</h2>
        {pulls.length === 0 ? (
          <EmptyState icon={<GitPullRequest aria-hidden />} title={t.pulls.empty} />
        ) : (
          pulls.map((pull) => <PullRequestCard key={pull.Id} pull={pull} />)
        )}
      </section>
      <ManifestCard context={context} data={data.data} />
      <NewVersionCard context={context} data={data.data} />
      {latest.data ? <ListingCard key={latest.data.Version ?? ''} context={context} data={data.data} manifest={latest.data} /> : null}
    </div>
  )
}

/** The winget screen of an app: the app workspace on its winget record. */
export function WingetPage() {
  return (
    <AppWorkspace section="winget" stores={[StoreKind.Winget]}>
      {(context) => <WingetBody context={context} />}
    </AppWorkspace>
  )
}
