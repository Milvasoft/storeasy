import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Sparkles } from 'lucide-react'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Select } from '@/components/ui/Select'
import { SegmentedControl } from '@/components/ui/Tabs'
import { Button, Checkbox, Chip, Field, Input, Textarea } from '@/components/ui/primitives'
import { apiErrorMessage, apiStatus } from '@/lib/api/http'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { studioApi } from './api'
import { useStudioT } from './i18n'
import { warningText } from './labels'
import { cellKey, cellWarnings, copyCaptions, SUBTITLE_SOFT_MAX, TITLE_SOFT_MAX, type CaptionCell } from './spec'
import type { MockupCaptionSuggestionItemDto } from './types'
import type { TemplateEditor } from './useTemplateEditor'

const EMPTY: CaptionCell = { title: '', subtitle: '' }

function SuggestModal({
  editor,
  onClose,
  onDisabled,
}: {
  editor: TemplateEditor
  onClose: () => void
  onDisabled: (message: string) => void
}) {
  const s = useStudioT()
  const organization = useCurrentOrganization()
  const draft = editor.draft!
  const targets = draft.Locales.filter((locale) => locale !== draft.PrimaryLocale)
  const [target, setTarget] = useState(targets[0] ?? draft.PrimaryLocale)
  const [scope, setScope] = useState<'problems' | 'all'>('problems')
  const [accepted, setAccepted] = useState<Set<number>>(new Set())

  const problemSlots = Array.from({ length: draft.SlotCount }, (_, index) => index + 1).filter(
    (order) => !editor.captions[cellKey(target, order)]?.title || cellWarnings(editor.captionWarnings, target, order).length > 0,
  )
  const slotOrders = scope === 'all' ? null : problemSlots

  const suggest = useMutation({
    mutationFn: () =>
      studioApi.suggestCaptions(organization.Id, {
        TemplateId: editor.detail.data!.Id,
        TargetLocale: target,
        SourceLocales: [draft.PrimaryLocale],
        SlotOrders: slotOrders,
      }),
    meta: { silentError: true },
    onSuccess: (result) => setAccepted(new Set((result.Items ?? []).map((_, index) => index))),
    onError: (error) => {
      // 503: the platform switched the feature off (Studio.AiCaptions) or no provider answers (design 12.9).
      if (apiStatus(error) === 503) {
        onDisabled(apiErrorMessage(error, s.ai.disabledBody))
        onClose()
      } else {
        showToast(apiErrorMessage(error, s.common.error), 'error')
      }
    },
  })

  const items: MockupCaptionSuggestionItemDto[] = suggest.data?.Items ?? []
  const apply = () => {
    const next = { ...editor.captions }
    items.forEach((item, index) => {
      if (!accepted.has(index)) {
        return
      }
      const key = cellKey(suggest.data?.TargetLocale ?? target, item.SlotOrder)
      const cell = next[key] ?? EMPTY
      next[key] = item.Field === 'subtitle' ? { ...cell, subtitle: item.After } : { ...cell, title: item.After }
    })
    editor.replaceCaptions(next)
    onClose()
  }

  return (
    <Modal
      open
      onClose={onClose}
      variant="drawer"
      title={interpolate(s.ai.title, { locale: target })}
      description={interpolate(s.ai.source, { locale: draft.PrimaryLocale })}
      footer={
        <>
          <Button onClick={onClose}>{s.common.cancel}</Button>
          {suggest.data ? (
            <Button variant="primary" disabled={accepted.size === 0} onClick={apply}>
              {interpolate(s.ai.apply, { count: accepted.size })}
            </Button>
          ) : (
            <Button
              variant="primary"
              loading={suggest.isPending}
              disabled={scope === 'problems' && problemSlots.length === 0}
              onClick={() => suggest.mutate()}
            >
              {s.ai.ask}
            </Button>
          )}
        </>
      }
    >
      <div className="grid gap-4">
        {targets.length > 1 ? (
          <Field label={s.ai.target}>
            <Select value={target} options={targets.map((value) => ({ value, label: value }))} onChange={setTarget} />
          </Field>
        ) : null}
        <SegmentedControl
          label={s.ai.scope}
          value={scope}
          onChange={setScope}
          options={[
            { value: 'problems', label: interpolate(s.ai.problems, { count: problemSlots.length }) },
            { value: 'all', label: interpolate(s.ai.all, { count: draft.SlotCount }) },
          ]}
        />
        {suggest.data && items.length === 0 ? <p className="text-[13px] text-fg3">{s.ai.none}</p> : null}
        <ul className="grid gap-3">
          {items.map((item, index) => (
            <li key={`${item.SlotOrder}-${item.Field}`} className="card grid gap-1.5 p-3">
              <Checkbox
                checked={accepted.has(index)}
                onChange={(checked) =>
                  setAccepted((current) => {
                    const next = new Set(current)
                    if (checked) {
                      next.add(index)
                    } else {
                      next.delete(index)
                    }
                    return next
                  })
                }
              >
                {interpolate(s.ai.item, {
                  order: item.SlotOrder,
                  field: item.Field === 'subtitle' ? s.captions.subtitle : s.captions.title,
                })}
              </Checkbox>
              {item.Before ? <del className="text-xs text-fg3">{item.Before}</del> : null}
              <b className="text-[13.5px]">{item.After}</b>
              {item.Reason ? <span className="text-xs text-fg3">{item.Reason}</span> : null}
            </li>
          ))}
        </ul>
        <p className="text-xs text-fg3">{s.ai.privacy}</p>
      </div>
    </Modal>
  )
}

