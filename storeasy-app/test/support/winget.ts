import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { ChangePlanKind } from '@/features/common/types'
import {
  WingetChangeKind,
  WingetPullRequestState,
  type WingetManifestDto,
  type WingetPackageDto,
  type WingetPullRequestDto,
} from '@/features/winget/types'
import { changePlan, contentHandlers, contentState } from './content'
import { appDetail, make, storeRecord } from './factories'
import { endpoint, envelope } from './handlers'
import { server } from './server'
import { testState } from './state'

// The winget fake backend (WingetController): DTO builders typed with the screens' own contract, the data the handlers
// answer with, and one handler per endpoint of src/features/winget/api.ts (wingetContract.test.ts checks the pairing).
// setupWingetApi() also answers the shared plan endpoints, through which winget pull requests are approved.

export const WINGET_STORE_APP_ID = 204

export function wingetPull(overrides: Partial<WingetPullRequestDto> = {}): WingetPullRequestDto {
  return make<WingetPullRequestDto>(
    {
      Id: 1,
      StoreAppId: WINGET_STORE_APP_ID,
      Number: 300512,
      Url: 'https://github.com/microsoft/winget-pkgs/pull/300512',
      Title: 'New version: KuzeyLab.Pusula version 2.4.0',
      PackageVersion: '2.4.0',
      ChangeKind: WingetChangeKind.NewVersion,
      State: WingetPullRequestState.NeedsAuthorFeedback,
      IsOpen: true,
      NeedsAction: true,
      Labels: ['Needs-Author-Feedback', 'Validation-Completed'],
      Checks: [{ Name: 'Validation', Status: 'completed', Conclusion: 'success' }],
      Comments: [{ Author: 'stephengillie', IsBot: false, Excerpt: 'Please add the silent switch.', Date: '2026-11-20T10:00:00Z' }],
      StatusHistory: [
        { State: WingetPullRequestState.Validating, Date: '2026-11-19T10:00:00Z' },
        { State: WingetPullRequestState.NeedsAuthorFeedback, Date: '2026-11-20T10:00:00Z' },
      ],
      CreatedByStoreasy: true,
      PlanId: 7201,
      OpenedDate: '2026-11-19T10:00:00Z',
    },
    overrides,
  )
}

export function wingetPackage(overrides: Partial<WingetPackageDto> = {}): WingetPackageDto {
  return make<WingetPackageDto>(
    {
      StoreAppId: WINGET_STORE_APP_ID,
      AppId: 101,
      AppName: 'Pusula',
      ConnectionId: 14,
      Fork: 'kuzeylab/winget-pkgs',
      PackageIdentifier: 'KuzeyLab.Pusula',
      IsPublished: true,
      LatestVersion: '2.3.0',
      Versions: ['2.3.0', '2.2.1'],
      Publisher: 'Kuzey Lab',
      PackageName: 'Pusula',
      DefaultLocale: 'en-US',
      Locales: ['en-US', 'tr-TR'],
      ManifestVersion: '1.10.0',
      WrittenManifestVersion: '1.12.0',
      SyncedDate: '2026-11-20T11:00:00Z',
      SyncError: null,
      PackagePageUrl: 'https://github.com/microsoft/winget-pkgs/tree/master/manifests/k/KuzeyLab/Pusula',
      PullRequests: [wingetPull()],
      InstallerSources: [
        {
          Url: 'https://kuzeylab.dev/pusula/2.4.0/PusulaSetup.msi',
          Type: 'msi',
          Architectures: ['x64', 'arm64'],
          Locales: ['en-US'],
          SignatureStatus: 2,
        },
      ],
    },
    overrides,
  )
}

export function wingetManifest(overrides: Partial<WingetManifestDto> = {}): WingetManifestDto {
  return make<WingetManifestDto>(
    {
      StoreAppId: WINGET_STORE_APP_ID,
      PackageIdentifier: 'KuzeyLab.Pusula',
      Version: '2.3.0',
      Versions: ['2.3.0', '2.2.1'],
      DefaultLocale: 'en-US',
      ManifestVersion: '1.10.0',
      Locales: [
        {
          Locale: 'en-US',
          IsDefault: true,
          Fields: { PackageName: 'Pusula', Publisher: 'Kuzey Lab', ShortDescription: 'Offline maps.', License: 'Proprietary' },
          Tags: ['maps'],
        },
        { Locale: 'tr-TR', IsDefault: false, Fields: { ShortDescription: 'Çevrimdışı haritalar.' }, Tags: [] },
      ],
      Installers: [{ Architecture: 'x64', Type: 'msi', Url: 'https://kuzeylab.dev/pusula/2.3.0/PusulaSetup.msi', Sha256: 'A'.repeat(64) }],
      Files: [{ FileName: 'KuzeyLab.Pusula.yaml', Content: 'PackageIdentifier: KuzeyLab.Pusula\n' }],
      Issues: [{ Key: 'WingetManifestVersionOld', Message: 'The manifests use schema 1.10.0; storeasy writes 1.12.0.', Blocking: false }],
    },
    overrides,
  )
}

export interface WingetState {
  package: WingetPackageDto
  manifest: WingetManifestDto
}

function initialWinget(): WingetState {
  return { package: wingetPackage(), manifest: wingetManifest() }
}

export const wingetState: WingetState = initialWinget()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

export const wingetHandlers: HttpHandler[] = [
  endpoint('post', '/winget/package', () => json(envelope({ AppId: 101, StoreAppId: WINGET_STORE_APP_ID, JobId: 48900 }))),
  endpoint('get', '/winget/package', () => json(envelope(wingetState.package))),
  endpoint('get', '/winget/manifest', () => json(envelope(wingetState.manifest))),
  endpoint('get', '/winget/pull-requests', () => json(envelope(wingetState.package.PullRequests ?? []))),
  endpoint('post', '/winget/change', () => json(envelope(contentState.plan))),
  endpoint('post', '/winget/sync', () => json(envelope(48901))),
]

/** The app of the tests with a winget record next to its Apple and Google ones. */
export function wingetAppDetail() {
  const base = appDetail()
  return appDetail({
    Stores: [
      ...(base.Stores ?? []),
      storeRecord({
        StoreAppId: WINGET_STORE_APP_ID,
        Store: StoreKind.Winget,
        ExternalId: 'KuzeyLab.Pusula',
        BundleId: 'KuzeyLab.Pusula',
        StoreConnectionId: 14,
      }),
    ],
  })
}

/** Resets the winget data and answers its endpoints (and the shared plan endpoints) for every test of the calling file. */
export function setupWingetApi(): void {
  beforeEach(() => {
    Object.assign(wingetState, initialWinget())
    testState.appDetail = wingetAppDetail()
    contentState.plan = changePlan({
      PlanId: 7201,
      Kind: ChangePlanKind.WingetChange,
      Title: 'winget: New version: KuzeyLab.Pusula version 2.4.0',
      Store: StoreKind.Winget,
      StoreAppId: WINGET_STORE_APP_ID,
      ConfirmRequired: true,
      ConfirmText: 'KuzeyLab.Pusula 2.4.0',
      Items: [],
      Warnings: [],
      Calls: [],
    })
    server.use(...contentHandlers, ...wingetHandlers)
  })
}
