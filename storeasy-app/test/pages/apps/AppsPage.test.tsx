import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { FilterType } from '@/lib/api/list'
import { OrgRole, StoreKind } from '@/lib/api/types'
import { AppsPage } from '@/pages/apps/AppsPage'
import { app, connection, organization, storeRecord } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

const halka = app({
  Id: 102,
  Name: 'Halka',
  Stores: [storeRecord({ StoreAppId: 210, Store: StoreKind.Google, ExternalId: 'dev.kuzeylab.halka', BundleId: null, SyncedDate: null })],
})

function render(route = '/apps', options: Parameters<typeof renderWithProviders>[1] = {}) {
  return renderWithProviders(<AppsPage />, { route, ...options })
}

describe('AppsPage', () => {
  it('shows each app as a card with one lane per store record and the sync state', async () => {
    testState.apps = [app(), halka]

    render()

    const pusula = (await screen.findByRole('link', { name: 'Pusula' })).closest('article') as HTMLElement
    expect(pusula).toHaveAttribute('class', 'appc')
    expect(within(pusula).getAllByText('Senkronlandı')).toHaveLength(2)
    expect(within(pusula).getAllByText('dev.kuzeylab.pusula').length).toBeGreaterThan(0)
    const halkaCard = screen.getByRole('link', { name: 'Halka' }).closest('article') as HTMLElement
    expect(within(halkaCard).getByText('İlk senkron bekleniyor')).toBeInTheDocument()
    expect(screen.getByText(/^Son senkron/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /App Store · 1/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Google Play · 2/ })).toBeInTheDocument()
  })

  it('filters by store from the address', async () => {
    testState.apps = [app(), halka]

    render('/apps?store=apple')

    expect(await screen.findByRole('link', { name: 'Pusula' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Halka' })).not.toBeInTheDocument()
  })

  it('shows the table view and opens an app from its row', async () => {
    testState.apps = [app(), halka]
    const { user } = render('/apps?view=table')

    const table = await screen.findByRole('table', { name: 'Uygulamalar' })
    expect(within(table).getByRole('columnheader', { name: 'Microsoft Store' })).toBeInTheDocument()
    await user.click(within(table).getByText('Halka'))

    expect(screen.getByTestId('location')).toHaveTextContent('/apps/102')
  })

  it('searches app names 400 ms after typing stops, from the first page', async () => {
    const { user } = render('/apps?page=3')
    await screen.findByRole('link', { name: 'Pusula' })

    await user.type(screen.getByRole('searchbox', { name: 'Uygulama adında ara' }), 'pus')

    await waitFor(() =>
      expect(lastRequestTo('/Apps')?.body).toMatchObject({
        OrganizationId: 7,
        PageNumber: 1,
        Filtering: { Criterias: [{ FilterBy: 'Name', Value: 'pus', Type: FilterType.Contains }] },
      }),
    )
    expect(screen.getByTestId('location')).toHaveTextContent('/apps?q=pus')
  })

  it('says what to do when a search finds nothing', async () => {
    testState.apps = []
    const { user } = render('/apps?q=kasa')

    expect(await screen.findByRole('heading', { name: '"kasa" için sonuç yok' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Aramayı temizle' }))

    expect(screen.getByTestId('location')).toHaveTextContent(/^\/apps$/)
  })

  it('leads a new organization to the store connections', async () => {
    testState.apps = []
    testState.connections = []

    render()

    expect(await screen.findByRole('heading', { name: 'Hat henüz boş' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Mağaza bağla' })).toHaveAttribute('href', '/connections')
  })

  it('offers to pull the apps when connections exist but no app came yet', async () => {
    testState.apps = []
    const { user } = render()

    expect(await screen.findByRole('heading', { name: 'Henüz uygulama yok' })).toBeInTheDocument()
    await user.click(
      within(screen.getByRole('heading', { name: 'Henüz uygulama yok' }).parentElement as HTMLElement).getByRole('button', {
        name: 'Mağazadan çek',
      }),
    )

    await waitFor(() => expect(lastRequestTo('/Apps/sync')?.body).toEqual({ OrganizationId: 7, ConnectionId: null }))
  })

  it('pulls every connection and says how many jobs started', async () => {
    testState.syncJobIds = [48300, 48301]
    const { user } = render()
    await screen.findByRole('link', { name: 'Pusula' })

    await user.click(screen.getByRole('button', { name: 'Mağazadan çek' }))

    expect(await screen.findByText('Senkron başladı: 2 iş hatta.')).toBeInTheDocument()
  })

  it('says when there is nothing to sync', async () => {
    testState.syncJobIds = []
    const { user } = render()
    await screen.findByRole('link', { name: 'Pusula' })

    await user.click(screen.getByRole('button', { name: 'Mağazadan çek' }))

    expect(await screen.findByText('Senkronlanacak App Store Connect ya da Google Play bağlantısı yok.')).toBeInTheDocument()
  })

  it('adds a Google Play app by its package name and opens it', async () => {
    testState.connections = [connection(), connection({ Id: 12, Store: StoreKind.Google, Name: 'Kuzey Lab Play' })]
    const { user } = render()
    await screen.findByRole('link', { name: 'Pusula' })

    await user.click(screen.getByRole('button', { name: 'Google Play uygulaması ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Google Play uygulaması ekle' })
    await user.type(within(dialog).getByLabelText('Paket adı'), 'kasa')
    await user.click(within(dialog).getByRole('button', { name: 'Uygulamayı ekle' }))
    expect(within(dialog).getByText(/noktayla ayrılmış en az iki parçadır/)).toBeInTheDocument()
    expect(requestsTo('/Apps/google')).toHaveLength(0)
    await user.clear(within(dialog).getByLabelText('Paket adı'))
    await user.type(within(dialog).getByLabelText('Paket adı'), 'dev.kuzeylab.kasa')
    await user.click(within(dialog).getByRole('button', { name: 'Uygulamayı ekle' }))

    await waitFor(() =>
      expect(lastRequestTo('/Apps/google')?.body).toEqual({ OrganizationId: 7, ConnectionId: 12, PackageName: 'dev.kuzeylab.kasa' }),
    )
    expect(await screen.findByText('dev.kuzeylab.kasa eklendi; bir senkron şimdi kontrol ediyor.')).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/apps/103')
  })

  it('keeps the dialog open and says so when the app is already added', async () => {
    testState.connections = [connection({ Id: 12, Store: StoreKind.Google, Name: 'Kuzey Lab Play' })]
    server.use(endpoint('post', '/Apps/google', () => errorResponse('StoreAppAlreadyAdded', 'Zaten ekli.', 409)))
    const { user } = render()
    await screen.findByRole('link', { name: 'Pusula' })

    await user.click(screen.getByRole('button', { name: 'Google Play uygulaması ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Google Play uygulaması ekle' })
    await user.type(within(dialog).getByLabelText('Paket adı'), 'dev.kuzeylab.pusula')
    await user.click(within(dialog).getByRole('button', { name: 'Uygulamayı ekle' }))

    expect(await within(dialog).findByText('Bu uygulama zaten ekli.')).toBeInTheDocument()
  })

  it('asks for a Google Play connection before a package name can be added', async () => {
    const { user } = render()
    await screen.findByRole('link', { name: 'Pusula' })

    await user.click(screen.getByRole('button', { name: 'Google Play uygulaması ekle' }))

    const dialog = await screen.findByRole('dialog', { name: 'Google Play uygulaması ekle' })
    expect(await within(dialog).findByText(/Önce bir Google Play bağlantısı ekle/)).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Uygulamayı ekle' })).toBeDisabled()
  })

  it('shows a viewer the apps without the sync and add actions', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    render()

    expect(await screen.findByRole('link', { name: 'Pusula' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Mağazadan çek' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Google Play uygulaması ekle' })).not.toBeInTheDocument()
  })

  it('shows the error state with a retry when the list cannot be read', async () => {
    server.use(endpoint('patch', '/Apps', () => errorResponse('UnhandledException', 'Sunucu hatası.', 500)))

    render()

    expect(await screen.findByText('Sunucu hatası.')).toBeInTheDocument()
  })
})
