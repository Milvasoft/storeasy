import { act, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ActivityEventType, createManualSource } from '@/lib/activity/events'
import { OrgRole, StepDirection, StoreJobKind, StoreJobStatus, StoreJobStepStatus, StoreKind } from '@/lib/api/types'
import { AppDetailPage, type AppTab } from '@/pages/apps/AppDetailPage'
import { activityEvent, app, appDetail, connection, organization, storeRecord } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

function render(tab: AppTab = 'overview', appId = 101) {
  const source = createManualSource()
  const suffix = tab === 'overview' ? '' : `/${tab}`
  const view = renderWithProviders(<AppDetailPage tab={tab} />, {
    route: `/apps/${appId}${suffix}`,
    path: `/apps/:appId${suffix}`,
    activity: source,
  })
  return { ...view, source }
}

describe('AppDetailPage', () => {
  it('shows one card per store record with its connection and the latest builds', async () => {
    testState.connections = [connection(), connection({ Id: 12, Store: StoreKind.Google, Name: 'Kuzey Lab Play' })]

    render()

    expect(await screen.findByRole('heading', { name: 'Pusula', level: 1 })).toBeInTheDocument()
    expect(await screen.findByText('Kuzey Lab ASC')).toBeInTheDocument()
    expect(screen.getByText('Kuzey Lab Play')).toBeInTheDocument()
    expect(screen.getByText('6740056789')).toBeInTheDocument()
    const builds = screen.getByRole('table', { name: 'Derlemeler' })
    expect(within(builds).getByText('İşleniyor')).toBeInTheDocument()
    expect(screen.getByText('Bu uygulama için süren iş yok.')).toBeInTheDocument()
  })

  it('links every record to its public store page and its console', async () => {
    render()
    const card = await screen.findByRole('region', { name: 'Mağaza sayfaları' })

    expect(within(card).getByRole('link', { name: 'App Store mağaza sayfasını yeni sekmede aç' })).toHaveAttribute(
      'href',
      'https://apps.apple.com/app/id6740056789',
    )
    expect(within(card).getByRole('link', { name: 'App Store konsolunu yeni sekmede aç' })).toHaveAttribute(
      'href',
      'https://appstoreconnect.apple.com/apps/6740056789/distribution',
    )
    const play = within(card).getByRole('link', { name: 'Google Play mağaza sayfasını yeni sekmede aç' })
    expect(play.getAttribute('href')).toMatch(/^https:\/\/play\.google\.com\/store\/apps\/details\?id=/)
    expect(play).toHaveAttribute('target', '_blank')
    expect(play).toHaveAttribute('rel', 'noreferrer noopener')
  })

  it('syncs the connections of its records and follows the jobs on the line', async () => {
    testState.syncJobIds = [48300]
    const { user, source } = render()
    await screen.findByRole('heading', { name: 'Pusula', level: 1 })

    await user.click(screen.getByRole('button', { name: 'Senkron' }))

    await waitFor(() => expect(requestsTo('/Apps/sync')).toHaveLength(2))
    expect(requestsTo('/Apps/sync').map((request) => request.body)).toEqual([
      { OrganizationId: 7, ConnectionId: 11 },
      { OrganizationId: 7, ConnectionId: 12 },
    ])
    expect(await screen.findByText('2 bağlantı için senkron başladı.')).toBeInTheDocument()
    act(() =>
      source.push(
        activityEvent({
          Type: ActivityEventType.JobStep,
          JobId: 48300,
          JobKind: StoreJobKind.SyncApps,
          Store: StoreKind.Apple,
          JobStatus: StoreJobStatus.Running,
          StepOrder: 3,
          StepName: 'builds.list',
          Direction: StepDirection.Outbound,
          StepStatus: StoreJobStepStatus.Running,
          Progress: 4,
          Total: 5,
        }),
      ),
    )
    expect(screen.getByText('Canlı')).toBeInTheDocument()
    expect(screen.getByText(/3\. adım · builds\.list · 4 \/ 5/)).toBeInTheDocument()
  })

  it('announces the end of a sync it started', async () => {
    testState.syncJobIds = [48300]
    testState.appDetail = appDetail({ Stores: [storeRecord()] })
    const { user, source } = render()
    await screen.findByRole('heading', { name: 'Pusula', level: 1 })
    await user.click(screen.getByRole('button', { name: 'Senkron' }))
    await screen.findByText('1 bağlantı için senkron başladı.')

    act(() =>
      source.push(
        activityEvent({
          Type: ActivityEventType.JobSucceeded,
          JobId: 48300,
          JobKind: StoreJobKind.SyncApps,
          JobStatus: StoreJobStatus.Succeeded,
        }),
      ),
    )

    expect(await screen.findByText('İş bitti: Uygulama ve build senkronu')).toBeInTheDocument()
  })

  it('splits a record into its own app after a confirmation', async () => {
    const { user } = render()
    await screen.findByRole('heading', { name: 'Pusula', level: 1 })

    await user.click(screen.getAllByRole('button', { name: 'Kayıt işlemleri' })[1])
    await user.click(screen.getByRole('menuitem', { name: 'Ayrı uygulama yap' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Google Play kaydı ayrı bir uygulama olsun mu?' })
    await user.click(within(dialog).getByRole('button', { name: 'Ayrı uygulama yap' }))

    await waitFor(() => expect(lastRequestTo('/Apps/app/unlink')?.body).toEqual({ OrganizationId: 7, StoreAppId: 202 }))
    expect(await screen.findByText('Kayıt artık ayrı bir uygulama.')).toBeInTheDocument()
  })

  it('does not split the only record of an app', async () => {
    testState.appDetail = appDetail({ Stores: [storeRecord()] })
    const { user } = render()
    await screen.findByRole('heading', { name: 'Pusula', level: 1 })

    await user.click(screen.getByRole('button', { name: 'Kayıt işlemleri' }))

    expect(screen.getByRole('menuitem', { name: 'Bu, uygulamanın tek kaydı; ayrılamaz.' })).toHaveAttribute('aria-disabled', 'true')
  })

  it('pairs a record of another app for a store it is missing', async () => {
    testState.appDetail = appDetail({ Stores: [storeRecord()] })
    testState.apps = [
      app({ Stores: [storeRecord()] }),
      app({
        Id: 102,
        Name: 'Pusula Android',
        Stores: [
          storeRecord({ StoreAppId: 210, Store: StoreKind.Google, ExternalId: 'dev.kuzeylab.pusula', BundleId: 'dev.kuzeylab.pusula' }),
        ],
      }),
      app({ Id: 103, Name: 'Halka', Stores: [storeRecord({ StoreAppId: 220 })] }),
    ]
    const { user } = render()
    await screen.findByRole('heading', { name: 'Pusula', level: 1 })

    await user.click(screen.getByRole('button', { name: 'Kayıt eşle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Pusula ile bir mağaza kaydı eşle' })
    await user.click(await within(dialog).findByRole('button', { name: 'Mağaza kaydı' }))
    expect(screen.queryByRole('option', { name: /Halka/ })).not.toBeInTheDocument()
    await user.click(screen.getByRole('option', { name: /Pusula Android/ }))
    await user.click(within(dialog).getByRole('button', { name: 'Kaydı eşle' }))

    await waitFor(() => expect(lastRequestTo('/Apps/app/link')?.body).toEqual({ OrganizationId: 7, AppId: 101, StoreAppId: 210 }))
    expect(await screen.findByText('Kayıt eşlendi')).toBeInTheDocument()
  })

  it('filters the builds by their state', async () => {
    const { user } = render('builds')

    const table = await screen.findByRole('table', { name: 'Derlemeler' })
    expect(within(table).getAllByRole('row')).toHaveLength(4)
    expect(within(table).getByText('internal')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'İşleniyor · 1' }))

    expect(within(screen.getByRole('table', { name: 'Derlemeler' })).getAllByRole('row')).toHaveLength(2)
  })

  it('says when an app has no builds yet', async () => {
    testState.appDetail = appDetail({ Builds: [] })

    render('builds')

    expect(await screen.findByRole('heading', { name: 'Henüz build yok' })).toBeInTheDocument()
  })

  it('lists the jobs of the app', async () => {
    render('jobs')

    expect(await screen.findByRole('table', { name: 'İşler' })).toBeInTheDocument()
    expect(requestsTo('/StoreJobs').map((request) => request.body)).toContainEqual(
      expect.objectContaining({ OrganizationId: 7, AppId: 101 }),
    )
  })

  it('says the app is not found for an unknown id', async () => {
    render('overview', 999)

    expect(await screen.findByRole('heading', { name: 'Kayıt bulunamadı' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Uygulamalar' })).toBeInTheDocument()
  })

  it('hides the app from its header', async () => {
    const { user } = render()
    await screen.findByRole('heading', { name: 'Pusula', level: 1 })

    await user.click(screen.getByRole('button', { name: 'Gizle' }))

    await waitFor(() => expect(lastRequestTo('/Apps/app/hidden')?.body).toEqual({ OrganizationId: 7, AppId: 101, Hidden: true }))
    expect(await screen.findByText('Pusula gizlendi. Gizli uygulamalar altında duruyor.')).toBeInTheDocument()
  })

  it('marks a hidden app and shows it again from its header', async () => {
    testState.appDetail = appDetail({ HiddenDate: '2026-11-18T10:00:00Z' })
    const { user } = render()
    await screen.findByRole('heading', { name: 'Pusula', level: 1 })

    expect(screen.getByText('Gizli')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Göster' }))

    await waitFor(() => expect(lastRequestTo('/Apps/app/hidden')?.body).toEqual({ OrganizationId: 7, AppId: 101, Hidden: false }))
  })

  it('hides pairing and sync from a viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    render()

    expect(await screen.findByRole('heading', { name: 'Pusula', level: 1 })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Senkron' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kayıt işlemleri' })).not.toBeInTheDocument()
  })
})
