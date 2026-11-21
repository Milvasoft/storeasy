import { apiGet, apiPost } from '@/lib/api/http'
import type { ChangePlanDto } from '@/features/common/types'
import type { WingetChangeInput, WingetManifestDto, WingetPackageAddedDto, WingetPackageDto, WingetPullRequestDto } from './types'

// The winget endpoints (storeasy-api WingetController, route prefix winget). package and pull-requests read storeasy's
// rows; manifest reads winget-pkgs in the request. Every write is a change plan: change is always a dry run and the saved
// plan is approved through plansApi.approve (ChangePlanKind.WingetChange), which opens the pull request.
// wingetContract.test.ts reads this file and checks that every endpoint has a handler in test/support/winget.ts.

export const wingetApi = {
  // winget has no API that lists a publisher's packages: a package is added by its identifier.
  addPackage: (organizationId: number, connectionId: number, packageIdentifier: string, appId: number | null, name: string | null) =>
    apiPost<WingetPackageAddedDto>(
      '/winget/package',
      { OrganizationId: organizationId, ConnectionId: connectionId, PackageIdentifier: packageIdentifier, AppId: appId, Name: name },
      { idempotent: true },
    ),

  package: (organizationId: number, storeAppId: number) =>
    apiGet<WingetPackageDto>('/winget/package', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  manifest: (organizationId: number, storeAppId: number, version: string | null) =>
    apiGet<WingetManifestDto>('/winget/manifest', { OrganizationId: organizationId, StoreAppId: storeAppId, Version: version }),

  pullRequests: (organizationId: number, storeAppId: number) =>
    apiGet<WingetPullRequestDto[]>('/winget/pull-requests', { OrganizationId: organizationId, StoreAppId: storeAppId }),

  change: (organizationId: number, storeAppId: number, change: WingetChangeInput) =>
    apiPost<ChangePlanDto>('/winget/change', { OrganizationId: organizationId, StoreAppId: storeAppId, ...change, DryRun: true }),

  sync: (organizationId: number, storeAppId: number) =>
    apiPost<number>('/winget/sync', { OrganizationId: organizationId, StoreAppId: storeAppId }),
}
