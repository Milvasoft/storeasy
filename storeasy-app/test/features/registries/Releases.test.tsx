import { configure, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ChangePlanStatus } from '@/features/common/types'
import { OrgRole } from '@/lib/api/types'
import { PackageGroupsPage } from '@/features/registries/GroupsPage'
import { RegistriesPage, RegistryPackagePage } from '@/features/registries/RegistriesPage'
import { PackageRegistry, RegistryConnectionKind, RegistryPlanKind, ReleaseFollowStatus, ReleaseTrigger } from '@/features/registries/types'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { NPM_PACKAGE_ID, registriesState, registryPlan, setupRegistriesApi, trackedPackage } from '../../support/registries'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupRegistriesApi()

configure({ asyncUtilTimeout: 5000 })

const detail = { route: `/registries/${NPM_PACKAGE_ID}`, path: '/registries/:packageId', lang: 'en' as const }

describe('package releases', () => {
  it('plans a release, shows the exact change and approves only with the typed confirmation', async () => {
    const { user } = renderWithProviders(<RegistryPackagePage />, detail)

    await user.click(await screen.findByRole('tab', { name: 'Release' }))
    expect(await screen.findByText('2.4.0')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Minor 2\.5\.0/ }))
    await user.click(screen.getByRole('button', { name: 'Plan the release' }))

    const dialog = await screen.findByRole('dialog', { name: /Plan: Release @kuzeylab\/pusula-sdk 2\.4\.1/ })
    expect(lastRequestTo('/registries/release/plan')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      TrackedPackageId: NPM_PACKAGE_ID,
      Version: '2.5.0',
    })
    expect(
      within(dialog).getByText('- "version": "2.4.0",', { normalizer: (text) => text.replace(/\s+/g, ' ').trim() }),
    ).toBeInTheDocument()
    expect(
      within(dialog).getByText('+ "version": "2.4.1",', { normalizer: (text) => text.replace(/\s+/g, ' ').trim() }),
    ).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Type @kuzeylab/pusula-sdk 2.4.1 to approve'), '@kuzeylab/pusula-sdk')
    await user.click(within(dialog).getByRole('button', { name: 'Approve and start' }))
    expect(within(dialog).getByText('Type the confirmation exactly.')).toBeInTheDocument()
    expect(requestsTo('/registries/plan/approve')).toHaveLength(0)

    await user.type(within(dialog).getByLabelText('Type @kuzeylab/pusula-sdk 2.4.1 to approve'), ' 2.4.1')
    await user.click(within(dialog).getByRole('button', { name: 'Approve and start' }))
    expect(await screen.findByText('Approved; storeasy is starting it.')).toBeInTheDocument()
    expect(lastRequestTo('/registries/plan/approve')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      PlanId: 610,
      Confirm: '@kuzeylab/pusula-sdk 2.4.1',
    })
  })

  it('lets an editor plan but not approve, and shows the progress of started releases', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    registriesState.plans = [
      registryPlan({
        Id: 611,
        Status: ChangePlanStatus.Applied,
        FollowStatus: ReleaseFollowStatus.Following,
        RunUrl: 'https://github.com/kuzeylab/pusula-sdk/actions/runs/77',
      }),
      registryPlan({
        Id: 612,
        Title: 'Release @kuzeylab/pusula-sdk 2.4.0',
        Status: ChangePlanStatus.Applied,
        FollowStatus: ReleaseFollowStatus.Published,
      }),
    ]
    const { user } = renderWithProviders(<RegistryPackagePage />, detail)

    await user.click(await screen.findByRole('tab', { name: 'Release' }))
    const plans = await screen.findByRole('table', { name: 'Plans' })
    expect(within(plans).getByText('Waiting for the CI and the registry')).toBeInTheDocument()
    expect(within(plans).getByText('On the registry')).toBeInTheDocument()
    expect(within(plans).getByRole('link', { name: 'CI run' })).toHaveAttribute(
      'href',
      'https://github.com/kuzeylab/pusula-sdk/actions/runs/77',
    )

    await user.click(screen.getByRole('button', { name: 'Plan the release' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Approving needs the Release to production permission.')).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Approve and start' })).toBeDisabled()
  })

  it('suggests settings from the workflows and saves them', async () => {
    const { user } = renderWithProviders(<RegistryPackagePage />, detail)

    await user.click(await screen.findByRole('tab', { name: 'Release' }))
    await user.click(await screen.findByRole('button', { name: 'Suggest from the workflows' }))
    const suggestions = await screen.findByRole('region', { name: 'Suggestions' })
    expect(within(suggestions).getByText('sdk-publish.yml runs on tags sdk-node-v*.', { exact: false })).toBeInTheDocument()
    await user.click(within(suggestions).getByRole('button', { name: 'Use' }))
    expect(screen.getByLabelText('Tag pattern')).toHaveValue('sdk-node-v{version}')

    await user.click(screen.getByRole('button', { name: 'Save settings' }))
    expect(await screen.findByText('Release settings saved.')).toBeInTheDocument()
    expect(requestsTo('/registries/release').find((request) => request.method === 'PUT')?.body).toMatchObject({
      TrackedPackageId: NPM_PACKAGE_ID,
      Settings: { Repository: 'kuzeylab/pusula-sdk', Trigger: ReleaseTrigger.Tag, TagPattern: 'sdk-node-v{version}', VersionFiles: [] },
    })
  })

  it('plans an npm deprecation from the versions table', async () => {
    registriesState.connections = [
      ...registriesState.connections,
      { ...registriesState.connections[0], Id: 83, Kind: RegistryConnectionKind.Npm, Fingerprint: '****9f2a' },
    ]
    registriesState.plan = registryPlan({
      Kind: RegistryPlanKind.Deprecate,
      Title: 'Deprecate @kuzeylab/pusula-sdk 2.4.0',
      Files: [],
      Argument: 'Use 2.5',
    })
    const { user } = renderWithProviders(<RegistryPackagePage />, detail)

    await user.click(await screen.findByRole('tab', { name: 'Versions' }))
    await user.click(await screen.findByRole('button', { name: 'Registry actions 2.4.0' }))
    const dialog = await screen.findByRole('dialog', { name: 'Actions for 2.4.0' })
    await user.click(within(dialog).getByRole('button', { name: 'Plan' }))
    expect(within(dialog).getByText('Give the value.')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Deprecation message'), 'Use 2.5')
    await user.click(within(dialog).getByRole('button', { name: 'Plan' }))
    expect(await screen.findByRole('dialog', { name: /Plan: Deprecate/ })).toBeInTheDocument()
    expect(lastRequestTo('/registries/operation/plan')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      TrackedPackageId: NPM_PACKAGE_ID,
      Kind: RegistryPlanKind.Deprecate,
      Version: '2.4.0',
      Argument: 'Use 2.5',
    })
  })

  it('links PyPI packages to the project page instead of an action', async () => {
    registriesState.package = trackedPackage({ Registry: PackageRegistry.PyPI, Name: 'pusula' })
    const { user } = renderWithProviders(<RegistryPackagePage />, detail)

    await user.click(await screen.findByRole('tab', { name: 'Versions' }))
    await user.click(await screen.findByRole('button', { name: 'Registry actions 2.4.0' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('link', { name: 'Manage on PyPI' })).toHaveAttribute(
      'href',
      'https://pypi.org/manage/project/pusula/releases/',
    )
    expect(within(dialog).queryByRole('button', { name: 'Plan' })).not.toBeInTheDocument()
  })

  it('shows the group release in the group card', async () => {
    registriesState.release = { ...registriesState.release, TrackedPackageId: null, PackageGroupId: 5 }
    const { user } = renderWithProviders(<PackageGroupsPage />, { route: '/registries/groups', path: '/registries/groups', lang: 'en' })

    const card = await screen.findByRole('article')
    await user.click(within(card).getByRole('button', { name: 'Release' }))
    expect(await within(card).findByRole('button', { name: 'Plan the release' })).toBeInTheDocument()
    expect(lastRequestTo('/registries/release')?.query).toMatchObject({ PackageGroupId: String(registriesState.groups[0].Id) })
  })
})

