import type { ReactNode } from 'react'
import { Route } from 'react-router-dom'
import { useFeature, type FeatureFlagKey } from '@/lib/features/FeatureFlagsContext'
import { lazyPage } from '@/lib/utils/lazyPage'
import type { SectionKey } from '@/pages/SectionPages'

// The phase 2 screens' routes, as one fragment App.tsx places inside the signed in shell (before the catch-all route).
// Every page is its own lazy chunk.

const ReadinessPage = lazyPage(() => import('@/features/readiness/ReadinessPage'), 'ReadinessPage')
const PreReviewPage = lazyPage(() => import('@/features/pre-review/PreReviewPage'), 'PreReviewPage')
const AsoKeywordsPage = lazyPage(() => import('@/features/aso/AsoKeywordsPage'), 'AsoKeywordsPage')
const AsoKeywordDetailPage = lazyPage(() => import('@/features/aso/AsoKeywordsPage'), 'AsoKeywordDetailPage')
const ExperimentsPage = lazyPage(() => import('@/features/experiments/ExperimentsPage'), 'ExperimentsPage')
const ExperimentWizardPage = lazyPage(() => import('@/features/experiments/ExperimentWizardPage'), 'ExperimentWizardPage')
const ExperimentDetailPage = lazyPage(() => import('@/features/experiments/ExperimentDetailPage'), 'ExperimentDetailPage')
const ReadinessOverviewPage = lazyPage(() => import('@/features/readiness/ReadinessPage'), 'ReadinessOverviewPage')
const AppCompliancePage = lazyPage(() => import('@/features/compliance/CompliancePages'), 'AppCompliancePage')
const OrgCompliancePage = lazyPage(() => import('@/features/compliance/CompliancePages'), 'OrgCompliancePage')
const AppProfilePage = lazyPage(() => import('@/features/profile/AppProfilePage'), 'AppProfilePage')
const ListingsPage = lazyPage(() => import('@/features/listings/ListingsPage'), 'ListingsPage')
const ListingDetailsPage = lazyPage(() => import('@/features/listings/ListingsPage'), 'ListingDetailsPage')
const ScreenshotsPage = lazyPage(() => import('@/features/screenshots/MediaPage'), 'ScreenshotsPage')
const PreviewsPage = lazyPage(() => import('@/features/screenshots/MediaPage'), 'PreviewsPage')
const CategoriesPage = lazyPage(() => import('@/features/metadata/MetadataPages'), 'CategoriesPage')
const AgeRatingPage = lazyPage(() => import('@/features/metadata/MetadataPages'), 'AgeRatingPage')
const PricingPage = lazyPage(() => import('@/features/metadata/MetadataPages'), 'PricingPage')
const ReviewInfoPage = lazyPage(() => import('@/features/metadata/MetadataPages'), 'ReviewInfoPage')
const ReleasesPage = lazyPage(() => import('@/features/releases/ReleasesPage'), 'ReleasesPage')
const TestersPage = lazyPage(() => import('@/features/releases/TestersPage'), 'TestersPage')
const AnswersPage = lazyPage(() => import('@/features/answers/AnswersPage'), 'AnswersPage')
const AssistedStepsPage = lazyPage(() => import('@/features/assisted/AssistedPages'), 'AssistedStepsPage')
const AssistedStepPage = lazyPage(() => import('@/features/assisted/AssistedPages'), 'AssistedStepPage')
const PlansPage = lazyPage(() => import('@/features/plans/PlansPages'), 'PlansPage')
const PlanDetailPage = lazyPage(() => import('@/features/plans/PlansPages'), 'PlanDetailPage')
const RulesPage = lazyPage(() => import('@/features/rules/RulesPage'), 'RulesPage')
// Phase 8: the organization's custom roles.
const OrgRolesPage = lazyPage(() => import('@/features/access/OrgRolesPage'), 'OrgRolesPage')
// Phase 7: subscriptions, In-App Purchases and purchase settings.
const SubscriptionsPage = lazyPage(() => import('@/features/monetization/SubscriptionsPage'), 'SubscriptionsPage')
const SubscriptionDetailPage = lazyPage(() => import('@/features/monetization/ProductPages'), 'SubscriptionDetailPage')
const BasePlanPricesPage = lazyPage(() => import('@/features/monetization/PlayPanels'), 'BasePlanPricesPage')
const IapListPage = lazyPage(() => import('@/features/monetization/ProductPages'), 'IapListPage')
const IapDetailPage = lazyPage(() => import('@/features/monetization/ProductPages'), 'IapDetailPage')
const PurchaseSettingsPage = lazyPage(() => import('@/features/monetization/PurchaseSettingsPage'), 'PurchaseSettingsPage')

