import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { AssistedStepStatus } from '@/features/common/types'
import { AssistedStepPage, AssistedStepsPage } from '@/features/assisted/AssistedPages'
import { setupContentApi } from '../../support/content'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupContentApi()

function renderStep() {
  return renderWithProviders(<AssistedStepPage />, { route: '/apps/101/assisted/5521', path: '/apps/:appId/assisted/:stepId' })
}

describe('AssistedStepsPage', () => {
  it('lists the steps of the app with their state and last run', async () => {
    renderWithProviders(<AssistedStepsPage />, { route: '/apps/101/assisted', path: '/apps/:appId/assisted' })

    expect(await screen.findByRole('link', { name: 'App Privacy etiketlerini gir' })).toHaveAttribute('href', '/apps/101/assisted/5521')
    expect(screen.getByText('Yapılacak')).toBeInTheDocument()
    expect(screen.getByText(/deniz@kuzeylab\.dev · Web/)).toBeInTheDocument()
    expect(lastRequestTo('/assisted-steps')?.body).toMatchObject({ AppId: 101 })
  })
})

describe('AssistedStepPage', () => {
  it('shows the checklist, the store link, the pitfalls and the answers used', async () => {
    renderStep()

    expect(await screen.findByRole('heading', { name: 'Kontrol listesi 1 / 2' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Mağazada aç' })).toHaveAttribute(
      'href',
      'https://appstoreconnect.apple.com/apps/6740056789/privacy',
    )
    expect(screen.getByText('Her türden sonra Save.')).toBeInTheDocument()
    expect(screen.getByText('Gizlilik etiketleri')).toBeInTheDocument()
  })

  it('hands the step off and shows the prompt for Claude', async () => {
    const { user } = renderStep()

    await user.click(await screen.findByRole('button', { name: 'Tarayıcıya devret' }))

    expect(await screen.findByText('App Privacy etiketlerini kayıtlı cevaplarla doldur.')).toBeInTheDocument()
    expect(screen.getByText('assisted_step_get')).toBeInTheDocument()
    expect(lastRequestTo('/assisted-steps/step/handoff')?.body).toMatchObject({ StepId: 5521 })
  })

  it('marks the step done with the ticked items and a note', async () => {
    const { user } = renderStep()
    await user.click(await screen.findByRole('checkbox', { name: 'Veri türlerini işaretle' }))

    await user.click(screen.getByRole('button', { name: 'Yapıldı olarak kaydet' }))
    const dialog = await screen.findByRole('dialog', { name: 'Adım yapıldı' })
    await user.type(within(dialog).getByLabelText(/Not/), 'Published göründü.')
    await user.click(within(dialog).getByRole('button', { name: 'Yapıldı olarak kaydet' }))

    await screen.findByText('Adım kaydedildi.')
    expect(lastRequestTo('/assisted-steps/step/complete')?.body).toMatchObject({
      StepId: 5521,
      Status: AssistedStepStatus.Done,
      DoneItems: [1, 2],
      EvidenceAssetId: null,
      Note: 'Published göründü.',
    })
  })

  it('marks a step as not needed', async () => {
    const { user } = renderStep()

    await user.click(await screen.findByRole('button', { name: 'Gerekmiyor' }))

    await screen.findByText('Adım gerekmiyor olarak işaretlendi.')
    expect(lastRequestTo('/assisted-steps/step/complete')?.body).toMatchObject({ Status: AssistedStepStatus.Skipped })
  })

  it('offers no hand-off or completion to a Viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    renderStep()

    expect(await screen.findByRole('checkbox', { name: 'Veri türlerini işaretle' })).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Tarayıcıya devret' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yapıldı olarak kaydet' })).not.toBeInTheDocument()
  })
})
