import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ExternalLink, ListChecks, Send } from 'lucide-react'
import { AssetUpload } from '@/components/ui/AssetUpload'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { CodeBlock } from '@/components/ui/Code'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, PageHeader, Textarea } from '@/components/ui/primitives'
import { AssetPurpose } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { assistedApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { answerKindLabel, assistedView, channelLabel } from '@/features/common/labels'
import {
  AssistedStepStatus,
  type AssistedStepDetailDto,
  type AssistedStepHandOffDto,
  type AssistedStepListDto,
} from '@/features/common/types'

// Design 6.16-6.18: work without an API stays in storeasy as a checklist; it can be handed to Claude in the browser
// (MCP) and is marked done with optional evidence.

function StatusBadge({ status }: { status: AssistedStepStatus }) {
  const t = useP2()
  const view = assistedView(status, t)
  return (
    <Badge tone={view.tone} dot small>
      {view.label}
    </Badge>
  )
}

function StepList({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const format = useFormat()
  const navigate = useNavigate()
  const { organizationId, app } = context
  const steps = useQuery({
    queryKey: contentKeys.assisted(organizationId, app.Id),
    queryFn: () => assistedApi.list(organizationId, { AppId: app.Id }),
  })
  const columns: Column<AssistedStepListDto>[] = [
    {
      key: 'step',
      header: t.assisted.step,
      role: 'main',
      cell: (step) => (
        <Link className="lnk font-semibold" to={`/apps/${app.Id}/assisted/${step.Id}`} onClick={(event) => event.stopPropagation()}>
          {step.Title ?? step.Key}
        </Link>
      ),
    },
    { key: 'store', header: t.workspace.store, cell: (step) => <StoreTag store={step.Store} small /> },
    { key: 'status', header: t.assisted.status, cell: (step) => <StatusBadge status={step.Status} /> },
    {
      key: 'last',
      header: t.assisted.lastDone,
      role: 'hidePhone',
      cell: (step) =>
        step.LastDoneDate ? (
          <span>
            {format.date(step.LastDoneDate)} · {step.LastDoneByUserName ?? '-'}
            {step.Channel ? ` · ${channelLabel(step.Channel, t)}` : ''}
          </span>
        ) : (
          '-'
        ),
    },
  ]
  return (
    <>
      <PageHeader title={t.assisted.title} subtitle={t.assisted.subtitle} />
      {steps.isLoading ? (
        <CardSkeleton />
      ) : steps.isError ? (
        <ScreenState error={steps.error} onRetry={() => void steps.refetch()} />
      ) : (steps.data ?? []).length === 0 ? (
        <EmptyState icon={<ListChecks aria-hidden />} title={t.assisted.emptyTitle} description={t.assisted.emptyBody} />
      ) : (
        <DataTable
          caption={t.assisted.title}
          columns={columns}
          rows={steps.data ?? []}
          rowKey={(step) => step.Id}
          onRowClick={(step) => navigate(`/apps/${app.Id}/assisted/${step.Id}`)}
        />
      )}
    </>
  )
}

export function AssistedStepsPage() {
  return (
    <AppWorkspace section="assisted" appWide>
      {(context) => <StepList context={context} />}
    </AppWorkspace>
  )
}

function CompleteDialog({
  step,
  doneItems,
  organizationId,
  onClose,
}: {
  step: AssistedStepDetailDto
  doneItems: number[]
  organizationId: number
  onClose: () => void
}) {
  const t = useP2()
  const queryClient = useQueryClient()
  const [evidence, setEvidence] = useState<number | null>(null)
  const [note, setNote] = useState('')
  const complete = useMutation({
    mutationFn: () =>
      assistedApi.complete(organizationId, step.Id, {
        Status: AssistedStepStatus.Done,
        DoneItems: doneItems,
        EvidenceAssetId: evidence,
        Note: note.trim() || null,
      }),
    onSuccess: () => {
      showToast(t.assisted.saved, 'success', step.Title ?? undefined)
      void queryClient.invalidateQueries({ queryKey: contentKeys.all })
      onClose()
    },
  })
  return (
    <Modal
      open
      onClose={onClose}
      title={t.assisted.completeTitle}
      description={t.assisted.completeBody}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.plan.close}
          </Button>
          <Button variant="primary" loading={complete.isPending} onClick={() => complete.mutate()}>
            {t.assisted.markDone}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <div className="grid gap-1">
          <span className="text-[13px] font-semibold">{t.assisted.evidence}</span>
          <AssetUpload
            purpose={AssetPurpose.Screenshot}
            accept={['.png', '.jpg', '.jpeg']}
            hint={t.assisted.evidenceHint}
            label={t.assisted.evidence}
            onUploaded={(asset) => setEvidence(asset.Id)}
          />
        </div>
        <Field label={t.assisted.note} optional>
          <Textarea rows={3} value={note} onChange={(event) => setNote(event.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

function StepDetail({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const format = useFormat()
  const queryClient = useQueryClient()
  const { organizationId, app, editor } = context
  const { stepId: rawId = '' } = useParams()
  const stepId = Number(rawId)
  const [done, setDone] = useState<number[] | null>(null)
  const [handOff, setHandOff] = useState<AssistedStepHandOffDto | null>(null)
  const [completing, setCompleting] = useState(false)
  const step = useQuery({
    queryKey: contentKeys.assistedStep(organizationId, stepId),
    queryFn: () => assistedApi.detail(organizationId, stepId),
    enabled: Number.isInteger(stepId) && stepId > 0,
  })
  const handOffMutation = useMutation({
    mutationFn: () => assistedApi.handOff(organizationId, stepId),
    onSuccess: (result) => {
      setHandOff(result)
      void queryClient.invalidateQueries({ queryKey: contentKeys.assistedStep(organizationId, stepId) })
    },
  })
  const skip = useMutation({
    mutationFn: () =>
      assistedApi.complete(organizationId, stepId, {
        Status: AssistedStepStatus.Skipped,
        DoneItems: done ?? [],
        EvidenceAssetId: null,
        Note: null,
      }),
    onSuccess: () => {
      showToast(t.assisted.skipped, 'success')
      void queryClient.invalidateQueries({ queryKey: contentKeys.all })
    },
  })
  const back = (
    <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to={`/apps/${app.Id}/assisted`}>
      <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
      {t.assisted.back}
    </Link>
  )
  if (step.isLoading) {
    return <CardSkeleton />
  }
  if (step.isError || !step.data) {
    return (
      <>
        {back}
        <ScreenState error={step.error} onRetry={() => void step.refetch()} />
      </>
    )
  }
  const data = step.data
  const items = [...(data.Steps ?? [])].sort((a, b) => a.Order - b.Order)
  const doneItems = done ?? items.filter((item) => item.Done).map((item) => item.Order)
  const prompt = handOff?.Prompt ?? null
  return (
    <>
      {back}
      <PageHeader
        title={data.Title ?? data.Key}
        badge={
          <span className="flex items-center gap-2">
            <StoreTag store={data.Store} small />
            <StatusBadge status={data.Status} />
          </span>
        }
        actions={
          data.StoreUrl ? (
            <a className="btn" href={data.StoreUrl} target="_blank" rel="noreferrer noopener">
              <ExternalLink aria-hidden />
              {t.assisted.openStore}
            </a>
          ) : undefined
        }
      />
      <div className="grid gap-4 lg:grid-cols-[1fr_320px]">
        <div className="grid content-start gap-4">
          <section className="card" aria-label={interpolate(t.assisted.checklist, { done: doneItems.length, total: items.length })}>
            <div className="card-h">
              <h2 className="h3">{interpolate(t.assisted.checklist, { done: doneItems.length, total: items.length })}</h2>
            </div>
            <ul className="card-b m-0 grid list-none gap-2">
              {items.map((item) => (
                <li key={item.Order}>
                  <Checkbox
                    checked={doneItems.includes(item.Order)}
                    disabled={!editor}
                    onChange={(checked) =>
                      setDone(checked ? [...doneItems, item.Order] : doneItems.filter((order) => order !== item.Order))
                    }
                  >
                    {item.Text ?? `#${item.Order}`}
                  </Checkbox>
                </li>
              ))}
            </ul>
          </section>
          {(data.Pitfalls ?? []).length > 0 ? (
            <Callout tone="warning" title={t.assisted.pitfalls}>
              <ul className="m-0 grid list-disc gap-1 pl-4">
                {(data.Pitfalls ?? []).map((pitfall) => (
                  <li key={pitfall}>{pitfall}</li>
                ))}
              </ul>
            </Callout>
          ) : null}
          {data.Note ? <p className="m-0 text-[13px] text-fg2">{interpolate(t.assisted.note2, { note: data.Note })}</p> : null}
        </div>
        <div className="grid content-start gap-4">
          <section className="card">
            <div className="card-h">
              <h2 className="h3">{t.assisted.handOff}</h2>
            </div>
            <div className="card-b grid gap-3 text-[13px]">
              <p className="m-0 text-fg2">{t.assisted.handOffBody}</p>
              {data.HandedOffDate ? (
                <p className="m-0 text-xs text-fg3">
                  {interpolate(t.assisted.handedOff, { name: data.HandedOffByUserName ?? '-', date: format.dateTime(data.HandedOffDate) })}
                </p>
              ) : null}
              {prompt ? (
                <>
                  {handOff?.McpTool ? (
                    <p className="m-0">
                      {t.assisted.tool}: <span className="mono">{handOff.McpTool}</span>
                    </p>
                  ) : null}
                  <CodeBlock label={t.assisted.prompt}>{prompt}</CodeBlock>
                </>
              ) : null}
              {editor ? (
                <Button variant="inverse" loading={handOffMutation.isPending} onClick={() => handOffMutation.mutate()}>
                  <Send aria-hidden />
                  {t.assisted.handOff}
                </Button>
              ) : null}
            </div>
          </section>
          {(data.AnswerKeys ?? []).length > 0 ? (
            <section className="card">
              <div className="card-h">
                <h2 className="h3">{t.assisted.answersUsed}</h2>
                <Link className="lnk ml-auto text-[13px]" to={`/apps/${app.Id}/answers`}>
                  {t.assisted.editAnswers}
                </Link>
              </div>
              <ul className="card-b m-0 grid list-none gap-1 text-[13px]">
                {(data.AnswerKeys ?? []).map((key) => (
                  <li key={key}>{answerKindLabel(key, t)}</li>
                ))}
              </ul>
            </section>
          ) : null}
          {editor ? (
            <div className="flex flex-wrap gap-2">
              <Button variant="primary" onClick={() => setCompleting(true)}>
                {t.assisted.markDone}
              </Button>
              <Button variant="ghost" loading={skip.isPending} onClick={() => skip.mutate()}>
                {t.assisted.skip}
              </Button>
            </div>
          ) : null}
        </div>
      </div>
      {completing ? (
        <CompleteDialog step={data} doneItems={doneItems} organizationId={organizationId} onClose={() => setCompleting(false)} />
      ) : null}
    </>
  )
}

export function AssistedStepPage() {
  return (
    <AppWorkspace section="assisted" appWide>
      {(context) => <StepDetail context={context} />}
    </AppWorkspace>
  )
}
