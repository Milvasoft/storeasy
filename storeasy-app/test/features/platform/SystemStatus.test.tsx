import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RegistrationMode, UserType } from '@/lib/api/types'
import { FeatureFlagsPage } from '@/features/platform/FeatureFlagsPage'
import { platformTr } from '@/features/platform/i18n'
import { dependencyView, modeLabel, SystemPage } from '@/features/platform/SystemPage'
import { publicSettings } from '../../support/factories'
import { permissionToken, platformState, setupPlatformApi } from '../../support/platform'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupPlatformApi()

const MANAGER = { userType: UserType.Manager }

describe('system status', () => {
  it('maps the dependency states and modes', () => {
    expect(dependencyView({ Configured: false }, platformTr).label).toBe('Ayarlı değil')
    expect(dependencyView({ Configured: true, Healthy: false }, platformTr).tone).toBe('danger')
    expect(dependencyView({ Configured: true, Healthy: true }, platformTr).tone).toBe('success')
    expect(dependencyView({ Configured: true, Healthy: null }, platformTr).tone).toBe('warning')
    expect(modeLabel(RegistrationMode.InviteOnly, platformTr)).toBe('Yalnız davetle')
  })

  it('shows the services, the job queue and the optional features from system/status', async () => {
    renderWithProviders(<SystemPage />, MANAGER)

    const services = await screen.findByRole('region', { name: 'Hizmetler' })
    expect(await within(services).findByText('Bucket storeasy-assets yanıt vermedi.')).toBeInTheDocument()
    expect(within(services).getByText('PostgreSQL · 8 ms')).toBeInTheDocument()
    expect(within(services).getByText('Ayarlı değil')).toBeInTheDocument()
    const jobs = screen.getByRole('region', { name: 'Mağaza iş kuyruğu' })
    expect(within(jobs).getByText('41')).toBeInTheDocument()
    const features = screen.getByRole('region', { name: 'İsteğe bağlı özellikler' })
    expect(within(features).getByText('Google ile giriş').nextSibling).toHaveTextContent('Ayarlı değil')
    expect(screen.getByText('storeasy v1.4.2 · commit 3f9c2ab · Production')).toBeInTheDocument()
  })

  it('reads the catalog version through rules/platform-catalog', async () => {
    renderWithProviders(<SystemPage />, MANAGER)

    expect(await screen.findByText(/v1\.3\.0 · 84 kural/)).toBeInTheDocument()
    expect(requestsTo('/Rules/platform-catalog')).toHaveLength(1)
  })

  it('changes the registration mode after the confirmation', async () => {
    const { user } = renderWithProviders(<SystemPage />, MANAGER)

    await user.click(await screen.findByRole('radio', { name: /Yalnız davetle/ }))
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Kayıt Yalnız davetle olsun mu?' })
    expect(dialog).toHaveTextContent('Mevcut hesaplar ve oturumlar etkilenmez.')
    await user.click(within(dialog).getByRole('button', { name: 'Kayıt modunu değiştir' }))

    await waitFor(() => expect(lastRequestTo('/System/settings/registration-mode')?.body).toEqual({ Mode: RegistrationMode.InviteOnly }))
    expect(await screen.findByText('Kayıt artık Yalnız davetle.')).toBeInTheDocument()
  })

  it('warns harder before closing the registration', async () => {
    const { user } = renderWithProviders(<SystemPage />, MANAGER)

    await user.click(await screen.findByRole('radio', { name: /Kapalı/ }))
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    const dialog = await screen.findByRole('alertdialog', { name: 'Kayıt Kapalı olsun mu?' })
    expect(dialog).toHaveTextContent("Kayıt sayfası 'Kayıt kapalı' gösterir")
    await user.click(within(dialog).getByRole('button', { name: 'Vazgeç' }))
    expect(requestsTo('/System/settings/registration-mode')).toHaveLength(0)
  })

  it('returns to the configured mode', async () => {
    platformState.settings = {
      RegistrationMode: RegistrationMode.Closed,
      RegistrationModeSaved: true,
      ConfiguredRegistrationMode: RegistrationMode.Open,
    }
    const { user } = renderWithProviders(<SystemPage />, MANAGER)

    expect(await screen.findByText(/Burada kaydedildi; yapılandırmadaki değer Açık\./)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Yapılandırmadaki moda dön' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Yapılandırmadaki moda dön' }))

    await waitFor(() => expect(lastRequestTo('/System/settings/registration-mode')?.body).toEqual({ Mode: null }))
  })

  it('shows the mode read only without the settings permission', async () => {
    testState.publicSettings = publicSettings({ RegistrationMode: RegistrationMode.InviteOnly })
    renderWithProviders(<SystemPage />, { ...MANAGER, accessToken: permissionToken('SystemManagement.Status') })

    expect(await screen.findByText('Kayıt modunu değiştirmek için SystemManagement.Settings izni gerekir.')).toBeInTheDocument()
    expect(await screen.findByRole('radio', { name: /Yalnız davetle · Şu an/ })).toBeChecked()
    expect(screen.getByRole('radio', { name: /Kapalı/ })).toBeDisabled()
    expect(screen.getByText('RuleCatalogManagement.List izni gerekir.')).toBeInTheDocument()
    expect(requestsTo('/System/settings')).toHaveLength(0)
  })

  it('refuses the screen without the status permission', async () => {
    renderWithProviders(<SystemPage />, { ...MANAGER, accessToken: permissionToken('UserManagement.List') })

    expect(await screen.findByText(/SystemManagement\.Status/)).toBeInTheDocument()
    expect(requestsTo('/System/status')).toHaveLength(0)
  })
})

describe('feature flags', () => {
  it('says what each flag is and who changed it last', async () => {
    platformState.flags = [
      {
        Id: 2,
        Key: 'Studio.AiCaptions',
        IsEnabled: false,
        Reason: 'Kota',
        Description: 'Stüdyoda yapay zekâ ile başlık önerir.',
        LastModifierUserName: 'deniz@kuzeylab.dev',
        LastModificationDate: '2026-11-18T14:22:00Z',
      },
    ]
    renderWithProviders(<FeatureFlagsPage />, MANAGER)

    expect(await screen.findByText('Stüdyoda yapay zekâ ile başlık önerir.')).toBeInTheDocument()
    expect(screen.getByText(/^Son değiştiren deniz@kuzeylab\.dev · /)).toBeInTheDocument()
  })
})
