import { act, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ActivityEventName, createManualSource, JobStatus, StepDirection, StepStatus, type ActivityEvent } from '@/lib/activity/events'
import { StoreKind } from '@/lib/api/types'
import { ActivityPage } from '@/pages/ActivityPage'
import { TEST_USER_NAME } from '../support/factories'
import { renderWithProviders } from '../support/render'
import { reducedMotion } from '../support/setup'

const DATE = '2026-11-18T14:02:41Z'

const created: ActivityEvent = {
  type: ActivityEventName.JobCreated,
  data: {
    JobId: 48213,
    Kind: 'screenshots.apply',
    Title: 'iPad 13" · tr',
    AppName: 'Pusula',
    Store: StoreKind.Apple,
    StepCount: 7,
    RequestedByUserName: TEST_USER_NAME,
    Date: DATE,
  },
}
const uploading: ActivityEvent = {
  type: ActivityEventName.JobStep,
  data: {
    JobId: 48213,
    Order: 4,
    Kind: 'asset.upload',
    Direction: StepDirection.Outbound,
    Status: StepStatus.Running,
    Message: '04-takvim.png · parça 2/3',
    Date: DATE,
  },
}

function setup(options: Parameters<typeof renderWithProviders>[1] = {}) {
  const source = createManualSource()
  const view = renderWithProviders(<ActivityPage />, { route: '/activity', activity: source, ...options })
  const push = (...events: ActivityEvent[]) =>
    act(() => {
      for (const event of events) {
        source.push(event)
      }
    })
  return { ...view, source, push }
}

describe('ActivityPage', () => {
  it('draws the quiet line before anything happens', async () => {
    setup()

    expect(await screen.findByText('Hat şu an sessiz')).toBeInTheDocument()
    expect(screen.getByText('Canlı')).toBeInTheDocument()
  })

  it('moves a packet on the store lane and lists the step as an outbound event', async () => {
    const { push } = setup()
    await screen.findByText('Hat şu an sessiz')

    push(created, uploading)

    expect(screen.queryByText('Hat şu an sessiz')).not.toBeInTheDocument()
    expect(screen.getAllByText('asset.upload').length).toBeGreaterThan(0)
    expect(screen.getAllByText('GİDEN').length).toBeGreaterThan(0)
    expect(document.querySelectorAll('animateMotion').length).toBeGreaterThan(0)
    expect(screen.getByRole('heading', { name: 'Etkin işler' })).toBeInTheDocument()
  })

  it('shows the status list and no animation under reduced motion', async () => {
    reducedMotion.value = true
    const { push } = setup()
    await screen.findByText('Hareket azaltma açık: hat durum listesi olarak gösteriliyor.')

    push(created, uploading)

    expect(screen.getByRole('list', { name: 'Hat durumu' })).toBeInTheDocument()
    expect(screen.getByText('Sürüyor')).toBeInTheDocument()
    expect(document.querySelectorAll('animateMotion')).toHaveLength(0)
  })

  it('marks a lane that waits for a retry', async () => {
    reducedMotion.value = true
    const { push } = setup()
    await screen.findByRole('list', { name: 'Hat durumu' })

    push(created, {
      type: ActivityEventName.JobStep,
      data: { ...uploading.data, Status: StepStatus.Retrying, Attempt: 2, MaxAttempt: 3 },
    } as ActivityEvent)

    expect(screen.getByText('Tekrar bekliyor')).toBeInTheDocument()
    expect(screen.getAllByText('deneme 2/3 · tekrar bekliyor').length).toBeGreaterThan(0)
  })

  it('freezes the screen while events keep arriving underneath', async () => {
    const { push, user } = setup()
    await screen.findByText('Hat şu an sessiz')

    await user.click(screen.getByRole('button', { name: 'Akışı dondur' }))
    push(created, uploading)

    expect(screen.getByText('Hat şu an sessiz')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Akışı sürdür' }))
    expect(screen.queryByText('Hat şu an sessiz')).not.toBeInTheDocument()
  })

  it('tells the person who asked for a job when it finishes', async () => {
    const { push } = setup()
    await screen.findByText('Hat şu an sessiz')

    push(created, {
      type: ActivityEventName.JobFinished,
      data: {
        JobId: 48213,
        Status: JobStatus.Failed,
        Title: 'iPad 13" · tr',
        AppName: 'Pusula',
        Store: StoreKind.Apple,
        Message: '502 Bad Gateway',
        RequestedByUserName: TEST_USER_NAME,
        Date: DATE,
      },
    })

    expect(await screen.findByText('İş başarısız: Pusula · iPad 13" · tr')).toBeInTheDocument()
  })

  it('filters lanes and events by store from the address', async () => {
    const { push } = setup({ route: '/activity?store=google' })
    await screen.findByText('Hat şu an sessiz')

    push(created, uploading)

    expect(screen.getByText('Hat şu an sessiz')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Google Play/, pressed: true })).toBeInTheDocument()
  })

  it('offers to reconnect when the live connection drops', async () => {
    const { source } = setup()
    await screen.findByText('Hat şu an sessiz')

    act(() => source.setConnection({ state: 'disconnected' }))

    expect(screen.getByText('Canlı bağlantı koptu. İşler sunucuda sürüyor.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tekrar bağlan' })).toBeInTheDocument()
  })

  it('counts the retry down while reconnecting', async () => {
    const { source } = setup()
    await screen.findByText('Hat şu an sessiz')

    act(() => source.setConnection({ state: 'reconnecting', attempt: 2, nextRetryAt: Date.now() + 10_000 }))

    expect(await screen.findByText(/deneme 2, 10 sn sonra/)).toBeInTheDocument()
  })
})
