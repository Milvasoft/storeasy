import type { ReactNode } from 'react'
import { Route } from 'react-router-dom'
import { useFeature, type FeatureFlagKey } from '@/lib/features/FeatureFlagsContext'
import { lazyPage } from '@/lib/utils/lazyPage'

// The phase 2 screens' routes, as one fragment App.tsx places inside the signed in shell (before the catch-all route).
// Every page is its own lazy chunk.

const ReadinessPage = lazyPage(() => import('@/features/readiness/ReadinessPage'), 'ReadinessPage')
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

// Phase 4: reviews, rejections, TestFlight, webhooks, notifications, rule watcher.
const SectionPage = lazyPage(() => import('@/pages/SectionPages'), 'SectionPage')
const PlatformPage = lazyPage(() => import('@/pages/SectionPages'), 'PlatformPage')
const ReviewsPage = lazyPage(() => import('@/features/reviews/ReviewsPage'), 'ReviewsPage')
const AppReviewsPage = lazyPage(() => import('@/features/reviews/ReviewsPage'), 'AppReviewsPage')
const SavedRepliesPage = lazyPage(() => import('@/features/reviews/SavedRepliesPage'), 'SavedRepliesPage')
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

// A section behind its feature flag: while the flag is off the route keeps its designed "coming" page (kill switch).
function Flagged({ flag, off, children }: { flag: FeatureFlagKey; off: ReactNode; children: ReactNode }) {
  return <>{useFeature(flag) ? children : off}</>
}

export function contentRoutes() {
  return (
    <>
      <Route path="/apps/readiness" element={<ReadinessOverviewPage />} />
      <Route path="/apps/:appId/readiness" element={<ReadinessPage />} />
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
      <Route path="/rules" element={<RulesPage />} />
      <Route path="/rules/:ruleId" element={<RulesPage />} />
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
    </>
  )
}
