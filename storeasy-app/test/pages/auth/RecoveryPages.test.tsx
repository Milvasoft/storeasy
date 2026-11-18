import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { InvitePage } from '@/pages/auth/InvitePage'
import { ForgotPasswordPage, ResetPasswordPage } from '@/pages/auth/PasswordRecoveryPages'
import { VerifyEmailPage, VerifyEmailTokenPage } from '@/pages/auth/VerifyEmailPage'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { accountDetail } from '../../support/factories'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

describe('ForgotPasswordPage', () => {
  it('sends the address and says "if there is an account", then counts down the resend', async () => {
    const { user } = renderWithProviders(<ForgotPasswordPage />, { authed: false })

    await user.type(screen.getByLabelText('E-posta'), 'deniz@kuzeylab.dev')
    await user.click(screen.getByRole('button', { name: 'Bağlantı gönder' }))

    expect(await screen.findByRole('heading', { name: 'Gelen kutunu kontrol et' })).toBeInTheDocument()
    expect(screen.getByText(/kayıtlı bir hesap varsa/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Yeniden gönder · 1:00/ })).toBeDisabled()
    expect(lastRequestTo('/Account/password/forgot')?.body).toEqual({ Email: 'deniz@kuzeylab.dev' })
  })
})

describe('ResetPasswordPage', () => {
  it('saves the new password with the token and returns to sign-in with the "changed" note', async () => {
    const { user } = renderWithProviders(<ResetPasswordPage />, { authed: false, route: '/reset-password?token=t-1' })

    await user.type(screen.getByLabelText('Yeni şifre'), 'Kuzey!2026')
    await user.type(screen.getByLabelText('Yeni şifre tekrar'), 'Kuzey!2026')
    await user.click(screen.getByRole('button', { name: 'Şifreyi kaydet' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/login?reset=ok'))
    expect(lastRequestTo('/Account/password/reset')?.body).toEqual({ Token: 't-1', NewPassword: 'Kuzey!2026' })
  })

  it('compares the repeat field before sending', async () => {
    const { user } = renderWithProviders(<ResetPasswordPage />, { authed: false, route: '/reset-password?token=t-1' })

    await user.type(screen.getByLabelText('Yeni şifre'), 'Kuzey!2026')
    await user.type(screen.getByLabelText('Yeni şifre tekrar'), 'Kuzey!2027')
    await user.click(screen.getByRole('button', { name: 'Şifreyi kaydet' }))

    expect(screen.getByText('İki şifre aynı değil.')).toBeInTheDocument()
    expect(requestsTo('/Account/password/reset')).toHaveLength(0)
  })

  it('shows the single "link no longer works" state for an invalid token', async () => {
    server.use(endpoint('post', '/Account/password/reset', () => errorResponse('AccountTokenInvalid', 'Geçersiz.', 400)))
    const { user } = renderWithProviders(<ResetPasswordPage />, { authed: false, route: '/reset-password?token=old' })

    await user.type(screen.getByLabelText('Yeni şifre'), 'Kuzey!2026')
    await user.type(screen.getByLabelText('Yeni şifre tekrar'), 'Kuzey!2026')
    await user.click(screen.getByRole('button', { name: 'Şifreyi kaydet' }))

    expect(await screen.findByRole('heading', { name: 'Bu bağlantı artık geçerli değil' })).toBeInTheDocument()
  })
})

describe('VerifyEmail pages', () => {
  it('verifies the token from the link once and leads to sign-in', async () => {
    renderWithProviders(<VerifyEmailTokenPage />, { authed: false, route: '/verify-email/abc', path: '/verify-email/:token' })

    expect(await screen.findByRole('heading', { name: 'E-postan doğrulandı' })).toBeInTheDocument()
    expect(requestsTo('/Account/email/verify')).toHaveLength(1)
    expect(lastRequestTo('/Account/email/verify')?.body).toEqual({ Token: 'abc' })
  })

  it('verifies the token of the e-mail link (/verify-email?token=) on the verify page', async () => {
    renderWithProviders(<VerifyEmailPage />, { authed: false, route: '/verify-email?token=mail-1', path: '/verify-email' })

    expect(await screen.findByRole('heading', { name: 'E-postan doğrulandı' })).toBeInTheDocument()
    expect(requestsTo('/Account/email/verify')).toHaveLength(1)
    expect(lastRequestTo('/Account/email/verify')?.body).toEqual({ Token: 'mail-1' })
  })

  it('asks a signed-out visitor to sign in for a new link when the token no longer works', async () => {
    server.use(endpoint('post', '/Account/email/verify', () => errorResponse('AccountTokenInvalid', 'Geçersiz bağlantı.', 400)))
    renderWithProviders(<VerifyEmailTokenPage />, { authed: false, route: '/verify-email/old', path: '/verify-email/:token' })

    expect(await screen.findByRole('heading', { name: 'Bu bağlantı artık geçerli değil' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Yeni bağlantı için giriş yap' })).toHaveAttribute('href', '/login')
  })

  it('sends a new link to the signed-in account when the token no longer works', async () => {
    server.use(endpoint('post', '/Account/email/verify', () => errorResponse('AccountTokenInvalid', 'Geçersiz bağlantı.', 400)))
    const { user } = renderWithProviders(<VerifyEmailTokenPage />, {
      route: '/verify-email/old',
      path: '/verify-email/:token',
      needsOrganization: false,
    })

    await user.click(await screen.findByRole('button', { name: 'Yeni bağlantı gönder' }))

    expect(await screen.findByText('Yeni bağlantıyı gönderdik. Öncekisi artık çalışmaz.')).toBeInTheDocument()
    expect(lastRequestTo('/Account/email/verify/resend')?.body).toEqual({})
    expect(lastRequestTo('/Account/email/verify/resend')?.headers.authorization).toBe('Bearer access-token')
  })

  it('reads the account again after a signed-in tab verified the link', async () => {
    renderWithProviders(<VerifyEmailTokenPage />, { route: '/verify-email/abc', path: '/verify-email/:token', needsOrganization: false })

    expect(await screen.findByText('Hesabın hazır. Artık organizasyon kurabilir ve mağaza bağlayabilirsin.')).toBeInTheDocument()
    await waitFor(() => expect(requestsTo('/Account/detail').length).toBeGreaterThan(1))
  })

  it('resends the link for the signed-in account and counts down', async () => {
    testState.account = accountDetail({ EmailConfirmed: false })
    const { user } = renderWithProviders(<VerifyEmailPage />, { route: '/verify-email', needsOrganization: false })

    await user.click(await screen.findByRole('button', { name: 'Yeniden gönder' }))

    expect(await screen.findByText('Yeni bağlantıyı gönderdik. Öncekisi artık çalışmaz.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Yeniden gönder · 1:00/ })).toBeDisabled()
    expect(screen.getByText('deniz@kuzeylab.dev')).toBeInTheDocument()
  })

  it('treats a 409 on resend as already verified', async () => {
    testState.account = accountDetail({ EmailConfirmed: false })
    server.use(
      endpoint('post', '/Account/email/verify/resend', () => {
        // The link was opened in another tab meanwhile.
        testState.account = accountDetail({ EmailConfirmed: true })
        return errorResponse('EmailAlreadyVerified', 'Zaten doğrulandı.', 409)
      }),
    )
    const { user } = renderWithProviders(<VerifyEmailPage />, { route: '/verify-email', needsOrganization: false })

    await user.click(await screen.findByRole('button', { name: 'Yeniden gönder' }))

    expect(await screen.findByRole('heading', { name: 'E-postan doğrulandı' })).toBeInTheDocument()
  })

  it('asks a signed-out visitor to sign in before resending', () => {
    renderWithProviders(<VerifyEmailPage />, { authed: false, route: '/verify-email' })

    expect(screen.getByRole('link', { name: 'Yeni bağlantı için giriş yap' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yeniden gönder' })).not.toBeInTheDocument()
  })
})

describe('InvitePage', () => {
  it('sends a guest to sign in or sign up, carrying the token', async () => {
    renderWithProviders(<InvitePage />, { authed: false, route: '/invite/tok-9', path: '/invite/:token' })

    expect(await screen.findByRole('link', { name: 'Giriş yap ve katıl' })).toHaveAttribute('href', '/login?returnUrl=%2Finvite%2Ftok-9')
    expect(screen.getByRole('link', { name: 'Hesap oluştur ve katıl' })).toHaveAttribute('href', '/register?invite=tok-9')
  })

  it('reads the token of the e-mail link (/invitation?token=)', async () => {
    renderWithProviders(<InvitePage />, { authed: false, route: '/invitation?token=tok-9', path: '/invitation' })

    expect(await screen.findByRole('link', { name: 'Giriş yap ve katıl' })).toHaveAttribute('href', '/login?returnUrl=%2Finvite%2Ftok-9')
  })

  it('accepts the invitation for a signed-in user and opens the organization', async () => {
    const { user } = renderWithProviders(<InvitePage />, { route: '/invite/tok-9', path: '/invite/:token', needsOrganization: false })

    await user.click(await screen.findByRole('button', { name: 'Katıl' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/apps'))
    expect(lastRequestTo('/Organizations/invitation/accept')?.body).toEqual({ Token: 'tok-9' })
    expect(await screen.findByText('Organizasyona katıldın')).toBeInTheDocument()
  })

  it('says the invitation is for another address and offers to switch account', async () => {
    server.use(
      endpoint('post', '/Organizations/invitation/accept', () =>
        errorResponse('InvitationEmailMismatch', 'Davet başka bir adres için.', 400),
      ),
    )
    const { user } = renderWithProviders(<InvitePage />, { route: '/invite/tok-9', path: '/invite/:token', needsOrganization: false })

    await user.click(await screen.findByRole('button', { name: 'Katıl' }))

    expect(await screen.findByRole('button', { name: 'Hesap değiştir' })).toBeInTheDocument()
  })
})
