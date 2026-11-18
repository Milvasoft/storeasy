import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UserType } from '@/lib/api/types'
import { FilterType } from '@/lib/api/list'
import { platformTr } from '@/features/platform/i18n'
import { sessionChanges, userUpdateCommand } from '@/features/platform/UserForm'
import { UserDetailPage, UsersPage, usersRequest } from '@/features/platform/UsersPages'
import { endpoint, errorResponse } from '../../support/handlers'
import { permissionToken, platformState, platformUser, setupPlatformApi } from '../../support/platform'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo } from '../../support/state'

setupPlatformApi()

// The last create, update or delete (a refetch of the detail follows every write).
const lastWrite = (path: string) => requestsTo(path).filter((request) => request.method !== 'GET').at(-1)

const MANAGER = { userType: UserType.Manager }

function renderDetail(id = 30, options = {}) {
  return renderWithProviders(<UserDetailPage />, { ...MANAGER, route: `/admin/users/${id}`, path: '/admin/users/:userId', ...options })
}

describe('usersRequest', () => {
  it('searches one field, filters the type and the locked accounts', () => {
    const now = new Date('2026-11-18T14:00:00Z')
    expect(usersRequest(new URLSearchParams('q=kaan&field=Name&type=1&locked=1&page=2&size=50'), now)).toEqual({
      PageNumber: 2,
      RowCount: 50,
      Sorting: { SortBy: 'UserName', Type: 0 },
      Filtering: {
        Criterias: [
          { FilterBy: 'Name', Value: 'kaan', Type: FilterType.Contains },
          { FilterBy: 'UserType', Value: 1, Type: FilterType.EqualTo },
          { FilterBy: 'LockoutEnd', Value: '2026-11-18T14:00:00.000Z', Type: FilterType.GreaterThan },
        ],
      },
    })
    expect(usersRequest(new URLSearchParams()).Filtering).toBeUndefined()
  })
})

describe('user update command', () => {
  const user = platformUser()
  const draft = { type: UserType.Manager, email: user.UserName, name: 'Operasyon', surname: 'Hesabı', password: '', roleIds: [22], locked: false }

  it('sends only what changed', () => {
    expect(userUpdateCommand(user, draft)).toEqual({ Id: 30 })
    expect(userUpdateCommand(user, { ...draft, name: 'Ops', roleIds: [22, 21], password: 'Yeni-Sifre1', locked: true })).toEqual({
      Id: 30,
      Name: { Value: 'Ops', IsUpdated: true },
      RoleIdList: { Value: [22, 21], IsUpdated: true },
      NewPassword: { Value: 'Yeni-Sifre1', IsUpdated: true },
      Lockout: { Value: true, IsUpdated: true },
    })
  })

  it('lists the session-ending changes', () => {
    const command = userUpdateCommand(user, { ...draft, roleIds: [21], password: 'Yeni-Sifre1' })
    const names: Record<number, string> = { 21: 'Katalog editörü', 22: 'Destek' }
    expect(sessionChanges(command, user, (id) => names[id], platformTr)).toEqual([
      'Roller: Katalog editörü eklendi, Destek çıkarıldı',
      'Yeni şifre belirlendi',
    ])
    expect(sessionChanges(userUpdateCommand(user, { ...draft, name: 'X' }), user, String, platformTr)).toEqual([])
  })
})

