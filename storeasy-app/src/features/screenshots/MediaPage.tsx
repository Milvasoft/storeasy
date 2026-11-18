import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Images, RotateCcw, Trash2, X } from 'lucide-react'
import { AssetUpload } from '@/components/ui/AssetUpload'
import { Badge } from '@/components/ui/Badge'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Chip, EmptyState, IconButton, PageHeader } from '@/components/ui/primitives'
import { AssetPurpose, type AssetDto } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { listingsApi, mediaApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import type { P2Dictionary } from '@/features/common/i18n/en'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { PlanDialog } from '@/features/common/PlanView'
import {
  MediaKind,
  MediaSlotFill,
  RuleApplicability,
  type ChangePlanDto,
  type MediaClassDto,
  type MediaItemDto,
} from '@/features/common/types'

// Design 7.12-7.18: screenshot and preview sets per device class with the catalog's state (required and filled, covered
// by a fallback class, missing, optional), and a per class draft (order, removals, new files) that becomes a plan.

export interface ClassDraft {
  order: string[]
  deletes: string[]
  assets: AssetDto[]
}

export function emptyDraft(items: MediaItemDto[]): ClassDraft {
  return { order: [...items].sort((a, b) => a.Position - b.Position).map((item) => item.Id), deletes: [], assets: [] }
}

/** Moves `id` one step up (-1) or down (+1) in the order; the ends stay put. */
export function move(order: string[], id: string, step: -1 | 1): string[] {
  const index = order.indexOf(id)
  const target = index + step
  if (index < 0 || target < 0 || target >= order.length) {
    return order
  }
  const next = [...order]
  ;[next[index], next[target]] = [next[target], next[index]]
  return next
}

export function draftChangeCount(draft: ClassDraft, items: MediaItemDto[]): number {
  const original = emptyDraft(items).order
  const reordered = draft.order.some((id, index) => original[index] !== id) ? 1 : 0
  return reordered + draft.deletes.length + draft.assets.length
}

export function classState(
  item: MediaClassDto,
  t: P2Dictionary,
): { label: string; tone: 'success' | 'info' | 'danger' | 'neutral' | 'warning' } {
  if (item.Applicability === RuleApplicability.DoesNotApply) {
    return { label: t.media.notApplicable, tone: 'neutral' }
  }
  if (item.Fill === MediaSlotFill.Own) {
    return { label: t.media.stateOwn, tone: 'success' }
  }
  if (item.Fill === MediaSlotFill.Fallback) {
    return { label: interpolate(t.media.stateFallback, { key: item.FallbackFromClassKey ?? '-' }), tone: 'info' }
  }
  if (item.Required) {
    return { label: item.FallbackFromClassKey ? t.media.stateWaiting : t.media.stateMissing, tone: 'danger' }
  }
  return { label: t.media.stateOptional, tone: 'neutral' }
}

function ClassRow({
  item,
  kind,
  editor,
  onPlan,
  planning,
}: {
  item: MediaClassDto
  kind: MediaKind
  editor: boolean
  onPlan: (draft: ClassDraft) => void
  planning: boolean
}) {
  const t = useP2()
  const items = item.Items ?? []
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState<ClassDraft>(() => emptyDraft(items))
  const [uploadKey, setUploadKey] = useState(0)
  const state = classState(item, t)
  const byId = new Map(items.map((media) => [media.Id, media]))
  const changes = draftChangeCount(draft, items)
  const max = item.Max ?? null
  const sizes = (item.Sizes ?? []).map((size) => `${size.Width}x${size.Height}`).join(', ')

  return (
    <li className="card">
      <div className="card-b grid gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <b className="min-w-0 flex-1 text-[14px]">
            {item.Title ?? item.ClassKey}
            <span className="mono ml-2 text-xs font-normal text-fg3">{item.ClassKey}</span>
          </b>
          <span className="mono text-xs text-fg2">
            {max ? interpolate(t.media.count, { count: item.Count, max }) : interpolate(t.media.countNoMax, { count: item.Count })}
          </span>
          <Badge tone={state.tone} dot>
            {state.label}
          </Badge>
          {editor ? (
            <Button size="sm" onClick={() => setEditing((value) => !value)} aria-expanded={editing}>
              {editing ? t.media.done : t.media.edit}
            </Button>
          ) : null}
        </div>
        <div className="flex flex-wrap gap-3 text-xs text-fg3">
          {sizes ? (
            <span>
              {t.media.sizes}: <span className="mono">{sizes}</span>
            </span>
          ) : null}
          {item.Condition ? <span>{interpolate(t.media.condition, { condition: item.Condition })}</span> : null}
          {item.RuleId ? <span className="mono">{item.RuleId}</span> : null}
        </div>
        {draft.order.length === 0 && draft.assets.length === 0 ? (
          <p className="m-0 text-[13px] text-fg3">{t.media.empty}</p>
        ) : (
          <ol className="m-0 flex list-none gap-2 overflow-x-auto p-0" aria-label={item.Title ?? item.ClassKey}>
            {draft.order.map((id, index) => {
              const media = byId.get(id)
              const name = media?.FileName ?? id
              const removed = draft.deletes.includes(id)
              return (
                <li key={id} className={cn('grid w-28 flex-none gap-1 text-xs', removed && 'opacity-50')}>
                  {media?.Url ? (
                    <img src={media.Url} alt={name} className="h-40 w-28 rounded-sm border border-border object-cover" />
                  ) : (
                    <span className="grid h-40 w-28 place-items-center rounded-sm border border-border text-fg3">{index + 1}</span>
                  )}
                  <span className="truncate" title={name}>
                    {name}
                  </span>
                  {removed ? <span className="text-danger">{t.media.willRemove}</span> : null}
                  {editing ? (
                    <span className="flex gap-1">
                      <IconButton
                        label={interpolate(t.media.moveUp, { name })}
                        disabled={index === 0}
                        onClick={() => setDraft((current) => ({ ...current, order: move(current.order, id, -1) }))}
                      >
                        <ArrowUp aria-hidden />
                      </IconButton>
                      <IconButton
                        label={interpolate(t.media.moveDown, { name })}
                        disabled={index === draft.order.length - 1}
                        onClick={() => setDraft((current) => ({ ...current, order: move(current.order, id, 1) }))}
                      >
                        <ArrowDown aria-hidden />
                      </IconButton>
                      <IconButton
                        label={interpolate(removed ? t.media.restore : t.media.remove, { name })}
                        onClick={() =>
                          setDraft((current) => ({
                            ...current,
                            deletes: removed ? current.deletes.filter((value) => value !== id) : [...current.deletes, id],
                          }))
                        }
                      >
                        {removed ? <RotateCcw aria-hidden /> : <Trash2 aria-hidden />}
                      </IconButton>
                    </span>
                  ) : null}
                </li>
              )
            })}
          </ol>
        )}
        {editing ? (
          <div className="grid gap-2">
            {draft.assets.length > 0 ? (
              <ul className="m-0 grid list-none gap-1 p-0 text-[13px]" aria-label={t.media.newFiles}>
                {draft.assets.map((asset) => (
                  <li key={asset.Id} className="flex items-center gap-2">
                    <Badge small tone="success">
                      +
                    </Badge>
                    <span className="mono truncate">{asset.FileName}</span>
                    <IconButton
                      label={interpolate(t.media.removeNew, { name: asset.FileName })}
                      onClick={() =>
                        setDraft((current) => ({ ...current, assets: current.assets.filter((value) => value.Id !== asset.Id) }))
                      }
                    >
                      <X aria-hidden />
                    </IconButton>
                  </li>
                ))}
              </ul>
            ) : null}
            <AssetUpload
              key={uploadKey}
              purpose={AssetPurpose.Screenshot}
              accept={kind === MediaKind.Preview ? ['.mov', '.mp4', '.m4v'] : ['.png', '.jpg', '.jpeg']}
              hint={kind === MediaKind.Preview ? t.media.uploadHintVideo : t.media.uploadHintImage}
              label={t.media.upload}
              onUploaded={(asset) => {
                setDraft((current) => ({ ...current, assets: [...current.assets, asset] }))
                setUploadKey((value) => value + 1)
              }}
            />
            <div className="flex flex-wrap items-center justify-end gap-2">
              <span className="text-xs text-fg3">{interpolate(t.media.pending, { count: changes })}</span>
              <Button variant="primary" size="sm" disabled={changes === 0} loading={planning} onClick={() => onPlan(draft)}>
                {t.media.planClass}
              </Button>
            </div>
          </div>
        ) : null}
      </div>
    </li>
  )
}

function Media({ context, kind }: { context: WorkspaceContext; kind: MediaKind }) {
  const t = useP2()
  const { record, organizationId, editor } = context
  const [search, setSearch] = useSearchParams()
  const requested = search.get('locale')
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const [planningClass, setPlanningClass] = useState<string | null>(null)

  // The languages come from the listing (the media sets carry one language each).
  const listing = useQuery({
    queryKey: contentKeys.listing(organizationId, record.StoreAppId, null),
    queryFn: () => listingsApi.get(organizationId, record.StoreAppId, null),
  })
  const locales = listing.data?.Locales ?? []
  const locale = requested ?? locales.find((item) => item.IsPrimary)?.Locale ?? locales[0]?.Locale ?? null
  const sets = useQuery({
    queryKey: contentKeys.media(organizationId, record.StoreAppId, kind, locale),
    queryFn: () => mediaApi.sets(kind, organizationId, record.StoreAppId, locale),
    enabled: !listing.isLoading,
  })
  const makePlan = useMutation({
    mutationFn: ({ classKey, draft }: { classKey: string; draft: ClassDraft }) =>
      mediaApi.plan(kind, organizationId, record.StoreAppId, {
        Locale: sets.data?.Locale ?? locale ?? '',
        ClassKey: classKey,
        Order: draft.order.filter((id) => !draft.deletes.includes(id)),
        Deletes: draft.deletes,
        AssetIds: draft.assets.map((asset) => asset.Id),
      }),
    onSuccess: (made) => setPlan(made),
    onSettled: () => setPlanningClass(null),
  })

  const title = kind === MediaKind.Preview ? t.media.previewsTitle : t.media.screenshotsTitle
  const classes = sets.data?.Classes ?? []
  return (
    <>
      <PageHeader title={title} badge={sets.data?.CatalogVersion ? <Badge small>{sets.data.CatalogVersion}</Badge> : undefined} />
      {locales.length > 0 ? (
        <div className="toolbar mb-3" role="group" aria-label={t.media.locale}>
          {locales.map((item) => (
            <Chip
              key={item.Locale}
              selected={item.Locale === locale}
              onClick={() => {
                const params = new URLSearchParams(search)
                params.set('locale', item.Locale)
                setSearch(params, { replace: true })
              }}
            >
              <span className="mono">{item.Locale}</span>
            </Chip>
          ))}
        </div>
      ) : null}
      {listing.isLoading || sets.isLoading ? (
        <CardSkeleton />
      ) : sets.isError ? (
        <ScreenState error={sets.error} onRetry={() => void sets.refetch()} />
      ) : classes.length === 0 ? (
        <EmptyState icon={<Images aria-hidden />} title={t.media.noClasses} />
      ) : (
        <ul className="m-0 grid list-none gap-3 p-0">
          {classes.map((item) => (
            <ClassRow
              key={`${locale}-${item.ClassKey}`}
              item={item}
              kind={kind}
              editor={editor}
              planning={planningClass === item.ClassKey && makePlan.isPending}
              onPlan={(draft) => {
                setPlanningClass(item.ClassKey)
                makePlan.mutate({ classKey: item.ClassKey, draft })
              }}
            />
          ))}
        </ul>
      )}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} />
    </>
  )
}

export function ScreenshotsPage() {
  return (
    <AppWorkspace section="media" sub="screenshots">
      {(context) => <Media context={context} kind={MediaKind.Screenshot} />}
    </AppWorkspace>
  )
}

export function PreviewsPage() {
  return (
    <AppWorkspace section="media" sub="previews">
      {(context) => <Media context={context} kind={MediaKind.Preview} />}
    </AppWorkspace>
  )
}
