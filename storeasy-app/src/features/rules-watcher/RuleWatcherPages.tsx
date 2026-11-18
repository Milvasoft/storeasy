import { useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ExternalLink, Radar, ShieldAlert } from 'lucide-react'
import { Badge, StoreTag, type BadgeTone } from '@/components/ui/Badge'
import { CodeBlock, DiffView, type DiffLine } from '@/components/ui/Code'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState, StateView } from '@/components/ui/ScreenState'
import {
  Button,
  CardSkeleton,
  Checkbox,
  Chip,
  EmptyState,
  Field,
  Input,
  PageHeader,
  RadioGroup,
  Textarea,
} from '@/components/ui/primitives'
import { FilterType, SortType, type ListRequest } from '@/lib/api/list'
import { useAuth } from '@/lib/auth/AuthContext'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { showToast } from '@/lib/toast/toast'
import { ruleWatcherApi, ruleWatcherKeys } from './api'
import { useRuleWatcherText, type RuleWatcherDictionary } from './i18n'
import {
  DECISION_NOTE_MAX,
  RuleProposalPreparer,
  RuleProposalRejectReason,
  RuleProposalStatus,
  RuleSourceStatus,
  type RuleProposalDetailDto,
  type RuleProposalListDto,
  type RuleSourceDto,
} from './types'

// Design 17.5-17.8: the watcher reads the stores' official pages; a changed source opens a proposal, Claude (MCP) may
// prepare the catalog patch, and only an administrator's approval publishes a new catalog version.

const DEFAULT_PAGE_SIZE = 20

function statusView(status: RuleProposalStatus, t: RuleWatcherDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case RuleProposalStatus.Approved:
      return { label: t.status.approved, tone: 'success' }
    case RuleProposalStatus.Rejected:
      return { label: t.status.rejected, tone: 'neutral' }
    default:
      return { label: t.status.pending, tone: 'warning' }
  }
}

function sourceView(status: RuleSourceStatus, t: RuleWatcherDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case RuleSourceStatus.Unchanged:
      return { label: t.sourceStatus.unchanged, tone: 'success' }
    case RuleSourceStatus.Changed:
      return { label: t.sourceStatus.changed, tone: 'warning' }
    case RuleSourceStatus.Unreachable:
      return { label: t.sourceStatus.unreachable, tone: 'danger' }
    default:
      return { label: t.sourceStatus.new, tone: 'neutral' }
  }
}

function preparerLabel(proposal: RuleProposalListDto, t: RuleWatcherDictionary): string {
  return proposal.PreparedBy === RuleProposalPreparer.Mcp ? t.preparer.mcp : t.preparer.watcher
}

function rejectReasonLabel(reason: RuleProposalRejectReason, t: RuleWatcherDictionary): string {
  switch (reason) {
    case RuleProposalRejectReason.FormattingOnly:
      return t.reject.formattingOnly
    case RuleProposalRejectReason.Misread:
      return t.reject.misread
    case RuleProposalRejectReason.SourceUnstable:
      return t.reject.sourceUnstable
    default:
      return t.reject.other
  }
}

function shortHash(hash: string | undefined): string {
  return hash ? `${hash.slice(0, 4)}…${hash.slice(-4)}` : '-'
}

/** The changed paragraph as diff lines: lines only in the old text are removed, lines only in the new one added. */
export function excerptDiff(before: string | undefined, after: string | undefined): DiffLine[] {
  const old = (before ?? '').split('\n').filter((line) => line.trim())
  const next = (after ?? '').split('\n').filter((line) => line.trim())
  return [
    ...old.map((line): DiffLine => ({ kind: next.includes(line) ? 'same' : 'del', text: line })),
    ...next.filter((line) => !old.includes(line)).map((line): DiffLine => ({ kind: 'add', text: line })),
  ]
}

