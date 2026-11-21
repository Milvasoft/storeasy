import { useQuery } from '@tanstack/react-query'
import type { ChangePlanDto } from '@/features/common/types'
import { apiDelete, apiGet, apiPost } from '@/lib/api/http'
import type {
  AsoExperimentDto,
  AsoExperimentExportDto,
  AsoExperimentOverviewDto,
  PlanExperimentInput,
  RecordResultInput,
  SaveExperimentInput,
} from './types'

// AsoExperimentsController (/api/v1.0/aso-experiments). Reads need View; drafts, results, the checklist and the sync
// need EditContent. Store writes are change plans: the plan's own permission (Submit: SubmitForReview; Start, Stop and
// ApplyWinner: ReleaseProduction) and typed confirmation are checked when it is approved.

export const experimentsApi = {
  overview: (organizationId: number, appId: number) =>
    apiGet<AsoExperimentOverviewDto>('/aso-experiments', { OrganizationId: organizationId, AppId: appId }),

  // 404 AsoExperimentNotFound.
  detail: (organizationId: number, id: number) =>
    apiGet<AsoExperimentDto>('/aso-experiments/experiment', { OrganizationId: organizationId, Id: id }),

  // Id null creates. 409 AsoExperimentNotEditable once the experiment left Draft and Prepared.
  save: (organizationId: number, input: SaveExperimentInput) =>
    apiPost<AsoExperimentDto>('/aso-experiments/experiment', { OrganizationId: organizationId, Id: null, ...input }),

  // Only records never written to a store, and Play records.
  remove: (organizationId: number, id: number) =>
    apiDelete<boolean>('/aso-experiments/experiment', { OrganizationId: organizationId, Id: id }),

  // Always a dry run: the answer is a pending plan, approved through the plans flow.
  plan: (organizationId: number, input: PlanExperimentInput) =>
    apiPost<ChangePlanDto>('/aso-experiments/experiment/plan', { OrganizationId: organizationId, ...input, DryRun: true }),

  recordResult: (organizationId: number, input: RecordResultInput) =>
    apiPost<AsoExperimentDto>('/aso-experiments/experiment/result', { OrganizationId: organizationId, ...input }),

  checklist: (organizationId: number, id: number, doneItems: number[]) =>
    apiPost<AsoExperimentDto>('/aso-experiments/experiment/checklist', { OrganizationId: organizationId, Id: id, DoneItems: doneItems }),

  exportFiles: (organizationId: number, id: number) =>
    apiGet<AsoExperimentExportDto>('/aso-experiments/experiment/export', { OrganizationId: organizationId, Id: id }),

  // Answers the job id of the read of the record's Apple experiments and custom product pages.
  sync: (organizationId: number, storeAppId: number) =>
    apiPost<number>('/aso-experiments/sync', { OrganizationId: organizationId, StoreAppId: storeAppId }),
}

// Under 'content', so an approved plan (which invalidates contentKeys.all) refreshes the experiments too.
export const experimentKeys = {
  all: ['content', 'experiments'] as const,
  overview: (organizationId: number, appId: number) => ['content', 'experiments', 'overview', organizationId, appId] as const,
  detail: (organizationId: number, id: number) => ['content', 'experiments', 'detail', organizationId, id] as const,
  exportFiles: (organizationId: number, id: number) => ['content', 'experiments', 'export', organizationId, id] as const,
}

export function useExperimentOverview(organizationId: number, appId: number) {
  return useQuery({
    queryKey: experimentKeys.overview(organizationId, appId),
    queryFn: () => experimentsApi.overview(organizationId, appId),
    enabled: appId > 0,
  })
}

export function useExperiment(organizationId: number, id: number) {
  return useQuery({
    queryKey: experimentKeys.detail(organizationId, id),
    queryFn: () => experimentsApi.detail(organizationId, id),
    enabled: id > 0,
  })
}
