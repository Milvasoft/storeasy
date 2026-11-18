import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { BasePlanPricesPage } from '@/features/monetization/PlayPanels'
import { IapDetailPage, IapListPage } from '@/features/monetization/ProductPages'
import { PriceSource, ProductState, StoreProductKind } from '@/features/monetization/types'
import { endpoint, errorResponse } from '../../support/handlers'
import { iapItem, moneyState, productPrice, setupMonetizationApi, subscriptionDetail } from '../../support/monetization'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo } from '../../support/state'

setupMonetizationApi()

function at(route: string, path: string, element: JSX.Element) {
  return renderWithProviders(element, { route, path })
}

const playSubscription = () =>
  subscriptionDetail({
    Id: 5101,
    StoreAppId: 202,
    Store: StoreKind.Google,
    ProductId: 'pusula_premium',
    ReferenceName: null,
    State: ProductState.Active,
    Localizations: [{ Locale: 'tr-TR', Name: 'Pusula Premium', Benefits: ['Çevrimdışı haritalar'] }],
    Plans: [
      {
        BasePlanId: 'aylik',
        Type: 'AutoRenewing',
        BillingPeriod: 'P1M',
        State: ProductState.Active,
        BasePrice: { Territory: 'TR', Currency: 'TRY', Amount: '54.99', Source: PriceSource.Manual },
        RegionCount: 2,
        Offers: [],
      },
    ],
  })

