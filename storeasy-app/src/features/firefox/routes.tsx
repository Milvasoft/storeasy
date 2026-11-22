import { Route } from 'react-router-dom'
import { lazyPage } from '@/lib/utils/lazyPage'

// The Firefox Add-ons screen's route, as one fragment App.tsx places inside the signed in shell next to the other stores'
// routes.

const FirefoxPage = lazyPage(() => import('@/features/firefox/FirefoxPage'), 'FirefoxPage')

export function firefoxRoutes() {
  return <Route path="/apps/:appId/firefox" element={<FirefoxPage />} />
}
