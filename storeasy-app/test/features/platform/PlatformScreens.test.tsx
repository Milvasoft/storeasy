import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UserType } from '@/lib/api/types'
import { FilterType } from '@/lib/api/list'
import { ActivityLogsPage, activityLogsRequest } from '@/features/platform/ActivityLogsPage'
import { FeatureFlagsPage, flagCommand } from '@/features/platform/FeatureFlagsPage'
import { platformTr } from '@/features/platform/i18n'
import { LanguagesPage } from '@/features/platform/LanguagesPage'
import { healthView, SystemPage } from '@/features/platform/SystemPage'
import { endpoint } from '../../support/handlers'
import { permissionToken, platformState, setupPlatformApi } from '../../support/platform'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo } from '../../support/state'

setupPlatformApi()

const MANAGER = { userType: UserType.Manager }

describe('ActivityLogsPage', () => {
  it('builds the request from the address', () => {
    const request = activityLogsRequest(new URLSearchParams('range=today&activity=16&user=deniz&related=30'))
    expect(request.Sorting).toEqual({ SortBy: 'ActivityDate', Type: 1 })
    expect(request.Filtering?.Criterias.map((criteria) => [criteria.FilterBy, criteria.Type])).toEqual([
      ['ActivityDate', FilterType.Between],
      ['Activity', FilterType.EqualTo],
      ['UserName', FilterType.Contains],
      ['RelatedId', FilterType.EqualTo],
    ])
  })

  it('lists the actions with links to the related records and opens the detail', async () => {
    const { user } = renderWithProviders(<ActivityLogsPage />, MANAGER)

    expect(await screen.findByRole('link', { name: 'Kullanıcı #30' })).toHaveAttribute('href', '/admin/users/30')
    expect(screen.getByText('#25 kayıt silindi')).toBeInTheDocument()
    expect(screen.getByText('Sayfa 1 / 3 · Toplam: 46')).toBeInTheDocument()
    await user.click(screen.getByText('{"Lockout":true}'))
    const drawer = await screen.findByRole('dialog', { name: 'Kullanıcı güncellendi' })
    expect(within(drawer).getByText('Aktivite #900')).toBeInTheDocument()
  })

  it('filters by range, action and related record', async () => {
    const { user } = renderWithProviders(<ActivityLogsPage />, { ...MANAGER, route: '/admin/activity-logs?related=30' })

    await screen.findByRole('link', { name: 'Kullanıcı #30' })
    expect(lastRequestTo('/ActivityLogs')?.body).toMatchObject({ Filtering: { Criterias: [{ FilterBy: 'RelatedId', Value: '30' }] } })
    await user.click(screen.getByRole('button', { name: '7 gün' }))
    await expect.poll(() => JSON.stringify(lastRequestTo('/ActivityLogs')?.body)).toContain('ActivityDate')
    await user.click(screen.getByRole('button', { name: 'Filtreleri temizle' }))
    await expect.poll(() => (lastRequestTo('/ActivityLogs')?.body as { Filtering?: unknown }).Filtering).toBeUndefined()
  })

  it('shows the empty state', async () => {
    platformState.logs = []
    renderWithProviders(<ActivityLogsPage />, MANAGER)

    expect(await screen.findByRole('heading', { name: 'Henüz aktivite yok' })).toBeInTheDocument()
  })
})

