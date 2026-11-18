import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole, StoreKind } from '@/lib/api/types'
import { ReadinessOverviewPage, ReadinessPage } from '@/features/readiness/ReadinessPage'
import { contentState, readinessReport, setupContentApi } from '../../support/content'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupContentApi()

function render(query = '?store=apple') {
  return renderWithProviders(<ReadinessPage />, { route: `/apps/101/readiness${query}`, path: '/apps/:appId/readiness' })
}

describe('ReadinessPage', () => {
  it('lists the checks by group with their state, rule and the classes of the media row', async () => {
    render()

    expect(await screen.findByText('Ekran görüntüleri')).toBeInTheDocument()
    expect(screen.getByText('1 engel')).toBeInTheDocument()
    expect(screen.getByText('apple.screenshots.iphone.69')).toBeInTheDocument()
    const slots = screen.getByRole('list', { name: 'Ekran görüntüsü sınıfları' })
    expect(within(slots).getByText(/eksik/)).toBeInTheDocument()
    expect(within(slots).getByText(/iphone-69 sınıfından/)).toBeInTheDocument()
    expect(lastRequestTo('/Apps/readiness')?.query).toMatchObject({ AppId: '101', Store: String(StoreKind.Apple) })
  })

  it('links a missing check to the screen that fixes it, store included', async () => {
    render()

    const links = await screen.findAllByRole('link', { name: 'Düzelt' })

    expect(links[0]).toHaveAttribute('href', '/apps/101/listing/screenshots?locale=en-US&store=apple')
  })

  it('keeps the submission closed while something blocks', async () => {
    render()

    expect(await screen.findByText(/Önce engelleri kapat/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'İncelemeye gönder' })).toBeDisabled()
  })

  it('tells an Editor that only Owners and Admins submit, with the button disabled', async () => {
    contentState.readiness = readinessReport({ CanSubmit: true, Missing: 0, Items: [] })
    testState.organizations = [organization({ Role: OrgRole.Editor })]

    render()

    expect(await screen.findByText('İncelemeye gönderme yalnız Sahip ve Yönetici rolünde.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'İncelemeye gönder' })).toBeDisabled()
  })

  it('submits only after the app name and version are typed exactly', async () => {
    contentState.readiness = readinessReport({ CanSubmit: true, Missing: 0, Items: [] })
    const { user } = render()
    await user.click(await screen.findByRole('button', { name: 'İncelemeye gönder' }))
    const dialog = await screen.findByRole('alertdialog')
    const send = within(dialog).getByRole('button', { name: 'İncelemeye gönder' })

    await user.type(within(dialog).getByRole('textbox'), 'Pusula 2')
    expect(send).toBeDisabled()
    expect(within(dialog).getByText('Yazdığın metin eşleşmiyor.')).toBeInTheDocument()
    await user.type(within(dialog).getByRole('textbox'), '.4')
    await user.click(send)

    expect(await screen.findByText('Gönderildi; mağaza işi kuyruğa girdi.')).toBeInTheDocument()
    expect(lastRequestTo('/Releases/submit-for-review')?.body).toMatchObject({ StoreAppId: 201, VersionId: 'v-24', Confirm: 'Pusula 2.4' })
  })

  it('explains that Google Play has no submission for review', async () => {
    contentState.readiness = readinessReport({ Store: StoreKind.Google, StoreAppId: 202, CanSubmit: true })

    render('?store=google')

    expect(await screen.findByText(/Google Play'de sürüm bir track'e gönderilir/)).toBeInTheDocument()
  })
})

describe('ReadinessOverviewPage', () => {
  it('shows every app with its missing count, a link per store and the next deadline', async () => {
    renderWithProviders(<ReadinessOverviewPage />, { route: '/apps/readiness' })

    expect(await screen.findByRole('link', { name: 'Pusula' })).toHaveAttribute('href', '/apps/101/readiness')
    expect(screen.getByText('2 eksik')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Hazırlığı aç' })).toHaveAttribute('href', '/apps/101/readiness?store=apple')
    expect(screen.getByText('Yaş derecelendirmesi')).toBeInTheDocument()
  })

  it('checks every store live on demand', async () => {
    const { user } = renderWithProviders(<ReadinessOverviewPage />, { route: '/apps/readiness' })
    await screen.findByText('2 eksik')

    await user.click(screen.getByRole('button', { name: 'Hepsini şimdi kontrol et' }))

    await screen.findByText('2 eksik')
    expect(lastRequestTo('/Apps/overview')?.query).toMatchObject({ Refresh: 'true' })
  })
})
