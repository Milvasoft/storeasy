import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { RuleStatus } from '@/features/common/types'
import { RulesPage } from '@/features/rules/RulesPage'
import { contentState, setupContentApi } from '../../support/content'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo } from '../../support/state'

setupContentApi()

function render(route = '/rules') {
  return renderWithProviders(<RulesPage />, { route, path: route === '/rules' ? '/rules' : '/rules/:ruleId' })
}

describe('RulesPage', () => {
  it('lists the catalog rules with the version, dates and affected app counts', async () => {
    render()

    expect(await screen.findByRole('link', { name: 'iPhone Duo ekran görüntüleri' })).toHaveAttribute(
      'href',
      '/rules/apple.screenshots.duo',
    )
    expect(screen.getByText('katalog 1.3 · 18 Kas 2026 · 42 kural')).toBeInTheDocument()
    expect(screen.getByText('2 uygulama')).toBeInTheDocument()
    expect(screen.getByText('Yaklaşan')).toBeInTheDocument()
  })

  it('filters by store and status on the server', async () => {
    const { user } = render()
    await screen.findByRole('link', { name: 'iPhone Duo ekran görüntüleri' })

    await user.click(within(screen.getByRole('group', { name: 'Mağaza' })).getByRole('button', { name: /Google Play/ }))
    await user.click(screen.getByLabelText('Durum'))
    await user.click(await screen.findByRole('option', { name: 'Yürürlükte' }))

    await screen.findByRole('link', { name: 'iPhone Duo ekran görüntüleri' })
    expect(lastRequestTo('/Rules')?.body).toMatchObject({ Store: StoreKind.Google, Status: RuleStatus.InForce })
  })

  it('searches after typing stops', async () => {
    const { user } = render()
    await screen.findByRole('link', { name: 'iPhone Duo ekran görüntüleri' })

    await user.type(screen.getByLabelText('Kural bul'), 'watch')
    contentState.rules = []

    expect(await screen.findByText('"watch" için kural yok', undefined, { timeout: 3000 })).toBeInTheDocument()
    expect(lastRequestTo('/Rules')?.body).toMatchObject({ Search: 'watch' })
  })

  it('opens a rule with its sources, the conflict and the apps of this organization', async () => {
    render('/rules/apple.screenshots.duo')

    const drawer = await screen.findByRole('dialog', { name: 'iPhone Duo ekran görüntüleri' })
    expect(await within(drawer).findByText('Sürüm notu bütün gönderimler diyor.')).toBeInTheDocument()
    expect(within(drawer).getByText('Katalog şunu tutuyor: SDK koşulu')).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'Screenshot specifications' })).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'Pusula' })).toHaveAttribute('href', '/apps/101/compliance')
    expect(within(drawer).getByText('bilinmiyor: build.sdk')).toBeInTheDocument()
    expect(within(drawer).getByText('1398x2034')).toBeInTheDocument()
    expect(lastRequestTo('/Rules/rule')?.query).toMatchObject({ RuleId: 'apple.screenshots.duo' })
  })
})
