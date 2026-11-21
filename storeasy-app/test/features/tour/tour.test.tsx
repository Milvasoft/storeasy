import { cleanup, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TourProvider, useTour } from '@/features/tour/TourProvider'
import { StorageKey, storageKey } from '@/lib/utils/storage'
import { accountDetail } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderApp, renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { requestsTo, testState } from '../../support/state'

const INVITATION = "storeasy'yi 2 dakikada tanıyalım mı?"
const SEEN_PATH = '/Account/tour-prompt-seen'

function newUser() {
  testState.account = accountDetail({ TourPromptSeenDate: null })
}

async function openTourFromInvitation() {
  newUser()
  const { user } = await renderApp({ route: '/apps' })
  await user.click(await screen.findByRole('button', { name: 'Turu başlat' }))
  await screen.findByRole('dialog', { name: 'Kısa bir tur' })
  return user
}

describe('tour invitation', () => {
  it('covers the screen on the first visit of a new user and does not start the tour by itself', async () => {
    newUser()

    await renderApp({ route: '/apps' })

    const invitation = await screen.findByRole('dialog', { name: INVITATION })
    expect(invitation).toHaveTextContent('Hesap sayfasındaki Tercihler bölümünden')
    expect(screen.getByRole('button', { name: 'Şimdi değil' })).toBeInTheDocument()
    expect(screen.queryByTestId('tour')).not.toBeInTheDocument()
  })

  it('does not show for a user who already answered it', async () => {
    await renderApp({ route: '/apps' })

    await screen.findByRole('heading', { name: 'Uygulamalar' })
    expect(screen.queryByRole('dialog', { name: INVITATION })).not.toBeInTheDocument()
  })

  it('closes with "not now", stores the answer on the account and stays closed on the next visit', async () => {
    newUser()
    const { user } = await renderApp({ route: '/apps' })

    await user.click(await screen.findByRole('button', { name: 'Şimdi değil' }))

    expect(screen.queryByRole('dialog', { name: INVITATION })).not.toBeInTheDocument()
    expect(screen.queryByTestId('tour')).not.toBeInTheDocument()
    await waitFor(() => expect(requestsTo(SEEN_PATH)).toHaveLength(1))
    // Another device: nothing in this browser, the account says it was answered.
    cleanup()
    localStorage.removeItem(storageKey(StorageKey.TourPromptSeen))
    await renderApp({ route: '/apps' })
    await screen.findByRole('heading', { name: 'Uygulamalar' })
    expect(screen.queryByRole('dialog', { name: INVITATION })).not.toBeInTheDocument()
  })

  it('starts the tour with "start" and does not come back afterwards', async () => {
    const user = await openTourFromInvitation()

    expect(screen.getByText(/^1 \/ \d+$/)).toBeInTheDocument()
    await waitFor(() => expect(requestsTo(SEEN_PATH)).toHaveLength(1))
    await user.click(screen.getByRole('button', { name: 'Turu bitir' }))
    expect(screen.queryByTestId('tour')).not.toBeInTheDocument()
    expect(screen.queryByRole('dialog', { name: INVITATION })).not.toBeInTheDocument()
  })

  it('is remembered in this browser when the account could not store the answer', async () => {
    newUser()
    server.use(endpoint('put', SEEN_PATH, () => errorResponse('ServerError', 'Kaydedilemedi.', 500)))
    const { user } = await renderApp({ route: '/apps' })
    await user.click(await screen.findByRole('button', { name: 'Şimdi değil' }))
    await waitFor(() => expect(requestsTo(SEEN_PATH)).toHaveLength(1))
    cleanup()

    await renderApp({ route: '/apps' })

    await screen.findByRole('heading', { name: 'Uygulamalar' })
    expect(testState.account.TourPromptSeenDate).toBeNull()
    expect(screen.queryByRole('dialog', { name: INVITATION })).not.toBeInTheDocument()
  })
})

describe('tour', () => {
  it('starts from the account page at any time', async () => {
    const { user } = await renderApp({ route: '/account' })

    await user.click(await screen.findByRole('button', { name: 'Turu başlat' }))

    expect(await screen.findByRole('dialog', { name: 'Kısa bir tur' })).toBeInTheDocument()
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/apps'))
    expect(requestsTo(SEEN_PATH)).toHaveLength(0)
  })

  it('moves with the buttons and the arrow keys', async () => {
    const user = await openTourFromInvitation()

    await user.click(screen.getByRole('button', { name: 'İleri' }))
    expect(await screen.findByRole('dialog', { name: 'Menü' })).toHaveTextContent(/2 \/ \d+/)
    await user.keyboard('{ArrowRight}')
    expect(await screen.findByRole('dialog', { name: 'Uygulamalar' })).toBeInTheDocument()
    await user.keyboard('{ArrowLeft}')
    expect(await screen.findByRole('dialog', { name: 'Menü' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Geri' }))
    expect(await screen.findByRole('dialog', { name: 'Kısa bir tur' })).toBeInTheDocument()
  })

  it('ends with Escape', async () => {
    const user = await openTourFromInvitation()

    await user.keyboard('{Escape}')

    expect(screen.queryByTestId('tour')).not.toBeInTheDocument()
  })

  it('opens the page of a step: the app of the organization for the app steps', async () => {
    const user = await openTourFromInvitation()

    for (let step = 0; step < 4; step += 1) {
      await user.click(await screen.findByRole('button', { name: 'İleri' }))
    }

    expect(await screen.findByRole('dialog', { name: 'Bir uygulama' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent(`/apps/${testState.apps[0].Id}`)
  })

  it('leaves the app steps out while the organization has no app', async () => {
    testState.apps = []
    const user = await openTourFromInvitation()

    for (let step = 0; step < 4; step += 1) {
      await user.click(await screen.findByRole('button', { name: 'İleri' }))
    }

    expect(await screen.findByRole('dialog', { name: 'Değişiklik planları ve onay' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/activity/plans')
  })
})

function StartButton() {
  const tour = useTour()
  return (
    <button type="button" onClick={tour?.start}>
      start
    </button>
  )
}

describe('tour steps', () => {
  it('skips a step whose element is not on the page', async () => {
    const { user } = renderWithProviders(
      <TourProvider steps={[{ id: 'welcome' }, { id: 'nav', targets: ['nowhere'] }, { id: 'done' }]} targetTimeoutMs={50}>
        <StartButton />
      </TourProvider>,
    )
    await user.click(await screen.findByRole('button', { name: 'start' }))
    await screen.findByRole('dialog', { name: 'Kısa bir tur' })

    await user.click(screen.getByRole('button', { name: 'İleri' }))

    const last = await screen.findByRole('dialog', { name: 'Hazırsın' })
    expect(last).toHaveTextContent('3 / 3')
    expect(screen.getByRole('button', { name: 'Bitir' })).toBeInTheDocument()
    expect(screen.queryByRole('dialog', { name: 'Menü' })).not.toBeInTheDocument()
  })

  it('shows the card in the middle when the element is hidden, and ends after the last step', async () => {
    const { user } = renderWithProviders(
      <TourProvider steps={[{ id: 'apps', targets: ['hidden-anchor'] }]} hiddenGraceMs={20}>
        <div data-tour="hidden-anchor" hidden />
        <StartButton />
      </TourProvider>,
    )
    await user.click(await screen.findByRole('button', { name: 'start' }))

    expect(await screen.findByRole('dialog', { name: 'Uygulamalar' })).toHaveClass('centered')
    await user.click(screen.getByRole('button', { name: 'Bitir' }))
    expect(screen.queryByTestId('tour')).not.toBeInTheDocument()
  })
})
