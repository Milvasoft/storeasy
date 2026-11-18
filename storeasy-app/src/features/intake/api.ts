import { apiDelete, apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type {
  ArtifactAppSummaryDto,
  ArtifactCountsDto,
  ArtifactSessionDetailDto,
  ArtifactSessionListDto,
  ArtifactSessionStatus,
  ArtifactSource,
  ArtifactSourceDto,
  ArtifactSourceSavedDto,
  ArtifactUploadDto,
  ArtifactUploadedPartDto,
  CiTokenCreatedDto,
  CiTokenDto,
  CreateCiTokenInput,
  CreateTrustedPublisherInput,
  DeliveryRuleDto,
  DeliveryRulesDto,
  IntakeSettingsDto,
  OpenArtifactSessionInput,
  SaveArtifactSourceInput,
  TrustedPublisherDto,
  TrustedPublisherTestDto,
} from './types'

export interface SessionFilter {
  AppId?: number | null
  Status?: ArtifactSessionStatus | null
  Source?: ArtifactSource | null
}

// storeasy-api ArtifactsController (design 13). Reads need View; uploads, delivering and deleting a session ReleaseTest;
// tokens, trusted publishers, rules, sources and settings ManageConnections. The anonymous pull receivers are not called.
export const intakeApi = {
  summary: (organizationId: number) => apiGet<ArtifactAppSummaryDto[]>('/Artifacts/summary', { OrganizationId: organizationId }),

  // Package counters of a period (at most 366 days; the API defaults to the last 30), by status and by source.
  counts: (organizationId: number, period: { From?: string | null; To?: string | null; AppId?: number | null } = {}) =>
    apiGet<ArtifactCountsDto>('/Artifacts/counts', { OrganizationId: organizationId, ...period }),

  sessions: (organizationId: number, request: ListRequest & SessionFilter) =>
    apiPatchList<ArtifactSessionListDto>('/Artifacts/sessions', { OrganizationId: organizationId, ...request }),

  session: (organizationId: number, sessionId: number) =>
    apiGet<ArtifactSessionDetailDto>('/Artifacts/session', { OrganizationId: organizationId, SessionId: sessionId }),

  deliver: (organizationId: number, sessionId: number) =>
    apiPost<ArtifactSessionDetailDto>('/Artifacts/session/deliver', { OrganizationId: organizationId, SessionId: sessionId }),

  deleteSession: (organizationId: number, sessionId: number) =>
    apiDelete<number>('/Artifacts/session', undefined, { OrganizationId: organizationId, SessionId: sessionId }),

  // POST /api/v1.0/apps/{appId}/artifacts: a new session, the open one of the same file resumed, or a duplicate.
  open: (organizationId: number, appId: number, input: OpenArtifactSessionInput) =>
    apiPost<ArtifactUploadDto>(`/apps/${appId}/artifacts`, { OrganizationId: organizationId, ...input }),

  complete: (organizationId: number, sessionId: number, parts: ArtifactUploadedPartDto[]) =>
    apiPut<ArtifactSessionListDto>(`/Artifacts/${sessionId}/complete`, { OrganizationId: organizationId, Parts: parts }),

  ciTokens: (organizationId: number) => apiGet<CiTokenDto[]>('/Artifacts/ci-tokens', { OrganizationId: organizationId }),

  createCiToken: (organizationId: number, input: CreateCiTokenInput) =>
    apiPost<CiTokenCreatedDto>('/Artifacts/ci-token', { OrganizationId: organizationId, ...input }, { idempotent: true }),

  revokeCiToken: (organizationId: number, id: number) =>
    apiDelete<number>('/Artifacts/ci-token', undefined, { OrganizationId: organizationId, Id: id }),

  publishers: (organizationId: number) =>
    apiGet<TrustedPublisherDto[]>('/Artifacts/trusted-publishers', { OrganizationId: organizationId }),

  publisher: (organizationId: number, id: number) =>
    apiGet<TrustedPublisherDto>('/Artifacts/trusted-publisher', { OrganizationId: organizationId, Id: id }),

  createPublisher: (organizationId: number, input: CreateTrustedPublisherInput) =>
    apiPost<number>('/Artifacts/trusted-publisher', { OrganizationId: organizationId, ...input }, { idempotent: true }),

  deletePublisher: (organizationId: number, id: number) =>
    apiDelete<number>('/Artifacts/trusted-publisher', undefined, { OrganizationId: organizationId, Id: id }),

  // A dry run: the token is checked against the rule, nothing is issued and the token is not stored.
  testPublisher: (organizationId: number, id: number, token: string) =>
    apiPost<TrustedPublisherTestDto>('/Artifacts/trusted-publisher/test', { OrganizationId: organizationId, Id: id, Token: token }),

  rules: (organizationId: number, appId: number) =>
    apiGet<DeliveryRulesDto>('/Artifacts/delivery-rules', { OrganizationId: organizationId, AppId: appId }),

  saveRules: (organizationId: number, appId: number, rules: DeliveryRuleDto[]) =>
    apiPut<number>('/Artifacts/delivery-rules', { OrganizationId: organizationId, AppId: appId, Rules: rules }),

  sources: (organizationId: number) => apiGet<ArtifactSourceDto[]>('/Artifacts/sources', { OrganizationId: organizationId }),

  saveSource: (organizationId: number, input: SaveArtifactSourceInput) =>
    apiPut<ArtifactSourceSavedDto>('/Artifacts/source', { OrganizationId: organizationId, ...input }),

  deleteSource: (organizationId: number, id: number) =>
    apiDelete<number>('/Artifacts/source', undefined, { OrganizationId: organizationId, Id: id }),

  settings: (organizationId: number) => apiGet<IntakeSettingsDto>('/Artifacts/settings', { OrganizationId: organizationId }),

  saveSettings: (organizationId: number, input: IntakeSettingsDto) =>
    apiPut<number>('/Artifacts/settings', { OrganizationId: organizationId, ...input }),
}

export const intakeKeys = {
  all: ['intake'] as const,
  summary: (organizationId: number) => ['intake', 'summary', organizationId] as const,
  counts: (organizationId: number, from: string) => ['intake', 'counts', organizationId, from] as const,
  sessions: (organizationId: number, request: ListRequest & SessionFilter) => ['intake', 'sessions', organizationId, request] as const,
  session: (organizationId: number, sessionId: number) => ['intake', 'session', organizationId, sessionId] as const,
  ciTokens: (organizationId: number) => ['intake', 'ci-tokens', organizationId] as const,
  publishers: (organizationId: number) => ['intake', 'publishers', organizationId] as const,
  publisher: (organizationId: number, id: number) => ['intake', 'publisher', organizationId, id] as const,
  rules: (organizationId: number, appId: number) => ['intake', 'rules', organizationId, appId] as const,
  sources: (organizationId: number) => ['intake', 'sources', organizationId] as const,
  settings: (organizationId: number) => ['intake', 'settings', organizationId] as const,
}
