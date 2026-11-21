import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import {
  ImpactDateSource,
  ImpactEventKind,
  ImpactMetric,
  ImpactSignal,
  ImpactStatus,
  type AsoImpactEventDto,
  type AsoImpactMetricDto,
  type AsoImpactRecentDto,
  type AsoImpactReportDto,
} from '@/features/aso-impact/types'
import { make } from './factories'
import { endpoint, envelope, errorResponse } from './handlers'
import { server } from './server'

// The impact fake backend: builders of the AsoImpactController answers and one handler per endpoint of
// src/features/aso-impact/api.ts (asoImpactContract.test.ts checks the pairing).

export function impactEvent(overrides: Partial<AsoImpactEventDto> = {}): AsoImpactEventDto {
  return make<AsoImpactEventDto>(
    {
      Id: 'plan-71',
      Kind: ImpactEventKind.Listing,
      Date: '2026-11-19',
      At: '2026-11-19T16:00:00Z',
      DateSource: ImpactDateSource.StoreLive,
      AppId: 101,
      AppName: 'Pusula',
      StoreAppId: 201,
      Store: StoreKind.Apple,
      Title: 'Listing of 2.0',
      Locales: ['tr'],
      Fields: ['subtitle'],
      PlanId: 71,
      VersionString: '2.0',
      ExternalId: null,
    },
    overrides,
  )
}

export function impactMetric(metric: AsoImpactMetricDto['Metric'], before: number | null, after: number | null): AsoImpactMetricDto {
  return {
    Metric: metric,
    Before: before,
    After: after,
    BeforeTotal: before == null || metric === ImpactMetric.ConversionRate ? null : before * 14,
    AfterTotal: after == null || metric === ImpactMetric.ConversionRate ? null : after * 14,
    AbsoluteChange: before == null || after == null ? null : after - before,
    RelativeChange: before == null || after == null || before === 0 ? null : (after - before) / before,
  }
}

function days(from: string, values: number[]) {
  const start = Date.parse(`${from}T00:00:00Z`)
  return values.map((value, index) => ({
    Offset: index,
    Date: new Date(start + index * 86400000).toISOString().slice(0, 10),
    Impressions: value * 10,
    PageViews: value * 10,
    Downloads: value,
    ConversionRate: 0.1,
  }))
}

export function impactReport(overrides: Partial<AsoImpactReportDto> = {}): AsoImpactReportDto {
  return {
    Event: impactEvent(),
    Status: ImpactStatus.Ready,
    WindowDays: 14,
    GapDays: 2,
    WholeWeeks: true,
    WeekdayMatched: false,
    Before: { From: '2026-10-01', To: '2026-10-14', DaysWithData: 14 },
    After: { From: '2026-10-17', To: '2026-10-30', DaysWithData: 14 },
    ReadyDate: '2026-10-31',
    Country: null,
    Source: null,
    Metrics: [
      impactMetric(ImpactMetric.Impressions, 1000, 1100),
      impactMetric(ImpactMetric.PageViews, 100, 120),
      impactMetric(ImpactMetric.Downloads, 10, 15),
      impactMetric(ImpactMetric.ConversionRate, 0.1, 0.125),
    ],
    Conversion: {
      Signal: ImpactSignal.Significant,
      ZScore: 2.4,
      PValue: 0.016,
      BeforeDownloads: 140,
      BeforePageViews: 1400,
      AfterDownloads: 210,
      AfterPageViews: 1680,
    },
    BeforeDays: days('2026-10-01', [10, 9, 11, 10, 10, 9, 11, 10, 10, 9, 11, 10, 10, 10]),
    AfterDays: days('2026-10-17', [15, 14, 16, 15, 15, 14, 16, 15, 15, 14, 16, 15, 15, 15]),
    Ranks: {
      KeywordCount: 2,
      Compared: 2,
      BeforeAverage: 13.5,
      AfterAverage: 9,
      BeforeTop10: 1,
      AfterTop10: 2,
      Improved: 1,
      Declined: 1,
      Keywords: [
        {
          Keyword: 'pusula',
          Country: 'tr',
          Before: 20,
          After: 12,
          Change: -8,
          BeforeCheckedDays: 14,
          BeforeRankedDays: 14,
          AfterCheckedDays: 14,
          AfterRankedDays: 14,
        },
        {
          Keyword: 'kıble',
          Country: 'tr',
          Before: 7,
          After: 6,
          Change: -1,
          BeforeCheckedDays: 14,
          BeforeRankedDays: 14,
          AfterCheckedDays: 14,
          AfterRankedDays: 14,
        },
      ],
    },
    Confounders: [],
    Related: [
      impactEvent({
        Id: 'version-9',
        Kind: ImpactEventKind.VersionReleased,
        PlanId: null,
        Locales: [],
        Fields: [],
        Title: 'Version 2.0 released',
      }),
    ],
    ...overrides,
  }
}

