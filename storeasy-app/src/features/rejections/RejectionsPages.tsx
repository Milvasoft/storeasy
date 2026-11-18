import { useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ClipboardCopy, ExternalLink, FileWarning, Plus, Send } from 'lucide-react'
import { Badge, StoreTag, type BadgeTone } from '@/components/ui/Badge'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Timeline } from '@/components/ui/Progress'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Chip, EmptyState, Field, Input, PageHeader, Textarea } from '@/components/ui/primitives'
import { RecordEventType } from '@/lib/activity/events'
import { SortType, type ListRequest } from '@/lib/api/list'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { recordsOf } from '@/lib/apps/apps'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { channelLabel } from '@/features/common/labels'
import { storeSlug } from '@/features/common/store'
import { useLiveRefresh } from '@/features/notifications/live'
import { rejectionKeys, rejectionsApi } from './api'
import { useRejectionsText, type RejectionsDictionary } from './i18n'
import {
  ChecklistItemStatus,
  REJECTION_REPLY_MAX,
  RejectionCloseReason,
  RejectionKind,
  RejectionSource,
  RejectionState,
  type RejectionCaseDto,
  type RejectionCaseListDto,
  type RejectionChecklistItem,
} from './types'

// Design 9.11-9.18: a rejection opens a case. The API cannot read or write Resolution Center, so the text is pasted, its
// guideline numbers become a checklist, the reply is drafted against the 4,000 character limit and sent by hand (or by
// the assisted step), then marked sent. A successful resubmission closes the case.

const LIVE_TYPES = [RecordEventType.RejectionOpened]
const LIST_REQUEST: ListRequest = { PageNumber: 1, RowCount: 50, Sorting: { SortBy: 'OpenedDate', Type: SortType.Desc } }

/** Resolution Center counts a line break as one character (RejectionCases.ReplyLength). */
export function replyLength(text: string): number {
  return text.replace(/\r\n/g, '\n').length
}

function kindLabel(kind: RejectionKind, t: RejectionsDictionary): string {
  switch (kind) {
    case RejectionKind.MetadataRejected:
      return t.kind.metadata
    case RejectionKind.CertificationFailed:
      return t.kind.certification
    default:
      return t.kind.rejected
  }
}

function sourceLabel(source: RejectionSource, t: RejectionsDictionary): string {
  switch (source) {
    case RejectionSource.Webhook:
      return t.source.webhook
    case RejectionSource.Sync:
      return t.source.sync
    default:
      return t.source.manual
  }
}

function statusView(status: ChecklistItemStatus, t: RejectionsDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case ChecklistItemStatus.Done:
      return { label: t.checklist.done, tone: 'success' }
    case ChecklistItemStatus.InProgress:
      return { label: t.checklist.inProgress, tone: 'info' }
    default:
      return { label: t.checklist.todo, tone: 'neutral' }
  }
}

function StateBadge({ state }: { state: RejectionState }) {
  const t = useRejectionsText()
  return state === RejectionState.Open ? (
    <Badge tone="danger" dot small>
      {t.state.open}
    </Badge>
  ) : (
    <Badge tone="success" dot small>
      {t.state.closed}
    </Badge>
  )
}

function versionLabel(item: { VersionString?: string; BuildNumber?: string }): string {
  return item.BuildNumber ? `${item.VersionString ?? '-'} (${item.BuildNumber})` : (item.VersionString ?? '-')
}

function guidelineList(item: { Guidelines?: Array<{ Code?: string }> }): string {
  return (
    (item.Guidelines ?? [])
      .map((guideline) => guideline.Code)
      .filter(Boolean)
      .join(' · ') || '-'
  )
}

async function copyText(value: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(value)
    return true
  } catch {
    return false
  }
}

// ---- Case list and history --------------------------------------------------------------------------------------------

