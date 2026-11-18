import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, Download } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Banner } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Tabs } from '@/components/ui/Tabs'
import { Button, Skeleton } from '@/components/ui/primitives'
import { OrgPermission } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { studioApi } from './api'
import { CaptionsPanel } from './CaptionsPanel'
import { DesignEditor } from './DesignEditor'
import { ExportPanel } from './ExportPanel'
import { useStudioT } from './i18n'
import { studioKeys } from './keys'
import { useZipDownload } from './useZipDownload'
import { useTemplateEditor, type TemplateEditor } from './useTemplateEditor'

export type EditorTab = 'design' | 'captions' | 'export'

function EditorSkeleton() {
  const s = useStudioT()
  return (
    <div className="grid gap-4 lg:grid-cols-[260px_1fr_300px]" aria-busy="true">
      <span className="sr-only" role="status">
        {s.editor.loading}
      </span>
      {[0, 1, 2].map((panel) => (
        <div key={panel} className="card grid content-start gap-3 p-4">
          <Skeleton shape="t" className="w-32" />
          <Skeleton className="h-24 w-full" />
          <Skeleton className="w-2/3" />
        </div>
      ))}
    </div>
  )
}

function ConflictModal({ editor }: { editor: TemplateEditor }) {
  const s = useStudioT()
  const navigate = useNavigate()
  return (
    <Modal
      open={editor.conflict}
      onClose={editor.closeConflict}
      role="alertdialog"
      title={s.conflict.title}
      description={s.conflict.body}
      size="sm"
      footer={
        <>
          <Button onClick={editor.closeConflict}>{s.conflict.stay}</Button>
          <Button variant="inverse" onClick={() => void editor.reload()}>
            {s.conflict.loadTheirs}
          </Button>
          <Button
            variant="primary"
            loading={editor.saveAsNew.isPending}
            onClick={() => editor.saveAsNew.mutate(undefined, { onSuccess: (id) => navigate(`/studio/${id}`) })}
          >
            {s.conflict.saveAsNew}
          </Button>
        </>
      }
    >
      {editor.detail.data?.LastModifierUserName ? (
        <p className="text-[13px] text-fg2">{interpolate(s.conflict.lastSaved, { user: editor.detail.data.LastModifierUserName })}</p>
      ) : null}
    </Modal>
  )
}

/** The template editor (design 12.4 to 12.16): the design, captions and export tabs over one document. */
export function TemplateEditorPage({ tab, templateId }: { tab: EditorTab; templateId: number }) {
  const s = useStudioT()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const canEdit = can(OrgPermission.EditContent)
  const editor = useTemplateEditor(templateId)
  const zip = useZipDownload()
  const frames = useQuery({
    queryKey: studioKeys.frames(organization.Id),
    queryFn: () => studioApi.frames(organization.Id),
    staleTime: 10 * 60_000,
  })
  const back = (
    <Link className="btn inv" to="/studio">
      {s.editor.backToList}
    </Link>
  )

  if (editor.detail.isLoading) {
    return <EditorSkeleton />
  }
  if (editor.detail.isError || !editor.detail.data || !editor.draft) {
    return <ScreenState error={editor.detail.error} onRetry={() => void editor.detail.refetch()} back={back} />
  }

  const template = editor.detail.data
  const base = `/studio/${templateId}`
  const warningCount = editor.captionWarnings.length
  return (
    <>
      <div className="pg-h">
        <div className="min-w-0">
          <Link to="/studio" className="lnk mb-1 inline-flex items-center gap-1 text-xs">
            <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
            {s.title}
          </Link>
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="pg-t truncate">{editor.draft.Name}</h1>
            {editor.dirty ? (
              <Badge tone="warning" small>
                {interpolate(s.editor.unsaved, { count: editor.changes.length })}
              </Badge>
            ) : null}
          </div>
          <p className="pg-s">
            {interpolate(s.editor.meta, { app: template.AppName ?? '', count: editor.draft.SlotCount })}
            {template.LastModificationDate
              ? ` · ${interpolate(s.editor.lastSaved, { date: format.dateTime(template.LastModificationDate), user: template.LastModifierUserName ?? '-' })}`
              : null}
          </p>
        </div>
        <div className="pg-a">
          {template.LastExportJobId ? (
            <Button loading={zip.pending} onClick={() => zip.download(template.LastExportJobId ?? 0)}>
              <Download aria-hidden />
              {s.editor.downloadLast}
            </Button>
          ) : null}
          {canEdit ? (
            <Button variant="primary" disabled={!editor.dirty} loading={editor.saving} onClick={() => void editor.save()}>
              {s.common.save}
            </Button>
          ) : null}
        </div>
      </div>
      {!canEdit ? (
        <div className="mb-4">
          <Banner tone="info" role="status">
            {s.editor.viewerBanner}
          </Banner>
        </div>
      ) : null}
      <Tabs
        label={s.editor.tabs}
        items={[
          { value: 'design', label: s.editor.design, to: base },
          {
            value: 'captions',
            label: s.editor.captions,
            to: `${base}/captions`,
            count: warningCount > 0 ? warningCount : undefined,
            countTone: 'danger',
          },
          { value: 'export', label: s.editor.export, to: `${base}/export` },
        ]}
      />
      <div className="mt-4">
        {tab === 'design' ? <DesignEditor editor={editor} frames={frames.data ?? []} canEdit={canEdit} /> : null}
        {tab === 'captions' ? <CaptionsPanel editor={editor} canEdit={canEdit} /> : null}
        {tab === 'export' ? <ExportPanel editor={editor} canEdit={canEdit} /> : null}
      </div>
      <ConflictModal editor={editor} />
    </>
  )
}

/** Keyed by the template, so opening another template starts with a fresh draft. */
export function TemplateEditorRoute({ tab }: { tab: EditorTab }) {
  const { templateId } = useParams()
  const id = Number(templateId)
  return <TemplateEditorPage key={id} tab={tab} templateId={id} />
}
