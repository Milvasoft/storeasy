import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { getDeviceId } from '@/lib/api/tokenStore'
import { LoginPage } from '@/pages/auth/LoginPage'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo } from '../../support/state'

async function fillAndSubmit(user: ReturnType<typeof renderWithProviders>['user'], password = 'Kuzey!2026') {
  await user.clear(screen.getByLabelText('E-posta'))
  await user.type(screen.getByLabelText('E-posta'), 'deniz@kuzeylab.dev')
  await user.type(screen.getByLabelText('Şifre'), password)
  await user.click(screen.getByRole('button', { name: 'Giriş yap' }))
}

describe('LoginPage', () => {
  it('signs in with the e-mail as user name and opens the apps', async () => {
    const { user } = renderWithProviders(<LoginPage />, { authed: false, route: '/login' })

    await fillAndSubmit(user)

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/apps'))
    expect(lastRequestTo('/Account/login')?.body).toEqual({
      UserName: 'deniz@kuzeylab.dev',
      Password: 'Kuzey!2026',
      DeviceId: getDeviceId(),
    })
  })

  it('returns to the in-app page it was sent from, never to another site', async () => {
    const { user } = renderWithProviders(<LoginPage />, { authed: false, route: '/login?returnUrl=%2Fmembers' })

    await fillAndSubmit(user)

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/members'))
  })

  it('ignores a return address outside the app', async () => {
    const { user } = renderWithProviders(<LoginPage />, { authed: false, route: '/login?returnUrl=%2F%2Fevil.example' })

    await fillAndSubmit(user)

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/apps'))
  })

  it('shows the API message above the form and clears the password when the credentials are wrong', async () => {
    const { user } = renderWithProviders(<LoginPage />, { authed: false, route: '/login' })

    await fillAndSubmit(user, 'wrong-password')

    expect(await screen.findByText('E-posta veya şifre hatalı.')).toBeInTheDocument()
    expect(screen.getByLabelText('Şifre')).toHaveValue('')
    expect(requestsTo('/Account/login/refresh')).toHaveLength(0)
  })

  it('counts down Retry-After and keeps the button disabled on 429', async () => {
    server.use(
      endpoint('post', '/Account/login', () => errorResponse('TooManyRequests', 'Çok fazla deneme.', 429, { 'Retry-After': '42' })),
    )
    const { user } = renderWithProviders(<LoginPage />, { authed: false, route: '/login' })

    await fillAndSubmit(user)

    expect(await screen.findByText('42 saniye sonra tekrar dene.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Giriş yap · 0:42/ })).toBeDisabled()
  })

  it('checks the e-mail before sending anything', async () => {
    const { user } = renderWithProviders(<LoginPage />, { authed: false, route: '/login' })

    await user.type(screen.getByLabelText('E-posta'), 'deniz@kuzeylab')
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(screen.getByText('Geçerli bir e-posta adresi yaz.')).toBeInTheDocument()
    expect(requestsTo('/Account/login')).toHaveLength(0)
  })

  it('signs the root user in by user name (the instance admin has no e-mail address)', async () => {
    const { user } = renderWithProviders(<LoginPage />, { authed: false, route: '/login' })

    await user.type(screen.getByLabelText('E-posta'), 'rootuser')
    await user.type(screen.getByLabelText('Şifre'), 'Root!2026')
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }))

    await waitFor(() => expect(lastRequestTo('/Account/login')?.body).toMatchObject({ UserName: 'rootuser', Password: 'Root!2026' }))
  })

  it('still asks for the e-mail when the field is empty', async () => {
    const { user } = renderWithProviders(<LoginPage />, { authed: false, route: '/login' })

    await user.type(screen.getByLabelText('Şifre'), 'Root!2026')
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(screen.getByText('E-posta adresini yaz.')).toBeInTheDocument()
    expect(requestsTo('/Account/login')).toHaveLength(0)
  })

  it('says the password changed after a reset', () => {
    renderWithProviders(<LoginPage />, { authed: false, route: '/login?reset=ok' })

    expect(screen.getByText('Şifren değişti.')).toBeInTheDocument()
  })

  it('renders in English when that is the chosen language', () => {
    renderWithProviders(<LoginPage />, { authed: false, route: '/login', lang: 'en' })

    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Source code' })).toHaveAttribute('href', 'https://example.test/storeasy')
  })
})
