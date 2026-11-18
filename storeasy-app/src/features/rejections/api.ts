import { apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type { StoreKind } from '@/lib/api/types'
import type {
  RejectionCaseDto,
  RejectionCaseListDto,
  RejectionChecklistItem,
  RejectionHistoryDto,
  RejectionKind,
  RejectionReplyDraftDto,
  RejectionState,
} from './types'

// storeasy-api RejectionsController. The App Store Connect API can neither read nor write Resolution Center: the text is
// pasted, the reply is a draft sent by hand or through the assisted step, then marked sent here.
export const rejectionsApi = {
  list: (organizationId: number, filter: { AppId?: number | null; State?: RejectionState | null }, request: ListRequest) =>
    apiPatchList<RejectionCaseListDto>('/Rejections', { OrganizationId: organizationId, ...filter, ...request }),

  detail: (organizationId: number, caseId: number) =>
    apiGet<RejectionCaseDto>('/Rejections/case', { OrganizationId: organizationId, CaseId: caseId }),

  open: (
    organizationId: number,
    input: { AppId: number; Store: StoreKind; VersionString: string; BuildNumber: string | null; Kind: RejectionKind },
  ) => apiPost<number>('/Rejections/case', { OrganizationId: organizationId, ...input }, { idempotent: true }),

  // The server finds the guideline numbers in the text and builds the checklist from them.
  updateText: (organizationId: number, caseId: number, rawText: string) =>
    apiPut<RejectionCaseDto>('/Rejections/case/text', { OrganizationId: organizationId, CaseId: caseId, RawText: rawText }),

  updateChecklist: (organizationId: number, caseId: number, items: RejectionChecklistItem[]) =>
    apiPut<RejectionCaseDto>('/Rejections/case/checklist', { OrganizationId: organizationId, CaseId: caseId, Items: items }),

  updateReplyDraft: (organizationId: number, caseId: number, body: string) =>
    apiPut<RejectionReplyDraftDto>('/Rejections/case/reply-draft', { OrganizationId: organizationId, CaseId: caseId, Body: body }),

  // Refused while the draft is over the Resolution Center limit.
  markReplySent: (organizationId: number, caseId: number) =>
    apiPut<RejectionReplyDraftDto>('/Rejections/case/reply-sent', { OrganizationId: organizationId, CaseId: caseId }),

  close: (organizationId: number, caseId: number, note: string | null) =>
    apiPut<number>('/Rejections/case/close', { OrganizationId: organizationId, CaseId: caseId, Note: note }),

  history: (organizationId: number, appId: number) =>
    apiGet<RejectionHistoryDto>('/Rejections/history', { OrganizationId: organizationId, AppId: appId }),
}

export const rejectionKeys = {
  all: ['rejections'] as const,
  list: (organizationId: number, appId: number, state: RejectionState | null, request: ListRequest) =>
    ['rejections', 'list', organizationId, appId, state, request] as const,
  detail: (organizationId: number, caseId: number) => ['rejections', 'detail', organizationId, caseId] as const,
  history: (organizationId: number, appId: number) => ['rejections', 'history', organizationId, appId] as const,
}
