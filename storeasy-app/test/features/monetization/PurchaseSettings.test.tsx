import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole, StoreKind } from '@/lib/api/types'
import { ReadinessStatus } from '@/features/common/types'
import { durationLabel, priceText, productStateView, renewalRateLabel } from '@/features/monetization/labels'
import { moneyTr } from '@/features/monetization/i18n'
import { SubscriptionDetailPage } from '@/features/monetization/ProductPages'
import { PurchaseSettingsPage } from '@/features/monetization/PurchaseSettingsPage'
import { ProductState } from '@/features/monetization/types'
import { contentState, readinessReport } from '../../support/content'
import { organization } from '../../support/factories'
import { moneyState, setupMonetizationApi, subscriptionDetail } from '../../support/monetization'
import { renderApp, renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupMonetizationApi()

function render(store = 'apple') {
  return renderWithProviders(<PurchaseSettingsPage />, {
    route: `/apps/101/money/settings?store=${store}`,
    path: '/apps/:appId/money/settings',
  })
}

describe('PurchaseSettingsPage', () => {
  it('plans the production and sandbox notification addresses', async () => {
    const { user } = render()

    const production = await screen.findByLabelText('Production adresi')
    expect(production).toHaveValue('https://api.kuzeylab.dev/apple/notifications')
    await user.type(screen.getByLabelText('Sandbox adresi'), 'http://sandbox.kuzeylab.dev')
    expect(screen.getByText('https adres gerekir.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Planı gözden geçir' })).toBeDisabled()
    await user.clear(screen.getByLabelText('Sandbox adresi'))
    await user.type(screen.getByLabelText('Sandbox adresi'), 'https://sandbox.kuzeylab.dev/apple')
    await user.click(screen.getByRole('button', { name: 'Planı gözden geçir' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/PurchaseSettings/server-notifications/plan')?.body).toMatchObject({
      StoreAppId: 201,
      Url: null,
      SandboxUrl: 'https://sandbox.kuzeylab.dev/apple',
      Version: 'V2',
      DryRun: true,
    })
  })

  it('leaves the addresses to owners and admins', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    render()

    expect(await screen.findByLabelText('Production adresi')).toBeDisabled()
    expect(screen.getByText('Bildirim adreslerini Sahip ve Yöneticiler değiştirir (webhook gibi).')).toBeInTheDocument()
  })

  it('plans a sandbox tester change', async () => {
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'sandbox.deniz@kuzeylab.dev düzenle' }))
    const dialog = screen.getByRole('dialog')
    await user.clear(within(dialog).getByLabelText('Mağaza bölgesi'))
    await user.type(within(dialog).getByLabelText('Mağaza bölgesi'), 'usa')
    await user.click(within(dialog).getByRole('switch', { name: 'Satın almaları kes' }))
    await user.click(within(dialog).getByLabelText('Yenileme hızı'))
    await user.click(await screen.findByRole('option', { name: '5 dakikada bir' }))
    await user.click(within(dialog).getByRole('button', { name: 'Plana ekle' }))

    await waitFor(() => expect(lastRequestTo('/PurchaseSettings/sandbox-testers/plan')).toBeDefined())
    expect(lastRequestTo('/PurchaseSettings/sandbox-testers/plan')?.body).toMatchObject({
      Changes: [
        { Kind: 25, TesterId: 'st-1', Territory: 'USA', InterruptPurchases: true, RenewalRate: 'MONTHLY_RENEWAL_EVERY_FIVE_MINUTES' },
      ],
    })
  })

  it('asks before planning to clear the purchase history', async () => {
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'sandbox.deniz@kuzeylab.dev satın alma geçmişini temizle' }))
    const confirm = await screen.findByRole('alertdialog')
    await user.click(within(confirm).getByRole('button', { name: 'Geçmişi temizle' }))

    await waitFor(() => expect(lastRequestTo('/PurchaseSettings/sandbox-testers/plan')).toBeDefined())
    expect(lastRequestTo('/PurchaseSettings/sandbox-testers/plan')?.body).toMatchObject({
      Changes: [{ Kind: 25, TesterId: 'st-1', ClearPurchaseHistory: true }],
    })
  })

  it('sends Play notifications to their assisted step', async () => {
    render('google')

    expect(await screen.findByRole('link', { name: 'Asistanlı adımı aç' })).toHaveAttribute('href', '/apps/101/assisted/5530?store=google')
    expect(screen.queryByRole('region', { name: 'Sandbox test kullanıcıları' })).not.toBeInTheDocument()
  })
})

