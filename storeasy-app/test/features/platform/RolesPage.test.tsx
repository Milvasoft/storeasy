import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UserType } from '@/lib/api/types'
import { platformTr } from '@/features/platform/i18n'
import { actionLabel, groupPermissions, RolesPage } from '@/features/platform/RolesPage'
import { endpoint, errorResponse } from '../../support/handlers'
import { PERMISSIONS, permissionToken, setupPlatformApi } from '../../support/platform'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo } from '../../support/state'

setupPlatformApi()

// The last create, update or delete (a refetch of the detail follows every write).
const lastWrite = (path: string) => requestsTo(path).filter((request) => request.method !== 'GET').at(-1)

const MANAGER = { userType: UserType.Manager }

function renderRole(id: number, options = {}) {
  return renderWithProviders(<RolesPage />, { ...MANAGER, route: `/admin/roles/${id}`, path: '/admin/roles/:roleId', ...options })
}

describe('permission groups', () => {
  it('groups the catalog and leaves full access out', () => {
    expect(groupPermissions(PERMISSIONS).map((group) => [group.key, group.label, group.permissions.length])).toEqual([
      ['UserManagement', 'Kullanıcı yönetimi', 3],
      ['RoleManagement', 'Rol yönetimi', 1],
      ['RuleCatalogManagement', 'Mağaza kuralları kataloğu', 1],
    ])
    expect(actionLabel('Approve', platformTr)).toBe('Onayla')
    expect(actionLabel('Publish', platformTr)).toBe('Publish')
  })
})

