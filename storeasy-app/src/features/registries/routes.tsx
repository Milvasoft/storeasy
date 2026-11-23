import { Route } from 'react-router-dom'
import { lazyPage } from '@/lib/utils/lazyPage'

// The package registries routes, as one fragment App.tsx places inside the signed in shell.

const RegistriesPage = lazyPage(() => import('@/features/registries/RegistriesPage'), 'RegistriesPage')
const RegistryPackagePage = lazyPage(() => import('@/features/registries/RegistriesPage'), 'RegistryPackagePage')
const PackageGroupsPage = lazyPage(() => import('@/features/registries/GroupsPage'), 'PackageGroupsPage')

export function registriesRoutes() {
  return (
    <>
      <Route path="/registries" element={<RegistriesPage />} />
      <Route path="/registries/groups" element={<PackageGroupsPage />} />
      <Route path="/registries/:packageId" element={<RegistryPackagePage />} />
    </>
  )
}
