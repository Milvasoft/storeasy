import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { FilterType } from '@/lib/api/list'
import { OrgRole, StoreKind } from '@/lib/api/types'
import { AppsPage } from '@/pages/apps/AppsPage'
import { app, connection, organization, storeRecord } from '../../support/factories'
import { endpoint, errorResponse, listEnvelope } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

const halka = app({
  Id: 102,
  Name: 'Halka',
  Stores: [storeRecord({ StoreAppId: 210, Store: StoreKind.Google, ExternalId: 'dev.kuzeylab.halka', BundleId: null, SyncedDate: null })],
})

const kasa = app({
  Id: 103,
  Name: 'Kasa',
  Stores: [storeRecord({ StoreAppId: 230, Store: StoreKind.Google, ExternalId: 'dev.kuzeylab.kasa', BundleId: null })],
})

function cardNames() {
  return screen.getAllByRole('article').map((card) => within(card).getAllByRole('link')[0].textContent)
}

function render(route = '/apps', options: Parameters<typeof renderWithProviders>[1] = {}) {
  return renderWithProviders(<AppsPage />, { route, ...options })
}

describe('AppsPage', () => {
  it('marks each lane with the icon of its own connection and the store chips with the oldest connection of the store', async () => {
    testState.connections = [
      connection({ Id: 11, Icon: 'phone' }),
      connection({ Id: 14, Name: 'Second key', Icon: 'appstore' }),
      connection({ Id: 12, Store: StoreKind.Google, Name: 'Play', Icon: null }),
    ]
    testState.apps = [app({ Stores: [storeRecord({ StoreConnectionId: 14 }), storeRecord({ StoreAppId: 202, Store: StoreKind.Google, StoreConnectionId: 12 })] })]

    render()

    const pusula = (await screen.findByRole('link', { name: 'Pusula' })).closest('article') as HTMLElement
    await waitFor(() => expect(pusula.querySelector('.sti')).toHaveAttribute('data-icon', 'appstore'))
    // The Google connection has no icon: its lane keeps the text code.
    expect(Array.from(pusula.querySelectorAll('.st b')).map((code) => code.textContent)).toEqual(['GP'])
    const chips = screen.getByRole('group', { name: 'Mağaza' })
    expect(chips.querySelector('.sti')).toHaveAttribute('data-icon', 'phone')
  })

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
    expect(requestsTo('/Apps').map((request) => request.body)).toContainEqual(
      expect.objectContaining({ Store: StoreKind.Apple, PageNumber: 1 }),
    )
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

  it('marks an app whose alerts are muted with a bell-off beside its name', async () => {
    testState.apps = [app(), { ...halka, AlertsMutedDate: '2026-11-19T08:00:00Z' }]
    render()
    await screen.findByRole('link', { name: 'Halka' })

    expect(screen.getByRole('img', { name: 'Halka susturuldu' })).toBeInTheDocument()
    expect(screen.queryByRole('img', { name: 'Pusula susturuldu' })).not.toBeInTheDocument()
  })

  it('hides an app from its card without a confirmation and offers to undo it', async () => {
    testState.apps = [app(), halka]
    const { user } = render()
    await screen.findByRole('link', { name: 'Halka' })

    await user.click(screen.getByRole('button', { name: 'Halka uygulamasını gizle' }))

    await waitFor(() => expect(lastRequestTo('/Apps/app/hidden')?.body).toEqual({ OrganizationId: 7, AppId: 102, Hidden: true }))
    expect(await screen.findByText('Halka gizlendi. Gizli uygulamalar altında duruyor.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Geri al' }))

    await waitFor(() => expect(lastRequestTo('/Apps/app/hidden')?.body).toEqual({ OrganizationId: 7, AppId: 102, Hidden: false }))
    expect(await screen.findByText('Halka yeniden uygulamalar listesinde.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Geri al' })).not.toBeInTheDocument()
  })

  it('leaves the hidden apps out of the list and the store counts, and links to them', async () => {
    testState.apps = [app(), { ...halka, HiddenDate: '2026-11-18T10:00:00Z' }]
    const { user } = render()

    expect(await screen.findByRole('link', { name: 'Pusula' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Halka' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Google Play · 1/ })).toBeInTheDocument()
    await user.click(await screen.findByRole('button', { name: 'Gizli uygulamalar · 1' }))

    expect(screen.getByTestId('location')).toHaveTextContent('/apps?hidden=1')
    expect(await screen.findByRole('heading', { name: 'Gizli uygulamalar' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Halka' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Pusula' })).not.toBeInTheDocument()
    expect(requestsTo('/Apps').map((request) => request.body)).toContainEqual(
      expect.objectContaining({ Hidden: true, PageNumber: 1, RowCount: 20 }),
    )
  })

  it('shows a hidden app again from the hidden apps table', async () => {
    testState.apps = [app(), { ...halka, HiddenDate: '2026-11-18T10:00:00Z' }]
    const { user } = render('/apps?hidden=1&view=table')

    const table = await screen.findByRole('table', { name: 'Gizli uygulamalar' })
    await user.click(within(table).getByRole('button', { name: 'Halka uygulamasını göster' }))

    await waitFor(() => expect(lastRequestTo('/Apps/app/hidden')?.body).toEqual({ OrganizationId: 7, AppId: 102, Hidden: false }))
    expect(await screen.findByText('Halka yeniden uygulamalar listesinde.')).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/apps?hidden=1&view=table')
  })

  it('says when there is no hidden app and leads back to the list', async () => {
    const { user } = render('/apps?hidden=1')

    expect(await screen.findByRole('heading', { name: 'Gizli uygulama yok' })).toBeInTheDocument()
    await user.click(
      within(screen.getByRole('heading', { name: 'Gizli uygulama yok' }).parentElement as HTMLElement).getByRole('button', {
        name: 'Tüm uygulamalar',
      }),
    )

    expect(screen.getByTestId('location')).toHaveTextContent(/^\/apps$/)
  })

  it('does not offer hiding to a viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    render()

    expect(await screen.findByRole('link', { name: 'Pusula' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Pusula uygulamasını gizle' })).not.toBeInTheDocument()
  })

  it('asks the server for the order members chose, without a sort', async () => {
    render()
    await screen.findByRole('link', { name: 'Pusula' })

    const pageRequest = requestsTo('/Apps')
      .map((request) => request.body as Record<string, unknown>)
      .find((body) => body.RowCount === 20 && !body.Hidden)
    expect(pageRequest).toBeDefined()
    expect(pageRequest).not.toHaveProperty('Sorting')
  })

  it('moves a card with the keyboard on its handle and saves the whole order', async () => {
    testState.apps = [app(), halka, kasa]
    const { user } = render()
    await screen.findByRole('link', { name: 'Kasa' })
    expect(screen.getByText(/^Kartları sürükleyerek sırala/)).toBeInTheDocument()

    screen.getByRole('button', { name: 'Kasa uygulamasını taşı' }).focus()
    await user.keyboard('{Alt>}{ArrowLeft}{/Alt}')

    await waitFor(() => expect(lastRequestTo('/Apps/app/order')?.body).toEqual({ OrganizationId: 7, AppIds: [101, 103, 102] }))
    expect(cardNames()).toEqual(['Pusula', 'Kasa', 'Halka'])
    expect(screen.getByText('Kasa 2. sıraya taşındı.')).toHaveAttribute('aria-live', 'polite')
    await waitFor(() => expect(screen.getByRole('button', { name: 'Kasa uygulamasını taşı' })).toHaveFocus())
  })

  it('moves a card with the earlier and later buttons behind its handle', async () => {
    testState.apps = [app(), halka, kasa]
    const { user } = render()
    await screen.findByRole('link', { name: 'Kasa' })

    await user.click(screen.getByRole('button', { name: 'Pusula uygulamasını taşı' }))
    expect(screen.getByRole('button', { name: 'Pusula uygulamasını öne al' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Pusula uygulamasını geriye al' }))

    await waitFor(() => expect(lastRequestTo('/Apps/app/order')?.body).toEqual({ OrganizationId: 7, AppIds: [102, 101, 103] }))
    expect(cardNames()).toEqual(['Halka', 'Pusula', 'Kasa'])
  })

  it('reorders the cards by dragging one onto another', async () => {
    testState.apps = [app(), halka, kasa]
    render()
    await screen.findByRole('link', { name: 'Kasa' })
    const card = (name: string) => screen.getByRole('link', { name }).closest('article') as HTMLElement
    const dataTransfer = { setData: () => undefined, effectAllowed: '', dropEffect: '' }

    fireEvent.dragStart(card('Kasa'), { dataTransfer })
    expect(card('Kasa')).toHaveClass('dragging')
    fireEvent.dragOver(card('Pusula'), { dataTransfer })
    expect(cardNames()).toEqual(['Kasa', 'Pusula', 'Halka'])
    fireEvent.drop(card('Pusula'), { dataTransfer })
    fireEvent.dragEnd(card('Kasa'), { dataTransfer })

    await waitFor(() => expect(lastRequestTo('/Apps/app/order')?.body).toEqual({ OrganizationId: 7, AppIds: [103, 101, 102] }))
    expect(cardNames()).toEqual(['Kasa', 'Pusula', 'Halka'])
    expect(card('Kasa')).toHaveAttribute('class', 'appc')
  })

  it('puts a dragged card back when the drag is cancelled', async () => {
    testState.apps = [app(), halka, kasa]
    render()
    await screen.findByRole('link', { name: 'Kasa' })
    const card = (name: string) => screen.getByRole('link', { name }).closest('article') as HTMLElement
    const dataTransfer = { setData: () => undefined, effectAllowed: '', dropEffect: '' }

    fireEvent.dragStart(card('Kasa'), { dataTransfer })
    fireEvent.dragOver(card('Pusula'), { dataTransfer })
    fireEvent.dragEnd(card('Kasa'), { dataTransfer })

    expect(cardNames()).toEqual(['Pusula', 'Halka', 'Kasa'])
    expect(requestsTo('/Apps/app/order')).toHaveLength(0)
  })

  it('puts the cards back and says why when the new order is refused', async () => {
    testState.apps = [app(), halka, kasa]
    server.use(endpoint('post', '/Apps/app/order', () => errorResponse('AppNotFound', 'Uygulama bulunamadı.', 404)))
    const { user } = render()
    await screen.findByRole('link', { name: 'Kasa' })

    screen.getByRole('button', { name: 'Kasa uygulamasını taşı' }).focus()
    await user.keyboard('{Alt>}{ArrowUp}{/Alt}')

    expect(await screen.findByText('Uygulama bulunamadı.')).toBeInTheDocument()
    await waitFor(() => expect(cardNames()).toEqual(['Pusula', 'Halka', 'Kasa']))
  })

  it('does not offer reordering while searching, filtering, on more than one page, in the table or to a viewer', async () => {
    testState.apps = [app(), halka, kasa]
    const searching = render('/apps?q=a')
    await screen.findByRole('link', { name: 'Kasa' })
    expect(screen.getByText('Sırayı değiştirmek için aramayı ve mağaza filtresini kaldır.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kasa uygulamasını taşı' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Kasa' }).closest('article')).not.toHaveAttribute('draggable')
    searching.unmount()

    server.use(endpoint('patch', '/Apps', () => HttpResponse.json(listEnvelope(testState.apps, 1, 25, 2))))
    const paged = render()
    await screen.findByRole('link', { name: 'Kasa' })
    expect(screen.getByText('Sırayı değiştirmek için tüm uygulamaları tek sayfada göster.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kasa uygulamasını taşı' })).not.toBeInTheDocument()
    paged.unmount()
    server.resetHandlers()

    const table = render('/apps?view=table')
    await screen.findByRole('table', { name: 'Uygulamalar' })
    expect(screen.queryByRole('button', { name: 'Kasa uygulamasını taşı' })).not.toBeInTheDocument()
    table.unmount()

    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    render()
    await screen.findByRole('link', { name: 'Kasa' })
    expect(screen.queryByRole('button', { name: 'Kasa uygulamasını taşı' })).not.toBeInTheDocument()
    expect(screen.queryByText(/^Kartları sürükleyerek sırala/)).not.toBeInTheDocument()
  })

  it('shows the error state with a retry when the list cannot be read', async () => {
    server.use(endpoint('patch', '/Apps', () => errorResponse('UnhandledException', 'Sunucu hatası.', 500)))

    render()

    expect(await screen.findByText('Sunucu hatası.')).toBeInTheDocument()
  })
})