describe('registry credentials', () => {
  it('saves a key without ever showing it back', async () => {
    const { user } = renderWithProviders(<RegistriesPage />, { route: '/registries', path: '/registries', lang: 'en' })

    const section = await screen.findByRole('region', { name: 'Credentials for releases and actions' })
    expect(await within(section).findByText('kuzeylab-bot')).toBeInTheDocument()
    expect(within(section).getByText('Verified')).toBeInTheDocument()

    await user.click(within(section).getByRole('button', { name: 'Add a credential' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(within(dialog).getByText('Paste the token or key.')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Token or key'), 'secret-value-for-test')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Credential saved.')).toBeInTheDocument()
    expect(lastRequestTo('/registries/connection')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      Kind: RegistryConnectionKind.GitHub,
      Secret: 'secret-value-for-test',
      ExpiryDate: null,
    })
    expect(screen.queryByText('secret-value-for-test')).not.toBeInTheDocument()
  })

  it('is read only without Manage connections', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    renderWithProviders(<RegistriesPage />, { route: '/registries', path: '/registries', lang: 'en' })

    const section = await screen.findByRole('region', { name: 'Credentials for releases and actions' })
    expect(within(section).queryByRole('button', { name: 'Add a credential' })).not.toBeInTheDocument()
    expect(within(section).getByText(/Manage connections permission/)).toBeInTheDocument()
  })
})
