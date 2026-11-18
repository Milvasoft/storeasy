import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, CircleCheck, Download, Send } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { Progress } from '@/components/ui/Progress'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, Chip, PageHeader, PageSkeleton } from '@/components/ui/primitives'
import { APP_NAMES_REQUEST, useActivity } from '@/lib/activity/ActivityContext'
import { appsApi, jobsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission, StepDirection, StoreJobStatus, StoreJobStepStatus, type StoreJobStepDto } from '@/lib/api/types'
import { recordFor } from '@/lib/apps/apps'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { isJobActive } from '@/lib/jobs/labels'
import { useCancelJob } from '@/lib/jobs/useCancelJob'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { mediaApi } from '@/features/common/api'
import type { ChangePlanDto } from '@/features/common/types'
import { studioApi } from './api'
import { useStudioT } from './i18n'
import { studioKeys } from './keys'
import { warningText } from './labels'
import type { MockupExportManifest, MockupExportSet } from './types'
import { useZipDownload } from './useZipDownload'

export const RENDER_STEP = 'render.size'

type PlanOutcome =
  | { kind: 'planned'; plan: ChangePlanDto }
  | { kind: 'failed'; message: string }
  | { kind: 'noRecord' }

function setKey(set: MockupExportSet): string {
  return `${set.ClassKey}|${set.Locale}`
}

/** Where a screenshot plan opens (the listing's plan detail, features/routes.tsx). */
export function planPath(appId: number, planId: number): string {
  return `/apps/${appId}/listing/plans/${planId}`
}

