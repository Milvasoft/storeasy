import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { tokenStore } from '@/lib/api/tokenStore'
import { AccountPage, SecurityPage } from '@/pages/account/AccountPage'
import { TEST_USER_NAME } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo } from '../../support/state'

describe('AccountPage', () => {
  it('shows the profile from account/detail and the organizations with the role', async () => {
    renderWithProviders(<AccountPage />, { route: '/account' })

    expect(await screen.findByDisplayValue('Arslan')).toBeInTheDocument()
    expect(screen.getByText('kuzey-lab')).toBeInTheDocument()
    expect(screen.getAllByText('Sahip').length).toBeGreaterThan(0)
  })

  it('switches the theme', async () => {
    const { user } = renderWithProviders(<AccountPage />, { route: '/account' })

    await user.click(await screen.findByRole('radio', { name: 'Koyu' }))

    expect(document.documentElement).toHaveClass('dark')
  })
})

describe('SecurityPage', () => {
  async function fill(user: ReturnType<typeof renderWithProviders>['user']) {
    await user.type(await screen.findByLabelText('Mevcut şifre'), 'Eski!2026')
    await user.type(screen.getByLabelText('Yeni şifre'), 'Yeni!2026x')
    await user.type(screen.getByLabelText('Yeni şifre tekrar'), 'Yeni!2026x')
    await user.click(screen.getByRole('button', { name: 'Şifreyi değiştir' }))
  }

  it('says every session ends, changes the password and signs out to the sign-in screen', async () => {
    const { user } = renderWithProviders(<SecurityPage />, { route: '/account/security' })

    await fill(user)
    const dialog = await screen.findByRole('alertdialog', { name: 'Şifren değişsin mi?' })
    await user.click(within(dialog).getByRole('button', { name: 'Şifreyi değiştir' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/login?reset=ok'))
    expect(lastRequestTo('/Account/password/change')?.body).toEqual({
      UserName: TEST_USER_NAME,
      OldPassword: 'Eski!2026',
      NewPassword: 'Yeni!2026x',
    })
    expect(tokenStore.get()).toBeNull()
  })

  it('shows a wrong current password above the form and keeps the session', async () => {
    server.use(endpoint('put', '/Account/password/change', () => errorResponse('WrongPassword', 'Mevcut şifre hatalı.', 400)))
    const { user } = renderWithProviders(<SecurityPage />, { route: '/account/security' })

    await fill(user)
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Şifreyi değiştir' }))

    expect(await screen.findByText('Mevcut şifre hatalı.')).toBeInTheDocument()
    expect(tokenStore.get()).not.toBeNull()
  })

  it('does nothing when the confirmation is cancelled', async () => {
    const { user } = renderWithProviders(<SecurityPage />, { route: '/account/security' })

    await fill(user)
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Vazgeç' }))

    expect(requestsTo('/Account/password/change')).toHaveLength(0)
  })
})
