import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AgeRatingPage, CategoriesPage, PricingPage, ReviewInfoPage } from '@/features/metadata/MetadataPages'
import { setupContentApi } from '../../support/content'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupContentApi()

function at(path: string, element: JSX.Element) {
  return renderWithProviders(element, { route: `/apps/101/${path}?store=apple`, path: `/apps/:appId/${path}` })
}

describe('CategoriesPage', () => {
  it('suggests categories with AI and plans them only after they are put in the form', async () => {
    const { user } = at('listing/category', <CategoriesPage />)
    await screen.findByLabelText(/İkincil kategori/)

    await user.click(screen.getByRole('button', { name: 'Kategori öner' }))

    const panel = await screen.findByRole('region', { name: 'Yapay zeka önerisi: kategoriler' })
    expect(lastRequestTo('/Categories/suggest')?.body).toMatchObject({ StoreAppId: 201, Instruction: null })
    expect(within(panel).getByText('Uygulama önce bir seyahat aracı.')).toBeInTheDocument()
    expect(within(panel).getByText('Travel')).toBeInTheDocument()
    expect(within(panel).getByText('(şu an Navigation)')).toBeInTheDocument()
    expect(requestsTo('/Categories').filter((request) => request.method === 'PUT')).toHaveLength(0)

    await user.click(within(panel).getByRole('button', { name: 'Forma al' }))
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/Categories')?.body).toMatchObject({ StoreAppId: 201, Primary: 'TRAVEL', Secondary: 'NAVIGATION', DryRun: true })
  })

  it('hides the category suggestion without an AI key', async () => {
    testState.aiSettings = { ...testState.aiSettings, HasApiKey: false }
    at('listing/category', <CategoriesPage />)
    await screen.findByLabelText(/İkincil kategori/)

    expect(screen.queryByRole('button', { name: 'Kategori öner' })).not.toBeInTheDocument()
  })

  it('refuses a secondary category equal to the primary one', async () => {
    const { user } = at('listing/category', <CategoriesPage />)
    const secondary = await screen.findByLabelText(/İkincil kategori/)

    await user.click(secondary)
    await user.click(await screen.findByRole('option', { name: 'Navigation' }))

    expect(screen.getByText('İkincil kategori birincille aynı olamaz.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Planla' })).toBeDisabled()
  })

  it('plans the changed copyright and rights, keeping the rest', async () => {
    const { user } = at('listing/category', <CategoriesPage />)
    const copyright = await screen.findByLabelText('Telif')

    await user.clear(copyright)
    await user.type(copyright, '2027 Kuzey Lab')
    await user.click(screen.getByRole('radio', { name: 'Üçüncü taraf içerik kullanıyor, hakları var' }))
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/Categories')?.body).toMatchObject({
      StoreAppId: 201,
      Primary: null,
      Secondary: null,
      Copyright: '2027 Kuzey Lab',
      ContentRightsDeclaration: 'USES_THIRD_PARTY_CONTENT',
      DryRun: true,
    })
  })
})

describe('AgeRatingPage', () => {
  it('shows the new descriptors as an assisted step and sends flags as booleans', async () => {
    const { user } = at('listing/age-rating', <AgeRatingPage />)

    expect(await screen.findByText('sharedLocation')).toBeInTheDocument()
    expect(screen.getByText('Yeni')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Asistanlı adım' })).toHaveAttribute('href', '/apps/101/assisted?store=apple')
    await user.click(screen.getByRole('switch', { name: 'gambling' }))
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/age-rating')?.body).toMatchObject({ StoreAppId: 201, Answers: { gambling: true }, DryRun: true })
  })
})

describe('PricingPage', () => {
  it('plans the price point of the base territory and the territory list', async () => {
    const { user } = at('listing/pricing', <PricingPage />)
    await screen.findByText('3 bölgeden 2 tanesi açık')

    await user.click(screen.getByRole('checkbox', { name: 'CHN' }))
    await user.click(screen.getByLabelText(/^Fiyat$/))
    await user.click(await screen.findByRole('option', { name: '49,99 TL' }))
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/Pricing/price-points')?.query).toMatchObject({ Territory: 'TUR' })
    expect(lastRequestTo('/Pricing')?.body).toMatchObject({
      BaseTerritory: 'TUR',
      PricePointId: 'pp-49',
      Territories: [
        { Code: 'TUR', Available: true },
        { Code: 'DEU', Available: true },
        { Code: 'CHN', Available: true },
      ],
    })
  })
})

describe('ReviewInfoPage', () => {
  it('never shows the demo password and sends it only when typed', async () => {
    const { user } = at('release/review-info', <ReviewInfoPage />)
    const password = await screen.findByLabelText('Şifre')

    expect(password).toHaveValue('')
    expect(screen.getByText(/Kayıtlı, şifreli\. Boş bırakırsan değişmez\./)).toBeInTheDocument()
    await user.type(screen.getByLabelText('İnceleme notları'), ' Teşekkürler.')
    await user.click(screen.getByRole('button', { name: 'Planla' }))
    await screen.findByRole('dialog')
    expect(lastRequestTo('/review-info')?.body).toMatchObject({
      VersionId: 'v-24',
      DemoPassword: null,
      Notes: 'Demo hesapla giriş yapın. Teşekkürler.',
    })
  })

  it('sends a typed password once and clears the field', async () => {
    const { user } = at('release/review-info', <ReviewInfoPage />)
    const password = await screen.findByLabelText('Şifre')

    await user.type(password, 'S3cret!pass')
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    const dialog = await screen.findByRole('dialog')
    expect(lastRequestTo('/review-info')?.body).toMatchObject({ DemoPassword: 'S3cret!pass', UseSavedAnswers: false })
    expect(within(dialog).queryByText('S3cret!pass')).not.toBeInTheDocument()
    expect(password).toHaveValue('')
  })

  it('hides the demo account fields when sign in is not required', async () => {
    const { user } = at('release/review-info', <ReviewInfoPage />)
    await screen.findByLabelText('Şifre')

    await user.click(screen.getByRole('switch', { name: 'Oturum açma gerekli' }))

    expect(screen.queryByLabelText('Şifre')).not.toBeInTheDocument()
  })
})
