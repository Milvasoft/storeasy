import { useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Frame, Images, MoreHorizontal, Plus } from 'lucide-react'
import { Badge, Tag } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Menu } from '@/components/ui/Menu'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Chip, EmptyState, IconButton, PageHeader } from '@/components/ui/primitives'
import { APP_NAMES_REQUEST } from '@/lib/activity/ActivityContext'
import { appsApi } from '@/lib/api/endpoints'
import { SortType, type ListRequest } from '@/lib/api/list'
import { OrgPermission, type AppListDto } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { studioApi } from './api'
import { useStudioT } from './i18n'
import { studioKeys } from './keys'
import { NewTemplateModal } from './NewTemplateModal'
import type { MockupTemplateListDto } from './types'

export const TEMPLATES_REQUEST: ListRequest = {
  PageNumber: 1,
  RowCount: 100,
  Sorting: { SortBy: 'LastModificationDate', Type: SortType.Desc },
}

function TemplateCard({
  template,
  canEdit,
  onCopy,
}: {
  template: MockupTemplateListDto
  canEdit: boolean
  onCopy: (template: MockupTemplateListDto) => void
}) {
  const s = useStudioT()
  const format = useFormat()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const remove = useMutation({
    mutationFn: () => studioApi.remove(organization.Id, template.Id),
    onSuccess: () => {
      showToast(interpolate(s.list.deleted, { name: template.Name }), 'success')
      void queryClient.invalidateQueries({ queryKey: studioKeys.all })
    },
  })

  const askDelete = async () => {
    const ok = await confirm({
      title: interpolate(s.list.deleteTitle, { name: template.Name }),
      message: s.list.deleteBody,
      confirmLabel: s.list.delete,
      danger: true,
    })
    if (ok) {
      remove.mutate()
    }
  }

  return (
    <article className="card relative grid gap-2 p-[18px]">
      <div className="flex items-start gap-2">
        <h2 className="h3 min-w-0 flex-1 truncate">
          {/* The whole card is the link (design 12.1); the menu stays a separate control above it. */}
          <Link to={`/studio/${template.Id}`} className="after:absolute after:inset-0 after:content-['']">
            {template.Name}
          </Link>
        </h2>
        {canEdit ? (
          <Menu
            label={interpolate(s.list.actionsOf, { name: template.Name })}
            trigger={
              <IconButton label={interpolate(s.list.actionsOf, { name: template.Name })} size="sm" ghost className="relative z-[1]">
                <MoreHorizontal aria-hidden />
              </IconButton>
            }
            entries={[
              { key: 'copy', label: s.list.copy, onSelect: () => onCopy(template) },
              { key: 'delete', label: s.list.delete, danger: true, onSelect: () => void askDelete() },
            ]}
          />
        ) : null}
      </div>
      <div className="flex flex-wrap items-center gap-1.5 text-[13px] text-fg2">
        <span className="font-medium text-fg">{template.AppName}</span>
        {(template.Locales ?? []).map((locale) => (
          <Tag key={locale}>{locale}</Tag>
        ))}
      </div>
      <p className="text-xs text-fg3">{interpolate(s.list.slots, { count: template.SlotCount })}</p>
      <div className="flex flex-wrap items-center gap-2 text-xs text-fg3">
        {template.LastExportDate ? (
          <span>
            {interpolate(s.list.lastExport, {
              date: format.date(template.LastExportDate),
              count: template.LastExportImageCount ?? 0,
            })}
          </span>
        ) : (
          <Badge small>{s.list.notExported}</Badge>
        )}
        {template.LastModifierUserName ? (
          <span className="ml-auto">
            {template.LastModifierUserName} · {format.relative(template.LastModificationDate)}
          </span>
        ) : null}
      </div>
    </article>
  )
}

