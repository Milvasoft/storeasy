import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgSettingsPage } from '@/pages/org/OrgSettingsPage'
import { organization, TEST_ORG_ID } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

describe('OrgSettingsPage short name', () => {
  it('warns what breaks while the short name is edited, and sends the new one', async () => {
    const { user } = renderWithProviders(<OrgSettingsPage />)

    const slug = await screen.findByLabelText('Kısa ad')
    await user.clear(slug)
    await user.type(slug, 'Kuzey Labs')

    expect(slug).toHaveValue('kuzey-labs')
    expect(screen.getByText(/kuzey-lab ile başlayan paylaşılmış bağlantılar/)).toBeInTheDocument()
    expect(screen.getByText('1 değişiklik')).toBeInTheDocument()
    testState.organizations = [organization({ Slug: 'kuzey-labs' })]
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() =>
      expect(lastRequestTo('/Organizations/organization')?.body).toEqual({
        OrganizationId: TEST_ORG_ID,
        Name: 'Kuzey Lab',
        Slug: 'kuzey-labs',
      }),
    )
    expect(await screen.findByText('Ayarlar kaydedildi')).toBeInTheDocument()
  })

  it('keeps a taken short name in the form with suggestions (409)', async () => {
    server.use(endpoint('put', '/Organizations/organization', () => errorResponse('OrganizationSlugTaken', 'Kısa ad kullanımda.', 409)))
    const { user } = renderWithProviders(<OrgSettingsPage />)

    const slug = await screen.findByLabelText('Kısa ad')
    await user.clear(slug)
    await user.type(slug, 'mavi-atolye')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Bu kısa ad kullanımda. Başka bir kısa ad seç; ad aynı kalabilir.')).toBeInTheDocument()
    expect(screen.getByText('mavi-atolye başka bir organizasyonda.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'mavi-atolye-2' }))
    expect(slug).toHaveValue('mavi-atolye-2')
    expect(screen.queryByText('mavi-atolye başka bir organizasyonda.')).not.toBeInTheDocument()
  })

  it('refuses a short name that is too short before sending', async () => {
    const { user } = renderWithProviders(<OrgSettingsPage />)

    const slug = await screen.findByLabelText('Kısa ad')
    await user.clear(slug)
    await user.type(slug, 'ab')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    expect(screen.getByText('En az 3 karakter: küçük harf, rakam ve tire.')).toBeInTheDocument()
    expect(requestsTo('/Organizations/organization')).toHaveLength(0)
  })

  it('puts both fields back with "cancel"', async () => {
    const { user } = renderWithProviders(<OrgSettingsPage />)

    const slug = await screen.findByLabelText('Kısa ad')
    await user.type(slug, '-x')
    await user.click(screen.getByRole('button', { name: 'Vazgeç' }))

    expect(slug).toHaveValue('kuzey-lab')
    expect(screen.getByRole('button', { name: 'Kaydet' })).toBeDisabled()
  })

  it('shows another refusal at the top of the form', async () => {
    server.use(endpoint('put', '/Organizations/organization', () => errorResponse('Forbidden', 'Yetkin yok.', 403)))
    const { user } = renderWithProviders(<OrgSettingsPage />)

    const name = await screen.findByLabelText('Ad')
    await user.type(name, ' 2')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Yetkin yok.')).toBeInTheDocument()
  })
})
