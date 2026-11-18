import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ExternalLoginProvider, InvitationStatus, OrgRole } from '@/lib/api/types'
import { describeAgent, langOfCulture } from '@/features/access/AccountCards'
import { GoogleCallbackPage } from '@/features/access/GoogleCallbackPage'
import { browserLocation } from '@/features/access/GoogleSignIn'
import { AccountPage, SecurityPage } from '@/pages/account/AccountPage'
import { InvitePage } from '@/pages/auth/InvitePage'
import { LoginPage } from '@/pages/auth/LoginPage'
import { RegisterPage } from '@/pages/auth/RegisterPage'
import { accountDetail, accountSession, invitationPreview, publicSettings } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderApp, renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

const PENDING_KEY = 'storeasy.google-sign-in'

describe('Google sign in', () => {
  it('shows no Google button when the instance has it off', async () => {
    renderWithProviders(<LoginPage />, { authed: false })

    await waitFor(() => expect(requestsTo('/System/public-settings')).toHaveLength(1))
    expect(screen.queryByRole('button', { name: /Google ile devam et/ })).not.toBeInTheDocument()
  })

  it('starts the sign in, keeps the state and goes to Google', async () => {
    testState.publicSettings = publicSettings({ GoogleSignInEnabled: true })
    const assign = vi.spyOn(browserLocation, 'assign').mockImplementation(() => undefined)
    const { user } = renderWithProviders(<LoginPage />, { authed: false, route: '/login?returnUrl=%2Freviews' })

    await user.click(await screen.findByRole('button', { name: /Google ile devam et/ }))

    await waitFor(() => expect(assign).toHaveBeenCalledWith('https://accounts.google.test/o/oauth2/v2/auth?state=google-state'))
    expect(JSON.parse(sessionStorage.getItem(PENDING_KEY) ?? '{}')).toEqual({
      state: 'google-state',
      invitationToken: null,
      returnTo: '/reviews',
    })
    expect(requestsTo('/Account/login/google/start')[0].headers.authorization).toBeUndefined()
  })

  it('shows why the start failed', async () => {
    testState.publicSettings = publicSettings({ GoogleSignInEnabled: true })
    server.use(
      endpoint('post', '/Account/login/google/start', () => errorResponse('GoogleSignInUnavailable', 'Google şu an yanıt vermiyor.', 503)),
    )
    const { user } = renderWithProviders(<LoginPage />, { authed: false })

    await user.click(await screen.findByRole('button', { name: /Google ile devam et/ }))

    expect(await screen.findByText('Google şu an yanıt vermiyor.')).toBeInTheDocument()
  })

  it('carries the invitation of the registration link', async () => {
    testState.publicSettings = publicSettings({ GoogleSignInEnabled: true })
    vi.spyOn(browserLocation, 'assign').mockImplementation(() => undefined)
    const { user } = renderWithProviders(<RegisterPage />, { authed: false, route: '/register?invite=tok-9' })

    await user.click(await screen.findByRole('button', { name: /Google ile devam et/ }))

    await waitFor(() => expect(JSON.parse(sessionStorage.getItem(PENDING_KEY) ?? '{}')).toMatchObject({ invitationToken: 'tok-9' }))
  })

  it('completes the sign in on the callback and opens the page it started from', async () => {
    sessionStorage.setItem(PENDING_KEY, JSON.stringify({ state: 'google-state', invitationToken: 'tok-9', returnTo: '/reviews' }))
    renderWithProviders(<GoogleCallbackPage />, {
      authed: false,
      needsOrganization: false,
      route: '/auth/google/callback?code=abc&state=google-state',
    })

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/reviews'))
    expect(lastRequestTo('/Account/login/google')?.body).toMatchObject({ Code: 'abc', State: 'google-state', InvitationToken: 'tok-9' })
    expect(sessionStorage.getItem(PENDING_KEY)).toBeNull()
    expect(JSON.parse(localStorage.getItem('storeasy.auth.tokens') ?? '{}')).toMatchObject({ userName: 'deniz@kuzeylab.dev' })
  })

  it('refuses an answer that belongs to another sign in without calling the API', async () => {
    sessionStorage.setItem(PENDING_KEY, JSON.stringify({ state: 'mine', invitationToken: null, returnTo: null }))
    renderWithProviders(<GoogleCallbackPage />, {
      authed: false,
      needsOrganization: false,
      route: '/auth/google/callback?code=abc&state=theirs',
    })

    expect(await screen.findByText('Bu yanıt, bu tarayıcının başlattığı girişe ait değil. Yeniden başla.')).toBeInTheDocument()
    expect(requestsTo('/Account/login/google')).toHaveLength(0)
  })

  it('says when Google refused or the user cancelled', async () => {
    renderWithProviders(<GoogleCallbackPage />, {
      authed: false,
      needsOrganization: false,
      route: '/auth/google/callback?error=access_denied',
    })

    expect(await screen.findByText("Girişi Google'da iptal ettin ya da Google reddetti.")).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Girişe dön' })).toHaveAttribute('href', '/login')
  })

  it('says when the answer is incomplete', async () => {
    renderWithProviders(<GoogleCallbackPage />, { authed: false, needsOrganization: false, route: '/auth/google/callback?state=x' })

    expect(await screen.findByText("Google'dan gelen yanıt eksik. Yeniden başla.")).toBeInTheDocument()
  })

  it('shows the API refusal of a used state', async () => {
    renderWithProviders(<GoogleCallbackPage />, {
      authed: false,
      needsOrganization: false,
      route: '/auth/google/callback?code=abc&state=used-state',
    })

    expect(await screen.findByText('Google ile giriş isteği geçersiz ya da süresi dolmuş.')).toBeInTheDocument()
  })

  it('has its own route outside the shell', async () => {
    await renderApp({ authed: false, route: '/auth/google/callback?error=access_denied' })

    expect(await screen.findByRole('heading', { name: 'Google ile giriş tamamlanmadı' })).toBeInTheDocument()
  })
})

