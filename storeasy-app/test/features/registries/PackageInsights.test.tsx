import { configure, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PackageGroupsPage } from '@/features/registries/GroupsPage'
import { RegistryPackagePage, sortPackages } from '@/features/registries/RegistriesPage'
import { renderWithProviders } from '../../support/render'
import { NPM_PACKAGE_ID, NUGET_PACKAGE_ID, packageSummary, setupRegistriesApi } from '../../support/registries'
import { lastRequestTo } from '../../support/state'

setupRegistriesApi()

configure({ asyncUtilTimeout: 5000 })

function renderPackage() {
  return renderWithProviders(<RegistryPackagePage />, {
    route: `/registries/${NPM_PACKAGE_ID}`,
    path: '/registries/:packageId',
    lang: 'en',
  })
}

describe('package page tabs', () => {
  it('shows downloads with the chart and the per version counts', async () => {
    const { user } = renderPackage()

    await user.click(await screen.findByRole('tab', { name: 'Downloads' }))
    const section = await screen.findByRole('region', { name: 'Downloads' })
    expect(within(section).getByText('Last 30 days')).toBeInTheDocument()
    expect(within(section).getByText('Last 7 days per version')).toBeInTheDocument()
    expect(within(section).getAllByText('90').length).toBeGreaterThan(0)
    expect(lastRequestTo('/registries/downloads')?.query).toMatchObject({ TrackedPackageId: String(NPM_PACKAGE_ID) })
  })

  it('shows the live listing as text with its checks', async () => {
    const { user } = renderPackage()

    await user.click(await screen.findByRole('tab', { name: 'Listing' }))
    const checks = await screen.findByRole('article', { name: 'Readiness' })
    expect(within(checks).getByText('Could be better')).toBeInTheDocument()
    const readme = screen.getByRole('article', { name: 'README' })
    expect(within(readme).getByText(/<script>alert\(1\)<\/script>/)).toBeInTheDocument()
    expect(screen.getByText(/ships with the package/)).toBeInTheDocument()
  })
})

describe('package groups', () => {
  it('lists groups with who is behind and creates a suggested group', async () => {
    const { user } = renderWithProviders(<PackageGroupsPage />, { route: '/registries/groups', path: '/registries/groups', lang: 'en' })

    const group = await screen.findByRole('article', { name: 'Pusula SDKs' })
    expect(within(group).getByText('1 behind')).toBeInTheDocument()
    expect(within(group).getByText('Behind')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Create this group' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByLabelText('Name')).toHaveValue('kuzeylab/tools')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Group saved.')).toBeInTheDocument()
    expect(lastRequestTo('/registries/group')?.body).toMatchObject({ Name: 'kuzeylab/tools', TrackedPackageIds: [41, 42] })
  })
})

describe('summary order', () => {
  it('sorts by downloads, name and latest release', () => {
    const rows = [
      packageSummary({ Id: 1, Name: 'b', TotalDownloads: 10, LatestPublishedDate: '2026-11-01T00:00:00Z' }),
      packageSummary({ Id: 2, Name: 'a', TotalDownloads: null, RecentDownloads: 50, LatestPublishedDate: '2026-11-20T00:00:00Z' }),
      packageSummary({ Id: NUGET_PACKAGE_ID, Name: 'c', TotalDownloads: null, RecentDownloads: null, LatestPublishedDate: null }),
    ]

    expect(sortPackages(rows, 'downloads').map((r) => r.Id)).toEqual([2, 1, NUGET_PACKAGE_ID])
    expect(sortPackages(rows, 'name').map((r) => r.Name)).toEqual(['a', 'b', 'c'])
    expect(sortPackages(rows, 'published').map((r) => r.Id)).toEqual([2, 1, NUGET_PACKAGE_ID])
  })
})
