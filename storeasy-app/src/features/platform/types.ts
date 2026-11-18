// Platform administration contract (storeasy-api Storeasy.Application UserDtos, RoleDtos, PermissionDtos, ActivityLogDtos,
// LanguageDtos, FeatureFlagDtos, the Users/Roles/Languages/FeatureFlags commands, HealthCheckController ReadinessReport and
// the Storeasy.Domain enums). Every endpoint here carries [UserTypeAuth(UserType.Manager)] plus a global permission.
import type { IsoDateTime, NameIntNavigationDto, UserType } from '@/lib/api/types'

// Seed records (Users, Roles, Permissions: Id 1-20) are protected by a database trigger: never editable (08 section 1.4).
export const SEED_ID_LIMIT = 21

export function isSeedRecord(id: number): boolean {
  return id < SEED_ID_LIMIT
}

/** Milvasoft UpdateProperty<T> (a struct): a field the update leaves out is not changed. */
export interface UpdateProperty<T> {
  Value: T
  IsUpdated: boolean
}

export function updated<T>(value: T): UpdateProperty<T> {
  return { Value: value, IsUpdated: true }
}

// Storeasy.Domain.Enums.NotificationType (only kept round trip; the platform form does not edit it).
export type NotificationType = number

// Storeasy.Domain.Enums.UserActivity (byte).
export const UserActivity = {
  CreateUser: 0,
  UpdateUser: 1,
  DeleteUser: 2,
  CreateRole: 3,
  UpdateRole: 4,
  DeleteRole: 5,
  UpdateLanguages: 15,
  UpdateFeatureFlag: 16,
  ApproveRuleProposal: 40,
  RejectRuleProposal: 41,
} as const
export type UserActivity = (typeof UserActivity)[keyof typeof UserActivity]

export const USER_ACTIVITIES: UserActivity[] = Object.values(UserActivity)

/** Storeasy.Application.Dtos.AuditDto<int>. */
export interface AuditDto {
  CreationDate?: IsoDateTime | null
  CreatorUserName?: string | null
  LastModificationDate?: IsoDateTime | null
  LastModifierUserName?: string | null
}

// ---- Users ----------------------------------------------------------------------------------------------------------

export interface UserListDto {
  Id: number
  UserName: string
  Email?: string | null
  Name?: string | null
  Surname?: string | null
  UserType: UserType
  LockoutEnd?: IsoDateTime | null
  IsLocked: boolean
}

export interface UserDetailDto extends UserListDto {
  Roles?: NameIntNavigationDto[] | null
  AllowedNotifications?: NotificationType[] | null
  AuditInfo?: AuditDto | null
}

export interface CreateUserCommand {
  UserType: UserType
  UserName: string
  Email: string
  Name: string | null
  Surname: string | null
  Password: string
  RoleIdList: number[]
  AllowedNotifications: NotificationType[] | null
}

export interface UpdateUserCommand {
  Id: number
  Name?: UpdateProperty<string | null>
  Surname?: UpdateProperty<string | null>
  Lockout?: UpdateProperty<boolean>
  NewPassword?: UpdateProperty<string>
  RoleIdList?: UpdateProperty<number[]>
  AllowedNotifications?: UpdateProperty<NotificationType[]>
}

// User.NameMaxLength (CreateUserCommandValidator).
export const USER_NAME_MAX = 100

// ---- Roles and permissions ------------------------------------------------------------------------------------------

export interface RoleListDto {
  Id: number
  Name: string
}

export interface RoleDetailDto extends RoleListDto {
  /** Users[].Name is the user name. */
  Users?: NameIntNavigationDto[] | null
  /** Permissions[].Name is the action only ("List"); the group comes from the permission catalog. */
  Permissions?: NameIntNavigationDto[] | null
  AuditInfo?: AuditDto | null
}

export interface CreateRoleCommand {
  Name: string
  PermissionIdList: number[]
}

export interface UpdateRoleCommand {
  Id: number
  Name?: UpdateProperty<string>
  PermissionIdList?: UpdateProperty<number[]>
}

export interface PermissionListDto {
  Id: number
  /** The action, e.g. "List". */
  Name: string
  Description?: string | null
  /** The group key, e.g. "UserManagement". */
  PermissionGroup: string
  PermissionGroupDescription?: string | null
}

// ---- Activity log ---------------------------------------------------------------------------------------------------

export interface ActivityLogListDto {
  Id: number
  UserName?: string | null
  Activity: UserActivity
  ActivityDescription?: string | null
  ActivityDate: IsoDateTime
  RelatedId?: string | null
  RelatedData?: string | null
}

// ---- Languages ------------------------------------------------------------------------------------------------------

export interface LanguageDto {
  Id: number
  Name: string
  Code: string
  Supported: boolean
  SupportedDescription?: string | null
  IsDefault: boolean
  IsDefaultDescription?: string | null
}

export interface UpdateLanguageCommand {
  Id: number
  IsDefault?: UpdateProperty<boolean>
  Supported?: UpdateProperty<boolean>
}

// ---- Feature flags --------------------------------------------------------------------------------------------------

export interface FeatureFlagListDto {
  Id: number
  /** One of FeatureFlagKeys (Export, Studio.AiCaptions, ...). */
  Key: string
  IsEnabled: boolean
  IsEnabledDescription?: string | null
  Reason?: string | null
}

export interface UpdateFeatureFlagCommand {
  Id: number
  IsEnabled?: UpdateProperty<boolean>
  Reason?: UpdateProperty<string | null>
}

// FeatureFlag.Reason [MaxLength(200)] (UpdateFeatureFlagCommandValidator).
export const FLAG_REASON_MAX = 200

// ---- Health ---------------------------------------------------------------------------------------------------------

export type HealthState = 'Healthy' | 'Degraded' | 'Unhealthy'

export interface ReadinessCheck {
  Status: HealthState | string
  Description?: string | null
}

/** GET healthcheck/ready: 200 when ready, 503 otherwise, with the same body. */
export interface ReadinessReport {
  Status: HealthState | string
  DurationMs: number
  Checks?: Record<string, ReadinessCheck> | null
}
