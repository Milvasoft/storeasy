import { apiGet, apiPost, apiPut } from '@/lib/api/http'
import type { StoreConnectionDto } from '@/lib/api/types'
import type {
  MsAddonDto,
  MsChangeInputDto,
  MsChangePlanDto,
  MsContentDto,
  MsExpiringConnectionDto,
  MsFlightDto,
  MsInstallerPackageDto,
  MsProductAddedDto,
  MsProductDto,
  MsSubmissionDto,
  MsSubmissionTarget,
} from './types'

// The Microsoft Store endpoints (storeasy-api MsController, route prefix ms). Reads of storeasy's rows never call
// Microsoft; content and change read the store in the request. Every store write is a change plan: change is always sent
// with DryRun true and the saved plan is approved through plansApi.approve (ChangePlanKind.MsChange).
// msContract.test.ts reads this file and checks that every endpoint has a handler in test/support/microsoft.ts.

export interface MsSubmitInput {
  Target: MsSubmissionTarget
  FlightId?: string | null
  AddonStoreId?: string | null
  // The product's Store id, typed by the user (400 MsConfirmationMismatch otherwise).
  Confirm: string
}

export const msApi = {
  // MSI/EXE products only: the API cannot list them, so they are added by Store id; a sync job confirms the record.
  addProduct: (organizationId: number, connectionId: number, productId: string, name: string | null) =>
    apiPost<MsProductAddedDto>(
      '/ms/product',
      { OrganizationId: organizationId, ConnectionId: connectionId, ProductId: productId, Name: name },
      { idempotent: true },
    ),

  product: (organizationId: number, storeAppId: number) =>
    apiGet<MsProductDto>('/ms/product', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  submissions: (organizationId: number, storeAppId: number) =>
    apiGet<MsSubmissionDto[]>('/ms/submissions', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  flights: (organizationId: number, storeAppId: number) =>
    apiGet<MsFlightDto[]>('/ms/flights', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  addons: (organizationId: number, storeAppId: number) =>
    apiGet<MsAddonDto[]>('/ms/addons', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  packages: (organizationId: number, storeAppId: number) =>
    apiGet<MsInstallerPackageDto[]>('/ms/packages', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  // Reads the store (rate limited): the pending submission's content, else the last published one.
  content: (organizationId: number, storeAppId: number, target: { FlightId?: string | null; AddonStoreId?: string | null } = {}) =>
    apiGet<MsContentDto>('/ms/content', {
      OrganizationId: organizationId,
      StoreAppId: storeAppId,
      FlightId: target.FlightId ?? null,
      AddonStoreId: target.AddonStoreId ?? null,
    }),

  // Always a dry run: the answer is the plan (or why it is blocked); a saved plan carries PlanId.
  change: (organizationId: number, storeAppId: number, change: MsChangeInputDto) =>
    apiPost<MsChangePlanDto>('/ms/change', { OrganizationId: organizationId, StoreAppId: storeAppId, Change: change, DryRun: true }),

  // SubmitForReview only; answers the job id.
  submit: (organizationId: number, storeAppId: number, input: MsSubmitInput) =>
    apiPost<number>('/ms/submission/submit', { OrganizationId: organizationId, StoreAppId: storeAppId, ...input }, { idempotent: true }),

  // Empty PackageUrls checks every installer of the product; answers the job id.
  verifyPackages: (organizationId: number, storeAppId: number, packageUrls: string[] = []) =>
    apiPost<number>('/ms/packages/verify', { OrganizationId: organizationId, StoreAppId: storeAppId, PackageUrls: packageUrls }),

  saveFlightGroup: (organizationId: number, groupId: string, name: string, memberCount: number | null) =>
    apiPut<number>('/ms/flight-group', { OrganizationId: organizationId, GroupId: groupId, Name: name, MemberCount: memberCount }),

  // The new secret is verified against Microsoft first; when that fails the old one stays.
  rotateSecret: (organizationId: number, connectionId: number, secret: string, credentialExpiryDate: string) =>
    apiPut<StoreConnectionDto>('/ms/connection/secret', {
      OrganizationId: organizationId,
      ConnectionId: connectionId,
      Secret: secret,
      CredentialExpiryDate: credentialExpiryDate,
    }),

  expiringConnections: (organizationId: number, days?: number) =>
    apiGet<MsExpiringConnectionDto[]>('/ms/connections/expiring', { OrganizationId: organizationId, Days: days ?? null }),
}