describe('IapListPage', () => {
  it('lists products with kind, base price and a missing review screenshot link', async () => {
    at('/apps/101/money/iap?store=apple', '/apps/:appId/money/iap', <IapListPage />)

    const table = await screen.findByRole('table', { name: 'Uygulama içi ürünler' })
    const goal = within(table).getByText('Ek hedef hakkı').closest('tr') as HTMLElement
    expect(goal).toHaveTextContent('Tüketilebilir')
    expect(within(goal).getByRole('link', { name: 'İnceleme görüntüsü yok' })).toHaveAttribute(
      'href',
      '/apps/101/money/iap/4102?store=apple#review',
    )
    expect(lastRequestTo('/Iap')?.body).toMatchObject({ StoreAppId: 201, PageNumber: 1, RowCount: 20 })
  })

  it('filters by kind and search on the server', async () => {
    const { user } = at('/apps/101/money/iap?store=apple', '/apps/:appId/money/iap', <IapListPage />)
    await screen.findByRole('table', { name: 'Uygulama içi ürünler' })

    await user.click(screen.getByRole('button', { name: 'Tüketilemez' }))
    await waitFor(() => expect(lastRequestTo('/Iap')?.body).toMatchObject({ Kind: StoreProductKind.NonConsumable }))
    await user.type(screen.getByLabelText('Ürün ara'), 'tema')
    await waitFor(() => expect(lastRequestTo('/Iap')?.body).toMatchObject({ Search: 'tema', Kind: StoreProductKind.NonConsumable }))
  })

  it('keeps the create dialog open with the store refusal of a taken product id', async () => {
    server.use(
      endpoint('post', '/Iap/plan', () => errorResponse('MonetizationProductIdTaken', 'Bu ürün kimliği hesapta zaten kullanılmış.', 409)),
    )
    const { user } = at('/apps/101/money/iap?store=apple', '/apps/:appId/money/iap', <IapListPage />)

    await user.click(await screen.findByRole('button', { name: 'Ürün oluştur' }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('radio', { name: /Tüketilebilir/ }))
    await user.type(within(dialog).getByLabelText('Ürün kimliği'), 'dev.kuzeylab.pusula.theme.night')
    await user.type(within(dialog).getByLabelText('Referans adı'), 'Gece teması')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    expect(await within(dialog).findByText('Bu ürün kimliği hesapta zaten kullanılmış.')).toBeInTheDocument()
    expect(within(dialog).getByText('Mağaza bu ürün kimliğini kabul etmedi.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Ürün kimliği')).toHaveValue('dev.kuzeylab.pusula.theme.night')
    expect(lastRequestTo('/Iap/plan')?.body).toMatchObject({
      Changes: [
        { Kind: 16, ProductKind: StoreProductKind.Consumable, ProductId: 'dev.kuzeylab.pusula.theme.night', ReferenceName: 'Gece teması' },
      ],
    })
  })

  it('creates a Play one-time product with its listing and first price', async () => {
    moneyState.iaps = [iapItem({ Id: 6101, Store: StoreKind.Google, Kind: StoreProductKind.OneTimeProduct, ProductId: 'tema_gece' })]
    const { user } = at('/apps/101/money/iap?store=google', '/apps/:appId/money/iap', <IapListPage />)

    expect(await screen.findByText("Ürünün tüketilebilir olup olmadığına uygulama karar verir; Play'de ayrı tür yok.")).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Ürün oluştur' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('Ürün kimliği'), 'rozet')
    await user.type(within(dialog).getByLabelText('Başlık'), 'Rozet')
    await user.type(within(dialog).getByLabelText('Açıklama'), 'Destek rozeti')
    await user.type(within(dialog).getByLabelText('Tutar'), '19.99')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Iap/plan')).toBeDefined())
    expect(lastRequestTo('/Iap/plan')?.body).toMatchObject({
      StoreAppId: 202,
      Changes: [
        {
          Kind: 23,
          ProductId: 'rozet',
          PurchaseOptionId: 'buy',
          Localizations: [{ Locale: 'tr-TR', Name: 'Rozet', Description: 'Destek rozeti' }],
          Prices: [{ Territory: 'TR', Currency: 'TRY', Amount: '19.99' }],
        },
      ],
    })
  })
})

describe('IapDetailPage', () => {
  it('shows the review screenshot as done and plans a base price without the subscribers choice', async () => {
    moneyState.iap = { ...moneyState.iap, State: ProductState.Approved }
    const { user } = at(
      '/apps/101/money/iap/4101/prices?store=apple',
      '/apps/:appId/money/iap/:iapId/prices',
      <IapDetailPage tab="prices" />,
    )

    await user.click(await screen.findByRole('button', { name: 'Taban fiyatı değiştir' }))
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).queryByText('Mevcut aboneler')).not.toBeInTheDocument()
    expect(within(dialog).queryByText(/eşitlediği fiyatı uygula/)).not.toBeInTheDocument()
    const trigger = within(dialog).getByLabelText('Yeni fiyat')
    await waitFor(() => expect(trigger).toBeEnabled())
    await user.click(trigger)
    await user.click(await screen.findByRole('option', { name: (name) => name.startsWith('1.99 EUR') }))
    await user.click(within(dialog).getByRole('button', { name: 'Planı gözden geçir' }))

    await waitFor(() => expect(lastRequestTo('/Iap/plan')).toBeDefined())
    expect(lastRequestTo('/Iap/price-points')?.query).toMatchObject({ IapId: '4101', Territory: 'TUR' })
    expect(lastRequestTo('/Iap/plan')?.body).toMatchObject({
      Changes: [{ Kind: 8, ProductId: 'dev.kuzeylab.pusula.theme.night', Territory: 'TUR', PreserveCurrentPrice: null }],
    })
  })

  it('shows the general fields of an In-App Purchase', async () => {
    at('/apps/101/money/iap/4101?store=apple', '/apps/:appId/money/iap/:iapId', <IapDetailPage tab="general" />)

    expect(await screen.findByText('tema-gece.png')).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'İnceleme bilgisi' })).toHaveTextContent('Tamam')
    expect(screen.getByRole('link', { name: 'Fiyatlar' })).toHaveAttribute('href', '/apps/101/money/iap/4101/prices?store=apple')
  })

  it('plans a Play purchase option price and state', async () => {
    moneyState.iap = subscriptionDetail({
      Id: 6101,
      StoreAppId: 202,
      Store: StoreKind.Google,
      Kind: StoreProductKind.OneTimeProduct,
      ProductId: 'tema_gece',
      ReferenceName: null,
      State: ProductState.Active,
      Localizations: [{ Locale: 'tr-TR', Name: 'Gece teması', Description: 'Koyu renkler.' }],
      Plans: [
        {
          BasePlanId: 'buy',
          Type: 'Buy',
          State: ProductState.Active,
          BasePrice: { Territory: 'TR', Currency: 'TRY', Amount: '34.99', Source: PriceSource.Manual },
          RegionCount: 1,
        },
      ],
    })
    moneyState.prices = [productPrice({ Territory: 'TR', TerritoryName: null, Currency: 'TRY', CustomerPrice: '34.99' })]
    const { user } = at('/apps/101/money/iap/6101?store=google', '/apps/:appId/money/iap/:iapId', <IapDetailPage tab="general" />)

    expect(await screen.findByRole('table', { name: 'buy fiyatları' })).toHaveTextContent('34.99 TRY')
    await user.click(screen.getByRole('button', { name: 'buy devre dışı bırak' }))
    await waitFor(() => expect(lastRequestTo('/Iap/plan')).toBeDefined())
    expect(lastRequestTo('/Iap/plan')?.body).toMatchObject({
      Changes: [{ Kind: 23, ProductId: 'tema_gece', PurchaseOptionId: 'buy', Active: false }],
    })
    expect(lastRequestTo('/Iap/prices')?.body).toMatchObject({ IapId: 6101, PlanId: 'buy' })
  })

  it('plans the Play listing of a one-time product', async () => {
    moneyState.iap = subscriptionDetail({
      Id: 6101,
      StoreAppId: 202,
      Store: StoreKind.Google,
      Kind: StoreProductKind.OneTimeProduct,
      ProductId: 'tema_gece',
      Localizations: [{ Locale: 'tr-TR', Name: 'Gece teması', Description: 'Koyu renkler.' }],
      Plans: [],
    })
    const { user } = at('/apps/101/money/iap/6101?store=google', '/apps/:appId/money/iap/:iapId', <IapDetailPage tab="general" />)

    const title = await screen.findByLabelText('Başlık')
    await user.clear(title)
    await user.type(title, 'Gece')
    await user.click(screen.getByRole('button', { name: 'Değişiklikleri gözden geçir · 1' }))

    await waitFor(() => expect(lastRequestTo('/Iap/plan')).toBeDefined())
    expect(lastRequestTo('/Iap/plan')?.body).toMatchObject({
      Changes: [{ Kind: 23, ProductId: 'tema_gece', Localizations: [{ Locale: 'tr-TR', Name: 'Gece', Description: 'Koyu renkler.' }] }],
    })
  })
})

