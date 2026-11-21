import { Link } from 'react-router-dom'
import { AlertTriangle } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { ScreenState } from '@/components/ui/ScreenState'
import { CardSkeleton } from '@/components/ui/primitives'
import { useFormat, useI18n } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { formatMetric } from '@/features/store-metrics/labels'
import { useRecentImpact } from './api'
import { ChangeArrow } from './ChangeArrow'
import { useImpactText } from './i18n'
import { eventTitle, impactLink, signalView, statusView } from './labels'
import { ImpactStatus, type AsoImpactMetricDto } from './types'

// "Son değişikliklerin etkisi": the organization's changes of the last 60 days with their headline numbers, on the
// organization's Store performance page.

function Cell({ metric, rate }: { metric: AsoImpactMetricDto | null; rate?: boolean }) {
  const { lang } = useI18n()
  if (!metric || (metric.Before == null && metric.After == null)) {
    return <span className="text-fg3">-</span>
  }
  return (
    <span className="grid justify-items-end gap-0.5">
      <span className="tabular-nums">{formatMetric(rate ? 'ConversionRate' : 'Downloads', metric.After, lang)}</span>
      <ChangeArrow change={metric.RelativeChange} />
    </span>
  )
}

export function RecentImpact({ organizationId }: { organizationId: number }) {
  const t = useImpactText()
  const format = useFormat()
  const recent = useRecentImpact(organizationId)
  const data = recent.data

  return (
    <section className="card" aria-label={t.recent.title}>
      <div className="card-h grid gap-1">
        <h2 className="h3">{t.recent.title}</h2>
        {data ? <span className="text-xs text-fg3">{interpolate(t.recent.subtitle, { days: data.WindowDays })}</span> : null}
      </div>
      {recent.isLoading ? (
        <div className="card-b">
          <CardSkeleton count={1} />
        </div>
      ) : recent.isError || !data ? (
        <div className="card-b">
          <ScreenState error={recent.error} onRetry={() => void recent.refetch()} />
        </div>
      ) : data.Items.length === 0 ? (
        <div className="card-b text-[13px] text-fg2">{t.recent.empty}</div>
      ) : (
        <div className="tbl-w">
          <table className="tbl">
            <thead>
              <tr>
                <th scope="col">{t.recent.change}</th>
                <th scope="col">{t.recent.date}</th>
                <th scope="col" className="text-right">
                  {t.recent.pageViews}
                </th>
                <th scope="col" className="text-right">
                  {t.recent.downloads}
                </th>
                <th scope="col" className="text-right">
                  {t.recent.conversion}
                </th>
              </tr>
            </thead>
            <tbody>
              {data.Items.map((item) => {
                const title = eventTitle(item.Event, t)
                const signal = signalView(item.Signal, t)
                const status = statusView(item.Status, t)
                return (
                  <tr key={item.Event.Id}>
                    <th scope="row" className="font-normal">
                      <span className="grid gap-1">
                        <span className="flex flex-wrap items-center gap-2">
                          <StoreTag store={item.Event.Store} small />
                          <Link
                            className="lnk"
                            to={impactLink(item.Event)}
                            aria-label={interpolate(t.recent.open, { title: `${item.Event.AppName ?? ''} ${title}`.trim() })}
                          >
                            {item.Event.AppName ? `${item.Event.AppName}: ${title}` : title}
                          </Link>
                        </span>
                        <span className="flex flex-wrap items-center gap-1.5">
                          {item.Status !== ImpactStatus.Ready ? (
                            <Badge small tone={status.tone}>
                              {status.label}
                            </Badge>
                          ) : null}
                          {item.Confounded ? (
                            <span className="inline-flex items-center gap-1 text-xs text-warningText">
                              <AlertTriangle className="h-3.5 w-3.5" aria-hidden />
                              {t.recent.confounded}
                            </span>
                          ) : null}
                        </span>
                      </span>
                    </th>
                    <td className="whitespace-nowrap">{format.date(item.Event.Date)}</td>
                    <td className="text-right">
                      <Cell metric={item.PageViews} />
                    </td>
                    <td className="text-right">
                      <Cell metric={item.Downloads} />
                    </td>
                    <td className="text-right">
                      <span className="grid justify-items-end gap-1">
                        <Cell metric={item.ConversionRate} rate />
                        <Badge small tone={signal.tone}>
                          {signal.label}
                        </Badge>
                      </span>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