describe('Play subscription detail', () => {
  it('plans the listing and shows the base plans', async () => {
    moneyState.subscription = subscriptionDetail({
      Id: 5101,
      StoreAppId: 202,
      Store: StoreKind.Google,
      ProductId: 'pusula_premium',
      State: ProductState.Active,
      Localizations: [{ Locale: 'tr-TR', Name: 'Pusula Premium', Benefits: ['Çevrimdışı haritalar'] }],
      Plans: moneyState.playGroups.PlaySubscriptions[0].BasePlans,
    })
    const { user } = renderWithProviders(<SubscriptionDetailPage tab="general" />, {
      route: '/apps/101/money/subscriptions/5101?store=google',
      path: '/apps/:appId/money/subscriptions/:subscriptionId',
    })

    expect(await screen.findByRole('list', { name: "Base plan'lar" })).toHaveTextContent('aylik')
    await user.type(screen.getByLabelText(/Avantajlar/), '{enter}Rotalar')
    await user.click(screen.getByRole('button', { name: 'Değişiklikleri gözden geçir · 1' }))

    await waitFor(() => expect(lastRequestTo('/Subscriptions/plan')).toBeDefined())
    expect(lastRequestTo('/Subscriptions/plan')?.body).toMatchObject({
      StoreAppId: 202,
      Changes: [
        { Kind: 17, ProductId: 'pusula_premium', Localizations: [{ Locale: 'tr-TR', Benefits: ['Çevrimdışı haritalar', 'Rotalar'] }] },
      ],
    })
  })
})

describe('phase 7 routes', () => {
  it('adds the money section to the app tabs with its parts', async () => {
    await renderApp({ route: '/apps/101/money/subscriptions?store=apple' })

    expect(await screen.findByRole('list', { name: 'Seviyeler' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Para', current: 'page' })).toHaveAttribute('href', '/apps/101/money/subscriptions?store=apple')
    expect(screen.getByRole('link', { name: 'Uygulama içi ürünler' })).toHaveAttribute('href', '/apps/101/money/iap?store=apple')
    expect(screen.getByRole('link', { name: 'Satın alma ayarları' })).toHaveAttribute('href', '/apps/101/money/settings?store=apple')
  })

  it('links a monetization readiness item into the subscriptions screen', async () => {
    contentState.readiness = readinessReport({
      Items: [
        {
          Key: 'monetization.review-screenshot',
          Group: 'Monetization',
          Status: ReadinessStatus.Missing,
          Title: 'İnceleme görüntüsü',
          Message: 'dev.kuzeylab.pusula.plus.monthly için inceleme görüntüsü yok.',
          RuleId: 'apple.iap.review-screenshot',
          FixRoute: 'money/subscriptions',
        },
      ],
    })
    await renderApp({ route: '/apps/101/readiness?store=apple' })

    const item = (await screen.findByText('dev.kuzeylab.pusula.plus.monthly için inceleme görüntüsü yok.')).closest('li') as HTMLElement
    const link = within(item)
      .getAllByRole('link')
      .find((element) => element.getAttribute('href')?.includes('/money/'))
    expect(link).toHaveAttribute('href', '/apps/101/money/subscriptions?store=apple')
  })
})

describe('monetization labels', () => {
  it('names states, durations, prices and renewal rates', () => {
    expect(Object.values(ProductState).map((state) => productStateView(state, moneyTr).label)).toEqual([
      'Taslak',
      'Eksik bilgi',
      'Gönderime hazır',
      'İnceleme bekliyor',
      'İncelemede',
      'İşlem gerekli',
      'Onaylandı',
      'Etkin',
      'Etkin değil',
      'Reddedildi',
      'Kaldırıldı',
    ])
    expect(durationLabel('ONE_YEAR', moneyTr)).toBe('1 yıl')
    expect(durationLabel('P3M', moneyTr)).toBe('3 ay')
    expect(durationLabel('P1Y2M', moneyTr)).toBe('P1Y2M')
    expect(durationLabel(null, moneyTr)).toBe('-')
    expect(priceText('49.99', 'TRY')).toBe('49.99 TRY')
    expect(priceText(null, 'TRY')).toBe('-')
    expect(renewalRateLabel('NEW_RATE', moneyTr)).toBe('NEW_RATE')
  })
})
