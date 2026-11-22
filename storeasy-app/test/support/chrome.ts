import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { ChangePlanKind } from '@/features/common/types'
import { ArtifactSessionStatus } from '@/features/intake/types'
import { ChromeItemState, ChromeUploadState, type ChromeItemDto } from '@/features/chrome/types'
import { changePlan, contentHandlers, contentState } from './content'
import { appDetail, make, storeRecord } from './factories'
import { endpoint, envelope } from './handlers'
import { server } from './server'
import { testState } from './state'

// The Chrome Web Store fake backend (ChromeController): DTO builders typed with the screens' own contract, the data the
// handlers answer with, and one handler per endpoint of src/features/chrome/api.ts (chromeContract.test.ts checks the
// pairing). setupChromeApi() also answers the shared plan endpoints, through which Chrome plans are approved.

export const CHROME_STORE_APP_ID = 205
export const CHROME_ITEM_ID = 'abcdefghijklmnopabcdefghijklmnop'

export function chromeItem(overrides: Partial<ChromeItemDto> = {}): ChromeItemDto {
  return make<ChromeItemDto>(
    {
      StoreAppId: CHROME_STORE_APP_ID,
      AppId: 101,
      AppName: 'Pusula',
      ItemId: CHROME_ITEM_ID,
      ConnectionId: 15,
      PublisherId: 'pub-kuzeylab',
      PublishedState: ChromeItemState.Published,
      PublishedVersion: '2.3.0',
      PublishedDeployPercentage: 40,
      SubmittedState: ChromeItemState.None,
      SubmittedVersion: null,
      SubmittedDeployPercentage: null,
      LastUploadState: ChromeUploadState.Succeeded,
      UploadedVersion: '2.4.0',
      Warned: true,
      TakenDown: false,
      Rating: 4.46,
      RatingCount: 120,
      WeeklyUsers: 25000,
      StatusHistory: [{ Revision: 'published', State: ChromeItemState.Published, Version: '2.3.0', Date: '2026-11-20T10:00:00Z' }],
      SyncedDate: '2026-11-20T11:00:00Z',
      SyncError: null,
      StoreUrl: `https://chromewebstore.google.com/detail/${CHROME_ITEM_ID}`,
      DashboardUrl: `https://chrome.google.com/webstore/devconsole/pub-kuzeylab/${CHROME_ITEM_ID}/edit`,
      Packages: [
        {
          SessionId: 9301,
          FileName: 'pusula-2.4.0.zip',
          Size: 120000,
          Version: '2.4.0',
          Name: 'Pusula',
          ManifestVersion: 3,
          Status: ArtifactSessionStatus.Delivered,
          StartDate: '2026-11-21T09:00:00Z',
          PackageDeleted: false,
          Uploaded: true,
          Issues: [],
          Blocked: false,
        },
        {
          SessionId: 9300,
          FileName: 'pusula-mv2.zip',
          Size: 110000,
          Version: '2.3.5',
          Name: 'Pusula',
          ManifestVersion: 2,
          Status: ArtifactSessionStatus.Rejected,
          StartDate: '2026-11-20T09:00:00Z',
          PackageDeleted: false,
          Uploaded: false,
          Issues: ['manifest_version is 2; the Chrome Web Store accepts Manifest V3 packages only.'],
          Blocked: true,
        },
      ],
    },
    overrides,
  )
}

export interface ChromeState {
  item: ChromeItemDto
}

function initialChrome(): ChromeState {
  return { item: chromeItem() }
}

export const chromeState: ChromeState = initialChrome()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

export const chromeHandlers: HttpHandler[] = [
  endpoint('post', '/chrome/item', () => json(envelope({ AppId: 101, StoreAppId: CHROME_STORE_APP_ID, JobId: 49900 }))),
  endpoint('get', '/chrome/item', () => json(envelope(chromeState.item))),
  endpoint('post', '/chrome/change', () => json(envelope(contentState.plan))),
  endpoint('post', '/chrome/sync', () => json(envelope(49901))),
]

/** The app of the tests with a Chrome Web Store record next to its Apple and Google ones. */
export function chromeAppDetail() {
  const base = appDetail()
  return appDetail({
    Stores: [
      ...(base.Stores ?? []),
      storeRecord({
        StoreAppId: CHROME_STORE_APP_ID,
        Store: StoreKind.Chrome,
        ExternalId: CHROME_ITEM_ID,
        BundleId: CHROME_ITEM_ID,
        StoreConnectionId: 15,
      }),
    ],
  })
}

/** Resets the Chrome data and answers its endpoints (and the shared plan endpoints) for every test of the calling file. */
export function setupChromeApi(): void {
  beforeEach(() => {
    Object.assign(chromeState, initialChrome())
    testState.appDetail = chromeAppDetail()
    contentState.plan = changePlan({
      PlanId: 7301,
      Kind: ChangePlanKind.ChromeChange,
      Title: `Chrome Web Store: Submit ${CHROME_ITEM_ID} 2.4.0`,
      Store: StoreKind.Chrome,
      StoreAppId: CHROME_STORE_APP_ID,
      ConfirmRequired: true,
      ConfirmText: `${CHROME_ITEM_ID} 2.4.0`,
      Items: [],
      Warnings: [],
      Calls: [],
    })
    server.use(...contentHandlers, ...chromeHandlers)
  })
}
