import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { SubscriptionsPage } from '@/features/monetization/SubscriptionsPage'
import { organization } from '../../support/factories'
import { moneyState, setupMonetizationApi } from '../../support/monetization'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo } from '../../support/state'
import { testState } from '../../support/state'

setupMonetizationApi()

function render(store = 'apple') {
  return renderWithProviders(<SubscriptionsPage />, {
    route: `/apps/101/money/subscriptions?store=${store}`,
    path: '/apps/:appId/money/subscriptions',
  })
}

describe('SubscriptionsPage on the App Store', () => {
  it('lists a group in level order with prices from the store', async () => {
    render()

    const levels = await screen.findByRole('list', { name: 'Seviyeler' })
    const rows = within(levels).getAllByRole('listitem')
    expect(rows[0]).toHaveTextContent('Seviye 1')
    expect(rows[0]).toHaveTextContent('Pusula Plus Yıllık')
    expect(rows[0]).toHaveTextContent('399.99 TRY')
    expect(rows[0]).toHaveTextContent('mağazadan')
    expect(rows[1]).toHaveTextContent('Pusula Plus Aylık')
    expect(lastRequestTo('/Subscriptions/groups')?.query).toMatchObject({ StoreAppId: '201' })
  })

  it('links a missing review screenshot to the review part of the subscription', async () => {
    render()

    const attention = await screen.findByText('İncelemeden önce gerekli')
    expect(attention.closest('.callout')).toHaveTextContent('"Pusula Plus Aylık" inceleme görüntüsü yok.')
    expect(screen.getByRole('link', { name: 'Görüntüyü yükle' })).toHaveAttribute(
      'href',
      '/apps/101/money/subscriptions/3101?store=apple#review',
    )
  })

  it('plans the new level order only for the subscriptions that moved', async () => {
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Pusula Plus Aylık yukarı taşı' }))
    await user.click(screen.getByRole('button', { name: 'Seviye sırasını gözden geçir · 2' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      StoreAppId: 201,
      DryRun: true,
      Changes: [
        { Kind: 7, ProductId: 'dev.kuzeylab.pusula.plus.monthly', Level: 1 },
        { Kind: 7, ProductId: 'dev.kuzeylab.pusula.plus.yearly', Level: 2 },
      ],
    })
  })

  it('adds a subscription to the group at the next level', async () => {
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Abonelik ekle' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('Ürün kimliği'), 'dev.kuzeylab.pusula.plus.weekly')
    await user.type(within(dialog).getByLabelText('Referans adı'), 'Pusula Plus Haftalık')
    await user.click(within(dialog).getByLabelText('Süre'))
    await user.click(await screen.findByRole('option', { name: '1 hafta' }))
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [
        {
          Kind: 4,
          GroupReferenceName: 'Pusula Plus',
          ProductId: 'dev.kuzeylab.pusula.plus.weekly',
          ReferenceName: 'Pusula Plus Haftalık',
          Duration: 'ONE_WEEK',
          Level: 3,
          FamilySharable: false,
        },
      ],
    })
  })

  it('plans a group localization', async () => {
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Lokalizasyon ekle' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('Dil'), 'en-US')
    await user.type(within(dialog).getByLabelText('Görünen ad'), 'Pusula Plus')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [{ Kind: 3, GroupReferenceName: 'Pusula Plus', Locale: 'en-US', Name: 'Pusula Plus', CustomAppName: null }],
    })
  })

  it('offers to create the first group when the app has none', async () => {
    moneyState.groups = { ...moneyState.groups, Groups: [] }
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Abonelik grubu oluştur' }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))
    expect(within(dialog).getByText('Zorunlu.')).toBeInTheDocument()
    await user.type(within(dialog).getByLabelText('Grup adı'), 'Pusula Pro')
    await user.type(within(dialog).getByLabelText('Görünen ad'), 'Pusula Pro')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [{ Kind: 1, ReferenceName: 'Pusula Pro', Localizations: [{ Locale: 'tr', Name: 'Pusula Pro', CustomAppName: null }] }],
    })
  })

  it('queues a sync of the store products', async () => {
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Mağazadan senkronla' }))

    expect(await screen.findByText('Senkron kuyruğa alındı.')).toBeInTheDocument()
    expect(lastRequestTo('/Subscriptions/sync')?.body).toMatchObject({ StoreAppId: 201 })
  })

  it('shows a viewer the groups without write actions', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    render()

    await screen.findByRole('list', { name: 'Seviyeler' })
    expect(screen.queryByRole('button', { name: 'Abonelik ekle' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Mağazadan senkronla' })).not.toBeInTheDocument()
  })
})

