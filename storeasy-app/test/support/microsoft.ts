import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { ConnectionStatus, OrgPermission, StoreKind, type StoreConnectionDto } from '@/lib/api/types'
import { ChangePlanKind } from '@/features/common/types'
import {
  MsAddonType,
  MsChangeKind,
  MsInstallerType,
  MsProductKind,
  MsRolloutStatus,
  MsSignatureStatus,
  MsSubmissionStatus,
  MsSubmissionTarget,
  type MsAddonDto,
  type MsChangePlanDto,
  type MsContentDto,
  type MsExpiringConnectionDto,
  type MsFlightDto,
  type MsInstallerPackageDto,
  type MsProductDto,
  type MsSubmissionDto,
} from '@/features/microsoft/types'
import { changePlan, contentHandlers, contentState } from './content'
import { appDetail, connection, make, storeRecord } from './factories'
import { endpoint, envelope } from './handlers'
import { server } from './server'
import { testState } from './state'

// The Microsoft Store fake backend (MsController): DTO builders typed with the screens' own contract, the data the
// handlers answer with, and one handler per endpoint of src/features/microsoft/api.ts (msContract.test.ts checks the
// pairing). setupMsApi() also answers the shared plan endpoints, through which Microsoft changes are approved.

export const MS_STORE_APP_ID = 203

// ---- Builders ----------------------------------------------------------------------------------------------------------
export function msSubmission(overrides: Partial<MsSubmissionDto> = {}): MsSubmissionDto {
  return make<MsSubmissionDto>(
    {
      Id: 900,
      StoreAppId: MS_STORE_APP_ID,
      Target: MsSubmissionTarget.App,
      SubmissionId: '1152921505698',
      FlightId: null,
      AddonStoreId: null,
      FriendlyName: '2.3.0.0',
      PackageVersion: '2.3.0.0',
      Status: MsSubmissionStatus.Certification,
      StatusHistory: [
        { Status: MsSubmissionStatus.PendingCommit, Date: '2026-11-17T12:02:00Z' },
        { Status: MsSubmissionStatus.CommitStarted, Date: '2026-11-17T12:04:00Z' },
        { Status: MsSubmissionStatus.Certification, Date: '2026-11-17T14:10:00Z' },
      ],
      StatusDetails: { Errors: [], Warnings: [], CertificationReports: [] },
      IsPending: true,
      CreatedByStoreasy: true,
      ModifiedOutsideApi: false,
      RolloutPercentage: null,
      RolloutStatus: null,
      PublishMode: null,
      PublishDate: null,
      AwaitsManualPublish: false,
      SyncedDate: '2026-11-18T09:00:00Z',
    },
    overrides,
  )
}

export function msProduct(overrides: Partial<MsProductDto> = {}): MsProductDto {
  return make<MsProductDto>(
    {
      StoreAppId: MS_STORE_APP_ID,
      AppId: 101,
      Name: 'Pusula',
      ProductId: '9P4K7Q2MZL1D',
      PackageFamilyName: 'KuzeyLab.Pusula_8wekyb3d8bbwe',
      Kind: MsProductKind.Msix,
      PricingV2: false,
      FirstSubmissionDone: true,
      HasMandatoryUpdate: false,
      IsReady: false,
      LastPublishedSubmissionId: '1152921505403',
      PendingSubmissionId: null,
      SyncedDate: '2026-11-18T09:00:00Z',
      ActiveSubmission: null,
      Notices: [
        'Microsoft allows one active submission at a time.',
        'If a submission created here is changed in Partner Center, the API cannot change it anymore. Make the changes here.',
      ],
      AssistedSteps: [
        {
          Key: 'ms.app.age-rating',
          Title: 'IARC age rating',
          Reason: null,
          Url: 'https://partner.microsoft.com/dashboard/products/9P4K7Q2MZL1D/agerating',
          Pending: true,
          StepId: 5601,
          Status: 1,
        },
      ],
    },
    overrides,
  )
}

