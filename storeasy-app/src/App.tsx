import { Suspense } from 'react'
import { Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import { BootScreen } from '@/components/common/BootScreen'
import { RouteErrorBoundary } from '@/components/common/ErrorBoundary'
import { GuestOnly, ProtectedRoute } from '@/components/common/ProtectedRoute'
import { AppShell } from '@/components/layout/AppShell'
import { ScreenState } from '@/components/ui/ScreenState'
import { PageSkeleton } from '@/components/ui/primitives'
import { contentRoutes } from '@/features/routes'
import { ActivityProvider } from '@/lib/activity/ActivityContext'
import { HOME_PATH } from '@/lib/nav/nav'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { lazyPage } from '@/lib/utils/lazyPage'
import { readStorage, StorageKey } from '@/lib/utils/storage'
import { LoginPage } from '@/pages/auth/LoginPage'
import { NotFoundPage, NoAccessPage } from '@/pages/StatePages'

const RegisterPage = lazyPage(() => import('@/pages/auth/RegisterPage'), 'RegisterPage')
const VerifyEmailPage = lazyPage(() => import('@/pages/auth/VerifyEmailPage'), 'VerifyEmailPage')
const VerifyEmailTokenPage = lazyPage(() => import('@/pages/auth/VerifyEmailPage'), 'VerifyEmailTokenPage')
const ForgotPasswordPage = lazyPage(() => import('@/pages/auth/PasswordRecoveryPages'), 'ForgotPasswordPage')
const ResetPasswordPage = lazyPage(() => import('@/pages/auth/PasswordRecoveryPages'), 'ResetPasswordPage')
const InvitePage = lazyPage(() => import('@/pages/auth/InvitePage'), 'InvitePage')
const SetupPage = lazyPage(() => import('@/pages/SetupPage'), 'SetupPage')
const ActivityPage = lazyPage(() => import('@/pages/ActivityPage'), 'ActivityPage')
const JobsPage = lazyPage(() => import('@/pages/activity/JobsPages'), 'JobsPage')
const JobDetailPage = lazyPage(() => import('@/pages/activity/JobsPages'), 'JobDetailPage')
const AppsPage = lazyPage(() => import('@/pages/apps/AppsPage'), 'AppsPage')
const AppDetailPage = lazyPage(() => import('@/pages/apps/AppDetailPage'), 'AppDetailPage')
const ConnectionsPage = lazyPage(() => import('@/pages/org/ConnectionsPage'), 'ConnectionsPage')
const MembersPage = lazyPage(() => import('@/pages/org/MembersPage'), 'MembersPage')
const AuditPage = lazyPage(() => import('@/pages/org/AuditPage'), 'AuditPage')
const OrgSettingsPage = lazyPage(() => import('@/pages/org/OrgSettingsPage'), 'OrgSettingsPage')
const AccountPage = lazyPage(() => import('@/pages/account/AccountPage'), 'AccountPage')
const SecurityPage = lazyPage(() => import('@/pages/account/AccountPage'), 'SecurityPage')
const SectionPage = lazyPage(() => import('@/pages/SectionPages'), 'SectionPage')
const PlatformPage = lazyPage(() => import('@/pages/SectionPages'), 'PlatformPage')
const StudioRoutes = lazyPage(() => import('@/features/studio/StudioRoutes'), 'StudioRoutes')

function Lazy({ children }: { children: JSX.Element }) {
  return <Suspense fallback={<BootScreen />}>{children}</Suspense>
}

// The shell needs an organization: none yet goes to the first setup, a lost membership to "no access" (design 5.32).
function OrganizationGate({ children }: { children: JSX.Element }) {
  const { organizations, isLoading, error, refetch } = useOrganization()
  if (isLoading) {
    return <BootScreen />
  }
  if (error) {
    return (
      <div className="grid min-h-full place-items-center bg-bg">
        <ScreenState error={error} onRetry={() => void refetch()} />
      </div>
    )
  }
  if (organizations.length === 0) {
    return <Navigate to={readStorage(StorageKey.CurrentOrganization) ? '/no-access' : '/setup'} replace />
  }
  return children
}

function ProtectedLayout() {
  const { pathname } = useLocation()
  return (
    <ProtectedRoute>
      <OrganizationGate>
        <ActivityProvider>
          <AppShell>
            {/* Keyed by path: a crash on one page is cleared by navigating away. */}
            <RouteErrorBoundary key={pathname}>
              <Suspense fallback={<PageSkeleton />}>
                <Outlet />
              </Suspense>
            </RouteErrorBoundary>
          </AppShell>
        </ActivityProvider>
      </OrganizationGate>
    </ProtectedRoute>
  )
}

function Guest({ children }: { children: JSX.Element }) {
  return (
    <GuestOnly>
      <Lazy>{children}</Lazy>
    </GuestOnly>
  )
}

export function App() {
  return (
    <Routes>
      <Route
        path="/login"
        element={
          <GuestOnly>
            <LoginPage />
          </GuestOnly>
        }
      />
      <Route
        path="/register"
        element={
          <Guest>
            <RegisterPage />
          </Guest>
        }
      />
      <Route
        path="/forgot-password"
        element={
          <Guest>
            <ForgotPasswordPage />
          </Guest>
        }
      />
      <Route
        path="/reset-password"
        element={
          <Guest>
            <ResetPasswordPage />
          </Guest>
        }
      />
      {/* Reached right after sign-up, signed in (the account can sign in at once) or from the e-mail on another device. */}
      <Route
        path="/verify-email"
        element={
          <Lazy>
            <VerifyEmailPage />
          </Lazy>
        }
      />
      {/* The e-mail link works signed in or out: verifying does not sign in (design 5.15). */}
      <Route
        path="/verify-email/:token"
        element={
          <Lazy>
            <VerifyEmailTokenPage />
          </Lazy>
        }
      />
      <Route
        path="/invite/:token"
        element={
          <Lazy>
            <InvitePage />
          </Lazy>
        }
      />
      <Route
        path="/invite"
        element={
          <Lazy>
            <InvitePage />
          </Lazy>
        }
      />
      <Route
        path="/setup"
        element={
          <ProtectedRoute>
            <Lazy>
              <SetupPage />
            </Lazy>
          </ProtectedRoute>
        }
      />
      <Route
        path="/no-access"
        element={
          <ProtectedRoute>
            <NoAccessPage />
          </ProtectedRoute>
        }
      />
      <Route element={<ProtectedLayout />}>
        <Route index element={<Navigate to={HOME_PATH} replace />} />
        <Route path="/apps" element={<AppsPage />} />
        <Route path="/apps/:appId" element={<AppDetailPage tab="overview" />} />
        <Route path="/apps/:appId/builds" element={<AppDetailPage tab="builds" />} />
        <Route path="/apps/:appId/jobs" element={<AppDetailPage tab="jobs" />} />
        <Route path="/activity" element={<ActivityPage />} />
        <Route path="/activity/jobs" element={<JobsPage />} />
        <Route path="/activity/jobs/:jobId" element={<JobDetailPage />} />
        <Route path="/studio/*" element={<StudioRoutes />} />
        <Route path="/intake" element={<SectionPage section="intake" />} />
        <Route path="/connections" element={<ConnectionsPage />} />
        <Route path="/members" element={<MembersPage />} />
        <Route path="/audit" element={<AuditPage />} />
        <Route path="/settings" element={<OrgSettingsPage />} />
        <Route path="/developer" element={<SectionPage section="developer" />} />
        <Route path="/account" element={<AccountPage />} />
        <Route path="/account/security" element={<SecurityPage />} />
        <Route path="/admin/users" element={<PlatformPage section="adminUsers" />} />
        <Route path="/admin/roles" element={<PlatformPage section="adminRoles" />} />
        <Route path="/admin/activity-logs" element={<PlatformPage section="adminLogs" />} />
        <Route path="/admin/languages" element={<PlatformPage section="adminLanguages" />} />
        <Route path="/admin/feature-flags" element={<PlatformPage section="adminFlags" />} />
        <Route path="/admin/system" element={<PlatformPage section="adminSystem" />} />
        {contentRoutes()}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  )
}