/** Platform screens are for the installation's administrators (Manager user type); the API checks the permission. */
function ManagerOnly({ children }: { children: ReactNode }) {
  const base = useT()
  const { isManager } = useAuth()
  if (!isManager) {
    return (
      <StateView
        icon={<ShieldAlert aria-hidden />}
        title={base.states.forbiddenTitle}
        description={base.states.platformOnly}
        action={
          <Link className="btn inv" to={HOME_PATH}>
            {base.states.backToApps}
          </Link>
        }
      />
    )
  }
  return <>{children}</>
}

// ---- Proposals and sources --------------------------------------------------------------------------------------------

function Overview() {
  const t = useRuleWatcherText()
  const format = useFormat()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [pendingOnly, setPendingOnly] = useState(true)
  const [page, setPage] = useState(1)
  const [size, setSize] = useState(DEFAULT_PAGE_SIZE)
  const request = useMemo<ListRequest>(
    () => ({
      PageNumber: page,
      RowCount: size,
      Sorting: { SortBy: 'CreationDate', Type: SortType.Desc },
      ...(pendingOnly
        ? { Filtering: { Criterias: [{ FilterBy: 'Status', Value: RuleProposalStatus.Pending, Type: FilterType.EqualTo }] } }
        : {}),
    }),
    [page, size, pendingOnly],
  )
  const proposals = useQuery({
    queryKey: ruleWatcherKeys.proposals(request),
    queryFn: () => ruleWatcherApi.proposals(request),
    placeholderData: keepPreviousData,
  })
  const sources = useQuery({ queryKey: ruleWatcherKeys.sources(), queryFn: () => ruleWatcherApi.sources() })
  const fetchNow = useMutation({
    mutationFn: () => ruleWatcherApi.fetch(),
    onSuccess: (result) => {
      showToast(
        interpolate(t.fetched, {
          checked: result.Checked,
          changed: result.Changed,
          unreachable: result.Unreachable,
          opened: result.ProposalsOpened,
        }),
        result.Unreachable > 0 ? 'warning' : 'success',
      )
      void queryClient.invalidateQueries({ queryKey: ruleWatcherKeys.all })
    },
  })

  const proposalColumns: Column<RuleProposalListDto>[] = [
    {
      key: 'title',
      header: t.proposals.proposal,
      role: 'main',
      cell: (proposal) => (
        <span className="grid gap-0.5">
          <Link className="lnk font-semibold" to={`/admin/rules/proposals/${proposal.Id}`} onClick={(event) => event.stopPropagation()}>
            {`#${proposal.Id} ${proposal.Title ?? ''}`}
          </Link>
          <span className="mono text-xs text-fg3">{(proposal.RuleIds ?? []).join(', ') || '-'}</span>
        </span>
      ),
    },
    { key: 'store', header: t.proposals.store, cell: (proposal) => <StoreTag store={proposal.Store} small codeOnly /> },
    {
      key: 'preparer',
      header: t.proposals.preparer,
      cell: (proposal) => [preparerLabel(proposal, t), proposal.PreparedByUserName].filter(Boolean).join(' · '),
    },
    {
      key: 'version',
      header: t.proposals.version,
      cell: (proposal) => (
        <span className="mono text-xs">{`${proposal.CatalogVersionFrom ?? '-'} → ${proposal.CatalogVersionTo ?? '-'}`}</span>
      ),
    },
    {
      key: 'status',
      header: t.proposals.status,
      cell: (proposal) => (
        <Badge tone={statusView(proposal.Status, t).tone} dot small>
          {statusView(proposal.Status, t).label}
        </Badge>
      ),
    },
    { key: 'opened', header: t.proposals.opened, role: 'hidePhone', cell: (proposal) => format.dateTime(proposal.CreationDate) },
  ]

  const sourceColumns: Column<RuleSourceDto>[] = [
    {
      key: 'name',
      header: t.sources.source,
      role: 'main',
      cell: (source) => (
        <span className="grid gap-0.5">
          <b>{source.Name}</b>
          {source.Url ? (
            <a className="lnk inline-flex items-center gap-1 break-all text-xs" href={source.Url} target="_blank" rel="noopener noreferrer">
              {source.Url}
              <ExternalLink className="h-3 w-3" aria-hidden />
            </a>
          ) : null}
        </span>
      ),
    },
    { key: 'store', header: t.proposals.store, cell: (source) => <StoreTag store={source.Store} small codeOnly /> },
    {
      key: 'fetched',
      header: t.sources.lastFetch,
      cell: (source) => (
        <span className="grid gap-0.5">
          <span>{source.LastFetchDate ? format.dateTime(source.LastFetchDate) : '-'}</span>
          {source.LastChangeDate ? (
            <span className="text-xs text-fg3">{interpolate(t.sources.changedOn, { date: format.dateTime(source.LastChangeDate) })}</span>
          ) : null}
        </span>
      ),
    },
    {
      key: 'hash',
      header: t.sources.hash,
      role: 'hidePhone',
      cell: (source) => (
        <span className="mono text-xs">
          {source.Status === RuleSourceStatus.Changed && source.PreviousContentHash
            ? `${shortHash(source.PreviousContentHash)} → ${shortHash(source.ContentHash)}`
            : shortHash(source.ContentHash)}
        </span>
      ),
    },
    {
      key: 'status',
      header: t.proposals.status,
      cell: (source) => (
        <span className="grid gap-0.5">
          <Badge tone={sourceView(source.Status, t).tone} small>
            {source.IsActive ? sourceView(source.Status, t).label : t.sources.inactive}
          </Badge>
          {source.LastError ? <span className="text-xs text-danger">{source.LastError}</span> : null}
        </span>
      ),
    },
  ]

  const rows = proposals.data?.Data ?? []
  return (
    <>
      <PageHeader
        title={t.title}
        subtitle={t.subtitle}
        actions={
          <Button variant="primary" loading={fetchNow.isPending} onClick={() => fetchNow.mutate()}>
            <Radar aria-hidden />
            {t.fetch}
          </Button>
        }
      />
      <div className="toolbar mb-3" role="group" aria-label={t.proposals.status}>
        <Chip
          selected={pendingOnly}
          onClick={() => {
            setPendingOnly(true)
            setPage(1)
          }}
        >
          {t.proposals.pending}
        </Chip>
        <Chip
          selected={!pendingOnly}
          onClick={() => {
            setPendingOnly(false)
            setPage(1)
          }}
        >
          {t.proposals.all}
        </Chip>
      </div>
      {proposals.isLoading ? (
        <CardSkeleton />
      ) : proposals.isError ? (
        <ScreenState error={proposals.error} onRetry={() => void proposals.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState
          icon={<Radar aria-hidden />}
          title={pendingOnly ? t.proposals.emptyPending : t.proposals.empty}
          description={t.proposals.emptyBody}
        />
      ) : (
        <>
          <DataTable
            caption={t.title}
            columns={proposalColumns}
            rows={rows}
            rowKey={(proposal) => proposal.Id}
            busy={proposals.isFetching}
            onRowClick={(proposal) => navigate(`/admin/rules/proposals/${proposal.Id}`)}
          />
          <Pagination
            page={page}
            pageCount={proposals.data?.TotalPageCount ?? 1}
            total={proposals.data?.TotalDataCount ?? rows.length}
            pageSize={size}
            onPage={setPage}
            onPageSize={(value) => {
              setSize(value)
              setPage(1)
            }}
          />
        </>
      )}
      <h2 className="mb-2 mt-5 text-[15px] font-semibold">{t.sources.title}</h2>
      {sources.isLoading ? (
        <CardSkeleton />
      ) : sources.isError ? (
        <ScreenState error={sources.error} onRetry={() => void sources.refetch()} />
      ) : (
        <DataTable caption={t.sources.title} columns={sourceColumns} rows={sources.data ?? []} rowKey={(source) => source.Id} />
      )}
    </>
  )
}

/** /admin/rules. */
export function RuleWatcherPage() {
  return (
    <ManagerOnly>
      <Overview />
    </ManagerOnly>
  )
}

// ---- Proposal detail --------------------------------------------------------------------------------------------------

function ApproveDialog({ proposal, onClose }: { proposal: RuleProposalDetailDto; onClose: () => void }) {
  const t = useRuleWatcherText()
  const base = useT()
  const queryClient = useQueryClient()
  const [notify, setNotify] = useState(true)
  const [note, setNote] = useState('')
  const impact = proposal.Impact ?? []
  const organizations = new Set(impact.map((item) => item.OrganizationId)).size
  const approve = useMutation({
    mutationFn: () => ruleWatcherApi.approve(proposal.Id, notify, note.trim() || null),
    onSuccess: (decision) => {
      showToast(
        interpolate(t.approve.done, {
          version: decision.CatalogVersion ?? proposal.CatalogVersionTo ?? '-',
          organizations: decision.NotifiedOrganizationCount,
          users: decision.NotifiedUserCount,
        }),
        'success',
      )
      void queryClient.invalidateQueries({ queryKey: ruleWatcherKeys.all })
      onClose()
    },
  })
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={interpolate(t.approve.title, { id: proposal.Id })}
      description={interpolate(t.approve.body, { from: proposal.CatalogVersionFrom ?? '-', to: proposal.CatalogVersionTo ?? '-' })}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={approve.isPending} onClick={() => approve.mutate()}>
            {t.approve.confirm}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        {impact.length > 0 ? (
          <ul className="grid gap-1 text-[13px]">
            {impact.map((item, index) => (
              <li key={`${item.OrganizationId}-${item.AppId}-${index}`}>
                <b>{item.AppName ?? `#${item.AppId}`}</b> · <span className="text-fg2">{item.Message}</span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-[13px] text-fg3">{t.impact.none}</p>
        )}
        <Checkbox checked={notify} onChange={setNotify}>
          {interpolate(t.approve.notify, { organizations, notifications: impact.length })}
        </Checkbox>
        <Field label={t.note} optional counter={{ value: note.length, max: DECISION_NOTE_MAX }}>
          <Textarea rows={2} maxLength={DECISION_NOTE_MAX} value={note} onChange={(event) => setNote(event.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

function RejectDialog({ proposal, onClose }: { proposal: RuleProposalDetailDto; onClose: () => void }) {
  const t = useRuleWatcherText()
  const base = useT()
  const queryClient = useQueryClient()
  const [reason, setReason] = useState<RuleProposalRejectReason | null>(null)
  const [note, setNote] = useState('')
  const reject = useMutation({
    mutationFn: () => ruleWatcherApi.reject(proposal.Id, reason ?? RuleProposalRejectReason.Other, note.trim() || null),
    onSuccess: () => {
      showToast(t.reject.done, 'success')
      void queryClient.invalidateQueries({ queryKey: ruleWatcherKeys.all })
      onClose()
    },
  })
  const reasons = [
    RuleProposalRejectReason.FormattingOnly,
    RuleProposalRejectReason.Misread,
    RuleProposalRejectReason.SourceUnstable,
    RuleProposalRejectReason.Other,
  ]
  return (
    <Modal
      open
      onClose={onClose}
      role="alertdialog"
      title={interpolate(t.reject.title, { id: proposal.Id })}
      description={t.reject.body}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="danger" loading={reject.isPending} disabled={reason === null} onClick={() => reject.mutate()}>
            {t.reject.confirm}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <RadioGroup<RuleProposalRejectReason | 0>
          name="reject-reason"
          label={t.reject.reason}
          value={reason ?? 0}
          options={reasons.map((value) => ({ value, label: rejectReasonLabel(value, t) }))}
          onChange={(value) => setReason(value === 0 ? null : value)}
        />
        <Field label={t.note} optional counter={{ value: note.length, max: DECISION_NOTE_MAX }}>
          <Textarea rows={3} maxLength={DECISION_NOTE_MAX} value={note} onChange={(event) => setNote(event.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

function PrepareDialog({ proposal, onClose }: { proposal: RuleProposalDetailDto; onClose: () => void }) {
  const t = useRuleWatcherText()
  const base = useT()
  const queryClient = useQueryClient()
  const [ruleIds, setRuleIds] = useState((proposal.RuleIds ?? []).join(', '))
  const [patch, setPatch] = useState(proposal.CatalogPatch ?? '')
  const [note, setNote] = useState(proposal.PreparerNote ?? '')
  const prepare = useMutation({
    mutationFn: () =>
      ruleWatcherApi.prepare(proposal.Id, {
        RuleIds: ruleIds
          .split(',')
          .map((item) => item.trim())
          .filter(Boolean),
        CatalogPatch: patch.trim() || null,
        Note: note.trim() || null,
      }),
    onSuccess: () => {
      showToast(t.prepare.done, 'success')
      void queryClient.invalidateQueries({ queryKey: ruleWatcherKeys.all })
      onClose()
    },
  })
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title={t.prepare.title}
      description={t.prepare.body}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={prepare.isPending} onClick={() => prepare.mutate()}>
            {base.common.save}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <Field label={t.prepare.ruleIds} hint={t.prepare.ruleIdsHint}>
          <Input value={ruleIds} mono onChange={(event) => setRuleIds(event.target.value)} />
        </Field>
        <Field label={t.prepare.patch}>
          <Textarea rows={12} mono value={patch} onChange={(event) => setPatch(event.target.value)} />
        </Field>
        <Field label={t.note} optional counter={{ value: note.length, max: DECISION_NOTE_MAX }}>
          <Textarea rows={2} maxLength={DECISION_NOTE_MAX} value={note} onChange={(event) => setNote(event.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

function ProposalDetail() {
  const t = useRuleWatcherText()
  const format = useFormat()
  const { proposalId: rawId = '' } = useParams()
  const proposalId = Number(rawId)
  const [dialog, setDialog] = useState<'approve' | 'reject' | 'prepare' | null>(null)
  const detail = useQuery({
    queryKey: ruleWatcherKeys.proposal(proposalId),
    queryFn: () => ruleWatcherApi.proposal(proposalId),
    enabled: Number.isInteger(proposalId) && proposalId > 0,
  })
  const back = (
    <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to="/admin/rules">
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
  const proposal = detail.data
  const pending = proposal.Status === RuleProposalStatus.Pending
  const impact = proposal.Impact ?? []
  const byOrganization = [...new Set(impact.map((item) => item.OrganizationId))].map((organizationId) => ({
    organizationId,
    apps: impact.filter((item) => item.OrganizationId === organizationId),
  }))
  const view = statusView(proposal.Status, t)

  return (
    <>
      {back}
      <PageHeader
        title={proposal.Title ?? `#${proposal.Id}`}
        badge={
          <span className="flex flex-wrap gap-1.5">
            <Badge tone={view.tone} dot small>
              {view.label}
            </Badge>
            <Badge tone={proposal.PreparedBy === RuleProposalPreparer.Mcp ? 'accent' : 'neutral'} small>
              {proposal.PreparedBy === RuleProposalPreparer.Mcp ? t.preparer.mcpPrepared : t.preparer.watcher}
            </Badge>
          </span>
        }
        subtitle={`#${proposal.Id} · ${proposal.CatalogVersionFrom ?? '-'} → ${proposal.CatalogVersionTo ?? '-'} · ${format.dateTime(proposal.CreationDate)}`}
        actions={
          pending ? (
            <>
              <Button onClick={() => setDialog('prepare')}>{t.prepare.action}</Button>
              <Button variant="danger-outline" onClick={() => setDialog('reject')}>
                {t.reject.action}
              </Button>
              <Button variant="primary" onClick={() => setDialog('approve')}>
                {t.approve.action}
              </Button>
            </>
          ) : null
        }
      />
      {!pending ? (
        <div className="mb-3">
          <Callout tone="neutral" title={view.label}>
            {[
              proposal.DecidedByUserName,
              proposal.DecidedDate ? format.dateTime(proposal.DecidedDate) : null,
              proposal.RejectReason ? rejectReasonLabel(proposal.RejectReason, t) : null,
              proposal.DecisionNote,
            ]
              .filter(Boolean)
              .join(' · ')}
          </Callout>
        </div>
      ) : null}
      <div className="grid gap-3 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div className="grid content-start gap-3">
          <section className="card grid gap-2 p-4" aria-label={t.detail.source}>
            <div className="flex flex-wrap items-center gap-2">
              <StoreTag store={proposal.Store} small codeOnly />
              <h2 className="mr-auto text-[15px] font-semibold">{t.detail.source}</h2>
              {proposal.SourceUrl ? (
                <a className="btn sm" href={proposal.SourceUrl} target="_blank" rel="noopener noreferrer">
                  <ExternalLink aria-hidden />
                  {t.detail.openSource}
                </a>
              ) : null}
            </div>
            <span className="text-xs text-fg3">
              {[proposal.SourceName, `${shortHash(proposal.PreviousContentHash)} → ${shortHash(proposal.ContentHash)}`]
                .filter(Boolean)
                .join(' · ')}
            </span>
            {proposal.SourceExcerptBefore || proposal.SourceExcerptAfter ? (
              <DiffView title={t.detail.changedParagraph} lines={excerptDiff(proposal.SourceExcerptBefore, proposal.SourceExcerptAfter)} />
            ) : (
              <p className="text-[13px] text-fg3">{t.detail.noExcerpt}</p>
            )}
          </section>
          <section className="card grid gap-2 p-4" aria-label={t.detail.catalog}>
            <h2 className="text-[15px] font-semibold">{t.detail.catalog}</h2>
            <span className="mono text-xs text-fg3">{(proposal.RuleIds ?? []).join(', ') || '-'}</span>
            {proposal.CatalogPatch ? (
              <CodeBlock label={t.detail.catalog}>{proposal.CatalogPatch}</CodeBlock>
            ) : (
              <Callout tone="info">{t.detail.noPatch}</Callout>
            )}
          </section>
        </div>
        <div className="grid content-start gap-3">
          <section className="card grid gap-2 p-4" aria-label={t.impact.title}>
            <h2 className="text-[15px] font-semibold">{t.impact.title}</h2>
            {impact.length === 0 ? (
              <p className="text-[13px] text-fg3">{t.impact.none}</p>
            ) : (
              <>
                <p className="text-[13px] text-fg2">
                  {interpolate(t.impact.summary, { organizations: byOrganization.length, apps: impact.length })}
                </p>
                <ul className="grid gap-2">
                  {byOrganization.map((group) => (
                    <li key={group.organizationId} className="grid gap-0.5 text-[13px]">
                      <b>{interpolate(t.impact.organization, { id: group.organizationId, count: group.apps.length })}</b>
                      <span className="text-fg2">{group.apps.map((app) => app.AppName ?? `#${app.AppId}`).join(', ')}</span>
                    </li>
                  ))}
                </ul>
                <p className="text-xs text-fg3">{interpolate(t.impact.sample, { message: impact[0]?.Message ?? '-' })}</p>
              </>
            )}
          </section>
          {proposal.PreparerNote ? (
            <section className="card grid gap-1 p-4" aria-label={t.detail.note}>
              <h2 className="text-[15px] font-semibold">{t.detail.note}</h2>
              <p className="whitespace-pre-wrap text-[13px]">{proposal.PreparerNote}</p>
              <span className="text-xs text-fg3">{proposal.PreparedByUserName ?? preparerLabel(proposal, t)}</span>
            </section>
          ) : null}
        </div>
      </div>
      {dialog === 'approve' ? <ApproveDialog proposal={proposal} onClose={() => setDialog(null)} /> : null}
      {dialog === 'reject' ? <RejectDialog proposal={proposal} onClose={() => setDialog(null)} /> : null}
      {dialog === 'prepare' ? <PrepareDialog proposal={proposal} onClose={() => setDialog(null)} /> : null}
    </>
  )
}

/** /admin/rules/proposals/:proposalId. */
export function RuleProposalPage() {
  return (
    <ManagerOnly>
      <ProposalDetail />
    </ManagerOnly>
  )
}
