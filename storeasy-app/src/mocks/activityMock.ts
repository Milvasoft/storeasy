import { http, HttpResponse, type HttpHandler } from 'msw'
import { ComplianceStatus, RuleApplicability, RuleStatus, type ComplianceItemDto } from '@/features/common/types'
import { RejectionKind, RejectionSource, RejectionState, type RejectionCaseListDto } from '@/features/rejections/types'
import { demoScenario } from '@/lib/activity/mockSource'
import { BuildProcessingState, StoreKind, type ActivityBoardDto, type ActivityBoardHourDto } from '@/lib/api/types'

// The dev mock of what the live page shows beside the stream (design brief 5 sample data): the board (App Review states,
// builds processing or near their TestFlight expiry, 24 hours per store), open rejection cases and compliance deadlines.
// ?demo=error adds a rejection; the rest is the same in every demo.

const BASE = '*/api/v1.0'
const HOUR = 3_600_000
const DAY = 24 * HOUR

function envelope<T>(data: T) {
  return HttpResponse.json({ isSuccess: true, statusCode: 200, messages: [], data })
}

const iso = (ms: number) => new Date(ms).toISOString()
const day = (ms: number) => iso(ms).slice(0, 10)

// A steady, made up day: syncs every few hours, uploads in working hours, Play purchase events through the evening.
function hours(now: number): { start: string; rows: ActivityBoardHourDto[] } {
  const start = Math.floor(now / HOUR) * HOUR - 23 * HOUR
  const rows: ActivityBoardHourDto[] = []
  for (let index = 0; index < 24; index += 1) {
    const hour = iso(start + index * HOUR)
    const wave = (index * 7) % 5
    if (index % 3 === 0 || wave > 2) {
      rows.push({
        Store: StoreKind.Apple,
        Hour: hour,
        Succeeded: 1 + (wave % 3),
        Failed: index === 17 ? 1 : 0,
        Inbound: index % 4 === 1 ? 2 : 0,
      })
    }
    if (index % 2 === 0) {
      rows.push({
        Store: StoreKind.Google,
        Hour: hour,
        Succeeded: index % 6 === 0 ? 2 : 1,
        Failed: index === 22 ? 1 : 0,
        Inbound: 1 + (wave % 3),
      })
    }
    if (index % 6 === 4) {
      rows.push({ Store: StoreKind.Microsoft, Hour: hour, Succeeded: 1, Failed: 0, Inbound: 0 })
    }
  }
  return { start: iso(start), rows }
}

export function mockBoard(now = Date.now()): ActivityBoardDto {
  const { start, rows } = hours(now)
  const rejected = demoScenario() === 'error'
  return {
    GeneratedDate: iso(now),
    HoursStartDate: start,
    Versions: [
      {
        AppId: 103,
        StoreAppId: 220,
        Store: StoreKind.Apple,
        VersionString: '1.4.0',
        BuildNumber: '400',
        State: rejected ? 'REJECTED' : 'IN_REVIEW',
        PreviousState: rejected ? 'IN_REVIEW' : 'WAITING_FOR_REVIEW',
        ChangedDate: iso(now - (rejected ? 0.2 : 26.5) * HOUR),
        SubmittedDate: iso(now - 41 * HOUR),
      },
      {
        AppId: 101,
        StoreAppId: 201,
        Store: StoreKind.Apple,
        VersionString: '2.3.1',
        BuildNumber: '400',
        State: 'WAITING_FOR_REVIEW',
        PreviousState: 'PREPARE_FOR_SUBMISSION',
        ChangedDate: iso(now - 3.2 * HOUR),
        SubmittedDate: iso(now - 3.2 * HOUR),
      },
      {
        AppId: 104,
        StoreAppId: 230,
        Store: StoreKind.Microsoft,
        VersionString: '3.0.2',
        BuildNumber: null,
        State: 'READY_FOR_DISTRIBUTION',
        PreviousState: 'IN_REVIEW',
        ChangedDate: iso(now - 20 * HOUR),
        SubmittedDate: iso(now - 70 * HOUR),
      },
    ],
    Builds: [
      {
        AppId: 101,
        StoreAppId: 201,
        Store: StoreKind.Apple,
        Version: '2.3.1',
        BuildNumber: '400',
        ProcessingState: BuildProcessingState.Processing,
        UploadedDate: iso(now - 9 * 60_000),
        ExpiryDate: null,
      },
      {
        AppId: 103,
        StoreAppId: 220,
        Store: StoreKind.Apple,
        Version: '1.3.2',
        BuildNumber: '401',
        ProcessingState: BuildProcessingState.Valid,
        UploadedDate: iso(now - 88 * DAY),
        ExpiryDate: iso(now + 2.4 * DAY),
      },
      {
        AppId: 101,
        StoreAppId: 201,
        Store: StoreKind.Apple,
        Version: '2.2.4',
        BuildNumber: '398',
        ProcessingState: BuildProcessingState.Valid,
        UploadedDate: iso(now - 79 * DAY),
        ExpiryDate: iso(now + 11.2 * DAY),
      },
    ],
    Hours: rows,
  }
}

function compliance(now: number): ComplianceItemDto[] {
  const item = (appId: number, appName: string, store: StoreKind, ruleId: string, title: string, days: number): ComplianceItemDto => ({
    AppId: appId,
    AppName: appName,
    RuleId: ruleId,
    Store: store,
    Title: title,
    DueDate: day(now + days * DAY),
    DaysLeft: days,
    Status: ComplianceStatus.Missing,
    RuleStatus: RuleStatus.InForce,
    Applicability: RuleApplicability.Applies,
  })
  return [
    item(102, 'Halka', StoreKind.Google, 'google.target-api-35', 'Target API level 35', 12),
    item(101, 'Pusula', StoreKind.Apple, 'apple.privacy-manifest', 'Privacy manifest for required reason APIs', 26),
    item(104, 'Ada Notları', StoreKind.Microsoft, 'microsoft.age-rating', 'IARC age rating update', 38),
  ]
}

function rejections(now: number): RejectionCaseListDto[] {
  if (demoScenario() !== 'error') {
    return []
  }
  return [
    {
      Id: 5,
      AppId: 103,
      Store: StoreKind.Apple,
      StoreAppId: 220,
      VersionString: '1.4.0',
      BuildNumber: '400',
      Kind: RejectionKind.Rejected,
      State: RejectionState.Open,
      Guidelines: [],
      OpenedVia: RejectionSource.Webhook,
      OpenedDate: iso(now - 0.2 * HOUR),
      ChecklistDone: 0,
      ChecklistTotal: 3,
    },
  ]
}

export const activityMockHandlers: HttpHandler[] = [
  http.get(`${BASE}/StoreJobs/board`, () => envelope(mockBoard())),
  http.get(`${BASE}/Compliance`, () => envelope(compliance(Date.now()))),
  http.patch(`${BASE}/Rejections`, () => {
    const rows = rejections(Date.now())
    return HttpResponse.json({
      isSuccess: true,
      statusCode: 200,
      messages: [],
      data: rows,
      currentPageNumber: 1,
      totalPageCount: 1,
      totalDataCount: rows.length,
    })
  }),
]
