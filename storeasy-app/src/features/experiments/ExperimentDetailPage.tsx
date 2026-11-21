import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Download, ExternalLink, Pencil, Trash2 } from 'lucide-react'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Callout } from '@/components/ui/Feedback'
import { Timeline } from '@/components/ui/Progress'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, Field, Input, PageHeader, Textarea } from '@/components/ui/primitives'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { permissionLabel } from '@/features/common/labels'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'
import { apiErrorMessage } from '@/lib/api/http'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { experimentKeys, experimentsApi, useExperiment } from './api'
import { ExperimentStatusChip } from './ExperimentsPage'
import { useExperimentsText } from './i18n'
import { kindLabel, operationHint, operationLabel, timelineTone } from './labels'
import { isAssistedKind, isEditable, operationPermission, textsCsv } from './rules'
import { AsoExperimentOperation, AsoExperimentStatus, type AsoExperimentDto } from './types'
import { AssetThumb } from './VariantMedia'

// One experiment: its state and links, the variants side by side, the timeline, the store actions (each a change plan,
// approved through the plans flow with its own permission and typed confirmation), and for Play the assisted checklist,
// the files to put into Play Console and the result entry.

function useSaved(organizationId: number) {
  const queryClient = useQueryClient()
  return (saved: AsoExperimentDto) => {
    queryClient.setQueryData(experimentKeys.detail(organizationId, saved.Id), saved)
    void queryClient.invalidateQueries({ queryKey: experimentKeys.all })
  }
}

function Variants({ experiment, organizationId }: { experiment: AsoExperimentDto; organizationId: number }) {
  const t = useExperimentsText()
  return (
    <section aria-label={t.detail.variants} className="grid gap-2">
      <h2 className="m-0 text-[15px] font-semibold">{t.detail.variants}</h2>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {experiment.Variants.map((variant) => (
          <article
            key={variant.Key}
            className="card grid content-start gap-2 p-3"
            aria-label={interpolate(t.variantLabel, { key: variant.Key })}
          >
            <div className="flex items-center gap-2">
              <b className="flex-1">{`${variant.Key}: ${variant.Name}`}</b>
              {experiment.WinnerVariantKey === variant.Key ? <span className="bdg ok sm">{t.column.winner}</span> : null}
            </div>
            {variant.AppIconName ? <span className="mono text-xs">{`${t.wizard.appIconName}: ${variant.AppIconName}`}</span> : null}
            {variant.Locales.map((locale) => (
              <div key={locale.Locale} className="grid gap-1">
                <span className="mono text-xs text-fg3">{locale.Locale}</span>
                {locale.PromotionalText ? <p className="m-0 text-xs">{locale.PromotionalText}</p> : null}
                {locale.ShortDescription ? <p className="m-0 text-xs">{locale.ShortDescription}</p> : null}
                {locale.FullDescription ? <p className="m-0 line-clamp-3 text-xs text-fg2">{locale.FullDescription}</p> : null}
                <div className="flex flex-wrap gap-1">
                  {locale.Media.flatMap((media) => media.AssetIds)
                    .slice(0, 4)
                    .map((assetId) => (
                      <AssetThumb
                        key={assetId}
                        organizationId={organizationId}
                        assetId={assetId}
                        label={`${variant.Key} ${locale.Locale} #${assetId}`}
                      />
                    ))}
                </div>
              </div>
            ))}
          </article>
        ))}
      </div>
    </section>
  )
}

