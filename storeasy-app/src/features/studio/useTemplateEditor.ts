import { useCallback, useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiErrorMessage, hasApiKey } from '@/lib/api/http'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { studioApi, TEMPLATE_CHANGED_KEY, type UpdateTemplateInput } from './api'
import { useStudioT } from './i18n'
import { studioKeys } from './keys'
import { captionMap, changedCaptions, sameJson, type CaptionCell } from './spec'
import type { MockupCaptionDto, MockupSlotDto, MockupSpec, MockupTemplateDetailDto, MockupWarningDto } from './types'

/** The part of a template the editor changes and PUT template saves. */
export interface TemplateDraft {
  Name: string
  Spec: MockupSpec
  Locales: string[]
  PrimaryLocale: string
  SlotCount: number
  Slots: MockupSlotDto[]
}

export type ChangeKey = 'name' | 'locales' | 'slots' | 'canvas' | 'background' | 'frame' | 'text' | 'layers' | 'captions'

export function toDraft(detail: MockupTemplateDetailDto): TemplateDraft {
  return {
    Name: detail.Name,
    Spec: detail.Spec,
    Locales: detail.Locales ?? [],
    PrimaryLocale: detail.PrimaryLocale,
    SlotCount: detail.SlotCount,
    Slots: detail.Slots ?? [],
  }
}

/** Which parts of the draft differ from the saved template (the "n unsaved changes" summary, design 12.16). */
export function changesOf(saved: TemplateDraft, draft: TemplateDraft): ChangeKey[] {
  const out: ChangeKey[] = []
  if (saved.Name !== draft.Name) {
    out.push('name')
  }
  if (!sameJson(saved.Locales, draft.Locales) || saved.PrimaryLocale !== draft.PrimaryLocale) {
    out.push('locales')
  }
  if (saved.SlotCount !== draft.SlotCount || !sameJson(saved.Slots, draft.Slots)) {
    out.push('slots')
  }
  const parts: Array<[ChangeKey, keyof MockupSpec]> = [
    ['canvas', 'Canvas'],
    ['background', 'Background'],
    ['frame', 'Frame'],
    ['text', 'Text'],
    ['layers', 'Layers'],
  ]
  for (const [key, part] of parts) {
    if (!sameJson(saved.Spec[part], draft.Spec[part])) {
      out.push(key)
    }
  }
  return out
}

function mergeCaptions(saved: MockupCaptionDto[], items: MockupCaptionDto[]): MockupCaptionDto[] {
  const map = new Map(saved.map((caption) => [`${caption.Locale}|${caption.SlotOrder}`, caption]))
  for (const item of items) {
    const key = `${item.Locale}|${item.SlotOrder}`
    if (!item.Title && !item.Subtitle) {
      map.delete(key)
    } else {
      map.set(key, item)
    }
  }
  return [...map.values()].sort((a, b) => a.Locale.localeCompare(b.Locale) || a.SlotOrder - b.SlotOrder)
}

/**
 * The editor's document: the saved template (query), the draft on top of it and the caption cells. A save sends the
 * version it started from; the answer's new version goes straight into the cache (no refetch), and a stale version (409
 * MockupTemplateChanged) opens the conflict choice instead of overwriting.
 */
