import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RegisterPage } from '@/pages/auth/RegisterPage'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo } from '../../support/state'

async function fill(user: ReturnType<typeof renderWithProviders>['user'], password = 'Kuzey!2026') {
  await user.type(screen.getByLabelText('Ad'), 'Deniz')
  await user.type(screen.getByLabelText('Soyad'), 'Arslan')
  await user.type(screen.getByLabelText('E-posta'), 'deniz@kuzeylab.dev')
  await user.type(screen.getByLabelText('Şifre'), password)
  await user.click(screen.getByRole('button', { name: 'Hesap oluştur' }))
}

describe('RegisterPage', () => {
  it('sends the planned register contract with the invitation token and goes to "check your e-mail"', async () => {
    const { user } = renderWithProviders(<RegisterPage />, { authed: false, route: '/register?invite=tok-1' })

    await fill(user)

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/verify-email'))
    expect(lastRequestTo('/Account/register')?.body).toEqual({
      Email: 'deniz@kuzeylab.dev',
      Name: 'Deniz',
      Surname: 'Arslan',
      Password: 'Kuzey!2026',
      InvitationToken: 'tok-1',
    })
  })

  it('shows the password rules live and refuses a weak password before sending', async () => {
    const { user } = renderWithProviders(<RegisterPage />, { authed: false, route: '/register' })

    await fill(user, 'abc')

    expect(screen.getByText('Şifre aşağıdaki kuralların hepsini karşılamıyor.')).toBeInTheDocument()
    expect(screen.getAllByText('En az 8 karakter').length).toBeGreaterThan(0)
    expect(requestsTo('/Account/register')).toHaveLength(0)
  })

  it('marks the e-mail and offers sign-in when the address already has an account (409)', async () => {
    server.use(endpoint('post', '/Account/register', () => errorResponse('EmailExists', 'Bu e-posta kayıtlı.', 409)))
    const { user } = renderWithProviders(<RegisterPage />, { authed: false, route: '/register' })

    await fill(user)

    expect(await screen.findByText('Bu e-posta ile bir hesap var.')).toBeInTheDocument()
    expect(screen.getByText('Bu adres kullanımda.')).toBeInTheDocument()
    expect(screen.getByLabelText('E-posta')).toHaveValue('deniz@kuzeylab.dev')
  })

  it('lists every policy message the API sent (400)', async () => {
    server.use(
      endpoint('post', '/Account/register', () =>
        HttpJson(
          {
            IsSuccess: false,
            StatusCode: 400,
            Messages: [
              { Key: 'A', Message: 'Şifre en az 8 karakter olmalı.' },
              { Key: 'B', Message: 'Şifre özel karakter içermeli.' },
            ],
          },
          400,
        ),
      ),
    )
    const { user } = renderWithProviders(<RegisterPage />, { authed: false, route: '/register' })

    await fill(user)

    expect(await screen.findByText('Hesap oluşturulamadı, 2 sorun var:')).toBeInTheDocument()
    expect(screen.getByText('Şifre özel karakter içermeli.')).toBeInTheDocument()
  })

  it('shows the invite-only state when the installation refuses open sign-up', async () => {
    server.use(endpoint('post', '/Account/register', () => errorResponse('RegistrationInviteOnly', 'Yalnız davetle.', 403)))
    const { user } = renderWithProviders(<RegisterPage />, { authed: false, route: '/register' })

    await fill(user)

    expect(await screen.findByRole('heading', { name: 'Bu kurulum yalnız davetle kayıt alıyor' })).toBeInTheDocument()
  })
})

function HttpJson(body: unknown, status: number) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}