function Actions({ experiment, context }: { experiment: AsoExperimentDto; context: WorkspaceContext }) {
  const t = useExperimentsText()
  const p2 = useP2()
  const base = useT()
  const { organizationId, can } = context
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const [withVersion, setWithVersion] = useState(false)
  const queryClient = useQueryClient()
  const makePlan = useMutation({
    mutationFn: (operation: AsoExperimentOperation) =>
      experimentsApi.plan(organizationId, {
        Id: experiment.Id,
        Operation: operation,
        WithVersion: operation === AsoExperimentOperation.Submit ? withVersion : undefined,
        WinnerVariantKey: operation === AsoExperimentOperation.ApplyWinner ? experiment.WinnerVariantKey : undefined,
      }),
    onSuccess: setPlan,
    onError: (error) => showToast(apiErrorMessage(error, base.common.error), 'error'),
  })
  const operations = experiment.Operations ?? []
  return (
    <section aria-label={t.detail.actions} className="card grid gap-3 p-4">
      <div>
        <h2 className="m-0 text-[15px] font-semibold">{t.detail.actions}</h2>
        <p className="m-0 mt-1 text-xs text-fg3">{t.detail.actionsHint}</p>
      </div>
      {operations.length === 0 ? <p className="m-0 text-[13px] text-fg2">{t.detail.noActions}</p> : null}
      {operations.includes(AsoExperimentOperation.Submit) ? (
        <Checkbox checked={withVersion} onChange={setWithVersion}>
          {t.detail.withVersion}
        </Checkbox>
      ) : null}
      <ul className="m-0 grid list-none gap-3 p-0">
        {operations.map((operation) => {
          const permission = operationPermission(operation)
          const allowed = can(permission)
          const needsWinner = operation === AsoExperimentOperation.ApplyWinner && !experiment.WinnerVariantKey
          return (
            <li key={operation} className="flex flex-wrap items-center gap-3">
              <Button
                variant={operation === AsoExperimentOperation.Stop ? 'danger-outline' : 'outline'}
                disabled={!allowed || needsWinner || makePlan.isPending}
                loading={makePlan.isPending && makePlan.variables === operation}
                onClick={() => makePlan.mutate(operation)}
              >
                {operationLabel(operation, t)}
              </Button>
              <span className="text-xs text-fg3">
                {needsWinner
                  ? t.detail.winnerFirst
                  : allowed
                    ? operationHint(operation, t)
                    : interpolate(t.detail.needs, { permission: permissionLabel(permission, p2) })}
              </span>
            </li>
          )
        })}
      </ul>
      <PlanDialog
        plan={plan}
        onClose={() => setPlan(null)}
        onDecided={() => void queryClient.invalidateQueries({ queryKey: experimentKeys.all })}
      />
    </section>
  )
}

function AssistedChecklist({ experiment, context }: { experiment: AsoExperimentDto; context: WorkspaceContext }) {
  const t = useExperimentsText()
  const base = useT()
  const saved = useSaved(context.organizationId)
  const assisted = experiment.Assisted
  const save = useMutation({
    mutationFn: (done: number[]) => experimentsApi.checklist(context.organizationId, experiment.Id, done),
    onSuccess: (next) => {
      saved(next)
      showToast(t.assisted.saved, 'success')
    },
    onError: (error) => showToast(apiErrorMessage(error, base.common.error), 'error'),
  })
  if (!assisted) {
    return null
  }
  const done = experiment.ChecklistDone ?? []
  return (
    <section aria-label={t.assisted.title} className="card grid gap-3 p-4">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="m-0 flex-1 text-[15px] font-semibold">{t.assisted.title}</h2>
        <a className="btn pri" href={assisted.Url} target="_blank" rel="noreferrer noopener">
          <ExternalLink className="h-4 w-4" aria-hidden />
          {t.assisted.open}
        </a>
      </div>
      <p className="m-0 text-xs text-fg3">{t.assisted.hint}</p>
      <ol className="m-0 grid list-none gap-2 p-0">
        {assisted.Steps.map((step, index) => (
          <li key={index}>
            <Checkbox
              checked={done.includes(index)}
              disabled={!context.editor || save.isPending}
              onChange={(checked) => save.mutate(checked ? [...done, index].sort((a, b) => a - b) : done.filter((item) => item !== index))}
            >
              {step}
            </Checkbox>
          </li>
        ))}
      </ol>
      <span className="text-xs text-fg3">{interpolate(t.assisted.progress, { done: done.length, total: assisted.Steps.length })}</span>
      {assisted.Pitfalls.length > 0 ? (
        <Callout tone="warning" title={t.assisted.pitfalls}>
          <ul className="m-0 pl-4">
            {assisted.Pitfalls.map((pitfall, index) => (
              <li key={index}>{pitfall}</li>
            ))}
          </ul>
        </Callout>
      ) : null}
    </section>
  )
}

