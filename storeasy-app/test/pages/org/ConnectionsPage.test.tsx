import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ConnectionStatus, OrgRole, StoreKind } from '@/lib/api/types'
import { ConnectionsPage } from '@/pages/org/ConnectionsPage'
import { connection, organization, SAMPLE_ISSUER, SAMPLE_P8, SAMPLE_SERVICE_ACCOUNT, TEST_ORG_ID } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

describe('ConnectionsPage', () => {
  it('lists the connections with their fingerprint only', async () => {
    testState.connections = [
      connection(),
      connection({
        Id: 12,
        Store: StoreKind.Google,
        Name: 'Play servis hesabı',
        Fingerprint: { ServiceAccountEmail: 'sa@p.iam.gserviceaccount.com', ProjectId: 'kuzeylab-play' },
        Status: ConnectionStatus.Rejected,
        LastVerifyError: 'Anahtar tanınmadı.',
      }),
    ]

    renderWithProviders(<ConnectionsPage />)

    const apple = await screen.findByRole('article', { name: 'Kuzey Lab ASC' })
    expect(within(apple).getByText('7QX2M4K9PL')).toBeInTheDocument()
    expect(within(apple).getByText('…a91f')).toBeInTheDocument()
    expect(within(apple).getByText('Doğrulandı')).toBeInTheDocument()
    const google = screen.getByRole('article', { name: 'Play servis hesabı' })
    expect(within(google).getByText('Reddedildi')).toBeInTheDocument()
    expect(within(google).getByText('Anahtar tanınmadı.')).toBeInTheDocument()
    expect(lastRequestTo('/StoreConnections')?.query).toEqual({ OrganizationId: String(TEST_ORG_ID) })
  })

  it('shows the first-use state with what each store needs', async () => {
    testState.connections = []

    renderWithProviders(<ConnectionsPage />)

    expect(await screen.findByRole('heading', { name: 'Henüz bağlantı yok' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Bağlantı ekle' })).toBeInTheDocument()
  })

  it('is closed to roles without ManageConnections and does not ask the API', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]

    renderWithProviders(<ConnectionsPage />)

    expect(await screen.findByRole('heading', { name: 'Yetkin yok' })).toBeInTheDocument()
    expect(requestsTo('/StoreConnections')).toHaveLength(0)
  })

  it('verifies a connection and updates the card in place', async () => {
    testState.connections = [connection({ Status: ConnectionStatus.NotVerified, LastVerifiedDate: null })]
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Doğrula' }))

    expect(await screen.findByText('Bağlantı doğrulandı')).toBeInTheDocument()
    expect(lastRequestTo('/StoreConnections/connection/verify')?.body).toEqual({
      OrganizationId: TEST_ORG_ID,
      ConnectionId: 11,
      PackageName: null,
    })
    expect(within(screen.getByRole('article', { name: 'Kuzey Lab ASC' })).getByText('Doğrulandı')).toBeInTheDocument()
  })

  it('shows the API message as a toast when verification is throttled', async () => {
    server.use(
      endpoint('post', '/StoreConnections/connection/verify', () =>
        errorResponse('TooManyRequests', 'Çok sık doğrulama; birkaç dakika sonra dene.', 429),
      ),
    )
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Doğrula' }))

    expect(await screen.findByText('Çok sık doğrulama; birkaç dakika sonra dene.')).toBeInTheDocument()
  })

  it('deletes only after the name is typed exactly', async () => {
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Sil' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Kuzey Lab ASC silinsin mi?' })
    const confirmButton = within(dialog).getByRole('button', { name: 'Sil' })
    expect(confirmButton).toBeDisabled()
    await user.type(within(dialog).getByLabelText('Onaylamak için Kuzey Lab ASC yaz'), 'Kuzey Lab ASC')
    await user.click(confirmButton)

    await waitFor(() => expect(lastRequestTo('/StoreConnections/connection')?.method).toBe('DELETE'))
    expect(lastRequestTo('/StoreConnections/connection')?.body).toEqual({ OrganizationId: TEST_ORG_ID, ConnectionId: 11 })
  })

  it('adds an Apple key from a pasted .p8, builds the secret JSON and verifies it right away', async () => {
    testState.connections = []
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Bağlantı ekle' }))
    await user.click(screen.getByRole('button', { name: 'Devam et' }))
    await user.type(screen.getByLabelText('Bağlantı adı'), 'Kuzey Lab ASC')
    await user.type(screen.getByLabelText('Issuer ID'), SAMPLE_ISSUER)
    await user.type(screen.getByLabelText('Key ID'), '7qx2m4k9pl')
    await user.click(screen.getByRole('radio', { name: 'Yapıştır' }))
    await user.click(screen.getByLabelText('.p8 anahtarı'))
    await user.paste(SAMPLE_P8)
    await user.click(screen.getByRole('button', { name: 'Kaydet ve doğrula' }))

    expect(await screen.findByRole('heading', { name: 'Bağlantı kaydedildi' })).toBeInTheDocument()
    const sent = lastRequestTo('/StoreConnections/connection')?.body as { Store: number; Name: string; Secret: string }
    expect(sent.Store).toBe(StoreKind.Apple)
    expect(JSON.parse(sent.Secret)).toEqual({ issuerId: SAMPLE_ISSUER, keyId: '7QX2M4K9PL', privateKey: SAMPLE_P8 })
    // The store's own logo is the suggested icon.
    expect(lastRequestTo('/StoreConnections/connection')?.body).toMatchObject({ Icon: 'appstore' })
    expect(lastRequestTo('/StoreConnections/connection/verify')?.body).toMatchObject({ ConnectionId: 55 })
  })

  it('refuses a broken .p8 before sending it', async () => {
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Bağlantı ekle' }))
    await user.click(screen.getByRole('button', { name: 'Devam et' }))
    await user.type(screen.getByLabelText('Bağlantı adı'), 'X')
    await user.type(screen.getByLabelText('Issuer ID'), SAMPLE_ISSUER)
    await user.type(screen.getByLabelText('Key ID'), '7QX2M4K9PL')
    await user.click(screen.getByRole('radio', { name: 'Yapıştır' }))
    await user.click(screen.getByLabelText('.p8 anahtarı'))
    await user.paste('hello')
    await user.click(screen.getByRole('button', { name: 'Kaydet ve doğrula' }))

    expect(screen.getByText('Bu bir App Store Connect özel anahtarı değil (PEM "BEGIN PRIVATE KEY").')).toBeInTheDocument()
    expect(requestsTo('/StoreConnections/connection')).toHaveLength(0)
  })

  it('reads a Google service account file and shows its e-mail and project before saving', async () => {
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Bağlantı ekle' }))
    await user.click(screen.getByRole('radio', { name: /Google Play/ }))
    await user.click(screen.getByRole('button', { name: 'Devam et' }))
    await user.type(screen.getByLabelText('Bağlantı adı'), 'Play')
    await user.upload(
      screen.getByLabelText("Servis hesabı JSON'u"),
      new File([SAMPLE_SERVICE_ACCOUNT], 'play.json', { type: 'application/json' }),
    )

    expect(await screen.findByText('storeasy@kuzeylab-play.iam.gserviceaccount.com')).toBeInTheDocument()
    expect(screen.getByText('kuzeylab-play')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Kaydet ve doğrula' }))
    await screen.findByRole('heading', { name: 'Bağlantı kaydedildi' })
    expect(lastRequestTo('/StoreConnections/connection')?.body).toMatchObject({
      Store: StoreKind.Google,
      Name: 'Play',
      Secret: SAMPLE_SERVICE_ACCOUNT,
      Icon: 'googleplay',
    })
  })

  it('keeps the form and asks for the key again when the API refuses it', async () => {
    server.use(endpoint('post', '/StoreConnections/connection', () => errorResponse('AppleKeyInvalid', 'Anahtar P-256 değil.', 400)))
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Bağlantı ekle' }))
    await user.click(screen.getByRole('button', { name: 'Devam et' }))
    await user.type(screen.getByLabelText('Bağlantı adı'), 'Kuzey')
    await user.type(screen.getByLabelText('Issuer ID'), SAMPLE_ISSUER)
    await user.type(screen.getByLabelText('Key ID'), '7QX2M4K9PL')
    await user.click(screen.getByRole('radio', { name: 'Yapıştır' }))
    await user.click(screen.getByLabelText('.p8 anahtarı'))
    await user.paste(SAMPLE_P8)
    await user.click(screen.getByRole('button', { name: 'Kaydet ve doğrula' }))

    expect(await screen.findByText('Anahtar P-256 değil.')).toBeInTheDocument()
    expect(screen.getByLabelText('Bağlantı adı')).toHaveValue('Kuzey')
    expect(screen.getByLabelText('.p8 anahtarı')).toHaveValue('')
  })

  it('offers only the own store logo plus the generic icons, and saves without an icon when none is picked', async () => {
    testState.connections = []
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Bağlantı ekle' }))
    await user.click(screen.getByRole('radio', { name: /Google Play/ }))
    await user.click(screen.getByRole('button', { name: 'Devam et' }))
    const picker = screen.getByRole('group', { name: /Simge/ })
    expect(within(picker).getByRole('radio', { name: 'Google Play' })).toBeChecked()
    expect(within(picker).queryByRole('radio', { name: 'App Store' })).not.toBeInTheDocument()
    expect(within(picker).queryByRole('radio', { name: 'Microsoft Store' })).not.toBeInTheDocument()
    expect(within(picker).getAllByRole('radio')).toHaveLength(10)

    await user.click(within(picker).getByRole('radio', { name: 'Yok (Metin kodu)' }))
    await user.type(screen.getByLabelText('Bağlantı adı'), 'Play')
    await user.upload(
      screen.getByLabelText("Servis hesabı JSON'u"),
      new File([SAMPLE_SERVICE_ACCOUNT], 'play.json', { type: 'application/json' }),
    )
    await user.click(screen.getByRole('button', { name: 'Kaydet ve doğrula' }))

    await screen.findByRole('heading', { name: 'Bağlantı kaydedildi' })
    expect(lastRequestTo('/StoreConnections/connection')?.body).toMatchObject({ Store: StoreKind.Google, Icon: null })
  })

  it('edits the name and the icon of a connection and shows the new icon on its card', async () => {
    testState.connections = [connection({ Icon: null })]
    const { user } = renderWithProviders(<ConnectionsPage />)

    const card = await screen.findByRole('article', { name: 'Kuzey Lab ASC' })
    expect(card.querySelector('.st b')).toHaveTextContent('AS')
    await user.click(within(card).getByRole('button', { name: 'Düzenle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Kuzey Lab ASC bağlantısını düzenle' })
    expect(within(dialog).getByRole('radio', { name: 'Yok (Metin kodu)' })).toBeChecked()
    await user.clear(within(dialog).getByLabelText('Bağlantı adı'))
    await user.type(within(dialog).getByLabelText('Bağlantı adı'), 'Yayın anahtarı')
    await user.click(within(dialog).getByRole('radio', { name: 'Roket' }))
    testState.connections = [connection({ Name: 'Yayın anahtarı', Icon: 'rocket' })]
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Bağlantı güncellendi')).toBeInTheDocument()
    expect(lastRequestTo('/StoreConnections/connection')?.method).toBe('PUT')
    expect(lastRequestTo('/StoreConnections/connection')?.body).toEqual({
      OrganizationId: TEST_ORG_ID,
      ConnectionId: 11,
      Name: 'Yayın anahtarı',
      Icon: 'rocket',
    })
    const updated = await screen.findByRole('article', { name: 'Yayın anahtarı' })
    await waitFor(() => expect(updated.querySelector('.sti')).toHaveAttribute('data-icon', 'rocket'))
  })

  it('replaces the key of an Apple connection in place, with the Key ID read from the file name', async () => {
    const { user } = renderWithProviders(<ConnectionsPage />)

    const card = await screen.findByRole('article', { name: 'Kuzey Lab ASC' })
    await user.click(within(card).getByRole('button', { name: 'Anahtarı değiştir' }))
    const dialog = await screen.findByRole('dialog', { name: 'Kuzey Lab ASC anahtarını değiştir' })
    expect(within(dialog).getByText('Bu bağlantının Issuer ID değeri …a91f ile biter.')).toBeInTheDocument()
    await user.type(within(dialog).getByLabelText('Issuer ID'), SAMPLE_ISSUER)
    await user.upload(within(dialog).getByLabelText('.p8 anahtarı'), new File([SAMPLE_P8], 'AuthKey_ADM1N2KEY3.p8'))
    expect(await within(dialog).findByText('AuthKey_ADM1N2KEY3.p8')).toBeInTheDocument()
    testState.connections = [connection({ Fingerprint: { KeyId: 'ADM1N2KEY3', IssuerIdSuffix: 'a91f' } })]
    await user.click(within(dialog).getByRole('button', { name: 'Doğrula ve değiştir' }))

    expect(await screen.findByText('Anahtar değiştirildi')).toBeInTheDocument()
    const sent = lastRequestTo('/StoreConnections/connection/key')?.body as { OrganizationId: number; ConnectionId: number; Secret: string }
    expect(lastRequestTo('/StoreConnections/connection/key')?.method).toBe('PUT')
    expect(sent.OrganizationId).toBe(TEST_ORG_ID)
    expect(sent.ConnectionId).toBe(11)
    expect(JSON.parse(sent.Secret)).toEqual({ issuerId: SAMPLE_ISSUER, keyId: 'ADM1N2KEY3', privateKey: SAMPLE_P8 })
    await waitFor(() => expect(within(screen.getByRole('article', { name: 'Kuzey Lab ASC' })).getByText('ADM1N2KEY3')).toBeInTheDocument())
    expect(screen.queryByRole('dialog', { name: 'Kuzey Lab ASC anahtarını değiştir' })).not.toBeInTheDocument()
  })

  it('refuses an Apple key of another team before sending it', async () => {
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Anahtarı değiştir' }))
    const dialog = await screen.findByRole('dialog', { name: 'Kuzey Lab ASC anahtarını değiştir' })
    await user.type(within(dialog).getByLabelText('Issuer ID'), '69a6de7e-1234-4abc-9def-0123456a0000')
    await user.type(within(dialog).getByLabelText('Key ID'), 'ADM1N2KEY3')
    await user.click(within(dialog).getByRole('radio', { name: 'Yapıştır' }))
    await user.click(within(dialog).getByLabelText('.p8 anahtarı'))
    await user.paste(SAMPLE_P8)
    await user.click(within(dialog).getByRole('button', { name: 'Doğrula ve değiştir' }))

    expect(
      within(dialog).getByText('Bu bağlantı Issuer ID değeri …a91f ile biten ekibe ait; başka bir ekibin anahtarı kullanılamaz.'),
    ).toBeInTheDocument()
    expect(requestsTo('/StoreConnections/connection/key')).toHaveLength(0)
  })

  it('shows the store refusal inline, keeps the dialog open and asks for the key again', async () => {
    server.use(
      endpoint('put', '/StoreConnections/connection/key', () =>
        errorResponse('StoreKeyRejected', 'Mağaza yeni anahtarı kabul etmedi; eski anahtar yerinde kaldı.', 400),
      ),
    )
    testState.connections = [
      connection({
        Id: 12,
        Store: StoreKind.Google,
        Name: 'Play',
        Fingerprint: { ServiceAccountEmail: 'old@p.iam.gserviceaccount.com', ProjectId: 'p' },
      }),
    ]
    const { user } = renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'Anahtarı değiştir' }))
    const dialog = await screen.findByRole('dialog', { name: 'Play anahtarını değiştir' })
    expect(within(dialog).queryByLabelText('Issuer ID')).not.toBeInTheDocument()
    await user.upload(
      within(dialog).getByLabelText("Servis hesabı JSON'u"),
      new File([SAMPLE_SERVICE_ACCOUNT], 'play.json', { type: 'application/json' }),
    )
    expect(await within(dialog).findByText('storeasy@kuzeylab-play.iam.gserviceaccount.com')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Doğrula ve değiştir' }))

    expect(await within(dialog).findByText('Mağaza yeni anahtarı kabul etmedi; eski anahtar yerinde kaldı.')).toBeInTheDocument()
    expect(lastRequestTo('/StoreConnections/connection/key')?.body).toMatchObject({ ConnectionId: 12, Secret: SAMPLE_SERVICE_ACCOUNT })
    expect(within(dialog).queryByText('play.json')).not.toBeInTheDocument()
  })

  it('offers no key replacement for a Microsoft connection', async () => {
    testState.connections = [
      connection({ Id: 13, Store: StoreKind.Microsoft, Name: 'Partner Center', Fingerprint: { SellerId: '40291187' } }),
    ]
    renderWithProviders(<ConnectionsPage />)

    const card = await screen.findByRole('article', { name: 'Partner Center' })
    expect(within(card).queryByRole('button', { name: 'Anahtarı değiştir' })).not.toBeInTheDocument()
  })
})
