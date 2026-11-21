import { configure, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { WingetPage } from '@/features/winget/WingetPage'
import { WingetChangeKind } from '@/features/winget/types'
import { ConnectionsPage } from '@/pages/org/ConnectionsPage'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'
import { WINGET_STORE_APP_ID, setupWingetApi } from '../../support/winget'

setupWingetApi()

// These screens chain a dry run, a plan read and a render; under a loaded coverage run that can pass the default 1 s.
configure({ asyncUtilTimeout: 5000 })

function render(lang: 'tr' | 'en' = 'en') {
  return renderWithProviders(<WingetPage />, { route: '/apps/101/winget?store=winget', path: '/apps/:appId/winget', lang })
}

describe('winget package', () => {
  it('shows the package, its pull request waiting for the author and the manifests', async () => {
    render()

    expect(await screen.findByRole('heading', { name: 'KuzeyLab.Pusula' })).toBeInTheDocument()
    expect(screen.getByText('kuzeylab/winget-pkgs')).toBeInTheDocument()
    expect(screen.getByText('2.3.0, 2.2.1')).toBeInTheDocument()
    expect(screen.getByText(/winget has no store page, ratings, reviews or install statistics/)).toBeInTheDocument()

    const pull = screen.getByRole('article', { name: '#300512' })
    expect(within(pull).getAllByText('Needs your feedback').length).toBeGreaterThan(0)
    expect(within(pull).getByText(/Waiting for you/)).toBeInTheDocument()
    expect(within(pull).getByText('Needs-Author-Feedback')).toBeInTheDocument()
    expect(within(pull).getByText(/Please add the silent switch/)).toBeInTheDocument()
    expect(within(pull).getByRole('link', { name: 'Open on GitHub' })).toHaveAttribute(
      'href',
      'https://github.com/microsoft/winget-pkgs/pull/300512',
    )

    expect(await screen.findByText('The manifests use schema 1.10.0; storeasy writes 1.12.0.')).toBeInTheDocument()
    expect(screen.getAllByText('Offline maps.').length).toBeGreaterThan(0)
    expect(lastRequestTo('/winget/package')?.query).toMatchObject({ StoreAppId: String(WINGET_STORE_APP_ID) })
  })

  it('plans a new version from the Microsoft Store installers with release notes', async () => {
    const { user } = render()

    const form = await screen.findByRole('article', { name: 'New version' })
    await user.click(within(form).getByRole('button', { name: 'Prepare the pull request' }))
    expect(within(form).getByText('Give the version.')).toBeInTheDocument()
    expect(requestsTo('/winget/change')).toHaveLength(0)

    await user.type(within(form).getByLabelText('Version'), '2.4.0')
    await user.click(within(form).getByRole('checkbox', { name: 'Use the Microsoft Store installers of this app' }))
    await user.type(within(form).getByLabelText(/Release notes/), 'Faster maps.')
    await user.click(within(form).getByRole('button', { name: 'Prepare the pull request' }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    const sent = lastRequestTo('/winget/change')?.body as {
      Kind: number
      Version: string
      FromMicrosoftStore: boolean
      Installers: unknown[]
      Locales: Array<{ Locale: string; Fields: Record<string, string> }>
      DryRun: boolean
    }
    expect(sent).toMatchObject({
      Kind: WingetChangeKind.NewVersion,
      Version: '2.4.0',
      FromMicrosoftStore: true,
      DryRun: true,
      Installers: [],
    })
    expect(sent.Locales).toEqual([{ Locale: 'en-US', Fields: { ReleaseNotes: 'Faster maps.' } }])
  })

  it('plans locale text edits of the latest version', async () => {
    const { user } = render()

    const form = await screen.findByRole('article', { name: 'Locale texts' })
    const field = within(form).getByLabelText(/ShortDescription/)
    await user.clear(field)
    await user.type(field, 'Maps without a network.')
    await user.click(within(form).getByRole('button', { name: 'Prepare the pull request' }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(lastRequestTo('/winget/change')?.body).toMatchObject({
      Kind: WingetChangeKind.Listing,
      Version: '2.3.0',
      Locales: [{ Locale: 'en-US', Fields: { ShortDescription: 'Maps without a network.' } }],
    })
  })
})

describe('winget connections', () => {
  it('adds a GitHub token with the fork after checking their shape', async () => {
    testState.connections = []
    const { user } = renderWithProviders(<ConnectionsPage />, { lang: 'en' })

    await user.click(await screen.findByRole('button', { name: 'Add a connection' }))
    await user.click(screen.getByRole('radio', { name: /winget \(GitHub token and fork\)/ }))
    await user.click(screen.getByRole('button', { name: 'Continue' }))
    await user.type(screen.getByLabelText('Connection name'), 'Kuzey Lab winget')
    await user.type(screen.getByLabelText('GitHub token'), 'not-a-token')
    await user.click(screen.getByRole('button', { name: 'Save and verify' }))
    expect(screen.getByText(/Paste a GitHub personal access token/)).toBeInTheDocument()
    expect(requestsTo('/StoreConnections/connection')).toHaveLength(0)

    const token = `ghp_${'k9'.repeat(18)}`
    await user.clear(screen.getByLabelText('GitHub token'))
    await user.click(screen.getByLabelText('GitHub token'))
    await user.paste(token)
    await user.type(screen.getByLabelText('Your fork of winget-pkgs'), 'https://github.com/kuzeylab/winget-pkgs')
    await user.click(screen.getByRole('button', { name: 'Save and verify' }))

    expect(await screen.findByRole('heading', { name: 'Connection saved' })).toBeInTheDocument()
    const sent = lastRequestTo('/StoreConnections/connection')?.body as {
      Store: number
      Secret: string
      CredentialExpiryDate: string | null
    }
    expect(sent.Store).toBe(StoreKind.Winget)
    expect(JSON.parse(sent.Secret)).toEqual({ token, fork: 'kuzeylab/winget-pkgs' })
    expect(sent.CredentialExpiryDate).toBeNull()
  })
})
