import { Route, Routes } from 'react-router-dom'
import { NotFoundPage } from '@/pages/StatePages'
import { IntakeCiTokensPage } from './CiTokensPage'
import { IntakeOverviewPage } from './OverviewPage'
import { IntakePublishersPage } from './PublishersPage'
import { IntakeRulesPage } from './RulesPage'
import { IntakeSessionPage, IntakeSessionsPage } from './SessionsPages'
import { IntakeSettingsPage, IntakeSetupPage } from './SetupPages'
import { IntakeSourcesPage } from './SourcesPage'

/** Everything under /intake (design 13), one lazy chunk. */
export function IntakeRoutes() {
  return (
    <Routes>
      <Route index element={<IntakeOverviewPage />} />
      <Route path="sessions" element={<IntakeSessionsPage />} />
      <Route path="sessions/:sessionId" element={<IntakeSessionPage />} />
      <Route path="rules" element={<IntakeRulesPage />} />
      <Route path="trusted-publishers" element={<IntakePublishersPage />} />
      <Route path="tokens" element={<IntakeCiTokensPage />} />
      <Route path="sources" element={<IntakeSourcesPage />} />
      <Route path="setup" element={<IntakeSetupPage />} />
      <Route path="settings" element={<IntakeSettingsPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