describe('RolesPage', () => {
  it('lists the roles with the locked seed role', async () => {
    renderWithProviders(<RolesPage />, MANAGER)

    const superRow = (await screen.findByText('SuperAdmin')).closest('tr')!
    expect(within(superRow).getByText('Sistem kaydı')).toBeInTheDocument()
    expect(within(superRow).getByText('Tam yetki')).toBeInTheDocument()
    expect(within(superRow).getByText('Salt okunur')).toBeInTheDocument()
    expect(lastRequestTo('/Roles')?.body).toMatchObject({ Sorting: { SortBy: 'Name', Type: 0 } })
  })

  it('opens a role in a drawer with its grouped permissions and users', async () => {
    const { user } = renderWithProviders(<RolesPage />, { ...MANAGER, route: '/admin/roles', path: '/admin/roles/:roleId?' })

    await user.click(await screen.findByText('Katalog editörü'))
    const drawer = await screen.findByRole('dialog', { name: 'Katalog editörü' })
    expect(await within(drawer).findByText('Kullanıcı yönetimi')).toBeInTheDocument()
    expect(within(drawer).getByText('Listele')).toBeInTheDocument()
    expect(within(drawer).getByText('Onayla')).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'ece@kuzeylab.dev' })).toHaveAttribute('href', '/admin/users/22')
    expect(screen.getByTestId('location')).toHaveTextContent('/admin/roles/21')
  })

  it('shows the seed role read only', async () => {
    renderRole(1)

    const drawer = await screen.findByRole('dialog', { name: 'SuperAdmin' })
    expect(await within(drawer).findByText('Sistem kaydı')).toBeInTheDocument()
    expect(within(drawer).queryByRole('button', { name: 'Düzenle' })).not.toBeInTheDocument()
    expect(within(drawer).queryByRole('button', { name: 'Sil' })).not.toBeInTheDocument()
  })

  it('edits the permissions with the grouped list and a group select all', async () => {
    const { user } = renderRole(21)

    await user.click(await within(await screen.findByRole('dialog', { name: 'Katalog editörü' })).findByRole('button', { name: 'Düzenle' }))
    const form = await screen.findByRole('dialog', { name: 'Rolü düzenle' })
    expect(await within(form).findByText('2 izin seçili')).toBeInTheDocument()
    expect(within(form).getByText('1 / 3 seçili')).toBeInTheDocument()
    await user.click(within(form).getAllByText('Tümünü seç')[0])
    expect(within(form).getByText('3 / 3 seçili')).toBeInTheDocument()
    await user.type(within(form).getByRole('searchbox', { name: 'İzin ara' }), 'rol')
    expect(within(form).queryByText('Kullanıcı yönetimi')).not.toBeInTheDocument()
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }))

    await expect.poll(() => lastWrite('/Roles/role')?.body).toEqual({ Id: 21, PermissionIdList: { Value: [2, 6, 3, 4], IsUpdated: true } })
    expect(await screen.findByText('Rol kaydedildi.')).toBeInTheDocument()
  })

  it('asks before giving full access', async () => {
    const { user } = renderRole(21)

    await user.click(await within(await screen.findByRole('dialog', { name: 'Katalog editörü' })).findByRole('button', { name: 'Düzenle' }))
    const form = await screen.findByRole('dialog', { name: 'Rolü düzenle' })
    await user.click(await within(form).findByRole('switch', { name: 'Tam yetki' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Tam yetki verilsin mi?' })
    await user.click(within(confirm).getByRole('button', { name: 'Tam yetkiyi aç' }))
    expect(within(form).getByText('Tam yetki açıkken etkisiz.')).toBeInTheDocument()
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }))

    await expect.poll(() => lastWrite('/Roles/role')?.body).toEqual({ Id: 21, PermissionIdList: { Value: [2, 6, 1], IsUpdated: true } })
  })

  it('creates a role with an idempotency key', async () => {
    const { user } = renderWithProviders(<RolesPage />, MANAGER)

    await user.click(await screen.findByRole('button', { name: 'Yeni rol' }))
    const form = await screen.findByRole('dialog', { name: 'Yeni rol' })
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }))
    expect(within(form).getByText('Rol adını yaz.')).toBeInTheDocument()
    await user.type(within(form).getByLabelText('Rol adı'), 'Destek')
    await user.click(await within(form).findByText('Detay'))
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }))

    await expect.poll(() => lastWrite('/Roles/role')?.body).toEqual({ Name: 'Destek', PermissionIdList: [3] })
    expect(lastWrite('/Roles/role')?.headers['idempotency-key']).toBeTruthy()
    await expect.poll(() => screen.getByTestId('location').textContent).toBe('/admin/roles/24')
  })

  it('edits only the name without the permission catalog right', async () => {
    const { user } = renderRole(21, { accessToken: permissionToken('RoleManagement.List', 'RoleManagement.Detail', 'RoleManagement.Update') })

    await user.click(await within(await screen.findByRole('dialog', { name: 'Katalog editörü' })).findByRole('button', { name: 'Düzenle' }))
    const form = await screen.findByRole('dialog', { name: 'Rolü düzenle' })
    expect(within(form).getByText(/İzinleri görmek için izin kataloğu yetkisi gerekir/)).toBeInTheDocument()
    expect(requestsTo('/Permissions')).toHaveLength(0)
    expect(screen.queryByRole('button', { name: 'Sil' })).not.toBeInTheDocument()
  })

  it('offers a retry when the catalog fails', async () => {
    server.use(endpoint('patch', '/Permissions', () => errorResponse('Error', 'Sunucu hatası.', 500)))
    const { user } = renderWithProviders(<RolesPage />, MANAGER)

    await user.click(await screen.findByRole('button', { name: 'Yeni rol' }))
    expect(await screen.findByText('İzin kataloğu yüklenemedi.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tekrar dene' })).toBeInTheDocument()
  })

  it('deletes a role after the confirmation', async () => {
    const { user } = renderRole(21)

    await user.click(await within(await screen.findByRole('dialog', { name: 'Katalog editörü' })).findByRole('button', { name: 'Sil' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Rol silinsin mi?' })
    expect(within(confirm).getByText(/Katalog editörü rolü silinecek/)).toBeInTheDocument()
    await user.click(within(confirm).getByRole('button', { name: 'Sil' }))

    await expect.poll(() => lastWrite('/Roles/role')?.query).toEqual({ RoleId: '21' })
    expect(await screen.findByText('Rol silindi.')).toBeInTheDocument()
  })

  it('shows the no results state of a search', async () => {
    server.use(endpoint('patch', '/Roles', () => Response.json({ IsSuccess: true, StatusCode: 200, Data: [] })))
    renderWithProviders(<RolesPage />, { ...MANAGER, route: '/admin/roles?q=xyz' })

    expect(await screen.findByRole('heading', { name: '"xyz" ile eşleşen rol yok' })).toBeInTheDocument()
  })
})
