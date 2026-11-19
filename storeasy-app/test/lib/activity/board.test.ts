import { describe, expect, it } from 'vitest'
import { assignRows } from '@/components/activity/Hat'
import { ComplianceStatus, RuleApplicability, RuleStatus, type ComplianceItemDto } from '@/features/common/types'
import { RejectionKind, RejectionSource, RejectionState, type RejectionCaseListDto } from '@/features/rejections/types'
import {
  attentionOf,
  EMPTY_INPUTS,
  gateItemsOf,
  gateState,
  hourStrip,
  lastFailedJob,
  reviewPhase,
  upcomingOf,
  type BoardInputs,
} from '@/lib/activity/board'
import { ActivityEventType } from '@/lib/activity/events'
import { hatReducer, initialHatState, jobsOf } from '@/lib/activity/hatState'
import {
  BuildProcessingState,
  ConnectionStatus,
  StoreJobKind,
  StoreJobStatus,
  StoreKind,
  type ActivityBoardDto,
  type ActivityBoardVersionDto,
} from '@/lib/api/types'
import { activityEvent, connection } from '../../support/factories'

const NOW = Date.parse('2026-11-18T12:00:00Z')
const HOUR = 3_600_000
const iso = (ms: number) => new Date(ms).toISOString()

function version(overrides: Partial<ActivityBoardVersionDto>): ActivityBoardVersionDto {
  return {
    AppId: 101,
    StoreAppId: 201,
    Store: StoreKind.Apple,
    VersionString: '2.3.1',
    BuildNumber: '400',
    State: 'IN_REVIEW',
    PreviousState: 'WAITING_FOR_REVIEW',
    ChangedDate: iso(NOW - 26 * HOUR),
    SubmittedDate: iso(NOW - 40 * HOUR),
    ...overrides,
  }
}

function board(overrides: Partial<ActivityBoardDto> = {}): ActivityBoardDto {
  return { GeneratedDate: iso(NOW), HoursStartDate: iso(NOW - 23 * HOUR), Versions: [], Builds: [], Hours: [], ...overrides }
}

function compliance(days: number, status: ComplianceStatus = ComplianceStatus.Missing): ComplianceItemDto {
  return {
    AppId: 102,
    AppName: 'Halka',
    RuleId: 'google.target-api-35',
    Store: StoreKind.Google,
    Title: 'Target API level 35',
    DueDate: iso(NOW + days * 24 * HOUR).slice(0, 10),
    DaysLeft: days,
    Status: status,
    RuleStatus: RuleStatus.InForce,
    Applicability: RuleApplicability.Applies,
  }
}

const rejection: RejectionCaseListDto = {
  Id: 5,
  AppId: 103,
  Store: StoreKind.Apple,
  StoreAppId: 220,
  VersionString: '1.4.0',
  Kind: RejectionKind.Rejected,
  State: RejectionState.Open,
  OpenedVia: RejectionSource.Webhook,
  OpenedDate: iso(NOW - HOUR),
  ChecklistDone: 1,
  ChecklistTotal: 3,
}

function failedJob(message: string, store: StoreKind = StoreKind.Google) {
  const sync = { JobId: 7, JobKind: StoreJobKind.SyncApps, Store: store, Date: iso(NOW - HOUR) }
  return jobsOf(
    [
      activityEvent({ ...sync, Type: ActivityEventType.JobQueued }),
      activityEvent({ ...sync, Type: ActivityEventType.JobFailed, JobStatus: StoreJobStatus.Failed, Message: message }),
    ].reduce(hatReducer, initialHatState),
  )
}

const inputs = (overrides: Partial<BoardInputs>): BoardInputs => ({ ...EMPTY_INPUTS, ...overrides })

describe('gates', () => {
  it('names App Review states by phase', () => {
    expect(reviewPhase('IN_REVIEW')).toBe('inReview')
    expect(reviewPhase('waiting_for_review')).toBe('waiting')
    expect(reviewPhase('METADATA_REJECTED')).toBe('rejected')
    expect(reviewPhase('PENDING_DEVELOPER_RELEASE')).toBe('pendingRelease')
    expect(reviewPhase('READY_FOR_SALE')).toBe('live')
    expect(reviewPhase('PREPARE_FOR_SUBMISSION')).toBe('other')
  })

  it('lists what is with the store, most urgent first (then newest), with the app, never a bare job id', () => {
    const items = gateItemsOf(
      StoreKind.Apple,
      inputs({
        board: board({
          Versions: [version({}), version({ AppId: 103, StoreAppId: 220, State: 'REJECTED', ChangedDate: iso(NOW - HOUR) })],
          Builds: [
            {
              AppId: 101,
              StoreAppId: 201,
              Store: StoreKind.Apple,
              Version: '2.3.2',
              BuildNumber: '412',
              ProcessingState: BuildProcessingState.Processing,
              UploadedDate: iso(NOW - 0.2 * HOUR),
            },
          ],
        }),
      }),
      NOW,
    )

    expect(items.map((item) => [item.kind, item.appId, item.subject])).toEqual([
      ['rejected', 103, '2.3.1 (400)'],
      ['processing', 101, '2.3.2 (412)'],
      ['inReview', 101, '2.3.1 (400)'],
    ])
    expect(items[2].since).toBe(iso(NOW - 26 * HOUR))
    expect(gateState(items, false)).toBe('error')
  })

  it('names a failed last job and a failing connection, and spots a rate limit', () => {
    const items = gateItemsOf(
      StoreKind.Google,
      inputs({
        jobs: failedJob('429 Too Many Requests'),
        connections: [connection({ Id: 12, Store: StoreKind.Google, Status: ConnectionStatus.Unreachable, LastVerifyError: '503' })],
      }),
      NOW,
    )

    expect(items.map((item) => item.kind)).toEqual(['connection', 'lastFailed'])
    expect(items[1]).toMatchObject({ tone: 'warning', rateLimited: true, href: '/activity/jobs/7' })
  })

  it('forgets a failure once a later job of the store went well', () => {
    const jobs = failedJob('403')
    const later = { ...jobs[0], id: 8, status: StoreJobStatus.Succeeded, updatedAt: iso(NOW - 0.5 * HOUR) }

    expect(lastFailedJob(jobs, StoreKind.Google, NOW)?.id).toBe(7)
    expect(lastFailedJob([later, ...jobs], StoreKind.Google, NOW)).toBeNull()
    expect(lastFailedJob(jobs, StoreKind.Google, NOW + 30 * HOUR)).toBeNull()
  })

  it('calls a gate busy only while work is on its line', () => {
    expect(gateState([], true)).toBe('busy')
    expect(gateState([], false)).toBe('calm')
  })

  it('lays the last 24 hours out hour by hour, empty hours included', () => {
    const bars = hourStrip(
      board({ Hours: [{ Store: StoreKind.Apple, Hour: iso(NOW - 2 * HOUR), Succeeded: 3, Failed: 1, Inbound: 2 }] }),
      StoreKind.Apple,
      NOW,
    )

    expect(bars).toHaveLength(24)
    expect(bars[21]).toMatchObject({ succeeded: 3, failed: 1, inbound: 2 })
    expect(bars.filter((bar) => bar.succeeded > 0)).toHaveLength(1)
  })
})

