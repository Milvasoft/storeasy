import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ArrowLeft, ClipboardCheck } from 'lucide-react'
import { Badge, StoreTag, Tag } from '@/components/ui/Badge'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { ScreenState } from '@/components/ui/ScreenState'
import { Chip, EmptyState, PageHeader, PageSkeleton, TableSkeleton } from '@/components/ui/primitives'
import type { ListRequest } from '@/lib/api/list'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { ActivityTabs } from '@/pages/ActivityPage'
import { plansApi } from '@/features/common/api'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { channelLabel, permissionLabel, planKindLabel, planStatusView } from '@/features/common/labels'
import { PlanActions, PlanBody } from '@/features/common/PlanView'
import { ChangePlanStatus, type ChangePlanListDto } from '@/features/common/types'

// Design 11.8-11.11: the approval queue (pending plans by default) and one plan with its diff and decision.

const PAGE_SIZE = 20

export function PlansPage() {
  const t = useP2()
  const base = useT()
  const format = useFormat()
  const navigate = useNavigate()
  const organization = useCurrentOrganization()
  const [pendingOnly, setPendingOnly] = useState(true)
  const [request, setRequest] = useState<ListRequest>({ PageNumber: 1, RowCount: PAGE_SIZE })
  const status = pendingOnly ? ChangePlanStatus.Pending : null
  const plans = useQuery({
    queryKey: contentKeys.plans(organization.Id, request, status),
    queryFn: () => plansApi.list(organization.Id, request, status),
    placeholderData: keepPreviousData,
  })

  const columns: Column<ChangePlanListDto>[] = [
    {
      key: 'plan',
      header: t.plans.plan,
      role: 'main',
      cell: (plan) => (
        <span className="grid gap-0.5">
          <Link className="lnk font-semibold" to={`/activity/plans/${plan.PlanId}`} onClick={(event) => event.stopPropagation()}>
            {plan.Title ?? planKindLabel(plan.Kind, t)}
          </Link>
          <span className="text-xs text-fg3">
            {plan.CreatedByUserName ?? '-'} · {channelLabel(plan.Source, t)} · {interpolate(t.plans.items, { count: plan.ItemCount })}
            {plan.WarningCount > 0 ? ` · ${interpolate(t.plans.warnings, { count: plan.WarningCount })}` : ''}
          </span>
        </span>
      ),
    },
    {
      key: 'app',
      header: t.plans.app,
      cell: (plan) => (
        <span className="flex flex-wrap items-center gap-2">
          <StoreTag store={plan.Store} small codeOnly />
          {plan.AppName ?? '-'}
        </span>
      ),
    },
    { key: 'needs', header: t.plans.needs, cell: (plan) => <Tag>{permissionLabel(plan.RequiredPermission, t)}</Tag> },
    {
      key: 'created',
      header: t.plans.created,
      role: 'hidePhone',
      cell: (plan) => <span className="tabular-nums">{format.dateTime(plan.CreationDate)}</span>,
    },
    {
      key: 'status',
      header: t.plans.status,
      cell: (plan) => {
        const view = planStatusView(plan.Status, t)
        return (
          <span className="grid gap-0.5">
            <Badge tone={view.tone} dot small>
              {view.label}
            </Badge>
            {plan.Status === ChangePlanStatus.Pending ? (
              <span className="text-xs text-fg3">{interpolate(t.plan.expires, { date: format.dateTime(plan.ExpiryDate) })}</span>
            ) : null}
          </span>
        )
      },
    },
  ]

  const rows = plans.data?.Data ?? []
  return (
    <>
      <PageHeader title={base.activity.title} subtitle={base.activity.subtitle} />
      <ActivityTabs />
      <p className="my-3 text-[13px] text-fg2" data-tour="plans">
        {t.plans.subtitle}
      </p>
      <div className="toolbar mb-3" role="group" aria-label={t.plans.status}>
        <Chip
          selected={pendingOnly}
          onClick={() => {
            setPendingOnly(true)
            setRequest((current) => ({ ...current, PageNumber: 1 }))
          }}
        >
          {t.plans.pending}
        </Chip>
        <Chip
          selected={!pendingOnly}
          onClick={() => {
            setPendingOnly(false)
            setRequest((current) => ({ ...current, PageNumber: 1 }))
          }}
        >
          {t.plans.all}
        </Chip>
      </div>
      {plans.isLoading ? (
        <TableSkeleton columns={[t.plans.plan, t.plans.app, t.plans.needs, t.plans.status]} label={t.plans.title} />
      ) : plans.isError ? (
        <ScreenState error={plans.error} onRetry={() => void plans.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={<ClipboardCheck aria-hidden />} title={t.plans.emptyTitle} description={t.plans.emptyBody} />
      ) : (
        <>
          <DataTable
            caption={t.plans.title}
            columns={columns}
            rows={rows}
            rowKey={(plan) => plan.PlanId}
            busy={plans.isFetching}
            onRowClick={(plan) => navigate(`/activity/plans/${plan.PlanId}`)}
          />
          <Pagination
            page={plans.data?.CurrentPageNumber ?? request.PageNumber}
            pageCount={plans.data?.TotalPageCount ?? 1}
            total={plans.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            onPage={(page) => setRequest((current) => ({ ...current, PageNumber: page }))}
            onPageSize={(size) => setRequest({ PageNumber: 1, RowCount: size })}
          />
        </>
      )}
    </>
  )
}

export function PlanDetailPage() {
  const t = useP2()
  const organization = useCurrentOrganization()
  const { planId: rawId = '' } = useParams()
  const planId = Number(rawId)
  const plan = useQuery({
    queryKey: contentKeys.plan(organization.Id, planId),
    queryFn: () => plansApi.detail(organization.Id, planId),
    enabled: Number.isInteger(planId) && planId > 0,
  })
  const back = (
    <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to="/activity/plans">
      <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
      {t.plans.back}
    </Link>
  )
  if (plan.isLoading) {
    return <PageSkeleton />
  }
  if (plan.isError || !plan.data) {
    return (
      <>
        {back}
        <ScreenState error={plan.error} onRetry={() => void plan.refetch()} />
      </>
    )
  }
  const data = plan.data
  return (
    <>
      {back}
      <PageHeader title={data.Title ?? planKindLabel(data.Kind, t)} subtitle={data.AppName ?? undefined} />
      <div className="card">
        <div className="card-b">
          <PlanBody plan={data} />
        </div>
        {data.Status === ChangePlanStatus.Pending ? (
          <div className="card-f">
            <PlanActions plan={data} onDone={() => void plan.refetch()} />
          </div>
        ) : null}
      </div>
    </>
  )
}
