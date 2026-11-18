import type { ListRequest } from '@/lib/api/list'
import type { ExportSelection, PreviewInput } from './api'

// Query keys of the studio, in the app's shape: [resource, scope, organizationId, ...args].
export const studioKeys = {
  all: ['studio'] as const,
  templates: (organizationId: number, request: ListRequest, appId: number | null) =>
    ['studio', 'templates', organizationId, request, appId] as const,
  template: (organizationId: number, templateId: number) => ['studio', 'template', organizationId, templateId] as const,
  frames: (organizationId: number) => ['studio', 'frames', organizationId] as const,
  starters: (organizationId: number) => ['studio', 'starters', organizationId] as const,
  preview: (organizationId: number, input: PreviewInput) => ['studio', 'preview', organizationId, input] as const,
  exportPlan: (organizationId: number, selection: ExportSelection) => ['studio', 'export-plan', organizationId, selection] as const,
  manifest: (organizationId: number, jobId: number) => ['studio', 'manifest', organizationId, jobId] as const,
}
