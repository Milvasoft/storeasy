import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { ListingDetailsPage, ListingsPage } from '@/features/listings/ListingsPage'
import { contentState, listing, setupContentApi } from '../../support/content'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupContentApi()

function render(query = '?store=apple') {
  return renderWithProviders(<ListingsPage />, { route: `/apps/101/listing/texts${query}`, path: '/apps/:appId/listing/texts' })
}

describe('ListingsPage', () => {
  it('suggests a text with AI, lets it be edited and puts it in the draft for a plan', async () => {
    const { user } = render()
    await screen.findByLabelText('Ad')

    await user.click(screen.getByRole('button', { name: 'Ad için yapay zekadan öneri al' }))

    const panel = await screen.findByRole('region', { name: 'Yapay zeka önerisi: Ad' })
    expect(lastRequestTo('/Listings/suggest')?.body).toMatchObject({ StoreAppId: 201, VersionId: 'v-24', Locale: 'tr', Field: 'name', Instruction: null })
    expect(within(panel).getByText('Ana faydayı adın yanına ekler.')).toBeInTheDocument()
    expect(requestsTo('/Listings/plan')).toHaveLength(0)
    const suggested = within(panel).getByRole('textbox', { name: 'Ad' })
    await user.clear(suggested)
    await user.type(suggested, 'Pusula Rota')
    await user.click(within(panel).getByRole('button', { name: 'Taslağa al' }))

    expect(screen.queryByRole('region', { name: 'Yapay zeka önerisi: Ad' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Ad')).toHaveValue('Pusula Rota')
    expect(screen.getByText('Değişti')).toBeInTheDocument()
    expect(requestsTo('/Listings/plan')).toHaveLength(0)
    expect(screen.queryByRole('button', { name: /URL'si için yapay zekadan/ })).not.toBeInTheDocument()
  })

  it('hides the suggestions without an AI key', async () => {
    testState.aiSettings = { ...testState.aiSettings, HasApiKey: false }
    render()
    await screen.findByLabelText('Ad')

    expect(screen.queryByRole('button', { name: /yapay zekadan öneri al/ })).not.toBeInTheDocument()
  })

  it('shows the fields of the primary language with the catalog limits counted', async () => {
    render()

    const keywords = await screen.findByLabelText('Anahtar kelimeler')
    expect(keywords).toHaveValue('harita,rota')
    expect(screen.getByText(/11 \/ 100 karakter · 11 \/ 100 bayt/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /tr/, pressed: true })).toBeInTheDocument()
  })

  it('counts keywords in bytes and blocks the plan when Turkish letters push them over 100', async () => {
    const { user } = render()
    const keywords = await screen.findByLabelText('Anahtar kelimeler')

    await user.clear(keywords)
    await user.type(keywords, 'ğ'.repeat(60))

    expect(screen.getByText(/Anahtar kelimeler sınırı aşıyor\. 60 \/ 100 karakter · 120 \/ 100 bayt/)).toBeInTheDocument()
    expect(screen.getByText('Plan için sınırı aşan alanları düzelt.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Planla' })).toBeDisabled()
  })

  it('makes a plan from the draft and shows its diff, then approves it', async () => {
    const { user } = render()
    const name = await screen.findByLabelText('Ad')

    await user.clear(name)
    await user.type(name, 'Pusula Harita')
    expect(screen.getByText('Değişti')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    const dialog = await screen.findByRole('dialog', { name: 'de-DE anahtar kelimeler' })
    expect(lastRequestTo('/Listings/plan')?.body).toMatchObject({
      StoreAppId: 201,
      VersionId: 'v-24',
      DryRun: true,
      Changes: [{ Locale: 'tr', Field: 'name', Value: 'Pusula Harita' }],
    })
    expect(within(dialog).getByText('karte,wandern')).toBeInTheDocument()
    expect(within(dialog).getByText('karte,gps')).toBeInTheDocument()
    expect(within(dialog).getByText(/en-US anahtar kelimelerinde de var/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Onayla ve gönder' }))

    expect(await screen.findByText('Plan onaylandı; mağaza işi kuyruğa girdi.')).toBeInTheDocument()
    expect(lastRequestTo('/plans/approve')?.body).toMatchObject({ PlanId: 7001, Confirm: null })
  })

  it('undoes one field of the draft', async () => {
    const { user } = render()
    const name = await screen.findByLabelText('Ad')

    await user.type(name, 'X')
    await user.click(screen.getByRole('button', { name: 'Geri al: Ad' }))

    expect(name).toHaveValue('Pusula')
    expect(screen.queryByRole('button', { name: 'Planla' })).not.toBeInTheDocument()
  })

  it('loads another language when it is picked', async () => {
    const { user } = render()
    await screen.findByLabelText('Ad')

    await user.click(screen.getByRole('button', { name: /de-DE/ }))

    await screen.findByLabelText('Ad')
    expect(lastRequestTo('/Listings')?.query).toMatchObject({ Locale: 'de-DE' })
  })

  it('locks the version texts of a version in review but keeps the promotional text open', async () => {
    contentState.listing = listing({
      VersionEditable: false,
      VersionState: 'IN_REVIEW',
      Fields: [
        { Key: 'description', Group: 'Version', Locale: 'tr', Value: 'x', MaxLength: 4000, Required: true },
        { Key: 'promotionalText', Group: 'Version', Locale: 'tr', Value: 'y', MaxLength: 170, Required: false },
      ],
    })

    render()

    expect(await screen.findByLabelText('Açıklama')).toBeDisabled()
    expect(screen.getByLabelText('Tanıtım metni')).toBeEnabled()
    expect(screen.getByText(/metinleri kilitli/)).toBeInTheDocument()
  })

  it('is read only for a Viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    render()

    expect(await screen.findByLabelText('Ad')).toBeDisabled()
  })
})

describe('ListingDetailsPage', () => {
  it('plans the changed Play contact details without a language', async () => {
    const { user } = renderWithProviders(<ListingDetailsPage />, {
      route: '/apps/101/listing/details?store=google',
      path: '/apps/:appId/listing/details',
    })
    const phone = await screen.findByLabelText('İletişim telefonu')

    await user.type(phone, '+905550102030')
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/Listings/plan')?.body).toMatchObject({
      StoreAppId: 202,
      VersionId: null,
      Changes: [{ Locale: null, Field: 'contactPhone', Value: '+905550102030' }],
    })
  })

  it('is not offered for the App Store', async () => {
    renderWithProviders(<ListingDetailsPage />, { route: '/apps/101/listing/details?store=apple', path: '/apps/:appId/listing/details' })

    expect(await screen.findByText('Bu bölüm App Store için yok.')).toBeInTheDocument()
  })
})
