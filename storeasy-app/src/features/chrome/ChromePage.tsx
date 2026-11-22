import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, Package, RefreshCw } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Timeline } from '@/components/ui/Progress'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, Input } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { apiErrorMessage } from '@/lib/api/http'
import { StoreKind } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { chromeApi } from './api'
import { useChrome } from './i18n/useChrome'
import { chromeKeys } from './keys'
import { compareVersions, stateKey, stateTone } from './rules'
import { ChromeChangeKind, ChromeItemState, ChromeUploadState, type ChromeChangeInput, type ChromeItemDto } from './types'

// The Chrome Web Store screen of an app (one page): the item's published and submitted revisions, the extension packages
// intake received, the submission form and the release actions (publish staged, deploy percentage, cancel, rollback).
// Each write is a change plan shown in the shared plan dialog; approving it calls the Chrome Web Store API
// (ChangePlanKind.ChromeChange). The listing, privacy practices and visibility have no API and are assisted steps.

const UPLOAD_KEYS = {
  [ChromeUploadState.None]: 'none',
  [ChromeUploadState.Succeeded]: 'succeeded',
  [ChromeUploadState.InProgress]: 'inProgress',
  [ChromeUploadState.Failed]: 'failed',
  [ChromeUploadState.Missing]: 'missing',
} as const

/** Plans a Chrome change (always a dry run) and shows the saved plan in the shared plan dialog. */
function useChromePlan(context: WorkspaceContext) {
  const queryClient = useQueryClient()
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const make = useMutation({
    mutationFn: (change: ChromeChangeInput) => chromeApi.change(context.organizationId, context.record.StoreAppId, change),
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
        onDecided={() => void queryClient.invalidateQueries({ queryKey: chromeKeys.all })}
      />
    </>
  )
  return { make: (change: ChromeChangeInput) => make.mutate(change), pending: make.isPending, element }
}

function percentOf(value: string): number | null {
  const number = Number(value.trim())
  return value.trim() && Number.isInteger(number) ? number : null
}

function ItemCard({ context, data }: { context: WorkspaceContext; data: ChromeItemDto }) {
  const t = useChrome()
  const format = useFormat()
  const { watchJobs } = useActivity()
  const sync = useMutation({
    mutationFn: () => chromeApi.sync(context.organizationId, data.StoreAppId),
    onSuccess: (jobId) => {
      watchJobs([jobId])
      showToast(t.item.syncQueued, 'success', data.ItemId)
    },
  })
  const revision = (state: ChromeItemState, version?: string | null) =>
    state === ChromeItemState.None ? (
      '-'
    ) : (
      <span className="inline-flex flex-wrap items-center gap-2">
        <Badge tone={stateTone(state)} dot>
          {t.state[stateKey(state)]}
        </Badge>
        <span className="mono">{version ?? '-'}</span>
      </span>
    )
  return (
    <article className="card" aria-label={t.item.title}>
      <div className="card-h flex-wrap">
        <h2 className="h3 min-w-0 flex-1 truncate mono">{data.ItemId}</h2>
        <a className="lnk inline-flex items-center gap-1 text-[13px]" href={data.StoreUrl} target="_blank" rel="noopener noreferrer">
          <ExternalLink className="h-3.5 w-3.5" aria-hidden />
          {t.item.openStore}
        </a>
        <a className="lnk inline-flex items-center gap-1 text-[13px]" href={data.DashboardUrl} target="_blank" rel="noopener noreferrer">
          <ExternalLink className="h-3.5 w-3.5" aria-hidden />
          {t.item.openDashboard}
        </a>
        <Button size="sm" loading={sync.isPending} onClick={() => sync.mutate()}>
          <RefreshCw aria-hidden />
          {t.item.sync}
        </Button>
      </div>
      <div className="card-b grid gap-3">
        {data.TakenDown ? (
          <Callout tone="danger">{t.item.takenDown}</Callout>
        ) : data.Warned ? (
          <Callout tone="warning">{t.item.warned}</Callout>
        ) : null}
        {data.SyncError ? <Callout tone="danger">{interpolate(t.item.syncError, { error: data.SyncError })}</Callout> : null}
        {!data.SyncedDate ? <Callout tone="info">{t.item.notSynced}</Callout> : null}
        <dl className="kv text-[13px]">
          <dt>{t.item.published}</dt>
          <dd>{revision(data.PublishedState, data.PublishedVersion)}</dd>
          <dt>{t.item.rollout}</dt>
          <dd>{data.PublishedDeployPercentage != null ? `%${data.PublishedDeployPercentage}` : '-'}</dd>
          <dt>{t.item.submitted}</dt>
          <dd>{revision(data.SubmittedState, data.SubmittedVersion)}</dd>
          <dt>{t.item.uploaded}</dt>
          <dd>
            {t.upload[UPLOAD_KEYS[data.LastUploadState] ?? 'none']}
            {data.UploadedVersion ? <span className="mono"> · {data.UploadedVersion}</span> : null}
          </dd>
          <dt>{t.item.rating}</dt>
          <dd>
            {data.Rating != null ? interpolate(t.item.ratingValue, { rating: data.Rating.toFixed(1), count: data.RatingCount ?? 0 }) : '-'}
          </dd>
          <dt>{t.item.users}</dt>
          <dd>{data.WeeklyUsers != null ? format.number(data.WeeklyUsers) : '-'}</dd>
          <dt>{t.item.publisher}</dt>
          <dd className="mono">{data.PublisherId ?? '-'}</dd>
          <dt>{t.item.synced}</dt>
          <dd>{data.SyncedDate ? format.dateTime(data.SyncedDate) : '-'}</dd>
        </dl>
        <p className="m-0 text-xs text-fg3">{t.item.listingNote}</p>
      </div>
    </article>
  )
}

function HistoryCard({ data }: { data: ChromeItemDto }) {
  const t = useChrome()
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
              id: `${change.Revision}-${change.Date}-${index}`,
              tone: change.State === ChromeItemState.Rejected ? 'dng' : index === 0 ? 'now' : 'ok',
              title:
                `${change.Revision === 'published' ? t.history.published : t.history.submitted}: ${t.state[stateKey(change.State)]} ${change.Version ?? ''}`.trim(),
              meta: format.dateTime(change.Date),
            }))}
          />
        )}
      </div>
    </article>
  )
}

