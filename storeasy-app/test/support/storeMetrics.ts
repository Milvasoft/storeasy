import type { HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import {
  MetricFeedStatus,
  TrafficSource,
  type StoreMetricFeedDto,
  type StoreMetricsReportDto,
  type StoreMetricsSummaryDto,
  type StoreMetricValuesDto,
} from '@/features/store-metrics/types'
import { errorResponse, endpoint, envelope } from './handlers'
import { server } from './server'

// The store performance fake backend: builders of the StoreMetricsController answers and one handler per endpoint of
// src/features/store-metrics/api.ts (storeMetricsContract.test.ts checks the pairing).

export function values(overrides: Partial<StoreMetricValuesDto> = {}): StoreMetricValuesDto {
  return { Impressions: null, PageViews: null, Downloads: null, Redownloads: null, ConversionRate: null, ...overrides }
}

export function metricsReport(overrides: Partial<StoreMetricsReportDto> = {}): StoreMetricsReportDto {
  return {
    From: '2026-10-22',
    To: '2026-11-20',
    PreviousFrom: '2026-09-22',
    PreviousTo: '2026-10-21',
    Totals: values({ Impressions: 15000, PageViews: 3500, Downloads: 550, Redownloads: 20, ConversionRate: 550 / 3500 }),
    PreviousTotals: values({ Impressions: 12000, PageViews: 3000, Downloads: 500, Redownloads: 25, ConversionRate: 500 / 3000 }),
    Changes: values({ Impressions: 0.25, PageViews: 1 / 6, Downloads: 0.1, Redownloads: -0.2, ConversionRate: -0.057 }),
    Days: [
      { Date: '2026-11-18', ...values({ Impressions: 500, PageViews: 120, Downloads: 18, ConversionRate: 0.15 }) },
      { Date: '2026-11-19', ...values({ Impressions: 520, PageViews: 110, Downloads: 20, ConversionRate: 20 / 110 }) },
      { Date: '2026-11-20', ...values({ Impressions: 610, PageViews: 140, Downloads: 25, ConversionRate: 25 / 140 }) },
    ],
    Sources: [
      {
        Source: TrafficSource.Search,
        Country: null,
        Store: null,
        Totals: values({ PageViews: 2500, Downloads: 400, ConversionRate: 0.16 }),
      },
      {
        Source: TrafficSource.Browse,
        Country: null,
        Store: null,
        Totals: values({ PageViews: 1000, Downloads: 150, ConversionRate: 0.15 }),
      },
    ],
    Countries: [
      { Source: null, Country: 'TR', Store: null, Totals: values({ PageViews: 3000, Downloads: 500, ConversionRate: 1 / 6 }) },
      { Source: null, Country: 'DE', Store: null, Totals: values({ PageViews: 500, Downloads: 50, ConversionRate: 0.1 }) },
    ],
    Stores: [
      {
        Source: null,
        Country: null,
        Store: StoreKind.Apple,
        Totals: values({ Impressions: 15000, PageViews: 1500, Downloads: 150, ConversionRate: 0.1 }),
      },
      { Source: null, Country: null, Store: StoreKind.Google, Totals: values({ PageViews: 2000, Downloads: 400, ConversionRate: 0.2 }) },
    ],
    ...overrides,
  }
}

export function metricFeed(overrides: Partial<StoreMetricFeedDto> = {}): StoreMetricFeedDto {
  return {
    StoreAppId: 201,
    Store: StoreKind.Apple,
    ConnectionId: 11,
    ConnectionName: 'Şirket anahtarı',
    Status: MetricFeedStatus.Active,
    CanEnable: false,
    ReportBucket: null,
    EnabledDate: '2026-11-01T09:00:00Z',
    EnabledByUserName: 'deniz@kuzeylab.dev',
    LastFetchedDate: '2026-11-21T05:30:00Z',
    LastSucceededDate: '2026-11-21T05:30:00Z',
    EarliestDataDate: '2026-11-02',
    LatestDataDate: '2026-11-20',
    LastError: null,
    LastJobId: 48500,
    ...overrides,
  }
}

export function metricsSummary(overrides: Partial<StoreMetricsSummaryDto> = {}): StoreMetricsSummaryDto {
  return {
    From: '2026-10-22',
    To: '2026-11-20',
    Totals: values({ Impressions: 15000, PageViews: 4000, Downloads: 600, ConversionRate: 0.15 }),
    Changes: values({ Impressions: 0.1, PageViews: 0.05, Downloads: 0.2, ConversionRate: 0.02 }),
    Apps: [
      {
        AppId: 101,
        AppName: 'Pusula',
        Totals: values({ Impressions: 15000, PageViews: 3500, Downloads: 550, ConversionRate: 550 / 3500 }),
        Changes: values({ Downloads: 0.1 }),
      },
      {
        AppId: 102,
        AppName: 'Defter',
        Totals: values({ PageViews: 500, Downloads: 50, ConversionRate: 0.1 }),
        Changes: values({ Downloads: -0.3 }),
      },
    ],
    ...overrides,
  }
}

/** What the handlers answer; reset before every test. */
export const metricsState: {
  report: StoreMetricsReportDto
  feeds: StoreMetricFeedDto[]
  summary: StoreMetricsSummaryDto
} = { report: metricsReport(), feeds: [], summary: metricsSummary() }

export const storeMetricsHandlers: HttpHandler[] = [
  endpoint('get', '/store-metrics', () => Response.json(envelope(metricsState.report))),
  endpoint('get', '/store-metrics/summary', () => Response.json(envelope(metricsState.summary))),
  endpoint('get', '/store-metrics/feeds', () => Response.json(envelope(metricsState.feeds))),
  endpoint('post', '/store-metrics/sync', () => Response.json(envelope([48601, 48602]))),
  endpoint('post', '/store-metrics/apple/enable', ({ body }) => {
    const request = body as { StoreAppId: number; Confirm?: boolean }
    if (!request.Confirm) {
      return errorResponse('AppleAnalyticsConfirmRequired', 'Onayla.')
    }
    metricsState.feeds = metricsState.feeds.map((feed) =>
      feed.StoreAppId === request.StoreAppId
        ? { ...feed, Status: MetricFeedStatus.Waiting, CanEnable: false, EnabledByUserName: 'deniz@kuzeylab.dev' }
        : feed,
    )
    return Response.json(envelope(48700))
  }),
  endpoint('put', '/store-metrics/google/bucket', ({ body }) => {
    const request = body as { Bucket: string }
    const bucket = request.Bucket.trim()
      .replace(/^gs:\/\//i, '')
      .split('/')[0]
    if (bucket && !/^[a-z0-9][a-z0-9._-]+$/.test(bucket)) {
      return errorResponse('ReportBucketInvalid', 'Play Console raporlarının Cloud Storage paketini (bucket) gir.')
    }
    return Response.json(envelope(bucket || null))
  }),
]

export function setupStoreMetricsApi(): void {
  beforeEach(() => {
    metricsState.report = metricsReport()
    metricsState.feeds = [
      metricFeed(),
      metricFeed({
        StoreAppId: 202,
        Store: StoreKind.Google,
        ConnectionId: 12,
        ConnectionName: 'Play hizmet hesabı',
        Status: MetricFeedStatus.NeedsSetup,
        EnabledDate: null,
        EnabledByUserName: null,
        LatestDataDate: null,
      }),
    ]
    metricsState.summary = metricsSummary()
    server.use(...storeMetricsHandlers)
  })
}