function downloadText(fileName: string, text: string) {
  const blob = new Blob([text], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}

function ExportPanel({ experiment, organizationId }: { experiment: AsoExperimentDto; organizationId: number }) {
  const t = useExperimentsText()
  const [open, setOpen] = useState(false)
  const files = useQuery({
    queryKey: experimentKeys.exportFiles(organizationId, experiment.Id),
    queryFn: () => experimentsApi.exportFiles(organizationId, experiment.Id),
    enabled: open,
  })
  return (
    <section aria-label={t.exportPanel.title} className="card grid gap-3 p-4">
      <h2 className="m-0 text-[15px] font-semibold">{t.exportPanel.title}</h2>
      <p className="m-0 text-xs text-fg3">{t.exportPanel.hint}</p>
      {!open ? (
        <div>
          <Button variant="outline" onClick={() => setOpen(true)}>
            {t.exportPanel.prepare}
          </Button>
        </div>
      ) : files.isLoading ? (
        <CardSkeleton count={1} />
      ) : files.isError || !files.data ? (
        <ScreenState error={files.error} onRetry={() => void files.refetch()} />
      ) : (
        <>
          <div>
            <Button
              variant="outline"
              disabled={files.data.Texts.length === 0}
              onClick={() => downloadText(`experiment-${experiment.Id}-texts.csv`, textsCsv(files.data.Texts))}
            >
              <Download className="h-4 w-4" aria-hidden />
              {t.exportPanel.texts}
            </Button>
          </div>
          {files.data.Texts.length === 0 ? <p className="m-0 text-xs text-fg3">{t.exportPanel.noTexts}</p> : null}
          {files.data.Files.length === 0 ? (
            <p className="m-0 text-xs text-fg3">{t.exportPanel.noFiles}</p>
          ) : (
            <ul className="m-0 grid list-none gap-1 p-0">
              {files.data.Files.map((file) => (
                <li key={`${file.VariantKey}-${file.Locale}-${file.ClassKey}-${file.AssetId}`}>
                  <a className="lnk text-[13px]" href={file.Url} target="_blank" rel="noreferrer noopener" download={file.FileName}>
                    {interpolate(t.exportPanel.file, {
                      variant: file.VariantKey,
                      locale: file.Locale,
                      class: file.ClassKey,
                      file: file.FileName,
                    })}
                  </a>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}

function dateInput(value: string | null): string {
  return value ? value.slice(0, 10) : ''
}

function ResultForm({ experiment, context }: { experiment: AsoExperimentDto; context: WorkspaceContext }) {
  const t = useExperimentsText()
  const base = useT()
  const saved = useSaved(context.organizationId)
  const assisted = isAssistedKind(experiment.Kind)
  const [status, setStatus] = useState<AsoExperimentStatus | null>(
    assisted &&
      [AsoExperimentStatus.Running, AsoExperimentStatus.Stopped, AsoExperimentStatus.Completed].includes(experiment.Status as 5 | 7 | 8)
      ? experiment.Status
      : null,
  )
  const [start, setStart] = useState(dateInput(experiment.StartDate))
  const [end, setEnd] = useState(dateInput(experiment.EndDate))
  const [winner, setWinner] = useState<string>(experiment.WinnerVariantKey ?? '')
  const [summary, setSummary] = useState(experiment.ResultSummary ?? '')
  const [url, setUrl] = useState(experiment.ResultUrl ?? '')
  const endError = assisted && start && end && end < start ? t.result.endBeforeStart : null
  const save = useMutation({
    mutationFn: () =>
      experimentsApi.recordResult(context.organizationId, {
        Id: experiment.Id,
        Status: assisted ? status : null,
        StartDate: assisted && start ? `${start}T00:00:00Z` : null,
        EndDate: assisted && end ? `${end}T00:00:00Z` : null,
        WinnerVariantKey: winner || null,
        ResultSummary: summary.trim() || null,
        ResultUrl: url.trim() || null,
      }),
    onSuccess: (next) => {
      saved(next)
      showToast(t.result.saved, 'success')
    },
    onError: (error) => showToast(apiErrorMessage(error, base.common.error), 'error'),
  })
  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!endError) {
      save.mutate()
    }
  }
  const disabled = !context.editor
  return (
    <form aria-label={t.result.title} className="card grid gap-3 p-4" noValidate onSubmit={submit}>
      <h2 className="m-0 text-[15px] font-semibold">{t.result.title}</h2>
      <p className="m-0 text-xs text-fg3">{t.result.hint}</p>
      {assisted ? (
        <>
          <Field label={t.result.status}>
            <Select<AsoExperimentStatus>
              value={status}
              disabled={disabled}
              options={[AsoExperimentStatus.Running, AsoExperimentStatus.Stopped, AsoExperimentStatus.Completed].map((value) => ({
                value,
                label:
                  value === AsoExperimentStatus.Running
                    ? t.status.running
                    : value === AsoExperimentStatus.Stopped
                      ? t.status.stopped
                      : t.status.completed,
              }))}
              onChange={setStatus}
            />
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t.result.startDate} optional>
              <Input type="date" value={start} disabled={disabled} onChange={(event) => setStart(event.target.value)} />
            </Field>
            <Field label={t.result.endDate} optional error={endError}>
              <Input type="date" value={end} disabled={disabled} onChange={(event) => setEnd(event.target.value)} />
            </Field>
          </div>
        </>
      ) : null}
      <Field label={t.result.winner}>
        <Select<string>
          value={winner}
          disabled={disabled}
          options={[
            { value: '', label: t.result.noWinner },
            ...experiment.Variants.map((variant) => ({ value: variant.Key, label: `${variant.Key}: ${variant.Name}` })),
          ]}
          onChange={setWinner}
        />
      </Field>
      <Field label={t.result.summary} optional>
        <Textarea
          rows={3}
          maxLength={2000}
          value={summary}
          disabled={disabled}
          placeholder={t.result.summaryPlaceholder}
          onChange={(event) => setSummary(event.target.value)}
        />
      </Field>
      <Field label={t.result.url} optional>
        <Input type="url" value={url} disabled={disabled} onChange={(event) => setUrl(event.target.value)} />
      </Field>
      {!disabled ? (
        <div>
          <Button type="submit" variant="primary" loading={save.isPending}>
            {t.result.save}
          </Button>
        </div>
      ) : null}
    </form>
  )
}

function Detail({ context, id }: { context: WorkspaceContext; id: number }) {
  const t = useExperimentsText()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { app, organizationId, editor, storeQuery } = context
  const experiment = useExperiment(organizationId, id)
  const remove = useMutation({
    mutationFn: () => experimentsApi.remove(organizationId, id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: experimentKeys.all })
      showToast(t.detail.removed, 'success')
      navigate(`/apps/${app.Id}/experiments${storeQuery}`)
    },
    onError: (error) => showToast(apiErrorMessage(error, base.common.error), 'error'),
  })

  if (experiment.isLoading) {
    return <CardSkeleton />
  }
  if (experiment.isError || !experiment.data) {
    return <ScreenState error={experiment.error} onRetry={() => void experiment.refetch()} />
  }
  const data = experiment.data
  const assisted = isAssistedKind(data.Kind)
  const removable = editor && data.Status === AsoExperimentStatus.Draft && (!data.ExternalId || assisted)
  const askRemove = async () => {
    if (
      await confirm({
        title: t.detail.remove,
        message: interpolate(t.detail.removeConfirm, { name: data.Name }),
        danger: true,
        confirmLabel: t.detail.remove,
      })
    ) {
      remove.mutate()
    }
  }
  const timeline = [...(data.Timeline ?? [])].sort((a, b) => b.Date.localeCompare(a.Date))

  return (
    <div className="grid gap-4">
      <Link className="lnk inline-flex items-center gap-1 text-[13px]" to={`/apps/${app.Id}/experiments${storeQuery}`}>
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
        {t.detail.back}
      </Link>
      <PageHeader
        title={data.Name}
        badge={<ExperimentStatusChip status={data.Status} />}
        subtitle={
          <>
            {kindLabel(data.Kind, t)}
            {data.TrafficProportion != null ? `, ${interpolate(t.trafficValue, { value: data.TrafficProportion })}` : null}
            {data.StoreState ? `, ${interpolate(t.detail.storeState, { state: data.StoreState })}` : null}
          </>
        }
        actions={
          <>
            {editor && isEditable(data.Status) ? (
              <Link className="btn" to={`/apps/${app.Id}/experiments/${data.Id}/edit${storeQuery}`}>
                <Pencil className="h-4 w-4" aria-hidden />
                {t.detail.edit}
              </Link>
            ) : null}
            {removable ? (
              <Button variant="danger-outline" loading={remove.isPending} onClick={() => void askRemove()}>
                <Trash2 className="h-4 w-4" aria-hidden />
                {t.detail.remove}
              </Button>
            ) : null}
          </>
        }
      />
      {data.ReviewRequired ? <Callout tone="info">{t.detail.reviewRequired}</Callout> : null}
      {data.Url || data.ConsoleUrl || data.ResultUrl ? (
        <nav aria-label={t.detail.links} className="flex flex-wrap gap-3 text-[13px]">
          {data.Url ? (
            <a className="lnk inline-flex items-center gap-1" href={data.Url} target="_blank" rel="noreferrer noopener">
              <ExternalLink className="h-3.5 w-3.5" aria-hidden />
              {t.detail.pageUrl}
            </a>
          ) : null}
          {data.ConsoleUrl ? (
            <a className="lnk inline-flex items-center gap-1" href={data.ConsoleUrl} target="_blank" rel="noreferrer noopener">
              <ExternalLink className="h-3.5 w-3.5" aria-hidden />
              {t.detail.console}
            </a>
          ) : null}
          {data.ResultUrl ? (
            <a className="lnk inline-flex items-center gap-1" href={data.ResultUrl} target="_blank" rel="noreferrer noopener">
              <ExternalLink className="h-3.5 w-3.5" aria-hidden />
              {t.detail.resultLink}
            </a>
          ) : null}
        </nav>
      ) : null}
      {data.Url ? <code className="mono break-all text-xs text-fg2">{data.Url}</code> : null}

      <Variants experiment={data} organizationId={organizationId} />

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="grid content-start gap-4">
          {assisted ? (
            <>
              <AssistedChecklist experiment={data} context={context} />
              <ExportPanel experiment={data} organizationId={organizationId} />
            </>
          ) : (
            <Actions experiment={data} context={context} />
          )}
          <ResultForm key={`${data.Id}-${data.WinnerVariantKey}-${data.ResultSummary}`} experiment={data} context={context} />
        </div>
        <section aria-label={t.detail.timeline} className="card grid content-start gap-3 p-4">
          <h2 className="m-0 text-[15px] font-semibold">{t.detail.timeline}</h2>
          {timeline.length === 0 ? (
            <p className="m-0 text-[13px] text-fg3">{t.detail.noTimeline}</p>
          ) : (
            <Timeline
              items={timeline.map((entry, index) => ({
                id: `${entry.Kind}-${entry.PlanId ?? index}-${entry.Date}`,
                tone: timelineTone(entry),
                title: entry.PlanId ? (
                  <Link className="lnk" to={`/activity/plans/${entry.PlanId}`}>
                    {`${t.detail.timelineKind[entry.Kind]}: ${entry.Title}`}
                  </Link>
                ) : (
                  `${t.detail.timelineKind[entry.Kind]}: ${entry.Title}`
                ),
                meta: format.dateTime(entry.Date),
              }))}
            />
          )}
          <div className="grid gap-1 text-xs text-fg3">
            {data.CreatedByUserName ? <span>{interpolate(t.detail.createdBy, { user: data.CreatedByUserName })}</span> : null}
            {data.SyncedDate ? <span>{interpolate(t.detail.syncedDate, { date: format.dateTime(data.SyncedDate) })}</span> : null}
          </div>
        </section>
      </div>
    </div>
  )
}

export function ExperimentDetailPage() {
  const { experimentId } = useParams()
  const id = Number(experimentId ?? 0)
  return <AppWorkspace section="experiments">{(context) => <Detail context={context} id={id} />}</AppWorkspace>
}