function ManifestView({ manifest, canEdit }: { manifest: MockupExportManifest; canEdit: boolean }) {
  const s = useStudioT()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const zip = useZipDownload()
  const [locale, setLocale] = useState<string | null>(null)
  const [outcomes, setOutcomes] = useState<Record<string, PlanOutcome>>({})
  const apps = useQuery({
    queryKey: queryKeys.apps.list(organization.Id, APP_NAMES_REQUEST),
    queryFn: () => appsApi.list(organization.Id, APP_NAMES_REQUEST),
    staleTime: 60_000,
  })
  const app = apps.data?.Data.find((item) => item.Id === manifest.AppId)

  const sets = manifest.Sets ?? []
  const locales = [...new Set(sets.map((set) => set.Locale))]
  const shown = locale ? sets.filter((set) => set.Locale === locale) : sets
  const uploadable = sets.filter((set) => !!set.StoreClassKey)
  const notUploadable = sets.filter((set) => !set.StoreClassKey)

  // One plan per set (docs/studio.md: the approval flow works per set); a failed set does not stop the others.
  const send = useMutation({
    mutationFn: async () => {
      const result: Record<string, PlanOutcome> = {}
      for (const set of uploadable) {
        const record = app ? recordFor(app, set.Store) : null
        if (!record) {
          result[setKey(set)] = { kind: 'noRecord' }
          continue
        }
        try {
          const plan = await mediaApi.planScreenshots(organization.Id, record.StoreAppId, {
            Locale: set.Locale,
            ClassKey: set.StoreClassKey ?? '',
            Order: [],
            Deletes: [],
            AssetIds: [...(set.Images ?? [])].sort((a, b) => a.SlotOrder - b.SlotOrder).map((image) => image.AssetId),
          })
          result[setKey(set)] = { kind: 'planned', plan }
        } catch (error) {
          result[setKey(set)] = { kind: 'failed', message: apiErrorMessage(error, s.common.error) }
        }
        setOutcomes({ ...result })
      }
      return result
    },
    onSuccess: (result) => setOutcomes(result),
  })
  const planned = Object.values(outcomes).filter((outcome) => outcome.kind === 'planned').length

  return (
    <div className="grid gap-4">
      <div className="card flex flex-wrap items-center gap-3 p-4">
        <CircleCheck className="h-5 w-5 text-success" aria-hidden />
        <div className="min-w-0">
          <h2 className="h3">{interpolate(s.job.ready, { count: manifest.ImageCount })}</h2>
          <p className="text-xs text-fg3">
            {format.bytes(manifest.TotalBytes)} · {format.dateTime(manifest.CreatedDate)} · {s.job.storeUntouched}
          </p>
        </div>
        <span className="ml-auto flex flex-wrap gap-2">
          <Button loading={zip.pending} onClick={() => zip.download(manifest.JobId)}>
            <Download aria-hidden />
            {s.job.zip}
          </Button>
          {canEdit ? (
            <Button variant="primary" loading={send.isPending} disabled={uploadable.length === 0 || send.isSuccess} onClick={() => send.mutate()}>
              <Send aria-hidden />
              {interpolate(s.job.sendToPlans, { count: uploadable.length })}
            </Button>
          ) : null}
        </span>
      </div>
      {send.isSuccess ? (
        <Callout tone={planned === uploadable.length ? 'success' : 'warning'} title={interpolate(s.job.plansCreated, { count: planned, total: uploadable.length })}>
          {s.job.plansHint}
        </Callout>
      ) : null}
      {(manifest.Failures ?? []).length > 0 ? (
        <Callout tone="danger" title={interpolate(s.job.failures, { count: manifest.Failures?.length ?? 0 })}>
          <ul className="grid gap-1">
            {(manifest.Failures ?? []).map((failure, index) => (
              <li key={index}>
                <span className="mono">{failure.ClassKey}</span> · {failure.Locale} · {interpolate(s.slots.slot, { order: failure.SlotOrder })}:{' '}
                {warningText(failure, s)}
              </li>
            ))}
          </ul>
        </Callout>
      ) : null}
      {(manifest.Warnings ?? []).length > 0 ? (
        <Callout tone="warning" title={interpolate(s.export.warnings, { count: manifest.Warnings?.length ?? 0 })}>
          <ul className="grid gap-1">
            {(manifest.Warnings ?? []).map((warning, index) => (
              <li key={index}>
                {warning.Locale} · {interpolate(s.slots.slot, { order: warning.SlotOrder })}: {warningText(warning, s)}
              </li>
            ))}
          </ul>
        </Callout>
      ) : null}
      {locales.length > 1 ? (
        <div className="toolbar" role="group" aria-label={s.export.locales}>
          <Chip selected={locale === null} onClick={() => setLocale(null)}>
            {interpolate(s.job.allSets, { count: sets.length })}
          </Chip>
          {locales.map((value) => (
            <Chip key={value} selected={locale === value} onClick={() => setLocale(value)}>
              {value}
            </Chip>
          ))}
        </div>
      ) : null}
      <div className="tbl-w">
        <table className="tbl">
          <caption className="sr-only">{s.job.sets}</caption>
          <thead>
            <tr>
              <th scope="col">{s.job.store}</th>
              <th scope="col">{s.job.class}</th>
              <th scope="col">{s.job.locale}</th>
              <th scope="col">{s.job.imageCount}</th>
              <th scope="col">{s.job.plan}</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((set) => {
              const outcome = outcomes[setKey(set)]
              let plan
              if (!set.StoreClassKey) {
                plan = <span className="text-xs text-fg3">{s.job.notUploadable}</span>
              } else if (!outcome) {
                plan = <span className="mono text-xs text-fg2">{set.StoreClassKey}</span>
              } else if (outcome.kind === 'planned') {
                plan = (
                  <Link className="lnk" to={planPath(manifest.AppId, outcome.plan.PlanId)}>
                    {interpolate(s.job.openPlan, { id: outcome.plan.PlanId })}
                  </Link>
                )
              } else if (outcome.kind === 'noRecord') {
                plan = <span className="err-t">{s.job.noRecord}</span>
              } else {
                plan = <span className="err-t">{outcome.message}</span>
              }
              return (
                <tr key={setKey(set)}>
                  <td>
                    <StoreTag store={set.Store} small codeOnly />
                  </td>
                  <td>
                    <span className="mono text-xs">{set.ClassKey}</span>
                    <span className="block text-xs text-fg3">
                      {set.Width}x{set.Height}
                    </span>
                  </td>
                  <td>{set.Locale}</td>
                  <td className="tabular-nums">{(set.Images ?? []).length}</td>
                  <td>{plan}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      {notUploadable.length > 0 ? (
        <p className="text-xs text-fg3">{interpolate(s.job.notUploadableNote, { count: notUploadable.length })}</p>
      ) : null}
    </div>
  )
}

function StepRow({ step }: { step: StoreJobStepDto }) {
  const s = useStudioT()
  const total = step.Total ?? 0
  const done = step.Progress ?? 0
  const label = step.Message || step.Name
  return (
    <li className="grid gap-1 border-b border-border py-2 last:border-b-0">
      <div className="flex gap-2 text-[13px]">
        <span className="min-w-0 flex-1 truncate">{label}</span>
        <span className="mono tabular-nums text-fg2">{total > 0 ? `${done} / ${total}` : s.job.stepStatus[step.Status] ?? ''}</span>
      </div>
      {total > 0 ? <Progress value={done} max={total} small label={label} tone={step.Status === StoreJobStepStatus.Failed ? 'dng' : undefined} /> : null}
    </li>
  )
}

/** Design 12.11 to 12.13: the export job while it runs (live hub), then its manifest, zip and upload plans. */
export function ExportJobPage() {
  const s = useStudioT()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const canEdit = can(OrgPermission.EditContent)
  const queryClient = useQueryClient()
  const params = useParams()
  const templateId = Number(params.templateId)
  const jobId = Number(params.jobId)
  const { state } = useActivity()
  const live = state.jobs[jobId]
  const cancel = useCancelJob()

  const detailKey = queryKeys.jobs.detail(organization.Id, jobId)
  const job = useQuery({ queryKey: detailKey, queryFn: () => jobsApi.detail(organization.Id, jobId) })
  const status = live?.status ?? job.data?.Status ?? null
  const succeeded = status === StoreJobStatus.Succeeded

  // Every live step event refreshes the step list (React Query merges the concurrent calls into one).
  const liveStamp = live ? `${live.updatedAt}|${live.stepOrder}|${live.progress}|${live.status}` : null
  useEffect(() => {
    if (liveStamp) {
      void queryClient.invalidateQueries({ queryKey: detailKey })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- the key is stable for this page
  }, [liveStamp, queryClient])

  const manifest = useQuery({
    queryKey: studioKeys.manifest(organization.Id, jobId),
    queryFn: () => studioApi.manifest(organization.Id, jobId),
    enabled: succeeded,
  })

  const steps = useMemo(() => {
    const list = [...(job.data?.Steps ?? [])]
    // The live step is newer than the last fetched list.
    if (live?.stepOrder && live.stepName) {
      const index = list.findIndex((step) => step.Order === live.stepOrder && step.Attempt === (live.attempt ?? step.Attempt))
      const merged: StoreJobStepDto = {
        Order: live.stepOrder,
        Attempt: live.attempt ?? 1,
        Name: live.stepName,
        Direction: live.direction ?? StepDirection.Internal,
        Status: live.stepStatus ?? StoreJobStepStatus.Running,
        Progress: live.progress,
        Total: live.total,
        Message: live.message,
        StartDate: live.updatedAt,
      }
      if (index >= 0) {
        list[index] = { ...list[index], ...merged, Message: list[index].Message ?? merged.Message }
      } else {
        list.push(merged)
      }
    }
    return list
  }, [job.data, live])
  const renderSteps = steps.filter((step) => step.Name === RENDER_STEP)
  const done = renderSteps.reduce((sum, step) => sum + (step.Progress ?? 0), 0)
  const total = renderSteps.reduce((sum, step) => sum + (step.Total ?? 0), 0)

  const back = (
    <Link to={`/studio/${templateId}/export`} className="lnk mb-1 inline-flex items-center gap-1 text-xs">
      <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
      {s.job.back}
    </Link>
  )

  if (job.isLoading) {
    return <PageSkeleton />
  }
  if (job.isError || !job.data) {
    return <ScreenState error={job.error} onRetry={() => void job.refetch()} />
  }

  const running = status !== null && isJobActive(status)
  let body
  if (succeeded) {
    if (manifest.isLoading) {
      body = <PageSkeleton />
    } else if (manifest.isError || !manifest.data) {
      body = <ScreenState error={manifest.error} onRetry={() => void manifest.refetch()} />
    } else {
      body = <ManifestView manifest={manifest.data} canEdit={canEdit} />
    }
  } else {
    body = (
      <div className="card grid gap-3 p-4">
        <div className="flex flex-wrap items-center gap-2">
          <b className="text-[13px]">{interpolate(s.job.jobLine, { id: jobId })}</b>
          <Badge tone={running ? 'info' : status === StoreJobStatus.Failed ? 'danger' : 'neutral'} small live={running}>
            {running ? s.job.running : status === StoreJobStatus.Failed ? s.job.failed : s.job.cancelled}
          </Badge>
          {running && canEdit ? (
            <Button size="sm" variant="ghost" className="ml-auto" loading={cancel.pending} onClick={() => void cancel.cancel(jobId)}>
              {s.job.cancel}
            </Button>
          ) : null}
        </div>
        {total > 0 ? (
          <>
            <Progress value={done} max={total} label={s.job.overall} />
            <span className="text-xs tabular-nums text-fg2">{interpolate(s.job.progress, { done, total })}</span>
          </>
        ) : null}
        {job.data.Error ? <Callout tone="danger">{job.data.Error}</Callout> : null}
        <ul aria-label={s.job.steps}>
          {steps.map((step) => (
            <StepRow key={`${step.Attempt}-${step.Order}`} step={step} />
          ))}
        </ul>
        {running ? <p className="text-xs text-fg3">{s.job.closeTab}</p> : null}
      </div>
    )
  }

  return (
    <>
      {back}
      <PageHeader title={succeeded ? s.job.resultTitle : s.job.runningTitle} subtitle={succeeded ? undefined : s.job.runningBody} />
      {body}
    </>
  )
}
