import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, ImageOff, Layers, Minus, Plus, Shapes, Tag as TagIcon } from 'lucide-react'
import { AssetUpload } from '@/components/ui/AssetUpload'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { Select } from '@/components/ui/Select'
import { Button, IconButton, Skeleton } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { AssetPurpose, type AssetDto } from '@/lib/api/types'
import { usePrefersReducedMotion } from '@/lib/hooks/usePrefersReducedMotion'
import { useI18n } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { cn } from '@/lib/utils/cn'
import { studioApi, type PreviewInput } from './api'
import { PanelSection, useDebounced } from './fields'
import { useStudioT } from './i18n'
import { studioKeys } from './keys'
import { classesOf, classLabel, FAMILIES, familyLabel, warningText } from './labels'
import { BackgroundProperties, FrameProperties, LayerProperties, TemplateProperties, TextProperties } from './PropertyPanels'
import {
  cellKey,
  MAX_SLOTS,
  newLayer,
  PREVIEW_DEBOUNCE_MS,
  PREVIEW_MAX_WIDTH,
  previewPage,
  removeSlotAsset,
  setSlotAsset,
  slotAsset,
  trimSlots,
} from './spec'
import { MockupDeviceFamily, type MockupFrameDto, type MockupWarningDto } from './types'
import type { TemplateEditor } from './useTemplateEditor'

/** A preview slower than this offers "try again" in its slot (design 12.14). */
export const PREVIEW_LATE_MS = 10_000
const SCREENSHOT_TYPES = ['.png', '.jpg', '.jpeg']

type Selection = 'template' | 'background' | 'frame' | 'text' | number

function SlotScreenshots({ editor, order, canEdit }: { editor: TemplateEditor; order: number; canEdit: boolean }) {
  const s = useStudioT()
  const draft = editor.draft!
  const [scope, setScope] = useState<string>('')
  const [uploading, setUploading] = useState<MockupDeviceFamily | null>(null)
  const [names, setNames] = useState<Record<number, string>>({})
  const locale = scope || null

  const onUploaded = (family: MockupDeviceFamily, asset: AssetDto) => {
    setNames((current) => ({ ...current, [asset.Id]: asset.FileName }))
    editor.update((d) => ({ ...d, Slots: setSlotAsset(d.Slots, { Order: order, Family: family, Locale: locale, AssetId: asset.Id }) }))
    setUploading(null)
  }

  return (
    <PanelSection title={interpolate(s.slots.screenshotsOf, { order })}>
      <div className="grid gap-1">
        <span className="text-xs text-fg3" id="studio-slot-scope">
          {s.slots.scope}
        </span>
        <Select
          aria-label={s.slots.scope}
          value={scope}
          options={[{ value: '', label: s.slots.allLocales }, ...draft.Locales.map((value) => ({ value, label: value }))]}
          onChange={setScope}
        />
      </div>
      {FAMILIES.map((family) => {
        const own = draft.Slots.find((slot) => slot.Order === order && slot.Family === family && (slot.Locale ?? null) === locale)
        const used = slotAsset(draft.Slots, order, family, locale)
        const label = familyLabel(family, s)
        return (
          <div key={family} className="grid gap-1.5 rounded-md border border-border p-2.5" aria-label={label} role="group">
            <div className="flex items-center gap-2 text-[13px]">
              <b>{label}</b>
              {used ? (
                <span className="mono truncate text-xs text-fg2">
                  {names[used.AssetId] ?? interpolate(s.slots.asset, { id: used.AssetId })}
                </span>
              ) : (
                <span className="text-xs text-fg3">{family === MockupDeviceFamily.Phone ? s.slots.none : s.slots.fallback}</span>
              )}
              {used && !own ? <Badge small>{s.slots.inherited}</Badge> : null}
            </div>
            {canEdit ? (
              uploading === family ? (
                <AssetUpload
                  purpose={AssetPurpose.Screenshot}
                  accept={SCREENSHOT_TYPES}
                  label={interpolate(s.slots.uploadLabel, { family: label, order })}
                  hint={s.slots.uploadHint}
                  onUploaded={(asset) => onUploaded(family, asset)}
                />
              ) : (
                <span className="flex gap-1.5">
                  <Button size="sm" onClick={() => setUploading(family)}>
                    {own ? interpolate(s.slots.replace, { family: label }) : interpolate(s.slots.upload, { family: label })}
                  </Button>
                  {own ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => editor.update((d) => ({ ...d, Slots: removeSlotAsset(d.Slots, order, family, locale) }))}
                    >
                      {interpolate(s.slots.remove, { family: label })}
                    </Button>
                  ) : null}
                </span>
              )
            ) : null}
          </div>
        )
      })}
      {editor.changes.includes('slots') ? <p className="text-xs text-fg3">{s.slots.savedToPreview}</p> : null}
    </PanelSection>
  )
}

