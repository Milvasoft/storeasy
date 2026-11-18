import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { NotificationPreferencesPage } from '@/features/account-preferences/NotificationPreferencesPage'
import { NotificationCenterBell } from '@/features/notifications/NotificationCenterBell'
import { NotificationsPage } from '@/features/notifications/NotificationsPage'
import { actionPath, notificationTone, notificationTypeDescription, notificationTypeLabel } from '@/features/notifications/labels'
import { notificationsTr } from '@/features/notifications/i18n'
import { NotificationType } from '@/features/notifications/types'
import { activityEvent } from '../../support/factories'
import { notification, phase4State, setupPhase4Api } from '../../support/phase4'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo } from '../../support/state'

setupPhase4Api()

describe('notification labels', () => {
  it('names every kind and gives it a tone', () => {
    const kinds = Object.values(NotificationType)

    expect(kinds.map((kind) => notificationTypeLabel(kind, notificationsTr)).every(Boolean)).toBe(true)
    expect(kinds.map((kind) => notificationTypeDescription(kind, notificationsTr)).every(Boolean)).toBe(true)
    expect(new Set(kinds.map(notificationTone))).toEqual(new Set(['danger', 'warning', 'success', 'info', 'neutral']))
  })

  it('turns an action link into an app path or an external link', () => {
    expect(actionPath('/apps/1')).toEqual({ internal: true, href: '/apps/1' })
    expect(actionPath(`${window.location.origin}/reviews?id=2`)).toEqual({ internal: true, href: '/reviews?id=2' })
    expect(actionPath('https://example.test/x')).toEqual({ internal: false, href: 'https://example.test/x' })
    expect(actionPath('not a url')).toBeNull()
    expect(actionPath(null)).toBeNull()
  })
})

