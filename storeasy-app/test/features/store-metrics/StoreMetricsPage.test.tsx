import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole, StoreKind } from '@/lib/api/types'
import { OrgStoreMetricsPage, StoreMetricsPage } from '@/features/store-metrics/StoreMetricsPage'
import { linePaths, niceMax } from '@/features/store-metrics/MetricChart'
import { formatChange, formatMetric } from '@/features/store-metrics/labels'
import { MetricFeedStatus, TrafficSource } from '@/features/store-metrics/types'
import { setupContentApi } from '../../support/content'
import { organization } from '../../support/factories'
import { setupImpactApi } from '../../support/asoImpact'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'
import { metricFeed, metricsReport, metricsState, setupStoreMetricsApi } from '../../support/storeMetrics'

setupContentApi()
setupStoreMetricsApi()
setupImpactApi()

function render() {
  return renderWithProviders(<StoreMetricsPage />, { route: '/apps/101/store-performance', path: '/apps/:appId/store-performance' })
}

describe('StoreMetricsPage', () => {
  it('shows the KPI tiles with the change, the chart and the breakdowns of the last 30 days', async () => {
    render()

    const downloads = await screen.findByRole('region', { name: 'İndirme' })
    expect(within(downloads).getByText('550')).toBeInTheDocument()
    expect(within(downloads).getByText('Önceki döneme göre %10 fazla')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Gösterim' })).getByText('15.000')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Dönüşüm oranı' })).getByText('Önceki döneme göre %5,7 az')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Günlük İndirme/ })).toBeInTheDocument()

    const sources = screen.getByRole('region', { name: 'Kaynağa göre' })
    expect(within(sources).getByRole('rowheader', { name: 'Arama' })).toBeInTheDocument()
    expect(within(sources).getByRole('rowheader', { name: 'Göz atma' })).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Ülkeye göre' })).getByRole('rowheader', { name: 'Türkiye' })).toBeInTheDocument()
    expect(
      within(screen.getByRole('region', { name: 'Mağazaya göre' })).getByRole('rowheader', { name: 'Google Play' }),
    ).toBeInTheDocument()
    expect(lastRequestTo('/store-metrics')?.query).toMatchObject({ OrganizationId: '7', AppId: '101', Days: '30' })
  })

  it('reads again for another period, store, country and source', async () => {
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: '7 gün' }))
    await waitFor(() => expect(lastRequestTo('/store-metrics')?.query).toMatchObject({ Days: '7' }))

    await user.click(screen.getByRole('button', { name: 'Google Play' }))
    await waitFor(() => expect(lastRequestTo('/store-metrics')?.query).toMatchObject({ Store: String(StoreKind.Google) }))

    await user.click(screen.getByRole('button', { name: 'Ülke' }))
    await user.click(await screen.findByRole('option', { name: 'Türkiye' }))
    await waitFor(() => expect(lastRequestTo('/store-metrics')?.query).toMatchObject({ Country: 'TR' }))

    await user.click(screen.getByRole('button', { name: 'Kaynak' }))
    await user.click(await screen.findByRole('option', { name: 'Arama' }))
    await waitFor(() => expect(lastRequestTo('/store-metrics')?.query).toMatchObject({ Source: String(TrafficSource.Search) }))
  })

  it('shows the onboarding state with each source and what to set up', async () => {
    metricsState.report = metricsReport({ Days: [], Sources: [], Countries: [], Stores: [] })
    metricsState.feeds = [
      metricFeed({
        Status: MetricFeedStatus.NotEnabled,
        CanEnable: true,
        EnabledDate: null,
        EnabledByUserName: null,
        LatestDataDate: null,
        LastFetchedDate: null,
      }),
      metricFeed({
        StoreAppId: 202,
        Store: StoreKind.Google,
        ConnectionId: 12,
        Status: MetricFeedStatus.AccessDenied,
        ReportBucket: 'pubsite_prod_rev_1',
        LastError: '403: no storage.objects.get',
      }),
    ]
    render()

    expect(await screen.findByText('Henüz mağaza performansı verisi yok')).toBeInTheDocument()
    const sources = screen.getByRole('region', { name: 'Veri kaynakları' })
    expect(within(sources).getByText('Etkin değil')).toBeInTheDocument()
    expect(within(sources).getByText(/analitik raporları bir kez istendikten sonra gönderir/)).toBeInTheDocument()
    expect(within(sources).getByText('Erişim yok')).toBeInTheDocument()
    expect(within(sources).getByText(/Google paketi okumaya izin vermedi/)).toBeInTheDocument()
    expect(within(sources).getByText('403: no storage.objects.get')).toBeInTheDocument()
  })

  it('enables the App Store reports only after the confirmation', async () => {
    metricsState.feeds = [metricFeed({ Status: MetricFeedStatus.NotEnabled, CanEnable: true, EnabledDate: null, EnabledByUserName: null })]
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Analitik raporlarını etkinleştir' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'App Store analitik raporları etkinleştirilsin mi?' })
    expect(lastRequestTo('/store-metrics/apple/enable')).toBeUndefined()

    await user.click(within(dialog).getByRole('button', { name: 'Etkinleştir' }))

    await waitFor(() =>
      expect(lastRequestTo('/store-metrics/apple/enable')?.body).toEqual({ OrganizationId: 7, StoreAppId: 201, Confirm: true }),
    )
    expect(await screen.findByText('Veri bekleniyor')).toBeInTheDocument()
  })

  it('saves the Play reports bucket', async () => {
    const { user } = render()

    const input = await screen.findByRole('textbox', { name: 'Rapor paketi (bucket)' })
    await user.type(input, 'gs://pubsite_prod_rev_01234567890987654321/')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() =>
      expect(lastRequestTo('/store-metrics/google/bucket')?.body).toEqual({
        OrganizationId: 7,
        ConnectionId: 12,
        Bucket: 'gs://pubsite_prod_rev_01234567890987654321/',
      }),
    )
    expect(await screen.findByText('Paket kaydedildi. Play raporları okunuyor.')).toBeInTheDocument()
  })

  it('fetches now for editors and leaves enabling and the bucket to connection managers', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    metricsState.feeds = [metricFeed({ Status: MetricFeedStatus.NotEnabled, CanEnable: true }), ...metricsState.feeds.slice(1)]
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Şimdi çek' }))
    await waitFor(() => expect(lastRequestTo('/store-metrics/sync')?.body).toEqual({ OrganizationId: 7, AppId: 101 }))
    expect(screen.getByRole('button', { name: 'Analitik raporlarını etkinleştir' })).toBeDisabled()
    expect(screen.getByText('Paketi yalnızca bağlantıları yöneten üyeler değiştirebilir.')).toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: 'Rapor paketi (bucket)' })).not.toBeInTheDocument()
  })

  it('keeps fetching off for a Viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    render()

    expect(await screen.findByRole('button', { name: 'Şimdi çek' })).toBeDisabled()
  })
})

