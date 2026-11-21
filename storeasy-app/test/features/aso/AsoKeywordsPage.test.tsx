import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole, StoreKind } from '@/lib/api/types'
import { AsoKeywordDetailPage, AsoKeywordsPage, rankPath } from '@/features/aso/AsoKeywordsPage'
import { parseKeywords } from '@/features/aso/api'
import { asoState, setupAsoApi } from '../../support/aso'
import { setupContentApi } from '../../support/content'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupContentApi()
setupAsoApi()

function render(query = '?store=apple') {
  return renderWithProviders(<AsoKeywordsPage />, { route: `/apps/101/keywords${query}`, path: '/apps/:appId/keywords' })
}

describe('AsoKeywordsPage', () => {
  it('lists the tracked keywords with rank, changes, the last 30 days and the estimates', async () => {
    render()

    const tracked = await screen.findByRole('region', { name: 'Takip edilen anahtar kelimeler' })
    const row = (await within(tracked).findByRole('link', { name: 'pusula' })).closest('tr') as HTMLElement
    expect(within(row).getByText('#8')).toBeInTheDocument()
    expect(within(row).getByText('4 yükseldi')).toBeInTheDocument()
    expect(within(row).getByText('2 düştü')).toBeInTheDocument()
    expect(within(row).getByRole('img', { name: 'pusula: Son 30 gündeki sıra' })).toBeInTheDocument()
    expect(within(row).getByText('62')).toBeInTheDocument()
    expect(within(row).getByRole('link', { name: 'pusula' })).toHaveAttribute('href', '/apps/101/keywords/501?store=apple')
    expect(screen.getByText(/Tahmini popülerlik/)).toBeInTheDocument()
    expect(lastRequestTo('/aso-keywords')?.query).toMatchObject({ AppId: '101', Store: String(StoreKind.Apple) })
  })

  it('shows the empty state without tracked keywords', async () => {
    asoState.tracked = []
    render()

    expect(await screen.findByText(/Henüz takip edilen anahtar kelime yok/)).toBeInTheDocument()
  })

  it('measures keywords, shows the data based scores and tracks the selected ones', async () => {
    const { user } = render()

    await user.type(await screen.findByLabelText('Anahtar kelimeler'), 'Kıble pusulası')
    await user.click(screen.getByRole('button', { name: 'Ölç' }))

    const research = screen.getByRole('region', { name: 'Araştırma' })
    const row = (await within(research).findByText('kıble pusulası', { selector: 'b' })).closest('tr') as HTMLElement
    expect(within(row).getByText('71')).toBeInTheDocument()
    expect(within(row).getByText('35')).toBeInTheDocument()
    expect(within(row).getByText('68')).toBeInTheDocument()
    expect(within(row).getByText('184')).toBeInTheDocument()
    expect(within(row).getByText('İlk 200 içinde değil')).toBeInTheDocument()
    expect(within(row).getByText('Kıble Bul')).toBeInTheDocument()
    expect(lastRequestTo('/aso-keywords/research')?.body).toEqual({
      OrganizationId: 7,
      AppId: 101,
      Store: StoreKind.Apple,
      Country: 'TR',
      Keywords: ['Kıble pusulası'],
    })

    await user.click(within(research).getByText('İlk sıradaki rakiplerin sırasını da kaydet'))
    await user.click(within(research).getByRole('button', { name: 'Seçilenleri takibe al' }))

    expect(await screen.findByText('Takip başladı. İlk sıra bir saat içinde kaydedilir.')).toBeInTheDocument()
    expect(lastRequestTo('/aso-keywords/track')?.body).toEqual({
      OrganizationId: 7,
      AppId: 101,
      Store: StoreKind.Apple,
      Country: 'TR',
      Keywords: ['kıble pusulası'],
      TrackCompetitors: true,
    })
    const tracked = screen.getByRole('region', { name: 'Takip edilen anahtar kelimeler' })
    expect(await within(tracked).findByRole('link', { name: 'kıble pusulası' })).toBeInTheDocument()
    expect(within(research).getByText('Takipte')).toBeInTheDocument()
  })

  it('shows the organization limit as the API words it', async () => {
    asoState.tracked = [
      asoState.tracked[0],
      { ...asoState.tracked[0], Id: 502, Keyword: 'kompas' },
      { ...asoState.tracked[0], Id: 503, Keyword: 'harita' },
    ]
    const { user } = render()

    await user.type(await screen.findByLabelText('Anahtar kelimeler'), 'kıble pusulası')
    await user.click(screen.getByRole('button', { name: 'Ölç' }))
    await user.click(await screen.findByRole('button', { name: 'Seçilenleri takibe al' }))

    expect(await screen.findByText('Organizasyonunuz en fazla 3 anahtar kelime takip edebilir.')).toBeInTheDocument()
  })

  it('suggests keywords from the listing and from AI, and adds a suggestion to the box', async () => {
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Mağaza kaydından öner' }))
    const suggestions = await screen.findByRole('group', { name: 'Öneriler' })
    await user.click(within(suggestions).getByRole('button', { name: 'kıble pusulası' }))
    expect(screen.getByLabelText('Anahtar kelimeler')).toHaveValue('kıble pusulası')

    await user.click(screen.getByRole('button', { name: 'Yapay zekadan fikir al' }))
    expect(await within(suggestions).findByRole('button', { name: 'yön bulucu' })).toHaveAttribute('title', 'Yapay zeka · Yön')
    expect(lastRequestTo('/aso-keywords/seeds/suggest')?.body).toMatchObject({ Country: 'TR', Seeds: ['kıble pusulası', 'pusula'] })
  })

  it('keeps AI ideas off without a key', async () => {
    testState.aiSettings = { HasApiKey: false, Model: 'claude-haiku-5-5' }
    render()

    expect(await screen.findByRole('button', { name: 'Yapay zekadan fikir al' })).toBeDisabled()
  })

  it('stops tracking after a confirmation', async () => {
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Takibi bırak' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/Sıra geçmişi saklanır/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Takibi bırak' }))

    await waitFor(() => expect(lastRequestTo('/aso-keywords/untrack')?.body).toEqual({ OrganizationId: 7, Ids: [501] }))
    expect(await screen.findByText(/Henüz takip edilen anahtar kelime yok/)).toBeInTheDocument()
  })

  it('shows the App Store keywords field helper', async () => {
    render()

    const helper = await screen.findByRole('region', { name: 'Anahtar kelime alanı (App Store)' })
    expect(await within(helper).findByText('100 karakterden 70 karakter kaldı')).toBeInTheDocument()
    expect(within(helper).getByText('Eksik: yönü')).toBeInTheDocument()
    expect(within(helper).getByText('İki kez harcanan: pusula, harita')).toBeInTheDocument()
    expect(within(helper).getByText('Kapsanıyor')).toBeInTheDocument()
  })

  it('has no keywords field helper for Google Play and says where its data comes from', async () => {
    render('?store=google')

    expect(await screen.findByText(/Google Play'in arama API'si yok/)).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Anahtar kelime alanı (App Store)' })).not.toBeInTheDocument()
  })

  it('lets a viewer read but not change tracking', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    const { user } = render()

    expect(await screen.findByRole('link', { name: 'pusula' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Takibi bırak' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yapay zekadan fikir al' })).not.toBeInTheDocument()

    await user.type(screen.getByLabelText('Anahtar kelimeler'), 'kıble pusulası')
    await user.click(screen.getByRole('button', { name: 'Ölç' }))
    await screen.findByText('kıble pusulası', { selector: 'b' })
    expect(screen.queryByRole('button', { name: 'Seçilenleri takibe al' })).not.toBeInTheDocument()
  })
})