describe('NotificationCenterBell', () => {
  it('shows the unread count and the newest notifications', async () => {
    const { user } = renderWithProviders(<NotificationCenterBell />)

    const bell = await screen.findByRole('button', { name: 'Bildirimler, 1 okunmamış' })
    await user.click(bell)

    expect(await screen.findByRole('menuitem', { name: /Defter 1\.4 reddedildi/ })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Bildirim tercihleri' })).toHaveAttribute('href', '/account/notifications')
  })

  it('opens a notification: marks it read and goes to its record', async () => {
    const { user } = renderWithProviders(<NotificationCenterBell />)

    await user.click(await screen.findByRole('button', { name: 'Bildirimler, 1 okunmamış' }))
    await user.click(await screen.findByRole('menuitem', { name: /Defter 1\.4 reddedildi/ }))

    await expect.poll(() => screen.getByTestId('location').textContent).toBe('/apps/101/rejections/601')
    expect(lastRequestTo('/Account/notifications/seen')?.body).toMatchObject({ NotificationIdList: [1201], MarkAll: false })
  })

  it('marks every notification read', async () => {
    const { user } = renderWithProviders(<NotificationCenterBell />)

    await user.click(await screen.findByRole('button', { name: 'Bildirimler, 1 okunmamış' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Hepsini okundu yap' }))

    await expect.poll(() => lastRequestTo('/Account/notifications/seen')?.body).toMatchObject({ MarkAll: true })
  })

  it('counts again when notification.created arrives on the hub', async () => {
    const { activity } = renderWithProviders(<NotificationCenterBell />)
    await screen.findByRole('button', { name: 'Bildirimler, 1 okunmamış' })
    phase4State.notifications = [notification({ Id: 1300 }), ...phase4State.notifications]

    ;(activity as unknown as { push: (event: unknown) => void }).push(activityEvent({ Type: 'notification.created', NotificationId: 1300 }))

    expect(await screen.findByRole('button', { name: 'Bildirimler, 2 okunmamış' })).toBeInTheDocument()
  })
})

describe('NotificationsPage', () => {
  it('lists the notifications with their organization and kind', async () => {
    renderWithProviders(<NotificationsPage />, { route: '/notifications', path: '/notifications' })

    expect(await screen.findByText('Defter 1.4 reddedildi: Guideline 2.1.')).toBeInTheDocument()
    expect(screen.getByText('Pusula 2.4 TestFlight grubuna teslim edildi.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Aç' })).toHaveAttribute('href', '/apps/101/rejections/601')
    expect(screen.getByText('Bütün organizasyonlarından · 1 okunmamış')).toBeInTheDocument()
  })

  it('filters by kind and unread in the address', async () => {
    const { user } = renderWithProviders(<NotificationsPage />, { route: '/notifications', path: '/notifications' })
    await screen.findByText('Defter 1.4 reddedildi: Guideline 2.1.')

    await user.click(screen.getByRole('button', { name: 'İş bitti' }))
    await expect.poll(() => screen.queryByText('Defter 1.4 reddedildi: Guideline 2.1.')).toBeNull()
    await user.click(screen.getByRole('button', { name: /^Okunmamış/ }))

    expect(screen.getByTestId('location')).toHaveTextContent('type=42')
    expect(screen.getByTestId('location')).toHaveTextContent('unread=1')
    expect(lastRequestTo('/Account/notifications')?.body).toMatchObject({
      Filtering: { Criterias: [{ FilterBy: 'IsSeen', Value: false }, { FilterBy: 'Type', Value: NotificationType.JobSucceeded }] },
    })
  })

  it('marks the selected notifications read and deletes them', async () => {
    const { user } = renderWithProviders(<NotificationsPage />, { route: '/notifications', path: '/notifications' })
    await screen.findByText('Defter 1.4 reddedildi: Guideline 2.1.')

    await user.click(screen.getByRole('checkbox', { name: 'Sayfayı seç' }))
    const bar = screen.getByRole('region', { name: 'Seçim' })
    await user.click(within(bar).getByRole('button', { name: 'Okundu yap' }))
    expect(await screen.findByText('2 bildirim okundu sayıldı.')).toBeInTheDocument()
    await user.click(screen.getByRole('checkbox', { name: '"Defter 1.4 reddedildi: Guideline 2.1." seç' }))
    await user.click(within(screen.getByRole('region', { name: 'Seçim' })).getByRole('button', { name: 'Sil' }))
    await user.click(within(await screen.findByRole('alertdialog', { name: '1 bildirim silinsin mi?' })).getByRole('button', { name: 'Sil' }))

    expect(await screen.findByText('1 bildirim silindi.')).toBeInTheDocument()
    const deletes = requestsTo('/Account/notifications').filter((request) => request.method === 'DELETE')
    expect(deletes.at(-1)?.body).toMatchObject({ NotificationIdList: [1201], DeleteAll: false })
  })

  it('deletes every notification after a confirmation with the count', async () => {
    const { user } = renderWithProviders(<NotificationsPage />, { route: '/notifications', path: '/notifications' })

    await user.click(await screen.findByRole('button', { name: 'Hepsini sil' }))
    await user.click(within(await screen.findByRole('alertdialog', { name: 'Bütün 2 bildirim silinsin mi?' })).getByRole('button', { name: 'Sil' }))

    await expect
      .poll(() => requestsTo('/Account/notifications').find((request) => request.method === 'DELETE')?.body)
      .toMatchObject({ DeleteAll: true })
  })

  it('marks everything read from the header and says when the list is empty', async () => {
    const { user } = renderWithProviders(<NotificationsPage />, { route: '/notifications', path: '/notifications' })

    await user.click(await screen.findByRole('button', { name: 'Hepsini okundu yap' }))
    expect(await screen.findByText('Bütün bildirimler okundu sayıldı.')).toBeInTheDocument()
    phase4State.notifications = []
    await user.click(screen.getByRole('button', { name: 'Davet' }))

    expect(await screen.findByRole('heading', { name: 'Bildirim yok' })).toBeInTheDocument()
  })
})

describe('NotificationPreferencesPage', () => {
  it('saves a channel on every switch and says so', async () => {
    const { user } = renderWithProviders(<NotificationPreferencesPage />)

    await user.click(await screen.findByRole('switch', { name: 'E-posta: Yeni yorum' }))

    await expect.poll(() => screen.getAllByText('Kaydedildi').length).toBe(2)
    expect(lastRequestTo('/account/preferences')?.body).toEqual({ Kind: NotificationType.NewReview, InApp: true, Email: true, WebPush: false })
    expect(screen.getByRole('switch', { name: 'E-posta: Yeni yorum' })).toHaveAttribute('aria-checked', 'true')
  })

  it('keeps the required kinds and web push locked', async () => {
    renderWithProviders(<NotificationPreferencesPage />)

    expect(await screen.findByRole('switch', { name: 'Uygulama içi: Güvenlik ve bağlantılar' })).toBeDisabled()
    expect(screen.getByRole('switch', { name: 'Web push: Red' })).toBeDisabled()
    expect(screen.getByRole('link', { name: 'Bildirimler' })).toHaveAttribute('href', '/account/notifications')
    expect(requestsTo('/account/preferences')).toHaveLength(1)
  })
})