function PreviewSlot({
  order,
  image,
  loading,
  late,
  selected,
  onSelect,
  onRetry,
  title,
}: {
  order: number
  image: { Url?: string | null; Width: number; Height: number } | undefined
  loading: boolean
  late: boolean
  selected: boolean
  onSelect: () => void
  onRetry: () => void
  title: string
}) {
  const s = useStudioT()
  const reduced = usePrefersReducedMotion()
  let body
  if (image?.Url) {
    body = (
      <img
        src={image.Url}
        width={image.Width}
        height={image.Height}
        alt={interpolate(s.preview.alt, { order })}
        className={cn('h-auto w-full rounded-md', loading && 'opacity-60', !reduced && 'transition-opacity')}
      />
    )
  } else if (image && !image.Url) {
    body = (
      <div className="grid aspect-[9/19] place-items-center rounded-md border border-dashed border-danger p-3 text-center text-xs text-fg2">
        <ImageOff className="h-5 w-5 text-danger" aria-hidden />
        {s.preview.failed}
      </div>
    )
  } else if (late) {
    body = (
      <div className="grid aspect-[9/19] place-items-center gap-2 rounded-md border border-dashed border-border p-3 text-center text-xs">
        {s.preview.late}
        <Button size="sm" onClick={onRetry}>
          {s.common.retry}
        </Button>
      </div>
    )
  } else {
    body = <Skeleton className="block aspect-[9/19] h-auto w-full" />
  }
  return (
    <figure className="grid min-w-0 gap-1.5">
      {body}
      <figcaption>
        <button
          type="button"
          aria-pressed={selected}
          onClick={onSelect}
          className={cn('w-full truncate rounded px-1 text-left text-xs', selected ? 'font-semibold text-accent-text' : 'text-fg2')}
        >
          {order} · {title || s.slots.noTitle}
        </button>
      </figcaption>
    </figure>
  )
}

function uniqueWarnings(warnings: MockupWarningDto[]): MockupWarningDto[] {
  const seen = new Set<string>()
  return warnings.filter((warning) => {
    const key = `${warning.Code}|${warning.Locale}|${warning.SlotOrder}|${warning.Field}|${warning.ClassKey}`
    if (seen.has(key)) {
      return false
    }
    seen.add(key)
    return true
  })
}

