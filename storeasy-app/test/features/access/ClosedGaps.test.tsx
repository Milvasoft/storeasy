import { screen, waitFor, within } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { AppProfilePage } from '@/features/profile/AppProfilePage'
import { IntakeRoutes } from '@/features/intake/IntakeRoutes'
import { ArtifactSource } from '@/features/intake/types'
import { setupContentApi } from '../../support/content'
import { appDetail, TEST_ORG_ID } from '../../support/factories'
import { PART_URL, setupPhase3Api } from '../../support/phase3'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, testState } from '../../support/state'

setupContentApi()
setupPhase3Api()

describe('app profile', () => {
  it('opens with the saved profile', async () => {
    testState.appDetail = appDetail({
      Profile: {
        Platforms: ['ios'],
        DeviceFamilies: ['iphone'],
        Regions: ['TR', 'DE'],
        DeveloperRegion: 'TR',
        AppType: 'game',
        Features: ['subscriptions'],
        BuildSdkVersion: '26.1',
        TargetSdk: 35,
        IsLive: true,
      },
    })
    renderWithProviders(<AppProfilePage />, { route: '/apps/101/settings/profile', path: '/apps/:appId/settings/profile' })

    await screen.findByRole('heading', { name: 'Katalog profili' })
    expect(screen.getByRole('checkbox', { name: 'ios' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'iphone' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'ipad' })).not.toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'subscriptions' })).toBeChecked()
    expect(screen.getByLabelText(/Dağıtım bölgeleri/)).toHaveValue('TR, DE')
    expect(screen.getByLabelText(/Android hedef API seviyesi/)).toHaveValue('35')
    expect(screen.getByRole('radio', { name: 'Oyun' })).toBeChecked()
  })
})

describe('manual upload hold', () => {
  async function upload(keepHold: boolean) {
    server.use(http.put(PART_URL, () => new HttpResponse(null, { status: 200, headers: { ETag: '"etag-1"' } })))
    const { user } = renderWithProviders(<IntakeRoutes />, { route: '/intake/sessions', path: '/intake/*' })
    await user.click(await screen.findByRole('button', { name: 'Paket yükle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Paket yükle' })
    await user.click(within(dialog).getByRole('button', { name: /Uygulama/ }))
    await user.click(await screen.findByRole('option', { name: 'Pusula' }))
    const hold = within(dialog).getByRole('checkbox', { name: 'Teslimden önce onay bekle' })
    expect(hold).toBeChecked()
    if (!keepHold) {
      await user.click(hold)
    }
    await user.upload(dialog.querySelector('input[type="file"]') as HTMLInputElement, new File(['bytes'], 'pusula.aab'))
    await waitFor(() => expect(lastRequestTo('/Artifacts/4200/complete')).toBeDefined())
    return lastRequestTo('/apps/101/artifacts')?.body as Record<string, unknown>
  }

  it('holds the package for confirmation by default, in the organization of the upload', async () => {
    const open = await upload(true)

    expect(open).toMatchObject({ OrganizationId: TEST_ORG_ID, Source: ArtifactSource.Manual, Delivery: { HoldForConfirmation: true } })
    expect(lastRequestTo('/Artifacts/4200/complete')?.body).toMatchObject({ OrganizationId: TEST_ORG_ID })
  })

  it('lets the delivery rule run at once when the hold is turned off', async () => {
    const open = await upload(false)

    expect(open.Delivery).toBeNull()
  })
})