export function msContent(overrides: Partial<MsContentDto> = {}): MsContentDto {
  return make<MsContentDto>(
    {
      Kind: MsProductKind.Msix,
      Target: MsSubmissionTarget.App,
      ProductId: '9P4K7Q2MZL1D',
      SubmissionId: '1152921505403',
      SubmissionIsPending: false,
      SubmissionStatus: MsSubmissionStatus.Published,
      Listings: [
        {
          Locale: 'tr-tr',
          Title: 'Pusula',
          Description: 'Pusula gününü tek pencerede toplar.',
          ShortDescription: 'Gününü planla.',
          WhatsNew: 'Haftalık görünüm geldi.',
          Features: ['Günlük takvim', 'Hedefler'],
          SearchTerms: ['takvim', 'plan'],
        },
        {
          Locale: 'en-us',
          Title: 'Pusula',
          Description: 'Plan your day.',
          ShortDescription: null,
          WhatsNew: null,
          Features: [],
          SearchTerms: [],
        },
      ],
      Images: [
        { Locale: 'tr-tr', Id: 'img-1', FileName: 'tr-1.png', ImageType: 'Screenshot', Description: 'Takvim' },
        { Locale: 'tr-tr', Id: 'img-2', FileName: 'tr-logo.png', ImageType: 'StoreLogoSquare', Description: null },
      ],
      Packages: [{ Id: 'pkg-1', FileName: 'Pusula_2.2.0.0_x64.msix', Version: '2.2.0.0', Architecture: 'x64' }],
      Pricing: { PriceId: 'Free', TrialPeriod: 'NoFreeTrial', Visibility: 'Public' },
      Rollout: { IsPackageRollout: true, Percentage: 25, Status: MsRolloutStatus.InProgress, FallbackSubmissionId: '1152921505300' },
      InstallerPackages: [],
      AddonListings: [],
      AddonLifetime: null,
      PricingV2: false,
      MandatoryUpdate: false,
      ContentHash: 'hash-1',
    },
    overrides,
  )
}

export function msFlight(overrides: Partial<MsFlightDto> = {}): MsFlightDto {
  return make<MsFlightDto>(
    {
      StoreAppId: MS_STORE_APP_ID,
      FlightId: 'flight-1',
      FriendlyName: 'Kuzey içi',
      Rank: 1,
      GroupIds: ['g-1'],
      Groups: [{ Id: 'g-1', Name: 'Kuzey Lab çalışanları', MemberCount: 12 }],
      LastPublishedSubmissionId: '1152921505744',
      PendingSubmissionId: null,
      SyncedDate: '2026-11-18T09:00:00Z',
    },
    overrides,
  )
}

export function msAddon(overrides: Partial<MsAddonDto> = {}): MsAddonDto {
  return make<MsAddonDto>(
    {
      StoreAppId: MS_STORE_APP_ID,
      StoreId: '9NBLGGH4R315',
      ProductId: 'pusula.pro',
      Title: 'Pusula Pro',
      Type: MsAddonType.Durable,
      Lifetime: 'Forever',
      PriceId: 'Tier5',
      LastPublishedSubmissionId: '1152921505800',
      PendingSubmissionId: null,
      ApiWritable: true,
      AssistedReason: null,
      SyncedDate: '2026-11-18T09:00:00Z',
    },
    overrides,
  )
}

export function msInstaller(overrides: Partial<MsInstallerPackageDto> = {}): MsInstallerPackageDto {
  return make<MsInstallerPackageDto>(
    {
      Id: 1,
      StoreAppId: MS_STORE_APP_ID,
      PackageId: 'p-1',
      PackageUrl: 'https://indir.kuzeylab.dev/tarla/0.9.4/TarlaSetup.exe',
      PackageType: MsInstallerType.Exe,
      Architectures: ['X64'],
      Languages: ['tr-tr', 'en-us'],
      InstallerParameters: '/S',
      IsSilentInstall: false,
      GenericDocUrl: 'https://kuzeylab.dev/tarla/codes',
      SignatureStatus: MsSignatureStatus.Signed,
      SignatureNote: 'CN=Kuzey Lab',
      CheckedDate: '2026-11-18T10:05:00Z',
    },
    overrides,
  )
}

export function msChangePlan(overrides: Partial<MsChangePlanDto> = {}): MsChangePlanDto {
  return make<MsChangePlanDto>(
    {
      StoreAppId: MS_STORE_APP_ID,
      Kind: MsChangeKind.Listings,
      Target: MsSubmissionTarget.App,
      SubmissionId: '1152921505403',
      CreatesSubmission: true,
      Items: [],
      Warnings: [],
      Calls: [],
      BaseHash: 'hash-1',
      RequiredPermission: OrgPermission.EditContent,
      Blocked: false,
      BlockKey: null,
      AssistedReason: null,
      DryRun: true,
      JobId: null,
      PlanId: 7101,
    },
    overrides,
  )
}