function OpenCaseDialog({ context, onClose }: { context: WorkspaceContext; onClose: () => void }) {
  const t = useRejectionsText()
  const base = useT()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const stores = recordsOf(context.app).map((record) => record.Store)
  const [store, setStore] = useState<StoreKind>(stores.includes(StoreKind.Apple) ? StoreKind.Apple : (stores[0] ?? StoreKind.Apple))
  const [version, setVersion] = useState('')
  const [build, setBuild] = useState('')
  const [kind, setKind] = useState<RejectionKind>(RejectionKind.Rejected)
  const open = useMutation({
    mutationFn: () =>
      rejectionsApi.open(context.organizationId, {
        AppId: context.app.Id,
        Store: store,
        VersionString: version.trim(),
        BuildNumber: build.trim() || null,
        Kind: kind,
      }),
    onSuccess: (caseId) => {
      void queryClient.invalidateQueries({ queryKey: rejectionKeys.all })
      onClose()
      navigate(`/apps/${context.app.Id}/rejections/${caseId}`)
    },
  })
  return (
    <Modal
      open
      onClose={onClose}
      title={t.open.title}
      description={t.open.body}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={open.isPending} disabled={!version.trim()} onClick={() => open.mutate()}>
            {t.open.submit}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <Field label={t.open.store}>
          <Select
            value={store}
            options={stores.map((value) => ({ value, label: storeLabel(value, base) }))}
            onChange={(value) => setStore(value)}
          />
        </Field>
        <Field label={t.open.version}>
          <Input value={version} onChange={(event) => setVersion(event.target.value)} />
        </Field>
        <Field label={t.open.build} optional>
          <Input value={build} onChange={(event) => setBuild(event.target.value)} />
        </Field>
        <Field label={t.open.kind}>
          <Select
            value={kind}
            options={[RejectionKind.Rejected, RejectionKind.MetadataRejected, RejectionKind.CertificationFailed].map((value) => ({
              value,
              label: kindLabel(value, t),
            }))}
            onChange={(value) => setKind(value)}
          />
        </Field>
      </div>
    </Modal>
  )
}

function storeLabel(store: StoreKind, base: ReturnType<typeof useT>): string {
  switch (store) {
    case StoreKind.Apple:
      return base.enums.store.apple
    case StoreKind.Google:
      return base.enums.store.google
    default:
      return base.enums.store.microsoft
  }
}

function History({ context }: { context: WorkspaceContext }) {
  const t = useRejectionsText()
  const format = useFormat()
  const history = useQuery({
    queryKey: rejectionKeys.history(context.organizationId, context.app.Id),
    queryFn: () => rejectionsApi.history(context.organizationId, context.app.Id),
  })
  if (!history.data) {
    return null
  }
  const data = history.data
  const repeated = data.RepeatedGuidelines ?? []
  return (
    <section className="card mt-4 grid gap-3 p-4" aria-label={t.history.title}>
      <h2 className="text-[15px] font-semibold">{t.history.title}</h2>
      <ol className="flex flex-wrap gap-2">
        {(data.Submissions ?? []).map((submission, index) => {
          const rejected = (submission.Rejections ?? []).length > 0
          return (
            <li key={`${submission.StoreAppId}-${submission.VersionString}-${submission.BuildNumber}-${index}`} className="grid gap-0.5">
              <Badge tone={rejected ? 'danger' : 'success'} small>
                {versionLabel(submission)}
              </Badge>
              <span className="text-xs text-fg3">
                {rejected ? (submission.Rejections ?? []).map(guidelineList).join(' · ') : (submission.State ?? t.history.accepted)}
                {submission.SubmittedDate ? ` · ${format.date(submission.SubmittedDate)}` : ''}
              </span>
            </li>
          )
        })}
      </ol>
      <p className="text-[13px] text-fg2">
        {interpolate(t.history.summary, { submissions: data.SubmissionCount, rejections: data.RejectionCount })}{' '}
        {repeated.length > 0 ? interpolate(t.history.repeated, { codes: repeated.join(', ') }) : t.history.noRepeat}
      </p>
    </section>
  )
}