export function impactRecent(overrides: Partial<AsoImpactRecentDto> = {}): AsoImpactRecentDto {
  return {
    From: '2026-09-23',
    To: '2026-11-21',
    WindowDays: 14,
    Items: [
      {
        Event: impactEvent({ Date: '2026-10-15' }),
        Status: ImpactStatus.Ready,
        PageViews: impactMetric(ImpactMetric.PageViews, 100, 120),
        Downloads: impactMetric(ImpactMetric.Downloads, 10, 15),
        ConversionRate: impactMetric(ImpactMetric.ConversionRate, 0.1, 0.125),
        Signal: ImpactSignal.Significant,
        Confounded: false,
      },
      {
        Event: impactEvent({
          Id: 'plan-80',
          PlanId: 80,
          AppId: 102,
          AppName: 'Defter',
          Store: StoreKind.Google,
          StoreAppId: 301,
          Kind: ImpactEventKind.Icon,
          Locales: [],
          Fields: ['icon'],
          Date: '2026-11-15',
          DateSource: ImpactDateSource.Applied,
        }),
        Status: ImpactStatus.Collecting,
        PageViews: impactMetric(ImpactMetric.PageViews, 50, 45),
        Downloads: impactMetric(ImpactMetric.Downloads, 5, 4),
        ConversionRate: impactMetric(ImpactMetric.ConversionRate, 0.1, 0.09),
        Signal: ImpactSignal.InsufficientData,
        Confounded: true,
      },
    ],
    ...overrides,
  }
}

/** What the handlers answer; reset before every test. */
export const impactState: {
  events: AsoImpactEventDto[]
  reports: Record<string, AsoImpactReportDto>
  recent: AsoImpactRecentDto
} = { events: [], reports: {}, recent: impactRecent() }

function initialEvents(): AsoImpactEventDto[] {
  return [
    impactEvent({
      Id: 'plan-90',
      PlanId: 90,
      Date: '2026-11-20',
      DateSource: ImpactDateSource.AwaitingRelease,
      Title: 'Listing of 2.1',
      Locales: ['en-US'],
      Fields: ['description'],
    }),
    impactEvent(),
    impactEvent({
      Id: 'version-9',
      Kind: ImpactEventKind.VersionReleased,
      PlanId: null,
      Locales: [],
      Fields: [],
      Title: 'Version 2.0 released',
    }),
    impactEvent({
      Id: 'plan-60',
      PlanId: 60,
      Kind: ImpactEventKind.Screenshots,
      Store: StoreKind.Google,
      StoreAppId: 202,
      Date: '2026-11-18',
      DateSource: ImpactDateSource.Applied,
      Locales: ['tr-TR'],
      Fields: ['phoneScreenshots'],
    }),
  ]
}

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

export const impactHandlers: HttpHandler[] = [
  endpoint('get', '/aso-impact/events', () => json(envelope(impactState.events))),
  endpoint('get', '/aso-impact', ({ url }) => {
    const id = url.searchParams.get('EventId') ?? ''
    const event = impactState.events.find((item) => item.Id === id)
    if (!event) {
      return errorResponse('AsoImpactEventNotFound', 'Değişiklik bulunamadı.', 404)
    }
    return json(envelope(impactState.reports[id] ?? impactReport({ Event: event })))
  }),
  endpoint('get', '/aso-impact/recent', () => json(envelope(impactState.recent))),
]

/** Resets the impact data and answers its endpoints for every test of the calling file. */
export function setupImpactApi(): void {
  beforeEach(() => {
    impactState.events = initialEvents()
    impactState.reports = {}
    impactState.recent = impactRecent()
    server.use(...impactHandlers)
  })
}