function SubmitCard({ context, data }: { context: WorkspaceContext; data: ChromeItemDto }) {
  const t = useChrome()
  const format = useFormat()
  const plan = useChromePlan(context)
  const packages = (data.Packages ?? []).filter((item) => !item.PackageDeleted)
  const candidates = packages.filter(
    (item) => !item.Blocked && (!data.PublishedVersion || compareVersions(item.Version, data.PublishedVersion) > 0),
  )
  const [sessionId, setSessionId] = useState<number>(candidates[0]?.SessionId ?? 0)
  const [staged, setStaged] = useState(false)
  const [skipReview, setSkipReview] = useState(false)
  const [percentage, setPercentage] = useState('')
  const pending = data.SubmittedState === ChromeItemState.PendingReview

  const submit = (event: FormEvent) => {
    event.preventDefault()
    plan.make({
      Kind: ChromeChangeKind.Publish,
      SessionId: sessionId || null,
      Staged: staged,
      SkipReview: skipReview,
      DeployPercentage: percentOf(percentage),
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
            {(data.Packages ?? []).map((item) => (
              <li key={item.SessionId} className="grid gap-1 text-[13px]">
                <span className="flex flex-wrap items-center gap-2">
                  <b className="mono">{item.FileName}</b>
                  <span className="mono">{item.Version ?? '-'}</span>
                  {item.ManifestVersion ? <Badge small>{interpolate(t.packages.manifest, { version: item.ManifestVersion })}</Badge> : null}
                  {item.Uploaded ? (
                    <Badge small tone="info">
                      {t.packages.uploaded}
                    </Badge>
                  ) : null}
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
          {pending ? <Callout tone="warning">{interpolate(t.submit.pending, { version: data.SubmittedVersion ?? '-' })}</Callout> : null}
          <Field label={t.submit.package}>
            <Select
              aria-label={t.submit.package}
              value={sessionId}
              options={[
                { value: 0, label: t.submit.lastUploaded },
                ...candidates.map((item) => ({ value: item.SessionId, label: `${item.Version ?? '-'} · ${item.FileName}` })),
              ]}
              onChange={setSessionId}
            />
          </Field>
          <Field label={t.submit.percentage} hint={t.submit.percentageHint} optional>
            <Input inputMode="numeric" value={percentage} maxLength={3} onChange={(event) => setPercentage(event.target.value)} />
          </Field>
          <Checkbox checked={staged} onChange={setStaged}>
            {t.submit.staged}
          </Checkbox>
          <Checkbox checked={skipReview} onChange={setSkipReview}>
            {t.submit.skipReview}
          </Checkbox>
          {plan.element}
          <span>
            <Button type="submit" variant="primary" loading={plan.pending} disabled={!context.editor || pending}>
              {t.submit.plan}
            </Button>
          </span>
        </form>
      </div>
    </article>
  )
}

function ReleaseCard({ context, data }: { context: WorkspaceContext; data: ChromeItemDto }) {
  const t = useChrome()
  const plan = useChromePlan(context)
  const [percentage, setPercentage] = useState('')
  const [targetVersion, setTargetVersion] = useState('')
  const [newVersion, setNewVersion] = useState('')
  const [reason, setReason] = useState('')
  const published = data.PublishedState === ChromeItemState.Published || data.PublishedState === ChromeItemState.PublishedToTesters
  const staged = data.SubmittedState === ChromeItemState.Staged
  const cancellable = data.SubmittedState === ChromeItemState.PendingReview || staged
  return (
    <article className="card" aria-label={t.release.title}>
      <div className="card-h">
        <h2 className="h3">{t.release.title}</h2>
      </div>
      <div className="card-b grid gap-4">
        {plan.element}
        {staged ? (
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-[13px]">{interpolate(t.release.stagedNote, { version: data.SubmittedVersion ?? '-' })}</span>
            <Button
              size="sm"
              variant="primary"
              disabled={!context.editor}
              loading={plan.pending}
              onClick={() => plan.make({ Kind: ChromeChangeKind.PublishStaged })}
            >
              {t.release.publishStaged}
            </Button>
          </div>
        ) : null}
        {cancellable ? (
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-[13px]">
              {interpolate(t.release.cancelNote, { version: data.SubmittedVersion ?? '-', state: t.state[stateKey(data.SubmittedState)] })}
            </span>
            <Button
              size="sm"
              disabled={!context.editor}
              loading={plan.pending}
              onClick={() => plan.make({ Kind: ChromeChangeKind.CancelSubmission })}
            >
              {t.release.cancel}
            </Button>
          </div>
        ) : null}
        {published ? (
          <form
            className="grid gap-2"
            aria-label={t.release.rolloutTitle}
            onSubmit={(event) => {
              event.preventDefault()
              plan.make({ Kind: ChromeChangeKind.DeployPercentage, DeployPercentage: percentOf(percentage) })
            }}
          >
            <Field label={t.release.rolloutTitle} hint={t.release.rolloutHint}>
              <Input
                inputMode="numeric"
                value={percentage}
                maxLength={3}
                placeholder={String(data.PublishedDeployPercentage ?? '')}
                onChange={(event) => setPercentage(event.target.value)}
              />
            </Field>
            <span>
              <Button type="submit" size="sm" disabled={!context.editor || percentOf(percentage) === null}>
                {t.release.rolloutPlan}
              </Button>
            </span>
          </form>
        ) : null}
        {published ? (
          <form
            className="grid gap-2"
            aria-label={t.release.rollbackTitle}
            onSubmit={(event) => {
              event.preventDefault()
              plan.make({
                Kind: ChromeChangeKind.Rollback,
                TargetVersion: targetVersion.trim() || null,
                NewVersion: newVersion.trim(),
                Reason: reason.trim(),
              })
            }}
          >
            <h3 className="h3">{t.release.rollbackTitle}</h3>
            <p className="m-0 text-[13px] text-fg2">{t.release.rollbackBody}</p>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label={t.release.targetVersion} hint={t.release.targetVersionHint} optional>
                <Input mono value={targetVersion} maxLength={30} onChange={(event) => setTargetVersion(event.target.value)} />
              </Field>
              <Field label={t.release.newVersion}>
                <Input mono value={newVersion} maxLength={30} onChange={(event) => setNewVersion(event.target.value)} />
              </Field>
            </div>
            <Field label={t.release.reason}>
              <Input value={reason} maxLength={1000} onChange={(event) => setReason(event.target.value)} />
            </Field>
            <span>
              <Button type="submit" size="sm" disabled={!context.editor || !newVersion.trim() || !reason.trim()}>
                {t.release.rollbackPlan}
              </Button>
            </span>
          </form>
        ) : null}
      </div>
    </article>
  )
}

function ChromeBody({ context }: { context: WorkspaceContext }) {
  const t = useChrome()
  const data = useQuery({
    queryKey: chromeKeys.item(context.organizationId, context.record.StoreAppId),
    queryFn: () => chromeApi.item(context.organizationId, context.record.StoreAppId),
  })
  if (data.isLoading) {
    return <CardSkeleton />
  }
  if (data.isError || !data.data) {
    return <ScreenState error={data.error} onRetry={() => void data.refetch()} />
  }
  return (
    <div className="grid gap-4">
      {!context.editor ? <Callout tone="info">{t.readOnly}</Callout> : null}
      <ItemCard context={context} data={data.data} />
      <SubmitCard key={(data.data.Packages ?? []).length} context={context} data={data.data} />
      <ReleaseCard context={context} data={data.data} />
      <HistoryCard data={data.data} />
    </div>
  )
}

/** The Chrome Web Store screen of an app: the app workspace on its Chrome record. */
export function ChromePage() {
  return (
    <AppWorkspace section="chrome" stores={[StoreKind.Chrome]}>
      {(context) => <ChromeBody context={context} />}
    </AppWorkspace>
  )
}
