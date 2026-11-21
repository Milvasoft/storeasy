import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ExperimentDetailPage } from '@/features/experiments/ExperimentDetailPage'
import { ExperimentsPage } from '@/features/experiments/ExperimentsPage'
import { ExperimentWizardPage } from '@/features/experiments/ExperimentWizardPage'
import { draftProblems, newVariant, textsCsv } from '@/features/experiments/rules'
import { AsoExperimentKind, AsoExperimentOperation, AsoExperimentStatus } from '@/features/experiments/types'
import { OrgRole, StoreKind } from '@/lib/api/types'
import { setupContentApi } from '../../support/content'
import { experiment, experimentsState, playExperiment, setupExperimentsApi } from '../../support/experiments'
import { appDetail, organization, storeRecord, TEST_ORG_ID } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupContentApi()
setupExperimentsApi()

function renderList(query = '?store=apple') {
  return renderWithProviders(<ExperimentsPage />, { route: `/apps/101/experiments${query}`, path: '/apps/:appId/experiments' })
}

function renderWizard(query = '?store=apple') {
  return renderWithProviders(<ExperimentWizardPage />, { route: `/apps/101/experiments/new${query}`, path: '/apps/:appId/experiments/new' })
}

function renderDetail(id: number, query = '?store=apple') {
  return renderWithProviders(<ExperimentDetailPage />, {
    route: `/apps/101/experiments/${id}${query}`,
    path: '/apps/:appId/experiments/:experimentId',
  })
}

describe('experiment rules', () => {
  it('checks names, traffic, variant count and text limits', () => {
    const variant = { ...newVariant([], ['tr']), Name: 'B', Locales: [{ Locale: 'tr', PromotionalText: 'x'.repeat(171), Media: [] }] }
    const problems = draftProblems({
      kind: AsoExperimentKind.CustomProductPage,
      name: ' ',
      traffic: null,
      variants: [variant, newVariant([variant], ['tr'])],
    })
    expect(problems.map((problem) => problem.code)).toEqual(['name', 'variantCount', 'tooLong', 'variantName', 'empty'])
    expect(
      draftProblems({ kind: AsoExperimentKind.ProductPageOptimization, name: 'Test', traffic: 0, variants: [] }).map((p) => p.code),
    ).toEqual(['traffic', 'variantCount'])
  })

  it('writes the texts as CSV with quotes escaped', () => {
    expect(textsCsv([{ VariantKey: 'A', Locale: 'tr', Field: 'shortDescription', Value: 'Bir "harita"' }])).toBe(
      'variant,locale,field,value\n"A","tr","shortDescription","Bir ""harita"""',
    )
  })
})