describe('SubscriptionsPage on Google Play', () => {
  it('lists the base plans with their offers', async () => {
    render('google')

    const plans = await screen.findByRole('list', { name: "Base plan'lar" })
    expect(plans).toHaveTextContent('aylik · P1M · Otomatik yenilenen')
    expect(plans).toHaveTextContent('54.99 TRY')
    expect(within(plans).getByRole('link', { name: 'Bölge fiyatları · 2' })).toHaveAttribute(
      'href',
      '/apps/101/money/subscriptions/5101/base-plans/aylik?store=google',
    )
    expect(screen.getByRole('list', { name: 'aylik teklifleri' })).toHaveTextContent('ilk-hafta')
  })

  it('plans deactivating a base plan and activating a draft one', async () => {
    const { user } = render('google')

    await user.click(await screen.findByRole('button', { name: 'aylik devre dışı bırak' }))
    await screen.findByRole('dialog')
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      StoreAppId: 202,
      Changes: [{ Kind: 19, ProductId: 'pusula_premium', BasePlanId: 'aylik', Active: false }],
    })
  })

  it('plans deactivating an offer of a base plan', async () => {
    const { user } = render('google')

    await user.click(await screen.findByRole('button', { name: 'ilk-hafta devre dışı bırak' }))
    await screen.findByRole('dialog')
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [{ Kind: 22, ProductId: 'pusula_premium', BasePlanId: 'aylik', Code: 'ilk-hafta', Active: false }],
    })
  })

  it('plans a new base plan with its first price', async () => {
    const { user } = render('google')

    await user.click(await screen.findByRole('button', { name: 'Base plan ekle' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('Base plan kimliği'), 'yillik')
    await user.type(within(dialog).getByLabelText('Tutar'), '479,99')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [
        {
          Kind: 18,
          ProductId: 'pusula_premium',
          BasePlanId: 'yillik',
          Duration: 'P1M',
          AutoRenewing: true,
          Prices: [{ Territory: 'TR', Currency: 'TRY', Amount: '479.99' }],
        },
      ],
    })
  })

  it('plans a free trial offer for an auto-renewing base plan only', async () => {
    const { user } = render('google')

    await screen.findByRole('button', { name: 'aylik için teklif ekle' })
    expect(screen.queryByRole('button', { name: 'prepaid-1ay için teklif ekle' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'aylik için teklif ekle' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('Teklif kimliği'), 'deneme')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [
        {
          Kind: 21,
          BasePlanId: 'aylik',
          Code: 'deneme',
          Eligibility: ['NewCustomers'],
          Phases: [{ Duration: 'P1W', Recurrence: 1, Free: true, Prices: null }],
        },
      ],
    })
  })

  it('plans a new Play subscription with its listing', async () => {
    const { user } = render('google')

    await user.click(await screen.findByRole('button', { name: 'Abonelik ekle' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('Ürün kimliği'), 'pusula_pro')
    await user.type(within(dialog).getByLabelText('Başlık'), 'Pusula Pro')
    await user.type(within(dialog).getByLabelText(/Avantajlar/), 'Rotalar{enter}Uyarılar')
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      Changes: [
        { Kind: 17, ProductId: 'pusula_pro', Localizations: [{ Locale: 'tr-TR', Name: 'Pusula Pro', Benefits: ['Rotalar', 'Uyarılar'] }] },
      ],
    })
  })
})
