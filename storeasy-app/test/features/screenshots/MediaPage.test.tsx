import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { PreviewsPage, ScreenshotsPage } from '@/features/screenshots/MediaPage'
import { setupContentApi } from '../../support/content'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupContentApi()

function render() {
  return renderWithProviders(<ScreenshotsPage />, {
    route: '/apps/101/listing/screenshots?store=apple',
    path: '/apps/:appId/listing/screenshots',
  })
}

function classCard(title: string): HTMLElement {
  return screen.getByText(title, { exact: false, selector: 'b' }).closest('li') as HTMLElement
}

describe('ScreenshotsPage', () => {
  it('shows each class with its catalog state: filled, covered by a fallback, missing', async () => {
    render()

    await screen.findByText('01-harita.png')
    expect(within(classCard('iPhone 6,9"')).getByText('Dolu')).toBeInTheDocument()
    expect(within(classCard('iPhone 6,5"')).getByText('iphone-69 sınıfıyla karşılanıyor')).toBeInTheDocument()
    expect(within(classCard('iPad 13"')).getByText('Zorunlu, eksik')).toBeInTheDocument()
    expect(within(classCard('iPhone 6,9"')).getByText('2 / 10')).toBeInTheDocument()
    expect(lastRequestTo('/Screenshots/sets')?.query).toMatchObject({ StoreAppId: '201', Locale: 'tr' })
  })

  it('plans a new order and a removal of one class', async () => {
    const { user } = render()
    await screen.findByText('01-harita.png')
    const card = classCard('iPhone 6,9"')

    await user.click(within(card).getByRole('button', { name: 'Düzenle' }))
    await user.click(within(card).getByRole('button', { name: '02-rota.png yukarı taşı' }))
    await user.click(within(card).getByRole('button', { name: '01-harita.png kaldır' }))
    expect(within(card).getByText('Bu sınıfta 2 değişiklik')).toBeInTheDocument()
    await user.click(within(card).getByRole('button', { name: 'Bu sınıfı planla' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/Screenshots/plan')?.body).toMatchObject({
      StoreAppId: 201,
      Locale: 'tr',
      ClassKey: 'iphone-69',
      Order: ['s2'],
      Deletes: ['s1'],
      AssetIds: [],
      DryRun: true,
    })
  })

  it('offers no editing to a Viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    render()

    await screen.findByText('01-harita.png')
    expect(screen.queryByRole('button', { name: 'Düzenle' })).not.toBeInTheDocument()
  })
})

describe('PreviewsPage', () => {
  it('reads the preview sets', async () => {
    renderWithProviders(<PreviewsPage />, { route: '/apps/101/listing/previews?store=apple', path: '/apps/:appId/listing/previews' })

    expect(await screen.findByRole('heading', { name: 'Önizleme videoları' })).toBeInTheDocument()
    await screen.findByText('01-harita.png')
    expect(lastRequestTo('/Previews/sets')?.query).toMatchObject({ StoreAppId: '201' })
  })
})