describe('account profile', () => {
  it('saves the name, surname and language and switches the interface language', async () => {
    const { user } = renderWithProviders(<AccountPage />)

    const name = await screen.findByLabelText('Ad')
    await user.clear(name)
    await user.type(name, 'Deniz Su')
    await user.click(screen.getByRole('button', { name: /Tercih edilen dil/ }))
    await user.click(await screen.findByRole('option', { name: 'English' }))
    expect(screen.getByText('2 değişiklik')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(lastRequestTo('/Account/profile')?.body).toEqual({ Name: 'Deniz Su', Surname: 'Arslan', Language: 'en-US' }))
    expect(await screen.findByRole('heading', { name: 'Account', level: 1 })).toBeInTheDocument()
  })

  it('asks for a name before sending', async () => {
    const { user } = renderWithProviders(<AccountPage />)

    await user.clear(await screen.findByLabelText('Ad'))
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    expect(screen.getByText('Adını yaz.')).toBeInTheDocument()
    expect(requestsTo('/Account/profile')).toHaveLength(0)
  })

  it('shows the saved language and the membership date', async () => {
    testState.account = accountDetail({ PreferredLanguage: 'en-US', CreationDate: '2026-09-02T09:00:00Z' })
    renderWithProviders(<AccountPage />)

    expect(await screen.findByRole('button', { name: /Tercih edilen dil/ })).toHaveTextContent('English')
    expect(screen.getByText('Üyelik tarihi')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Kaydet' })).toBeDisabled()
  })

  it('maps cultures and user agents', () => {
    expect(langOfCulture('en-GB')).toBe('en')
    expect(langOfCulture('de-DE')).toBeNull()
    expect(
      describeAgent('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile Safari/604.1'),
    ).toBe('Safari · iOS')
    expect(describeAgent('Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0')).toBe('Firefox · Linux')
    expect(describeAgent('curl/8.0')).toBeNull()
    expect(describeAgent(null)).toBeNull()
  })
})

describe('account sessions and linked accounts', () => {
  const other = accountSession({
    Id: 502,
    IsCurrent: false,
    UserAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 15_1) AppleWebKit/605.1.15 Version/18.1 Safari/605.1.15',
    IpAddress: '10.0.0.8',
  })

  it('lists the devices and signs one out after the confirmation', async () => {
    testState.sessions = [accountSession(), other]
    const { user } = renderWithProviders(<SecurityPage />)

    const card = await screen.findByRole('region', { name: 'Oturumlarım' })
    expect(await within(card).findByText('Chrome · Windows')).toBeInTheDocument()
    expect(within(card).getByText('Bu cihaz')).toBeInTheDocument()
    expect(within(card).getByText('2 cihaz')).toBeInTheDocument()
    await user.click(within(card).getByRole('button', { name: 'Safari · macOS oturumunu kapat' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Safari · macOS oturumu kapatılsın mı?' })
    await user.click(within(dialog).getByRole('button', { name: 'Oturumu kapat' }))

    await waitFor(() => expect(lastRequestTo('/Account/session')?.body).toEqual({ Id: 502 }))
    expect(await screen.findByText('Oturum kapatıldı.')).toBeInTheDocument()
  })

  it('signs out every other device', async () => {
    testState.sessions = [accountSession(), other, { ...other, Id: 503 }]
    const { user } = renderWithProviders(<SecurityPage />)

    await user.click(await screen.findByRole('button', { name: 'Diğerlerini kapat' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Diğer 2 oturum kapatılsın mı?' })
    await user.click(within(dialog).getByRole('button', { name: 'Diğerlerini kapat' }))

    expect(await screen.findByText('2 oturum kapatıldı.')).toBeInTheDocument()
    expect(requestsTo('/Account/sessions/others').map((request) => request.method)).toEqual(['DELETE'])
  })

  it('has nothing to sign out on a single device', async () => {
    renderWithProviders(<SecurityPage />)

    expect(await screen.findByText('Yalnız bu cihazda oturum açık.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Diğerlerini kapat' })).toBeDisabled()
  })

  it('keeps the designed coming card while the AccountSessions flag is off', async () => {
    renderWithProviders(<SecurityPage />, { flags: { AccountSessions: false } })

    expect(await screen.findByText('Yakında')).toBeInTheDocument()
    expect(requestsTo('/Account/sessions')).toHaveLength(0)
  })

  it('shows a linked Google account', async () => {
    testState.account = accountDetail({ ExternalLogins: [ExternalLoginProvider.Google] })
    renderWithProviders(<SecurityPage />)

    const card = await screen.findByRole('region', { name: 'Bağlı hesaplar' })
    expect(within(card).getByText('Bağlı')).toBeInTheDocument()
  })

  it('offers linking when Google sign in is on but the account is not linked', async () => {
    testState.publicSettings = publicSettings({ GoogleSignInEnabled: true })
    renderWithProviders(<SecurityPage />)

    const card = await screen.findByRole('region', { name: 'Bağlı hesaplar' })
    expect(within(card).getByText('Bağlı değil')).toBeInTheDocument()
    expect(within(card).getByText('Bağlamak için bu adresle bir kez Google ile giriş yap.')).toBeInTheDocument()
  })
})

describe('invitation preview', () => {
  function renderInvite(authed = false) {
    return renderWithProviders(<InvitePage />, { authed, route: '/invite/tok-9', path: '/invite/:token', needsOrganization: false })
  }

  it('shows the organization, role, inviter, masked address and permissions before accepting', async () => {
    renderInvite()

    expect(await screen.findByRole('heading', { name: "Kuzey Lab'e katıl" })).toBeInTheDocument()
    expect(screen.getByText('Deniz Arslan')).toBeInTheDocument()
    expect(screen.getByText('c**@kuzeylab.dev')).toBeInTheDocument()
    expect(screen.getByText('Editör')).toBeInTheDocument()
    expect(within(screen.getByRole('list', { name: 'Neler yapabileceksin' })).getByText('Yorum yanıtla')).toBeInTheDocument()
    expect(requestsTo('/Organizations/invitation/preview')[0].query).toEqual({ Token: 'tok-9' })
  })

  it('names a custom role', async () => {
    testState.invitationPreview = invitationPreview({ Role: OrgRole.Custom, CustomRoleName: 'Yayın sorumlusu' })
    renderInvite()

    expect(await screen.findByText('Yayın sorumlusu')).toBeInTheDocument()
  })

  it('says an expired invitation expired and who to ask', async () => {
    testState.invitationPreview = invitationPreview({ Status: InvitationStatus.Expired })
    renderInvite()

    expect(await screen.findByRole('heading', { name: 'Davetin süresi dolmuş' })).toBeInTheDocument()
    expect(screen.getByText(/Deniz Arslan kişisinden yenisini iste/)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Giriş yap ve katıl' })).not.toBeInTheDocument()
  })

  it('says a revoked and a used invitation apart', async () => {
    testState.invitationPreview = invitationPreview({ Status: InvitationStatus.Revoked })
    const { unmount } = renderInvite(true)

    expect(await screen.findByRole('heading', { name: 'Davet geri alınmış' })).toBeInTheDocument()
    unmount()
    testState.invitationPreview = invitationPreview({ Status: InvitationStatus.Accepted })
    renderInvite(true)
    expect(await screen.findByRole('heading', { name: 'Davet zaten kullanıldı' })).toBeInTheDocument()
  })

  it('treats an unknown token as an invalid link', async () => {
    testState.invitationPreview = null
    renderInvite()

    expect(await screen.findByRole('heading', { name: 'Bu davet artık geçerli değil' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Giriş sayfasına git' })).toHaveAttribute('href', '/login')
  })
})