describe('AsoKeywordDetailPage', () => {
  it('shows the rank chart, the top results with the app marked and switches the period', async () => {
    const { user } = renderWithProviders(<AsoKeywordDetailPage />, {
      route: '/apps/101/keywords/501?store=apple',
      path: '/apps/:appId/keywords/:keywordId',
    })

    expect(await screen.findByRole('heading', { name: '"pusula", TR' })).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Günlük sıra' })).getByRole('img', { name: 'Günlük sıra' })).toBeInTheDocument()
    const top = screen.getByRole('region', { name: 'İlk sonuçlar' })
    expect(within(top).getByText('Pusula (Bu uygulama)')).toBeInTheDocument()
    expect(lastRequestTo('/aso-keywords/ranks')?.query).toMatchObject({ Id: '501', Days: '30' })

    await user.click(screen.getByRole('button', { name: '90 gün' }))
    await waitFor(() => expect(lastRequestTo('/aso-keywords/ranks')?.query).toMatchObject({ Days: '90' }))
  })
})

describe('keyword helpers', () => {
  it('reads keywords typed one per line or comma separated, without duplicates', () => {
    expect(parseKeywords(' pusula ,Kıble  pusulası\n\npusula\nharita')).toEqual(['pusula', 'Kıble pusulası', 'harita'])
  })

  it('draws a rank line from the top, with a gap for a day without a rank', () => {
    expect(rankPath([1, 3, null, 5], 30, 40, 5)).toBe('M0 0 L10 20 M30 40')
    expect(rankPath([], 30, 40, 5)).toBe('')
  })
})
