import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ShieldAlert } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { StateView } from '@/components/ui/ScreenState'
import { UserType } from '@/lib/api/types'
import { useAuth } from '@/lib/auth/AuthContext'
import { useGlobalPermissions } from '@/lib/auth/globalPermissions'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { usePlatformText } from './i18n'
import type { AuditDto } from './types'

// The platform screens' global permissions (storeasy-api PermissionCatalog, "Group.Action").
export const PlatformPermission = {
  UserList: 'UserManagement.List',
  UserDetail: 'UserManagement.Detail',
  UserCreate: 'UserManagement.Create',
  UserUpdate: 'UserManagement.Update',
  UserDelete: 'UserManagement.Delete',
  RoleList: 'RoleManagement.List',
  RoleDetail: 'RoleManagement.Detail',
  RoleCreate: 'RoleManagement.Create',
  RoleUpdate: 'RoleManagement.Update',
  RoleDelete: 'RoleManagement.Delete',
  PermissionList: 'PermissionManagement.List',
  ActivityLogList: 'ActivityLogManagement.List',
  LanguageList: 'LanguageManagement.List',
  LanguageUpdate: 'LanguageManagement.Update',
  FeatureFlagList: 'FeatureFlagManagement.List',
  FeatureFlagUpdate: 'FeatureFlagManagement.Update',
} as const

// Every list asks for at most this many rows (GlobalConstant.MaxRowCount is 100); the role and permission pickers read
// one page of this size (design 17.12: at most 100 roles, the rest by search).
export const MAX_ROWS = 100

/** Design 17.26 and 17.27: an app user gets "platform only"; a manager without the screen's permission gets 403. */
export function PlatformGate({ permission, children }: { permission?: string; children: ReactNode }) {
  const base = useT()
  const { isManager } = useAuth()
  const global = useGlobalPermissions()
  if (!isManager) {
    return (
      <StateView
        icon={<ShieldAlert aria-hidden />}
        title={base.states.forbiddenTitle}
        description={base.states.platformOnly}
        action={
          <Link className="btn inv" to={HOME_PATH}>
            {base.states.backToApps}
          </Link>
        }
      />
    )
  }
  if (permission && !global.has(permission)) {
    return <PlatformForbidden permission={permission} />
  }
  return <>{children}</>
}

export function PlatformForbidden({ permission }: { permission: string }) {
  const base = useT()
  const t = usePlatformText()
  return (
    <StateView
      icon={<ShieldAlert aria-hidden />}
      title={base.states.forbiddenTitle}
      description={interpolate(t.common.forbidden, { permission })}
    />
  )
}

/** The "System record" mark of the seed band (Id < 21): edit, delete and lock are hidden on it. */
export function SystemRecordBadge() {
  const t = usePlatformText()
  return (
    <Badge small tone="info">
      {t.common.systemRecord}
    </Badge>
  )
}

export function userTypeLabel(type: UserType, t: ReturnType<typeof usePlatformText>): string {
  return type === UserType.Manager ? t.userType.manager : t.userType.appUser
}

/** Who created and last changed a record (AuditDto). */
export function AuditCard({ audit }: { audit?: AuditDto | null }) {
  const t = usePlatformText()
  const format = useFormat()
  return (
    <section className="card grid gap-2 p-4" aria-label={t.common.audit}>
      <h2 className="text-[15px] font-semibold">{t.common.audit}</h2>
      <dl className="kv">
        <dt>{t.common.createdBy}</dt>
        <dd>{audit?.CreatorUserName ?? '-'}</dd>
        <dt>{t.common.created}</dt>
        <dd>{format.dateTime(audit?.CreationDate)}</dd>
        <dt>{t.common.modifiedBy}</dt>
        <dd>{audit?.LastModifierUserName ?? '-'}</dd>
        <dt>{t.common.modified}</dt>
        <dd>{format.dateTime(audit?.LastModificationDate)}</dd>
      </dl>
    </section>
  )
}
