import { configure, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { RegistriesPage, RegistryPackagePage } from '@/features/registries/RegistriesPage'
import { PackageRegistry } from '@/features/registries/types'
import { organization } from '../../support/factories'
import { renderApp, renderWithProviders } from '../../support/render'
import { NPM_PACKAGE_ID, setupRegistriesApi } from '../../support/registries'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupRegistriesApi()

configure({ asyncUtilTimeout: 5000 })

describe('package registries', () => {
  it('shows the summary with the CI run, provenance and open alerts', async () => {
    renderWithProviders(<RegistriesPage />, { route: '/registries', path: '/registries', lang: 'en' })

    const table = await screen.findByRole('table', { name: 'Tracked packages' })
    const npm = within(table).getByRole('row', { name: /@kuzeylab\/pusula-sdk/ })
    expect(within(npm).getByText('2.4.0')).toBeInTheDocument()
    expect(within(npm).getByRole('link', { name: 'Open run' })).toHaveAttribute(
      'href',
      'https://github.com/kuzeylab/pusula-sdk/actions/runs/4242',
    )
    expect(within(npm).getByText('Provenance')).toBeInTheDocument()
    expect(within(npm).getByText('1')).toBeInTheDocument()
    const nuget = within(table).getByRole('row', { name: /KuzeyLab.Pusula.Client/ })
    expect(within(nuget).getByText('Not offered')).toBeInTheDocument()
    expect(within(nuget).getByText('None')).toBeInTheDocument()
  })

  it('adds a package after checking its name for the registry', async () => {
    const { user } = renderWithProviders(<RegistriesPage />, { route: '/registries', path: '/registries', lang: 'en' })

    await user.click(await screen.findByRole('button', { name: 'Add package' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Package name'), 'Has Spaces')
    await user.click(within(dialog).getByRole('button', { name: 'Track' }))
    expect(within(dialog).getByText("This name does not follow the registry's naming rules.")).toBeInTheDocument()
    expect(requestsTo('/registries/package')).toHaveLength(0)

    await user.clear(within(dialog).getByLabelText('Package name'))
    await user.type(within(dialog).getByLabelText('Package name'), 'left-pad')
    await user.click(within(dialog).getByRole('button', { name: 'Track' }))

    expect(await screen.findByText('Package added; reading it from the registry.')).toBeInTheDocument()
    expect(lastRequestTo('/registries/package')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      Registry: PackageRegistry.Npm,
      Name: 'left-pad',
      AppId: null,
    })
  })

  it('hides the writes from a viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    renderWithProviders(<RegistriesPage />, { route: '/registries', path: '/registries', lang: 'en' })

    expect(await screen.findByRole('table', { name: 'Tracked packages' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add package' })).not.toBeInTheDocument()
    expect(screen.getByText(/Read only/)).toBeInTheDocument()
  })

  it('shows a package with its versions and alerts, syncs and removes it', async () => {
    const { user } = renderWithProviders(<RegistryPackagePage />, {
      route: `/registries/${NPM_PACKAGE_ID}`,
      path: '/registries/:packageId',
      lang: 'en',
    })

    expect(await screen.findByRole('heading', { name: '@kuzeylab/pusula-sdk', level: 1 })).toBeInTheDocument()
    const alerts = screen.getByRole('table', { name: 'Alerts' })
    expect(within(alerts).getByText('Security advisory · HIGH')).toBeInTheDocument()
    expect(within(alerts).getByRole('link', { name: /GHSA-abcd-1234-wxyz/ })).toHaveAttribute(
      'href',
      'https://osv.dev/vulnerability/GHSA-abcd-1234-wxyz',
    )
    await user.click(screen.getByRole('tab', { name: 'Versions' }))
    const versions = await screen.findByRole('table', { name: 'Versions' })
    expect(within(versions).getByText('2.4.0 (latest)')).toBeInTheDocument()
    expect(within(versions).getByText('Deprecated')).toBeInTheDocument()
    expect(lastRequestTo('/registries/package')?.query).toMatchObject({ TrackedPackageId: String(NPM_PACKAGE_ID) })

    await user.click(screen.getByRole('button', { name: 'Sync now' }))
    expect(await screen.findByText('Sync queued.')).toBeInTheDocument()
    expect(lastRequestTo('/registries/sync')?.body).toMatchObject({ TrackedPackageId: NPM_PACKAGE_ID })

    await user.click(screen.getByRole('button', { name: 'Stop tracking' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Stop tracking' }))
    expect(await screen.findByText('Package removed.')).toBeInTheDocument()
    expect(requestsTo('/registries/package').find((request) => request.method === 'DELETE')?.body).toMatchObject({
      TrackedPackageId: NPM_PACKAGE_ID,
    })
  })

  it('says the module is off when the feature switch is off', async () => {
    renderWithProviders(<RegistriesPage />, { route: '/registries', path: '/registries', lang: 'tr', flags: { Registries: false } })

    expect(await screen.findByRole('heading', { name: 'Paket depoları kapalı' })).toBeInTheDocument()
    expect(requestsTo('/registries/packages')).toHaveLength(0)
  })

  it('lists the module in the menu only while it is switched on', async () => {
    const on = await renderApp({ route: '/apps' })
    expect(
      await within(await screen.findByRole('complementary', { name: 'Ana menü' })).findByRole('link', { name: /Paket depoları/ }),
    ).toBeInTheDocument()
    on.unmount()

    await renderApp({ route: '/apps', flags: { Registries: false } })
    await screen.findByRole('heading', { name: 'Uygulamalar', level: 1 })
    expect(
      within(screen.getByRole('complementary', { name: 'Ana menü' })).queryByRole('link', { name: /Paket depoları/ }),
    ).not.toBeInTheDocument()
  })
})
