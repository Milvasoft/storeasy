import { fireEvent, render as renderPlain, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { AsoImpactPage } from '@/features/aso-impact/AsoImpactPage'
import { ChartMarkers } from '@/features/aso-impact/ChartMarkers'
import { eventLink, impactLink, markerIndex } from '@/features/aso-impact/labels'
import { ImpactSignal, ImpactStatus } from '@/features/aso-impact/types'
import { MetricChart } from '@/features/store-metrics/MetricChart'
import { MetricFeedStatus } from '@/features/store-metrics/types'
import { impactEvent, impactReport, impactState, setupImpactApi } from '../../support/asoImpact'
import { setupContentApi } from '../../support/content'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo } from '../../support/state'
import { metricFeed, metricsState, setupStoreMetricsApi } from '../../support/storeMetrics'

setupContentApi()
setupStoreMetricsApi()
setupImpactApi()

function render(query = '') {
  return renderWithProviders(<AsoImpactPage />, {
    route: `/apps/101/store-performance/impact${query}`,
    path: '/apps/:appId/store-performance/impact',
  })
}

describe('AsoImpactPage', () => {
  it('opens the newest live change with before and after cards, the confidence label, ranks and the related release', async () => {
    render()

    const downloads = await screen.findByRole('region', { name: 'İndirme' })
    expect(within(downloads).getByText('15')).toBeInTheDocument()
    expect(within(downloads).getByText('%50 arttı')).toBeInTheDocument()
    const conversion = screen.getByRole('region', { name: 'Dönüşüm oranı' })
    expect(within(conversion).getByText('Anlamlı (%95)')).toBeInTheDocument()
    expect(within(conversion).getByText('p = 0.016')).toBeInTheDocument()

    const ranks = screen.getByRole('region', { name: 'Anahtar kelime sıraları' })
    expect(within(ranks).getByRole('rowheader', { name: 'pusula' })).toBeInTheDocument()
    expect(within(ranks).getByText('8 sıra yükseldi')).toBeInTheDocument()
    expect(within(ranks).getByText('2 kelimede ortalama sıra 13,5 → 9')).toBeInTheDocument()

    expect(screen.getByText(/Birlikte yayına girdi: Sürüm yayında: 2.0/)).toBeInTheDocument()
    expect(screen.getByText('İki pencere de tam hafta; haftanın her günü eşit sayılıyor.')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'İndirme: önceki ve sonraki 14 gün' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Planı aç' })).toHaveAttribute('href', '/apps/101/listing/plans/71')
    // The awaiting release change is listed but not opened by default.
    expect(lastRequestTo('/aso-impact')?.query).toMatchObject({
      OrganizationId: '7',
      AppId: '101',
      EventId: 'plan-71',
      WindowDays: '14',
      GapDays: '2',
    })
    expect(within(screen.getByRole('region', { name: 'Değişiklikler' })).getByText('Yayın bekliyor')).toBeInTheDocument()
  })

  it('is a part of Store performance next to its overview', async () => {
    render()

    const parts = await screen.findByRole('group', { name: 'Mağaza performansı' })
    expect(within(parts).getByRole('link', { name: 'Genel bakış' })).toHaveAttribute('href', '/apps/101/store-performance?store=apple')
    expect(within(parts).getByRole('link', { name: 'Etki' })).toHaveAttribute('aria-current', 'page')
  })

  it('reads again for another change, window, gap, country and source', async () => {
    const { user } = render()

    await screen.findByRole('region', { name: 'İndirme' })
    const timeline = screen.getByRole('region', { name: 'Değişiklikler' })
    await user.click(within(timeline).getByRole('button', { name: /Ekran görüntüleri/ }))
    await waitFor(() => expect(lastRequestTo('/aso-impact')?.query).toMatchObject({ EventId: 'plan-60' }))
    expect(screen.getByTestId('location')).toHaveTextContent('event=plan-60')

    await user.click(screen.getByRole('button', { name: '28 gün' }))
    await waitFor(() => expect(lastRequestTo('/aso-impact')?.query).toMatchObject({ WindowDays: '28' }))

    await user.click(screen.getByRole('button', { name: 'Atlanan gün' }))
    await user.click(await screen.findByRole('option', { name: '0 gün atla' }))
    await waitFor(() => expect(lastRequestTo('/aso-impact')?.query).toMatchObject({ GapDays: '0' }))

    await user.type(screen.getByRole('textbox', { name: 'Ülke kodu' }), 'tr')
    await waitFor(() => expect(lastRequestTo('/aso-impact')?.query).toMatchObject({ Country: 'TR' }))

    await user.click(screen.getByRole('button', { name: 'Kaynak' }))
    await user.click(await screen.findByRole('option', { name: 'Arama' }))
    await waitFor(() => expect(lastRequestTo('/aso-impact')?.query).toMatchObject({ Source: '1' }))
  })

  it('warns about other changes inside the windows and opens one', async () => {
    impactState.reports['plan-71'] = impactReport({
      Confounders: [
        impactEvent({ Id: 'plan-60', PlanId: 60, Kind: 2, Store: StoreKind.Apple, Date: '2026-10-20', Locales: [], Fields: [] }),
      ],
      Conversion: { ...impactReport().Conversion, Signal: ImpactSignal.InsufficientData, PValue: null, ZScore: null },
      WeekdayMatched: true,
    })
    const { user } = render('?event=plan-71')

    const warning = await screen.findByText('Bu pencerelerde başka değişiklikler de var')
    expect(screen.getByText('Yeterli veri yok')).toBeInTheDocument()
    expect(screen.queryByText(/^p = /)).not.toBeInTheDocument()
    expect(screen.getByText(/yalnızca iki pencerede de bulunan haftanın günleriyle/)).toBeInTheDocument()

    await user.click(within(warning.closest('.callout') as HTMLElement).getByRole('button', { name: /Ekran görüntüleri/ }))
    await waitFor(() => expect(lastRequestTo('/aso-impact')?.query).toMatchObject({ EventId: 'plan-60' }))
  })

  it('says when a change is not live yet or the window is still open', async () => {
    impactState.reports['plan-90'] = impactReport({ Status: ImpactStatus.AwaitingRelease, Event: impactState.events[0] })
    impactState.reports['plan-71'] = impactReport({ Status: ImpactStatus.Collecting, ReadyDate: '2026-12-01' })
    const { user } = render('?event=plan-90')

    expect(await screen.findByText(/Bu değişiklik henüz yayında değil/)).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'İndirme' })).not.toBeInTheDocument()

    await user.click(
      within(screen.getByRole('region', { name: 'Değişiklikler' })).getAllByRole('button', { name: /Mağaza metinleri \(tr\)/ })[0],
    )
    expect(await screen.findByText(/rakamlar 1 Ara 2026 tarihinde kesinleşir/)).toBeInTheDocument()
  })

  it('points to the Store performance setup when no metrics arrive yet', async () => {
    metricsState.feeds = [metricFeed({ Status: MetricFeedStatus.NotEnabled, LatestDataDate: null, CanEnable: true })]
    render()

    expect(await screen.findByRole('heading', { name: 'Henüz mağaza performansı verisi yok' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Mağaza performansına git' })).toHaveAttribute('href', '/apps/101/store-performance')
  })

  it('shows the empty timeline', async () => {
    impactState.events = []
    render()

    expect(await screen.findByText(/Son 180 günde uygulanmış bir mağaza sayfası değişikliği/)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Etkisini görmek için bir değişiklik seç.' })).toBeInTheDocument()
  })
})

describe('chart markers', () => {
  it('places a day between the points of the chart, and nothing outside it', () => {
    const dates = ['2026-11-01', '2026-11-02', '2026-11-05']
    expect(markerIndex('2026-11-02', dates)).toBe(1)
    expect(markerIndex('2026-11-03', dates)).toBeCloseTo(1 + 1 / 3)
    expect(markerIndex('2026-10-31', dates)).toBeNull()
    expect(markerIndex('2026-11-06', dates)).toBeNull()
    expect(markerIndex('2026-11-01', [])).toBeNull()
  })

  it('draws a marker per change on the metric chart; a click or Enter opens it', () => {
    const open = vi.fn()
    renderPlain(
      <MetricChart
        points={[
          { date: '2026-11-18', value: 10 },
          { date: '2026-11-19', value: 12 },
          { date: '2026-11-20', value: 15 },
        ]}
        label="Downloads"
        formatValue={String}
        formatDate={String}
        markers={[
          { id: 'plan-71', date: '2026-11-19', label: '19 Kas 2026: Mağaza metinleri (tr)' },
          { id: 'plan-1', date: '2026-10-01', label: 'outside' },
        ]}
        onMarker={open}
      />,
    )

    const marker = screen.getByRole('button', { name: '19 Kas 2026: Mağaza metinleri (tr)' })
    expect(screen.queryByRole('button', { name: 'outside' })).not.toBeInTheDocument()
    expect(marker.querySelector('title')).toHaveTextContent('19 Kas 2026: Mağaza metinleri (tr)')
    fireEvent.click(marker)
    fireEvent.keyDown(marker, { key: 'Enter' })
    expect(open).toHaveBeenCalledTimes(2)
    expect(open).toHaveBeenCalledWith('plan-71')
  })

  it('is not a button without a handler', () => {
    renderPlain(
      <svg>
        <ChartMarkers
          markers={[{ id: 'a', date: '2026-11-02', label: 'A' }]}
          dates={['2026-11-01', '2026-11-03']}
          x={(i) => i * 10}
          top={0}
          bottom={10}
        />
      </svg>,
    )
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('links a change to its plan or versions and to its impact', () => {
    expect(eventLink(impactEvent())).toBe('/apps/101/listing/plans/71')
    expect(eventLink(impactEvent({ Id: 'version-9', PlanId: null }))).toBe('/apps/101/release/versions?store=apple')
    expect(impactLink(impactEvent())).toBe('/apps/101/store-performance/impact?event=plan-71')
  })
})
