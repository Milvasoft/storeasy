import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgPermission, OrgRole } from '@/lib/api/types'
import { ChangePlanKind, ChangePlanStatus } from '@/features/common/types'
import { PlanDetailPage, PlansPage } from '@/features/plans/PlansPages'
import { changePlan, contentState, setupContentApi } from '../../support/content'
import { errorResponse, endpoint } from '../../support/handlers'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupContentApi()

function renderDetail() {
  return renderWithProviders(<PlanDetailPage />, { route: '/activity/plans/7001', path: '/activity/plans/:planId' })
}

describe('PlansPage', () => {
  it('lists the pending plans with the permission each needs', async () => {
    renderWithProviders(<PlansPage />, { route: '/activity/plans' })

    expect(await screen.findByRole('link', { name: 'de-DE anahtar kelimeler' })).toHaveAttribute('href', '/activity/plans/7001')
    expect(screen.getByText('İçerik düzenleme')).toBeInTheDocument()
    expect(lastRequestTo('/plans')?.body).toMatchObject({ Status: ChangePlanStatus.Pending, PageNumber: 1 })
  })

  it('lists every plan when "All" is chosen', async () => {
    const { user } = renderWithProviders(<PlansPage />, { route: '/activity/plans' })
    await screen.findByRole('link', { name: 'de-DE anahtar kelimeler' })

    await user.click(screen.getByRole('button', { name: 'Tümü' }))

    await screen.findByRole('link', { name: 'de-DE anahtar kelimeler' })
    expect(lastRequestTo('/plans')?.body).toMatchObject({ Status: null })
  })

  it('shows the empty queue', async () => {
    contentState.plans = []

    renderWithProviders(<PlansPage />, { route: '/activity/plans' })

    expect(await screen.findByText('Onay bekleyen plan yok')).toBeInTheDocument()
  })
})

describe('PlanDetailPage', () => {
  it('keeps the approve button of a production plan disabled for an Editor, with the reason', async () => {
    contentState.plan = changePlan({ Kind: ChangePlanKind.TrackRelease, RequiredPermission: OrgPermission.ReleaseProduction })
    testState.organizations = [organization({ Role: OrgRole.Editor })]

    renderDetail()

    expect(await screen.findByRole('button', { name: 'Onayla ve gönder' })).toBeDisabled()
    expect(screen.getByText('Bu planı onaylamak Production yayını iznini gerektirir.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reddet' })).toBeEnabled()
  })

  it('shows no decision to a Viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    renderDetail()

    expect(await screen.findByText('karte,gps')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Onayla ve gönder' })).not.toBeInTheDocument()
  })

  it('asks for the typed app name and version before approving a store-bound plan', async () => {
    contentState.plan = changePlan({ ConfirmRequired: true, ConfirmText: 'Pusula 2.4.0', Kind: ChangePlanKind.TrackRelease })
    const { user } = renderDetail()

    await user.click(await screen.findByRole('button', { name: 'Onayla ve gönder' }))
    const confirm = await screen.findByRole('alertdialog')
    expect(within(confirm).getByRole('button', { name: 'Onayla ve gönder' })).toBeDisabled()
    await user.type(within(confirm).getByRole('textbox'), 'Pusula 2.4.0')
    await user.click(within(confirm).getByRole('button', { name: 'Onayla ve gönder' }))

    await screen.findByText('Plan onaylandı; mağaza işi kuyruğa girdi.')
    expect(lastRequestTo('/plans/approve')?.body).toMatchObject({ PlanId: 7001, Confirm: 'Pusula 2.4.0' })
  })

  it('says the store changed when approval answers 409', async () => {
    server.use(endpoint('post', '/plans/approve', () => errorResponse('PlanConflict', 'Mağaza değişti.', 409)))
    const { user } = renderDetail()

    await user.click(await screen.findByRole('button', { name: 'Onayla ve gönder' }))

    expect(await screen.findByText(/Plan hazırlandıktan sonra mağazada değişti/)).toBeInTheDocument()
  })

  it('needs a reason of at least 10 characters to reject', async () => {
    const { user } = renderDetail()
    await user.click(await screen.findByRole('button', { name: 'Reddet' }))
    const dialog = await screen.findByRole('dialog', { name: 'Plan reddedilsin mi?' })

    await user.type(within(dialog).getByLabelText('Gerekçe'), 'kısa')
    await user.click(within(dialog).getByRole('button', { name: 'Reddet' }))
    expect(within(dialog).getByText('Gerekçe en az 10 karakter olmalı.')).toBeInTheDocument()
    expect(requestsTo('/plans/reject')).toHaveLength(0)
    await user.type(within(dialog).getByLabelText('Gerekçe'), ' ve marka adı var')
    await user.click(within(dialog).getByRole('button', { name: 'Reddet' }))

    await screen.findByText('Plan reddedildi; mağazaya hiçbir şey yazılmadı.')
    expect(lastRequestTo('/plans/reject')?.body).toMatchObject({ PlanId: 7001, Reason: 'kısa ve marka adı var' })
  })

  it('withdraws a plan', async () => {
    const { user } = renderDetail()

    await user.click(await screen.findByRole('button', { name: 'Geri çek' }))

    await screen.findByText('Plan geri çekildi; mağazaya hiçbir şey yazılmadı.')
    expect(lastRequestTo('/plans/discard')?.body).toMatchObject({ PlanId: 7001 })
  })
})
