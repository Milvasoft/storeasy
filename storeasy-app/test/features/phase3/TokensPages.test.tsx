import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { McpSetupPage } from '@/features/mcp/McpSetupPage'
import { TokensPage } from '@/features/tokens/TokensPage'
import { organization } from '../../support/factories'
import { phase3State, setupPhase3Api } from '../../support/phase3'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupPhase3Api()

function renderTokens(route = '/developer') {
  return renderWithProviders(<TokensPage />, { route, path: '/developer' })
}

describe('TokensPage', () => {
  it('lists the tokens with prefix, organization, access and an expired row', async () => {
    renderTokens()

    expect(await screen.findByText('Claude Code · dizüstü')).toBeInTheDocument()
    expect(screen.getByText('sea_9c1…')).toBeInTheDocument()
    expect(screen.getByText('Okuma ve yazma')).toBeInTheDocument()
    expect(screen.getByText(/Süresi doldu/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Eski deneme gibi yeni token oluştur' })).toBeInTheDocument()
  })

  it('shows the first-use state without tokens', async () => {
    phase3State.tokens = []
    renderTokens()

    expect(await screen.findByRole('heading', { name: 'Henüz token yok' })).toBeInTheDocument()
  })

  it('creates a token and shows the value once, closing only after the save is confirmed', async () => {
    const { user } = renderTokens('/developer?create=1')

    const dialog = await screen.findByRole('dialog', { name: 'Yeni token' })
    await user.type(within(dialog).getByLabelText('Ad'), 'Haftalık rapor')
    await user.click(within(dialog).getByRole('switch', { name: 'Salt okunur' }))
    await user.click(within(dialog).getByRole('radio', { name: '30 gün' }))
    await user.click(within(dialog).getByRole('button', { name: 'Token oluştur' }))

    const created = await screen.findByRole('dialog', { name: '"Haftalık rapor" hazır' })
    expect(within(created).getByText('sea_9c1f4b7e2a6d0c83f15a9b2e7d4c61aa0f3e8b52')).toBeInTheDocument()
    expect(lastRequestTo('/Tokens/token')?.body).toEqual({ OrganizationId: 7, Name: 'Haftalık rapor', ReadOnly: true, ExpiryDays: 30 })
    expect(lastRequestTo('/Tokens/token')?.headers['idempotency-key']).toBeTruthy()

    const done = within(created).getByRole('button', { name: 'Bitti' })
    expect(done).toBeDisabled()
    await user.click(within(created).getByRole('checkbox', { name: "Token'ı güvenli bir yere kopyaladım" }))
    await user.click(done)
    expect(screen.queryByRole('dialog', { name: '"Haftalık rapor" hazır' })).not.toBeInTheDocument()
  })

  it('locks read only for a viewer and keeps the form open on a refusal', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    const { user } = renderTokens('/developer?create=1')

    const dialog = await screen.findByRole('dialog', { name: 'Yeni token' })
    expect(within(dialog).getByRole('switch', { name: 'Salt okunur' })).toBeDisabled()
    expect(within(dialog).getByRole('switch', { name: 'Salt okunur' })).toHaveAttribute('aria-checked', 'true')
    await user.type(within(dialog).getByLabelText('Ad'), 'conflict')
    await user.click(within(dialog).getByRole('button', { name: 'Salt okunur token oluştur' }))

    expect(await within(dialog).findByText('Bu organizasyonda yalnız salt okunur token oluşturabilirsin.')).toBeInTheDocument()
  })

  it('revokes a token after a confirmation', async () => {
    const { user } = renderTokens()

    await user.click(await screen.findByRole('button', { name: 'Claude Code · dizüstü iptal et' }))
    const confirm = await screen.findByRole('alertdialog')
    expect(within(confirm).getByText(/sea_9c1… önekli token hemen çalışmaz/)).toBeInTheDocument()
    await user.click(within(confirm).getByRole('button', { name: 'İptal et' }))

    expect(await screen.findByText('Token iptal edildi.')).toBeInTheDocument()
    expect(lastRequestTo('/Tokens/token')?.body).toEqual({ TokenId: 51 })
  })

  it('renews an expired token with its name and organization', async () => {
    const { user } = renderTokens()

    await user.click(await screen.findByRole('button', { name: 'Eski deneme gibi yeni token oluştur' }))
    const dialog = await screen.findByRole('dialog', { name: 'Yeni token' })
    expect(within(dialog).getByLabelText('Ad')).toHaveValue('Eski deneme')
    expect(within(dialog).getByRole('switch', { name: 'Salt okunur' })).toHaveAttribute('aria-checked', 'true')
  })
})

describe('McpSetupPage', () => {
  it('shows the remote address and the bridge with the -p form', async () => {
    renderWithProviders(<McpSetupPage />, { route: '/developer/setup' })

    expect((await screen.findAllByText('http://localhost:3000/mcp')).length).toBeGreaterThan(0)
    expect(screen.getByText(/npx -y -p storeasy storeasy-mcp/)).toBeInTheDocument()
    expect(screen.queryByText(/npx -y storeasy-mcp/)).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Onay kuralları' })).toBeInTheDocument()
  })

  it('puts the chosen token prefix into the snippets, never a value', async () => {
    const { user } = renderWithProviders(<McpSetupPage />, { route: '/developer/setup' })

    await user.click(await screen.findByRole('button', { name: 'Parçacıklardaki token' }))
    await user.click(await screen.findByRole('option', { name: 'Claude Code · dizüstü · sea_9c1…' }))

    expect(screen.getByLabelText('Claude Code komut satırı')).toHaveTextContent('Bearer sea_9c1…')
  })

  it('asks for a token first when there is none', async () => {
    phase3State.tokens = []
    renderWithProviders(<McpSetupPage />, { route: '/developer/setup' })

    expect(await screen.findByText(/Önce kişisel erişim token'ı oluştur/)).toBeInTheDocument()
  })
})
