import { Route } from 'react-router-dom'
import { lazyPage } from '@/lib/utils/lazyPage'

// The Chrome Web Store screen's route, as one fragment App.tsx places inside the signed in shell next to the other
// stores' routes.

const ChromePage = lazyPage(() => import('@/features/chrome/ChromePage'), 'ChromePage')

export function chromeRoutes() {
  return <Route path="/apps/:appId/chrome" element={<ChromePage />} />
}
