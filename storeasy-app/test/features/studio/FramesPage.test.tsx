import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StudioRoutes } from '@/features/studio/StudioRoutes'
import { renderApp, renderWithProviders } from '../../support/render'
import { lastRequestTo } from '../../support/state'
import { setupStudioApi } from '../../support/studio'

setupStudioApi()

describe('FramesPage', () => {
  it('shows each frame with its defaults and the catalog classes drawn with it', async () => {
    renderWithProviders(<StudioRoutes />, { route: '/studio/frames', path: '/studio/*' })

    const phone = await screen.findByRole('article', { name: 'Telefon' })
    expect(within(phone).getByText('13%')).toBeInTheDocument()
    expect(within(phone).getByText('island, notch, hole, none')).toBeInTheDocument()
    expect(within(phone).getByText('iPhone DI orta')).toBeInTheDocument()
    expect(within(phone).getByText('1080x1920')).toBeInTheDocument()
    expect(lastRequestTo('/Mockups/frames')?.query).toEqual({ OrganizationId: '7' })
  })

  it('filters the gallery by frame kind', async () => {
    const { user } = renderWithProviders(<StudioRoutes />, { route: '/studio/frames', path: '/studio/*' })
    await screen.findByRole('article', { name: 'Telefon' })

    await user.click(screen.getByRole('button', { name: 'Masaüstü' }))

    expect(screen.queryByRole('article', { name: 'Telefon' })).not.toBeInTheDocument()
    expect(screen.getByRole('article', { name: 'Masaüstü pencere' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Katlanır' }))
    expect(screen.getByText('Bu türde çerçeve yok.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Tablet' }))
    expect(screen.getByText('Bu türde çerçeve yok.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Telefon' }))
    expect(screen.getByRole('article', { name: 'Telefon' })).toBeInTheDocument()
  })
})

describe('studio route', () => {
  it('serves the studio in the shell instead of its coming soon page', async () => {
    await renderApp({ route: '/studio' })

    expect(await screen.findByRole('heading', { name: 'Stüdyo', level: 1 })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Mağaza seti 2026' })).toBeInTheDocument()
    expect(within(screen.getByRole('main')).queryByText('Yakında')).not.toBeInTheDocument()
    expect(within(screen.getByRole('link', { name: /Stüdyo/ })).queryByText('Yakında')).not.toBeInTheDocument()
  })
})
