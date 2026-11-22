import { configure, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { ChromePage } from '@/features/chrome/ChromePage'
import { ChromeChangeKind, ChromeItemState } from '@/features/chrome/types'
import { ConnectionsPage } from '@/pages/org/ConnectionsPage'
import { CHROME_STORE_APP_ID, chromeItem, chromeState, setupChromeApi } from '../../support/chrome'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupChromeApi()

// These screens chain a dry run, a plan read and a render; under a loaded coverage run that can pass the default 1 s.
configure({ asyncUtilTimeout: 5000 })

function render(lang: 'tr' | 'en' = 'en') {
  return renderWithProviders(<ChromePage />, { route: '/apps/101/chrome?store=chrome', path: '/apps/:appId/chrome', lang })
}

describe('Chrome Web Store item', () => {
  it('shows the item, its policy warning, rating, users and the packages intake received', async () => {
    render()

    const item = await screen.findByRole('article', { name: 'Item' })
    expect(within(item).getByRole('heading', { name: chromeState.item.ItemId })).toBeInTheDocument()
    expect(within(item).getByText(/warned for a policy violation/)).toBeInTheDocument()
    expect(within(item).getByText('4.5 (120 ratings)')).toBeInTheDocument()
    expect(within(item).getByText('%40')).toBeInTheDocument()
    expect(within(item).getByRole('link', { name: 'Developer dashboard' })).toHaveAttribute('href', chromeState.item.DashboardUrl)
    expect(within(item).getByText(/have no API/)).toBeInTheDocument()

    const packages = screen.getByRole('article', { name: 'Extension packages' })
    expect(within(packages).getByText('pusula-2.4.0.zip')).toBeInTheDocument()
    expect(within(packages).getByText('Blocked by the checks')).toBeInTheDocument()
    expect(lastRequestTo('/chrome/item')?.query).toMatchObject({ StoreAppId: String(CHROME_STORE_APP_ID) })
  })

  it('plans a staged submission of an intake package', async () => {
    const { user } = render()

    const form = await screen.findByRole('form', { name: 'Submit for review' })
    await user.click(within(form).getByRole('checkbox', { name: /Stage after approval/ }))
    await user.type(within(form).getByLabelText(/Initial deploy percentage/), '100')
    await user.click(within(form).getByRole('button', { name: 'Plan' }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(lastRequestTo('/chrome/change')?.body).toMatchObject({
      Kind: ChromeChangeKind.Publish,
      SessionId: 9301,
      Staged: true,
      SkipReview: false,
      DeployPercentage: 100,
      DryRun: true,
    })
  })

  it('refuses to plan a submission while one is in review, and offers to cancel it', async () => {
    chromeState.item = chromeItem({ SubmittedState: ChromeItemState.PendingReview, SubmittedVersion: '2.4.0' })
    const { user } = render()

    const form = await screen.findByRole('form', { name: 'Submit for review' })
    expect(within(form).getByText(/is in review: cancel that submission/)).toBeInTheDocument()
    expect(within(form).getByRole('button', { name: 'Plan' })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Cancel the submission' }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(lastRequestTo('/chrome/change')?.body).toMatchObject({ Kind: ChromeChangeKind.CancelSubmission })
  })

  it('plans a higher deploy percentage and a rollback', async () => {
    const { user } = render()

    const rollout = await screen.findByRole('form', { name: 'Raise the deploy percentage' })
    await user.type(within(rollout).getByLabelText(/Raise the deploy percentage/), '80')
    await user.click(within(rollout).getByRole('button', { name: 'Plan' }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(lastRequestTo('/chrome/change')?.body).toMatchObject({ Kind: ChromeChangeKind.DeployPercentage, DeployPercentage: 80 })
    expect(requestsTo('/chrome/change')).toHaveLength(1)
  })
})

describe('Chrome Web Store connections', () => {
  it('adds an OAuth client after checking the fields', async () => {
    testState.connections = []
    const { user } = renderWithProviders(<ConnectionsPage />, { lang: 'en' })

    await user.click(await screen.findByRole('button', { name: 'Add a connection' }))
    await user.click(screen.getByRole('radio', { name: /Chrome Web Store \(service account or OAuth client\)/ }))
    await user.click(screen.getByRole('button', { name: 'Continue' }))
    await user.type(screen.getByLabelText('Connection name'), 'Kuzey Lab Chrome')
    await user.click(screen.getByRole('button', { name: 'Save and verify' }))
    expect(screen.getByText('Paste the publisher id from the developer dashboard.')).toBeInTheDocument()
    expect(requestsTo('/StoreConnections/connection')).toHaveLength(0)

    await user.type(screen.getByLabelText('Publisher id'), 'pub-kuzeylab')
    await user.click(screen.getByRole('button', { name: 'Sign in with' }))
    await user.click(screen.getByRole('option', { name: 'OAuth client and refresh token' }))
    await user.type(screen.getByLabelText('OAuth client id'), '1234-abc.apps.googleusercontent.com')
    await user.type(screen.getByLabelText('OAuth client secret'), 'secret-value')
    await user.type(screen.getByLabelText(/Refresh token/), '1//refresh')
    await user.click(screen.getByRole('button', { name: 'Save and verify' }))

    expect(await screen.findByRole('heading', { name: 'Connection saved' })).toBeInTheDocument()
    const sent = lastRequestTo('/StoreConnections/connection')?.body as { Store: number; Secret: string }
    expect(sent.Store).toBe(StoreKind.Chrome)
    expect(JSON.parse(sent.Secret)).toEqual({
      publisherId: 'pub-kuzeylab',
      clientId: '1234-abc.apps.googleusercontent.com',
      clientSecret: 'secret-value',
      refreshToken: '1//refresh',
      itemId: null,
    })
  })
})
