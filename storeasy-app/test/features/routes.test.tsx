import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { setupContentApi } from '../support/content'
import { renderApp } from '../support/render'

setupContentApi()

describe('phase 2 routes', () => {
  it('opens an app section inside the shell with the app tabs and the store picker', async () => {
    await renderApp({ route: '/apps/101/listing/texts?store=apple' })

    expect(await screen.findByLabelText('Anahtar kelimeler')).toBeInTheDocument()
    const sections = screen.getByRole('navigation', { name: 'Uygulama bölümleri' })
    expect(sections).toHaveTextContent('Genel bakış')
    expect(screen.getByRole('link', { name: 'Mağaza kaydı', current: 'page' })).toHaveAttribute(
      'href',
      '/apps/101/listing/texts?store=apple',
    )
    expect(screen.getByRole('link', { name: 'Hazırlık' })).toHaveAttribute('href', '/apps/101/readiness?store=apple')
    expect(screen.getByRole('link', { name: /Google Play/ })).toHaveAttribute('href', '/apps/101/listing/texts?store=google')
  })

  it('serves the approval queue in place of its coming soon page', async () => {
    await renderApp({ route: '/activity/plans' })
    expect(await screen.findByRole('link', { name: 'de-DE anahtar kelimeler' })).toBeInTheDocument()
  })

  it('serves the rules catalog in place of its coming soon page', async () => {
    await renderApp({ route: '/rules' })
    expect(await screen.findByRole('link', { name: 'iPhone Duo ekran görüntüleri' })).toBeInTheDocument()
  })

  it('serves the organization compliance calendar', async () => {
    await renderApp({ route: '/compliance' })
    expect(await screen.findByRole('heading', { name: 'Uyum takvimi' })).toBeInTheDocument()
  })

  it('adds the phase 2 sections to the app detail tabs', async () => {
    await renderApp({ route: '/apps/101' })
    expect(await screen.findByRole('link', { name: 'Kayıtlı cevaplar' })).toHaveAttribute('href', '/apps/101/answers')
  })
})
