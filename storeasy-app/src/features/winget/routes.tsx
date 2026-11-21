import { Route } from 'react-router-dom'
import { lazyPage } from '@/lib/utils/lazyPage'

// The winget screen's route, as one fragment App.tsx places inside the signed in shell next to the other stores' routes.

const WingetPage = lazyPage(() => import('@/features/winget/WingetPage'), 'WingetPage')

export function wingetRoutes() {
  return <Route path="/apps/:appId/winget" element={<WingetPage />} />
}
