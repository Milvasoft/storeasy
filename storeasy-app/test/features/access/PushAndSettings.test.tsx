import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { OrgRole, UserType } from '@/lib/api/types'
import { DEFAULT_FLAGS, serverFlags, useFeature } from '@/lib/features/FeatureFlagsContext'
import { applicationServerKey, PUSH_WORKER_SCOPE, PUSH_WORKER_URL } from '@/features/access/push'
import { NotificationPreferencesPage } from '@/features/account-preferences/NotificationPreferencesPage'
import { OrgSettingsPage } from '@/pages/org/OrgSettingsPage'
import { accountDetail, organization, orgSettings, publicSettings, TEST_ORG_ID } from '../../support/factories'
import { setupPhase4Api } from '../../support/phase4'
import { setupPlatformApi } from '../../support/platform'
import { renderApp, renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupPhase4Api()
setupPlatformApi()

const KEY = 'BEl62iUYgUivxIkv69yViEuiBIa-Ib9-SkvMeAtA3LFgDzkrxZJjSgSnfckjBJuBkr3qBUYIHBQFLXYp5Nksh8U'

describe('server feature flags', () => {
  it('maps the server keys to the client keys and ignores unknown ones', () => {
    expect(
      serverFlags([
        { Key: 'Studio.AiCaptions', IsEnabled: true },
        { Key: 'Export', IsEnabled: false },
        { Key: 'Reviews', IsEnabled: false },
      ]),
    ).toEqual({ StudioAiCaptions: true, Reviews: false })
    expect(serverFlags(null)).toEqual({})
    expect(DEFAULT_FLAGS.AccountSessions && DEFAULT_FLAGS.OrganizationRetention).toBe(true)
  })

  it('lets the server switch a section off in the signed in shell', async () => {
    testState.featureFlags = [{ Key: 'Reviews', IsEnabled: false }]
    await renderApp({ route: '/reviews' })

    expect(await screen.findByText('Yakında')).toBeInTheDocument()
    expect(requestsTo('/System/feature-flags')).toHaveLength(1)
  })

  it('keeps the client defaults when the server says nothing about a key', async () => {
    function Probe() {
      return <output>{String(useFeature('StudioAiCaptions'))}</output>
    }
    renderWithProviders(<Probe />)

    expect(await screen.findByText('false')).toBeInTheDocument()
  })
})

describe('platform permissions from the account detail', () => {
  it('reads the permissions account/detail lists instead of the token', async () => {
    testState.account = accountDetail({ UserType: UserType.Manager, Permissions: ['ActivityLogManagement.List'] })
    await renderApp({ route: '/admin/activity-logs', userType: UserType.Manager })

    expect(await screen.findByRole('heading', { name: 'Aktivite logu', level: 1 })).toBeInTheDocument()
    const menu = screen.getByRole('navigation', { name: /menü/i })
    expect(within(menu).queryByRole('link', { name: /Kullanıcılar/ })).not.toBeInTheDocument()
    expect(within(menu).queryByRole('link', { name: /Sistem durumu/ })).not.toBeInTheDocument()
  })
})

describe('organization retention and language', () => {
  it('sends only the changed field', async () => {
    const { user } = renderWithProviders(<OrgSettingsPage />)

    const form = await screen.findByRole('form', { name: 'Saklama ve dil' })
    expect(within(form).getByText('30 gün')).toBeInTheDocument()
    await user.click(within(form).getByRole('button', { name: /Denetim kaydı saklama/ }))
    await user.click(await screen.findByRole('option', { name: '730 gün' }))
    expect(within(form).getByText('1 değişiklik')).toBeInTheDocument()
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }))

    await waitFor(() =>
      expect(
        requestsTo('/Organizations/settings')
          .filter((request) => request.method === 'PUT')
          .at(-1)?.body,
      ).toEqual({ OrganizationId: TEST_ORG_ID, AuditRetentionDays: 730 }),
    )
    expect(await screen.findByText('Saklama ve dil kaydedildi.')).toBeInTheDocument()
  })

  it('changes the default language', async () => {
    testState.orgSettings = orgSettings({ DefaultLocale: null })
    const { user } = renderWithProviders(<OrgSettingsPage />)

    const form = await screen.findByRole('form', { name: 'Saklama ve dil' })
    await user.click(within(form).getByRole('button', { name: /Varsayılan dil/ }))
    await user.click(await screen.findByRole('option', { name: /de-DE/ }))
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }))

    await waitFor(() =>
      expect(
        requestsTo('/Organizations/settings')
          .filter((request) => request.method === 'PUT')
          .at(-1)?.body,
      ).toEqual({ OrganizationId: TEST_ORG_ID, DefaultLocale: 'de-DE' }),
    )
  })

  it('is read only for an admin', async () => {
    testState.organizations = [organization({ Role: OrgRole.Admin })]
    renderWithProviders(<OrgSettingsPage />)

    const form = await screen.findByRole('form', { name: 'Saklama ve dil' })
    expect(within(form).getByText('Bunları yalnız Sahip değiştirir.')).toBeInTheDocument()
    expect(within(form).queryByRole('button', { name: 'Kaydet' })).not.toBeInTheDocument()
  })

  it('is not read by a member without the manage members permission', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    renderWithProviders(<OrgSettingsPage />)

    await screen.findByRole('form', { name: 'Genel' })
    expect(screen.queryByRole('form', { name: 'Saklama ve dil' })).not.toBeInTheDocument()
    expect(requestsTo('/Organizations/settings')).toHaveLength(0)
  })
})

