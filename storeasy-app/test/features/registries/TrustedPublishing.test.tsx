import { configure, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RegistriesPage, RegistryPackagePage } from '@/features/registries/RegistriesPage'
import { isSetupRepository, isSetupWorkflow, looksLikeToken, registryLabel } from '@/features/registries/rules'
import { CiProvider, PackageRegistry } from '@/features/registries/types'
import { renderWithProviders } from '../../support/render'
import { NPM_PACKAGE_ID, registriesState, setupRegistriesApi, trustedPublishing } from '../../support/registries'
import { lastRequestTo, requestsTo } from '../../support/state'

setupRegistriesApi()

configure({ asyncUtilTimeout: 5000 })

function renderPackage() {
  return renderWithProviders(<RegistryPackagePage />, {
    route: `/registries/${NPM_PACKAGE_ID}`,
    path: '/registries/:packageId',
    lang: 'en',
  })
}

describe('trusted publishing wizard', () => {
  it('shows the registry values, the CI file and the check of the latest release', async () => {
    renderPackage()

    const section = await screen.findByRole('region', { name: 'Trusted publishing' })
    expect(await within(section).findByText('Does not match the setup', { selector: '.card *' })).toBeInTheDocument()
    const step1 = within(section).getByRole('article', { name: '1. On npm' })
    expect(within(step1).getByText('kuzeylab')).toBeInTheDocument()
    expect(within(step1).getByRole('link', { name: /Open the settings on npm/ })).toHaveAttribute(
      'href',
      'https://www.npmjs.com/package/@kuzeylab/pusula-sdk/access',
    )
    const step2 = within(section).getByRole('article', { name: '2. In your repository' })
    expect(within(step2).getByText(/id-token: write/)).toBeInTheDocument()
    expect(within(step2).getByRole('button', { name: 'Copy' })).toBeInTheDocument()
    expect(within(section).getByText(/\.github\/workflows\/manual\.yml/)).toBeInTheDocument()
  })

  it('checks the values, previews and saves the setup', async () => {
    const { user } = renderPackage()

    const section = await screen.findByRole('region', { name: 'Trusted publishing' })
    const repository = await within(section).findByLabelText(/Repository/, { selector: 'input' })
    await user.clear(repository)
    await user.type(repository, 'not a repo')
    await user.click(within(section).getByRole('button', { name: 'Save setup' }))
    expect(within(section).getByText('Give the repository as owner/repo (GitHub) or namespace/project (GitLab).')).toBeInTheDocument()
    expect(requestsTo('/registries/setup').filter((r) => r.method === 'PUT')).toHaveLength(0)

    await user.clear(repository)
    await user.type(repository, 'kuzeylab/pusula')
    await user.click(within(section).getByRole('button', { name: 'Show the steps' }))
    expect(lastRequestTo('/registries/setup')?.query).toMatchObject({
      Provider: String(CiProvider.GitHubActions),
      Repository: 'kuzeylab/pusula',
      Workflow: 'release.yml',
    })

    await user.click(within(section).getByRole('button', { name: 'Save setup' }))
    expect(await screen.findByText(/Setup saved/)).toBeInTheDocument()
    expect(requestsTo('/registries/setup').find((r) => r.method === 'PUT')?.body).toMatchObject({
      Provider: CiProvider.GitHubActions,
      Repository: 'kuzeylab/pusula',
      Workflow: 'release.yml',
      Environment: 'npm',
    })
  })

  it('says when a registry has no trusted publishing', async () => {
    registriesState.setup = trustedPublishing({
      Registry: PackageRegistry.Maven,
      Providers: [],
      Saved: false,
      Provider: null,
      RegistryFields: [],
      WorkflowText: null,
    })
    renderPackage()

    expect(await screen.findByText(/Maven Central does not offer trusted publishing/)).toBeInTheDocument()
  })
})

describe('token reminders', () => {
  it('lists reminders with the days left and refuses token values', async () => {
    const { user } = renderWithProviders(<RegistriesPage />, { route: '/registries', path: '/registries', lang: 'en' })

    const table = await screen.findByRole('table', { name: 'Token reminders' })
    expect(within(table).getByText('NuGet push key')).toBeInTheDocument()
    expect(within(table).getByText('2 days left')).toBeInTheDocument()
    expect(within(table).getByText('Trusted publishing removes this token.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Add reminder' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText(/^Name/), 'npm_A1b2C3d4E5f6G7h8I9j0K1l2')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(within(dialog).getAllByText(/storeasy never stores tokens/).length).toBeGreaterThan(0)
    expect(requestsTo('/registries/reminder')).toHaveLength(0)

    await user.clear(within(dialog).getByLabelText(/^Name/))
    await user.type(within(dialog).getByLabelText(/^Name/), 'npm publish token')
    await user.type(within(dialog).getByLabelText(/Expires on/), '2026-12-31')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Reminder saved.')).toBeInTheDocument()
    expect(lastRequestTo('/registries/reminder')?.body).toMatchObject({
      Label: 'npm publish token',
      ExpiryDate: '2026-12-31',
      Registry: PackageRegistry.Npm,
    })
  })
})

describe('phase 2 rules', () => {
  it('checks setup values, token shapes and the new registry names', () => {
    expect(isSetupRepository(CiProvider.GitHubActions, 'acme/lib')).toBe(true)
    expect(isSetupRepository(CiProvider.GitHubActions, 'acme/team/lib')).toBe(false)
    expect(isSetupRepository(CiProvider.GitLabCi, 'acme/team/lib')).toBe(true)
    expect(isSetupWorkflow(CiProvider.GitHubActions, 'release.yml')).toBe(true)
    expect(isSetupWorkflow(CiProvider.GitHubActions, '.github/workflows/release.yml')).toBe(false)
    expect(looksLikeToken('NPM_TOKEN in acme/lib')).toBe(false)
    expect(looksLikeToken('pypi-AgEIcHlwaS5vcmcCJGQ0')).toBe(true)
    expect(registryLabel(PackageRegistry.Pub)).toBe('pub.dev')
  })
})
