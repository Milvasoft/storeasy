import { Suspense } from 'react'
import { Navigate, Outlet, Route, Routes, useLocation, useSearchParams } from 'react-router-dom'
import { BootScreen } from '@/components/common/BootScreen'
import { RouteErrorBoundary } from '@/components/common/ErrorBoundary'
import { GuestOnly, ProtectedRoute } from '@/components/common/ProtectedRoute'
import { AppShell } from '@/components/layout/AppShell'
import { ScreenState } from '@/components/ui/ScreenState'
import { UserType } from '@/lib/api/types'
import { useAuth } from '@/lib/auth/AuthContext'
import { PageSkeleton } from '@/components/ui/primitives'
import { useDeployment } from '@/features/access/publicSettings'
import { contentRoutes } from '@/features/routes'
import { microsoftRoutes } from '@/features/microsoft/routes'
import { wingetRoutes } from '@/features/winget/routes'
import { chromeRoutes } from '@/features/chrome/routes'
import { firefoxRoutes } from '@/features/firefox/routes'
import { ActivityProvider } from '@/lib/activity/ActivityContext'
import { ServerFeatureFlags } from '@/lib/features/FeatureFlagsContext'
import { HOME_PATH } from '@/lib/nav/nav'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { lazyPage } from '@/lib/utils/lazyPage'
import { readStorage, StorageKey } from '@/lib/utils/storage'
import { LoginPage } from '@/pages/auth/LoginPage'
import { NotFoundPage, NoAccessPage, PlatformNoOrganizationPage } from '@/pages/StatePages'

const RegisterPage = lazyPage(() => import('@/pages/auth/RegisterPage'), 'RegisterPage')
const VerifyEmailPage = lazyPage(() => import('@/pages/auth/VerifyEmailPage'), 'VerifyEmailPage')
const VerifyEmailTokenPage = lazyPage(() => import('@/pages/auth/VerifyEmailPage'), 'VerifyEmailTokenPage')
const ForgotPasswordPage = lazyPage(() => import('@/pages/auth/PasswordRecoveryPages'), 'ForgotPasswordPage')
const ResetPasswordPage = lazyPage(() => import('@/pages/auth/PasswordRecoveryPages'), 'ResetPasswordPage')
const InvitePage = lazyPage(() => import('@/pages/auth/InvitePage'), 'InvitePage')
const GoogleCallbackPage = lazyPage(() => import('@/features/access/GoogleCallbackPage'), 'GoogleCallbackPage')
const SetupPage = lazyPage(() => import('@/pages/SetupPage'), 'SetupPage')
const FirstRunSetupPage = lazyPage(() => import('@/pages/auth/FirstRunSetupPage'), 'FirstRunSetupPage')
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
const StudioRoutes = lazyPage(() => import('@/features/studio/StudioRoutes'), 'StudioRoutes')

function Lazy({ children }: { children: JSX.Element }) {
  return <Suspense fallback={<BootScreen />}>{children}</Suspense>
}

// The shell needs an organization: none yet goes to the first setup, a lost membership to "no access" (design 5.32).
// The admin area (/admin/...) is the instance's, not an organization's: a platform administrator (Manager, e.g. the
// root user of docs/self-host.md section 5) reaches it without being a member of any organization.
const PLATFORM_HOME_PATH = '/admin/system'

function OrganizationGate({ children }: { children: JSX.Element }) {
  const { organizations, isLoading, error, refetch } = useOrganization()
  const { user } = useAuth()
  const { pathname } = useLocation()
  const deployment = useDeployment()
  if (isLoading || deployment.isLoading) {
    return <BootScreen />
  }
  if (error) {
    return (
      <div className="grid min-h-full place-items-center bg-bg">
        <ScreenState error={error} onRetry={() => void refetch()} />
      </div>
    )
  }
  // Single team: the administrator of an install that has no team yet (an install made before the single team mode)
  // creates it in one step; the System pages stay reachable meanwhile.
  if (organizations.length === 0 && user?.userType === UserType.Manager && deployment.singleTeam) {
    return pathname.startsWith('/admin/') ? children : <Navigate to="/setup" replace />
  }
  // A platform administrator without an organization lands on the admin area; an organization page (a sidebar link,
  // an old bookmark) explains why it cannot open instead of silently bouncing back, which looked like dead links.
  if (organizations.length === 0 && user?.userType === UserType.Manager) {
    if (pathname.startsWith('/admin/')) {
      return children
    }
    if (pathname === '/') {
      return <Navigate to={PLATFORM_HOME_PATH} replace />
    }
    return <PlatformNoOrganizationPage adminPath={PLATFORM_HOME_PATH} />
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
          <ServerFeatureFlags />
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

// The sign-in, or the first run setup while a single team instance has no user yet (system/public-settings).
function SignInOrSetup() {
  const { setupRequired, isLoading } = useDeployment()
  if (isLoading) {
    return <BootScreen />
  }
  return setupRequired ? (
    <Lazy>
      <FirstRunSetupPage />
    </Lazy>
  ) : (
    <LoginPage />
  )
}

// Sign-up: a single team instance has none; an invitation link (?invite=...) still creates the invited account.
function SignUp() {
  const { singleTeam, isLoading } = useDeployment()
  const [params] = useSearchParams()
  if (isLoading) {
    return <BootScreen />
  }
  return singleTeam && !params.get('invite') ? <Navigate to="/login" replace /> : <RegisterPage />
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
            <SignInOrSetup />
          </GuestOnly>
        }
      />
      <Route
        path="/register"
        element={
          <Guest>
            <SignUp />
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
      {/* Google sends the browser back here with the code and the state (account/login/google). */}
      <Route
        path="/auth/google/callback"
        element={
          <Lazy>
            <GoogleCallbackPage />
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
      {/* The invitation e-mail's link (AppOptions.InvitationPath): /invitation?token=... */}
      <Route
        path="/invitation"
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
        <Route path="/connections" element={<ConnectionsPage />} />
        <Route path="/members" element={<MembersPage />} />
        <Route path="/audit" element={<AuditPage />} />
        <Route path="/settings" element={<OrgSettingsPage />} />
        <Route path="/account" element={<AccountPage />} />
        <Route path="/account/security" element={<SecurityPage />} />
        {contentRoutes()}
        {microsoftRoutes()}
        {wingetRoutes()}
        {chromeRoutes()}
        {firefoxRoutes()}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  )
}
