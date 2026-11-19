import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { RefreshCw, Send } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, EmptyState, PageHeader } from '@/components/ui/primitives'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { appContentApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { readinessView } from '@/features/common/labels'
import { fixRouteHref, storeSlug } from '@/features/common/store'
import { SubmitDialog } from '@/features/common/SubmitDialog'
import { PreReviewEntry } from '@/features/pre-review/PreReviewEntry'
import { MediaSlotFill, ReadinessStatus, type AppOverviewDto, type ReadinessItemDto, type ReadinessSlotDto } from '@/features/common/types'

// Design 6.8-6.11: "what is missing before a submission", one row per catalog check, grouped; the submission gate opens
// when nothing blocks and only for SubmitForReview.

const BLOCKING: ReadinessStatus[] = [ReadinessStatus.Missing]

function SlotList({ slots }: { slots: ReadinessSlotDto[] }) {
  const t = useP2()
  return (
    <ul className="m-0 mt-1.5 flex list-none flex-wrap gap-1.5 p-0" aria-label={t.readiness.slots}>
      {slots.map((slot) => {
        const text =
          slot.FilledBy === MediaSlotFill.Own
            ? interpolate(t.readiness.slotOwn, { count: slot.Count })
            : slot.FilledBy === MediaSlotFill.Fallback
              ? interpolate(t.readiness.slotFallback, { key: slot.FallbackFromClassKey ?? '-' })
              : slot.Required
                ? t.readiness.slotNone
                : t.readiness.slotOptional
        const tone =
          slot.FilledBy === MediaSlotFill.None
            ? slot.Required
              ? 'danger'
              : 'neutral'
            : slot.FilledBy === MediaSlotFill.Fallback
              ? 'info'
              : 'success'
        return (
          <li key={slot.ClassKey}>
            <Badge small tone={tone}>
              <span className="mono">{slot.ClassKey}</span> · {text}
            </Badge>
          </li>
        )
      })}
    </ul>
  )
}

function ReadinessRow({ item, appId, store }: { item: ReadinessItemDto; appId: number; store: StoreKind }) {
  const t = useP2()
  const format = useFormat()
  const view = readinessView(item.Status, t)
  return (
    <li className="ck-i items-start">
      <div className="min-w-0 flex-1">
        <b className="text-[13.5px]">
          {item.Title ?? item.Key}
          {item.Locale ? <span className="mono ml-2 text-xs text-fg3">{item.Locale}</span> : null}
        </b>
        {item.Message ? <div className="text-[12.5px] text-fg2">{item.Message}</div> : null}
        <div className="mt-0.5 flex flex-wrap gap-2 text-xs text-fg3">
          {item.RuleId ? <span className="mono">{item.RuleId}</span> : null}
          {item.DueDate ? <span>{interpolate(t.readiness.due, { date: format.date(item.DueDate) })}</span> : null}
        </div>
        {item.Slots && item.Slots.length > 0 ? <SlotList slots={item.Slots} /> : null}
      </div>
      <div className="flex flex-none flex-col items-end gap-1.5">
        <Badge tone={view.tone} dot>
          {view.label}
        </Badge>
        {item.FixRoute && item.Status !== ReadinessStatus.Ok ? (
          <Link className="lnk text-[13px]" to={fixRouteHref(appId, store, item.FixRoute)}>
            {t.readiness.fix}
          </Link>
        ) : null}
      </div>
    </li>
  )
}

function Readiness({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const format = useFormat()
  const queryClient = useQueryClient()
  const { app, organizationId, store, can } = context
  const [submitting, setSubmitting] = useState(false)
  const key = contentKeys.readiness(organizationId, app.Id, store)
  const report = useQuery({ queryKey: key, queryFn: () => appContentApi.readiness(organizationId, app.Id, store) })

  if (report.isLoading) {
    return <CardSkeleton />
  }
  if (report.isError || !report.data) {
    return <ScreenState error={report.error} onRetry={() => void report.refetch()} />
  }
  const data = report.data
  const items = data.Items ?? []
  const counted = items.filter((item) => item.Status !== ReadinessStatus.NotApplicable)
  const blockers = items.filter((item) => BLOCKING.includes(item.Status)).length
  const groups = [...new Set(items.map((item) => item.Group ?? '-'))]
  const canSubmit = can(OrgPermission.SubmitForReview)
  const gateReason =
    store !== StoreKind.Apple
      ? t.readiness.submitAppleOnly
      : !canSubmit
        ? t.readiness.submitNoPermission
        : !data.CanSubmit
          ? t.readiness.submitBlocked
          : null

  return (
    <>
      <PageHeader
        title={t.readiness.title}
        subtitle={t.readiness.subtitle}
        badge={
          <Badge tone={blockers > 0 ? 'danger' : 'success'} dot>
            {blockers > 0 ? interpolate(t.readiness.blockers, { count: blockers }) : t.readiness.noBlockers}
          </Badge>
        }
        actions={
          <Button loading={report.isFetching} onClick={() => void queryClient.invalidateQueries({ queryKey: key })}>
            <RefreshCw aria-hidden />
            {t.readiness.recheck}
          </Button>
        }
      />
      <p className="mb-3 text-xs text-fg3">
        {interpolate(t.readiness.summary, { ok: data.Ok, total: counted.length })} ·{' '}
        {interpolate(t.readiness.checked, { date: format.dateTime(data.CheckedDate), version: data.CatalogVersion ?? '-' })}
        {data.VersionString ? <span className="mono"> · {data.VersionString}</span> : null}
      </p>
      <div className="grid gap-4">
        {groups.map((group) => (
          <section key={group} className="card">
            <div className="card-h">
              <h2 className="h3">{group}</h2>
            </div>
            <ul className="m-0 grid list-none px-4 py-1">
              {items
                .filter((item) => (item.Group ?? '-') === group)
                .map((item) => (
                  <ReadinessRow key={`${item.Key}-${item.Locale ?? ''}`} item={item} appId={app.Id} store={store} />
                ))}
            </ul>
          </section>
        ))}
      </div>
      <div className="card mt-4">
        <div className="card-b flex flex-wrap items-center gap-3">
          <div className="grid min-w-0 flex-1 gap-1.5">
            <div className="text-[13px] text-fg2">{gateReason ?? t.readiness.noBlockers}</div>
            <PreReviewEntry organizationId={organizationId} appId={app.Id} store={store} />
          </div>
          <Button variant="primary" disabled={!!gateReason} onClick={() => setSubmitting(true)}>
            <Send aria-hidden />
            {t.readiness.submit}
          </Button>
        </div>
      </div>
      <SubmitDialog
        open={submitting}
        onClose={() => setSubmitting(false)}
        organizationId={organizationId}
        storeAppId={data.StoreAppId}
        appName={app.Name}
        versionId={data.VersionId ?? null}
        versionString={data.VersionString ?? null}
        appId={app.Id}
        store={store}
      />
    </>
  )
}

export function ReadinessPage() {
  return <AppWorkspace section="readiness">{(context) => <Readiness context={context} />}</AppWorkspace>
}

// ---- Organization overview ----------------------------------------------------------------------------------------------
export function ReadinessOverviewPage() {
  const t = useP2()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [refresh, setRefresh] = useState(false)
  const overview = useQuery({
    queryKey: [...contentKeys.overview(organization.Id), refresh],
    queryFn: () => appContentApi.overview(organization.Id, refresh),
  })

  const columns: Column<AppOverviewDto>[] = [
    {
      key: 'app',
      header: t.workspace.readiness,
      role: 'main',
      cell: (row) => (
        <Link className="lnk font-semibold" to={`/apps/${row.AppId}/readiness`}>
          {row.Name}
        </Link>
      ),
    },
    {
      key: 'stores',
      header: t.workspace.store,
      cell: (row) => (
        <ul className="m-0 grid list-none gap-1 p-0">
          {(row.Stores ?? []).map((store) => (
            <li key={store.StoreAppId} className="flex flex-wrap items-center gap-2">
              <StoreTag store={store.Store} small codeOnly />
              {store.Error ? (
                <span className="text-danger">{interpolate(t.readiness.error, { error: store.Error })}</span>
              ) : !store.CheckedDate ? (
                <span className="text-fg3">{t.readiness.neverChecked}</span>
              ) : (
                <>
                  <Badge small tone={store.Missing > 0 ? 'danger' : 'success'}>
                    {store.Missing > 0
                      ? interpolate(t.readiness.missingCount, { count: store.Missing })
                      : interpolate(t.readiness.summary, {
                          ok: store.Ok,
                          total: store.Ok + store.Warning + store.Assisted + store.Unknown,
                        })}
                  </Badge>
                  <Link className="lnk text-xs" to={`/apps/${row.AppId}/readiness?store=${storeSlug(store.Store)}`}>
                    {t.readiness.open}
                  </Link>
                </>
              )}
            </li>
          ))}
        </ul>
      ),
    },
    {
      key: 'deadline',
      header: t.readiness.nextDeadline,
      cell: (row) =>
        row.NextDeadline ? (
          <span>
            <span className="tabular-nums">{format.date(row.NextDeadline.Date)}</span>
            <span className="block text-xs text-fg3">{row.NextDeadline.Title}</span>
          </span>
        ) : (
          '-'
        ),
    },
  ]

  return (
    <>
      <PageHeader
        title={t.readiness.overviewTitle}
        subtitle={t.readiness.overviewSubtitle}
        actions={
          <Button
            loading={overview.isFetching && refresh}
            onClick={() => {
              setRefresh(true)
              void queryClient.invalidateQueries({ queryKey: contentKeys.overview(organization.Id) })
            }}
          >
            <RefreshCw aria-hidden />
            {t.readiness.overviewRefresh}
          </Button>
        }
      />
      {overview.isLoading ? (
        <CardSkeleton />
      ) : overview.isError ? (
        <ScreenState error={overview.error} onRetry={() => void overview.refetch()} />
      ) : (overview.data ?? []).length === 0 ? (
        <EmptyState title={t.readiness.overviewEmpty} />
      ) : (
        <DataTable
          caption={t.readiness.overviewTitle}
          columns={columns}
          rows={overview.data ?? []}
          rowKey={(row) => row.AppId}
          busy={overview.isFetching}
        />
      )}
    </>
  )
}
