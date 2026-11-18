import { Route } from 'react-router-dom'
import { lazyPage } from '@/lib/utils/lazyPage'

// The Microsoft Store screens' routes, as one fragment App.tsx places inside the signed in shell next to contentRoutes().
// Every page is its own lazy chunk.

const MsProductPage = lazyPage(() => import('@/features/microsoft/ProductPage'), 'MsProductPage')
const MsListingsPage = lazyPage(() => import('@/features/microsoft/ListingsPage'), 'MsListingsPage')
const MsImagesPage = lazyPage(() => import('@/features/microsoft/ImagesPage'), 'MsImagesPage')
const MsPricingPage = lazyPage(() => import('@/features/microsoft/PricingPage'), 'MsPricingPage')
const MsPackagesPage = lazyPage(() => import('@/features/microsoft/PackagesPage'), 'MsPackagesPage')
const MsInstallersPage = lazyPage(() => import('@/features/microsoft/InstallersPage'), 'MsInstallersPage')
const MsFlightsPage = lazyPage(() => import('@/features/microsoft/FlightsPage'), 'MsFlightsPage')
const MsAddonsPage = lazyPage(() => import('@/features/microsoft/AddonsPage'), 'MsAddonsPage')
const MsSubmissionsPage = lazyPage(() => import('@/features/microsoft/SubmissionsPage'), 'MsSubmissionsPage')

export function microsoftRoutes() {
  return (
    <>
      <Route path="/apps/:appId/ms" element={<MsProductPage />} />
      <Route path="/apps/:appId/ms/product" element={<MsProductPage />} />
      <Route path="/apps/:appId/ms/listings" element={<MsListingsPage />} />
      <Route path="/apps/:appId/ms/images" element={<MsImagesPage />} />
      <Route path="/apps/:appId/ms/pricing" element={<MsPricingPage />} />
      <Route path="/apps/:appId/ms/packages" element={<MsPackagesPage />} />
      <Route path="/apps/:appId/ms/installers" element={<MsInstallersPage />} />
      <Route path="/apps/:appId/ms/flights" element={<MsFlightsPage />} />
      <Route path="/apps/:appId/ms/addons" element={<MsAddonsPage />} />
      <Route path="/apps/:appId/ms/submissions" element={<MsSubmissionsPage />} />
    </>
  )
}