function CaseList({ context }: { context: WorkspaceContext }) {
  const t = useRejectionsText()
  const format = useFormat()
  const navigate = useNavigate()
  const [state, setState] = useState<RejectionState | null>(RejectionState.Open)
  const [opening, setOpening] = useState(false)
  useLiveRefresh(LIVE_TYPES, rejectionKeys.all)
  const cases = useQuery({
    queryKey: rejectionKeys.list(context.organizationId, context.app.Id, state, LIST_REQUEST),
    queryFn: () => rejectionsApi.list(context.organizationId, { AppId: context.app.Id, State: state }, LIST_REQUEST),
  })
  const rows = cases.data?.Data ?? []
  const caseLink = (item: RejectionCaseListDto) => `/apps/${context.app.Id}/rejections/${item.Id}`
  const columns: Column<RejectionCaseListDto>[] = [
    {
      key: 'version',
      header: t.list.version,
      role: 'main',
      cell: (item) => (
        <Link className="lnk font-semibold" to={caseLink(item)} onClick={(event) => event.stopPropagation()}>
          {versionLabel(item)}
        </Link>
      ),
    },
    { key: 'store', header: t.list.store, cell: (item) => <StoreTag store={item.Store} small codeOnly /> },
    { key: 'kind', header: t.list.kind, cell: (item) => <Badge small>{kindLabel(item.Kind, t)}</Badge> },
    { key: 'guidelines', header: t.list.guidelines, cell: (item) => <span className="mono">{guidelineList(item)}</span> },
    { key: 'state', header: t.list.state, cell: (item) => <StateBadge state={item.State} /> },
    {
      key: 'opened',
      header: t.list.opened,
      role: 'hidePhone',
      cell: (item) => `${format.date(item.OpenedDate)} · ${sourceLabel(item.OpenedVia, t)}`,
    },
    {
      key: 'checklist',
      header: t.list.checklist,
      cell: (item) => <span className="tabular-nums">{`${item.ChecklistDone} / ${item.ChecklistTotal}`}</span>,
    },
  ]
  return (
    <>
      <PageHeader
        title={t.title}
        subtitle={t.subtitle}
        actions={
          context.can(OrgPermission.EditContent) ? (
            <Button onClick={() => setOpening(true)}>
              <Plus aria-hidden />
              {t.open.action}
            </Button>
          ) : null
        }
      />
      <div className="toolbar mb-3" role="group" aria-label={t.list.state}>
        <Chip selected={state === RejectionState.Open} onClick={() => setState(RejectionState.Open)}>
          {t.list.openOnly}
        </Chip>
        <Chip selected={state === null} onClick={() => setState(null)}>
          {t.list.all}
        </Chip>
      </div>
      {cases.isLoading ? (
        <CardSkeleton />
      ) : cases.isError ? (
        <ScreenState error={cases.error} onRetry={() => void cases.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={<FileWarning aria-hidden />} title={t.list.emptyTitle} description={t.list.emptyBody} />
      ) : (
        <DataTable
          caption={t.title}
          columns={columns}
          rows={rows}
          rowKey={(item) => item.Id}
          onRowClick={(item) => navigate(caseLink(item))}
        />
      )}
      <History context={context} />
      {opening ? <OpenCaseDialog context={context} onClose={() => setOpening(false)} /> : null}
    </>
  )
}

/** /apps/:appId/rejections. */
export function RejectionsPage() {
  return (
    <AppWorkspace section="rejections" appWide>
      {(context) => <CaseList context={context} />}
    </AppWorkspace>
  )
}

// ---- Case detail ------------------------------------------------------------------------------------------------------

function Step({ number, title, children, muted }: { number: number; title: string; children: ReactNode; muted?: boolean }) {
  return (
    <section className={muted ? 'card grid gap-3 p-4 opacity-60' : 'card grid gap-3 p-4'} aria-label={title}>
      <h2 className="flex items-center gap-2 text-[15px] font-semibold">
        <span className="grid h-6 w-6 place-items-center rounded-full bg-bg2 text-xs" aria-hidden>
          {number}
        </span>
        {title}
      </h2>
      {children}
    </section>
  )
}

function TextStep({ item, editable }: { item: RejectionCaseDto; editable: boolean }) {
  const t = useRejectionsText()
  const format = useFormat()
  const queryClient = useQueryClient()
  const organizationId = useOrganizationId()
  const [editing, setEditing] = useState(!item.RawText)
  const [text, setText] = useState(item.RawText ?? '')
  const save = useMutation({
    mutationFn: () => rejectionsApi.updateText(organizationId, item.Id, text),
    onSuccess: (updated) => {
      setEditing(false)
      showToast((updated.Guidelines ?? []).length > 0 ? t.text.parsed : t.text.savedNoGuideline, 'success')
      void queryClient.invalidateQueries({ queryKey: rejectionKeys.all })
    },
  })
  const missingGuidelines = !!item.RawText && (item.Guidelines ?? []).length === 0

  if (!editable && !item.RawText) {
    return (
      <Step number={1} title={t.text.title}>
        <Callout tone="info">{t.text.waiting}</Callout>
      </Step>
    )
  }
  return (
    <Step number={1} title={t.text.title}>
      {editing && editable ? (
        <>
          <Field label={t.text.paste} hint={t.text.pasteHint}>
            <Textarea rows={8} value={text} onChange={(event) => setText(event.target.value)} />
          </Field>
          <div className="flex flex-wrap justify-end gap-2">
            {item.RawText ? (
              <Button variant="ghost" onClick={() => setEditing(false)}>
                {t.text.cancel}
              </Button>
            ) : null}
            <Button variant="primary" loading={save.isPending} disabled={!text.trim()} onClick={() => save.mutate()}>
              {t.text.save}
            </Button>
          </div>
        </>
      ) : (
        <>
          <div className="flex flex-wrap items-center gap-2 text-xs text-fg3">
            <span className="mr-auto">
              {interpolate(t.text.by, {
                name: item.RawTextByUserName ?? '-',
                date: item.RawTextDate ? format.dateTime(item.RawTextDate) : '-',
              })}
            </span>
            {editable ? (
              <Button size="sm" onClick={() => setEditing(true)}>
                {t.text.edit}
              </Button>
            ) : null}
          </div>
          <pre className="code whitespace-pre-wrap">{item.RawText}</pre>
        </>
      )}
      {missingGuidelines ? (
        <Callout tone="warning" title={t.text.noGuidelineTitle}>
          {t.text.noGuidelineBody}
        </Callout>
      ) : null}
    </Step>
  )
}

function ChecklistStep({ item, editable }: { item: RejectionCaseDto; editable: boolean }) {
  const t = useRejectionsText()
  const queryClient = useQueryClient()
  const organizationId = useOrganizationId()
  const [code, setCode] = useState('')
  const [text, setText] = useState('')
  const items = item.Checklist ?? []
  const save = useMutation({
    mutationFn: (next: RejectionChecklistItem[]) => rejectionsApi.updateChecklist(organizationId, item.Id, next),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: rejectionKeys.all }),
  })
  const done = items.filter((entry) => entry.Status === ChecklistItemStatus.Done).length
  const groups = [...new Set(items.map((entry) => entry.GuidelineCode ?? ''))]
  const titleOf = (guidelineCode: string) => item.Guidelines?.find((guideline) => guideline.Code === guidelineCode)?.Title

  const setStatus = (index: number, status: ChecklistItemStatus) =>
    save.mutate(items.map((entry, position) => (position === index ? { ...entry, Status: status } : entry)))

  return (
    <Step number={2} title={interpolate(t.checklist.title, { done, total: items.length })} muted={!item.RawText && items.length === 0}>
      {items.length === 0 ? <p className="text-[13px] text-fg3">{t.checklist.empty}</p> : null}
      {groups.map((group) => (
        <div key={group || 'none'} className="grid gap-1.5">
          <b className="text-[13px]">
            {group ? `${group}${titleOf(group) ? ` · ${titleOf(group)}` : ''}` : t.checklist.general}
            {(item.RepeatedGuidelines ?? []).includes(group) ? (
              <span className="ml-2">
                <Badge tone="warning" small>
                  {t.checklist.repeated}
                </Badge>
              </span>
            ) : null}
          </b>
          <ul className="grid gap-1.5">
            {items.map((entry, index) =>
              (entry.GuidelineCode ?? '') === group ? (
                <li key={entry.Id ?? index} className="flex flex-wrap items-center gap-2 rounded-md border border-border px-3 py-2">
                  <span className="min-w-0 flex-1 text-[13px]">{entry.Text}</span>
                  {entry.FixRoute ? (
                    <Link className="lnk text-xs" to={`/apps/${item.AppId}/${entry.FixRoute}`}>
                      {t.checklist.fix}
                    </Link>
                  ) : null}
                  {editable ? (
                    <span className="w-36">
                      <Select
                        aria-label={interpolate(t.checklist.statusOf, { text: entry.Text ?? '' })}
                        value={entry.Status}
                        options={[ChecklistItemStatus.Todo, ChecklistItemStatus.InProgress, ChecklistItemStatus.Done].map((status) => ({
                          value: status,
                          label: statusView(status, t).label,
                        }))}
                        onChange={(status) => setStatus(index, status)}
                      />
                    </span>
                  ) : (
                    <Badge tone={statusView(entry.Status, t).tone} small>
                      {statusView(entry.Status, t).label}
                    </Badge>
                  )}
                </li>
              ) : null,
            )}
          </ul>
        </div>
      ))}
      {editable ? (
        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={(event) => {
            event.preventDefault()
            if (!text.trim()) {
              return
            }
            save.mutate([...items, { GuidelineCode: code.trim() || undefined, Text: text.trim(), Status: ChecklistItemStatus.Todo }], {
              onSuccess: () => {
                setText('')
                setCode('')
              },
            })
          }}
        >
          <span className="w-28">
            <Field label={t.checklist.code} optional>
              <Input value={code} placeholder="2.1" onChange={(event) => setCode(event.target.value)} />
            </Field>
          </span>
          <span className="min-w-[200px] flex-1">
            <Field label={t.checklist.newItem}>
              <Input value={text} onChange={(event) => setText(event.target.value)} />
            </Field>
          </span>
          <Button type="submit" loading={save.isPending} disabled={!text.trim()}>
            <Plus aria-hidden />
            {t.checklist.add}
          </Button>
        </form>
      ) : null}
    </Step>
  )
}