describe('StoreMetricsPage change markers', () => {
  it('marks the changes on the chart and opens a change in the impact view', async () => {
    const { user } = render()

    const marker = await screen.findByRole('button', { name: /Mağaza metinleri \(tr\)/, hidden: false })
    expect(screen.getByRole('button', { name: /Ekran görüntüleri \(tr-TR\)/ })).toBeInTheDocument()
    expect(lastRequestTo('/aso-impact/events')?.query).toMatchObject({ AppId: '101', From: '2026-10-22', To: '2026-11-20' })

    await user.click(marker)
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/apps/101/store-performance/impact?event=plan'))
  })
})

describe('OrgStoreMetricsPage', () => {
  it('lists every app with its totals and links to the app tab', async () => {
    renderWithProviders(<OrgStoreMetricsPage />, { route: '/store-performance', path: '/store-performance' })

    const link = await screen.findByRole('link', { name: 'Pusula uygulamasını aç' })
    expect(link).toHaveAttribute('href', '/apps/101/store-performance')
    expect(screen.getByRole('link', { name: 'Defter uygulamasını aç' })).toBeInTheDocument()
    expect(screen.getByText('Önceki döneme göre %30 az')).toBeInTheDocument()
    expect(lastRequestTo('/store-metrics/summary')?.query).toMatchObject({ OrganizationId: '7', Days: '30' })
  })

  it('lists the impact of recent changes with their signal and a link to each', async () => {
    renderWithProviders(<OrgStoreMetricsPage />, { route: '/store-performance', path: '/store-performance' })

    const recent = await screen.findByRole('region', { name: 'Son değişikliklerin etkisi' })
    const link = await within(recent).findByRole('link', { name: 'Pusula Mağaza metinleri (tr) değişikliğinin etkisini aç' })
    expect(link).toHaveAttribute('href', '/apps/101/store-performance/impact?event=plan-71')
    expect(within(recent).getByText('Anlamlı (%95)')).toBeInTheDocument()
    expect(within(recent).getByText('Yeterli veri yok')).toBeInTheDocument()
    expect(within(recent).getByText('Başka bir değişiklikle çakışıyor')).toBeInTheDocument()
    expect(within(recent).getByText('Veri toplanıyor')).toBeInTheDocument()
  })
})

describe('store metrics helpers', () => {
  it('formats counts, rates and changes in the UI language', () => {
    expect(formatMetric('Downloads', 12345, 'tr')).toBe('12.345')
    expect(formatMetric('ConversionRate', 0.157, 'en')).toBe('15.7%')
    expect(formatMetric('Impressions', null, 'tr')).toBe('-')
    expect(formatChange(0.1, 'en')).toBe('+10%')
    expect(formatChange(0, 'en')).toBe('0%')
    expect(formatChange(null, 'en')).toBeNull()
  })

  it('draws gaps where a day has no value and rounds the axis', () => {
    expect(
      linePaths([
        { x: 0, y: 10 },
        { x: 1, y: 20 },
        { x: 2, y: null },
        { x: 3, y: 5 },
      ]),
    ).toEqual(['M0.0,10.0L1.0,20.0', 'M3.0,5.0'])
    expect(niceMax(0)).toBe(1)
    expect(niceMax(87)).toBe(100)
    expect(niceMax(120)).toBe(200)
    expect(niceMax(2300)).toBe(2500)
  })
})
