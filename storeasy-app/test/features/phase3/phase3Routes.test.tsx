import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { setupPhase3Api } from '../../support/phase3'
import { renderApp } from '../../support/render'

setupPhase3Api()

describe('phase 3 routes', () => {
  it('serves package intake in place of its coming soon page', async () => {
    await renderApp({ route: '/intake/sessions' })
    expect(await screen.findByText('pusula-dev-1.0.0-41.aab', {}, { timeout: 5000 })).toBeInTheDocument()
  })

  it('keeps the coming soon page while the Intake flag is off', async () => {
    await renderApp({ route: '/intake', flags: { Intake: false } })
    expect(await screen.findByRole('heading', { name: 'Henüz paket gelmedi' }, { timeout: 5000 })).toBeInTheDocument()
  })

  it('serves the tokens and the MCP setup under the developer section', async () => {
    await renderApp({ route: '/developer/setup' })
    expect(await screen.findByRole('heading', { name: 'Onay kuralları' }, { timeout: 5000 })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Token'lar/ })).toHaveAttribute('href', '/developer')
  })
})
