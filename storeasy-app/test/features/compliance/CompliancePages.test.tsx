import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AppCompliancePage, OrgCompliancePage } from '@/features/compliance/CompliancePages'
import { ComplianceStatus } from '@/features/common/types'
import { complianceItem, contentState, setupContentApi } from '../../support/content'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo } from '../../support/state'

setupContentApi()

describe('OrgCompliancePage', () => {
  it('groups the open deadlines by month, nearest first, with the days left', async () => {
    renderWithProviders(<OrgCompliancePage />, { route: '/compliance' })

    const months = await screen.findAllByRole('heading', { level: 2 })
    expect(months.map((heading) => heading.textContent)).toEqual(['Aralık 2026', 'Ocak 2027'])
    expect(screen.getByText('22 gün')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Defter' })).toHaveAttribute('href', '/apps/102/compliance')
    expect(lastRequestTo('/Compliance')?.query).toMatchObject({ OpenOnly: 'true' })
  })

  it('lists done items too when "All" is chosen', async () => {
    const { user } = renderWithProviders(<OrgCompliancePage />, { route: '/compliance' })
    await screen.findByText('22 gün')

    await user.click(screen.getByRole('button', { name: 'Tümü' }))

    await screen.findByText('22 gün')
    expect(lastRequestTo('/Compliance')?.query).toMatchObject({ OpenOnly: 'false' })
  })
})

describe('AppCompliancePage', () => {
  it('shows a late deadline in danger, the source, and a note when sources disagree', async () => {
    contentState.appCompliance = [complianceItem({ DaysLeft: -3, Conflict: 'Sürüm notu farklı diyor.', AssistedStepId: 5521 })]

    renderWithProviders(<AppCompliancePage />, { route: '/apps/101/compliance', path: '/apps/:appId/compliance' })

    expect(await screen.findByText('3 gün geçti')).toBeInTheDocument()
    expect(screen.getByText('Kaynaklar çelişiyor')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Kaynak' })).toHaveAttribute('href', 'https://developer.apple.com/news/')
    expect(screen.getByRole('link', { name: 'Asistanlı adımı aç' })).toHaveAttribute('href', '/apps/101/assisted/5521')
  })

  it('shows a passed date as in effect, not late, while the state is unknown, and a missing item as Eksik', async () => {
    contentState.appCompliance = [
      complianceItem({ RuleId: 'a.unknown', DaysLeft: -206, Status: ComplianceStatus.Unknown }),
      complianceItem({ RuleId: 'b.missing', DaysLeft: -128 }),
    ]

    renderWithProviders(<AppCompliancePage />, { route: '/apps/101/compliance', path: '/apps/:appId/compliance' })

    expect(await screen.findByText('206 gündür yürürlükte')).toBeInTheDocument()
    expect(screen.queryByText('206 gün geçti')).not.toBeInTheDocument()
    expect(screen.getByText('128 gün geçti')).toBeInTheDocument()
    expect(screen.getByText('Eksik')).toBeInTheDocument()
  })

  it('says so when no deadline applies', async () => {
    contentState.appCompliance = []

    renderWithProviders(<AppCompliancePage />, { route: '/apps/101/compliance', path: '/apps/:appId/compliance' })

    expect(await screen.findByText('Geçerli son tarih yok')).toBeInTheDocument()
  })
})
