import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import {
  PackageAlertKind,
  PackageRegistry,
  type PackageAlertDto,
  type TrackedPackageDto,
  type TrackedPackageSummaryDto,
} from '@/features/registries/types'
import { make } from './factories'
import { endpoint, envelope } from './handlers'
import { server } from './server'

// The package registries fake backend (RegistriesController): DTO builders typed with the screens' own contract, the data
// the handlers answer with, and one handler per endpoint of src/features/registries/api.ts (registriesContract.test.ts
// checks the pairing).

export const NPM_PACKAGE_ID = 31
export const NUGET_PACKAGE_ID = 32

export function packageAlert(overrides: Partial<PackageAlertDto> = {}): PackageAlertDto {
  return make<PackageAlertDto>(
    {
      Id: 901,
      TrackedPackageId: NPM_PACKAGE_ID,
      Registry: PackageRegistry.Npm,
      PackageName: '@kuzeylab/pusula-sdk',
      Kind: PackageAlertKind.Advisory,
      Version: '2.4.0',
      AdvisoryId: 'GHSA-abcd-1234-wxyz',
      Severity: 'HIGH',
      Summary: 'Prototype pollution in the options parser',
      Url: 'https://osv.dev/vulnerability/GHSA-abcd-1234-wxyz',
      OpenedDate: '2026-11-21T08:00:00Z',
      ResolvedDate: null,
    },
    overrides,
  )
}

export function packageSummary(overrides: Partial<TrackedPackageSummaryDto> = {}): TrackedPackageSummaryDto {
  return make<TrackedPackageSummaryDto>(
    {
      Id: NPM_PACKAGE_ID,
      Registry: PackageRegistry.Npm,
      Name: '@kuzeylab/pusula-sdk',
      AppId: 101,
      AppName: 'Pusula',
      LatestVersion: '2.4.0',
      LatestPublishedDate: '2026-11-20T10:00:00Z',
      LatestCiRunUrl: 'https://github.com/kuzeylab/pusula-sdk/actions/runs/4242',
      LatestWorkflow: '.github/workflows/release.yml',
      SourceRepository: 'https://github.com/kuzeylab/pusula-sdk',
      HasProvenance: true,
      ProvenanceSupported: true,
      DeprecationMessage: null,
      OpenAlertCount: 1,
      SyncedDate: '2026-11-22T10:00:00Z',
      SyncError: null,
      RegistryUrl: 'https://www.npmjs.com/package/@kuzeylab/pusula-sdk',
    },
    overrides,
  )
}

export function trackedPackage(overrides: Partial<TrackedPackageDto> = {}): TrackedPackageDto {
  return make<TrackedPackageDto>(
    {
      ...packageSummary(),
      Versions: [
        {
          Version: '2.4.0',
          PublishedDate: '2026-11-20T10:00:00Z',
          IsYanked: false,
          IsUnlisted: false,
          IsDeprecated: false,
          HasProvenance: true,
          CiRunUrl: 'https://github.com/kuzeylab/pusula-sdk/actions/runs/4242',
        },
        {
          Version: '2.3.0',
          PublishedDate: '2026-10-01T10:00:00Z',
          IsYanked: false,
          IsUnlisted: false,
          IsDeprecated: true,
          Message: 'Use 2.4',
          HasProvenance: false,
        },
      ],
      Alerts: [packageAlert()],
    },
    overrides,
  )
}

export interface RegistriesState {
  packages: TrackedPackageSummaryDto[]
  package: TrackedPackageDto
}

function initialRegistries(): RegistriesState {
  return {
    packages: [
      packageSummary(),
      packageSummary({
        Id: NUGET_PACKAGE_ID,
        Registry: PackageRegistry.NuGet,
        Name: 'KuzeyLab.Pusula.Client',
        AppId: null,
        AppName: null,
        LatestVersion: '1.8.0',
        LatestCiRunUrl: null,
        LatestWorkflow: null,
        HasProvenance: false,
        ProvenanceSupported: false,
        OpenAlertCount: 0,
        RegistryUrl: 'https://www.nuget.org/packages/KuzeyLab.Pusula.Client',
      }),
    ],
    package: trackedPackage(),
  }
}

export const registriesState: RegistriesState = initialRegistries()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

export const registriesHandlers: HttpHandler[] = [
  endpoint('get', '/registries/packages', () => json(envelope(registriesState.packages))),
  endpoint('get', '/registries/package', () => json(envelope(registriesState.package))),
  endpoint('get', '/registries/alerts', () => json(envelope(registriesState.package.Alerts ?? []))),
  endpoint('post', '/registries/package', () => json(envelope({ Id: 33, Name: 'serde_json', JobId: 52100 }))),
  endpoint('delete', '/registries/package', () => json(envelope(NPM_PACKAGE_ID))),
  endpoint('post', '/registries/sync', () => json(envelope(52101))),
]

/** Resets the registries data and answers its endpoints for every test of the calling file. */
export function setupRegistriesApi(): void {
  beforeEach(() => {
    Object.assign(registriesState, initialRegistries())
    server.use(...registriesHandlers)
  })
}
