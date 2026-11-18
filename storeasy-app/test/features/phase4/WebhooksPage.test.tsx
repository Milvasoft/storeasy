import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { WebhooksPage } from '@/features/webhooks/WebhooksPage'
import { WebhookRegistrationState, WebhookSource } from '@/features/webhooks/types'
import { organization } from '../../support/factories'
import { phase4State, setupPhase4Api, webhookEndpoint } from '../../support/phase4'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupPhase4Api()

function renderPage(route = '/webhooks') {
  return renderWithProviders(<WebhooksPage />, { route, path: '/webhooks' })
}

describe('WebhooksPage: incoming', () => {
  it('shows the endpoint with its URL and registration, and the event log', async () => {
    renderPage()

    expect(await screen.findByText('Kuzey Lab ASC')).toBeInTheDocument()
    expect(screen.getByText("Apple'da kayıtlı · API")).toBeInTheDocument()
    expect(screen.getByText('https://storeasy.example.test/api/webhooks/apple/whe_3k9x2m7q')).toBeInTheDocument()
    expect(await screen.findByText('IN_REVIEW → REJECTED')).toBeInTheDocument()
    expect(screen.getByText('İmza geçersiz')).toBeInTheDocument()
  })

  it('creates a signed endpoint and shows its secret once', async () => {
    const { user } = renderPage()

    await user.click(await screen.findByRole('button', { name: 'Uç ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Webhook ucu ekle' })
    await user.click(within(dialog).getByRole('radio', { name: /İmzalı webhook/ }))
    await user.type(within(dialog).getByLabelText('Ad'), 'CI')
    await user.click(within(dialog).getByRole('button', { name: 'Uç oluştur' }))

    const secret = await screen.findByRole('dialog', { name: 'Uç hazır' })
    expect(within(secret).getByText('whsec_asc_Vb7Kq2Xm9Lp4Rt8N')).toBeInTheDocument()
    expect(lastRequestTo('/Webhooks/endpoint')?.body).toMatchObject({
      Source: WebhookSource.Signed,
      Name: 'CI',
      RegisterAtStore: false,
      StoreConnectionId: null,
    })
  })

  it('creates an Apple endpoint for a connection', async () => {
    const { user } = renderPage()

    await user.click(await screen.findByRole('button', { name: 'Uç ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Webhook ucu ekle' })
    await user.click(within(dialog).getByRole('button', { name: /Bağlantı/ }))
    await user.click(await screen.findByRole('option', { name: testState.connections[0].Name }))
    await user.click(within(dialog).getByRole('button', { name: 'Uç oluştur' }))

    await screen.findByRole('dialog', { name: 'Uç hazır' })
    expect(lastRequestTo('/Webhooks/endpoint')?.body).toMatchObject({
      Source: WebhookSource.AppStoreConnect,
      StoreConnectionId: testState.connections[0].Id,
      RegisterAtStore: true,
    })
  })

  it('rotates a secret and shows the grace period', async () => {
    const { user } = renderPage()

    await user.click(await screen.findByRole('button', { name: 'Anahtarı yenile' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Anahtarı yenile' }))

    const secret = await screen.findByRole('dialog', { name: 'Yeni gizli anahtar hazır' })
    expect(within(secret).getByText('whsec_rotated_123')).toBeInTheDocument()
    expect(within(secret).getByText(/Eski anahtar .* kabul edilir\./)).toBeInTheDocument()
  })

  it('deletes an endpoint after a confirmation', async () => {
    const { user } = renderPage()

    await user.click(await screen.findByRole('button', { name: 'Sil' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Sil' }))

    expect(await screen.findByText('Uç silindi.')).toBeInTheDocument()
    expect(lastRequestTo('/Webhooks/endpoint')?.body).toMatchObject({ EndpointId: 31 })
  })

  it('opens an event, links its record and processes it again', async () => {
    const { user } = renderPage()

    await user.click(await screen.findByText('IN_REVIEW → REJECTED'))
    const drawer = await screen.findByRole('dialog', { name: 'IN_REVIEW → REJECTED' })
    expect(await within(drawer).findByText('hmacsha256=4f9a••••c21e')).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'Kaydı aç' })).toHaveAttribute('href', '/apps/101/rejections/601')
    await user.click(within(drawer).getByRole('button', { name: 'Yeniden işle' }))

    expect(await screen.findByText('Olay yeniden işlenmek üzere kuyruğa alındı.')).toBeInTheDocument()
    expect(lastRequestTo('/Webhooks/event/reprocess')?.body).toMatchObject({ EventId: 7001 })
  })

  it('does not process an event with an invalid signature again', async () => {
    renderPage('/webhooks?event=7002')

    expect(await screen.findByText('İmza eşleşmiyor')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yeniden işle' })).not.toBeInTheDocument()
  })

  it('shows the manual registration steps when Apple refused the registration', async () => {
    phase4State.endpoints = [webhookEndpoint({ Registration: WebhookRegistrationState.Failed, RegistrationError: '403 FORBIDDEN' })]

    renderPage()

    expect(await screen.findByText('403 FORBIDDEN')).toBeInTheDocument()
    expect(screen.getByText('Kayıt başarısız')).toBeInTheDocument()
  })

  it('is closed to roles without ManageWebhooks', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]

    renderPage()

    expect(await screen.findByText("Webhook'ları sahipler ve yöneticiler yönetir.")).toBeInTheDocument()
  })
})

describe('WebhooksPage: outgoing', () => {
  it('lists subscriptions and their deliveries', async () => {
    renderPage('/webhooks?tab=outgoing')

    expect((await screen.findAllByText('Slack köprüsü')).length).toBeGreaterThan(0)
    expect(await screen.findByText('HTTP 500 · 120 ms')).toBeInTheDocument()
    expect(screen.getByText('2 / 6')).toBeInTheDocument()
  })

  it('adds a subscription for chosen events and shows its secret', async () => {
    const { user } = renderPage('/webhooks?tab=outgoing')

    await user.click(await screen.findByRole('button', { name: 'Abonelik ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Yeni abonelik' })
    await user.type(within(dialog).getByLabelText('Ad'), 'CI')
    await user.type(within(dialog).getByLabelText('URL'), 'http://ci.example.test')
    expect(within(dialog).getByText('Adres https:// ile başlamalı.')).toBeInTheDocument()
    await user.clear(within(dialog).getByLabelText('URL'))
    await user.type(within(dialog).getByLabelText('URL'), 'https://ci.example.test/hook')
    await user.click(within(dialog).getByRole('checkbox', { name: 'review.received' }))
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('whsec_sub_456')).toBeInTheDocument()
    expect(lastRequestTo('/Webhooks/subscription')?.body).toMatchObject({
      Name: 'CI',
      Url: 'https://ci.example.test/hook',
      Events: ['review.received'],
    })
  })

  it('pauses, tests, edits and deletes a subscription', async () => {
    const { user } = renderPage('/webhooks?tab=outgoing')

    await user.click(await screen.findByRole('switch', { name: 'Slack köprüsü etkin' }))
    await expect.poll(() => lastRequestTo('/Webhooks/subscription')?.body).toMatchObject({ WebhookId: 51, IsActive: false })
    await user.click(screen.getByRole('button', { name: 'Slack köprüsü için deneme gönder' }))
    expect(await screen.findByText('Deneme teslimi: Teslim edildi')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Slack köprüsü düzenle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Aboneliği düzenle' })
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))
    expect(await screen.findByText('Abonelik kaydedildi.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Slack köprüsü sil' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Sil' }))

    expect(await screen.findByText('Abonelik silindi.')).toBeInTheDocument()
  })

  it('rotates a subscription secret and filters the delivery log', async () => {
    const { user } = renderPage('/webhooks?tab=outgoing')

    await user.click(await screen.findByRole('button', { name: 'Slack köprüsü anahtarını yenile' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Anahtarı yenile' }))
    expect(await screen.findByText('whsec_sub_rotated')).toBeInTheDocument()
    await user.keyboard('{Escape}')
    await user.click(screen.getByRole('button', { name: 'Abonelik' }))
    await user.click(await screen.findByRole('option', { name: 'Slack köprüsü' }))

    await expect.poll(() => lastRequestTo('/Webhooks/deliveries')?.body).toMatchObject({ WebhookId: 51 })
  })

  it('switches between the tabs', async () => {
    const { user } = renderPage()

    await user.click(await screen.findByRole('tab', { name: 'Giden' }))

    expect(screen.getByTestId('location')).toHaveTextContent('tab=outgoing')
    expect((await screen.findAllByText('Slack köprüsü')).length).toBeGreaterThan(0)
  })
})
