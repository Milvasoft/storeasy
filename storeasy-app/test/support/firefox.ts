import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { ChangePlanKind } from '@/features/common/types'
import { ArtifactSessionStatus } from '@/features/intake/types'
import { FirefoxAddonStatus, FirefoxFileStatus, FirefoxVersionChannel, type FirefoxAddonDto } from '@/features/firefox/types'
import { changePlan, contentHandlers, contentState } from './content'
import { appDetail, make, storeRecord } from './factories'
import { endpoint, envelope } from './handlers'
import { server } from './server'
import { testState } from './state'

// The Firefox Add-ons fake backend (FirefoxController): DTO builders typed with the screens' own contract, the data the
// handlers answer with, and one handler per endpoint of src/features/firefox/api.ts (firefoxContract.test.ts checks the
// pairing). setupFirefoxApi() also answers the shared plan endpoints, through which Firefox plans are approved.

export const FIREFOX_STORE_APP_ID = 206
export const FIREFOX_GUID = 'pusula@kuzeylab.com'

export function firefoxAddon(overrides: Partial<FirefoxAddonDto> = {}): FirefoxAddonDto {
  return make<FirefoxAddonDto>(
    {
      StoreAppId: FIREFOX_STORE_APP_ID,
      AppId: 101,
      AppName: 'Pusula',
      ConnectionId: 16,
      AddonId: 640123,
      Guid: FIREFOX_GUID,
      Slug: 'pusula',
      Status: FirefoxAddonStatus.Public,
      IsDisabled: false,
      DefaultLocale: 'en-US',
      ListedVersion: '2.3.0',
      LatestVersion: '2.3.0',
      LatestChannel: FirefoxVersionChannel.Listed,
      LatestFileStatus: FirefoxFileStatus.Public,
      Rating: 4.46,
      RatingCount: 120,
      AverageDailyUsers: 8400,
      WeeklyDownloads: 950,
      Listing: [
        { Field: 'Name', Locale: 'en-US', Value: 'Pusula' },
        { Field: 'Summary', Locale: 'en-US', Value: 'Find your tabs.' },
        { Field: 'Name', Locale: 'tr', Value: 'Pusula TR' },
      ],
      Categories: ['tabs'],
      Previews: [{ Id: 77, ImageUrl: 'https://cdn.test/77.png', ThumbnailUrl: 'https://cdn.test/77t.png', Position: 0, Caption: null, Width: 1280, Height: 800 }],
      IconUrl: 'https://cdn.test/icon.png',
      Versions: [
        {
          Id: 5001,
          Version: '2.3.0',
          Channel: FirefoxVersionChannel.Listed,
          FileStatus: FirefoxFileStatus.Public,
          IsDisabled: false,
          ReleaseNotes: [{ Locale: 'en-US', Value: 'Faster search.' }],
          License: 'MPL-2.0',
          HasSource: false,
          Created: '2026-11-10T10:00:00Z',
          FileUrl: 'https://addons.test/files/5001.xpi',
          DataCollectionPermissions: ['none'],
        },
      ],
      ReadError: null,
      StatusHistory: [{ Subject: 'version', State: 'Public', Version: '2.3.0', Channel: FirefoxVersionChannel.Listed, Date: '2026-11-12T10:00:00Z' }],
      SyncedDate: '2026-11-20T11:00:00Z',
      SyncError: null,
      StoreUrl: 'https://addons.mozilla.org/firefox/addon/pusula/',
      DeveloperHubUrl: 'https://addons.mozilla.org/developers/addon/pusula/edit',
      Packages: [
        {
          SessionId: 9401,
          FileName: 'pusula-2.4.0.xpi',
          Size: 120000,
          Version: '2.4.0',
          GeckoId: FIREFOX_GUID,
          Name: 'Pusula',
          ManifestVersion: 3,
          Status: ArtifactSessionStatus.Held,
          StartDate: '2026-11-21T09:00:00Z',
          PackageDeleted: false,
          Issues: ['dist/app.js looks minified or bundled; AMO requires the source code and build instructions for such versions.'],
          Blocked: false,
        },
        {
          SessionId: 9400,
          FileName: 'pusula-other.xpi',
          Size: 110000,
          Version: '2.5.0',
          GeckoId: 'other@kuzeylab.com',
          Name: 'Pusula',
          ManifestVersion: 3,
          Status: ArtifactSessionStatus.Rejected,
          StartDate: '2026-11-20T09:00:00Z',
          PackageDeleted: false,
          Issues: [],
          Blocked: true,
        },
      ],
    },
    overrides,
  )
}

export interface FirefoxState {
  addon: FirefoxAddonDto
}

function initialFirefox(): FirefoxState {
  return { addon: firefoxAddon() }
}

export const firefoxState: FirefoxState = initialFirefox()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

export const firefoxHandlers: HttpHandler[] = [
  endpoint('post', '/firefox/addon', () => json(envelope({ AppId: 101, StoreAppId: FIREFOX_STORE_APP_ID, JobId: 49950 }))),
  endpoint('get', '/firefox/addon', () => json(envelope(firefoxState.addon))),
  endpoint('post', '/firefox/change', () => json(envelope(contentState.plan))),
  endpoint('post', '/firefox/sync', () => json(envelope(49951))),
]

/** The app of the tests with a Firefox Add-ons record next to its Apple and Google ones. */
export function firefoxAppDetail() {
  const base = appDetail()
  return appDetail({
    Stores: [
      ...(base.Stores ?? []),
      storeRecord({
        StoreAppId: FIREFOX_STORE_APP_ID,
        Store: StoreKind.Firefox,
        ExternalId: '640123',
        BundleId: FIREFOX_GUID,
        StoreConnectionId: 16,
      }),
    ],
  })
}

/** Resets the Firefox data and answers its endpoints (and the shared plan endpoints) for every test of the calling file. */
export function setupFirefoxApi(): void {
  beforeEach(() => {
    Object.assign(firefoxState, initialFirefox())
    testState.appDetail = firefoxAppDetail()
    contentState.plan = changePlan({
      PlanId: 7401,
      Kind: ChangePlanKind.FirefoxChange,
      Title: 'Firefox Add-ons: Submit Pusula 2.4.0 (listed)',
      Store: StoreKind.Firefox,
      StoreAppId: FIREFOX_STORE_APP_ID,
      ConfirmRequired: true,
      ConfirmText: `${FIREFOX_GUID} 2.4.0`,
      Items: [],
      Warnings: [],
      Calls: [],
    })
    server.use(...contentHandlers, ...firefoxHandlers)
  })
}
