import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { ChangePlanStatus } from '@/features/common/types'
import { ConnectionStatus } from '@/lib/api/types'
import {
  CiProvider,
  RegistryConnectionKind,
  RegistryPlanKind,
  ReleaseTrigger,
  VersionFileFormat,
  type RegistryConnectionDto,
  type RegistryPlanDto,
  type ReleaseSettingsDto,
  type ReleaseSuggestionDto,
  PackageAlertKind,
  PackageRegistry,
  PublisherCheckStatus,
  DownloadScope,
  ListingCheckStatus,
  ReadmeFormat,
  type PackageDownloadsDto,
  type PackageGroupDto,
  type PackageGroupSuggestionDto,
  type PackageListingDto,
  type TokenReminderDto,
  type TrustedPublishingDto,
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
      SetupProvider: CiProvider.GitHubActions,
      SetupRepository: 'kuzeylab/pusula-sdk',
      SetupWorkflow: 'release.yml',
      PublisherCheck: PublisherCheckStatus.Match,
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

export function trustedPublishing(overrides: Partial<TrustedPublishingDto> = {}): TrustedPublishingDto {
  return make<TrustedPublishingDto>(
    {
      TrackedPackageId: NPM_PACKAGE_ID,
      Registry: PackageRegistry.Npm,
      PackageName: '@kuzeylab/pusula-sdk',
      Providers: [CiProvider.GitHubActions, CiProvider.GitLabCi],
      SettingsUrl: 'https://www.npmjs.com/package/@kuzeylab/pusula-sdk/access',
      Saved: true,
      Provider: CiProvider.GitHubActions,
      Repository: 'kuzeylab/pusula-sdk',
      Workflow: 'release.yml',
      Environment: 'npm',
      RegistryFields: [
        { Key: 'owner', Value: 'kuzeylab' },
        { Key: 'repository', Value: 'pusula-sdk' },
        { Key: 'workflow', Value: 'release.yml' },
        { Key: 'environment', Value: 'npm' },
      ],
      WorkflowPath: '.github/workflows/release.yml',
      WorkflowText: 'name: Publish to npm\npermissions:\n  id-token: write\n',
      Check: {
        Status: PublisherCheckStatus.Mismatch,
        ExpectedRepository: 'https://github.com/kuzeylab/pusula-sdk',
        ExpectedWorkflow: '.github/workflows/release.yml',
        ObservedRepository: 'https://github.com/kuzeylab/pusula-sdk',
        ObservedWorkflow: '.github/workflows/manual.yml',
        Version: '2.4.0',
      },
    },
    overrides,
  )
}

export function tokenReminder(overrides: Partial<TokenReminderDto> = {}): TokenReminderDto {
  return make<TokenReminderDto>(
    {
      Id: 71,
      Registry: PackageRegistry.NuGet,
      Label: 'NuGet push key',
      UsedIn: 'NUGET_API_KEY in kuzeylab/pusula',
      ExpiryDate: '2026-11-25T00:00:00Z',
      DaysLeft: 2,
      Stage: 2,
      TrustedPublishingAvailable: true,
    },
    overrides,
  )
}

export function packageDownloads(overrides: Partial<PackageDownloadsDto> = {}): PackageDownloadsDto {
  return make<PackageDownloadsDto>(
    {
      TrackedPackageId: NPM_PACKAGE_ID,
      Registry: PackageRegistry.Npm,
      Available: true,
      RecentDownloads: 1200,
      RecentDownloadsDays: 30,
      VersionScope: DownloadScope.LastWeek,
      DownloadsSyncedDate: '2026-11-22T10:00:00Z',
      Series: [
        { Day: '2026-11-20T00:00:00Z', Downloads: 40 },
        { Day: '2026-11-21T00:00:00Z', Downloads: 60 },
      ],
      Versions: [{ Version: '2.4.0', Downloads: 90 }],
    },
    overrides,
  )
}

export function packageListing(overrides: Partial<PackageListingDto> = {}): PackageListingDto {
  return make<PackageListingDto>(
    {
      TrackedPackageId: NPM_PACKAGE_ID,
      Registry: PackageRegistry.Npm,
      Available: true,
      Version: '2.4.0',
      Description: 'Pusula SDK',
      Readme: '# Pusula SDK<script>alert(1)</script>',
      ReadmeFormat: ReadmeFormat.Markdown,
      Keywords: ['maps'],
      License: 'MIT',
      Repository: 'https://github.com/kuzeylab/pusula-sdk',
      Authors: ['Kuzey Lab'],
      Checks: [
        { Key: 'description', Status: ListingCheckStatus.Warning },
        { Key: 'readme', Status: ListingCheckStatus.Ok },
      ],
      ShipsWithRelease: true,
    },
    overrides,
  )
}

export function packageGroup(overrides: Partial<PackageGroupDto> = {}): PackageGroupDto {
  return make<PackageGroupDto>(
    {
      Id: 5,
      Name: 'Pusula SDKs',
      HighestVersion: '2.4.0',
      BehindCount: 1,
      OpenAlertCount: 1,
      Members: [
        {
          TrackedPackageId: NUGET_PACKAGE_ID,
          Registry: PackageRegistry.NuGet,
          Name: 'KuzeyLab.Pusula.Client',
          LatestVersion: '1.8.0',
          Behind: true,
          OpenAlertCount: 0,
        },
        {
          TrackedPackageId: NPM_PACKAGE_ID,
          Registry: PackageRegistry.Npm,
          Name: '@kuzeylab/pusula-sdk',
          LatestVersion: '2.4.0',
          Behind: false,
          OpenAlertCount: 1,
        },
      ],
    },
    overrides,
  )
}

export function registryConnection(overrides: Partial<RegistryConnectionDto> = {}): RegistryConnectionDto {
  return make<RegistryConnectionDto>(
    {
      Id: 81,
      Kind: RegistryConnectionKind.GitHub,
      Fingerprint: 'kuzeylab-bot',
      Status: ConnectionStatus.Verified,
      LastVerifiedDate: '2026-11-22T09:00:00Z',
      LastVerifyError: null,
      ExpiryDate: null,
    },
    overrides,
  )
}

export function releaseSettings(overrides: Partial<ReleaseSettingsDto> = {}): ReleaseSettingsDto {
  return make<ReleaseSettingsDto>(
    {
      TrackedPackageId: NPM_PACKAGE_ID,
      PackageGroupId: null,
      PackageGroupName: null,
      FromGroup: false,
      Settings: {
        Repository: 'kuzeylab/pusula-sdk',
        Branch: 'master',
        Trigger: ReleaseTrigger.VersionBump,
        VersionFiles: [{ Path: 'package.json', Format: VersionFileFormat.PackageJson }],
        TagPattern: null,
        Workflow: 'release.yml',
        DispatchVersionInput: null,
        DispatchPrereleaseInput: null,
        DispatchTakesTag: false,
      },
      Problems: [],
      CurrentVersion: '2.4.0',
      NextVersions: [
        { Kind: 'patch', Version: '2.4.1' },
        { Kind: 'minor', Version: '2.5.0' },
        { Kind: 'major', Version: '3.0.0' },
      ],
      GitHubConnected: true,
    },
    overrides,
  )
}

export function registryPlan(overrides: Partial<RegistryPlanDto> = {}): RegistryPlanDto {
  return make<RegistryPlanDto>(
    {
      Id: 610,
      TrackedPackageId: NPM_PACKAGE_ID,
      PackageGroupId: null,
      Kind: RegistryPlanKind.Release,
      Status: ChangePlanStatus.Pending,
      Title: 'Release @kuzeylab/pusula-sdk 2.4.1',
      Version: '2.4.1',
      CurrentVersion: '2.4.0',
      Tag: null,
      ConfirmText: '@kuzeylab/pusula-sdk 2.4.1',
      RequiredPermission: 4,
      Release: releaseSettings().Settings,
      Files: [
        {
          Path: 'package.json',
          BaseSha: 'abc123',
          OldVersion: '2.4.0',
          Before: '  "version": "2.4.0",',
          After: '  "version": "2.4.1",',
          Line: 3,
        },
      ],
      PackageIds: [NPM_PACKAGE_ID],
      DispatchInputs: {},
      BaseSha: 'f00dbabe',
      Argument: null,
      ExpiryDate: '2026-11-25T10:00:00Z',
      CreatedByUserName: 'deniz',
      CreationDate: '2026-11-23T10:00:00Z',
      DecidedByUserName: null,
      DecisionDate: null,
      StoreJobId: null,
      CommitSha: null,
      RunId: null,
      RunUrl: null,
      RunStatus: null,
      RunConclusion: null,
      FollowStatus: null,
      StartedDate: null,
      FinishedDate: null,
      Error: null,
    },
    overrides,
  )
}

export interface RegistriesState {
  packages: TrackedPackageSummaryDto[]
  package: TrackedPackageDto
  setup: TrustedPublishingDto
  reminders: TokenReminderDto[]
  downloads: PackageDownloadsDto
  listing: PackageListingDto
  groups: PackageGroupDto[]
  suggestions: PackageGroupSuggestionDto[]
  connections: RegistryConnectionDto[]
  release: ReleaseSettingsDto
  detected: ReleaseSuggestionDto[]
  plans: RegistryPlanDto[]
  plan: RegistryPlanDto
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
    setup: trustedPublishing(),
    reminders: [tokenReminder()],
    downloads: packageDownloads(),
    listing: packageListing(),
    groups: [packageGroup()],
    suggestions: [
      { Name: 'kuzeylab/tools', Repository: 'https://github.com/kuzeylab/tools', TrackedPackageIds: [41, 42], PackageNames: ['a', 'b'] },
    ],
    connections: [registryConnection()],
    release: releaseSettings(),
    detected: [
      {
        Settings: {
          ...releaseSettings().Settings!,
          Trigger: ReleaseTrigger.Tag,
          VersionFiles: [],
          TagPattern: 'sdk-node-v{version}',
          Workflow: 'sdk-publish.yml',
        },
        Reason: 'sdk-publish.yml runs on tags sdk-node-v*.',
      },
    ],
    plans: [],
    plan: registryPlan(),
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
  endpoint('get', '/registries/setup', () => json(envelope(registriesState.setup))),
  endpoint('put', '/registries/setup', () => json(envelope(registriesState.setup))),
  endpoint('get', '/registries/reminders', () => json(envelope(registriesState.reminders))),
  endpoint('post', '/registries/reminder', () => json(envelope(72))),
  endpoint('delete', '/registries/reminder', () => json(envelope(71))),
  endpoint('get', '/registries/downloads', () => json(envelope(registriesState.downloads))),
  endpoint('get', '/registries/listing', () => json(envelope(registriesState.listing))),
  endpoint('get', '/registries/groups', () => json(envelope(registriesState.groups))),
  endpoint('get', '/registries/groups/suggestions', () => json(envelope(registriesState.suggestions))),
  endpoint('post', '/registries/group', () => json(envelope(6))),
  endpoint('delete', '/registries/group', () => json(envelope(5))),
  endpoint('get', '/registries/connections', () => json(envelope(registriesState.connections))),
  endpoint('post', '/registries/connection', () => json(envelope(82))),
  endpoint('delete', '/registries/connection', () => json(envelope(81))),
  endpoint('get', '/registries/release', () => json(envelope(registriesState.release))),
  endpoint('put', '/registries/release', () => json(envelope(NPM_PACKAGE_ID))),
  endpoint('get', '/registries/release/detect', () => json(envelope(registriesState.detected))),
  endpoint('post', '/registries/release/plan', () => json(envelope(registriesState.plan))),
  endpoint('post', '/registries/operation/plan', () => json(envelope(registriesState.plan))),
  endpoint('get', '/registries/plans', () => json(envelope(registriesState.plans))),
  endpoint('get', '/registries/plan', () => json(envelope(registriesState.plan))),
  endpoint('post', '/registries/plan/approve', () =>
    json(envelope({ ...registriesState.plan, Status: ChangePlanStatus.Approved, StoreJobId: 52200 })),
  ),
  endpoint('post', '/registries/plan/reject', () => json(envelope({ ...registriesState.plan, Status: ChangePlanStatus.Rejected }))),
]

/** Resets the registries data and answers its endpoints for every test of the calling file. */
export function setupRegistriesApi(): void {
  beforeEach(() => {
    Object.assign(registriesState, initialRegistries())
    server.use(...registriesHandlers)
  })
}
