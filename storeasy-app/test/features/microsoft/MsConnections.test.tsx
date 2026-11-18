import { configure, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { AddMsProductDialog } from '@/features/microsoft/AddMsProductDialog'
import { ConnectionsPage } from '@/pages/org/ConnectionsPage'
import { connection } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { daysFromNow, msConnection, msState, setupMsApi } from '../../support/microsoft'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupMsApi()

// These screens chain a dry run, a plan read and a render; under a loaded coverage run that can pass the default 1 s.
configure({ asyncUtilTimeout: 5000 })

type User = ReturnType<typeof renderWithProviders>['user']

// Long ids are pasted, as people do; typing them key by key only slows the suite.
async function paste(user: User, field: HTMLElement, text: string) {
  await user.click(field)
  await user.paste(text)
}

const TENANT = '8d6b2e5a-1111-2222-3333-444455553c07'
const CLIENT = 'f19c4e27-1111-2222-3333-44445555be12'

describe('Microsoft Store connections', () => {
  it('adds an Entra app with its secret expiry after checking the ids', async () => {
    testState.connections = []
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Bağlantı ekle' }))
    await user.click(screen.getByRole('radio', { name: /Microsoft Entra uygulaması/ }))
    await user.click(screen.getByRole('button', { name: 'Devam et' }))
    expect(screen.getByRole('dialog', { name: 'Microsoft Store Entra uygulaması' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Bağlantı adı'), 'Kuzey Lab Partner Center')
    await user.type(screen.getByLabelText('Tenant ID'), 'not-a-guid')
    await user.click(screen.getByRole('button', { name: 'Kaydet ve doğrula' }))
    expect(screen.getByText('Tenant ID bir GUID (8-4-4-4-12).')).toBeInTheDocument()
    expect(screen.getByText('Client secret gir (en çok 512 karakter).')).toBeInTheDocument()
    expect(requestsTo('/StoreConnections/connection')).toHaveLength(0)

    await user.clear(screen.getByLabelText('Tenant ID'))
    await paste(user, screen.getByLabelText('Tenant ID'), TENANT)
    await paste(user, screen.getByLabelText('Client ID'), CLIENT)
    await user.type(screen.getByLabelText('Client secret'), 'abc~secret')
    await user.type(screen.getByLabelText('Satıcı kimliği'), '40291187')
    await user.click(screen.getByRole('button', { name: 'Kaydet ve doğrula' }))

    expect(await screen.findByRole('heading', { name: 'Bağlantı kaydedildi' })).toBeInTheDocument()
    const sent = lastRequestTo('/StoreConnections/connection')?.body as { Store: number; Secret: string; CredentialExpiryDate: string }
    expect(sent.Store).toBe(StoreKind.Microsoft)
    expect(JSON.parse(sent.Secret)).toEqual({ tenantId: TENANT, clientId: CLIENT, clientSecret: 'abc~secret', sellerId: '40291187' })
    expect(sent.CredentialExpiryDate).toMatch(/^\d{4}-\d{2}-\d{2}T00:00:00Z$/)
    const days = (Date.parse(sent.CredentialExpiryDate) - Date.now()) / 86_400_000
    expect(days).toBeGreaterThan(330)
    expect(days).toBeLessThan(370)
  })

  it('asks for an expiry when it is cleared', async () => {
    testState.connections = []
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Bağlantı ekle' }))
    await user.click(screen.getByRole('radio', { name: /Microsoft Entra uygulaması/ }))
    await user.click(screen.getByRole('button', { name: 'Devam et' }))
    await user.click(screen.getByRole('button', { name: 'Temizle' }))
    await user.click(screen.getByRole('button', { name: 'Kaydet ve doğrula' }))

    expect(screen.getByText('Yarından başlayıp en çok 24 ay sonrasına bir gün seç.')).toBeInTheDocument()
  })

  it('shows the Microsoft fingerprint with the secret expiry and an expiring badge and banner', async () => {
    testState.connections = [connection(), msConnection({ CredentialExpiryDate: daysFromNow(22) })]
    msState.expiring = [
      { ConnectionId: 13, OrganizationId: 7, Name: 'Kuzey Lab Partner Center', CredentialExpiryDate: daysFromNow(22), DaysLeft: 22 },
    ]

    renderWithProviders(<ConnectionsPage />)

    const card = await screen.findByRole('article', { name: 'Kuzey Lab Partner Center' })
    expect(within(card).getByText('…3c07')).toBeInTheDocument()
    expect(within(card).getByText('…be12')).toBeInTheDocument()
    expect(within(card).getByText('40291187')).toBeInTheDocument()
    expect(within(card).getByText(/· 22 gün/)).toBeInTheDocument()
    expect(within(card).getByText('22 gün içinde bitiyor')).toBeInTheDocument()
    expect(await screen.findByRole('alert')).toHaveTextContent(
      /Kuzey Lab Partner Center bağlantısının client secret'ı .* bitiyor \(22 gün\)/,
    )
    expect(lastRequestTo('/ms/connections/expiring')?.query).toMatchObject({ Days: '30' })
  })

  it('does not ask for expiring secrets without a Microsoft connection', async () => {
    renderWithProviders(<ConnectionsPage />)

    await screen.findByRole('article', { name: 'Kuzey Lab ASC' })

    expect(requestsTo('/ms/connections/expiring')).toHaveLength(0)
    expect(screen.queryByRole('button', { name: 'Sırrı güncelle' })).not.toBeInTheDocument()
  })

  it('says an expired secret stopped the submissions', async () => {
    testState.connections = [msConnection({ CredentialExpiryDate: daysFromNow(-3) })]
    msState.expiring = [
      { ConnectionId: 13, OrganizationId: 7, Name: 'Kuzey Lab Partner Center', CredentialExpiryDate: daysFromNow(-3), DaysLeft: -3 },
      { ConnectionId: 14, OrganizationId: 7, Name: 'Yedek', CredentialExpiryDate: daysFromNow(4), DaysLeft: 4 },
    ]

    renderWithProviders(<ConnectionsPage />)

    expect(await screen.findByText('2 Microsoft bağlantısı yakında yeni client secret istiyor.')).toBeInTheDocument()
    const card = screen.getByRole('article', { name: 'Kuzey Lab Partner Center' })
    expect(within(card).getByText('Sırrın süresi doldu')).toBeInTheDocument()
    expect(within(card).getByText(/tarihinde bitti/)).toBeInTheDocument()
  })

  it('renews the secret from the banner after checking it belongs to the same app', async () => {
    testState.connections = [msConnection({ CredentialExpiryDate: daysFromNow(10) })]
    msState.expiring = [
      { ConnectionId: 13, OrganizationId: 7, Name: 'Kuzey Lab Partner Center', CredentialExpiryDate: daysFromNow(10), DaysLeft: 10 },
    ]
    const { user } = renderWithProviders(<ConnectionsPage />)

    const banner = await screen.findByRole('alert')
    await user.click(within(banner).getByRole('button', { name: 'Sırrı güncelle' }))
    const dialog = await screen.findByRole('dialog', { name: "Client secret'ı güncelle" })
    expect(within(dialog).getByLabelText('Satıcı kimliği')).toHaveValue('40291187')
    await paste(user, within(dialog).getByLabelText('Tenant ID'), TENANT)
    await paste(user, within(dialog).getByLabelText('Client ID'), 'f19c4e27-1111-2222-3333-444455559999')
    await user.type(within(dialog).getByLabelText('Yeni client secret'), 'new~secret')
    await user.click(within(dialog).getByRole('button', { name: 'Doğrula ve değiştir' }))
    expect(within(dialog).getByText(/Tenant ve client bu bağlantınınki olmalı/)).toBeInTheDocument()

    await user.clear(within(dialog).getByLabelText('Client ID'))
    await paste(user, within(dialog).getByLabelText('Client ID'), CLIENT)
    await user.click(within(dialog).getByRole('button', { name: 'Doğrula ve değiştir' }))

    expect(await screen.findByText('Client secret değiştirildi.')).toBeInTheDocument()
    const sent = lastRequestTo('/ms/connection/secret')?.body as { ConnectionId: number; Secret: string; CredentialExpiryDate: string }
    expect(sent.ConnectionId).toBe(13)
    expect(JSON.parse(sent.Secret)).toMatchObject({ clientSecret: 'new~secret', sellerId: '40291187' })
  })

  it('keeps the old secret and shows why when Microsoft refuses the new one', async () => {
    testState.connections = [msConnection()]
    server.use(endpoint('put', '/ms/connection/secret', () => errorResponse('MsSecretRejected', 'Yeni sırla token alınamadı.', 400)))
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Sırrı güncelle' }))
    const dialog = await screen.findByRole('dialog', { name: "Client secret'ı güncelle" })
    await paste(user, within(dialog).getByLabelText('Tenant ID'), TENANT)
    await paste(user, within(dialog).getByLabelText('Client ID'), CLIENT)
    await user.type(within(dialog).getByLabelText('Yeni client secret'), 'bad')
    await user.click(within(dialog).getByRole('button', { name: 'Doğrula ve değiştir' }))

    expect(await within(dialog).findByText('Yeni sırla token alınamadı.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Yeni client secret')).toHaveValue('')
  })
})

describe('Adding an MSI/EXE product', () => {
  it('checks the Store id and adds the product with the Microsoft connection', async () => {
    testState.connections = [connection(), msConnection()]
    const { user } = renderWithProviders(<AddMsProductDialog open onClose={() => undefined} />)

    const input = await screen.findByLabelText('Store kimliği')
    await waitFor(() => expect(screen.getByRole('button', { name: 'Microsoft bağlantısı' })).toHaveTextContent('Kuzey Lab Partner Center'))
    await user.type(input, 'short')
    await user.click(screen.getByRole('button', { name: 'Ürünü ekle' }))
    expect(screen.getByText('Store kimliği 12 harf ve rakamdır.')).toBeInTheDocument()
    await user.clear(input)
    await user.type(input, '9nblggh4r315')
    await user.type(screen.getByLabelText(/Görünen ad/), 'Tarla')
    await user.click(screen.getByRole('button', { name: 'Ürünü ekle' }))

    expect(await screen.findByText('9NBLGGH4R315 eklendi; senkron doğruluyor.')).toBeInTheDocument()
    expect(lastRequestTo('/ms/product')?.body).toMatchObject({ ConnectionId: 13, ProductId: '9NBLGGH4R315', Name: 'Tarla' })
  })

  it('says the product is already there on a 409', async () => {
    testState.connections = [msConnection()]
    server.use(endpoint('post', '/ms/product', () => errorResponse('StoreAppExists', 'Var.', 409)))
    const { user } = renderWithProviders(<AddMsProductDialog open onClose={() => undefined} />)

    await waitFor(() => expect(screen.getByRole('button', { name: 'Microsoft bağlantısı' })).toHaveTextContent('Kuzey Lab Partner Center'))
    await user.type(screen.getByLabelText('Store kimliği'), '9NBLGGH4R315')
    await user.click(screen.getByRole('button', { name: 'Ürünü ekle' }))

    expect(await screen.findByText('Bu ürün zaten eklenmiş.')).toBeInTheDocument()
  })

  it('leads to the connections without a Microsoft connection', async () => {
    renderWithProviders(<AddMsProductDialog open onClose={() => undefined} />)

    expect(await screen.findByText(/Önce bir Microsoft Store bağlantısı ekle/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Mağaza bağlantıları' })).toHaveAttribute('href', '/connections')
    expect(screen.getByRole('button', { name: 'Ürünü ekle' })).toBeDisabled()
  })
})
