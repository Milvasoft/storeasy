import { useQuery } from '@tanstack/react-query'
import { ExternalLink } from 'lucide-react'
import { Badge, type BadgeTone } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { CardSkeleton } from '@/components/ui/primitives'
import { MetricChart } from '@/features/store-metrics/MetricChart'
import type { DateOnly } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { registriesApi } from './api'
import { useRegistries } from './i18n/useRegistries'
import type { RegistriesDictionary } from './i18n/en'
import { registriesKeys } from './keys'
import { DownloadScope, ListingCheckStatus, ReadmeFormat } from './types'

// The Downloads and Listing tabs of a package page (docs/package-registries.md). The README is always shown as text:
// registry HTML is never rendered.

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="card card-b">
      <div className="text-fg3 text-[13px]">{label}</div>
      <div className="mono text-[20px] font-semibold">{value}</div>
    </div>
  )
}

/** Downloads and usage of a package: totals, the daily chart and the per version counts. */
export function DownloadsTab({ packageId }: { packageId: number }) {
  const t = useRegistries()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const data = useQuery({
    queryKey: registriesKeys.downloads(organization.Id, packageId),
    queryFn: () => registriesApi.downloads(organization.Id, packageId),
  })

  if (data.isPending) {
    return <CardSkeleton count={2} />
  }
  if (data.isError) {
    return <ScreenState error={data.error} onRetry={() => void data.refetch()} />
  }

  const downloads = data.data
  if (!downloads.Available) {
    return <Callout tone="info">{t.downloads.unavailable}</Callout>
  }

  const series = downloads.Series ?? []
  const versions = downloads.Versions ?? []
  const stats: Array<{ label: string; value: string }> = []
  if (downloads.TotalDownloads != null) {
    stats.push({ label: t.downloads.total, value: format.number(downloads.TotalDownloads) })
  }
  if (downloads.RecentDownloads != null) {
    stats.push({
      label: interpolate(t.downloads.recent, { days: downloads.RecentDownloadsDays ?? 30 }),
      value: format.number(downloads.RecentDownloads),
    })
  }
  if (downloads.Likes != null) {
    stats.push({ label: t.downloads.likes, value: format.number(downloads.Likes) })
  }
  if (downloads.Points != null && downloads.MaxPoints != null) {
    stats.push({ label: t.downloads.points, value: `${downloads.Points} / ${downloads.MaxPoints}` })
  }

  return (
    <section aria-label={t.tabs.downloads} className="grid gap-3">
      <p className="text-fg3">
        {downloads.DownloadsSyncedDate
          ? interpolate(t.downloads.synced, { when: format.relative(downloads.DownloadsSyncedDate) })
          : t.downloads.never}
      </p>
      {stats.length > 0 ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {stats.map((stat) => (
            <Stat key={stat.label} label={stat.label} value={stat.value} />
          ))}
        </div>
      ) : null}
      {series.length > 1 ? (
        <div className="card card-b">
          <MetricChart
            points={series.map((day) => ({ date: day.Day.slice(0, 10), value: day.Downloads }))}
            label={interpolate(t.downloads.chart, { from: format.date(series[0].Day), to: format.date(series[series.length - 1].Day) })}
            formatValue={(value) => format.number(value)}
            formatDate={(date: DateOnly) => format.date(date)}
          />
        </div>
      ) : (
        <Callout tone="info">{t.downloads.noSeries}</Callout>
      )}
      {versions.length > 0 ? (
        <section aria-label={t.downloads.versions} className="grid gap-2">
          <h3 className="text-[15px] font-semibold">
            {downloads.VersionScope === DownloadScope.LastWeek ? t.downloads.scopeLastWeek : t.downloads.scopeTotal}
          </h3>
          <ul className="grid gap-1">
            {versions.slice(0, 20).map((version) => (
              <li key={version.Version} className="flex justify-between gap-2">
                <span className="mono">{version.Version}</span>
                <span className="mono">{format.number(version.Downloads)}</span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </section>
  )
}

function statusView(status: ListingCheckStatus, t: RegistriesDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case ListingCheckStatus.Ok:
      return { label: t.listing.status.ok, tone: 'success' }
    case ListingCheckStatus.Warning:
      return { label: t.listing.status.warning, tone: 'warning' }
    default:
      return { label: t.listing.status.missing, tone: 'danger' }
  }
}

function Link({ href }: { href: string }) {
  return (
    <a href={href} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 underline break-all">
      {href}
      <ExternalLink aria-hidden className="size-3.5" />
    </a>
  )
}

/** The package's page on its registry, read live, with readiness checks. */
export function ListingTab({ packageId }: { packageId: number }) {
  const t = useRegistries()
  const organization = useCurrentOrganization()
  const data = useQuery({
    queryKey: registriesKeys.listing(organization.Id, packageId),
    queryFn: () => registriesApi.listing(organization.Id, packageId),
  })

  if (data.isPending) {
    return <CardSkeleton count={2} />
  }
  if (data.isError) {
    return <ScreenState error={data.error} onRetry={() => void data.refetch()} />
  }

  const listing = data.data
  if (!listing.Available) {
    return <Callout tone="info">{t.listing.unavailable}</Callout>
  }

  const fields: Array<{ label: string; value: React.ReactNode }> = [
    { label: t.listing.description, value: listing.Description ?? '-' },
    { label: t.listing.keywords, value: (listing.Keywords ?? []).join(', ') || '-' },
    { label: t.listing.license, value: listing.License ?? '-' },
    { label: t.listing.homepage, value: listing.Homepage ? <Link href={listing.Homepage} /> : '-' },
    { label: t.listing.repository, value: listing.Repository ? <Link href={listing.Repository} /> : '-' },
    { label: t.listing.authors, value: (listing.Authors ?? []).join(', ') || '-' },
  ]

  return (
    <section aria-label={t.listing.title} className="grid gap-3">
      {listing.ReadError ? <Callout tone="danger">{interpolate(t.listing.readError, { error: listing.ReadError })}</Callout> : null}
      <Callout tone="info">{listing.ShipsWithRelease ? t.listing.shipsWithRelease : t.listing.fromRepository}</Callout>
      <article className="card" aria-label={t.listing.checks}>
        <div className="card-h">
          <h3 className="h3">{t.listing.checks}</h3>
        </div>
        <ul className="card-b grid gap-1.5">
          {(listing.Checks ?? []).map((check) => {
            const view = statusView(check.Status, t)
            return (
              <li key={check.Key} className="flex items-center justify-between gap-2">
                <span>{t.listing.check[check.Key as keyof RegistriesDictionary['listing']['check']] ?? check.Key}</span>
                <Badge small tone={view.tone}>
                  {view.label}
                </Badge>
              </li>
            )
          })}
        </ul>
      </article>
      <article className="card" aria-label={t.listing.title}>
        <div className="card-h">
          <h3 className="h3">
            {t.listing.title}
            {listing.Version ? <span className="mono text-fg3"> {listing.Version}</span> : null}
          </h3>
        </div>
        <dl className="card-b grid gap-2 sm:grid-cols-2">
          {fields.map((field) => (
            <div key={field.label}>
              <dt className="text-fg3">{field.label}</dt>
              <dd className="break-words">{field.value}</dd>
            </div>
          ))}
        </dl>
      </article>
      {listing.Readme ? (
        <article className="card" aria-label={t.listing.readme}>
          <div className="card-h">
            <h3 className="h3">{t.listing.readme}</h3>
          </div>
          <div className="card-b grid gap-2">
            {listing.ReadmeFormat === ReadmeFormat.Html ? <p className="text-fg3">{t.listing.readmeHtml}</p> : null}
            <pre className="code max-h-[480px] overflow-auto whitespace-pre-wrap">{listing.Readme}</pre>
          </div>
        </article>
      ) : null}
    </section>
  )
}
