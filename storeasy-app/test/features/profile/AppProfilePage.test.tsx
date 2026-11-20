import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { AppProfilePage } from '@/features/profile/AppProfilePage'
import { setupContentApi } from '../../support/content'
import { appDetail, organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupContentApi()

function render() {
  return renderWithProviders(<AppProfilePage />, { route: '/apps/101/settings/profile', path: '/apps/:appId/settings/profile' })
}

describe('AppProfilePage', () => {
  it('sends the profile in the catalog vocabulary, unknown fields as null', async () => {
    const { user } = render()
    await screen.findByRole('heading', { name: 'Katalog profili' })

    await user.click(screen.getByRole('checkbox', { name: 'iphone' }))
    await user.click(screen.getByRole('checkbox', { name: 'ipad' }))
    await user.click(screen.getByRole('checkbox', { name: 'subscriptions' }))
    await user.type(screen.getByLabelText(/Dağıtım bölgeleri/), 'tr, de')
    await user.type(screen.getByLabelText(/Android hedef API seviyesi/), '36')
    await user.click(screen.getByRole('radio', { name: 'Oyun' }))
    await user.click(screen.getByRole('button', { name: 'Profili kaydet' }))

    expect(await screen.findByText(/Profil kaydedildi/)).toBeInTheDocument()
    expect(lastRequestTo('/Apps/profile')?.body).toEqual({
      OrganizationId: 7,
      AppId: 101,
      Platforms: null,
      DeviceFamilies: ['iphone', 'ipad'],
      Features: ['subscriptions'],
      Regions: ['TR', 'DE'],
      DeveloperRegion: null,
      AppType: 'game',
      BuildSdkVersion: null,
      TargetSdk: 36,
      IsLive: null,
    })
  })

  it('opens with the saved profile', async () => {
    testState.appDetail = appDetail({
      Profile: {
        Platforms: ['ios'],
        DeviceFamilies: ['iphone'],
        Features: ['subscriptions'],
        Regions: ['TR', 'DE'],
        DeveloperRegion: 'TR',
        AppType: 'game',
        BuildSdkVersion: '26.1',
        TargetSdk: 35,
        IsLive: true,
      },
    })

    render()
    await screen.findByRole('heading', { name: 'Katalog profili' })

    expect(screen.getByRole('checkbox', { name: 'ios' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'iphone' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'ipad' })).not.toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'subscriptions' })).toBeChecked()
    expect(screen.getByLabelText(/Dağıtım bölgeleri/)).toHaveValue('TR, DE')
    expect(screen.getByLabelText(/Android hedef API seviyesi/)).toHaveValue('35')
    expect(screen.getByRole('radio', { name: 'Oyun' })).toBeChecked()
    expect(screen.queryByText(/henüz geri göndermiyor/)).not.toBeInTheDocument()
  })

  it('refuses region codes that are not two letters before sending', async () => {
    const { user } = render()
    await screen.findByRole('heading', { name: 'Katalog profili' })

    await user.type(screen.getByLabelText(/Dağıtım bölgeleri/), 'TUR')
    await user.click(screen.getByRole('button', { name: 'Profili kaydet' }))

    expect(await screen.findByText('İki harfli ülke kodu kullan: TUR')).toBeInTheDocument()
    expect(lastRequestTo('/Apps/profile')).toBeUndefined()
  })

  it('is read only for a Viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    render()

    expect(await screen.findByRole('checkbox', { name: 'iphone' })).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Profili kaydet' })).not.toBeInTheDocument()
    expect(screen.getByText(/değişiklik için Editor ya da üstü rol gerekir/)).toBeInTheDocument()
  })
})