// Phase 4: reviews, rejections, TestFlight, webhooks, notifications, rule watcher.
const SectionPage = lazyPage(() => import('@/pages/SectionPages'), 'SectionPage')
const PlatformPage = lazyPage(() => import('@/pages/SectionPages'), 'PlatformPage')
const ReviewsPage = lazyPage(() => import('@/features/reviews/ReviewsPage'), 'ReviewsPage')
const AppReviewsPage = lazyPage(() => import('@/features/reviews/ReviewsPage'), 'AppReviewsPage')
const SavedRepliesPage = lazyPage(() => import('@/features/reviews/SavedRepliesPage'), 'SavedRepliesPage')
// ASO conversion analytics: store page performance per app and for the organization.
const StoreMetricsPage = lazyPage(() => import('@/features/store-metrics/StoreMetricsPage'), 'StoreMetricsPage')
const OrgStoreMetricsPage = lazyPage(() => import('@/features/store-metrics/StoreMetricsPage'), 'OrgStoreMetricsPage')
const AsoImpactPage = lazyPage(() => import('@/features/aso-impact/AsoImpactPage'), 'AsoImpactPage')
const RejectionsPage = lazyPage(() => import('@/features/rejections/RejectionsPages'), 'RejectionsPage')
const RejectionCasePage = lazyPage(() => import('@/features/rejections/RejectionsPages'), 'RejectionCasePage')
const TestFlightPage = lazyPage(() => import('@/features/testflight/TestFlightPages'), 'TestFlightPage')
const TestFlightGroupPage = lazyPage(() => import('@/features/testflight/TestFlightPages'), 'TestFlightGroupPage')
const TestFlightBuildPage = lazyPage(() => import('@/features/testflight/TestFlightPages'), 'TestFlightBuildPage')
const WebhooksPage = lazyPage(() => import('@/features/webhooks/WebhooksPage'), 'WebhooksPage')
const NotificationsPage = lazyPage(() => import('@/features/notifications/NotificationsPage'), 'NotificationsPage')
const NotificationPreferencesPage = lazyPage(
  () => import('@/features/account-preferences/NotificationPreferencesPage'),
  'NotificationPreferencesPage',
)
const RuleWatcherPage = lazyPage(() => import('@/features/rules-watcher/RuleWatcherPages'), 'RuleWatcherPage')
const RuleProposalPage = lazyPage(() => import('@/features/rules-watcher/RuleWatcherPages'), 'RuleProposalPage')

// Phase 3: personal access tokens, MCP setup and package intake.
const TokensPage = lazyPage(() => import('@/features/tokens/TokensPage'), 'TokensPage')
const McpSetupPage = lazyPage(() => import('@/features/mcp/McpSetupPage'), 'McpSetupPage')
const IntakeRoutes = lazyPage(() => import('@/features/intake/IntakeRoutes'), 'IntakeRoutes')

// Platform administration (design 17.9-17.27): Manager user type and the global permission of each screen.
const UsersPage = lazyPage(() => import('@/features/platform/UsersPages'), 'UsersPage')
const UserDetailPage = lazyPage(() => import('@/features/platform/UsersPages'), 'UserDetailPage')
const RolesPage = lazyPage(() => import('@/features/platform/RolesPage'), 'RolesPage')
const ActivityLogsPage = lazyPage(() => import('@/features/platform/ActivityLogsPage'), 'ActivityLogsPage')
const LanguagesPage = lazyPage(() => import('@/features/platform/LanguagesPage'), 'LanguagesPage')
const FeatureFlagsPage = lazyPage(() => import('@/features/platform/FeatureFlagsPage'), 'FeatureFlagsPage')
const SystemPage = lazyPage(() => import('@/features/platform/SystemPage'), 'SystemPage')