describe('BasePlanPricesPage', () => {
  function render() {
    moneyState.subscription = playSubscription()
    moneyState.prices = [
      productPrice({ Territory: 'TR', TerritoryName: null, Currency: 'TRY', CustomerPrice: '54.99', Source: PriceSource.Manual }),
      productPrice({
        Territory: 'US',
        TerritoryName: null,
        Currency: 'USD',
        CustomerPrice: '1.79',
        IsBase: false,
        Source: PriceSource.Converted,
      }),
    ]
    return at(
      '/apps/101/money/subscriptions/5101/base-plans/aylik?store=google',
      '/apps/:appId/money/subscriptions/:subscriptionId/base-plans/:basePlanId',
      <BasePlanPricesPage />,
    )
  }

  it("converts the base price with Play's conversion and plans the changed regions with the migration choice", async () => {
    const { user } = render()

    const amount = await screen.findByLabelText('Tutar')
    await user.clear(amount)
    await user.type(amount, '59,99')
    await user.click(screen.getByRole('button', { name: 'Fiyatları dönüştür' }))
    expect(await screen.findByText(/3 bölge dönüştürüldü, henüz kaydedilmedi/)).toBeInTheDocument()
    expect(lastRequestTo('/Subscriptions/prices/convert')?.body).toMatchObject({ StoreAppId: 202, Currency: 'TRY', Amount: '59.99' })
    await user.clear(screen.getByLabelText('IN yeni fiyatı'))
    await user.type(screen.getByLabelText('IN yeni fiyatı'), '129')
    await user.click(screen.getByRole('button', { name: 'Plana ekle · 3' }))
    expect(screen.getByText('Mevcut abonelere ne olacağını seç.')).toBeInTheDocument()
    await user.click(screen.getByRole('radio', { name: /Mevcut fiyatı koru/ }))
    await user.click(screen.getByRole('button', { name: 'Plana ekle · 3' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      StoreAppId: 202,
      Changes: [
        {
          Kind: 20,
          ProductId: 'pusula_premium',
          BasePlanId: 'aylik',
          Prices: [
            { Territory: 'TR', Currency: 'TRY', Amount: '59.99' },
            { Territory: 'US', Currency: 'USD', Amount: '1.99' },
            { Territory: 'IN', Currency: 'INR', Amount: '129' },
          ],
          OtherRegionsUsd: '1.99',
          OtherRegionsEur: '1.99',
          RegionsVersion: '2022/02',
          PreserveCurrentPrice: true,
        },
      ],
    })
  })

  it('marks the regions Play refused', async () => {
    server.use(
      endpoint('post', '/Subscriptions/plan', () => errorResponse('Prices[IN].Amount', "Play'in bu bölgedeki alt sınırının altında.", 400)),
    )
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Fiyatları dönüştür' }))
    await screen.findByText(/bölge dönüştürüldü/)
    await user.click(screen.getByRole('radio', { name: /Mevcut abonelere de uygula/ }))
    await user.click(screen.getByRole('button', { name: /Plana ekle/ }))

    expect(await screen.findByText('1 bölgede fiyat geçersiz')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Hatalı satırları göster' }))
    const table = screen.getByRole('table', { name: 'Bölge fiyatları' })
    expect(within(table).getAllByRole('row')).toHaveLength(2)
    expect(within(table).getByText('Play bu tutarı kabul etmedi.')).toBeInTheDocument()
  })

  it('says so when the base plan is not in the subscription', async () => {
    moneyState.subscription = { ...playSubscription(), Plans: [] }
    at(
      '/apps/101/money/subscriptions/5101/base-plans/aylik?store=google',
      '/apps/:appId/money/subscriptions/:subscriptionId/base-plans/:basePlanId',
      <BasePlanPricesPage />,
    )

    expect(await screen.findByText('aylik base plan bulunamadı.')).toBeInTheDocument()
  })
})