describe('UsersPage', () => {
  it('lists every account with the system record, the lock and the current user marked', async () => {
    renderWithProviders(<UsersPage />, MANAGER)

    const rootRow = (await screen.findByRole('link', { name: 'rootuser' })).closest('tr')!
    expect(within(rootRow).getByText('Sistem kaydı')).toBeInTheDocument()
    expect(within(rootRow).getByText('Salt okunur')).toBeInTheDocument()
    expect(within(rootRow).queryByRole('button', { name: /Düzenle/ })).not.toBeInTheDocument()
    const opsRow = screen.getByRole('link', { name: 'ops@kuzeylab.dev' }).closest('tr')!
    expect(within(opsRow).getByText('Kilitli')).toBeInTheDocument()
    expect(within(opsRow).getByRole('button', { name: 'Sil: ops@kuzeylab.dev' })).toBeInTheDocument()
    expect(screen.getByText('#7 · sen')).toBeInTheDocument()
  })

  it('searches the chosen field and filters the locked accounts', async () => {
    const { user } = renderWithProviders(<UsersPage />, MANAGER)

    await screen.findByRole('link', { name: 'rootuser' })
    await user.type(screen.getByRole('searchbox', { name: 'E-posta ile ara' }), 'ops{Enter}')
    await expect
      .poll(() => (lastRequestTo('/Users')?.body as { Filtering?: { Criterias: unknown[] } }).Filtering?.Criterias)
      .toEqual([{ FilterBy: 'UserName', Value: 'ops', Type: FilterType.Contains }])
    await user.click(screen.getByRole('button', { name: 'Kilitli olanlar' }))
    await expect.poll(() => JSON.stringify(lastRequestTo('/Users')?.body)).toContain('LockoutEnd')
    expect(screen.getByText('1 filtre etkin')).toBeInTheDocument()
  })

  it('says when a search finds nobody and clears it', async () => {
    server.use(endpoint('patch', '/Users', () => Response.json({ IsSuccess: true, StatusCode: 200, Data: [], TotalDataCount: 0 })))
    const { user } = renderWithProviders(<UsersPage />, { ...MANAGER, route: '/admin/users?q=kaan' })

    expect(await screen.findByRole('heading', { name: '"kaan" için sonuç yok' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Aramayı temizle' }))
    expect(await screen.findByRole('heading', { name: 'Henüz kullanıcı yok' })).toBeInTheDocument()
  })

  it('creates a user with the e-mail as the user name and at least one role', async () => {
    const { user } = renderWithProviders(<UsersPage />, MANAGER)

    await user.click(await screen.findByRole('button', { name: 'Yeni kullanıcı' }))
    const dialog = await screen.findByRole('dialog', { name: 'Yeni kullanıcı' })
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))
    expect(within(dialog).getByText('En az bir rol seç.')).toBeInTheDocument()
    await user.type(within(dialog).getByLabelText('E-posta'), 'mert@kuzeylab.dev')
    await user.type(within(dialog).getByLabelText('Ad'), 'Mert')
    await user.type(within(dialog).getByLabelText('Şifre'), 'Guclu-Sifre1')
    await user.click(await within(dialog).findByText('Destek'))
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    await expect.poll(() => lastWrite('/Users/user')?.body).toEqual({
      UserType: UserType.Manager,
      UserName: 'mert@kuzeylab.dev',
      Email: 'mert@kuzeylab.dev',
      Name: 'Mert',
      Surname: null,
      Password: 'Guclu-Sifre1',
      RoleIdList: [22],
      AllowedNotifications: null,
    })
    expect(await screen.findByText('Kullanıcı oluşturuldu.')).toBeInTheDocument()
  })

  it('keeps the form open with the server message on 409', async () => {
    server.use(endpoint('post', '/Users/user', () => errorResponse('EmailExists', 'Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var.', 409)))
    const { user } = renderWithProviders(<UsersPage />, MANAGER)

    await user.click(await screen.findByRole('button', { name: 'Yeni kullanıcı' }))
    const dialog = await screen.findByRole('dialog', { name: 'Yeni kullanıcı' })
    await user.type(within(dialog).getByLabelText('E-posta'), 'ops@kuzeylab.dev')
    await user.type(within(dialog).getByLabelText('Şifre'), 'Guclu-Sifre1')
    await user.click(await within(dialog).findByText('Destek'))
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    expect(await within(dialog).findByText('Kullanıcı oluşturulamadı')).toBeInTheDocument()
    expect(within(dialog).getByText('Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var.')).toBeInTheDocument()
  })

  it('deletes a user after the confirmation', async () => {
    const { user } = renderWithProviders(<UsersPage />, MANAGER)

    await user.click(await screen.findByRole('button', { name: 'Sil: ops@kuzeylab.dev' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Kullanıcı silinsin mi?' })
    expect(within(dialog).getByText(/ops@kuzeylab.dev silinecek/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Sil' }))

    await expect.poll(() => lastWrite('/Users/user')?.query).toEqual({ UserId: '30' })
    expect(await screen.findByText('Kullanıcı silindi.')).toBeInTheDocument()
  })

  it('hides create, edit and delete without the permissions', async () => {
    renderWithProviders(<UsersPage />, { ...MANAGER, accessToken: permissionToken('UserManagement.List') })

    expect(await screen.findByText('rootuser')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'rootuser' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yeni kullanıcı' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Sil:/ })).not.toBeInTheDocument()
  })

  it('shows the 403 state without the list permission', async () => {
    renderWithProviders(<UsersPage />, { ...MANAGER, accessToken: permissionToken('RoleManagement.List') })

    expect(await screen.findByRole('heading', { name: 'Yetkin yok' })).toBeInTheDocument()
    expect(screen.getByText(/UserManagement.List izni gerekir/)).toBeInTheDocument()
    expect(requestsTo('/Users')).toHaveLength(0)
  })

  it('shows the 403 state the API answers', async () => {
    server.use(endpoint('patch', '/Users', () => errorResponse('Forbidden', 'Yetkiniz yok.', 403)))
    renderWithProviders(<UsersPage />, MANAGER)

    expect(await screen.findByRole('heading', { name: 'Yetkin yok' })).toBeInTheDocument()
  })

  it('is closed to app users', async () => {
    renderWithProviders(<UsersPage />)

    expect(await screen.findByText('Platform ekranları bu kurulumun yöneticisine açık.')).toBeInTheDocument()
  })
})

describe('UserDetailPage', () => {
  it('shows the identity, the lock, the roles, the recent actions and the audit', async () => {
    renderDetail()

    expect(await screen.findByRole('heading', { name: 'ops@kuzeylab.dev' })).toBeInTheDocument()
    expect(screen.getByText('Hesap kilitli')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Destek' })).toHaveAttribute('href', '/admin/roles/22')
    expect(await screen.findByText('Kullanıcı güncellendi')).toBeInTheDocument()
    expect(lastRequestTo('/ActivityLogs')?.body).toMatchObject({
      Filtering: { Criterias: [{ FilterBy: 'RelatedId', Value: '30' }, { FilterBy: 'Activity', Value: 1 }] },
    })
    expect(screen.getByRole('region', { name: 'Denetim' })).toHaveTextContent('deniz@kuzeylab.dev')
  })

  it('unlocks without a confirmation', async () => {
    const { user } = renderDetail()

    await user.click(await screen.findByRole('button', { name: 'Kilidi aç' }))

    await expect.poll(() => lastWrite('/Users/user')?.body).toEqual({ Id: 30, Lockout: { Value: false, IsUpdated: true } })
    expect(await screen.findByText('Kilit kaldırıldı.')).toBeInTheDocument()
  })

  it('locks after the confirmation', async () => {
    platformState.users = [platformUser()]
    const { user } = renderDetail()

    await user.click(await screen.findByRole('button', { name: 'Kilitle' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'ops@kuzeylab.dev kilitlensin mi?' })
    await user.click(within(dialog).getByRole('button', { name: 'Kilitle' }))

    await expect.poll(() => lastWrite('/Users/user')?.body).toEqual({ Id: 30, Lockout: { Value: true, IsUpdated: true } })
  })

  it('confirms the session-ending edits before saving', async () => {
    platformState.users = [platformUser()]
    const { user } = renderDetail()

    await user.click(await screen.findByRole('button', { name: 'Düzenle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Kullanıcıyı düzenle' })
    await user.click(await within(dialog).findByText('Katalog editörü'))
    expect(within(dialog).getByText('Değişti. Kaydedince kullanıcının oturumları kapanır.')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'ops@kuzeylab.dev kullanıcısının oturumları kapanacak' })
    expect(within(confirm).getByText('Roller: Katalog editörü eklendi')).toBeInTheDocument()
    await user.click(within(confirm).getByRole('button', { name: 'Kaydet ve oturumları kapat' }))

    await expect.poll(() => lastWrite('/Users/user')?.body).toEqual({ Id: 30, RoleIdList: { Value: [22, 21], IsUpdated: true } })
    expect(await screen.findByText('Kullanıcı kaydedildi.')).toBeInTheDocument()
  })

  it('asks before closing a form with unsaved changes', async () => {
    platformState.users = [platformUser()]
    const { user } = renderDetail()

    await user.click(await screen.findByRole('button', { name: 'Düzenle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Kullanıcıyı düzenle' })
    await user.type(within(dialog).getByLabelText('Ad'), 'x')
    expect(within(dialog).getByText('1 değişiklik')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Vazgeç' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Kaydedilmemiş değişiklikler silinsin mi?' })
    await user.click(within(confirm).getByRole('button', { name: 'Değişiklikleri sil' }))

    await expect.poll(() => screen.queryByRole('dialog', { name: 'Kullanıcıyı düzenle' })).toBeNull()
    expect(requestsTo('/Users/user').filter((request) => request.method === 'PUT')).toHaveLength(0)
  })

  it('shows a system record read only', async () => {
    renderDetail(1)

    expect(await screen.findByRole('heading', { name: 'rootuser' })).toBeInTheDocument()
    expect(screen.getByText(/Sistem kayıtları veritabanı tarafından korunur/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Düzenle' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Sil' })).not.toBeInTheDocument()
  })

  it('deletes from the detail and goes back to the list', async () => {
    const { user } = renderDetail()

    await user.click(await screen.findByRole('button', { name: 'Sil' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Sil' }))

    await expect.poll(() => screen.getByTestId('location').textContent).toBe('/admin/users')
  })

  it('shows the not found state', async () => {
    renderDetail(404)

    expect(await screen.findByRole('heading', { name: 'Kayıt bulunamadı' })).toBeInTheDocument()
  })
})