describe('browser push', () => {
  const subscription = {
    endpoint: 'https://fcm.googleapis.test/wp/abc',
    toJSON: () => ({ endpoint: 'https://fcm.googleapis.test/wp/abc', keys: { p256dh: 'p-key', auth: 'a-key' } }),
    unsubscribe: vi.fn(async () => true),
  }
  let current: typeof subscription | null = null
  const subscribe = vi.fn(async () => {
    current = subscription
    return subscription
  })
  const register = vi.fn(async () => registration)
  const registration = { pushManager: { getSubscription: async () => current, subscribe } }
  let permission: NotificationPermission = 'default'

  beforeEach(() => {
    current = null
    permission = 'default'
    vi.stubGlobal('PushManager', function PushManager() {})
    const notification = Object.defineProperties(function Notification() {}, {
      permission: { get: () => permission },
      requestPermission: {
        value: vi.fn(async () => {
          permission = 'granted'
          return permission
        }),
      },
    })
    vi.stubGlobal('Notification', notification)
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: { register, getRegistration: async () => (current ? registration : undefined) },
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    Reflect.deleteProperty(navigator, 'serviceWorker')
  })

  it('decodes the VAPID key', () => {
    expect(applicationServerKey('AQID_-8')).toEqual(new Uint8Array([1, 2, 3, 255, 239]))
  })

  it('says when the installation has no push keys', async () => {
    renderWithProviders(<NotificationPreferencesPage />)

    expect(await screen.findByText('Bu kurulumda anlık bildirimler ayarlı değil (Storeasy:WebPush).')).toBeInTheDocument()
  })

  it('subscribes this browser with the public key and hands the subscription to the API', async () => {
    testState.publicSettings = publicSettings({ WebPushPublicKey: KEY })
    const { user } = renderWithProviders(<NotificationPreferencesPage />)

    await user.click(await screen.findByRole('button', { name: 'Bu tarayıcıda aç' }))

    await waitFor(() =>
      expect(lastRequestTo('/Account/push/subscription')?.body).toMatchObject({
        Endpoint: 'https://fcm.googleapis.test/wp/abc',
        P256dh: 'p-key',
        Auth: 'a-key',
      }),
    )
    expect(register).toHaveBeenCalledWith(PUSH_WORKER_URL, { scope: PUSH_WORKER_SCOPE })
    expect(subscribe).toHaveBeenCalledWith({ userVisibleOnly: true, applicationServerKey: applicationServerKey(KEY) })
    expect(await screen.findByText('Bu tarayıcıda anlık bildirimler açıldı.')).toBeInTheDocument()
  })

  it('turns push off for this browser', async () => {
    testState.publicSettings = publicSettings({ WebPushPublicKey: KEY })
    current = subscription
    const { user } = renderWithProviders(<NotificationPreferencesPage />)

    await user.click(await screen.findByRole('button', { name: 'Bu tarayıcıda kapat' }))

    await waitFor(() => expect(lastRequestTo('/Account/push/subscription')?.body).toEqual({ Id: null, Endpoint: subscription.endpoint }))
    expect(subscription.unsubscribe).toHaveBeenCalled()
  })

  it('says the browser blocks notifications', async () => {
    testState.publicSettings = publicSettings({ WebPushPublicKey: KEY })
    permission = 'denied'
    renderWithProviders(<NotificationPreferencesPage />)

    expect(await screen.findByText(/Tarayıcı bu site için bildirimleri engelliyor/)).toBeInTheDocument()
  })

  it('sends a test push and removes another browser', async () => {
    testState.publicSettings = publicSettings({ WebPushPublicKey: KEY })
    testState.pushSubscriptions = [
      {
        Id: 7,
        PushServiceHost: 'fcm.googleapis.com',
        UserAgent: 'Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 Chrome/142.0 Safari/537.36',
        LastSuccessDate: null,
      },
    ]
    const { user } = renderWithProviders(<NotificationPreferencesPage />)

    await user.click(await screen.findByRole('button', { name: 'Deneme gönder' }))
    expect(await screen.findByText('Deneme bildirimi 1 tarayıcıya yola çıktı.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Chrome · Windows kaldır' }))

    await waitFor(() => expect(lastRequestTo('/Account/push/subscription')?.body).toEqual({ Id: 7, Endpoint: null }))
  })

  it('says when the browser cannot do push at all', async () => {
    vi.unstubAllGlobals()
    Reflect.deleteProperty(navigator, 'serviceWorker')
    renderWithProviders(<NotificationPreferencesPage />)

    expect(await screen.findByText('Bu tarayıcı anlık bildirimleri desteklemiyor.')).toBeInTheDocument()
  })
})