/** The studio's home (design 12.1, 12.3): the organization's templates, filtered by app. */
export function StudioPage() {
  const s = useStudioT()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const canEdit = can(OrgPermission.EditContent)
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const appFilter = Number(params.get('app')) || null
  const [creating, setCreating] = useState<{ appId: number | null; copyFrom: MockupTemplateListDto | null } | null>(null)

  const apps = useQuery({
    queryKey: queryKeys.apps.list(organization.Id, APP_NAMES_REQUEST),
    queryFn: () => appsApi.list(organization.Id, APP_NAMES_REQUEST),
    staleTime: 60_000,
  })
  const templates = useQuery({
    queryKey: studioKeys.templates(organization.Id, TEMPLATES_REQUEST, null),
    queryFn: () => studioApi.templates(organization.Id, TEMPLATES_REQUEST),
  })

  const all = useMemo(() => templates.data?.Data ?? [], [templates.data])
  const appList: AppListDto[] = apps.data?.Data ?? []
  const shown = appFilter ? all.filter((template) => template.AppId === appFilter) : all
  const filteredApp = appFilter ? appList.find((app) => app.Id === appFilter) : undefined
  const otherTemplate = appFilter ? all.find((template) => template.AppId !== appFilter) : undefined

  const setFilter = (appId: number | null) => {
    const next = new URLSearchParams(params)
    if (appId) {
      next.set('app', String(appId))
    } else {
      next.delete('app')
    }
    setParams(next, { replace: true })
  }

  const emptyBody = s.list.emptyBody
  let content
  if (templates.isLoading) {
    content = <CardSkeleton count={3} />
  } else if (templates.isError) {
    content = <ScreenState error={templates.error} onRetry={() => void templates.refetch()} />
  } else if (shown.length === 0) {
    const title = filteredApp ? interpolate(s.list.emptyAppTitle, { app: filteredApp.Name }) : s.list.emptyTitle
    content = (
      <EmptyState
        icon={<Images aria-hidden />}
        title={title}
        description={emptyBody}
        action={
          canEdit ? (
            <>
              <Button variant="primary" onClick={() => setCreating({ appId: appFilter, copyFrom: null })}>
                <Plus aria-hidden />
                {filteredApp ? interpolate(s.list.createFor, { app: filteredApp.Name }) : s.list.create}
              </Button>
              {otherTemplate ? (
                <Button onClick={() => setCreating({ appId: appFilter, copyFrom: otherTemplate })}>
                  {interpolate(s.list.copyOf, { name: otherTemplate.Name })}
                </Button>
              ) : null}
            </>
          ) : (
            <span className="text-[13px] text-fg3">{s.list.editorCreates}</span>
          )
        }
      />
    )
  } else {
    content = (
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {shown.map((template) => (
          <TemplateCard
            key={template.Id}
            template={template}
            canEdit={canEdit}
            onCopy={(source) => setCreating({ appId: source.AppId, copyFrom: source })}
          />
        ))}
      </div>
    )
  }

  // One primary action per screen: the empty state's own button replaces the header's (design 12.3).
  const headerCreate = canEdit && shown.length > 0
  return (
    <>
      <PageHeader
        title={s.title}
        subtitle={s.subtitle}
        actions={
          <>
            <Link className="btn max-sm:hidden" to="/studio/frames">
              <Frame aria-hidden />
              {s.frames.title}
            </Link>
            {headerCreate ? (
              <Button variant="primary" onClick={() => setCreating({ appId: appFilter, copyFrom: null })}>
                <Plus aria-hidden />
                {s.list.create}
              </Button>
            ) : null}
          </>
        }
      />
      {appList.length > 0 ? (
        <div className="toolbar" role="group" aria-label={s.list.filter}>
          <Chip selected={appFilter === null} onClick={() => setFilter(null)}>
            {interpolate(s.list.all, { count: all.length })}
          </Chip>
          {appList.map((app) => (
            <Chip key={app.Id} selected={appFilter === app.Id} onClick={() => setFilter(app.Id)}>
              {app.Name} · {all.filter((template) => template.AppId === app.Id).length}
            </Chip>
          ))}
        </div>
      ) : null}
      {content}
      {creating ? (
        <NewTemplateModal
          apps={appList}
          templates={all}
          initialAppId={creating.appId}
          copyFrom={creating.copyFrom}
          onClose={() => setCreating(null)}
          onCreated={(id) => {
            setCreating(null)
            navigate(`/studio/${id}`)
          }}
        />
      ) : null}
    </>
  )
}
