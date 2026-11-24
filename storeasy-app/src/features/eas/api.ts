import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api/http'
import type {
  EasAppDto,
  EasBuildDto,
  EasChainDto,
  EasProfilesDto,
  EasProjectDto,
  EasStartInput,
  EasSubmissionDto,
  EasSubmitInput,
  ExpoConnectionDto,
} from './types'

// The EAS endpoints (storeasy-api EasController, route prefix eas). Builds start only from a member's request with the typed
// confirmation; the controller answers 503 while the EasBuilds feature flag is off. easContract.test.ts checks every endpoint
// has a handler in test/support/eas.ts.

export const easApi = {
  connection: (organizationId: number) => apiGet<ExpoConnectionDto | null>('/eas/connection', { OrganizationId: organizationId }),

  // The token goes to storeasy once and is sealed there; it is never sent back.
  saveConnection: (organizationId: number, token: string) =>
    apiPost<ExpoConnectionDto>('/eas/connection', { OrganizationId: organizationId, Token: token }),

  removeConnection: (organizationId: number) => apiDelete<number>('/eas/connection', undefined, { OrganizationId: organizationId }),

  projects: (organizationId: number, appId: number) =>
    apiGet<EasProjectDto[]>('/eas/projects', { OrganizationId: organizationId, AppId: appId }),

  // Links the app to a project, or unlinks it with null.
  link: (organizationId: number, appId: number, projectId: string | null) =>
    apiPut<EasAppDto>('/eas/project', { OrganizationId: organizationId, AppId: appId, ProjectId: projectId }),

  app: (organizationId: number, appId: number) => apiGet<EasAppDto>('/eas/app', { OrganizationId: organizationId, AppId: appId }),

  profiles: (organizationId: number, appId: number, gitRef?: string) =>
    apiGet<EasProfilesDto>('/eas/profiles', { OrganizationId: organizationId, AppId: appId, ...(gitRef ? { GitRef: gitRef } : {}) }),

  start: (organizationId: number, appId: number, input: EasStartInput) =>
    apiPost<EasBuildDto[]>('/eas/build/start', { OrganizationId: organizationId, AppId: appId, ...input }),

  build: (organizationId: number, id: number) => apiGet<EasBuildDto>('/eas/build', { OrganizationId: organizationId, Id: id }),

  cancel: (organizationId: number, id: number) => apiPost<EasBuildDto>('/eas/build/cancel', { OrganizationId: organizationId, Id: id }),

  sendToIntake: (organizationId: number, id: number) =>
    apiPost<EasBuildDto>('/eas/build/intake', { OrganizationId: organizationId, Id: id }),

  sync: (organizationId: number, appId: number) => apiPost<number>('/eas/sync', { OrganizationId: organizationId, AppId: appId }),

  saveChain: (organizationId: number, appId: number, chain: EasChainDto) =>
    apiPut<EasAppDto>('/eas/chain', { OrganizationId: organizationId, AppId: appId, Chain: chain }),

  submit: (organizationId: number, id: number, input: EasSubmitInput) =>
    apiPost<EasSubmissionDto>('/eas/submission/start', { OrganizationId: organizationId, Id: id, ...input }),

  submissions: (organizationId: number, appId: number) =>
    apiGet<EasSubmissionDto[]>('/eas/submissions', { OrganizationId: organizationId, AppId: appId }),

  submission: (organizationId: number, id: number) =>
    apiGet<EasSubmissionDto>('/eas/submission', { OrganizationId: organizationId, Id: id }),
}
