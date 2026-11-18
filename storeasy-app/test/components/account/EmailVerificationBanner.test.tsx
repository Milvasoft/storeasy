import { act, fireEvent, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { EmailVerificationBanner } from '@/components/account/EmailVerificationBanner'
import { organizationsApi } from '@/lib/api/endpoints'
import { UserType } from '@/lib/api/types'
import { accountDetail } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderApp, renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

function render() {
  return renderWithProviders(<EmailVerificationBanner />, { needsOrganization: false })
}

describe('EmailVerificationBanner', () => {
  it('says what waits for the verification and resends the link to the signed-in account', async () => {
    testState.account = accountDetail({ EmailConfirmed: false })
    const { user } = render()

    expect(await screen.findByText('E-posta adresin henüz doğrulanmadı.')).toBeInTheDocument()
    expect(screen.getByText('deniz@kuzeylab.dev')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Bağlantıyı yeniden gönder' }))

    expect(await screen.findByText('Yeni bağlantıyı gönderdik. Öncekiler artık çalışmaz.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Bağlantıyı yeniden gönder · 1:00/ })).toBeDisabled()
    expect(lastRequestTo('/Account/email/verify/resend')?.body).toEqual({})
  })

  it('counts down the Retry-After of a 429', async () => {
    testState.account = accountDetail({ EmailConfirmed: false })
    server.use(
      endpoint('post', '/Account/email/verify/resend', () =>
        errorResponse('TooManyRequests', 'Çok fazla istek.', 429, { 'Retry-After': '120' }),
      ),
    )
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Bağlantıyı yeniden gönder' }))

    expect(await screen.findByRole('button', { name: /· 2:00/ })).toBeDisabled()
    expect(await screen.findByText('Çok fazla istek.')).toBeInTheDocument()
  })

  it('goes away when the address turns out to be verified already (409)', async () => {
    testState.account = accountDetail({ EmailConfirmed: false })
    server.use(
      endpoint('post', '/Account/email/verify/resend', () => {
        testState.account = accountDetail({ EmailConfirmed: true })
        return errorResponse('EmailAlreadyVerified', 'Zaten doğrulandı.', 409)
      }),
    )
    const { user } = render()

    await user.click(await screen.findByRole('button', { name: 'Bağlantıyı yeniden gönder' }))

    expect(await screen.findByText('E-posta adresin zaten doğrulanmış.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByText('E-posta adresin henüz doğrulanmadı.')).not.toBeInTheDocument())
  })

  it('reads the account again when the tab gets focus, so a link opened elsewhere clears it', async () => {
    testState.account = accountDetail({ EmailConfirmed: false })
    render()
    await screen.findByText('E-posta adresin henüz doğrulanmadı.')
    testState.account = accountDetail({ EmailConfirmed: true })

    fireEvent.focus(window)

    await waitFor(() => expect(screen.queryByText('E-posta adresin henüz doğrulanmadı.')).not.toBeInTheDocument())
  })

  it('shows up when any request is refused with EmailNotVerified, even with a stale account', async () => {
    server.use(endpoint('post', '/Organizations/organization', () => errorResponse('EmailNotVerified', 'Önce e-postanı doğrula.', 403)))
    render()
    await waitFor(() => expect(requestsTo('/Account/detail').length).toBeGreaterThan(0))
    expect(screen.queryByText('E-posta adresin henüz doğrulanmadı.')).not.toBeInTheDocument()

    await act(() => organizationsApi.create({ Name: 'Mavi', Slug: 'mavi' }).catch(() => undefined))

    expect(await screen.findByText('E-posta adresin henüz doğrulanmadı.')).toBeInTheDocument()
  })

  it('stays hidden for a verified account and for a platform manager', async () => {
    testState.account = accountDetail({ EmailConfirmed: false })
    renderWithProviders(<EmailVerificationBanner />, { needsOrganization: false, userType: UserType.Manager })
    await waitFor(() => expect(requestsTo('/Account/detail').length).toBeGreaterThan(0))

    expect(screen.queryByText('E-posta adresin henüz doğrulanmadı.')).not.toBeInTheDocument()
  })

  it('is part of the shell', async () => {
    testState.account = accountDetail({ EmailConfirmed: false })

    await renderApp({ route: '/apps' })

    expect(await screen.findByText('E-posta adresin henüz doğrulanmadı.')).toBeInTheDocument()
  })
})
