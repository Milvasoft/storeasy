import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { UserType } from '@/lib/api/types'
import {
  UserActivity,
  type ActivityLogListDto,
  type FeatureFlagListDto,
  type LanguageDto,
  type PermissionListDto,
  type ReadinessReport,
  type RoleDetailDto,
  type RoleListDto,
  type UserDetailDto,
  type UserListDto,
} from '@/features/platform/types'
import { endpoint, envelope, listEnvelope } from './handlers'
import { server } from './server'

// The platform administration fake backend (storeasy-api Users, Roles, Permissions, ActivityLogs, Languages,
// FeatureFlags controllers and the health probes), shapes from the real DTOs. Reset before every test.

/** An unsigned JWT whose payload carries the given permission claims (the API writes them as role claims). */
export function permissionToken(...permissions: string[]): string {
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ sub: '7', role: permissions })}.signature`
}

export function platformUser(overrides: Partial<UserDetailDto> = {}): UserDetailDto {
  return {
    Id: 30,
    UserName: 'ops@kuzeylab.dev',
    Email: 'ops@kuzeylab.dev',
    Name: 'Operasyon',
    Surname: 'Hesabı',
    UserType: UserType.Manager,
    LockoutEnd: null,
    IsLocked: false,
    Roles: [{ Id: 22, Name: 'Destek' }],
    AllowedNotifications: [],
    AuditInfo: {
      CreationDate: '2026-11-02T09:05:00Z',
      CreatorUserName: 'deniz@kuzeylab.dev',
      LastModificationDate: '2026-11-18T14:22:00Z',
      LastModifierUserName: 'deniz@kuzeylab.dev',
    },
    ...overrides,
  }
}

function listRow(user: UserDetailDto): UserListDto {
  const { Id, UserName, Email, Name, Surname, UserType: type, LockoutEnd, IsLocked } = user
  return { Id, UserName, Email, Name, Surname, UserType: type, LockoutEnd, IsLocked }
}

export const PERMISSIONS: PermissionListDto[] = [
  { Id: 1, Name: 'SuperAdmin', Description: 'Bütün sisteme erişim.', PermissionGroup: 'App', PermissionGroupDescription: 'Uygulama geneli' },
  { Id: 2, Name: 'List', Description: 'Kullanıcı listeleme yetkisi.', PermissionGroup: 'UserManagement', PermissionGroupDescription: 'Kullanıcı yönetimi' },
  { Id: 3, Name: 'Detail', Description: 'Kullanıcı detayını görme yetkisi.', PermissionGroup: 'UserManagement', PermissionGroupDescription: 'Kullanıcı yönetimi' },
  { Id: 4, Name: 'Create', Description: 'Kullanıcı oluşturma yetkisi.', PermissionGroup: 'UserManagement', PermissionGroupDescription: 'Kullanıcı yönetimi' },
  { Id: 5, Name: 'List', Description: 'Rol listeleme yetkisi.', PermissionGroup: 'RoleManagement', PermissionGroupDescription: 'Rol yönetimi' },
  { Id: 6, Name: 'Approve', Description: 'Katalog önerisini onaylama.', PermissionGroup: 'RuleCatalogManagement', PermissionGroupDescription: 'Mağaza kuralları kataloğu' },
]

export function role(overrides: Partial<RoleDetailDto> = {}): RoleDetailDto {
  return {
    Id: 21,
    Name: 'Katalog editörü',
    Users: [{ Id: 22, Name: 'ece@kuzeylab.dev' }],
    Permissions: [
      { Id: 2, Name: 'List' },
      { Id: 6, Name: 'Approve' },
    ],
    AuditInfo: { CreationDate: '2026-11-02T09:05:00Z', CreatorUserName: 'deniz@kuzeylab.dev' },
    ...overrides,
  }
}

function initialState() {
  return {
    users: [
      platformUser({ Id: 1, UserName: 'rootuser', Email: '', Name: 'Administrator', Surname: 'User', Roles: [{ Id: 1, Name: 'SuperAdmin' }] }),
      platformUser({ Id: 7, UserName: 'deniz@kuzeylab.dev', Email: 'deniz@kuzeylab.dev', Name: 'Deniz', Surname: 'Arslan' }),
      platformUser({ IsLocked: true, LockoutEnd: '2026-11-18T14:27:00Z' }),
    ] as UserDetailDto[],
    roles: [
      { Id: 1, Name: 'SuperAdmin' },
      { Id: 21, Name: 'Katalog editörü' },
      { Id: 22, Name: 'Destek' },
    ] as RoleListDto[],
    roleDetail: role(),
    permissions: PERMISSIONS,
    logs: [
      {
        Id: 900,
        UserName: 'deniz@kuzeylab.dev',
        Activity: UserActivity.UpdateUser,
        ActivityDescription: 'Kullanıcı güncellendi',
        ActivityDate: '2026-11-18T14:22:00Z',
        RelatedId: '30',
        RelatedData: '{"Lockout":true}',
      },
      {
        Id: 899,
        UserName: 'deniz@kuzeylab.dev',
        Activity: UserActivity.DeleteRole,
        ActivityDescription: 'Rol silindi',
        ActivityDate: '2026-11-12T09:31:00Z',
        RelatedId: '25',
        RelatedData: null,
      },
    ] as ActivityLogListDto[],
    languages: [
      { Id: 21, Name: 'Türkçe', Code: 'tr-TR', Supported: true, IsDefault: true },
      { Id: 22, Name: 'English', Code: 'en-US', Supported: true, IsDefault: false },
      { Id: 23, Name: 'Deutsch', Code: 'de-DE', Supported: false, IsDefault: false },
    ] as LanguageDto[],
    flags: [
      { Id: 1, Key: 'Export', IsEnabled: true, Reason: null },
      { Id: 2, Key: 'Studio.AiCaptions', IsEnabled: false, Reason: 'Sağlayıcı kotası doldu' },
    ] as FeatureFlagListDto[],
    ready: {
      status: 200,
      body: {
        status: 'Healthy',
        durationMs: 42,
        checks: { database: { status: 'Healthy', description: null }, redis: { status: 'Healthy', description: null } },
      } as unknown as ReadinessReport,
    },
  }
}

export const platformState = initialState()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

export const platformHandlers: HttpHandler[] = [
  endpoint('patch', '/Users', () => json(listEnvelope(platformState.users.map(listRow)))),
  endpoint('get', '/Users/user', ({ url }) => {
    const user = platformState.users.find((item) => item.Id === Number(url.searchParams.get('UserId')))
    return user
      ? json(envelope(user))
      : HttpResponse.json({ IsSuccess: false, StatusCode: 404, Messages: [{ Key: 'UserNotFound', Message: 'Kullanıcı bulunamadı.' }], Data: null }, { status: 404 })
  }),
  endpoint('post', '/Users/user', () => json(envelope(31))),
  endpoint('put', '/Users/user', ({ body }) => json(envelope((body as { Id: number }).Id))),
  endpoint('delete', '/Users/user', ({ url }) => json(envelope(Number(url.searchParams.get('UserId'))))),
  endpoint('patch', '/Roles', () => json(listEnvelope(platformState.roles))),
  endpoint('get', '/Roles/role', ({ url }) => {
    const id = Number(url.searchParams.get('RoleId'))
    return json(envelope(id === 1 ? role({ Id: 1, Name: 'SuperAdmin', Permissions: [{ Id: 1, Name: 'SuperAdmin' }] }) : { ...platformState.roleDetail, Id: id }))
  }),
  endpoint('post', '/Roles/role', () => json(envelope(24))),
  endpoint('put', '/Roles/role', ({ body }) => json(envelope((body as { Id: number }).Id))),
  endpoint('delete', '/Roles/role', ({ url }) => json(envelope(Number(url.searchParams.get('RoleId'))))),
  endpoint('patch', '/Permissions', () => json(listEnvelope(platformState.permissions))),
  endpoint('patch', '/ActivityLogs', () => json(listEnvelope(platformState.logs, 1, 46, 3))),
  endpoint('patch', '/Languages', () => json(listEnvelope(platformState.languages))),
  endpoint('put', '/Languages/language', ({ body }) => json(envelope((body as { Id: number }).Id))),
  endpoint('patch', '/FeatureFlags', () => json(listEnvelope(platformState.flags))),
  endpoint('put', '/FeatureFlags/featureFlag', ({ body }) => json(envelope((body as { Id: number }).Id))),
  endpoint('get', '/HealthCheck', () => new HttpResponse('Ok', { headers: { 'Content-Type': 'text/plain' } })),
  endpoint('get', '/HealthCheck/ready', () => HttpResponse.json(platformState.ready.body as never, { status: platformState.ready.status })),
  endpoint('get', '/Rules/catalog', () => json(envelope({ Version: '1.3.0', PublishedDate: '2026-11-14T00:00:00Z', RuleCount: 84, Source: 'embedded' }))),
]

export function setupPlatformApi(): void {
  beforeEach(() => {
    Object.assign(platformState, initialState())
    server.use(...platformHandlers)
  })
}
