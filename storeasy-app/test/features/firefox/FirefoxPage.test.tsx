import { configure, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { FirefoxPage } from '@/features/firefox/FirefoxPage'
import { FirefoxAddonStatus, FirefoxChangeKind, FirefoxVersionChannel } from '@/features/firefox/types'
import { ConnectionsPage } from '@/pages/org/ConnectionsPage'
import { FIREFOX_STORE_APP_ID, firefoxAddon, firefoxState, setupFirefoxApi } from '../../support/firefox'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupFirefoxApi()

// These screens chain a dry run, a plan read and a render; under a loaded coverage run that can pass the default 1 s.
configure({ asyncUtilTimeout: 5000 })

function render(lang: 'tr' | 'en' = 'en') {
  return renderWithProviders(<FirefoxPage />, { route: '/apps/101/firefox?store=firefox', path: '/apps/:appId/firefox', lang })
}

describe('Firefox add-on', () => {
  it('shows the add-on, its users, rating, listing, versions and the packages intake received', async () => {
    render()

    const addon = await screen.findByRole('article', { name: 'Add-on' })
    expect(within(addon).getByRole('heading', { name: 'Pusula' })).toBeInTheDocument()
    expect(within(addon).getByText('4.5 (120 ratings)')).toBeInTheDocument()
    expect(within(addon).getByText('8,400')).toBeInTheDocument()
    expect(within(addon).getByRole('link', { name: 'Developer Hub' })).toHaveAttribute('href', firefoxState.addon.DeveloperHubUrl)
    expect(within(addon).getByText(/AMO has no statistics API/)).toBeInTheDocument()

    const listing = screen.getByRole('article', { name: 'Listing' })
    expect(within(listing).getByText('Pusula TR')).toBeInTheDocument()

    const packages = screen.getByRole('article', { name: 'Firefox packages' })
    expect(within(packages).getByText('pusula-2.4.0.xpi')).toBeInTheDocument()
    expect(within(packages).getByText('Blocked by the checks')).toBeInTheDocument()
    expect(within(packages).getByText(/looks minified or bundled/)).toBeInTheDocument()

    const versions = screen.getByRole('article', { name: 'Versions' })
    expect(within(versions).getByText('Faster search.')).toBeInTheDocument()
    expect(lastRequestTo('/firefox/addon')?.query).toMatchObject({ StoreAppId: String(FIREFOX_STORE_APP_ID) })
  })

  it('plans a listed version of an intake package with notes for the reviewer', async () => {
    const { user } = render()

    const form = await screen.findByRole('form', { name: 'Submit a new version' })
    await user.type(within(form).getByLabelText(/Release notes/), 'Faster tabs.')
    await user.type(within(form).getByLabelText(/Notes to the reviewer/), 'npm ci && npm run build')
    await user.type(within(form).getByLabelText(/Source code asset/), '55')
    await user.click(within(form).getByRole('button', { name: 'Plan' }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(lastRequestTo('/firefox/change')?.body).toMatchObject({
      Kind: FirefoxChangeKind.Version,
      SessionId: 9401,
      Channel: FirefoxVersionChannel.Listed,
      ReleaseNotes: [{ Locale: 'en-US', Value: 'Faster tabs.' }],
      ApprovalNotes: 'npm ci && npm run build',
      SourceAssetId: 55,
      DryRun: true,
    })
  })

  it('plans listing values per locale and the categories', async () => {
    const { user } = render()

    const listing = await screen.findByRole('article', { name: 'Listing' })
    const form = within(listing).getByRole('form', { name: 'Add to the plan' })
    await user.type(within(form).getByLabelText(/New value/), 'Find every tab.')
    await user.click(within(form).getByRole('button', { name: 'Add to the plan' }))
    await user.clear(within(listing).getByLabelText(/Categories/))
    await user.type(within(listing).getByLabelText(/Categories/), 'tabs, privacy-security')
    await user.click(within(listing).getByRole('button', { name: 'Plan the listing change' }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(lastRequestTo('/firefox/change')?.body).toMatchObject({
      Kind: FirefoxChangeKind.Listing,
      Listing: [{ Field: 'Summary', Locale: 'en-US', Value: 'Find every tab.' }],
      Categories: ['tabs', 'privacy-security'],
    })
  })

  it('plans removing a preview, and warns when Mozilla disabled the add-on', async () => {
    firefoxState.addon = firefoxAddon({ Status: FirefoxAddonStatus.Disabled })
    const { user } = render('tr')

    const media = await screen.findByRole('article', { name: 'Önizlemeler ve simge' })
    expect(screen.getByText(/Mozilla eklentiyi devre dışı bıraktı/)).toBeInTheDocument()
    await user.click(within(media).getByRole('checkbox', { name: 'Kaldır' }))
    await user.click(within(media).getByRole('button', { name: 'Seçilenlerin kaldırılmasını planla' }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(lastRequestTo('/firefox/change')?.body).toMatchObject({ Kind: FirefoxChangeKind.Previews, DeletePreviewIds: [77] })
  })
})

describe('Firefox connections', () => {
  it('adds an AMO API key after checking its shape', async () => {
    testState.connections = []
    const { user } = renderWithProviders(<ConnectionsPage />, { lang: 'en' })

    await user.click(await screen.findByRole('button', { name: 'Add a connection' }))
    await user.click(screen.getByRole('radio', { name: /Firefox Add-ons \(AMO API key\)/ }))
    await user.click(screen.getByRole('button', { name: 'Continue' }))
    await user.type(screen.getByLabelText('Connection name'), 'Kuzey Lab AMO')
    await user.type(screen.getByLabelText('JWT issuer'), '12345')
    await user.click(screen.getByRole('button', { name: 'Save and verify' }))
    expect(screen.getByText('The JWT issuer looks like user:12345:67.')).toBeInTheDocument()
    expect(requestsTo('/StoreConnections/connection')).toHaveLength(0)

    const secret = 'f0'.repeat(32)
    await user.clear(screen.getByLabelText('JWT issuer'))
    await user.type(screen.getByLabelText('JWT issuer'), 'user:12345:67')
    await user.click(screen.getByLabelText('JWT secret'))
    await user.paste(secret)
    await user.click(screen.getByRole('button', { name: 'Save and verify' }))

    expect(await screen.findByRole('heading', { name: 'Connection saved' })).toBeInTheDocument()
    const sent = lastRequestTo('/StoreConnections/connection')?.body as { Store: number; Secret: string }
    expect(sent.Store).toBe(StoreKind.Firefox)
    expect(JSON.parse(sent.Secret)).toEqual({ issuer: 'user:12345:67', secret })
  })
})
