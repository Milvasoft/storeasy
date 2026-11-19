import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { OrgAvatar } from '@/components/org/OrgAvatar'
import { OrgRole } from '@/lib/api/types'
import { OrgSettingsPage } from '@/pages/org/OrgSettingsPage'
import { organization, TEST_ORG_ID } from '../../support/factories'
import { endpoint, errorResponse, STORAGE_URL } from '../../support/handlers'
import { renderApp, renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

const LOGO_URL = `${STORAGE_URL}/org/7/logo/current.png`

// The presigned storage: records what each PUT received.
function storage() {
  const received: Array<{ path: string; contentType: string | null; size: number }> = []
  server.use(
    http.put(`${STORAGE_URL}/*`, async ({ request }) => {
      const body = await request.arrayBuffer()
      received.push({ path: new URL(request.url).pathname, contentType: request.headers.get('Content-Type'), size: body.byteLength })
      return new HttpResponse(null, { status: 200, headers: { 'Access-Control-Allow-Origin': '*' } })
    }),
  )
  return received
}

function logoCard() {
  return screen.findByRole('region', { name: 'Logo' })
}

describe('OrgAvatar', () => {
  it('shows the initial without a logo', () => {
    const { container } = render(<OrgAvatar name="istanbul lab" />)

    expect(container.querySelector('img')).toBeNull()
    expect(screen.getByText('İ')).toBeInTheDocument()
  })

  it('shows the logo when there is one', () => {
    const { container } = render(<OrgAvatar name="Kuzey Lab" logoUrl={LOGO_URL} />)

    expect(container.querySelector('img')).toHaveAttribute('src', LOGO_URL)
    expect(screen.queryByText('K')).not.toBeInTheDocument()
  })

  it('falls back to the initial when the logo does not load, and tries a new logo again', () => {
    const { container, rerender } = render(<OrgAvatar name="Kuzey Lab" logoUrl={LOGO_URL} />)

    fireEvent.error(container.querySelector('img')!)

    expect(container.querySelector('img')).toBeNull()
    expect(screen.getByText('K')).toBeInTheDocument()
    rerender(<OrgAvatar name="Kuzey Lab" logoUrl={`${STORAGE_URL}/org/7/logo/next.png`} />)
    expect(container.querySelector('img')).toHaveAttribute('src', `${STORAGE_URL}/org/7/logo/next.png`)
  })
})

describe('organization switcher logo', () => {
  it('shows the organization logo in the menu instead of the initial', async () => {
    testState.organizations = [organization({ LogoUrl: LOGO_URL })]

    const { user } = await renderApp({ route: '/apps' })

    const trigger = (await screen.findAllByRole('button', { name: 'Organizasyon: Kuzey Lab. Değiştir' }))[0]
    expect(trigger.querySelector('img')).toHaveAttribute('src', LOGO_URL)
    await user.click(trigger)
    const item = await screen.findByRole('menuitemradio', { name: /Kuzey Lab/ })
    expect(item.querySelector('img')).toHaveAttribute('src', LOGO_URL)
  })
})

describe('OrgSettingsPage logo', () => {
  it('uploads a dropped logo: declares it, PUTs it to storage, confirms it and reloads the organizations', async () => {
    const received = storage()
    const { user } = renderWithProviders(<OrgSettingsPage />)
    const card = await logoCard()
    expect(within(card).getByText('Henüz logo yok: adın baş harfi gösteriliyor.')).toBeInTheDocument()
    const loadsBefore = requestsTo('/Organizations/mine').length
    testState.organizations = [organization({ LogoUrl: LOGO_URL })]

    await user.upload(within(card).getByLabelText('Logo seç'), new File([new Uint8Array(1234)], 'Logo.WEBP', { type: '' }))

    expect(await screen.findByText('Logo kaydedildi.')).toBeInTheDocument()
    expect(lastRequestTo('/Organizations/logo/upload')?.body).toEqual({ OrganizationId: TEST_ORG_ID, ContentType: 'image/webp', Size: 1234 })
    expect(received).toEqual([{ path: '/bucket/logo', contentType: 'image/webp', size: 1234 }])
    expect(lastRequestTo('/Organizations/logo')?.body).toEqual({ OrganizationId: TEST_ORG_ID, AssetId: 601 })
    expect(requestsTo('/Organizations/mine').length).toBeGreaterThan(loadsBefore)
    await waitFor(() => expect(card.querySelector('img')).toHaveAttribute('src', LOGO_URL))
  })

  it('refuses another file type before anything is sent', async () => {
    renderWithProviders(<OrgSettingsPage />)
    const card = await logoCard()

    fireEvent.change(within(card).getByLabelText('Logo seç'), {
      target: { files: [new File(['<svg/>'], 'logo.svg', { type: 'image/svg+xml' })] },
    })

    expect(within(card).getByRole('alert')).toHaveTextContent('logo.svg: bu dosya türü kabul edilmiyor.')
    expect(requestsTo('/Organizations/logo/upload')).toHaveLength(0)
  })

  it('shows why the API refused the file', async () => {
    storage()
    server.use(endpoint('put', '/Organizations/logo', () => errorResponse('OrganizationLogoInvalid', 'Logo bir PNG, JPEG ya da WebP resmi olmalı.')))
    const { user } = renderWithProviders(<OrgSettingsPage />)
    const card = await logoCard()

    await user.upload(within(card).getByLabelText('Logo seç'), new File([new Uint8Array(10)], 'logo.png', { type: 'image/png' }))

    expect(await within(card).findByRole('alert')).toHaveTextContent('Logo bir PNG, JPEG ya da WebP resmi olmalı.')
  })

  it('removes the logo after the confirmation', async () => {
    testState.organizations = [organization({ LogoUrl: LOGO_URL })]
    const { user } = renderWithProviders(<OrgSettingsPage />)
    const card = await logoCard()
    await waitFor(() => expect(card.querySelector('img')).toHaveAttribute('src', LOGO_URL))
    testState.organizations = [organization()]

    await user.click(within(card).getByRole('button', { name: 'Kaldır' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText('Logo kaldırılsın mı?')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Kaldır' }))

    expect(await screen.findByText('Logo kaldırıldı.')).toBeInTheDocument()
    expect(lastRequestTo('/Organizations/logo')?.method).toBe('DELETE')
    expect(lastRequestTo('/Organizations/logo')?.body).toEqual({ OrganizationId: TEST_ORG_ID })
    await waitFor(() => expect(card.querySelector('img')).toBeNull())
  })

  it('keeps the logo when the removal is cancelled', async () => {
    testState.organizations = [organization({ LogoUrl: LOGO_URL })]
    const { user } = renderWithProviders(<OrgSettingsPage />)
    const card = await logoCard()

    await user.click(await within(card).findByRole('button', { name: 'Kaldır' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Vazgeç' }))

    expect(requestsTo('/Organizations/logo')).toHaveLength(0)
  })

  it('shows an admin the logo read only', async () => {
    testState.organizations = [organization({ Role: OrgRole.Admin, LogoUrl: LOGO_URL })]
    renderWithProviders(<OrgSettingsPage />)
    const card = await logoCard()

    expect(within(card).getByText('Logoyu yalnız Sahip değiştirir.')).toBeInTheDocument()
    expect(within(card).queryByLabelText('Logo seç')).not.toBeInTheDocument()
    expect(within(card).queryByRole('button', { name: 'Kaldır' })).not.toBeInTheDocument()
  })
})