const PLATFORM_ROUTES: Array<{ path: string; section: Extract<SectionKey, `admin${string}`>; page: ReactNode }> = [
  { path: '/admin/users', section: 'adminUsers', page: <UsersPage /> },
  { path: '/admin/users/:userId', section: 'adminUsers', page: <UserDetailPage /> },
  { path: '/admin/roles', section: 'adminRoles', page: <RolesPage /> },
  { path: '/admin/roles/:roleId', section: 'adminRoles', page: <RolesPage /> },
  { path: '/admin/activity-logs', section: 'adminLogs', page: <ActivityLogsPage /> },
  { path: '/admin/languages', section: 'adminLanguages', page: <LanguagesPage /> },
  { path: '/admin/feature-flags', section: 'adminFlags', page: <FeatureFlagsPage /> },
  { path: '/admin/system', section: 'adminSystem', page: <SystemPage /> },
]

// A section behind its feature flag: while the flag is off the route keeps its designed "coming" page (kill switch).
function Flagged({ flag, off, children }: { flag: FeatureFlagKey; off: ReactNode; children: ReactNode }) {
  return <>{useFeature(flag) ? children : off}</>
}

export function contentRoutes() {
  return (
    <>
      <Route path="/apps/readiness" element={<ReadinessOverviewPage />} />
      <Route path="/apps/:appId/readiness" element={<ReadinessPage />} />
      <Route path="/apps/:appId/pre-review" element={<PreReviewPage />} />
      <Route path="/apps/:appId/keywords" element={<AsoKeywordsPage />} />
      <Route path="/apps/:appId/keywords/:keywordId" element={<AsoKeywordDetailPage />} />
      <Route path="/apps/:appId/experiments" element={<ExperimentsPage />} />
      <Route path="/apps/:appId/experiments/new" element={<ExperimentWizardPage />} />
      <Route path="/apps/:appId/experiments/:experimentId" element={<ExperimentDetailPage />} />
      <Route path="/apps/:appId/experiments/:experimentId/edit" element={<ExperimentWizardPage />} />
      <Route path="/apps/:appId/compliance" element={<AppCompliancePage />} />
      <Route path="/apps/:appId/settings/profile" element={<AppProfilePage />} />
      <Route path="/apps/:appId/listing/texts" element={<ListingsPage />} />
      <Route path="/apps/:appId/listing/details" element={<ListingDetailsPage />} />
      <Route path="/apps/:appId/listing/screenshots" element={<ScreenshotsPage />} />
      <Route path="/apps/:appId/listing/previews" element={<PreviewsPage />} />
      <Route path="/apps/:appId/listing/category" element={<CategoriesPage />} />
      <Route path="/apps/:appId/listing/age-rating" element={<AgeRatingPage />} />
      <Route path="/apps/:appId/listing/pricing" element={<PricingPage />} />
      <Route path="/apps/:appId/listing/plans/:planId" element={<PlanDetailPage />} />
      <Route path="/apps/:appId/release/versions" element={<ReleasesPage />} />
      <Route path="/apps/:appId/release/testers" element={<TestersPage />} />
      <Route path="/apps/:appId/release/review-info" element={<ReviewInfoPage />} />
      <Route path="/apps/:appId/answers" element={<AnswersPage />} />
      <Route path="/apps/:appId/assisted" element={<AssistedStepsPage />} />
      <Route path="/apps/:appId/assisted/:stepId" element={<AssistedStepPage />} />
      <Route path="/activity/plans" element={<PlansPage />} />
      <Route path="/activity/plans/:planId" element={<PlanDetailPage />} />
      <Route path="/compliance" element={<OrgCompliancePage />} />
      <Route path="/members/roles" element={<OrgRolesPage />} />
      <Route path="/rules" element={<RulesPage />} />
      <Route path="/rules/:ruleId" element={<RulesPage />} />
      <Route path="/apps/:appId/money/subscriptions" element={<SubscriptionsPage />} />
      <Route path="/apps/:appId/money/subscriptions/:subscriptionId" element={<SubscriptionDetailPage tab="general" />} />
      <Route path="/apps/:appId/money/subscriptions/:subscriptionId/prices" element={<SubscriptionDetailPage tab="prices" />} />
      <Route path="/apps/:appId/money/subscriptions/:subscriptionId/offers" element={<SubscriptionDetailPage tab="offers" />} />
      <Route path="/apps/:appId/money/subscriptions/:subscriptionId/base-plans/:basePlanId" element={<BasePlanPricesPage />} />
      <Route path="/apps/:appId/money/iap" element={<IapListPage />} />
      <Route path="/apps/:appId/money/iap/:iapId" element={<IapDetailPage tab="general" />} />
      <Route path="/apps/:appId/money/iap/:iapId/prices" element={<IapDetailPage tab="prices" />} />
      <Route path="/apps/:appId/money/settings" element={<PurchaseSettingsPage />} />
      <Route
        path="/reviews"
        element={
          <Flagged flag="Reviews" off={<SectionPage section="reviews" />}>
            <ReviewsPage />
          </Flagged>
        }
      />
      <Route
        path="/reviews/saved-replies"
        element={
          <Flagged flag="Reviews" off={<SectionPage section="reviews" />}>
            <SavedRepliesPage />
          </Flagged>
        }
      />
      <Route path="/apps/:appId/reviews" element={<AppReviewsPage />} />
      <Route path="/apps/:appId/store-performance" element={<StoreMetricsPage />} />
      <Route path="/apps/:appId/store-performance/impact" element={<AsoImpactPage />} />
      <Route path="/store-performance" element={<OrgStoreMetricsPage />} />
      <Route path="/apps/:appId/rejections" element={<RejectionsPage />} />
      <Route path="/apps/:appId/rejections/:caseId" element={<RejectionCasePage />} />
      <Route path="/apps/:appId/release/testflight" element={<TestFlightPage />} />
      <Route path="/apps/:appId/release/testflight/groups/:groupId" element={<TestFlightGroupPage />} />
      <Route path="/apps/:appId/release/testflight/builds/:buildId" element={<TestFlightBuildPage />} />
      <Route
        path="/webhooks"
        element={
          <Flagged flag="Webhooks" off={<SectionPage section="webhooks" />}>
            <WebhooksPage />
          </Flagged>
        }
      />
      <Route path="/notifications" element={<NotificationsPage />} />
      <Route path="/account/notifications" element={<NotificationPreferencesPage />} />
      <Route
        path="/admin/rules"
        element={
          <Flagged flag="RuleWatcher" off={<PlatformPage section="adminRules" />}>
            <RuleWatcherPage />
          </Flagged>
        }
      />
      <Route path="/admin/rules/proposals/:proposalId" element={<RuleProposalPage />} />
      <Route
        path="/developer"
        element={
          <Flagged flag="Developer" off={<SectionPage section="developer" />}>
            <TokensPage />
          </Flagged>
        }
      />
      <Route
        path="/developer/setup"
        element={
          <Flagged flag="Developer" off={<SectionPage section="developer" />}>
            <McpSetupPage />
          </Flagged>
        }
      />
      {PLATFORM_ROUTES.map((route) => (
        <Route
          key={route.path}
          path={route.path}
          element={
            <Flagged flag="Platform" off={<PlatformPage section={route.section} />}>
              {route.page}
            </Flagged>
          }
        />
      ))}
      <Route
        path="/intake/*"
        element={
          <Flagged flag="Intake" off={<SectionPage section="intake" />}>
            <IntakeRoutes />
          </Flagged>
        }
      />
    </>
  )
}
