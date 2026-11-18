import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { OrgPermission, OrgRole } from '@/lib/api/types'
import { SubscriptionDetailPage, type SubscriptionTab } from '@/features/monetization/ProductPages'
import { ProductState } from '@/features/monetization/types'
import { organization } from '../../support/factories'
import { STORAGE_URL } from '../../support/handlers'
import { moneyPlan, moneyState, setupMonetizationApi, subscriptionDetail } from '../../support/monetization'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupMonetizationApi()

type User = ReturnType<typeof render>['user']

// A price point picker opens once the territory's price points are read; an option is named by its price.
async function pickPrice(user: User, trigger: HTMLElement, price: string) {
  await waitFor(() => expect(trigger).toBeEnabled())
  await user.click(trigger)
  await user.click(await screen.findByRole('option', { name: (name) => name.startsWith(price) }))
}

const PATHS: Record<SubscriptionTab, string> = { general: '', prices: '/prices', offers: '/offers' }

function render(tab: SubscriptionTab) {
  return renderWithProviders(<SubscriptionDetailPage tab={tab} />, {
    route: `/apps/101/money/subscriptions/3101${PATHS[tab]}?store=apple`,
    path: `/apps/:appId/money/subscriptions/:subscriptionId${PATHS[tab]}`,
  })
}

