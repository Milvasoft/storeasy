import { http, HttpResponse, type HttpHandler } from 'msw'
import {
  PackageAlertKind,
  PublisherCheckStatus,
  PackageRegistry,
  type PackageAlertDto,
  type TrackedPackageDto,
  type TrackedPackageSummaryDto,
} from '@/features/registries/types'

// The dev mock's package registries (RegistriesController): two tracked packages of the sample organization, one with a
// security advisory. In memory; a reload starts over.

const BASE = '*/api/v1.0'

function envelope<T>(data: T) {
  return HttpResponse.json({ isSuccess: true, statusCode: 200, messages: [], data })
}

function refuse(key: string, message: string, status: number) {
  return HttpResponse.json({ isSuccess: false, statusCode: status, messages: [{ key, message, type: 3 }], data: null }, { status })
}

const advisory: PackageAlertDto = {
  Id: 1,
  TrackedPackageId: 1,
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
}

let packages: TrackedPackageDto[] = [
  {
    Id: 1,
    Registry: PackageRegistry.Npm,
    Name: '@kuzeylab/pusula-sdk',
    LatestVersion: '2.4.0',
    LatestPublishedDate: '2026-11-20T10:00:00Z',
    LatestCiRunUrl: 'https://github.com/kuzeylab/pusula-sdk/actions/runs/4242',
    LatestWorkflow: '.github/workflows/release.yml',
    SourceRepository: 'https://github.com/kuzeylab/pusula-sdk',
    HasProvenance: true,
    ProvenanceSupported: true,
    OpenAlertCount: 1,
    PublisherCheck: PublisherCheckStatus.NotConfigured,
    SyncedDate: '2026-11-22T10:00:00Z',
    RegistryUrl: 'https://www.npmjs.com/package/@kuzeylab/pusula-sdk',
    Versions: [
      {
        Version: '2.4.0',
        PublishedDate: '2026-11-20T10:00:00Z',
        IsYanked: false,
        IsUnlisted: false,
        IsDeprecated: false,
        HasProvenance: true,
      },
      {
        Version: '2.3.0',
        PublishedDate: '2026-10-01T10:00:00Z',
        IsYanked: false,
        IsUnlisted: false,
        IsDeprecated: false,
        HasProvenance: true,
      },
    ],
    Alerts: [advisory],
  },
  {
    Id: 2,
    Registry: PackageRegistry.NuGet,
    Name: 'KuzeyLab.Pusula.Client',
    LatestVersion: '1.8.0',
    LatestPublishedDate: '2026-11-02T09:00:00Z',
    HasProvenance: false,
    ProvenanceSupported: false,
    OpenAlertCount: 0,
    PublisherCheck: PublisherCheckStatus.NotConfigured,
    SyncedDate: '2026-11-22T10:00:00Z',
    RegistryUrl: 'https://www.nuget.org/packages/KuzeyLab.Pusula.Client',
    Versions: [
      {
        Version: '1.8.0',
        PublishedDate: '2026-11-02T09:00:00Z',
        IsYanked: false,
        IsUnlisted: false,
        IsDeprecated: false,
        HasProvenance: false,
      },
    ],
    Alerts: [],
  },
]

function summary(item: TrackedPackageDto): TrackedPackageSummaryDto {
  return { ...item, Versions: undefined, Alerts: undefined } as TrackedPackageSummaryDto
}

export const registriesMockHandlers: HttpHandler[] = [
  http.get(`${BASE}/registries/packages`, () => envelope(packages.map(summary))),
  http.get(`${BASE}/registries/package`, ({ request }) => {
    const id = Number(new URL(request.url).searchParams.get('TrackedPackageId'))
    const item = packages.find((candidate) => candidate.Id === id)
    return item ? envelope(item) : refuse('TrackedPackageNotFound', 'The tracked package was not found.', 404)
  }),
  http.get(`${BASE}/registries/alerts`, () => envelope(packages.flatMap((item) => item.Alerts ?? []))),
  http.post(`${BASE}/registries/package`, async ({ request }) => {
    const body = (await request.json()) as { Registry: PackageRegistry; Name: string }
    const id = Math.max(...packages.map((item) => item.Id)) + 1
    packages = [
      ...packages,
      {
        Id: id,
        Registry: body.Registry,
        Name: body.Name,
        HasProvenance: false,
        ProvenanceSupported: body.Registry !== PackageRegistry.NuGet,
        OpenAlertCount: 0,
        PublisherCheck: PublisherCheckStatus.NotConfigured,
        Versions: [],
        Alerts: [],
      },
    ]
    return envelope({ Id: id, Name: body.Name, JobId: 99_500 + id })
  }),
  http.delete(`${BASE}/registries/package`, async ({ request }) => {
    const body = (await request.json()) as { TrackedPackageId: number }
    packages = packages.filter((item) => item.Id !== body.TrackedPackageId)
    return envelope(body.TrackedPackageId)
  }),
  http.post(`${BASE}/registries/sync`, () => envelope(99_499)),
  http.get(`${BASE}/registries/setup`, ({ request }) => {
    const id = Number(new URL(request.url).searchParams.get('TrackedPackageId'))
    const item = packages.find((candidate) => candidate.Id === id)
    return envelope({
      TrackedPackageId: id,
      Registry: item?.Registry ?? PackageRegistry.Npm,
      PackageName: item?.Name ?? '',
      Providers: item?.Registry === PackageRegistry.NuGet ? [1] : [1, 2],
      Saved: false,
      Check: { Status: 1 },
    })
  }),
  http.put(`${BASE}/registries/setup`, () => refuse('FeatureDisabled', 'Not available in the demo.', 503)),
  http.get(`${BASE}/registries/reminders`, () => envelope([])),
  http.post(`${BASE}/registries/reminder`, () => refuse('FeatureDisabled', 'Not available in the demo.', 503)),
  http.delete(`${BASE}/registries/reminder`, () => envelope(0)),
]