/** Design 12.7 to 12.9: rows are slots, columns locales; overflow comes from the server's measurement on save. */
export function CaptionsPanel({ editor, canEdit }: { editor: TemplateEditor; canEdit: boolean }) {
  const s = useStudioT()
  const aiEnabled = useFeature('StudioAiCaptions')
  const draft = editor.draft!
  const [onlyProblems, setOnlyProblems] = useState(false)
  const [column, setColumn] = useState<string>('')
  const [copyFrom, setCopyFrom] = useState(draft.PrimaryLocale)
  const [copyTo, setCopyTo] = useState(draft.Locales.find((locale) => locale !== draft.PrimaryLocale) ?? draft.PrimaryLocale)
  const [overwrite, setOverwrite] = useState(false)
  const [suggesting, setSuggesting] = useState(false)
  const [aiOff, setAiOff] = useState<string | null>(null)

  const locales = column ? [column] : draft.Locales
  const orders = Array.from({ length: draft.SlotCount }, (_, index) => index + 1)
  const problemOrders = orders.filter((order) => editor.captionWarnings.some((warning) => warning.SlotOrder === order))
  const rows = onlyProblems ? problemOrders : orders

  const copy = () => {
    editor.replaceCaptions(copyCaptions(editor.captions, copyFrom, copyTo, draft.SlotCount, overwrite))
  }

  return (
    <div className="grid gap-4">
      <div className="toolbar flex-wrap" role="group" aria-label={s.captions.tools}>
        <Chip selected={onlyProblems} onClick={() => setOnlyProblems((value) => !value)}>
          {interpolate(s.captions.onlyProblems, { count: problemOrders.length })}
        </Chip>
        <span className="w-40">
          <Select
            aria-label={s.captions.column}
            value={column}
            options={[{ value: '', label: s.captions.allColumns }, ...draft.Locales.map((value) => ({ value, label: value }))]}
            onChange={setColumn}
          />
        </span>
        {aiEnabled && canEdit ? (
          <Button
            className="ml-auto"
            disabled={aiOff !== null || draft.Locales.length < 2}
            title={aiOff ? s.ai.disabledTitle : undefined}
            onClick={() => setSuggesting(true)}
          >
            <Sparkles aria-hidden />
            {s.ai.button}
          </Button>
        ) : null}
      </div>
      {aiOff ? (
        <Callout tone="warning" title={s.ai.disabledTitle}>
          {aiOff}
        </Callout>
      ) : null}
      {canEdit && draft.Locales.length > 1 ? (
        <fieldset className="card flex flex-wrap items-end gap-3 p-3">
          <legend className="sr-only">{s.captions.copy}</legend>
          <span className="w-32">
            <Field label={s.captions.copyFrom}>
              <Select value={copyFrom} options={draft.Locales.map((value) => ({ value, label: value }))} onChange={setCopyFrom} />
            </Field>
          </span>
          <span className="w-32">
            <Field label={s.captions.copyTo}>
              <Select value={copyTo} options={draft.Locales.map((value) => ({ value, label: value }))} onChange={setCopyTo} />
            </Field>
          </span>
          <Checkbox checked={overwrite} onChange={setOverwrite} className="mb-2">
            {s.captions.overwrite}
          </Checkbox>
          <Button className="mb-0.5" disabled={copyFrom === copyTo} onClick={copy}>
            {s.captions.copy}
          </Button>
        </fieldset>
      ) : null}
      {editor.measuredClassKey ? (
        <p className="text-xs text-fg3">{interpolate(s.captions.measured, { classKey: editor.measuredClassKey })}</p>
      ) : null}
      <div className="tbl-w">
        <table className="tbl">
          <caption className="sr-only">{s.captions.grid}</caption>
          <thead>
            <tr>
              <th scope="col" className="w-12">
                {s.captions.slot}
              </th>
              {locales.map((locale) => (
                <th key={locale} scope="col">
                  {locale === draft.PrimaryLocale ? interpolate(s.captions.primaryColumn, { locale }) : locale}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={locales.length + 1} className="text-[13px] text-fg3">
                  {s.captions.noProblems}
                </td>
              </tr>
            ) : null}
            {rows.map((order) => (
              <tr key={order}>
                <th scope="row" className="mono align-top">
                  {order}
                </th>
                {locales.map((locale) => {
                  const key = cellKey(locale, order)
                  const cell = editor.captions[key] ?? EMPTY
                  const saved = editor.savedCaptions[key] ?? EMPTY
                  const changed = cell.title !== saved.title || cell.subtitle !== saved.subtitle
                  const problems = cellWarnings(editor.captionWarnings, locale, order)
                  const fallback =
                    locale !== draft.PrimaryLocale ? interpolate(s.captions.fallback, { locale: draft.PrimaryLocale }) : undefined
                  return (
                    <td key={locale} className={cn('min-w-[220px] align-top', problems.length > 0 && 'bg-danger/5')}>
                      <div className="grid gap-2">
                        <Field
                          label={interpolate(s.captions.titleOf, { order, locale })}
                          counter={{ value: cell.title.length, max: TITLE_SOFT_MAX }}
                        >
                          <Textarea
                            rows={2}
                            value={cell.title}
                            readOnly={!canEdit}
                            placeholder={fallback}
                            onChange={(event) => editor.setCaption(key, { ...cell, title: event.target.value })}
                          />
                        </Field>
                        <Field
                          label={interpolate(s.captions.subtitleOf, { order, locale })}
                          counter={{ value: cell.subtitle.length, max: SUBTITLE_SOFT_MAX }}
                        >
                          <Input
                            value={cell.subtitle}
                            readOnly={!canEdit}
                            onChange={(event) => editor.setCaption(key, { ...cell, subtitle: event.target.value })}
                          />
                        </Field>
                        {changed ? <span className="text-xs text-warningText">{s.captions.unsavedCell}</span> : null}
                        {problems.map((warning, index) => (
                          <span key={index} className="err-t" role="note">
                            {warningText(warning, s)}
                          </span>
                        ))}
                      </div>
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="text-xs text-fg3">{s.captions.saveHint}</p>
      {suggesting ? <SuggestModal editor={editor} onClose={() => setSuggesting(false)} onDisabled={setAiOff} /> : null}
    </div>
  )
}
