import { delay, http, HttpResponse, type HttpHandler } from 'msw'
import type { ListRequest } from '@/lib/api/list'
import {
  ActionChannel,
  BuildProcessingState,
  StepDirection,
  StoreJobKind,
  StoreJobStatus,
  StoreJobStepStatus,
  StoreKind,
  type AppDetailDto,
  type BuildDto,
  type StoreJobDetailDto,
} from '@/lib/api/types'

// The dev mock's apps, builds and jobs (design brief 5 sample data), behind the same paths as storeasy-api. In memory.

const BASE = '*/api/v1.0'
const now = Date.now()
const minutesAgo = (minutes: number) => new Date(now - minutes * 60_000).toISOString()

function envelope<T>(data: T) {
  return HttpResponse.json({ isSuccess: true, statusCode: 200, messages: [], data })
}

function refuse(key: string, message: string, status: number) {
  return HttpResponse.json({ isSuccess: false, statusCode: status, messages: [{ key, message, type: 3 }], data: null }, { status })
}

function page<T>(rows: T[], list: ListRequest) {
  const start = (list.PageNumber - 1) * list.RowCount
  return HttpResponse.json({
    isSuccess: true,
    statusCode: 200,
    messages: [],
    data: rows.slice(start, start + list.RowCount),
    currentPageNumber: list.PageNumber,
    totalPageCount: Math.max(1, Math.ceil(rows.length / list.RowCount)),
    totalDataCount: rows.length,
  })
}

function builds(storeAppId: number, store: StoreKind, versions: string[]): BuildDto[] {
  return versions.map((version, index) => ({
    Id: storeAppId * 100 + index,
    StoreAppId: storeAppId,
    Store: store,
    ExternalId: `b-${storeAppId}-${index}`,
    Version: version,
    BuildNumber: String(400 + index),
    ProcessingState:
      index === 0 ? BuildProcessingState.Processing : index === 3 ? BuildProcessingState.Expired : BuildProcessingState.Valid,
    Tracks: store === StoreKind.Google ? 'internal' : null,
    MinOsVersion: store === StoreKind.Apple ? '16.0' : '26',
    UploadedDate: minutesAgo(60 * 24 * index + 9),
    ExpiryDate: store === StoreKind.Apple ? minutesAgo(-60 * 24 * (90 - index * 30)) : null,
  }))
}

const apps: AppDetailDto[] = [
  {
    Id: 101,
    Name: 'Pusula',
    CreationDate: '2026-09-02T09:30:00Z',
    Stores: [
      {
        StoreAppId: 201,
        Store: StoreKind.Apple,
        ExternalId: '6740056789',
        BundleId: 'dev.kuzeylab.pusula',
        Name: 'Pusula',
        StoreConnectionId: 11,
        SyncedDate: minutesAgo(3),
      },
      {
        StoreAppId: 202,
        Store: StoreKind.Google,
        ExternalId: 'dev.kuzeylab.pusula',
        BundleId: 'dev.kuzeylab.pusula',
        Name: 'Pusula',
        StoreConnectionId: 12,
        SyncedDate: minutesAgo(3),
      },
    ],
    Builds: [...builds(201, StoreKind.Apple, ['2.3.1', '2.3.0', '2.2.4', '2.2.3']), ...builds(202, StoreKind.Google, ['2.3.0'])],
  },
  {
    Id: 102,
    Name: 'Halka',
    CreationDate: '2026-10-11T12:00:00Z',
    Stores: [
      {
        StoreAppId: 210,
        Store: StoreKind.Google,
        ExternalId: 'dev.kuzeylab.halka',
        BundleId: 'dev.kuzeylab.halka',
        Name: 'Halka',
        StoreConnectionId: 12,
        SyncedDate: null,
      },
    ],
    Builds: [],
  },
  {
    Id: 103,
    Name: 'Mola',
    CreationDate: '2026-10-20T08:00:00Z',
    Stores: [
      {
        StoreAppId: 220,
        Store: StoreKind.Apple,
        ExternalId: '6741100223',
        BundleId: 'dev.kuzeylab.mola',
        Name: 'Mola',
        StoreConnectionId: 11,
        SyncedDate: minutesAgo(3),
      },
    ],
    Builds: builds(220, StoreKind.Apple, ['1.4.0', '1.3.2']),
  },
]

let nextJobId = 48300

