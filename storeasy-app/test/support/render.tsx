import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactElement, ReactNode } from 'react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { ConfirmProvider } from '@/components/ui/ConfirmProvider'
import { Toaster } from '@/components/ui/Toaster'
import { ActivityProvider, ActivitySourceFactoryContext, type ActivitySourceFactory } from '@/lib/activity/ActivityContext'
import { createManualSource, type ActivitySource, type ConnectionInfo } from '@/lib/activity/events'
import { UserType } from '@/lib/api/types'
import { AuthProvider } from '@/lib/auth/AuthContext'
import { FeatureFlagsProvider, type FeatureFlags } from '@/lib/features/FeatureFlagsContext'
import { I18nProvider } from '@/lib/i18n/I18nContext'
import { OrganizationProvider, useOrganization } from '@/lib/org/OrganizationContext'
import { createMutationCache } from '@/lib/query/queryClient'
import { preloadLazyPages } from '@/lib/utils/lazyPage'
import { StorageKey, storageKey } from '@/lib/utils/storage'
import { TEST_ORG_ID, TEST_USER_ID, TEST_USER_NAME } from './factories'

/** A query client per test: no retries, no caching between tests, no background refetching. */
export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    mutationCache: createMutationCache(),
    defaultOptions: {
      queries: { retry: false, gcTime: 0, staleTime: 0, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  })
}

/** Writes a valid token into storage, so AuthProvider starts "authed". */
export function signIn(
  userName = TEST_USER_NAME,
  userId = TEST_USER_ID,
  userType: UserType = UserType.AppUser,
  storedOrganization = true,
  accessToken = 'access-token',
): void {
  localStorage.setItem(
    storageKey(StorageKey.AuthTokens),
    JSON.stringify({
      accessToken,
      refreshToken: 'refresh-token',
      expiresIn: 3600,
      refreshTokenExpiresIn: 86_400,
      issuedAt: Date.now(),
      userId,
      userName,
      userType,
    }),
  )
  if (storedOrganization) {
    localStorage.setItem(storageKey(StorageKey.CurrentOrganization), String(TEST_ORG_ID))
  }
}

export interface RenderOptions {
  /** The URL the test starts at; a :param in `path` is filled from it. */
  route?: string
  /** Route pattern for an element that reads route params ('/verify-email/:token'). */
  path?: string
  /** Sign a user in before rendering (default yes; false for guest screens). */
  authed?: boolean
  userType?: UserType
  /** The stored access token; a JWT with role claims carries the platform permissions (test/support/platform.ts). */
  accessToken?: string
  /** Remember an organization as the last used one (default yes). */
  storedOrganization?: boolean
  lang?: 'tr' | 'en'
  queryClient?: QueryClient
  flags?: Partial<FeatureFlags>
  /** Wait for the organization list before rendering (pages inside the shell need one). Default: same as authed. */
  needsOrganization?: boolean
  /** The live source the activity provider gets; a manual one by default. */
  activity?: ActivitySource
  activityConnection?: ConnectionInfo
}

function OrganizationReady({ children }: { children: ReactNode }) {
  const { current } = useOrganization()
  return current ? <>{children}</> : null
}

// Shows the current address, so navigation tests can read it.
function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{location.pathname + location.search}</output>
}

// The production provider stack, with MemoryRouter instead of BrowserRouter. A provider added to main.tsx is added here too.
function Providers({
  children,
  queryClient,
  flags,
  activity,
}: {
  children: ReactNode
  queryClient: QueryClient
  flags?: Partial<FeatureFlags>
  activity: ActivitySourceFactory
}) {
  return (
    <QueryClientProvider client={queryClient}>
      <FeatureFlagsProvider flags={flags}>
        <I18nProvider>
          <AuthProvider>
            <OrganizationProvider>
              <ActivitySourceFactoryContext.Provider value={activity}>
                <ConfirmProvider>
                  {children}
                  <Toaster />
                </ConfirmProvider>
              </ActivitySourceFactoryContext.Provider>
            </OrganizationProvider>
          </AuthProvider>
        </I18nProvider>
      </FeatureFlagsProvider>
    </QueryClientProvider>
  )
}

export function renderWithProviders(ui: ReactElement, options: RenderOptions = {}) {
  const {
    route = '/',
    path,
    authed = true,
    userType,
    lang = 'tr',
    queryClient = createTestQueryClient(),
    flags,
    activityConnection,
  } = options
  const needsOrganization = options.needsOrganization ?? authed
  const activity = options.activity ?? createManualSource(activityConnection)
  const factory: ActivitySourceFactory = () => activity
  if (authed) {
    signIn(TEST_USER_NAME, TEST_USER_ID, userType, options.storedOrganization ?? true, options.accessToken)
  }
  localStorage.setItem(storageKey(StorageKey.Lang), lang)
  const user = userEvent.setup()
  const content = path ? (
    <Routes>
      <Route path={path} element={ui} />
      <Route path="*" element={null} />
    </Routes>
  ) : (
    ui
  )
  const result = render(
    <MemoryRouter initialEntries={[route]}>
      <Providers queryClient={queryClient} flags={flags} activity={factory}>
        {needsOrganization ? (
          <OrganizationReady>
            <ActivityProvider>{content}</ActivityProvider>
          </OrganizationReady>
        ) : (
          content
        )}
        <LocationProbe />
      </Providers>
    </MemoryRouter>,
  )
  return { ...result, user, queryClient, activity }
}

/** The whole app (routing included) at a URL, for navigation and route guard tests. */
export async function renderApp(options: RenderOptions = {}) {
  const { App } = await import('@/App')
  // The route chunks load here, before the test starts waiting for a page: their import cost is not a findBy's budget.
  await preloadLazyPages()
  return renderWithProviders(<App />, { ...options, needsOrganization: false })
}
