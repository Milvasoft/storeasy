import { Route } from 'react-router-dom'
import { lazyPage } from '@/lib/utils/lazyPage'

// The package registries routes, as one fragment App.tsx places inside the signed in shell.

const RegistriesPage = lazyPage(() => import('@/features/registries/RegistriesPage'), 'RegistriesPage')
const RegistryPackagePage = lazyPage(() => import('@/features/registries/RegistriesPage'), 'RegistryPackagePage')

export function registriesRoutes() {
  return (
    <>
      <Route path="/registries" element={<RegistriesPage />} />
      <Route path="/registries/:packageId" element={<RegistryPackagePage />} />
    </>
  )
}