const jobs: StoreJobDetailDto[] = [
  {
    Id: 48213,
    Kind: StoreJobKind.SyncApps,
    Status: StoreJobStatus.Succeeded,
    Store: StoreKind.Apple,
    AppId: null,
    Channel: ActionChannel.Web,
    RequestedByUserName: 'deniz@kuzeylab.dev',
    Attempt: 1,
    MaxAttempts: 3,
    CancelRequested: false,
    Summary: '3 apps, 2 new builds.',
    Error: null,
    CreationDate: minutesAgo(4),
    StartDate: minutesAgo(4),
    EndDate: minutesAgo(3),
    Payload: JSON.stringify({ ConnectionId: 11 }),
    Result: null,
    NextAttemptDate: minutesAgo(4),
    Steps: [
      {
        Order: 1,
        Attempt: 1,
        Name: 'apps.list',
        Direction: StepDirection.Outbound,
        Status: StoreJobStepStatus.Succeeded,
        Message: '3 apps.',
        StartDate: minutesAgo(4),
        EndDate: minutesAgo(4),
      },
      {
        Order: 2,
        Attempt: 1,
        Name: 'apps.save',
        Direction: StepDirection.Internal,
        Status: StoreJobStepStatus.Succeeded,
        Message: '3 records saved.',
        StartDate: minutesAgo(4),
        EndDate: minutesAgo(4),
      },
      {
        Order: 3,
        Attempt: 1,
        Name: 'builds.list',
        Direction: StepDirection.Outbound,
        Status: StoreJobStepStatus.Succeeded,
        Progress: 3,
        Total: 3,
        Message: '6 builds.',
        StartDate: minutesAgo(4),
        EndDate: minutesAgo(3),
      },
      {
        Order: 4,
        Attempt: 1,
        Name: 'builds.save',
        Direction: StepDirection.Internal,
        Status: StoreJobStepStatus.Succeeded,
        Message: '2 new builds.',
        StartDate: minutesAgo(3),
        EndDate: minutesAgo(3),
      },
    ],
  },
  {
    Id: 48214,
    Kind: StoreJobKind.SyncApps,
    Status: StoreJobStatus.Failed,
    Store: StoreKind.Google,
    AppId: null,
    Channel: ActionChannel.System,
    RequestedByUserName: 'system',
    Attempt: 3,
    MaxAttempts: 3,
    CancelRequested: false,
    Summary: null,
    Error: '403 The caller does not have permission',
    CreationDate: minutesAgo(70),
    StartDate: minutesAgo(70),
    EndDate: minutesAgo(62),
    Payload: JSON.stringify({ ConnectionId: 12 }),
    Result: null,
    NextAttemptDate: minutesAgo(64),
    Steps: [1, 2, 3].map((attempt) => ({
      Order: attempt,
      Attempt: attempt,
      Name: 'apps.list',
      Direction: StepDirection.Outbound,
      Status: StoreJobStepStatus.Failed,
      Message: attempt < 3 ? '503 Service Unavailable' : '403 The caller does not have permission',
      StartDate: minutesAgo(70 - attempt * 3),
      EndDate: minutesAgo(70 - attempt * 3),
    })),
  },
]

// The list row of a job: the detail without its parameters, result and steps.
function listOf({ Payload: _payload, Result: _result, NextAttemptDate: _next, Steps: _steps, ...row }: StoreJobDetailDto) {
  return row
}

export const appsMockHandlers: HttpHandler[] = [
  http.patch(`${BASE}/Apps`, async ({ request }) => {
    await delay(250)
    const list = (await request.json()) as ListRequest
    const search = String(list.Filtering?.Criterias?.find((criteria) => criteria.FilterBy === 'Name')?.Value ?? '').toLocaleLowerCase('tr')
    const rows = apps
      .filter((app) => !search || app.Name.toLocaleLowerCase('tr').includes(search))
      .map(({ Builds: _builds, ...app }) => app)
    return page(rows, list)
  }),
  http.get(`${BASE}/Apps/app`, async ({ request }) => {
    await delay(200)
    const app = apps.find((item) => item.Id === Number(new URL(request.url).searchParams.get('AppId')))
    return app ? envelope(app) : refuse('AppNotFound', 'Uygulama bulunamadı.', 404)
  }),
  http.post(`${BASE}/Apps/google`, async ({ request }) => {
    const { PackageName, ConnectionId } = (await request.json()) as { PackageName: string; ConnectionId: number }
    if (apps.some((app) => app.Stores?.some((record) => record.ExternalId === PackageName))) {
      return refuse('StoreAppAlreadyAdded', 'Bu uygulama zaten ekli.', 409)
    }
    const id = apps.length + 101
    apps.push({
      Id: id,
      Name: PackageName,
      CreationDate: new Date().toISOString(),
      Stores: [
        {
          StoreAppId: id + 100,
          Store: StoreKind.Google,
          ExternalId: PackageName,
          BundleId: PackageName,
          Name: PackageName,
          StoreConnectionId: ConnectionId,
        },
      ],
      Builds: [],
    })
    return envelope({ AppId: id, StoreAppId: id + 100, JobId: (nextJobId += 1) })
  }),
  http.post(`${BASE}/Apps/app/link`, async ({ request }) => {
    const { AppId } = (await request.json()) as { AppId: number }
    return envelope(AppId)
  }),
  http.post(`${BASE}/Apps/app/unlink`, () => envelope(101)),
  http.post(`${BASE}/Apps/sync`, () => envelope([(nextJobId += 1)])),

  http.patch(`${BASE}/StoreJobs`, async ({ request }) => {
    await delay(200)
    const list = (await request.json()) as ListRequest & { AppId?: number | null }
    return page(jobs.filter((job) => !list.AppId || job.AppId === list.AppId).map(listOf), list)
  }),
  http.get(`${BASE}/StoreJobs/job`, async ({ request }) => {
    await delay(200)
    const job = jobs.find((item) => item.Id === Number(new URL(request.url).searchParams.get('JobId')))
    return job ? envelope(job) : refuse('StoreJobNotFound', 'İş bulunamadı.', 404)
  }),
  http.post(`${BASE}/StoreJobs/job/cancel`, () => refuse('StoreJobAlreadyFinished', 'İş zaten bitti.', 409)),
]
