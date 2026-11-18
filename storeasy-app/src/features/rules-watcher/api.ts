import { apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type {
  RuleProposalDecisionDto,
  RuleProposalDetailDto,
  RuleProposalListDto,
  RuleProposalRejectReason,
  RuleSourceDto,
  RuleWatchResultDto,
} from './types'

// storeasy-api RuleWatcherController (api/v1.0/rules/...): Manager users with RuleCatalogManagement.List to read and
// RuleCatalogManagement.Approve to fetch, prepare and decide. Nothing in the catalog changes without an approval.
export const ruleWatcherApi = {
  sources: () => apiGet<RuleSourceDto[]>('/rules/sources'),

  // Reads every source now (10 an hour); a changed source opens a proposal.
  fetch: () => apiPost<RuleWatchResultDto>('/rules/sources/fetch'),

  proposals: (request: ListRequest) => apiPatchList<RuleProposalListDto>('/rules/proposals', request),

  proposal: (proposalId: number) => apiGet<RuleProposalDetailDto>('/rules/proposals/proposal', { ProposalId: proposalId }),

  prepare: (proposalId: number, input: { RuleIds: string[]; CatalogPatch: string | null; Note: string | null }) =>
    apiPut<number>('/rules/proposal/prepare', { ProposalId: proposalId, ...input }),

  // 409 when another administrator decided first.
  approve: (proposalId: number, notify: boolean, note: string | null) =>
    apiPut<RuleProposalDecisionDto>('/rules/proposal/approve', { ProposalId: proposalId, Notify: notify, Note: note }),

  reject: (proposalId: number, reason: RuleProposalRejectReason, note: string | null) =>
    apiPut<number>('/rules/proposal/reject', { ProposalId: proposalId, Reason: reason, Note: note }),
}

export const ruleWatcherKeys = {
  all: ['rule-watcher'] as const,
  sources: () => ['rule-watcher', 'sources'] as const,
  proposals: (request: ListRequest) => ['rule-watcher', 'proposals', request] as const,
  proposal: (proposalId: number) => ['rule-watcher', 'proposal', proposalId] as const,
}
