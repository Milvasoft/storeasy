import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UserType } from '@/lib/api/types'
import { readGlobalPermissions } from '@/lib/auth/globalPermissions'
import { platformEn, platformTr } from '@/features/platform/i18n'
import { permissionToken, platformHandlers, setupPlatformApi } from '../../support/platform'
import { renderApp } from '../../support/render'

setupPlatformApi()

const MANAGER = { userType: UserType.Manager }
const menu = () => screen.getByRole('navigation', { name: /menü/i })

describe('global permissions', () => {
  it('reads the role claims of the access token', () => {
    expect(readGlobalPermissions(permissionToken('UserManagement.List', 'App.SuperAdmin'))).toEqual(
      new Set(['UserManagement.List', 'App.SuperAdmin']),
    )
    expect(readGlobalPermissions(permissionToken())).toEqual(new Set())
    expect(readGlobalPermissions('access-token')).toBeNull()
    expect(readGlobalPermissions(null)).toBeNull()
    expect(readGlobalPermissions('a.%%%.c')).toBeNull()
  })
})

describe('platform routes', () => {
  it('serves the users screen to a platform administrator', async () => {
    await renderApp({ ...MANAGER, route: '/admin/users' })

    expect(await screen.findByRole('heading', { name: 'Kullanıcılar', level: 1 })).toBeInTheDocument()
  })

  it('opens a user detail and a role drawer by address', async () => {
    await renderApp({ ...MANAGER, route: '/admin/roles/21' })

    expect(await screen.findByRole('dialog', { name: 'Katalog editörü' })).toBeInTheDocument()
  })

  it('keeps the designed page while the Platform flag is off', async () => {
    await renderApp({ ...MANAGER, route: '/admin/system', flags: { Platform: false } })

    expect(await screen.findByRole('heading', { name: 'Sistem durumu', level: 1 })).toBeInTheDocument()
    expect(screen.queryByText('Bağımlılıklar')).not.toBeInTheDocument()
  })

  it('filters the Platform menu by the global permissions', async () => {
    await renderApp({
      ...MANAGER,
      route: '/admin/activity-logs',
      accessToken: permissionToken('RuleCatalogManagement.List', 'ActivityLogManagement.List'),
    })

    await screen.findByRole('heading', { name: 'Aktivite logu', level: 1 })
    expect(within(menu()).getByRole('link', { name: /Aktivite logu/ })).toBeInTheDocument()
    expect(within(menu()).queryByRole('link', { name: /Sistem durumu/ })).not.toBeInTheDocument()
    expect(within(menu()).queryByRole('link', { name: /Roller/ })).not.toBeInTheDocument()
    expect(within(menu()).queryByRole('link', { name: /Kullanıcılar/ })).not.toBeInTheDocument()
  })

  it('shows every platform item to a full access administrator', async () => {
    await renderApp({ ...MANAGER, route: '/admin/system', accessToken: permissionToken('App.SuperAdmin') })

    await screen.findByRole('heading', { name: 'Sistem durumu', level: 1 })
    for (const name of [/Kullanıcılar/, /Roller/, /Aktivite logu/, /Diller/, /Özellik anahtarları/]) {
      expect(within(menu()).getByRole('link', { name })).toBeInTheDocument()
    }
  })

  it('refuses the screens to an app user', async () => {
    await renderApp({ route: '/admin/users' })

    expect(await screen.findByText('Platform ekranları bu kurulumun yöneticisine açık.')).toBeInTheDocument()
  })
})

const HELPER_METHOD: Record<string, string> = {
  apiGet: 'GET',
  apiPost: 'POST',
  apiPut: 'PUT',
  apiPatchList: 'PATCH',
  apiDelete: 'DELETE',
  apiProbe: 'GET',
}

describe('platform api contract', () => {
  const source = readFileSync(resolve(process.cwd(), 'src/features/platform/api.ts'), 'utf-8')
  const called = new Set(
    [...source.matchAll(/(api(?:Get|Post|Put|PatchList|Delete|Probe))(?:<[^>]*>)?\(\s*'(\/[^']+)'/g)].map(
      (match) => `${HELPER_METHOD[match[1]]} ${match[2]}`,
    ),
  )
  const stubbed = new Set(
    platformHandlers.map(
      (handler) => `${String(handler.info.method).toUpperCase()} ${String(handler.info.path).replace('*/api/v1.0', '')}`,
    ),
  )

  it('answers every endpoint the platform screens call', () => {
    expect(called.size).toBe(22)
    expect([...called].filter((call) => !stubbed.has(call))).toEqual([])
  })

  it('stubs nothing the screens never call', () => {
    expect([...stubbed].filter((stub) => !called.has(stub))).toEqual([])
  })
})

function leaves(tree: object, prefix = ''): Map<string, string> {
  const out = new Map<string, string>()
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (typeof value === 'string') {
      out.set(path, value)
    } else {
      for (const [childPath, childValue] of leaves(value as object, path)) {
        out.set(childPath, childValue)
      }
    }
  }
  return out
}

const placeholders = (text: string) =>
  [...text.matchAll(/\{(\w+)\}/g)]
    .map((match) => match[1])
    .sort()
    .join()

describe('platform dictionary', () => {
  it('has the same keys, placeholders and no empty text in both languages', () => {
    const english = leaves(platformEn)
    const turkish = leaves(platformTr)
    expect([...turkish.keys()].sort()).toEqual([...english.keys()].sort())
    expect(
      [...turkish].filter(([path, text]) => placeholders(text) !== placeholders(english.get(path) ?? '')).map(([path]) => path),
    ).toEqual([])
    expect([...english, ...turkish].filter(([, text]) => text.trim() === '').map(([path]) => path)).toEqual([])
  })
})