export function msConnection(overrides: Partial<StoreConnectionDto> = {}): StoreConnectionDto {
  return connection({
    Id: 13,
    Store: StoreKind.Microsoft,
    Name: 'Kuzey Lab Partner Center',
    Fingerprint: { TenantIdSuffix: '3c07', ClientIdSuffix: 'be12', SellerId: '40291187' },
    Status: ConnectionStatus.Verified,
    CredentialExpiryDate: daysFromNow(200),
    ...overrides,
  })
}

/** An instant the given number of days from now (whole days plus a margin, so the day count is stable). */
export function daysFromNow(days: number): string {
  return new Date(Date.now() + days * 86_400_000 + 3_600_000).toISOString()
}

// ---- State -------------------------------------------------------------------------------------------------------------
export interface MsState {
  product: MsProductDto
  content: MsContentDto
  submissions: MsSubmissionDto[]
  flights: MsFlightDto[]
  addons: MsAddonDto[]
  packages: MsInstallerPackageDto[]
  change: MsChangePlanDto
  expiring: MsExpiringConnectionDto[]
}

function initialMs(): MsState {
  return {
    product: msProduct(),
    content: msContent(),
    submissions: [msSubmission()],
    flights: [msFlight()],
    addons: [msAddon()],
    packages: [msInstaller()],
    change: msChangePlan(),
    expiring: [],
  }
}

export const msState: MsState = initialMs()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

// ---- Handlers ----------------------------------------------------------------------------------------------------------
export const msHandlers: HttpHandler[] = [
  endpoint('post', '/ms/product', () => json(envelope({ AppId: 104, StoreAppId: 240, JobId: 48600 }))),
  endpoint('get', '/ms/product', () => json(envelope(msState.product))),
  endpoint('get', '/ms/submissions', () => json(envelope(msState.submissions))),
  endpoint('get', '/ms/flights', () => json(envelope(msState.flights))),
  endpoint('get', '/ms/addons', () => json(envelope(msState.addons))),
  endpoint('get', '/ms/packages', () => json(envelope(msState.packages))),
  endpoint('get', '/ms/content', () => json(envelope(msState.content))),
  endpoint('post', '/ms/change', ({ body }) => {
    const change = (body as { Change: { Kind: MsChangeKind } }).Change
    return json(envelope({ ...msState.change, Kind: change.Kind }))
  }),
  endpoint('post', '/ms/submission/submit', () => json(envelope(48700))),
  endpoint('post', '/ms/packages/verify', () => json(envelope(48800))),
  endpoint('put', '/ms/flight-group', () => json(envelope(3))),
  endpoint('put', '/ms/connection/secret', ({ body }) => {
    const request = body as { ConnectionId: number; CredentialExpiryDate: string }
    const known = testState.connections.find((item) => item.Id === request.ConnectionId) ?? msConnection()
    return json(envelope({ ...known, CredentialExpiryDate: request.CredentialExpiryDate, Status: ConnectionStatus.Verified }))
  }),
  endpoint('get', '/ms/connections/expiring', () => json(envelope(msState.expiring))),
]

/** The app of the tests with a Microsoft Store record next to its Apple and Google ones. */
export function msAppDetail() {
  const base = appDetail()
  return appDetail({
    Stores: [
      ...(base.Stores ?? []),
      storeRecord({
        StoreAppId: MS_STORE_APP_ID,
        Store: StoreKind.Microsoft,
        ExternalId: '9P4K7Q2MZL1D',
        BundleId: null,
        StoreConnectionId: 13,
      }),
    ],
  })
}

/** Resets the Microsoft data and answers its endpoints (and the shared plan endpoints) for every test of the calling file. */
export function setupMsApi(): void {
  beforeEach(() => {
    Object.assign(msState, initialMs())
    testState.appDetail = msAppDetail()
    contentState.plan = changePlan({
      PlanId: 7101,
      Kind: ChangePlanKind.MsChange,
      Title: 'Microsoft Store Listings of 9P4K7Q2MZL1D',
      Store: StoreKind.Microsoft,
      StoreAppId: MS_STORE_APP_ID,
      VersionId: '1152921505403',
      VersionString: '9P4K7Q2MZL1D',
      Items: [{ Group: 'listing', Locale: 'tr-tr', Field: 'description', Before: 'Eski metin', After: 'Yeni metin', Action: 2 }],
      Warnings: [],
      Calls: [{ Method: 'POST', Path: 'applications/9P4K7Q2MZL1D/submissions', Target: 'submission' }],
    })
    server.use(...contentHandlers, ...msHandlers)
  })
}