export function useTemplateEditor(templateId: number) {
  const s = useStudioT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const key = studioKeys.template(organization.Id, templateId)
  const detail = useQuery({ queryKey: key, queryFn: () => studioApi.template(organization.Id, templateId) })

  const [draftState, setDraftState] = useState<TemplateDraft | null>(null)
  const [captionState, setCaptionState] = useState<Record<string, CaptionCell> | null>(null)
  const [captionWarnings, setCaptionWarnings] = useState<MockupWarningDto[]>([])
  const [measuredClassKey, setMeasuredClassKey] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)

  const saved = useMemo(() => (detail.data ? toDraft(detail.data) : null), [detail.data])
  const savedCaptions = useMemo(() => captionMap(detail.data?.Captions ?? []), [detail.data])
  const draft = draftState ?? saved
  const captions = captionState ?? savedCaptions
  const changes = useMemo(() => (saved && draft ? changesOf(saved, draft) : []), [saved, draft])
  const captionItems = useMemo(() => changedCaptions(savedCaptions, captions), [savedCaptions, captions])
  const allChanges: ChangeKey[] = captionItems.length > 0 ? [...changes, 'captions'] : changes
  const dirty = allChanges.length > 0

  // Leaving the page with unsaved work: the browser's own warning (design 12.16).
  useEffect(() => {
    if (!dirty) {
      return
    }
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault()
    }
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [dirty])

  const update = useCallback(
    (change: (draft: TemplateDraft) => TemplateDraft) => {
      setDraftState((current) => {
        const base = current ?? saved
        return base ? change(base) : current
      })
    },
    [saved],
  )

  const updateSpec = useCallback((change: (spec: MockupSpec) => MockupSpec) => update((d) => ({ ...d, Spec: change(d.Spec) })), [update])

  const setCaption = useCallback(
    (cellKey: string, cell: CaptionCell) => setCaptionState((current) => ({ ...(current ?? savedCaptions), [cellKey]: cell })),
    [savedCaptions],
  )
  const replaceCaptions = useCallback((next: Record<string, CaptionCell>) => setCaptionState(next), [])

  const saveTemplate = useMutation({
    mutationFn: (input: UpdateTemplateInput) => studioApi.update(organization.Id, input),
    meta: { silentError: true },
    onSuccess: (version, input) => {
      // The answer is the new version: the cache takes what was sent, so the draft is clean without a refetch.
      queryClient.setQueryData<MockupTemplateDetailDto>(key, (old) =>
        old
          ? {
              ...old,
              Name: input.Name,
              Spec: input.Spec,
              Locales: input.Locales,
              PrimaryLocale: input.PrimaryLocale,
              SlotCount: input.SlotCount,
              Slots: input.Slots,
              Version: version,
              LastModificationDate: new Date().toISOString(),
            }
          : old,
      )
      void queryClient.invalidateQueries({ queryKey: ['studio', 'templates'] })
      void queryClient.invalidateQueries({ queryKey: ['studio', 'preview'] })
      void queryClient.invalidateQueries({ queryKey: ['studio', 'export-plan'] })
    },
    onError: (error) => {
      if (hasApiKey(error, TEMPLATE_CHANGED_KEY)) {
        setConflict(true)
      } else {
        showToast(apiErrorMessage(error, s.common.error), 'error')
      }
    },
  })

  const saveCaptions = useMutation({
    mutationFn: (items: MockupCaptionDto[]) => studioApi.saveCaptions(organization.Id, templateId, items),
    onSuccess: (result, items) => {
      queryClient.setQueryData<MockupTemplateDetailDto>(key, (old) =>
        old ? { ...old, Captions: mergeCaptions(old.Captions ?? [], items) } : old,
      )
      setCaptionWarnings(result.Warnings ?? [])
      setMeasuredClassKey(result.MeasuredClassKey ?? null)
      void queryClient.invalidateQueries({ queryKey: ['studio', 'preview'] })
    },
  })

  const save = useCallback(async () => {
    if (!detail.data || !draft) {
      return
    }
    try {
      if (changes.length > 0) {
        await saveTemplate.mutateAsync({ TemplateId: templateId, Version: detail.data.Version, ...draft })
      }
      if (captionItems.length > 0) {
        await saveCaptions.mutateAsync(captionItems)
      }
      showToast(s.editor.saved, 'success')
    } catch {
      /* shown by the mutation: the conflict dialog or the error toast */
    }
  }, [detail.data, draft, changes.length, captionItems, saveTemplate, saveCaptions, templateId, s])

  /** Drops the draft and shows the template as it is saved now (the other editor's version). */
  const reload = useCallback(async () => {
    await detail.refetch()
    setDraftState(null)
    setConflict(false)
  }, [detail])

  /** Keeps my work as a new template: a copy of this one, then my design saved over the copy (no overwrite here). */
  const saveAsNew = useMutation({
    mutationFn: async () => {
      if (!detail.data || !draft) {
        throw new Error('no template')
      }
      const id = await studioApi.create(organization.Id, {
        AppId: detail.data.AppId,
        Name: `${draft.Name} (${s.editor.mine})`,
        Locales: draft.Locales,
        PrimaryLocale: draft.PrimaryLocale,
        CopyFromTemplateId: templateId,
        CopyCaptions: true,
      })
      const copy = await studioApi.template(organization.Id, id)
      await studioApi.update(organization.Id, {
        TemplateId: id,
        Version: copy.Version,
        ...draft,
        Name: copy.Name,
      })
      if (captionItems.length > 0) {
        await studioApi.saveCaptions(organization.Id, id, captionItems)
      }
      return id
    },
    onSuccess: () => {
      setConflict(false)
      void queryClient.invalidateQueries({ queryKey: ['studio', 'templates'] })
    },
  })

  return {
    detail,
    saved,
    draft,
    update,
    updateSpec,
    captions,
    savedCaptions,
    setCaption,
    replaceCaptions,
    captionWarnings,
    measuredClassKey,
    changes: allChanges,
    templateDirty: changes.length > 0,
    captionsDirty: captionItems.length > 0,
    dirty,
    save,
    saving: saveTemplate.isPending || saveCaptions.isPending,
    conflict,
    closeConflict: () => setConflict(false),
    reload,
    saveAsNew,
  }
}

export type TemplateEditor = ReturnType<typeof useTemplateEditor>
