import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole, UserType } from '@/lib/api/types'
import { organization } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderApp } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

function menu() {
  return screen.getByRole('complementary', { name: 'Ana menü' })
}

describe('app shell and routing', () => {
  it('sends a guest to sign in and keeps the page to return to', async () => {
    await renderApp({ authed: false, route: '/members' })

    expect(await screen.findByRole('heading', { name: 'Tekrar hoş geldin' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/login?returnUrl=%2Fmembers')
  })

  it('opens the first setup when the user has no organization', async () => {
    testState.organizations = []

    await renderApp({ route: '/apps', storedOrganization: false })

    expect(await screen.findByRole('heading', { name: 'Organizasyonunu oluştur' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/setup')
  })

  it('says access is gone when the remembered organization is no longer among the memberships', async () => {
    testState.organizations = []

    await renderApp({ route: '/apps' })

    expect(await screen.findByRole('heading', { name: 'Erişimin kalmadı' })).toBeInTheDocument()
  })

  it('creates the first organization with a slug suggested from the name', async () => {
    testState.organizations = []
    const { user } = await renderApp({ route: '/setup' })

    await user.type(await screen.findByLabelText('Ad'), 'Mavi Atölye')
    expect(screen.getByLabelText('Kısa ad')).toHaveValue('mavi-atolye')
    testState.organizations = [organization({ Id: 42, Name: 'Mavi Atölye', Slug: 'mavi-atolye' })]
    await user.click(screen.getByRole('button', { name: 'Oluştur ve devam et' }))

    expect(await screen.findByRole('heading', { name: 'İlk mağazanı bağla' })).toBeInTheDocument()
    expect(lastRequestTo('/Organizations/organization')?.body).toEqual({ Name: 'Mavi Atölye', Slug: 'mavi-atolye' })
    expect(lastRequestTo('/Organizations/organization')?.headers['idempotency-key']).toBeTruthy()
  })

  it('shows the menu with the not-yet-built sections marked and opens their designed first-use state', async () => {
    const { user } = await renderApp({ route: '/apps' })

    expect(await screen.findByRole('heading', { name: 'Hat henüz boş' })).toBeInTheDocument()
    expect(within(menu()).getByRole('link', { name: /Mağaza bağlantıları/ })).toBeInTheDocument()
    expect(within(menu()).getByRole('link', { name: /Yorumlar/ })).toHaveTextContent('Yakında')
    await user.click(within(menu()).getByRole('link', { name: /Yorumlar/ }))
    expect(await screen.findByRole('heading', { name: 'Henüz yorum yok' })).toBeInTheDocument()
  })

  it('hides connections and webhooks from an editor (design kit ROLE_HIDE)', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]

    await renderApp({ route: '/members' })

    await screen.findByRole('heading', { name: 'Üyeler', level: 1 })
    expect(within(menu()).queryByRole('link', { name: /Mağaza bağlantıları/ })).not.toBeInTheDocument()
    expect(within(menu()).queryByRole('link', { name: /Webhook/ })).not.toBeInTheDocument()
  })

  it('shows the Platform group only to a platform administrator', async () => {
    await renderApp({ route: '/apps', userType: UserType.Manager })

    await screen.findByRole('heading', { name: 'Hat henüz boş' })
    expect(within(menu()).getByText('Platform')).toBeInTheDocument()
    expect(within(menu()).getByRole('link', { name: /Sistem durumu/ })).toBeInTheDocument()
  })

  it('refuses platform screens to everyone else', async () => {
    await renderApp({ route: '/admin/system' })

    expect(await screen.findByText('Platform ekranları bu kurulumun yöneticisine açık.')).toBeInTheDocument()
  })

  it('switches the organization from the menu', async () => {
    testState.organizations = [organization(), organization({ Id: 8, Name: 'Ada Stüdyo', Slug: 'ada-studyo', Role: OrgRole.Editor })]
    const { user } = await renderApp({ route: '/members' })

    await screen.findByRole('heading', { name: 'Üyeler', level: 1 })
    await user.click(within(menu()).getByRole('button', { name: 'Organizasyon: Kuzey Lab. Değiştir' }))
    await user.click(await screen.findByRole('menuitemradio', { name: /Ada Stüdyo/ }))

    await waitFor(() => expect(lastRequestTo('/Organizations/members')?.query).toEqual({ OrganizationId: '8' }))
  })

  it('changes the theme from the top bar', async () => {
    const { user } = await renderApp({ route: '/apps' })

    await screen.findByRole('heading', { name: 'Hat henüz boş' })
    await user.click(screen.getByRole('button', { name: 'Tema' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Koyu' }))

    expect(document.documentElement).toHaveClass('dark')
  })

  it('signs out from the account menu', async () => {
    const { user } = await renderApp({ route: '/apps' })

    await screen.findByRole('heading', { name: 'Hat henüz boş' })
    await user.click(within(menu()).getByRole('button', { name: 'Hesap menüsü' }))
    await user.click(screen.getByRole('menuitem', { name: 'Çıkış yap' }))

    expect(await screen.findByRole('heading', { name: 'Tekrar hoş geldin' })).toBeInTheDocument()
    expect(requestsTo('/Account/logout')).toHaveLength(1)
  })

  it('shows the real not-found page inside the shell for an unknown address', async () => {
    await renderApp({ route: '/aplications' })

    expect(await screen.findByRole('heading', { name: 'Sayfa bulunamadı' })).toBeInTheDocument()
    expect(menu()).toBeInTheDocument()
  })

  it('ends the session when the token cannot be refreshed and says so on the sign-in screen', async () => {
    server.use(endpoint('get', '/Organizations/members', () => errorResponse('Unauthorized', 'x', 401)))
    server.use(endpoint('post', '/Account/login/refresh', () => errorResponse('InvalidToken', 'x', 401)))

    await renderApp({ route: '/members' })

    expect(await screen.findByText('Oturumun sonlandı, tekrar giriş yap.')).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/login?returnUrl=%2Fmembers')
  })

  it('renews the token silently on 419 and keeps the page', async () => {
    let calls = 0
    server.use(
      endpoint('get', '/Organizations/members', () => {
        calls += 1
        return calls === 1
          ? errorResponse('SessionTimeout', 'x', 419)
          : Response.json({ IsSuccess: true, StatusCode: 200, Data: testState.members })
      }),
    )

    await renderApp({ route: '/members' })

    expect(await screen.findByText('Ece Yılmaz')).toBeInTheDocument()
    expect(requestsTo('/Account/login/refresh')).toHaveLength(1)
  })
})
