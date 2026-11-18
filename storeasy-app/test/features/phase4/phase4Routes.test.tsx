import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UserType } from '@/lib/api/types'
import { setupContentApi } from '../../support/content'
import { setupPhase4Api } from '../../support/phase4'
import { renderApp } from '../../support/render'

setupContentApi()
setupPhase4Api()

describe('phase 4 routes', () => {
  it('serves the review inbox in place of its coming soon page', async () => {
    await renderApp({ route: '/reviews' })
    expect(await screen.findByRole('button', { name: /Ayşe K\./ }, { timeout: 5000 })).toBeInTheDocument()
  })

  it('keeps the coming soon page while the Reviews flag is off', async () => {
    await renderApp({ route: '/reviews', flags: { Reviews: false } })
    expect(await screen.findByText('Bu bölüm API hazır olunca açılır. Burada örnek veri yok.', {}, { timeout: 5000 })).toBeInTheDocument()
  })

  it('serves the webhooks screen to an owner', async () => {
    await renderApp({ route: '/webhooks' })
    expect(await screen.findByText('Kuzey Lab ASC')).toBeInTheDocument()
  })

  it('adds reviews, rejections and TestFlight to the app sections', async () => {
    await renderApp({ route: '/apps/101/release/testflight' })
    expect(await screen.findByRole('link', { name: '1.0 (37)' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Red kayıtları' })).toHaveAttribute('href', '/apps/101/rejections?store=apple')
    expect(screen.getByRole('link', { name: 'TestFlight', current: 'page' })).toBeInTheDocument()
  })

  it('serves the notification center and the preferences', async () => {
    await renderApp({ route: '/account/notifications' })
    expect(await screen.findByRole('switch', { name: 'E-posta: Red' })).toBeInTheDocument()
  })

  it('serves the rule watcher to a platform administrator', async () => {
    await renderApp({ route: '/admin/rules', userType: UserType.Manager })
    expect(await screen.findByRole('link', { name: '#14 Duo koşulu netleşti' })).toBeInTheDocument()
  })
})
