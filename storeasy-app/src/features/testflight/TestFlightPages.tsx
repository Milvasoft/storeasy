import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Clock, Pencil, Plus, RefreshCw, SearchX, Trash2, Users } from 'lucide-react'
import { Badge, type BadgeTone } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState, StateView } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, Chip, EmptyState, Field, IconButton, Input, PageHeader, Switch, Textarea } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { SortType, type ListRequest } from '@/lib/api/list'
import { BuildProcessingState, OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { testFlightApi, testFlightKeys } from './api'
import { useTestFlightText, type TestFlightDictionary } from './i18n'
import {
  BetaInviteType,
  BetaReviewState,
  BetaTesterState,
  EXPIRY_URGENT_DAYS,
  EXPIRY_WARN_DAYS,
  WHAT_TO_TEST_MAX,
  type BetaBuildDto,
  type BetaGroupDto,
  type BetaLocalizationDto,
  type BetaTesterDto,
} from './types'

// Design 8.16-8.19: TestFlight builds with their 90 day life, groups, testers, "what to test" and beta app review. Every
// write is a store job; the copy refreshes when the job ends (sync).

const DEFAULT_TESTER_PAGE = 20
const BASE = (appId: number) => `/apps/${appId}/release/testflight`

export function buildLabel(build: { VersionString?: string; BuildNumber?: string }): string {
  return `${build.VersionString ?? '-'} (${build.BuildNumber ?? '-'})`
}

function reviewView(state: BetaReviewState, t: TestFlightDictionary): { label: string; tone: BadgeTone } {
  switch (state) {
    case BetaReviewState.WaitingForReview:
      return { label: t.review.waiting, tone: 'info' }
    case BetaReviewState.InReview:
      return { label: t.review.inReview, tone: 'info' }
    case BetaReviewState.Approved:
      return { label: t.review.approved, tone: 'success' }
    case BetaReviewState.Rejected:
      return { label: t.review.rejected, tone: 'danger' }
    default:
      return { label: t.review.notSubmitted, tone: 'neutral' }
  }
}

function testerStateLabel(state: BetaTesterState, t: TestFlightDictionary): string {
  switch (state) {
    case BetaTesterState.Installed:
      return t.testers.installed
    case BetaTesterState.Accepted:
      return t.testers.accepted
    case BetaTesterState.Invited:
      return t.testers.invited
    case BetaTesterState.Revoked:
      return t.testers.revoked
    default:
      return t.testers.notInvited
  }
}

function ReviewBadge({ state }: { state: BetaReviewState }) {
  const t = useTestFlightText()
  const view = reviewView(state, t)
  return (
    <Badge tone={view.tone} dot small>
      {view.label}
    </Badge>
  )
}

function ExpiryText({ build }: { build: BetaBuildDto }) {
  const t = useTestFlightText()
  const format = useFormat()
  if (build.Expired) {
    return (
      <Badge tone="neutral" small>
        {interpolate(t.builds.expiredOn, { date: format.date(build.ExpirationDate) })}
      </Badge>
    )
  }
  if (!build.ExpirationDate) {
    return <span className="text-fg3">{t.builds.noExpiry}</span>
  }
  const days = build.DaysLeft ?? null
  const tone = days !== null && days < EXPIRY_URGENT_DAYS ? 'text-danger font-semibold' : days !== null && days < EXPIRY_WARN_DAYS ? 'text-warningText' : ''
  return (
    <span className={cn('tabular-nums', tone)}>
      {format.date(build.ExpirationDate)}
      {days !== null ? ` · ${interpolate(t.builds.daysLeft, { days })}` : ''}
    </span>
  )
}

function useBuilds(context: WorkspaceContext) {
  return useQuery({
    queryKey: testFlightKeys.builds(context.organizationId, context.record.StoreAppId),
    queryFn: () => testFlightApi.builds(context.organizationId, context.record.StoreAppId),
  })
}

function useGroups(context: WorkspaceContext) {
  return useQuery({
    queryKey: testFlightKeys.groups(context.organizationId, context.record.StoreAppId),
    queryFn: () => testFlightApi.groups(context.organizationId, context.record.StoreAppId),
  })
}

/** After a write: the job id goes to the live view (its end is announced) and the copy is read again. */
function useStoreWrite() {
  const t = useTestFlightText()
  const queryClient = useQueryClient()
  const { watchJobs } = useActivity()
  return (jobId: number, message: string = t.jobQueued) => {
    watchJobs([jobId])
    showToast(message, 'success', interpolate(t.job, { id: jobId }))
    void queryClient.invalidateQueries({ queryKey: testFlightKeys.all })
  }
}

function NotFound({ back }: { back: ReactNode }) {
  const base = useT()
  return <StateView icon={<SearchX aria-hidden />} title={base.states.recordNotFoundTitle} description={base.states.recordNotFoundBody} action={back} />
}

// ---- Overview ---------------------------------------------------------------------------------------------------------

function ExpiryCard({ builds, groups }: { builds: BetaBuildDto[]; groups: BetaGroupDto[] }) {
  const t = useTestFlightText()
  const format = useFormat()
  const expiring = builds
    .filter((build) => !build.Expired && build.DaysLeft !== undefined && build.DaysLeft < EXPIRY_WARN_DAYS)
    .sort((a, b) => (a.DaysLeft ?? 0) - (b.DaysLeft ?? 0))[0]
  if (!expiring) {
    return null
  }
  const testers = groups.filter((group) => expiring.GroupIds?.includes(group.Id)).reduce((sum, group) => sum + group.TesterCount, 0)
  const newer = builds.some(
    (build) => build.BuildId !== expiring.BuildId && !build.Expired && (build.DaysLeft ?? 0) > (expiring.DaysLeft ?? 0),
  )
  const urgent = (expiring.DaysLeft ?? 0) < EXPIRY_URGENT_DAYS
  return (
    <div className="mb-3">
      <Callout
        tone={urgent ? 'danger' : 'warning'}
        title={interpolate(t.expiry.title, {
          build: buildLabel(expiring),
          date: format.date(expiring.ExpirationDate),
          days: expiring.DaysLeft ?? 0,
        })}
      >
        {interpolate(newer ? t.expiry.bodyNewer : t.expiry.body, { testers })}
      </Callout>
    </div>
  )
}

function GroupDialog({ context, group, onClose }: { context: WorkspaceContext; group: BetaGroupDto | null; onClose: () => void }) {
  const t = useTestFlightText()
  const base = useT()
  const written = useStoreWrite()
  const [name, setName] = useState(group?.Name ?? '')
  const [internal, setInternal] = useState(group?.IsInternal ?? false)
  const [linkEnabled, setLinkEnabled] = useState(group?.PublicLinkEnabled ?? false)
  const [limitEnabled, setLimitEnabled] = useState(group?.PublicLinkLimitEnabled ?? false)
  const [limit, setLimit] = useState(group?.PublicLinkLimit ? String(group.PublicLinkLimit) : '')
  const limitValue = Number(limit)
  const limitError = limitEnabled && (!Number.isInteger(limitValue) || limitValue < 1 || limitValue > 10000) ? t.groups.limitInvalid : null
  const save = useMutation({
    mutationFn: () =>
      group
        ? testFlightApi.updateGroup(context.organizationId, group.Id, {
            Name: name.trim(),
            PublicLinkEnabled: group.IsInternal ? null : linkEnabled,
            PublicLinkLimit: group.IsInternal || !limitEnabled ? null : limitValue,
            PublicLinkLimitEnabled: group.IsInternal ? null : limitEnabled,
          })
        : testFlightApi.createGroup(context.organizationId, context.record.StoreAppId, {
            Name: name.trim(),
            IsInternal: internal,
            PublicLinkEnabled: !internal && linkEnabled,
            PublicLinkLimit: !internal && limitEnabled ? limitValue : null,
            PublicLinkLimitEnabled: !internal && limitEnabled,
          }),
    onSuccess: (jobId) => {
      written(jobId)
      onClose()
    },
  })
  const external = group ? !group.IsInternal : !internal
  return (
    <Modal
      open
      onClose={onClose}
      title={group ? t.groups.editTitle : t.groups.createTitle}
      description={t.groups.dialogBody}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={save.isPending} disabled={!name.trim() || !!limitError} onClick={() => save.mutate()}>
            {base.common.save}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <Field label={t.groups.name}>
          <Input value={name} maxLength={50} onChange={(event) => setName(event.target.value)} />
        </Field>
        {!group ? (
          <Checkbox checked={internal} onChange={setInternal}>
            {t.groups.internalHint}
          </Checkbox>
        ) : null}
        {external ? (
          <>
            <span className="flex items-center gap-2 text-[13px]">
              <Switch checked={linkEnabled} onChange={setLinkEnabled} label={t.groups.publicLink} />
              {t.groups.publicLink}
            </span>
            {linkEnabled ? (
              <span className="flex items-center gap-2 text-[13px]">
                <Switch checked={limitEnabled} onChange={setLimitEnabled} label={t.groups.limitToggle} />
                {t.groups.limitToggle}
              </span>
            ) : null}
            {linkEnabled && limitEnabled ? (
              <Field label={t.groups.limit} error={limitError}>
                <Input type="number" min={1} max={10000} value={limit} onChange={(event) => setLimit(event.target.value)} />
              </Field>
            ) : null}
          </>
        ) : null}
      </div>
    </Modal>
  )
}

function LocalizationDialog({
  context,
  localization,
  onClose,
}: {
  context: WorkspaceContext
  localization: BetaLocalizationDto | null
  onClose: () => void
}) {
  const t = useTestFlightText()
  const base = useT()
  const written = useStoreWrite()
  const [locale, setLocale] = useState(localization?.Locale ?? '')
  const [description, setDescription] = useState(localization?.Description ?? '')
  const [email, setEmail] = useState(localization?.FeedbackEmail ?? '')
  const [marketing, setMarketing] = useState(localization?.MarketingUrl ?? '')
  const [privacy, setPrivacy] = useState(localization?.PrivacyPolicyUrl ?? '')
  const save = useMutation({
    mutationFn: () =>
      testFlightApi.updateLocalization(context.organizationId, context.record.StoreAppId, {
        Locale: locale.trim(),
        Description: description.trim() || null,
        FeedbackEmail: email.trim() || null,
        MarketingUrl: marketing.trim() || null,
        PrivacyPolicyUrl: privacy.trim() || null,
      }),
    onSuccess: (jobId) => {
      written(jobId)
      onClose()
    },
  })
  return (
    <Modal
      open
      onClose={onClose}
      title={localization ? interpolate(t.localizations.editTitle, { locale: localization.Locale ?? '' }) : t.localizations.addTitle}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={save.isPending} disabled={!locale.trim()} onClick={() => save.mutate()}>
            {base.common.save}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        {!localization ? (
          <Field label={t.localizations.locale}>
            <Input value={locale} placeholder="en-US" onChange={(event) => setLocale(event.target.value)} />
          </Field>
        ) : null}
        <Field label={t.localizations.description} optional counter={{ value: description.length, max: WHAT_TO_TEST_MAX }}>
          <Textarea rows={4} value={description} onChange={(event) => setDescription(event.target.value)} />
        </Field>
        <Field label={t.localizations.feedbackEmail} optional>
          <Input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
        </Field>
        <Field label={t.localizations.marketingUrl} optional>
          <Input type="url" value={marketing} onChange={(event) => setMarketing(event.target.value)} />
        </Field>
        <Field label={t.localizations.privacyUrl} optional>
          <Input type="url" value={privacy} onChange={(event) => setPrivacy(event.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

function Overview({ context }: { context: WorkspaceContext }) {
  const t = useTestFlightText()
  const format = useFormat()
  const written = useStoreWrite()
  const builds = useBuilds(context)
  const groups = useGroups(context)
  const localizations = useQuery({
    queryKey: testFlightKeys.localizations(context.organizationId, context.record.StoreAppId),
    queryFn: () => testFlightApi.localizations(context.organizationId, context.record.StoreAppId),
  })
  const [groupDialog, setGroupDialog] = useState<BetaGroupDto | 'new' | null>(null)
  const [localizationDialog, setLocalizationDialog] = useState<BetaLocalizationDto | 'new' | null>(null)
  const canWrite = context.can(OrgPermission.ReleaseTest)
  const sync = useMutation({
    mutationFn: () => testFlightApi.sync(context.organizationId, context.record.StoreAppId),
    onSuccess: (jobId) => written(jobId, t.synced),
  })
  const groupList = groups.data ?? []
  const groupNames = (ids: number[] | undefined) =>
    (ids ?? []).map((id) => groupList.find((group) => group.Id === id)?.Name).filter(Boolean).join(', ') || '-'

  const columns: Column<BetaBuildDto>[] = [
    {
      key: 'build',
      header: t.builds.build,
      role: 'main',
      cell: (build) => (
        <Link className="lnk font-semibold" to={`${BASE(context.app.Id)}/builds/${build.BuildId}`}>
          {buildLabel(build)}
        </Link>
      ),
    },
    { key: 'uploaded', header: t.builds.uploaded, cell: (build) => (build.UploadedDate ? format.date(build.UploadedDate) : '-') },
    { key: 'expiry', header: t.builds.expiry, cell: (build) => <ExpiryText build={build} /> },
    { key: 'review', header: t.builds.review, cell: (build) => <ReviewBadge state={build.BetaReviewState} /> },
    { key: 'groups', header: t.builds.groups, role: 'hidePhone', cell: (build) => groupNames(build.GroupIds) },
  ]

  if (builds.isLoading || groups.isLoading) {
    return <CardSkeleton />
  }
  if (builds.isError || groups.isError) {
    return (
      <ScreenState
        error={builds.error ?? groups.error}
        onRetry={() => {
          void builds.refetch()
          void groups.refetch()
        }}
      />
    )
  }
  const buildList = builds.data ?? []

  return (
    <>
      <PageHeader
        title={t.title}
        subtitle={t.subtitle}
        actions={
          <>
            <Button loading={sync.isPending} onClick={() => sync.mutate()}>
              <RefreshCw aria-hidden />
              {t.sync}
            </Button>
            {canWrite ? (
              <Button variant="primary" onClick={() => setGroupDialog('new')}>
                <Plus aria-hidden />
                {t.groups.create}
              </Button>
            ) : null}
          </>
        }
      />
      <ExpiryCard builds={buildList} groups={groupList} />
      {buildList.length === 0 ? (
        <EmptyState icon={<Clock aria-hidden />} title={t.builds.emptyTitle} description={t.builds.emptyBody} />
      ) : (
        <DataTable caption={t.builds.title} columns={columns} rows={buildList} rowKey={(build) => build.BuildId} />
      )}
      <h2 className="mb-2 mt-5 text-[15px] font-semibold">{t.groups.title}</h2>
      {groupList.length === 0 ? (
        <EmptyState icon={<Users aria-hidden />} title={t.groups.emptyTitle} description={t.groups.emptyBody} />
      ) : (
        <ul className="grid gap-3 sm:grid-cols-2">
          {groupList.map((group) => (
            <li key={group.Id} className="card grid gap-1.5 p-4">
              <span className="flex flex-wrap items-center gap-2">
                <b className="text-[14px]">{group.Name}</b>
                <Badge small tone={group.IsInternal ? 'neutral' : 'accent'}>
                  {group.IsInternal ? t.groups.internal : t.groups.external}
                </Badge>
              </span>
              <span className="text-[13px] text-fg2">
                {interpolate(t.groups.testerCount, { count: group.TesterCount })}
                {!group.IsInternal ? ` · ${group.PublicLinkEnabled ? t.groups.linkOn : t.groups.linkOff}` : ''}
              </span>
              <span className="flex flex-wrap items-center gap-2">
                <Link className="lnk text-[13px]" to={`${BASE(context.app.Id)}/groups/${group.Id}`}>
                  {t.groups.open}
                </Link>
                {canWrite ? (
                  <IconButton label={interpolate(t.groups.editOf, { name: group.Name ?? '' })} size="sm" ghost onClick={() => setGroupDialog(group)}>
                    <Pencil aria-hidden />
                  </IconButton>
                ) : null}
              </span>
            </li>
          ))}
        </ul>
      )}
      <div className="mb-2 mt-5 flex items-center gap-2">
        <h2 className="mr-auto text-[15px] font-semibold">{t.localizations.title}</h2>
        {canWrite ? (
          <Button size="sm" onClick={() => setLocalizationDialog('new')}>
            <Plus aria-hidden />
            {t.localizations.add}
          </Button>
        ) : null}
      </div>
      {(localizations.data ?? []).length === 0 ? (
        <p className="text-[13px] text-fg3">{t.localizations.empty}</p>
      ) : (
        <ul className="grid gap-2">
          {(localizations.data ?? []).map((localization) => (
            <li key={localization.Id} className="card flex flex-wrap items-start gap-2 p-3 text-[13px]">
              <Badge small>{localization.Locale}</Badge>
              <span className="grid min-w-0 flex-1 gap-0.5">
                <span>{localization.Description ?? '-'}</span>
                <span className="text-xs text-fg3">{localization.FeedbackEmail ?? '-'}</span>
              </span>
              {canWrite ? (
                <IconButton
                  label={interpolate(t.localizations.editTitle, { locale: localization.Locale ?? '' })}
                  size="sm"
                  ghost
                  onClick={() => setLocalizationDialog(localization)}
                >
                  <Pencil aria-hidden />
                </IconButton>
              ) : null}
            </li>
          ))}
        </ul>
      )}
      {groupDialog ? <GroupDialog context={context} group={groupDialog === 'new' ? null : groupDialog} onClose={() => setGroupDialog(null)} /> : null}
      {localizationDialog ? (
        <LocalizationDialog
          context={context}
          localization={localizationDialog === 'new' ? null : localizationDialog}
          onClose={() => setLocalizationDialog(null)}
        />
      ) : null}
    </>
  )
}

/** /apps/:appId/release/testflight. */
export function TestFlightPage() {
  return (
    <AppWorkspace section="release" sub="testflight">
      {(context) => <Overview context={context} />}
    </AppWorkspace>
  )
}

// ---- Group ------------------------------------------------------------------------------------------------------------

function AddTesterDialog({ context, group, onClose }: { context: WorkspaceContext; group: BetaGroupDto; onClose: () => void }) {
  const t = useTestFlightText()
  const base = useT()
  const written = useStoreWrite()
  const [email, setEmail] = useState('')
  const [first, setFirst] = useState('')
  const [last, setLast] = useState('')
  const valid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())
  const add = useMutation({
    mutationFn: () =>
      testFlightApi.updateGroupTesters(context.organizationId, group.Id, [{ Email: email.trim(), FirstName: first.trim() || null, LastName: last.trim() || null }], []),
    onSuccess: (jobId) => {
      written(jobId, t.testers.added)
      onClose()
    },
  })
  return (
    <Modal
      open
      onClose={onClose}
      title={t.testers.addTitle}
      description={interpolate(t.testers.addBody, { group: group.Name ?? '' })}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={add.isPending} disabled={!valid} onClick={() => add.mutate()}>
            {t.testers.invite}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <Field label={t.testers.email} error={email && !valid ? t.testers.emailInvalid : null}>
          <Input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
        </Field>
        <Field label={t.testers.firstName} optional>
          <Input value={first} onChange={(event) => setFirst(event.target.value)} />
        </Field>
        <Field label={t.testers.lastName} optional>
          <Input value={last} onChange={(event) => setLast(event.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}

function GroupBuilds({ context, group, builds }: { context: WorkspaceContext; group: BetaGroupDto; builds: BetaBuildDto[] }) {
  const t = useTestFlightText()
  const written = useStoreWrite()
  const canWrite = context.can(OrgPermission.ReleaseTest)
  const inGroup = builds.filter((build) => group.BuildIds?.includes(build.BuildId) || build.GroupIds?.includes(group.Id))
  const addable = builds.filter(
    (build) => !build.Expired && build.ProcessingState === BuildProcessingState.Valid && !inGroup.some((item) => item.BuildId === build.BuildId),
  )
  const change = useMutation({
    mutationFn: ({ add, remove }: { add: number[]; remove: number[] }) => testFlightApi.updateGroupBuilds(context.organizationId, group.Id, add, remove),
    onSuccess: (jobId) => written(jobId),
  })
  return (
    <section className="card grid gap-2 p-4" aria-label={t.groups.builds}>
      <h2 className="text-[15px] font-semibold">{t.groups.builds}</h2>
      {inGroup.length === 0 ? <p className="text-[13px] text-fg3">{t.groups.noBuilds}</p> : null}
      <ul className="grid gap-1.5">
        {inGroup.map((build) => (
          <li key={build.BuildId} className="flex items-center gap-2 text-[13px]">
            <span className="mr-auto">{buildLabel(build)}</span>
            <ExpiryText build={build} />
            {canWrite ? (
              <IconButton
                label={interpolate(t.groups.removeBuild, { build: buildLabel(build) })}
                size="sm"
                ghost
                onClick={() => change.mutate({ add: [], remove: [build.BuildId] })}
              >
                <Trash2 aria-hidden />
              </IconButton>
            ) : null}
          </li>
        ))}
      </ul>
      {canWrite && addable.length > 0 ? (
        <span className="w-full sm:w-64">
          <Select<number>
            aria-label={t.groups.addBuild}
            placeholder={t.groups.addBuild}
            value={null}
            options={addable.map((build) => ({ value: build.BuildId, label: buildLabel(build) }))}
            onChange={(buildId) => change.mutate({ add: [buildId], remove: [] })}
          />
        </span>
      ) : null}
    </section>
  )
}

function GroupDetail({ context }: { context: WorkspaceContext }) {
  const t = useTestFlightText()
  const confirm = useConfirm()
  const written = useStoreWrite()
  const { groupId: rawId = '' } = useParams()
  const groupId = Number(rawId)
  const [page, setPage] = useState(1)
  const [size, setSize] = useState(DEFAULT_TESTER_PAGE)
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState(false)
  const groups = useGroups(context)
  const builds = useBuilds(context)
  const request: ListRequest = { PageNumber: page, RowCount: size, Sorting: { SortBy: 'Email', Type: SortType.Asc } }
  const testers = useQuery({
    queryKey: testFlightKeys.testers(context.organizationId, context.record.StoreAppId, groupId, request),
    queryFn: () => testFlightApi.testers(context.organizationId, context.record.StoreAppId, groupId, request),
    placeholderData: keepPreviousData,
    enabled: Number.isInteger(groupId) && groupId > 0,
  })
  const remove = useMutation({
    mutationFn: (testerId: number) => testFlightApi.updateGroupTesters(context.organizationId, groupId, [], [testerId]),
    onSuccess: (jobId) => written(jobId, t.testers.removed),
  })
  const canWrite = context.can(OrgPermission.ReleaseTest)
  const back = (
    <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to={BASE(context.app.Id)}>
      <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
      {t.title}
    </Link>
  )
  if (groups.isLoading) {
    return <CardSkeleton />
  }
  const group = groups.data?.find((item) => item.Id === groupId)
  if (groups.isError) {
    return <ScreenState error={groups.error} onRetry={() => void groups.refetch()} back={back} />
  }
  if (!group) {
    return <NotFound back={back} />
  }

  const columns: Column<BetaTesterDto>[] = [
    {
      key: 'name',
      header: t.testers.name,
      role: 'main',
      cell: (tester) => [tester.FirstName, tester.LastName].filter(Boolean).join(' ') || '-',
    },
    { key: 'email', header: t.testers.email, cell: (tester) => tester.Email ?? '-' },
    {
      key: 'source',
      header: t.testers.source,
      cell: (tester) => (tester.InviteType === BetaInviteType.PublicLink ? t.testers.viaLink : t.testers.viaEmail),
    },
    { key: 'state', header: t.testers.state, cell: (tester) => <Badge small>{testerStateLabel(tester.State, t)}</Badge> },
    {
      key: 'actions',
      header: t.testers.actions,
      role: 'action',
      cell: (tester) =>
        canWrite ? (
          <IconButton
            label={interpolate(t.testers.remove, { email: tester.Email ?? '' })}
            size="sm"
            ghost
            onClick={async () => {
              if (
                await confirm({
                  title: interpolate(t.testers.removeTitle, { email: tester.Email ?? '' }),
                  message: t.testers.removeBody,
                  danger: true,
                  confirmLabel: t.testers.removeConfirm,
                })
              ) {
                remove.mutate(tester.Id)
              }
            }}
          >
            <Trash2 aria-hidden />
          </IconButton>
        ) : null,
    },
  ]
  const rows = testers.data?.Data ?? []

  return (
    <>
      {back}
      <PageHeader
        title={group.Name ?? '-'}
        badge={
          <Badge small tone={group.IsInternal ? 'neutral' : 'accent'}>
            {group.IsInternal ? t.groups.internal : t.groups.external}
          </Badge>
        }
        subtitle={interpolate(t.groups.testerCount, { count: group.TesterCount })}
        actions={
          canWrite ? (
            <>
              <Button onClick={() => setEditing(true)}>
                <Pencil aria-hidden />
                {t.groups.edit}
              </Button>
              <Button variant="primary" onClick={() => setAdding(true)}>
                <Plus aria-hidden />
                {t.testers.add}
              </Button>
            </>
          ) : null
        }
      />
      <div className="grid gap-3 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div className="grid content-start gap-2">
          {testers.isLoading ? (
            <CardSkeleton />
          ) : testers.isError ? (
            <ScreenState error={testers.error} onRetry={() => void testers.refetch()} />
          ) : rows.length === 0 ? (
            <EmptyState icon={<Users aria-hidden />} title={t.testers.emptyTitle} description={t.testers.emptyBody} />
          ) : (
            <>
              <DataTable caption={t.testers.title} columns={columns} rows={rows} rowKey={(tester) => tester.Id} busy={testers.isFetching} />
              <Pagination
                page={page}
                pageCount={testers.data?.TotalPageCount ?? 1}
                total={testers.data?.TotalDataCount ?? rows.length}
                pageSize={size}
                onPage={setPage}
                onPageSize={(value) => {
                  setSize(value)
                  setPage(1)
                }}
              />
            </>
          )}
        </div>
        <div className="grid content-start gap-3">
          {!group.IsInternal ? (
            <section className="card grid gap-1.5 p-4" aria-label={t.groups.publicLink}>
              <h2 className="text-[15px] font-semibold">{t.groups.publicLink}</h2>
              {group.PublicLinkEnabled && group.PublicLink ? (
                <>
                  <code className="mono break-all text-[13px]">{group.PublicLink}</code>
                  {group.PublicLinkLimitEnabled && group.PublicLinkLimit ? (
                    <span className="text-xs text-fg3">{interpolate(t.groups.limitValue, { limit: group.PublicLinkLimit })}</span>
                  ) : null}
                  <span className="text-xs text-fg3">{t.groups.linkCloseNote}</span>
                </>
              ) : (
                <span className="text-[13px] text-fg3">{t.groups.linkOff}</span>
              )}
            </section>
          ) : null}
          <GroupBuilds context={context} group={group} builds={builds.data ?? []} />
        </div>
      </div>
      {adding ? <AddTesterDialog context={context} group={group} onClose={() => setAdding(false)} /> : null}
      {editing ? <GroupDialog context={context} group={group} onClose={() => setEditing(false)} /> : null}
    </>
  )
}

/** /apps/:appId/release/testflight/groups/:groupId. */
export function TestFlightGroupPage() {
  return (
    <AppWorkspace section="release" sub="testflight">
      {(context) => <GroupDetail context={context} />}
    </AppWorkspace>
  )
}

// ---- Build ------------------------------------------------------------------------------------------------------------

function WhatToTest({ context, build, locales }: { context: WorkspaceContext; build: BetaBuildDto; locales: string[] }) {
  const t = useTestFlightText()
  const written = useStoreWrite()
  const known = [...new Set([...(build.WhatToTest ?? []).map((item) => item.Locale ?? ''), ...locales].filter(Boolean))]
  const [locale, setLocale] = useState(known[0] ?? 'en-US')
  const [texts, setTexts] = useState<Record<string, string>>(
    Object.fromEntries((build.WhatToTest ?? []).map((item) => [item.Locale ?? '', item.Text ?? ''])),
  )
  const text = texts[locale] ?? ''
  const saved = (build.WhatToTest ?? []).find((item) => item.Locale === locale)?.Text ?? ''
  const editable = !build.Expired && context.can(OrgPermission.ReleaseTest)
  const save = useMutation({
    mutationFn: () => testFlightApi.updateWhatToTest(context.organizationId, build.BuildId, locale, text),
    onSuccess: (jobId) => written(jobId, t.whatToTest.saved),
  })
  return (
    <section className="card grid gap-2 p-4" aria-label={t.whatToTest.title}>
      <h2 className="text-[15px] font-semibold">{t.whatToTest.title}</h2>
      <div className="toolbar" role="group" aria-label={t.whatToTest.locales}>
        {(known.length > 0 ? known : [locale]).map((item) => (
          <Chip key={item} selected={item === locale} onClick={() => setLocale(item)}>
            {item}
          </Chip>
        ))}
      </div>
      {editable ? (
        <>
          <Field
            label={interpolate(t.whatToTest.label, { locale })}
            counter={{ value: text.length, max: WHAT_TO_TEST_MAX }}
            error={text.length > WHAT_TO_TEST_MAX ? t.whatToTest.over : null}
          >
            <Textarea rows={6} value={text} onChange={(event) => setTexts((all) => ({ ...all, [locale]: event.target.value }))} />
          </Field>
          <div className="flex justify-end">
            <Button
              variant="primary"
              loading={save.isPending}
              disabled={text === saved || text.length > WHAT_TO_TEST_MAX}
              onClick={() => save.mutate()}
            >
              {t.whatToTest.save}
            </Button>
          </div>
        </>
      ) : (
        <p className="whitespace-pre-wrap text-[13px]">{text || '-'}</p>
      )}
    </section>
  )
}

function BetaReview({ context, build }: { context: WorkspaceContext; build: BetaBuildDto }) {
  const t = useTestFlightText()
  const confirm = useConfirm()
  const written = useStoreWrite()
  const label = buildLabel(build)
  const submit = useMutation({
    mutationFn: () => testFlightApi.submitBetaReview(context.organizationId, build.BuildId, label),
    onSuccess: (jobId) => written(jobId, t.review.submitted),
  })
  const canSubmit =
    context.can(OrgPermission.ReleaseTest) &&
    !build.Expired &&
    build.ProcessingState === BuildProcessingState.Valid &&
    (build.BetaReviewState === BetaReviewState.NotSubmitted || build.BetaReviewState === BetaReviewState.Rejected)
  return (
    <section className="card grid gap-2 p-4" aria-label={t.review.title}>
      <h2 className="text-[15px] font-semibold">{t.review.title}</h2>
      <div>
        <ReviewBadge state={build.BetaReviewState} />
      </div>
      <p className="text-[13px] text-fg2">{build.BetaReviewState === BetaReviewState.Approved ? t.review.approvedNote : t.review.note}</p>
      {canSubmit ? (
        <div>
          <Button
            variant="primary"
            loading={submit.isPending}
            onClick={async () => {
              if (
                await confirm({
                  title: interpolate(t.review.confirmTitle, { build: label }),
                  message: t.review.confirmBody,
                  confirmLabel: t.review.submit,
                  typeToConfirm: label,
                })
              ) {
                submit.mutate()
              }
            }}
          >
            {t.review.submit}
          </Button>
        </div>
      ) : null}
    </section>
  )
}

function BuildDetail({ context }: { context: WorkspaceContext }) {
  const t = useTestFlightText()
  const format = useFormat()
  const { buildId: rawId = '' } = useParams()
  const buildId = Number(rawId)
  const builds = useBuilds(context)
  const groups = useGroups(context)
  const localizations = useQuery({
    queryKey: testFlightKeys.localizations(context.organizationId, context.record.StoreAppId),
    queryFn: () => testFlightApi.localizations(context.organizationId, context.record.StoreAppId),
  })
  const written = useStoreWrite()
  const addToGroup = useMutation({
    mutationFn: (groupId: number) => testFlightApi.updateGroupBuilds(context.organizationId, groupId, [buildId], []),
    onSuccess: (jobId) => written(jobId),
  })
  const back = (
    <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to={BASE(context.app.Id)}>
      <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
      {t.title}
    </Link>
  )
  if (builds.isLoading) {
    return <CardSkeleton />
  }
  const build = builds.data?.find((item) => item.BuildId === buildId)
  if (builds.isError) {
    return <ScreenState error={builds.error} onRetry={() => void builds.refetch()} back={back} />
  }
  if (!build) {
    return <NotFound back={back} />
  }
  const groupList = groups.data ?? []
  const memberOf = groupList.filter((group) => build.GroupIds?.includes(group.Id) || group.BuildIds?.includes(build.BuildId))
  const others = groupList.filter((group) => !memberOf.includes(group))
  const canWrite = context.can(OrgPermission.ReleaseTest) && !build.Expired

  return (
    <>
      {back}
      <PageHeader
        title={buildLabel(build)}
        badge={build.Expired ? <Badge small>{t.builds.expired}</Badge> : null}
        subtitle={interpolate(t.builds.detailSubtitle, {
          uploaded: build.UploadedDate ? format.date(build.UploadedDate) : '-',
          expires: build.ExpirationDate ? format.date(build.ExpirationDate) : '-',
          groups: memberOf.length,
        })}
      />
      {build.Expired ? (
        <div className="mb-3">
          <Callout tone="neutral" title={t.builds.expiredTitle}>
            {t.builds.expiredBody}
          </Callout>
        </div>
      ) : null}
      <div className="grid gap-3 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <WhatToTest context={context} build={build} locales={(localizations.data ?? []).map((item) => item.Locale ?? '')} />
        <div className="grid content-start gap-3">
          <BetaReview context={context} build={build} />
          <section className="card grid gap-2 p-4" aria-label={t.builds.groups}>
            <h2 className="text-[15px] font-semibold">{t.builds.groups}</h2>
            <span className="text-[13px]">{memberOf.map((group) => group.Name).join(', ') || '-'}</span>
            {canWrite && others.length > 0 ? (
              <Select<number>
                aria-label={t.builds.addToGroup}
                placeholder={t.builds.addToGroup}
                value={null}
                options={others.map((group) => ({ value: group.Id, label: group.Name ?? `#${group.Id}` }))}
                onChange={(groupId) => addToGroup.mutate(groupId)}
              />
            ) : null}
          </section>
        </div>
      </div>
    </>
  )
}

/** /apps/:appId/release/testflight/builds/:buildId. */
export function TestFlightBuildPage() {
  return (
    <AppWorkspace section="release" sub="testflight">
      {(context) => <BuildDetail context={context} />}
    </AppWorkspace>
  )
}