describe('ExperimentsPage', () => {
  it('lists the experiments of the store with status chips and reads them from the store', async () => {
    const { user } = renderList()

    expect(await screen.findByRole('link', { name: 'Yeni simge testi' })).toBeInTheDocument()
    const table = screen.getByRole('table')
    expect(within(table).getByText('Hazır')).toBeInTheDocument()
    expect(within(table).getByText('Sürüyor')).toBeInTheDocument()
    expect(within(table).getByText('Özel ürün sayfası')).toBeInTheDocument()
    expect(within(table).getByText('%30')).toBeInTheDocument()
    expect(screen.getByText(/ürün sayfası optimizasyonu, simgenin/)).toBeInTheDocument()
    expect(screen.getByText('Ürün sayfası optimizasyonu: API ile')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Mağazadan oku' }))
    await waitFor(() => expect(lastRequestTo('/aso-experiments/sync')?.body).toMatchObject({ StoreAppId: 201 }))
    expect(lastRequestTo('/aso-experiments')?.query).toMatchObject({ AppId: '101' })
  })

  it('explains the Play assisted step when the store has no experiment yet', async () => {
    renderList('?store=google')

    expect(await screen.findByText('Bu mağaza için henüz deney yok.')).toBeInTheDocument()
    expect(screen.getByText(/Android Publisher API'de mağaza girişi deneyleri/)).toBeInTheDocument()
    expect(screen.getByText('Mağaza girişi deneyi: Yardımlı adım')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Yeni deney' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Mağazadan oku' })).not.toBeInTheDocument()
  })

  it('says Microsoft Store has no experiments', async () => {
    testState.appDetail = appDetail({ Stores: [storeRecord({ StoreAppId: 203, Store: StoreKind.Microsoft, ExternalId: '9NBLGGH4R315' })] })
    renderList('?store=microsoft')

    expect(await screen.findByText(/Partner Center'da mağaza girişi deneyi yok/)).toBeInTheDocument()
    expect(screen.getByText("storeasy'de bu mağaza için deney yok.")).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Yeni deney' })).not.toBeInTheDocument()
  })
})

describe('ExperimentWizardPage', () => {
  it('offers the kinds of the store and limits the variants of each kind', async () => {
    const { user } = renderWizard()

    const kinds = await screen.findByRole('radiogroup', { name: 'Tür' })
    expect(within(kinds).getAllByRole('radio')).toHaveLength(2)
    expect(within(kinds).getByText('Ürün sayfası optimizasyonu')).toBeInTheDocument()
    expect(screen.getByLabelText('Varyantlara giden trafik (%)')).toBeInTheDocument()

    const add = screen.getByRole('button', { name: 'Varyant ekle' })
    await user.click(add)
    await user.click(add)
    expect(screen.getAllByRole('article')).toHaveLength(3)
    expect(add).toBeDisabled()

    await user.click(within(kinds).getByText('Özel ürün sayfası'))
    expect(screen.getAllByRole('article')).toHaveLength(1)
    expect(screen.queryByLabelText('Varyantlara giden trafik (%)')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Varyant ekle' })).toBeDisabled()
  })

  it('offers only the Play kinds for Google Play', async () => {
    renderWizard('?store=google')

    const kinds = await screen.findByRole('radiogroup', { name: 'Tür' })
    expect(within(kinds).getByText('Mağaza girişi deneyi')).toBeInTheDocument()
    expect(within(kinds).getByText('Özel mağaza girişi')).toBeInTheDocument()
    expect(within(kinds).queryByText('Özel ürün sayfası')).not.toBeInTheDocument()
    expect(await screen.findByText('Kısa açıklama')).toBeInTheDocument()
  })

  it('shows what is missing and sends nothing', async () => {
    const { user } = renderWizard()

    await user.click(await screen.findByRole('button', { name: 'Taslağı kaydet' }))
    expect(await screen.findByText('Deneye bir ad ver.')).toBeInTheDocument()
    expect(screen.getByText('A varyantının adı olmalı.')).toBeInTheDocument()
    expect(screen.getByText('A varyantı henüz hiçbir şeyi değiştirmiyor.')).toBeInTheDocument()
    expect(requestsTo('/aso-experiments/experiment').filter((request) => request.method === 'POST')).toHaveLength(0)
  })

  it('saves a custom product page draft and opens it', async () => {
    const { user } = renderWizard()

    const kinds = await screen.findByRole('radiogroup', { name: 'Tür' })
    await user.click(within(kinds).getByText('Özel ürün sayfası'))
    await user.type(screen.getByLabelText('Ad'), 'Sonbahar')
    await user.type(screen.getByLabelText('Varyant adı'), 'Kampanya sayfası')
    await user.type(await screen.findByLabelText(/Tanıtım metni/), 'Yeni rotalar seni bekliyor')
    await user.click(screen.getByRole('button', { name: 'Taslağı kaydet' }))

    await waitFor(() => expect(lastRequestTo('/aso-experiments/experiment')?.method).toBe('POST'))
    expect(lastRequestTo('/aso-experiments/experiment')?.body).toEqual({
      OrganizationId: TEST_ORG_ID,
      Id: null,
      AppId: 101,
      StoreAppId: 201,
      Kind: AsoExperimentKind.CustomProductPage,
      Name: 'Sonbahar',
      TrafficProportion: null,
      Variants: [
        {
          Key: 'A',
          Name: 'Kampanya sayfası',
          AppIconName: null,
          Locales: [{ Locale: 'tr', Media: [], PromotionalText: 'Yeni rotalar seni bekliyor' }],
        },
      ],
      Note: null,
    })
    expect(await screen.findByTestId('location')).toHaveTextContent('/apps/101/experiments/900?store=apple')
  })
})

describe('ExperimentDetailPage', () => {
  it('shows the variants side by side and the timeline of an Apple test', async () => {
    renderDetail(501)

    expect(await screen.findByRole('heading', { name: 'Yeni simge testi' })).toBeInTheDocument()
    expect(screen.getByRole('article', { name: 'A varyantı' })).toHaveTextContent('Turuncu simge')
    expect(screen.getByRole('article', { name: 'B varyantı' })).toHaveTextContent('Yeni ekranlar')
    expect(screen.getByText("Başlamadan önce App Review'dan geçmesi gerekiyor.")).toBeInTheDocument()
    const timeline = screen.getByRole('region', { name: 'Zaman çizelgesi' })
    expect(within(timeline).getByRole('link', { name: 'Plan: Mağazaya yaz' })).toHaveAttribute('href', '/activity/plans/7101')
    expect(screen.getByRole('link', { name: 'Mağaza konsolunda aç' })).toBeInTheDocument()
  })

  it('lets an editor plan the store write but not the start, which needs the production release permission', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    const { user } = renderDetail(501)

    const actions = await screen.findByRole('region', { name: 'Mağaza işlemleri' })
    expect(within(actions).getByRole('button', { name: 'Başlat' })).toBeDisabled()
    expect(within(actions).getByText(/Gereken yetki/)).toBeInTheDocument()

    await user.click(within(actions).getByRole('button', { name: 'Mağazaya yaz' }))
    await waitFor(() =>
      expect(lastRequestTo('/aso-experiments/experiment/plan')?.body).toMatchObject({
        Id: 501,
        Operation: AsoExperimentOperation.Push,
        DryRun: true,
      }),
    )
    expect(await screen.findByRole('dialog')).toHaveTextContent('Yeni simge testi: mağazaya yaz')
  })

  it('lets an owner plan the start', async () => {
    const { user } = renderDetail(501)

    const start = await screen.findByRole('button', { name: 'Başlat' })
    expect(start).toBeEnabled()
    await user.click(start)
    await waitFor(() =>
      expect(lastRequestTo('/aso-experiments/experiment/plan')?.body).toMatchObject({ Operation: AsoExperimentOperation.Start }),
    )
  })

  it('keeps the Play checklist and records the result by hand', async () => {
    experimentsState.experiments = [playExperiment()]
    const { user } = renderDetail(601, '?store=google')

    const checklist = await screen.findByRole('region', { name: "Play Console'daki adımlar" })
    expect(within(checklist).getByRole('link', { name: "Play Console'u aç" })).toHaveAttribute(
      'href',
      'https://play.google.com/console/developers/app/store-listing-experiments',
    )
    expect(within(checklist).getByText('Aynı anda tek bir varsayılan giriş deneyi çalışabilir.')).toBeInTheDocument()
    await user.click(within(checklist).getByRole('checkbox', { name: 'Varyantları yükle' }))
    await waitFor(() => expect(lastRequestTo('/aso-experiments/experiment/checklist')?.body).toMatchObject({ Id: 601, DoneItems: [1] }))
    expect(screen.queryByRole('region', { name: 'Mağaza işlemleri' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Dosyaları hazırla' }))
    expect(await screen.findByRole('link', { name: 'A, tr, phoneScreenshots: 01.png' })).toHaveAttribute(
      'href',
      'https://storage.test/bucket/01.png',
    )

    const result = screen.getByRole('form', { name: 'Sonuç' })
    await user.type(within(result).getByLabelText(/Başlangıç tarihi/), '2026-11-01')
    await user.type(within(result).getByLabelText(/Sonuç özeti/), 'A: +3% yükleme')
    await user.click(within(result).getByRole('button', { name: 'Sonucu kaydet' }))
    await waitFor(() =>
      expect(lastRequestTo('/aso-experiments/experiment/result')?.body).toMatchObject({
        Id: 601,
        StartDate: '2026-11-01T00:00:00Z',
        EndDate: null,
        ResultSummary: 'A: +3% yükleme',
        WinnerVariantKey: null,
      }),
    )
  })

  it('deletes a draft that never reached a store', async () => {
    experimentsState.experiments = [experiment({ Id: 503, Status: AsoExperimentStatus.Draft, ExternalId: null })]
    const { user } = renderDetail(503)

    await user.click(await screen.findByRole('button', { name: 'Sil' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Sil' }))
    const deletes = () => requestsTo('/aso-experiments/experiment').filter((request) => request.method === 'DELETE')
    await waitFor(() => expect(deletes()).toHaveLength(1))
    expect(deletes()[0].query).toMatchObject({ Id: '503' })
    expect(await screen.findByText('Deney silindi.')).toBeInTheDocument()
  })
})
