import { act, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ActivityEventType, createManualSource, type ActivityEvent } from '@/lib/activity/events'
import { ConnectionStatus, StepDirection, StoreJobKind, StoreJobStatus, StoreJobStepStatus, StoreKind } from '@/lib/api/types'
import { ActivityPage } from '@/pages/ActivityPage'
import { activityEvent, app, job, TEST_USER_NAME } from '../support/factories'
import { renderWithProviders } from '../support/render'
import { reducedMotion } from '../support/setup'
import { testState } from '../support/state'

const sync = { JobId: 48213, JobKind: StoreJobKind.SyncApps, Store: StoreKind.Apple, AppId: 101, Attempt: 1 }
const queued = activityEvent({ ...sync, Type: ActivityEventType.JobQueued, JobStatus: StoreJobStatus.Queued })
const listing = activityEvent({
  ...sync,
  Type: ActivityEventType.JobStep,
  JobStatus: StoreJobStatus.Running,
  StepOrder: 3,
  StepName: 'builds.list',
  Direction: StepDirection.Outbound,
  StepStatus: StoreJobStepStatus.Running,
  Progress: 2,
  Total: 5,
})

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
    testState.jobs = []
    setup()

    expect(await screen.findByText('Hat şu an sessiz')).toBeInTheDocument()
    expect(screen.getByText('Canlı')).toBeInTheDocument()
    expect(screen.getByText('İlk iş bir senkron başlayınca burada görünür.')).toBeInTheDocument()
  })

  it('moves a packet on the store lane and lists the step as an outbound event with the app name', async () => {
    testState.jobs = []
    testState.apps = [app()]
    const { push } = setup()
    await screen.findByText('Hat şu an sessiz')

    push(queued, listing)

    expect(screen.queryByText('Hat şu an sessiz')).not.toBeInTheDocument()
    expect(screen.getAllByText('builds.list').length).toBeGreaterThan(0)
    expect(screen.getAllByText('GİDEN').length).toBeGreaterThan(0)
    expect(document.querySelectorAll('animateMotion').length).toBeGreaterThan(0)
    expect(screen.getByRole('heading', { name: 'Etkin işler' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Pusula · Uygulama ve build senkronu' })).toHaveAttribute('href', '/activity/jobs/48213')
  })

  it('starts with the jobs that were already running when the page opened', async () => {
    testState.jobs = [job({ Id: 7, Status: StoreJobStatus.Running, Store: StoreKind.Google, AppId: null })]
    setup()

    expect(await screen.findByRole('heading', { name: 'Etkin işler' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Uygulama ve build senkronu' })).toHaveAttribute('href', '/activity/jobs/7')
  })

  it('shows the status list and no animation under reduced motion', async () => {
    reducedMotion.value = true
    testState.jobs = []
    const { push } = setup()
    await screen.findByText('Hareket azaltma açık: hat durum listesi olarak gösteriliyor.')

    push(queued, listing)

    expect(screen.getByRole('list', { name: 'Hat durumu' })).toBeInTheDocument()
    expect(screen.getAllByText('Sürüyor').length).toBeGreaterThan(0)
    expect(document.querySelectorAll('animateMotion')).toHaveLength(0)
  })

  it('marks a lane whose attempt failed and waits for a retry', async () => {
    reducedMotion.value = true
    testState.jobs = [job({ Id: 48213, Status: StoreJobStatus.Running, MaxAttempts: 3 })]
    const { push } = setup()
    await screen.findByRole('heading', { name: 'Etkin işler' })

    push(
      listing,
      activityEvent({ ...sync, Type: ActivityEventType.JobProgress, JobStatus: StoreJobStatus.Queued, Message: '503 Service Unavailable' }),
    )

    expect(screen.getAllByText('Tekrar bekliyor').length).toBeGreaterThan(0)
    expect(screen.getAllByText('deneme 1/3 · tekrar bekliyor').length).toBeGreaterThan(0)
  })

  it('freezes the screen while events keep arriving underneath', async () => {
    testState.jobs = []
    const { push, user } = setup()
    await screen.findByText('Hat şu an sessiz')

    await user.click(screen.getByRole('button', { name: 'Akışı dondur' }))
    push(queued, listing)

    expect(screen.getByText('Hat şu an sessiz')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Akışı sürdür' }))
    expect(screen.queryByText('Hat şu an sessiz')).not.toBeInTheDocument()
  })

  it('tells the person who asked for a job when it fails', async () => {
    testState.jobs = [job({ Id: 48213, Status: StoreJobStatus.Running, RequestedByUserName: TEST_USER_NAME, AppId: 101 })]
    testState.apps = [app()]
    const { push } = setup()
    await screen.findByRole('link', { name: 'Pusula · Uygulama ve build senkronu' })

    push(activityEvent({ ...sync, Type: ActivityEventType.JobFailed, JobStatus: StoreJobStatus.Failed, Message: '403 Forbidden' }))

    expect(await screen.findByText('İş başarısız: Pusula · Uygulama ve build senkronu')).toBeInTheDocument()
  })

  it('stays quiet about a job someone else asked for', async () => {
    testState.jobs = [job({ Id: 48213, Status: StoreJobStatus.Running, RequestedByUserName: 'ece@kuzeylab.dev' })]
    const { push } = setup()
    await screen.findByRole('heading', { name: 'Etkin işler' })

    push(activityEvent({ ...sync, Type: ActivityEventType.JobSucceeded, JobStatus: StoreJobStatus.Succeeded }))

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Son biten işler' })).toBeInTheDocument())
    expect(screen.queryByText(/İş bitti/)).not.toBeInTheDocument()
  })

  it('names the result of a connection check in the feed', async () => {
    testState.jobs = []
    const { push } = setup()
    await screen.findByText('Hat şu an sessiz')

    push(
      activityEvent({
        Type: ActivityEventType.ConnectionVerified,
        Store: StoreKind.Google,
        ConnectionId: 12,
        ConnectionStatus: ConnectionStatus.Verified,
      }),
    )

    expect(screen.getByText('connection.verified')).toBeInTheDocument()
    expect(screen.getByText('Bağlantı kontrol edildi: Doğrulandı')).toBeInTheDocument()
  })

  it('filters lanes and events by store from the address', async () => {
    testState.jobs = []
    const { push } = setup({ route: '/activity?store=google' })
    await screen.findByText('Hat şu an sessiz')

    push(queued, listing)

    expect(screen.getByText('Hat şu an sessiz')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Google Play/, pressed: true })).toBeInTheDocument()
  })

  it('offers to reconnect when the live connection drops', async () => {
    testState.jobs = []
    const { source } = setup()
    await screen.findByText('Hat şu an sessiz')

    act(() => source.setConnection({ state: 'disconnected' }))

    expect(screen.getByText('Canlı bağlantı koptu. İşler sunucuda sürüyor.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tekrar bağlan' })).toBeInTheDocument()
  })

  it('says so without a reconnect when the hub refuses the organization', async () => {
    testState.jobs = []
    const { source } = setup()
    await screen.findByText('Hat şu an sessiz')

    act(() => source.setConnection({ state: 'disconnected', refused: true }))

    expect(screen.getByText('Bu organizasyonun üyesi olmadığın için canlı akışı kapalı.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Tekrar bağlan' })).not.toBeInTheDocument()
  })

  it('counts the retry down while reconnecting', async () => {
    testState.jobs = []
    const { source } = setup()
    await screen.findByText('Hat şu an sessiz')

    act(() => source.setConnection({ state: 'reconnecting', attempt: 2, nextRetryAt: Date.now() + 10_000 }))

    expect(await screen.findByText(/deneme 2, 10 sn sonra/)).toBeInTheDocument()
  })
})