describe('SubscriptionDetailPage general', () => {
  it('collects field changes and plans them together', async () => {
    const { user } = render('general')
    const name = await screen.findByLabelText('Görünen ad')

    expect(screen.getByLabelText('Ürün kimliği')).toHaveAttribute('readonly')
    await user.clear(name)
    await user.type(name, 'Pusula Plus')
    await user.clear(screen.getByLabelText(/İnceleme notu/))
    await user.type(screen.getByLabelText(/İnceleme notu/), 'Demo hesap hazır.')
    await user.click(screen.getByRole('button', { name: 'Değişiklikleri gözden geçir · 2' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      StoreAppId: 201,
      Changes: [
        { Kind: 5, ProductId: 'dev.kuzeylab.pusula.plus.monthly', SubscriptionId: 3101, ReviewNote: 'Demo hesap hazır.' },
        { Kind: 6, ProductId: 'dev.kuzeylab.pusula.plus.monthly', Locale: 'tr', Name: 'Pusula Plus', Description: 'Çevrimdışı haritalar.' },
      ],
    })
  })

  it('blocks a display name longer than the store allows', async () => {
    const { user } = render('general')
    const name = await screen.findByLabelText('Görünen ad')

    await user.type(name, ' çok uzun bir ad ekleniyor')

    expect(screen.getByText('Bir metin mağazanın sınırından uzun.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Değişiklikleri gözden geçir/ })).toBeDisabled()
  })

  it('asks before turning on Family Sharing, which cannot be undone', async () => {
    const { user } = render('general')

    await user.click(await screen.findByRole('switch', { name: 'Aile paylaşımı' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Aile paylaşımı açılsın mı?' })
    await user.click(within(confirm).getByRole('button', { name: 'Aç' }))
    await user.click(screen.getByRole('button', { name: 'Değişiklikleri gözden geçir · 1' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({ Changes: [{ Kind: 5, FamilySharable: true }] })
  })

  it('uploads the review screenshot and plans it', async () => {
    server.use(
      http.put(
        `${STORAGE_URL}/*`,
        () => new HttpResponse(null, { status: 200, headers: { 'Access-Control-Allow-Origin': '*', ETag: '"e"' } }),
      ),
    )
    const { user, container } = render('general')

    const review = await screen.findByRole('region', { name: 'İnceleme bilgisi' })
    expect(within(review).getByText('İnceleme görüntüsü eksik.')).toBeInTheDocument()
    const input = container.querySelector('#review input[type="file"]') as HTMLInputElement
    await user.upload(input, new File([new Uint8Array(4)], 'satin-alma.png', { type: 'image/png' }))
    await user.click(await screen.findByRole('button', { name: 'Değişiklikleri gözden geçir · 1' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect((lastRequestTo('/Subscriptions/plan')?.body as { Changes: Array<{ Kind: number; AssetId: number }> }).Changes[0]).toMatchObject({
      Kind: 15,
      ProductId: 'dev.kuzeylab.pusula.plus.monthly',
    })
    expect(requestsTo('/Assets/upload')).toHaveLength(1)
  })

  it('plans the territories where the product is sold', async () => {
    const { user } = render('general')

    await user.click(await screen.findByRole('button', { name: 'Bölgeleri düzenle' }))
    const dialog = screen.getByRole('dialog')
    const codes = within(dialog).getByLabelText(/Bölge kodları/)
    await user.clear(codes)
    await user.type(codes, 'tur, usa, xx')
    expect(within(dialog).getByText('Bölge kodu değil: XX')).toBeInTheDocument()
    await user.clear(codes)
    await user.type(codes, 'tur, usa')
    await user.click(within(dialog).getByRole('button', { name: 'Tamam' }))
    await user.click(screen.getByRole('button', { name: 'Değişiklikleri gözden geçir · 1' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [{ Kind: 10, Territories: ['TUR', 'USA'], AvailableInNewTerritories: true }],
    })
  })

  it('keeps the fields read only for a viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    render('general')

    expect(await screen.findByLabelText('Görünen ad')).toBeDisabled()
    expect(screen.queryByRole('button', { name: /Değişiklikleri gözden geçir/ })).not.toBeInTheDocument()
  })
})

describe('SubscriptionDetailPage prices', () => {
  it('lists territory prices with their source and the scheduled change', async () => {
    render('prices')

    const table = await screen.findByRole('table', { name: 'Bölge fiyatları' })
    const germany = within(table).getByText('Almanya').closest('tr') as HTMLElement
    expect(germany).toHaveTextContent('1.99 EUR')
    expect(germany).toHaveTextContent('2.49 EUR')
    expect(germany).toHaveTextContent('Planlı')
    expect(germany).toHaveTextContent('elle')
    expect(lastRequestTo('/Subscriptions/prices')?.body).toMatchObject({ SubscriptionId: 3101, PageNumber: 1, RowCount: 20 })
  })

  it('filters by source, schedule and territory name on the server', async () => {
    const { user } = render('prices')
    await screen.findByRole('table', { name: 'Bölge fiyatları' })

    await user.click(screen.getByRole('button', { name: 'Planlı' }))
    await waitFor(() => expect(lastRequestTo('/Subscriptions/prices')?.body).toMatchObject({ HasPendingChange: true, Source: null }))
    await user.click(screen.getByRole('button', { name: 'Elle' }))
    await waitFor(() => expect(lastRequestTo('/Subscriptions/prices')?.body).toMatchObject({ Source: 2, HasPendingChange: null }))
    await user.type(screen.getByLabelText('Bölge ara'), 'Alm')
    await waitFor(() => expect(lastRequestTo('/Subscriptions/prices')?.body).toMatchObject({ TerritoryName: 'Alm' }))
  })

  it('changes one territory from its price points without asking about subscribers when not on sale', async () => {
    const { user } = render('prices')

    await user.click(await screen.findByRole('button', { name: 'Almanya değiştir' }))
    const drawer = screen.getByRole('dialog', { name: 'Almanya fiyatı' })
    expect(within(drawer).queryByText('Mevcut aboneler')).not.toBeInTheDocument()
    await pickPrice(user, within(drawer).getByLabelText('Yeni fiyat'), '2.49 EUR')
    await user.click(within(drawer).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/price-points')?.query).toMatchObject({ SubscriptionId: '3101', Territory: 'DEU' })
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [{ Kind: 9, Territory: 'DEU', PricePointId: 'pp-deu-249', StartDate: null, PreserveCurrentPrice: null }],
    })
  })

  it('requires the existing subscribers choice for a subscription on sale', async () => {
    moneyState.subscription = subscriptionDetail({ State: ProductState.Approved, StoreState: 'APPROVED' })
    const { user } = render('prices')

    await user.click(await screen.findByRole('button', { name: 'Taban fiyatı değiştir' }))
    const dialog = screen.getByRole('dialog')
    await pickPrice(user, within(dialog).getByLabelText('Yeni fiyat'), '2.49 EUR')
    await user.click(within(dialog).getByRole('button', { name: 'Planı gözden geçir' }))

    expect(within(dialog).getByText('Mevcut abonelere ne olacağını seç.')).toBeInTheDocument()
    expect(lastRequestTo('/Subscriptions/plan')).toBeUndefined()
    expect(within(dialog).getByText('Satıştaki ürün fiyatı: onaylayabilirsin (Sahip veya Yönetici).')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('radio', { name: /Mevcut abonelere de uygula/ }))
    await user.click(within(dialog).getByRole('button', { name: 'Planı gözden geçir' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [{ Kind: 8, Territory: 'TUR', PricePointId: 'pp-deu-249', Equalize: true, PreserveCurrentPrice: false }],
    })
  })

  it('approves a plan that reaches existing subscribers only with the typed confirmation text', async () => {
    moneyState.subscription = subscriptionDetail({ State: ProductState.Approved })
    moneyState.plan = moneyPlan({
      RequiredPermission: OrgPermission.ReleaseProduction,
      ConfirmRequired: true,
      ConfirmText: 'Pusula dev.kuzeylab.pusula.plus.monthly',
      Warnings: [{ Key: 'MonetizationExistingSubscribersMigrated', Message: 'Mevcut aboneler yeni fiyata geçer.' }],
    })
    const { user } = render('prices')

    await user.click(await screen.findByRole('button', { name: 'Almanya değiştir' }))
    const drawer = screen.getByRole('dialog', { name: 'Almanya fiyatı' })
    await pickPrice(user, within(drawer).getByLabelText('Yeni fiyat'), '2.49 EUR')
    await user.click(within(drawer).getByRole('radio', { name: /Mevcut abonelere de uygula/ }))
    await user.click(within(drawer).getByRole('button', { name: 'Plana ekle' }))
    const plan = await screen.findByRole('dialog', { name: moneyState.plan.Title })
    expect(within(plan).getByText('Mevcut aboneler yeni fiyata geçer.')).toBeInTheDocument()
    await user.click(within(plan).getByRole('button', { name: 'Onayla ve gönder' }))
    const confirm = await screen.findByRole('alertdialog')
    await user.type(
      within(confirm).getByLabelText('Onaylamak için Pusula dev.kuzeylab.pusula.plus.monthly yaz'),
      'Pusula dev.kuzeylab.pusula.plus.monthly',
    )
    await user.click(within(confirm).getByRole('button', { name: 'Onayla ve gönder' }))

    await waitFor(() => expect(lastRequestTo('/plans/approve')).toBeDefined())
    expect(lastRequestTo('/plans/approve')?.body).toMatchObject({ PlanId: 7701, Confirm: 'Pusula dev.kuzeylab.pusula.plus.monthly' })
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({ Changes: [{ Kind: 9, PreserveCurrentPrice: false }] })
  })

  it('leaves an editor the plan of a price on sale to approve by an owner or admin', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    moneyState.subscription = subscriptionDetail({ State: ProductState.Approved })
    moneyState.plan = moneyPlan({ RequiredPermission: OrgPermission.ReleaseProduction })
    const { user } = render('prices')

    await user.click(await screen.findByRole('button', { name: 'Almanya değiştir' }))
    const drawer = screen.getByRole('dialog', { name: 'Almanya fiyatı' })
    expect(within(drawer).getByText('Satıştaki ürün fiyatı: planı Sahip veya Yönetici onaylar.')).toBeInTheDocument()
    await pickPrice(user, within(drawer).getByLabelText('Yeni fiyat'), '1.99 EUR')
    await user.click(within(drawer).getByRole('radio', { name: /Mevcut fiyatı koru/ }))
    await user.click(within(drawer).getByRole('button', { name: 'Plana ekle' }))

    const plan = await screen.findByRole('dialog', { name: moneyState.plan.Title })
    expect(within(plan).getByRole('button', { name: 'Onayla ve gönder' })).toBeDisabled()
    expect(within(plan).getByText('Bu planı onaylamak Production yayını iznini gerektirir.')).toBeInTheDocument()
  })
})

describe('SubscriptionDetailPage offers', () => {
  it('shows offer codes by their counts only', async () => {
    render('offers')

    const codes = await screen.findByRole('region', { name: 'Teklif kodları' })
    expect(codes).toHaveTextContent('Özel kod PUSULA2027 · 500 kullanım')
    expect(codes).toHaveTextContent('1000 tek kullanımlık kod')
    expect(screen.getByRole('region', { name: 'Tanıtım teklifi' })).toHaveTextContent('Ücretsiz deneme · 1 hafta')
  })

  it('plans a free trial introductory offer for every territory', async () => {
    const { user } = render('offers')

    await user.click(await screen.findByRole('button', { name: 'Tanıtım teklifi ekle' }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [{ Kind: 11, SubscriptionId: 3101, Mode: 1, Duration: 'ONE_WEEK', Periods: 1, Prices: null, StartDate: null }],
    })
  })

  it('plans a promotional offer with a price point per territory', async () => {
    const { user } = render('offers')

    await user.click(await screen.findByRole('button', { name: 'Promosyon teklifleri ekle' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('Referans adı'), 'Geri dönüş')
    await user.type(within(dialog).getByLabelText('Teklif kimliği'), 'geri_donus_50')
    await user.click(within(dialog).getByRole('radio', { name: /Kullandıkça öde/ }))
    await user.clear(within(dialog).getByLabelText('Dönem sayısı'))
    await user.type(within(dialog).getByLabelText('Dönem sayısı'), '3')
    await pickPrice(user, within(dialog).getByLabelText('Fiyat 1'), '1.99 EUR')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [
        {
          Kind: 12,
          ReferenceName: 'Geri dönüş',
          Code: 'geri_donus_50',
          Mode: 2,
          Duration: 'ONE_MONTH',
          Periods: 3,
          Prices: [{ Territory: 'TUR', PricePointId: 'pp-deu-199' }],
        },
      ],
    })
  })

  it('plans an offer code with a custom code and one-time codes', async () => {
    const { user } = render('offers')

    await user.click(await screen.findByRole('button', { name: 'Teklif kodları ekle' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('Referans adı'), 'Bahar')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Aboneliği bitmiş olanlar' }))
    await pickPrice(user, within(dialog).getByLabelText('Fiyat 1'), '1.99 EUR')
    await user.type(within(dialog).getByLabelText(/Özel kod/), 'bahar27')
    await user.type(within(dialog).getByLabelText('Kullanım'), '100')
    await user.type(within(dialog).getByLabelText(/Tek kullanımlık kodlar/), '500')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [
        { Kind: 13, ReferenceName: 'Bahar', Eligibility: ['NEW', 'EXPIRED'], Code: 'BAHAR27', CustomCodeCount: 100, OneTimeCodeCount: 500 },
      ],
    })
  })

  it('plans a win-back offer that starts on a future day', async () => {
    const { user } = render('offers')

    await user.click(await screen.findByRole('button', { name: 'Geri kazanma teklifleri ekle' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('Referans adı'), 'Geri gel')
    await user.type(within(dialog).getByLabelText('Teklif kimliği'), 'geri_gel')
    await pickPrice(user, within(dialog).getByLabelText('Fiyat 1'), '1.99 EUR')
    expect(within(dialog).getByRole('button', { name: 'Plana ekle' })).toBeDisabled()
    await user.type(within(dialog).getByLabelText(/^Başlangıç/), '2027-01-10')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [
        { Kind: 14, Code: 'geri_gel', PaidMonths: 1, SinceMinMonths: 1, SinceMaxMonths: 12, Priority: 'NORMAL', StartDate: '2027-01-10' },
      ],
    })
  })

  it('plans deactivating an offer code', async () => {
    const { user } = render('offers')

    await user.click(await screen.findByRole('button', { name: 'Lansman kapat' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({ Changes: [{ Kind: 13, ReferenceName: 'Lansman', Active: false }] })
  })
})
