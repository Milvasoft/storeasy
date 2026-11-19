import { screen, waitFor, within } from '@testing-library/react'
import { HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { DeploymentMode, RegistrationMode, UserType } from '@/lib/api/types'
import { getDeviceId } from '@/lib/api/tokenStore'
import { loginResponse, publicSettings } from '../../support/factories'
import { endpoint, envelope, errorResponse } from '../../support/handlers'
import { permissionToken } from '../../support/platform'
import { renderApp } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

const SINGLE_TEAM = { Mode: DeploymentMode.SingleTeam, RegistrationMode: RegistrationMode.InviteOnly }

function menu() {
  return screen.findByRole('complementary', { name: 'Ana menü' })
}

describe('first run setup (single team)', () => {
  it('shows the setup instead of the sign-in at / while the server has no user', async () => {
    testState.publicSettings = publicSettings({ ...SINGLE_TEAM, SetupRequired: true })

    await renderApp({ authed: false, route: '/' })

    expect(await screen.findByRole('heading', { name: "storeasy'yi kur" })).toBeInTheDocument()
    expect(screen.getByLabelText('Kurulum kodu')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Giriş yap' })).not.toBeInTheDocument()
  })

  it('creates the administrator and the team with the code and signs in', async () => {
    testState.publicSettings = publicSettings({ ...SINGLE_TEAM, SetupRequired: true })
    server.use(
      endpoint('post', '/System/setup', () => {
        testState.publicSettings = publicSettings({ ...SINGLE_TEAM, SetupRequired: false })
        return HttpResponse.json(envelope(loginResponse({ UserType: UserType.Manager })))
      }),
    )
    const { user } = await renderApp({ authed: false, route: '/login' })

    await user.type(await screen.findByLabelText('Kurulum kodu'), 'abcd-efgh-jkmn')
    await user.type(screen.getByLabelText('Ad'), 'Deniz')
    await user.type(screen.getByLabelText('E-posta'), 'deniz@kuzeylab.dev')
    await user.type(screen.getByLabelText('Şifre'), 'Kuzey!2026')
    await user.type(screen.getByLabelText('Takım adı'), 'Kuzey Lab')
    await user.click(screen.getByRole('button', { name: 'Kurulumu bitir' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/apps'))
    expect(lastRequestTo('/System/setup')?.body).toEqual({
      SetupCode: 'abcd-efgh-jkmn',
      Name: 'Deniz',
      Surname: null,
      Email: 'deniz@kuzeylab.dev',
      Password: 'Kuzey!2026',
      OrganizationName: 'Kuzey Lab',
      DeviceId: getDeviceId(),
    })
  })

  it('says the code is wrong on 403 and keeps the form', async () => {
    testState.publicSettings = publicSettings({ ...SINGLE_TEAM, SetupRequired: true })
    server.use(endpoint('post', '/System/setup', () => errorResponse('SetupCodeInvalid', 'Kurulum kodu doğru değil.', 403)))
    const { user } = await renderApp({ authed: false, route: '/login' })

    await user.type(await screen.findByLabelText('Kurulum kodu'), 'WRONG')
    await user.type(screen.getByLabelText('Ad'), 'Deniz')
    await user.type(screen.getByLabelText('E-posta'), 'deniz@kuzeylab.dev')
    await user.type(screen.getByLabelText('Şifre'), 'Kuzey!2026')
    await user.type(screen.getByLabelText('Takım adı'), 'Kuzey Lab')
    await user.click(screen.getByRole('button', { name: 'Kurulumu bitir' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Kurulum kodu doğru değil.')
    expect(screen.getByLabelText('Takım adı')).toHaveValue('Kuzey Lab')
  })

  it('asks for the code before calling the API', async () => {
    testState.publicSettings = publicSettings({ ...SINGLE_TEAM, SetupRequired: true })
    const { user } = await renderApp({ authed: false, route: '/login' })

    await user.click(await screen.findByRole('button', { name: 'Kurulumu bitir' }))

    expect(await screen.findByText('Kurulum kodunu gir.')).toBeInTheDocument()
    expect(requestsTo('/System/setup')).toHaveLength(0)
  })
})

describe('single team sign-in', () => {
  it('hides the sign-up link once the server is set up', async () => {
    testState.publicSettings = publicSettings(SINGLE_TEAM)

    await renderApp({ authed: false, route: '/login' })

    expect(await screen.findByRole('button', { name: 'Giriş yap' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Kayıt ol' })).not.toBeInTheDocument()
  })

  it('keeps the sign-up link on a multi tenant server', async () => {
    testState.publicSettings = publicSettings({ Mode: DeploymentMode.MultiTenant })

    await renderApp({ authed: false, route: '/login' })

    expect(await screen.findByRole('link', { name: 'Kayıt ol' })).toHaveAttribute('href', '/register')
  })

  it('sends /register without an invitation back to the sign-in', async () => {
    testState.publicSettings = publicSettings(SINGLE_TEAM)

    await renderApp({ authed: false, route: '/register' })

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/login'))
  })

  it('still opens /register with an invitation link', async () => {
    testState.publicSettings = publicSettings(SINGLE_TEAM)

    await renderApp({ authed: false, route: '/register?invite=invite-token' })

    expect(await screen.findByRole('button', { name: 'Hesap oluştur' })).toBeInTheDocument()
  })
})

describe('single team shell', () => {
  it('shows the team without a switcher', async () => {
    testState.publicSettings = publicSettings(SINGLE_TEAM)

    await renderApp({ route: '/apps' })

    const aside = within(await menu())
    expect(await aside.findByRole('group', { name: /^Takım: / })).toBeInTheDocument()
    expect(aside.queryByRole('button', { name: /Değiştir$/ })).not.toBeInTheDocument()
  })

  it('merges the team settings and the System pages into one Settings area', async () => {
    testState.publicSettings = publicSettings(SINGLE_TEAM)

    await renderApp({
      route: '/apps',
      userType: UserType.Manager,
      accessToken: permissionToken('App.SuperAdmin'),
    })

    const nav = within(await screen.findByRole('navigation', { name: 'Ana menü' }))
    expect(await nav.findByText('Ayarlar')).toBeInTheDocument()
    expect(nav.getByText('Sistem')).toBeInTheDocument()
    expect(nav.queryByText('Organizasyon')).not.toBeInTheDocument()
    expect(nav.queryByText('Platform')).not.toBeInTheDocument()
    expect(nav.getByRole('link', { name: 'Takım' })).toHaveAttribute('href', '/settings')
    expect(nav.getByRole('link', { name: 'Mağaza bağlantıları' })).toHaveAttribute('href', '/connections')
    expect(nav.getByRole('link', { name: 'Sistem durumu' })).toHaveAttribute('href', '/admin/system')
    expect(nav.getByRole('link', { name: 'Kullanıcılar' })).toHaveAttribute('href', '/admin/users')
  })

  it('takes an administrator without a team to the create-team step instead of the admin area', async () => {
    testState.publicSettings = publicSettings(SINGLE_TEAM)
    testState.organizations = []

    await renderApp({ route: '/', userType: UserType.Manager, storedOrganization: false })

    expect(await screen.findByRole('heading', { name: 'Takımını oluştur' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/setup')
  })
})
