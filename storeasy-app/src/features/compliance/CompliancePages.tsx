import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CalendarClock } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { CardSkeleton, Chip, EmptyState, PageHeader } from '@/components/ui/primitives'
import { useFormat, useI18n } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { cultureOf } from '@/lib/i18n/lang'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { parseDateOnly } from '@/lib/utils/date'
import { appContentApi, complianceApi } from '@/features/common/api'
import { AppWorkspace } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { complianceView } from '@/features/common/labels'
import { ComplianceStatus, type ComplianceItemDto } from '@/features/common/types'

// Design 6.12 (one app) and 6.13 (the organization, grouped by month): catalog deadlines with their state, the days left
// in a warning tone under 30 days and in danger once passed, the source and its check date, a note when sources disagree.

const SOON_DAYS = 30

function DaysLeft({ days }: { days: number }) {
  const t = useP2()
  const tone = days < 0 ? 'danger' : days <= SOON_DAYS ? 'warning' : 'neutral'
  const text =
    days < 0
      ? interpolate(t.compliance.overdue, { days: -days })
      : days === 0
        ? t.compliance.today
        : interpolate(t.compliance.daysLeft, { days })
  return (
    <Badge small tone={tone}>
      {text}
    </Badge>
  )
}

function ComplianceRow({ item, showApp }: { item: ComplianceItemDto; showApp: boolean }) {
  const t = useP2()
  const format = useFormat()
  const view = complianceView(item.Status, t)
  return (
    <li className="card">
      <div className="card-b grid gap-1.5">
        <div className="flex flex-wrap items-center gap-2">
          <StoreTag store={item.Store} small codeOnly />
          <b className="min-w-0 flex-1 text-[14px]">{item.Title ?? item.RuleId}</b>
          {typeof item.DaysLeft === 'number' && item.Status !== ComplianceStatus.Done ? <DaysLeft days={item.DaysLeft} /> : null}
          <Badge tone={view.tone} dot>
            {view.label}
          </Badge>
        </div>
        {showApp && item.AppName ? (
          <Link className="lnk w-fit text-[13px]" to={`/apps/${item.AppId}/compliance`}>
            {item.AppName}
          </Link>
        ) : null}
        {item.Summary ? <p className="m-0 text-[13px] text-fg2">{item.Summary}</p> : null}
        {item.Conflict ? (
          <Callout tone="warning" title={t.compliance.conflict}>
            {item.Conflict}
          </Callout>
        ) : null}
        {item.MatchReason && item.MatchReason.length > 0 ? (
          <p className="m-0 text-xs text-fg3">
            {t.compliance.why}: {item.MatchReason.join(', ')}
          </p>
        ) : null}
        <div className="flex flex-wrap items-center gap-2 text-xs text-fg3">
          <span className="tabular-nums">{item.DueDate ? format.date(item.DueDate) : t.compliance.noDate}</span>
          <span className="mono">{item.RuleId}</span>
          {item.CheckedDate ? <span>{interpolate(t.compliance.checked, { date: format.date(item.CheckedDate) })}</span> : null}
          {item.SourceUrl ? (
            <a className="lnk" href={item.SourceUrl} target="_blank" rel="noreferrer noopener">
              {t.compliance.source}
            </a>
          ) : null}
          {item.AssistedStepId ? (
            <Link className="lnk" to={`/apps/${item.AppId}/assisted/${item.AssistedStepId}`}>
              {t.compliance.assisted}
            </Link>
          ) : null}
        </div>
      </div>
    </li>
  )
}

// Nearest first; items without a date at the end.
function sortItems(items: ComplianceItemDto[]): ComplianceItemDto[] {
  return [...items].sort((a, b) => (a.DueDate ?? '9999').localeCompare(b.DueDate ?? '9999'))
}

function ComplianceList({ items, showApp, byMonth }: { items: ComplianceItemDto[]; showApp: boolean; byMonth: boolean }) {
  const t = useP2()
  const { lang } = useI18n()
  if (items.length === 0) {
    return <EmptyState icon={<CalendarClock aria-hidden />} title={t.compliance.emptyTitle} description={t.compliance.emptyBody} />
  }
  const sorted = sortItems(items)
  if (!byMonth) {
    return (
      <ul className="m-0 grid list-none gap-3 p-0">
        {sorted.map((item) => (
          <ComplianceRow key={`${item.AppId}-${item.RuleId}`} item={item} showApp={showApp} />
        ))}
      </ul>
    )
  }
  const monthOf = (item: ComplianceItemDto) => {
    const day = item.DueDate ? parseDateOnly(item.DueDate) : null
    return day ? day.toLocaleDateString(cultureOf(lang), { month: 'long', year: 'numeric' }) : t.compliance.noDate
  }
  const months = [...new Set(sorted.map(monthOf))]
  return (
    <div className="grid gap-5">
      {months.map((month) => (
        <section key={month} aria-label={month}>
          <h2 className="h3 mb-2">{month}</h2>
          <ul className="m-0 grid list-none gap-3 p-0">
            {sorted
              .filter((item) => monthOf(item) === month)
              .map((item) => (
                <ComplianceRow key={`${item.AppId}-${item.RuleId}`} item={item} showApp={showApp} />
              ))}
          </ul>
        </section>
      ))}
    </div>
  )
}

function AppCompliance({ organizationId, appId }: { organizationId: number; appId: number }) {
  const t = useP2()
  const items = useQuery({
    queryKey: contentKeys.appCompliance(organizationId, appId),
    queryFn: () => appContentApi.compliance(organizationId, appId),
  })
  return (
    <>
      <PageHeader title={t.compliance.appTitle} subtitle={t.compliance.appSubtitle} />
      {items.isLoading ? (
        <CardSkeleton />
      ) : items.isError ? (
        <ScreenState error={items.error} onRetry={() => void items.refetch()} />
      ) : (
        <ComplianceList items={items.data ?? []} showApp={false} byMonth={false} />
      )}
    </>
  )
}

export function AppCompliancePage() {
  return (
    <AppWorkspace section="compliance" appWide>
      {({ organizationId, app }) => <AppCompliance organizationId={organizationId} appId={app.Id} />}
    </AppWorkspace>
  )
}

export function OrgCompliancePage() {
  const t = useP2()
  const organization = useCurrentOrganization()
  const [openOnly, setOpenOnly] = useState(true)
  const items = useQuery({
    queryKey: contentKeys.orgCompliance(organization.Id, openOnly),
    queryFn: () => complianceApi.organization(organization.Id, { OpenOnly: openOnly }),
  })
  return (
    <>
      <PageHeader
        title={t.compliance.orgTitle}
        subtitle={t.compliance.orgSubtitle}
        actions={
          <Link className="btn" to="/apps/readiness">
            {t.readiness.overviewTitle}
          </Link>
        }
      />
      <div className="toolbar mb-3" role="group" aria-label={t.compliance.orgTitle}>
        <Chip selected={openOnly} onClick={() => setOpenOnly(true)}>
          {t.compliance.openOnly}
        </Chip>
        <Chip selected={!openOnly} onClick={() => setOpenOnly(false)}>
          {t.compliance.allItems}
        </Chip>
      </div>
      {items.isLoading ? (
        <CardSkeleton />
      ) : items.isError ? (
        <ScreenState error={items.error} onRetry={() => void items.refetch()} />
      ) : (
        <ComplianceList items={items.data ?? []} showApp byMonth />
      )}
    </>
  )
}