describe('LanguagesPage', () => {
  it('locks the default language and an unsupported one cannot become the default', async () => {
    renderWithProviders(<LanguagesPage />, MANAGER)

    expect(await screen.findByRole('switch', { name: 'Destekleniyor: Türkçe' })).toBeDisabled()
    expect(screen.getByText('Varsayılan dil kapatılamaz')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Deutsch varsayılan dil olsun' })).toBeDisabled()
    expect(screen.getByText('Önce desteği aç')).toBeInTheDocument()
  })

  it('switches support without a confirmation', async () => {
    const { user } = renderWithProviders(<LanguagesPage />, MANAGER)

    await user.click(await screen.findByRole('switch', { name: 'Destekleniyor: Deutsch' }))

    await expect.poll(() => lastRequestTo('/Languages/language')?.body).toEqual({ Id: 23, Supported: { Value: true, IsUpdated: true } })
    expect(await screen.findByText('Dil kaydedildi.')).toBeInTheDocument()
  })

  it('changes the default language after the confirmation', async () => {
    const { user } = renderWithProviders(<LanguagesPage />, MANAGER)

    await user.click(await screen.findByRole('radio', { name: 'English varsayılan dil olsun' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Varsayılan dil değişsin mi?' })
    expect(within(confirm).getByText(/Varsayılan dil English olacak/)).toBeInTheDocument()
    await user.click(within(confirm).getByRole('button', { name: 'Varsayılan yap' }))

    await expect.poll(() => lastRequestTo('/Languages/language')?.body).toEqual({ Id: 22, IsDefault: { Value: true, IsUpdated: true } })
  })

  it('filters the supported languages and is read only without the update permission', async () => {
    const { user } = renderWithProviders(<LanguagesPage />, { ...MANAGER, accessToken: permissionToken('LanguageManagement.List') })

    expect(await screen.findByRole('switch', { name: 'Destekleniyor: English' })).toBeDisabled()
    expect(screen.getByText(/LanguageManagement.Update izni gerekir/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Yalnız desteklenenler' }))
    await expect.poll(() => JSON.stringify(lastRequestTo('/Languages')?.body)).toContain('Supported')
  })
})

describe('FeatureFlagsPage', () => {
  it('builds the command from the changed fields only', () => {
    const flag = { Id: 1, Key: 'Export', IsEnabled: true, Reason: null }
    expect(flagCommand(flag, true, '')).toBeNull()
    expect(flagCommand(flag, false, ' Kota ')).toEqual({
      Id: 1,
      IsEnabled: { Value: false, IsUpdated: true },
      Reason: { Value: 'Kota', IsUpdated: true },
    })
  })

  it('lists the flags with their state and reason', async () => {
    renderWithProviders(<FeatureFlagsPage />, MANAGER)

    expect(await screen.findByText('Excel dışa aktarma')).toBeInTheDocument()
    expect(screen.getByText('Yapay zekâ başlık önerileri')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Sağlayıcı kotası doldu')).toBeInTheDocument()
    expect(screen.getByText(/Sistem durumu > Kayıt ayarıdır/)).toBeInTheDocument()
  })

  it('needs a reason and a confirmation to turn a feature off', async () => {
    const { user } = renderWithProviders(<FeatureFlagsPage />, MANAGER)

    await user.click(await screen.findByRole('switch', { name: 'Excel dışa aktarma açık' }))
    expect(screen.getByText('Kaydedilmedi')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    expect(screen.getByText('Özelliğin neden kapatıldığını yaz.')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Excel dışa aktarma gerekçesi'), 'Depolama taşınıyor')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Excel dışa aktarma kapatılsın mı?' })
    expect(within(confirm).getByText('Gerekçe: Depolama taşınıyor')).toBeInTheDocument()
    await user.click(within(confirm).getByRole('button', { name: 'Özelliği kapat' }))

    await expect
      .poll(() => lastRequestTo('/FeatureFlags/featureFlag')?.body)
      .toEqual({
        Id: 1,
        IsEnabled: { Value: false, IsUpdated: true },
        Reason: { Value: 'Depolama taşınıyor', IsUpdated: true },
      })
    expect(await screen.findByText('Excel dışa aktarma kaydedildi.')).toBeInTheDocument()
  })

  it('turns a feature on without a confirmation and clears the reason', async () => {
    const { user } = renderWithProviders(<FeatureFlagsPage />, MANAGER)

    await user.click(await screen.findByRole('switch', { name: 'Yapay zekâ başlık önerileri açık' }))
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await expect
      .poll(() => lastRequestTo('/FeatureFlags/featureFlag')?.body)
      .toEqual({
        Id: 2,
        IsEnabled: { Value: true, IsUpdated: true },
        Reason: { Value: null, IsUpdated: true },
      })
  })

  it('puts a row back with cancel', async () => {
    const { user } = renderWithProviders(<FeatureFlagsPage />, MANAGER)

    await user.click(await screen.findByRole('switch', { name: 'Excel dışa aktarma açık' }))
    await user.click(screen.getByRole('button', { name: 'Vazgeç' }))

    expect(screen.queryByText('Kaydedilmedi')).not.toBeInTheDocument()
    expect(requestsTo('/FeatureFlags/featureFlag')).toHaveLength(0)
  })

  it('is read only without the update permission', async () => {
    renderWithProviders(<FeatureFlagsPage />, { ...MANAGER, accessToken: permissionToken('FeatureFlagManagement.List') })

    expect(await screen.findByRole('switch', { name: 'Excel dışa aktarma açık' })).toBeDisabled()
    expect(screen.getByText(/FeatureFlagManagement.Update izni gerekir/)).toBeInTheDocument()
  })
})

describe('SystemPage', () => {
  it('maps the health states', () => {
    expect(healthView('Healthy', platformTr).tone).toBe('success')
    expect(healthView('Degraded', platformTr).tone).toBe('warning')
    expect(healthView('Unhealthy', platformTr).label).toBe('Sağlıksız')
    expect(healthView(undefined, platformTr).label).toBe('Ulaşılamıyor')
  })

  it('shows the probes, the dependencies, the versions and the registration mode', async () => {
    renderWithProviders(<SystemPage />, MANAGER)

    expect(await screen.findByText(/^Hazırlık yoklaması 42 ms · Durum .* kontrol edildi · 30 saniyede bir yenilenir$/)).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Canlılık' })).toHaveTextContent('Ok')
    const checks = screen.getByRole('region', { name: 'Bağımlılıklar' })
    expect(within(checks).getByText('PostgreSQL')).toBeInTheDocument()
    expect(within(checks).getByText('Redis')).toBeInTheDocument()
    expect(screen.getByText('v9.9.9-test')).toBeInTheDocument()
    expect(await screen.findByText(/v1\.3\.0 · 84 kural/)).toBeInTheDocument()
    expect(screen.getByText('v1.4.2 · commit 3f9c2ab')).toBeInTheDocument()
    expect(await screen.findByRole('radio', { name: /Açık · Şu an/ })).toBeChecked()
    expect(screen.getByRole('radio', { name: /Yalnız davetle/ })).toBeEnabled()
  })

  it('shows a not ready probe without breaking the rest of the screen', async () => {
    platformState.ready = {
      status: 503,
      body: {
        status: 'Unhealthy',
        durationMs: 5003,
        checks: { database: { status: 'Healthy' }, redis: { status: 'Unhealthy', description: 'Redis yanıt vermedi.' } },
      } as never,
    }
    renderWithProviders(<SystemPage />, MANAGER)

    expect(await screen.findByText('Hazır değil (503)')).toBeInTheDocument()
    expect(screen.getByText('Redis yanıt vermedi.')).toBeInTheDocument()
    expect(screen.getByText('v9.9.9-test')).toBeInTheDocument()
  })

  it('says when the probes do not answer', async () => {
    server.use(
      endpoint('get', '/HealthCheck', () => Response.error()),
      endpoint('get', '/HealthCheck/ready', () => Response.error()),
    )
    renderWithProviders(<SystemPage />, MANAGER)

    expect(await screen.findByText('Yoklama yanıt vermedi.')).toBeInTheDocument()
    expect(screen.getAllByText('Ulaşılamıyor').length).toBeGreaterThan(0)
  })
})
