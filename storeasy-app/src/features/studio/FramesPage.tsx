import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, Frame } from 'lucide-react'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { ScreenState } from '@/components/ui/ScreenState'
import { CardSkeleton, Chip, EmptyState, PageHeader } from '@/components/ui/primitives'
import { useFormat, useI18n } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { studioApi } from './api'
import { useStudioT } from './i18n'
import { studioKeys } from './keys'
import { classLabel, frameKindLabel, requirementView } from './labels'
import { toPercent } from './spec'
import { MockupFrameKind, type MockupFrameDto } from './types'

type KindFilter = 'all' | 'phone' | 'tablet' | 'foldable' | 'desktop'

function matches(frame: MockupFrameDto, filter: KindFilter): boolean {
  switch (filter) {
    case 'phone':
      return frame.Kind === MockupFrameKind.Phone
    case 'tablet':
      return frame.Kind === MockupFrameKind.Tablet
    case 'foldable':
      return frame.Kind === MockupFrameKind.FoldableOuter || frame.Kind === MockupFrameKind.FoldableInner
    case 'desktop':
      return frame.Kind === MockupFrameKind.Desktop
    default:
      return true
  }
}

function FrameCard({ frame }: { frame: MockupFrameDto }) {
  const s = useStudioT()
  const { lang } = useI18n()
  const format = useFormat()
  const name = frameKindLabel(frame.Kind, s)
  return (
    <article className="card grid content-start gap-3 p-[18px]" aria-label={name}>
      <h2 className="h3">{name}</h2>
      <dl className="kv text-[13px]">
        <dt>{s.frames.cornerRadius}</dt>
        <dd className="mono">{toPercent(frame.CornerRadius)}%</dd>
        <dt>{s.frames.bezel}</dt>
        <dd className="mono">{toPercent(frame.Bezel)}%</dd>
        <dt>{s.frames.cutouts}</dt>
        <dd>{(frame.Cutouts ?? []).length > 0 ? (frame.Cutouts ?? []).join(', ') : '-'}</dd>
      </dl>
      <ul className="grid gap-2">
        {(frame.Classes ?? []).map((item) => {
          const requirement = requirementView(item.Status, s)
          return (
            <li key={item.ClassKey} className="flex flex-wrap items-center gap-2 text-[13px]">
              <StoreTag store={item.Store} small codeOnly />
              <span className="min-w-0 flex-1 truncate">{classLabel(item, lang)}</span>
              <span className="mono text-xs text-fg2">
                {item.Width}x{item.Height}
              </span>
              <Badge tone={requirement.tone} small>
                {requirement.label}
              </Badge>
              {item.RequiredFrom ? (
                <span className="w-full pl-9 text-xs text-fg3">{interpolate(s.export.requiredFrom, { date: format.date(item.RequiredFrom) })}</span>
              ) : null}
            </li>
          )
        })}
      </ul>
    </article>
  )
}

/** Design 12.6: storeasy's own parametric frames and the catalog classes drawn with each. */
export function FramesPage() {
  const s = useStudioT()
  const organization = useCurrentOrganization()
  const [filter, setFilter] = useState<KindFilter>('all')
  const frames = useQuery({
    queryKey: studioKeys.frames(organization.Id),
    queryFn: () => studioApi.frames(organization.Id),
    staleTime: 10 * 60_000,
  })
  const list = frames.data ?? []
  const shown = list.filter((frame) => matches(frame, filter))
  const filters: KindFilter[] = ['all', 'phone', 'tablet', 'foldable', 'desktop']

  let content
  if (frames.isLoading) {
    content = <CardSkeleton count={4} />
  } else if (frames.isError) {
    content = <ScreenState error={frames.error} onRetry={() => void frames.refetch()} />
  } else if (shown.length === 0) {
    content = <EmptyState icon={<Frame aria-hidden />} title={s.frames.empty} />
  } else {
    content = (
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {shown.map((frame) => (
          <FrameCard key={frame.Kind} frame={frame} />
        ))}
      </div>
    )
  }

  return (
    <>
      <Link to="/studio" className="lnk mb-1 inline-flex items-center gap-1 text-xs">
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
        {s.title}
      </Link>
      <PageHeader title={s.frames.title} subtitle={s.frames.subtitle} />
      <div className="toolbar" role="group" aria-label={s.frames.filter}>
        {filters.map((value) => (
          <Chip key={value} selected={filter === value} onClick={() => setFilter(value)}>
            {value === 'all' ? interpolate(s.frames.all, { count: list.length }) : s.frames.kinds[value]}
          </Chip>
        ))}
      </div>
      {content}
      <p className="mt-4 text-xs text-fg3">{s.frames.note}</p>
    </>
  )
}
