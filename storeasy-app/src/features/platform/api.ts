import { apiDelete, apiGet, apiPatchList, apiPost, apiProbe, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type {
  ActivityLogListDto,
  CreateRoleCommand,
  CreateUserCommand,
  FeatureFlagListDto,
  LanguageDto,
  PermissionListDto,
  ReadinessReport,
  RoleDetailDto,
  RoleListDto,
  UpdateFeatureFlagCommand,
  UpdateLanguageCommand,
  UpdateRoleCommand,
  UpdateUserCommand,
  UserDetailDto,
  UserListDto,
} from './types'

// storeasy-api UsersController, RolesController, PermissionsController, ActivityLogsController, LanguagesController,
// FeatureFlagsController (api/v1.0/<controller>, Manager user type + the global permission of each action) and the
// anonymous HealthCheckController probes. Lists are Milvasoft ListRequest bodies sent with PATCH.
export const platformApi = {
  users: (request: ListRequest) => apiPatchList<UserListDto>('/Users', request),
  user: (userId: number) => apiGet<UserDetailDto>('/Users/user', { UserId: userId }),
  // 409 UserExists / EmailExists.
  createUser: (command: CreateUserCommand) => apiPost<number>('/Users/user', command),
  // A role, password or lock change revokes the user's sessions.
  updateUser: (command: UpdateUserCommand) => apiPut<number>('/Users/user', command),
  deleteUser: (userId: number) => apiDelete<number>('/Users/user', { UserId: userId }),

  roles: (request: ListRequest) => apiPatchList<RoleListDto>('/Roles', request),
  role: (roleId: number) => apiGet<RoleDetailDto>('/Roles/role', { RoleId: roleId }),
  // Role names are not unique: the create is sent with an Idempotency-Key so a double click opens one role.
  createRole: (command: CreateRoleCommand) => apiPost<number>('/Roles/role', command, { idempotent: true }),
  updateRole: (command: UpdateRoleCommand) => apiPut<number>('/Roles/role', command),
  deleteRole: (roleId: number) => apiDelete<number>('/Roles/role', { RoleId: roleId }),

  permissions: (request: ListRequest) => apiPatchList<PermissionListDto>('/Permissions', request),

  activityLogs: (request: ListRequest) => apiPatchList<ActivityLogListDto>('/ActivityLogs', request),

  languages: (request: ListRequest) => apiPatchList<LanguageDto>('/Languages', request),
  updateLanguage: (command: UpdateLanguageCommand) => apiPut<number>('/Languages/language', command),

  featureFlags: (request: ListRequest) => apiPatchList<FeatureFlagListDto>('/FeatureFlags', request),
  updateFeatureFlag: (command: UpdateFeatureFlagCommand) => apiPut<number>('/FeatureFlags/featureFlag', command),

  // Liveness answers the plain text "Ok"; readiness answers its report with 200 or 503.
  live: () => apiProbe<string>('/HealthCheck'),
  ready: () => apiProbe<ReadinessReport>('/HealthCheck/ready'),
}

export const platformKeys = {
  all: ['platform'] as const,
  users: (request: ListRequest) => ['platform', 'users', request] as const,
  user: (userId: number) => ['platform', 'user', userId] as const,
  roles: (request: ListRequest) => ['platform', 'roles', request] as const,
  role: (roleId: number) => ['platform', 'role', roleId] as const,
  permissions: () => ['platform', 'permissions'] as const,
  activityLogs: (request: ListRequest) => ['platform', 'activity-logs', request] as const,
  languages: () => ['platform', 'languages'] as const,
  featureFlags: () => ['platform', 'feature-flags'] as const,
  health: () => ['platform', 'health'] as const,
}
