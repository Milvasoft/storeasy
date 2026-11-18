import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, Checkbox, Chip, Skeleton } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { STORE_ORDER } from '@/lib/apps/apps'
import { useFormat, useI18n } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { studioApi, type ExportSelection } from './api'
import { useStudioT } from './i18n'
import { studioKeys } from './keys'
import { classLabel, frameKindLabel, requirementView, warningText } from './labels'
import type { MockupExportPlanItemDto } from './types'
import type { TemplateEditor } from './useTemplateEditor'

function PlanRow({
  item,
  disabled,
  onToggle,
}: {
  item: MockupExportPlanItemDto
  disabled: boolean
  onToggle: (selected: boolean) => void
}) {
  const s = useStudioT()
  const { lang } = useI18n()
  const format = useFormat()
  const requirement = requirementView(item.Status, s)
  return (
    <li className="flex flex-wrap items-start gap-3 border-b border-border py-2.5 last:border-b-0">
      <Checkbox checked={item.Selected} disabled={disabled} onChange={onToggle}>
        <span className="font-medium text-fg">{classLabel(item, lang)}</span>
      </Checkbox>
      <span className="mono text-xs text-fg2">
        {item.Width}x{item.Height} · {frameKindLabel(item.Frame, s)}
      </span>
      <span className="ml-auto flex items-center gap-2">
        <span className="text-xs text-fg3">{interpolate(s.export.perLocale, { count: item.PerLocaleCount })}</span>
        <Badge tone={requirement.tone} small>
          {requirement.label}
        </Badge>
      </span>
      {item.Note || item.RequiredFrom ? (
        <p className="w-full pl-7 text-xs text-fg3">
          {item.Note}
          {item.RequiredFrom ? ` ${interpolate(s.export.requiredFrom, { date: format.date(item.RequiredFrom) })}` : null}
        </p>
      ) : null}
    </li>
  )
}

/** Design 12.10: the classes of the app's stores from the rules catalog, the refused sizes, the image count, and start. */
export function ExportPanel({ editor, canEdit }: { editor: TemplateEditor; canEdit: boolean }) {
  const s = useStudioT()
  const organization = useCurrentOrganization()
  const navigate = useNavigate()
  const { watchJobs } = useActivity()
  const template = editor.detail.data!
  const templateLocales = useMemo(() => template.Locales ?? [], [template.Locales])
  const [locales, setLocales] = useState<string[]>(templateLocales)
  const [classKeys, setClassKeys] = useState<string[] | null>(null)

  const selection: ExportSelection = {
    TemplateId: template.Id,
    Locales: locales,
    ClassKeys: classKeys,
  }
  const plan = useQuery({
    queryKey: studioKeys.exportPlan(organization.Id, selection),
    queryFn: () => studioApi.exportPlan(organization.Id, selection),
    placeholderData: keepPreviousData,
  })

  const start = useMutation({
    mutationFn: () => studioApi.startExport(organization.Id, selection),
    onSuccess: (jobId) => {
      watchJobs([jobId])
      navigate(`/studio/${template.Id}/export/${jobId}`)
    },
  })

  if (plan.isLoading) {
    return (
      <div className="grid gap-3" aria-busy="true">
        <span className="sr-only" role="status">
          {s.export.planning}
        </span>
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-24 w-full" />
      </div>
    )
  }
  if (plan.isError || !plan.data) {
    return <ScreenState error={plan.error} onRetry={() => void plan.refetch()} />
  }

  const items = plan.data.Items ?? []
  const selected = items.filter((item) => item.Selected).map((item) => item.ClassKey)
  const toggleClass = (key: string, on: boolean) => {
    setClassKeys(on ? [...selected, key] : selected.filter((value) => value !== key))
  }
  const toggleLocale = (locale: string) => {
    setLocales((current) => (current.includes(locale) ? current.filter((value) => value !== locale) : [...current, locale]))
  }
  const rejected = plan.data.Rejected ?? []
  const warnings = plan.data.Warnings ?? []
  const stores = STORE_ORDER.filter((store) => items.some((item) => item.Store === store))
  const blocked = editor.templateDirty || editor.captionsDirty

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
      <div className="grid content-start gap-4">
        {stores.map((store) => {
          const ofStore = items.filter((item) => item.Store === store)
          return (
            <section key={store} className="card p-4">
              <div className="mb-1 flex items-center gap-2">
                <StoreTag store={store} />
                <span className="ml-auto text-xs text-fg3">
                  {interpolate(s.export.selectedOf, { selected: ofStore.filter((item) => item.Selected).length, total: ofStore.length })}
                </span>
              </div>
              <ul>
                {ofStore.map((item) => (
                  <PlanRow key={item.ClassKey} item={item} disabled={!canEdit} onToggle={(on) => toggleClass(item.ClassKey, on)} />
                ))}
              </ul>
            </section>
          )
        })}
        {items.length === 0 ? <Callout tone="info">{s.export.noClasses}</Callout> : null}
        {rejected.length > 0 ? (
          <section className="card p-4" aria-label={s.export.rejected}>
            <h3 className="mb-2 text-[13px] font-semibold">{s.export.rejected}</h3>
            <ul className="grid gap-2">
              {rejected.map((item) => (
                <li key={`${item.ClassKey}-${item.Width}x${item.Height}`} className="text-[13px]">
                  <Badge tone="danger" small>
                    {s.export.refused}
                  </Badge>{' '}
                  <span className="mono">
                    {item.Width}x{item.Height}
                  </span>{' '}
                  → <span className="mono text-fg2">{item.ClassKey}</span>
                  {item.Reason ? <p className="text-xs text-fg3">{item.Reason}</p> : null}
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </div>
      <aside className="card grid content-start gap-3 p-4" aria-label={s.export.summary}>
        <h3 className="text-[13px] font-semibold uppercase tracking-wide text-fg3">{s.export.willProduce}</h3>
        <p className="font-display text-3xl font-semibold" role="status">
          {interpolate(s.export.images, { count: plan.data.Total })}
        </p>
        <div className="flex flex-wrap gap-1.5" role="group" aria-label={s.export.locales}>
          {templateLocales.map((locale) => (
            <Chip key={locale} selected={locales.includes(locale)} onClick={() => toggleLocale(locale)}>
              {locale}
            </Chip>
          ))}
        </div>
        <p className="text-xs text-fg3">{s.export.format}</p>
        {plan.data.CatalogVersion ? (
          <p className="text-xs text-fg3">{interpolate(s.export.catalog, { version: plan.data.CatalogVersion })}</p>
        ) : null}
        {warnings.length > 0 ? (
          <Callout tone="warning" title={interpolate(s.export.warnings, { count: warnings.length })}>
            <ul className="grid gap-1">
              {warnings.map((warning, index) => (
                <li key={index}>{warningText(warning, s)}</li>
              ))}
            </ul>
          </Callout>
        ) : null}
        {canEdit ? (
          <>
            {blocked ? <Callout tone="info">{s.export.saveFirst}</Callout> : null}
            <Button
              variant="primary"
              block
              loading={start.isPending}
              disabled={blocked || plan.data.Total === 0 || locales.length === 0 || plan.isFetching}
              onClick={() => start.mutate()}
            >
              {s.export.start}
            </Button>
            <p className="text-xs text-fg3">{s.export.storeUntouched}</p>
          </>
        ) : (
          <p className="text-xs text-fg3">{s.export.editorOnly}</p>
        )}
      </aside>
    </div>
  )
}
