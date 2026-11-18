import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { App } from '@/App'
import { RootErrorBoundary } from '@/components/common/ErrorBoundary'
import { ConfirmProvider } from '@/components/ui/ConfirmProvider'
import { Toaster } from '@/components/ui/Toaster'
import { AuthProvider } from '@/lib/auth/AuthContext'
import { FeatureFlagsProvider } from '@/lib/features/FeatureFlagsContext'
import { I18nProvider } from '@/lib/i18n/I18nContext'
import { OrganizationProvider } from '@/lib/org/OrganizationContext'
import { queryClient } from '@/lib/query/queryClient'
import { applyTheme, watchSystemTheme } from '@/lib/theme/theme'
// Fonts are served with the app: no request to Google Fonts, which would hand every visitor's IP address to Google.
import '@fontsource/bricolage-grotesque/700.css'
import '@fontsource/bricolage-grotesque/800.css'
import '@fontsource/onest/400.css'
import '@fontsource/onest/500.css'
import '@fontsource/onest/600.css'
import '@fontsource/onest/700.css'
import '@fontsource/jetbrains-mono/500.css'
import '@fontsource/jetbrains-mono/600.css'
import '@fontsource/jetbrains-mono/700.css'
import '@/index.css'

// Apply the saved light/dark/system preference before the first render so there is no flash.
applyTheme()
watchSystemTheme()

function render() {
  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <RootErrorBoundary>
        <BrowserRouter>
          <QueryClientProvider client={queryClient}>
            <FeatureFlagsProvider>
              <I18nProvider>
                <AuthProvider>
                  <OrganizationProvider>
                    <ConfirmProvider>
                      <App />
                      <Toaster />
                    </ConfirmProvider>
                  </OrganizationProvider>
                </AuthProvider>
              </I18nProvider>
            </FeatureFlagsProvider>
          </QueryClientProvider>
        </BrowserRouter>
      </RootErrorBoundary>
    </React.StrictMode>,
  )
}

// Dev only (VITE_MOCK_API=true): the in-browser mock answers the API before anything is fetched. The condition is written
// out here (not read from config) so the build replaces it with false and the mock never reaches the production bundle.
if (import.meta.env.DEV && import.meta.env.VITE_MOCK_API === 'true') {
  void import('@/mocks/browser').then(({ startMockApi }) => startMockApi()).then(render)
} else {
  render()
}