function ReplyStep({ item, editable }: { item: RejectionCaseDto; editable: boolean }) {
  const t = useRejectionsText()
  const p2 = useP2()
  const format = useFormat()
  const base = useT()
  const queryClient = useQueryClient()
  const organizationId = useOrganizationId()
  const draft = item.ReplyDraft
  const [body, setBody] = useState(draft?.Body ?? '')
  const length = replyLength(body)
  const over = length > REJECTION_REPLY_MAX
  const dirty = body !== (draft?.Body ?? '')
  const sent = !!draft?.SentDate

  const save = useMutation({
    mutationFn: () => rejectionsApi.updateReplyDraft(organizationId, item.Id, body),
    onSuccess: () => {
      showToast(t.reply.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: rejectionKeys.detail(organizationId, item.Id) })
    },
  })
  const markSent = useMutation({
    mutationFn: async () => {
      if (dirty) {
        await rejectionsApi.updateReplyDraft(organizationId, item.Id, body)
      }
      return rejectionsApi.markReplySent(organizationId, item.Id)
    },
    onSuccess: () => {
      showToast(t.reply.markedSent, 'success')
      void queryClient.invalidateQueries({ queryKey: rejectionKeys.all })
    },
  })

  const copy = async () => {
    if (await copyText(body)) {
      showToast(base.common.copied, 'success')
    } else {
      showToast(base.common.copyFailed, 'error')
    }
  }

  const stepHref = item.ReplyStepId ? `/apps/${item.AppId}/assisted/${item.ReplyStepId}` : `/apps/${item.AppId}/assisted`

  return (
    <Step number={3} title={t.reply.title}>
      {sent ? (
        <Callout tone="success" title={t.reply.sentTitle}>
          {interpolate(t.reply.sentBody, {
            date: format.dateTime(draft?.SentDate),
            name: draft?.SentByUserName ?? '-',
            count: draft?.Length ?? length,
          })}
        </Callout>
      ) : null}
      {editable && !sent ? (
        <Field
          label={t.reply.label}
          counter={{ value: length, max: REJECTION_REPLY_MAX }}
          error={over ? interpolate(t.reply.over, { count: length - REJECTION_REPLY_MAX }) : null}
          hint={
            draft?.UpdatedDate
              ? interpolate(t.reply.lastSaved, {
                  date: format.dateTime(draft.UpdatedDate),
                  name: draft.ByUserName ?? '-',
                  channel: draft.Channel ? channelLabel(draft.Channel, p2) : '-',
                })
              : t.reply.hint
          }
        >
          <Textarea rows={10} value={body} onChange={(event) => setBody(event.target.value)} />
        </Field>
      ) : body ? (
        <pre className="code whitespace-pre-wrap">{body}</pre>
      ) : (
        <p className="text-[13px] text-fg3">{t.reply.none}</p>
      )}
      <Callout tone="info">{t.reply.apiNote}</Callout>
      <div className="flex flex-wrap justify-end gap-2">
        <Button disabled={!body || over} onClick={() => void copy()}>
          <ClipboardCopy aria-hidden />
          {t.reply.copy}
        </Button>
        {editable && !sent ? (
          <>
            {over ? (
              <Button disabled>
                <Send aria-hidden />
                {t.reply.assisted}
              </Button>
            ) : (
              <Link className="btn" to={stepHref}>
                <Send aria-hidden />
                {t.reply.assisted}
              </Link>
            )}
            <Button loading={save.isPending} disabled={!dirty} onClick={() => save.mutate()}>
              {t.reply.save}
            </Button>
            <Button variant="primary" loading={markSent.isPending} disabled={!body.trim() || over} onClick={() => markSent.mutate()}>
              {t.reply.markSent}
            </Button>
          </>
        ) : null}
      </div>
    </Step>
  )
}