describe('quiet board and attention', () => {
  it('lists reviews waiting first, then dates soonest first, and leaves done or far away dates out', () => {
    const items = upcomingOf(
      inputs({
        board: board({
          Versions: [version({ State: 'WAITING_FOR_REVIEW' })],
          Builds: [
            {
              AppId: 103,
              StoreAppId: 220,
              Store: StoreKind.Apple,
              Version: '1.3.2',
              BuildNumber: '401',
              ProcessingState: BuildProcessingState.Valid,
              ExpiryDate: iso(NOW + 2.5 * 24 * HOUR),
            },
          ],
        }),
        compliance: [compliance(12), compliance(3, ComplianceStatus.Done), compliance(90)],
      }),
      NOW,
    )

    expect(items.map((item) => [item.kind, item.daysLeft, item.tone])).toEqual([
      ['review', null, 'neutral'],
      ['expiry', 2, 'warning'],
      ['deadline', 12, 'neutral'],
    ])
    expect(items[0].date).toBe(iso(NOW - 40 * HOUR))
  })

  it('puts a rejection before a failed job before a date, and links each to its page', () => {
    const items = attentionOf(
      inputs({
        rejections: [rejection],
        jobs: failedJob('403 The caller does not have permission'),
        compliance: [compliance(5)],
        board: board({ Versions: [version({ AppId: 104, StoreAppId: 230, State: 'PENDING_DEVELOPER_RELEASE' })] }),
      }),
      NOW,
    )

    expect(items.map((item) => [item.kind, item.href])).toEqual([
      ['rejection', '/apps/103/rejections/5'],
      ['jobFailed', '/activity/jobs/7'],
      ['deadline', '/apps/102/compliance'],
      ['pendingRelease', '/apps/104/release/versions'],
    ])
    expect(items[0].detail).toBe('1/3')
  })

  it('asks for nothing when all is well', () => {
    expect(attentionOf(inputs({ board: board({ Versions: [version({})] }), compliance: [compliance(30)] }), NOW)).toEqual([])
  })

  it('groups a rule shared by many apps into one item and folds unverifiable rows into one', () => {
    const overdue = (appId: number, status: ComplianceStatus = ComplianceStatus.Missing): ComplianceItemDto => ({
      ...compliance(-80, status),
      AppId: appId,
      AppName: `App ${appId}`,
      RuleId: 'apple.sdk.xcode-26',
      Store: StoreKind.Apple,
      Title: 'Xcode 26',
    })
    const items = attentionOf(
      inputs({
        compliance: [
          overdue(1),
          overdue(2),
          overdue(3),
          overdue(4, ComplianceStatus.Unknown),
          overdue(5, ComplianceStatus.Unknown),
          { ...compliance(-3, ComplianceStatus.Unknown), AppId: 6 },
          compliance(5),
        ],
      }),
      NOW,
    )

    expect(items.map((item) => [item.kind, item.count, item.href, item.tone])).toEqual([
      ['deadline', 3, '/compliance', 'danger'],
      ['deadline', 1, '/apps/102/compliance', 'warning'],
      ['unverified', 3, '/compliance', 'neutral'],
    ])
  })

  it('names a rejected version with no case yet, once', () => {
    const rejectedVersion = version({ AppId: 103, StoreAppId: 220, State: 'REJECTED' })

    expect(attentionOf(inputs({ board: board({ Versions: [rejectedVersion] }) }), NOW).map((item) => item.href)).toEqual([
      '/apps/103/rejections',
    ])
    expect(attentionOf(inputs({ board: board({ Versions: [rejectedVersion] }), rejections: [rejection] }), NOW)).toHaveLength(1)
  })
})

describe('token rows', () => {
  it('puts tokens that would touch on different rows', () => {
    expect(assignRows([{ x: 0.1 }, { x: 0.8 }], 0.36)).toEqual([0, 0])
    expect(assignRows([{ x: 0.5 }, { x: 0.6 }], 0.36)).toEqual([0, 1])
    expect(assignRows([{ x: 0.5 }, { x: 0.55 }, { x: 0.6 }], 0.36)).toEqual([0, 1, 0])
  })
})
