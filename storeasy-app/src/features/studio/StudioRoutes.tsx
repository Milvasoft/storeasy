import { Route, Routes } from 'react-router-dom'
import { NotFoundPage } from '@/pages/StatePages'
import { ExportJobPage } from './ExportJobPage'
import { FramesPage } from './FramesPage'
import { StudioPage } from './StudioPage'
import { TemplateEditorRoute } from './TemplateEditorPage'

/** Everything under /studio (design 12), one lazy chunk. */
export function StudioRoutes() {
  return (
    <Routes>
      <Route index element={<StudioPage />} />
      <Route path="frames" element={<FramesPage />} />
      <Route path=":templateId" element={<TemplateEditorRoute tab="design" />} />
      <Route path=":templateId/captions" element={<TemplateEditorRoute tab="captions" />} />
      <Route path=":templateId/export" element={<TemplateEditorRoute tab="export" />} />
      <Route path=":templateId/export/:jobId" element={<ExportJobPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