function CloseDialog({ item, onClose }: { item: RejectionCaseDto; onClose: () => void }) {
  const t = useRejectionsText()
  const base = useT()
  const queryClient = useQueryClient()
  const organizationId = useOrganizationId()
  const [note, setNote] = useState('')
  const close = useMutation({
    mutationFn: () => rejectionsApi.close(organizationId, item.Id, note.trim() || null),
    onSuccess: () => {
      showToast(t.close.done, 'success')
      void queryClient.invalidateQueries({ queryKey: rejectionKeys.all })
      onClose()
    },
  })
  return (
    <Modal
      open
      onClose={onClose}
      role="alertdialog"
      size="sm"
      title={t.close.title}
      description={t.close.body}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="danger" loading={close.isPending} onClick={() => close.mutate()}>
            {t.close.confirm}
          </Button>
        </>
      }
    >
      <Field label={t.close.note} optional>
        <Textarea rows={3} value={note} onChange={(event) => setNote(event.target.value)} />
      </Field>
    </Modal>
  )
}

function useOrganizationId(): number {
  return useCurrentOrganization().Id
}

function CaseDetail({ context }: { context: WorkspaceContext }) {
  const t = useRejectionsText()
  const p2 = useP2()
  const format = useFormat()
  const { caseId: rawId = '' } = useParams()
  const caseId = Number(rawId)
  const [closing, setClosing] = useState(false)
  const detail = useQuery({
    queryKey: rejectionKeys.detail(context.organizationId, caseId),
    queryFn: () => rejectionsApi.detail(context.organizationId, caseId),
    enabled: Number.isInteger(caseId) && caseId > 0,
  })
  const back = (
    <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to={`/apps/${context.app.Id}/rejections`}>
      <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
      {t.title}
    </Link>
  )
  if (detail.isLoading) {
    return <CardSkeleton />
  }
  if (detail.isError || !detail.data) {
    return <ScreenState error={detail.error} onRetry={() => void detail.refetch()} back={back} />
  }
  const item = detail.data
  const open = item.State === RejectionState.Open
  const editable = open && context.can(OrgPermission.EditContent)
  const title = interpolate(item.Kind === RejectionKind.CertificationFailed ? t.detail.titleCertification : t.detail.title, {
    app: item.AppName ?? context.app.Name,
    version: versionLabel(item),
  })
  const guidelines = (item.Guidelines ?? []).map((guideline) => guideline.Code).filter(Boolean)

  return (
    <>
      {back}
      <PageHeader
        title={title}
        badge={
          <span className="flex flex-wrap gap-1.5">
            <StoreTag store={item.Store} small />
            <StateBadge state={item.State} />
            <Badge small>{kindLabel(item.Kind, t)}</Badge>
          </span>
        }
        subtitle={interpolate(t.detail.subtitle, {
          guidelines: guidelines.length > 0 ? guidelines.join(', ') : '-',
          date: format.dateTime(item.OpenedDate),
          via: sourceLabel(item.OpenedVia, t),
        })}
        actions={
          <>
            {item.ResolutionCenterUrl ? (
              <a className="btn" href={item.ResolutionCenterUrl} target="_blank" rel="noopener noreferrer">
                <ExternalLink aria-hidden />
                {t.detail.resolutionCenter}
              </a>
            ) : null}
            {editable ? (
              <Button variant="danger-outline" onClick={() => setClosing(true)}>
                {t.close.action}
              </Button>
            ) : null}
          </>
        }
      />
      {!open ? (
        <div className="mb-3">
          <Callout tone="neutral" title={t.detail.closedTitle}>
            {interpolate(item.ClosedVia === RejectionCloseReason.Submission ? t.detail.closedBySubmission : t.detail.closedByHand, {
              date: format.dateTime(item.ClosedDate),
              note: item.CloseNote ?? '',
            })}
          </Callout>
        </div>
      ) : null}
      {(item.RepeatedGuidelines ?? []).length > 0 ? (
        <div className="mb-3">
          <Callout tone="warning" title={t.detail.repeatedTitle}>
            {interpolate(t.detail.repeatedBody, { codes: (item.RepeatedGuidelines ?? []).join(', ') })}
          </Callout>
        </div>
      ) : null}
      <div className="grid gap-3 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div className="grid gap-3">
          <TextStep key={`text-${item.RawText ?? ''}`} item={item} editable={editable} />
          <ChecklistStep item={item} editable={editable} />
          <ReplyStep
            key={`reply-${item.ReplyDraft?.UpdatedDate ?? ''}-${item.ReplyDraft?.SentDate ?? ''}`}
            item={item}
            editable={editable}
          />
          <Step number={4} title={t.resubmit.title}>
            <p className="text-[13px] text-fg2">{item.Kind === RejectionKind.MetadataRejected ? t.resubmit.metadata : t.resubmit.body}</p>
            <div>
              <Link className="btn" to={`/apps/${item.AppId}/readiness?store=${storeSlug(item.Store)}`}>
                {t.resubmit.readiness}
              </Link>
            </div>
          </Step>
        </div>
        <aside className="card grid content-start gap-3 p-4" aria-label={t.detail.timeline}>
          <h2 className="text-[15px] font-semibold">{t.detail.timeline}</h2>
          {(item.Timeline ?? []).length === 0 ? (
            <p className="text-[13px] text-fg3">-</p>
          ) : (
            <Timeline
              items={(item.Timeline ?? []).map((entry, index) => ({
                id: index,
                tone: 'ok',
                title: entry.Text ?? entry.Kind ?? '-',
                meta: `${format.dateTime(entry.Date)} · ${channelLabel(entry.Channel, p2)}`,
              }))}
            />
          )}
          {item.PreviousCaseId ? (
            <Link className="lnk text-[13px]" to={`/apps/${item.AppId}/rejections/${item.PreviousCaseId}`}>
              {t.detail.previous}
            </Link>
          ) : null}
        </aside>
      </div>
      {closing ? <CloseDialog item={item} onClose={() => setClosing(false)} /> : null}
    </>
  )
}

/** /apps/:appId/rejections/:caseId. */
export function RejectionCasePage() {
  return (
    <AppWorkspace section="rejections" appWide>
      {(context) => <CaseDetail context={context} />}
    </AppWorkspace>
  )
}