/** Design 12.4: slots and layers on the left, the server drawn preview in the middle, the properties on the right. */
export function DesignEditor({ editor, frames, canEdit }: { editor: TemplateEditor; frames: MockupFrameDto[]; canEdit: boolean }) {
  const s = useStudioT()
  const { lang } = useI18n()
  const organization = useCurrentOrganization()
  const draft = editor.draft!
  const templateId = editor.detail.data!.Id
  const classes = useMemo(() => classesOf(frames), [frames])
  const [selectedSlot, setSelectedSlot] = useState(1)
  const [selection, setSelection] = useState<Selection>('background')
  const [locale, setLocale] = useState(draft.PrimaryLocale)
  const [classKey, setClassKey] = useState<string | null>(draft.Spec.Canvas?.ClassKey ?? null)
  const activeClass = classKey ?? classes[0]?.ClassKey ?? null
  const slot = Math.min(selectedSlot, draft.SlotCount)
  const page = previewPage(slot, draft.SlotCount)
  const spec = useDebounced(draft.Spec, PREVIEW_DEBOUNCE_MS)

  const input: PreviewInput = {
    TemplateId: templateId,
    Locale: draft.Locales.includes(locale) ? locale : draft.PrimaryLocale,
    ClassKey: activeClass,
    SlotOrders: page,
    Spec: spec,
    MaxWidth: PREVIEW_MAX_WIDTH,
  }
  const preview = useQuery({
    queryKey: studioKeys.preview(organization.Id, input),
    queryFn: () => studioApi.preview(organization.Id, input),
    // The last images stay on screen while the next ones are drawn; an answer for an older design never replaces a
    // newer one (each design has its own key).
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
    retry: false,
  })

  const [late, setLate] = useState(false)
  useEffect(() => {
    if (!preview.isFetching) {
      setLate(false)
      return
    }
    const timer = window.setTimeout(() => setLate(true), PREVIEW_LATE_MS)
    return () => window.clearTimeout(timer)
  }, [preview.isFetching])

  const images = preview.data?.Images ?? []
  const shownImages = preview.isPlaceholderData && !images.some((image) => page.includes(image.SlotOrder)) ? [] : images
  const warnings = uniqueWarnings([...shownImages.flatMap((image) => image.Warnings ?? []), ...editor.captionWarnings])
  const titleOf = (order: number) => editor.captions[cellKey(input.Locale ?? '', order)]?.title ?? ''
  const layers = draft.Spec.Layers ?? []
  const pageStart = page[0] ?? 1

  const setSlotCount = (count: number) => {
    editor.update((d) => ({ ...d, SlotCount: count, Slots: trimSlots(d.Slots, count) }))
    setSelectedSlot((current) => Math.min(current, count))
  }
  const addLayer = (type: 'shape' | 'badge') => {
    editor.updateSpec((current) => ({ ...current, Layers: [...(current.Layers ?? []), newLayer(type)] }))
    setSelection(layers.length)
  }

  const layerItems: Array<{ key: Selection; label: string }> = [
    { key: 'template', label: s.props.template },
    { key: 'background', label: s.props.background },
    { key: 'frame', label: s.props.frame },
    { key: 'text', label: s.props.text },
    ...layers.map((layer, index) => ({
      key: index as Selection,
      label: `${layer.Type === 'badge' ? s.layers.badge : s.layers.shape} ${index + 1}`,
    })),
  ]

  let properties
  if (selection === 'template') {
    properties = <TemplateProperties draft={draft} classes={classes} onDraft={editor.update} />
  } else if (selection === 'background') {
    properties = <BackgroundProperties spec={draft.Spec} onSpec={editor.updateSpec} />
  } else if (selection === 'frame') {
    properties = <FrameProperties spec={draft.Spec} onSpec={editor.updateSpec} />
  } else if (selection === 'text') {
    properties = <TextProperties spec={draft.Spec} onSpec={editor.updateSpec} />
  } else if (layers[selection]) {
    properties = (
      <LayerProperties
        layer={layers[selection]}
        index={selection}
        locales={draft.Locales}
        onSpec={editor.updateSpec}
        onRemoved={() => setSelection('background')}
      />
    )
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[260px_minmax(0,1fr)] xl:grid-cols-[260px_minmax(0,1fr)_300px]">
      {/* ---- left: slots, their screenshots, layers ---- */}
      <aside className="card grid content-start gap-4 p-4" aria-label={s.editor.leftPanel}>
        <PanelSection
          title={s.slots.title}
          actions={
            canEdit ? (
              <>
                <IconButton
                  label={s.slots.removeLast}
                  size="sm"
                  ghost
                  disabled={draft.SlotCount <= 1}
                  onClick={() => setSlotCount(draft.SlotCount - 1)}
                >
                  <Minus aria-hidden />
                </IconButton>
                <IconButton
                  label={s.slots.add}
                  size="sm"
                  ghost
                  disabled={draft.SlotCount >= MAX_SLOTS}
                  onClick={() => setSlotCount(draft.SlotCount + 1)}
                >
                  <Plus aria-hidden />
                </IconButton>
              </>
            ) : null
          }
        >
          <ol className="grid gap-1">
            {Array.from({ length: draft.SlotCount }, (_, index) => index + 1).map((order) => (
              <li key={order}>
                <button
                  type="button"
                  aria-pressed={order === slot}
                  onClick={() => setSelectedSlot(order)}
                  className={cn(
                    'flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-[13px]',
                    order === slot ? 'bg-accent/10 font-semibold' : 'hover:bg-bg2',
                  )}
                >
                  <span className="mono w-5 text-fg3">{order}</span>
                  <span className="truncate">{titleOf(order) || s.slots.noTitle}</span>
                </button>
              </li>
            ))}
          </ol>
        </PanelSection>
        <SlotScreenshots key={slot} editor={editor} order={slot} canEdit={canEdit} />
        <PanelSection
          title={s.layers.title}
          actions={
            canEdit ? (
              <>
                <IconButton label={s.layers.addShape} size="sm" ghost onClick={() => addLayer('shape')}>
                  <Shapes aria-hidden />
                </IconButton>
                <IconButton label={s.layers.addBadge} size="sm" ghost onClick={() => addLayer('badge')}>
                  <TagIcon aria-hidden />
                </IconButton>
              </>
            ) : null
          }
        >
          <ul className="grid gap-1">
            {layerItems.map((item) => (
              <li key={String(item.key)}>
                <button
                  type="button"
                  aria-pressed={selection === item.key}
                  onClick={() => setSelection(item.key)}
                  className={cn(
                    'flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-[13px]',
                    selection === item.key ? 'bg-accent/10 font-semibold' : 'hover:bg-bg2',
                  )}
                >
                  <Layers className="h-3.5 w-3.5 text-fg3" aria-hidden />
                  {item.label}
                </button>
              </li>
            ))}
          </ul>
        </PanelSection>
      </aside>

      {/* ---- center: the canvas ---- */}
      <section className="card grid content-start gap-4 p-4" aria-label={s.preview.title}>
        <div className="flex flex-wrap items-center gap-2">
          <span className="w-36">
            <Select
              aria-label={s.preview.locale}
              value={input.Locale ?? null}
              options={draft.Locales.map((value) => ({ value, label: value }))}
              onChange={setLocale}
            />
          </span>
          <span className="min-w-0 flex-1">
            <Select
              aria-label={s.preview.class}
              value={activeClass}
              options={classes.map((item) => ({
                value: item.ClassKey,
                label: `${classLabel(item, lang)} · ${item.Width}x${item.Height}`,
              }))}
              onChange={setClassKey}
            />
          </span>
        </div>
        <div className="flex items-center gap-2 text-xs text-fg3">
          <span>{interpolate(s.preview.range, { from: pageStart, to: page[page.length - 1] ?? pageStart, count: draft.SlotCount })}</span>
          <span className="ml-auto" role="status" aria-live="polite">
            {preview.isFetching
              ? s.preview.drawing
              : preview.data
                ? interpolate(s.preview.duration, { ms: preview.data.DurationMs })
                : null}
          </span>
          <IconButton
            label={s.preview.previous}
            size="sm"
            ghost
            disabled={pageStart <= 1}
            onClick={() => setSelectedSlot(Math.max(1, pageStart - 1))}
          >
            <ChevronLeft aria-hidden />
          </IconButton>
          <IconButton
            label={s.preview.next}
            size="sm"
            ghost
            disabled={(page[page.length - 1] ?? 0) >= draft.SlotCount}
            onClick={() => setSelectedSlot((page[page.length - 1] ?? 0) + 1)}
          >
            <ChevronRight aria-hidden />
          </IconButton>
        </div>
        {preview.isError && !preview.data ? (
          <Callout tone="danger" title={s.preview.error}>
            {apiErrorMessage(preview.error, s.common.error)}{' '}
            <button type="button" className="lnk" onClick={() => void preview.refetch()}>
              {s.common.retry}
            </button>
          </Callout>
        ) : (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {page.map((order) => (
              <PreviewSlot
                key={order}
                order={order}
                image={shownImages.find((image) => image.SlotOrder === order)}
                loading={preview.isFetching}
                late={late}
                selected={order === slot}
                onSelect={() => setSelectedSlot(order)}
                onRetry={() => void preview.refetch()}
                title={titleOf(order)}
              />
            ))}
          </div>
        )}
        <section aria-label={s.preview.warnings} className="grid gap-2">
          <h3 className="text-[13px] font-semibold">{interpolate(s.preview.warningsCount, { count: warnings.length })}</h3>
          {warnings.length === 0 ? <p className="text-xs text-fg3">{s.preview.noWarnings}</p> : null}
          <ul className="grid gap-1.5">
            {warnings.map((warning, index) => (
              <li key={index} className="text-[13px] text-fg2">
                <Badge tone="warning" small>
                  {warning.Code}
                </Badge>{' '}
                {warning.Locale ? `${warning.Locale} · ` : null}
                {warning.SlotOrder ? `${interpolate(s.slots.slot, { order: warning.SlotOrder })} · ` : null}
                {warningText(warning, s)}
                {warning.Code === 'CaptionOverflow' ? (
                  <>
                    {' '}
                    <Link className="lnk" to={`/studio/${templateId}/captions`}>
                      {s.preview.toCaptions}
                    </Link>
                  </>
                ) : null}
              </li>
            ))}
          </ul>
        </section>
      </section>

      {/* ---- right: properties ---- */}
      <aside className="card p-4 lg:col-span-2 xl:col-span-1" aria-label={s.editor.properties}>
        <fieldset disabled={!canEdit} className="grid gap-4">
          <legend className="sr-only">{s.editor.properties}</legend>
          {properties}
        </fieldset>
      </aside>
    </div>
  )
}
