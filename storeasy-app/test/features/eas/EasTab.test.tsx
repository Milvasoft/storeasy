import { configure, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { EasTab } from '@/features/eas/EasTab'
import { ExpoConnectionSection } from '@/features/eas/ExpoConnectionSection'
import { EasAfterBuild, EasPlatform } from '@/features/eas/types'
import { OrgRole } from '@/lib/api/types'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { EAS_APP_ID, easApp, easState, setupEasApi } from '../../support/eas'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupEasApi()

configure({ asyncUtilTimeout: 5000 })

const route = { route: `/apps/${EAS_APP_ID}/eas`, path: '/apps/:appId/eas', lang: 'en' as const }

describe('EAS tab', () => {
  it('lists builds with their state and links, and hands a finished one to the intake', async () => {
    const { user } = renderWithProviders(<EasTab appId={EAS_APP_ID} />, route)

    const table = await screen.findByRole('table', { name: 'EAS builds' })
    const finished = within(table).getAllByRole('row')[1]
    expect(within(finished).getByText('Finished')).toBeInTheDocument()
    expect(within(finished).getByRole('link', { name: 'expo.dev' })).toHaveAttribute('href', expect.stringContaining('/builds/'))
    expect(within(table).getByText('Started outside storeasy')).toBeInTheDocument()

    await user.click(within(finished).getByRole('button', { name: 'Send to intake' }))
    expect(await screen.findByText('Package sent to the build intake.')).toBeInTheDocument()
    expect(lastRequestTo('/eas/build/intake')?.body).toEqual({ OrganizationId: testState.organizations[0].Id, Id: 701 })
  })

  it('starts a build only after the typed confirmation, with the profile from eas.json', async () => {
    const { user } = renderWithProviders(<EasTab appId={EAS_APP_ID} />, route)

    await user.click(await screen.findByRole('button', { name: 'Start build' }))
    const dialog = await screen.findByRole('dialog', { name: 'Start an EAS build' })
    expect(within(dialog).getByText('Uses your EAS build quota.')).toBeInTheDocument()
    expect(await within(dialog).findByDisplayValue('production')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Type Pusula all to start'), 'Pusula')
    await user.click(within(dialog).getByRole('button', { name: 'Start' }))
    expect(within(dialog).getByText('Type the confirmation exactly.')).toBeInTheDocument()
    expect(requestsTo('/eas/build/start')).toHaveLength(0)

    await user.type(within(dialog).getByLabelText('Type Pusula all to start'), ' all')
    await user.click(within(dialog).getByRole('button', { name: 'Start' }))
    expect(await screen.findByText('Build requested from Expo.')).toBeInTheDocument()
    expect(lastRequestTo('/eas/build/start')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      AppId: EAS_APP_ID,
      Platform: EasPlatform.All,
      Profile: 'production',
      GitRef: 'main',
      Chain: { AfterBuild: EasAfterBuild.Intake, PrepareReviewPlan: true, SubmitTrack: null, SubmitReleaseStatus: null },
      Confirm: 'Pusula all',
    })
  })

  it('links an unlinked app to the suggested project', async () => {
    easState.app = easApp({ Link: null, Builds: [] })
    const { user } = renderWithProviders(<EasTab appId={EAS_APP_ID} />, route)

    await user.click(await screen.findByRole('button', { name: 'Link' }))
    expect(await screen.findByText('EAS project linked.')).toBeInTheDocument()
    expect(lastRequestTo('/eas/project')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      AppId: EAS_APP_ID,
      ProjectId: 'p-1',
    })
  })

  it('points to expo.dev when the project has no GitHub repository, and hides the start for viewers', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    easState.app = easApp({ Link: { ...easApp().Link!, GitHubRepository: null } })
    renderWithProviders(<EasTab appId={EAS_APP_ID} />, route)

    expect(await screen.findByRole('link', { name: 'Connect GitHub on expo.dev' })).toHaveAttribute(
      'href',
      'https://expo.dev/accounts/kuzeylab/projects/pusula/github',
    )
    expect(screen.queryByRole('button', { name: 'Start build' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Send to intake' })).not.toBeInTheDocument()
  })

  it('submits a finished Android build with EAS Submit to the chosen track after the typed confirmation, and lists submissions', async () => {
    const { user } = renderWithProviders(<EasTab appId={EAS_APP_ID} />, route)

    const submissions = await screen.findByRole('table', { name: 'EAS submissions' })
    expect(within(submissions).getByText('Package not found on Play')).toBeInTheDocument()
    expect(within(submissions).getByText('After build option')).toBeInTheDocument()

    const table = screen.getByRole('table', { name: 'EAS builds' })
    await user.click(within(table).getByRole('button', { name: 'EAS Submit' }))
    const dialog = await screen.findByRole('dialog', { name: 'Submit with EAS Submit' })
    await user.type(within(dialog).getByLabelText('Type Pusula Android to submit'), 'Pusula Android')
    await user.click(within(dialog).getByRole('button', { name: 'Submit' }))

    expect(await screen.findByText('EAS Submit started.')).toBeInTheDocument()
    expect(lastRequestTo('/eas/submission/start')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      Id: 701,
      Track: 'internal',
      ReleaseStatus: 'COMPLETED',
      Confirm: 'Pusula Android',
    })
  })

  it('saves the after build defaults of the app', async () => {
    const { user } = renderWithProviders(<EasTab appId={EAS_APP_ID} />, route)

    await user.click(await screen.findByRole('button', { name: 'Save defaults' }))
    expect(await screen.findByText('Defaults saved.')).toBeInTheDocument()
    expect(lastRequestTo('/eas/chain')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      AppId: EAS_APP_ID,
      Chain: { AfterBuild: EasAfterBuild.Intake, PrepareReviewPlan: true, SubmitTrack: null, SubmitReleaseStatus: null },
    })
  })
})

describe('Expo connection', () => {
  it('shows the token owner and saves a new token without showing it back', async () => {
    const { user } = renderWithProviders(<ExpoConnectionSection />, { route: '/connections', path: '/connections', lang: 'en' })

    expect(await screen.findByText('kuzeylab-ci (robot)')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Replace token' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(within(dialog).getByText('Paste the access token.')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Expo access token'), 'expo-robot-token-for-test')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Expo token saved.')).toBeInTheDocument()
    expect(requestsTo('/eas/connection').find((request) => request.method === 'POST')?.body).toEqual({
      OrganizationId: testState.organizations[0].Id,
      Token: 'expo-robot-token-for-test',
    })
    expect(screen.queryByText('expo-robot-token-for-test')).not.toBeInTheDocument()
  })
})
