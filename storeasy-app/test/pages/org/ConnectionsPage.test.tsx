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
})
