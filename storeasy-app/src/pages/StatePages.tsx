import { Link, Navigate } from 'react-router-dom'
import { SearchX, UserX } from 'lucide-react'
import { AuthCard, SoloLayout } from '@/components/layout/AuthLayout'
import { StateView } from '@/components/ui/ScreenState'
import { Avatar, Button } from '@/components/ui/primitives'
import { displayName, initialsOf, useAuth } from '@/lib/auth/AuthContext'
import { useT } from '@/lib/i18n/I18nContext'
import { HOME_PATH } from '@/lib/nav/nav'
import { useOrganization } from '@/lib/org/OrganizationContext'

/** An unknown address inside the shell (design 18.9): the menu stays, one way back to the apps. */
export function NotFoundPage() {
  const t = useT()
  return (
    <StateView
      icon={<SearchX aria-hidden />}
      title={t.states.notFoundTitle}
      description={t.states.notFoundBody}
      action={
        <Link className="btn inv" to={HOME_PATH}>
          {t.states.backToApps}
        </Link>
      }
    />
  )
}

/** Design 5.32 / 18.23: every membership is gone. The account stays; a new organization or an invitation is the way on. */
export function NoAccessPage() {
  const t = useT()
  const { user, logout } = useAuth()
  const { organizations, isLoading } = useOrganization()
  if (!isLoading && organizations.length > 0) {
    return <Navigate to={HOME_PATH} replace />
  }
  return (
    <SoloLayout>
      <AuthCard icon={<UserX aria-hidden />} tone="warn" title={t.noAccess.title}>
        <p className="m-0 text-sm text-fg2">{t.noAccess.body}</p>
        <Link className="btn pri blk" to="/setup">
          {t.noAccess.create}
        </Link>
        {user ? (
          <div className="flex items-center gap-2.5 rounded-md border border-border p-3 text-left">
            <Avatar initials={initialsOf(displayName(user))} />
            <span className="min-w-0 flex-1 text-[13px]">
              <span className="block text-fg3">{t.invite.signedInLabel}</span>
              <b className="block truncate">{user.email}</b>
            </span>
            <Button size="sm" variant="ghost" onClick={() => void logout()}>
              {t.userMenu.logout}
            </Button>
          </div>
        ) : null}
      </AuthCard>
    </SoloLayout>
  )
}
