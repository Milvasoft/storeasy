import { act, screen, waitFor, within } from '@testing-library/react'
import { HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import type { ComplianceItemDto } from '@/features/common/types'
import { RejectionKind, RejectionSource, RejectionState, type RejectionCaseListDto } from '@/features/rejections/types'
import { ActivityEventType, createManualSource, InboundEventType, type ActivityEvent } from '@/lib/activity/events'
import {
  BuildProcessingState,
  ConnectionStatus,
  StepDirection,
  StoreJobKind,
  StoreJobStatus,
  StoreJobStepStatus,
  StoreKind,
} from '@/lib/api/types'
import { ActivityPage } from '@/pages/ActivityPage'
import { activityEvent, app, job, TEST_USER_NAME } from '../support/factories'
import { endpoint, envelope, listEnvelope } from '../support/handlers'
import { renderWithProviders } from '../support/render'
import { server } from '../support/server'
import { reducedMotion } from '../support/setup'
import { testState } from '../support/state'

// What the live page reads beside the stream from the feature endpoints (empty unless a test fills them).
let openRejections: RejectionCaseListDto[] = []
let openCompliance: ComplianceItemDto[] = []
beforeEach(() => {
  openRejections = []
  openCompliance = []
  server.use(
    endpoint('patch', '/Rejections', () => HttpResponse.json(listEnvelope(openRejections))),
    endpoint('get', '/Compliance', () => HttpResponse.json(envelope(openCompliance))),
  )
})

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
    // The token: the app's name, at its step's place, opening the job.
    const line = screen.getByRole('list', { name: "storeasy'den App Store hattı" })
    const token = within(line).getByRole('link', {
      name: /^Pusula · Uygulama ve build senkronu, mağazaya gidiyor, 3\. adım: builds\.list, 2 \/ 5/,
    })
    expect(token).toHaveAttribute('href', '/activity/jobs/48213')
    expect(token).toHaveClass('tk', 'out')
    expect(token.parentElement).toHaveStyle({ transform: 'translateX(44.00%)' })
    expect(screen.getByText('1 iş hatta')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Etkin işler' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Pusula · Uygulama ve build senkronu' })).toHaveAttribute('href', '/activity/jobs/48213')
  })

  it('starts with the jobs that were already running when the page opened', async () => {
    testState.jobs = [job({ Id: 7, Status: StoreJobStatus.Running, Store: StoreKind.Google, AppId: null })]
    setup()

    expect(await screen.findByRole('heading', { name: 'Etkin işler' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Uygulama ve build senkronu' })).toHaveAttribute('href', '/activity/jobs/7')
  })

  it('keeps the same line without motion under reduced motion', async () => {
    reducedMotion.value = true
    testState.jobs = []
    const { push } = setup()
    await screen.findByText('Hareket azaltma açık: hat hareketsiz gösteriliyor, bilgiler aynı.')

    push(queued, listing)

    expect(screen.getByRole('group', { name: /Canlı hat/ })).toHaveClass('ln', 'calm')
    expect(screen.getByRole('link', { name: /mağazaya gidiyor, 3\. adım: builds\.list/ })).toBeInTheDocument()
  })

  it('stops a failed token in red with its reason and asks for attention when the job fails', async () => {
    testState.jobs = []
    const { push } = setup()
    await screen.findByText('Hat şu an sessiz')

    // Today: a failure asks for attention for a day.
    const Date = new globalThis.Date().toISOString()
    push(
      { ...queued, Date },
      { ...listing, Date, StepStatus: StoreJobStepStatus.Failed, Message: '429 Too Many Requests' },
      activityEvent({ ...sync, Date, Type: ActivityEventType.JobFailed, JobStatus: StoreJobStatus.Failed, Message: '429 Too Many Requests' }),
    )

    const token = screen.getByRole('link', { name: /durdu, 3\. adım: builds\.list.*429 Too Many Requests/ })
    expect(token).toHaveClass('failed')
    const attention = screen.getByRole('region', { name: 'Dikkat isteyenler' })
    expect(within(attention).getByText('App Store için son iş başarısız')).toBeInTheDocument()
    expect(within(attention).getByRole('link', { name: /İşi aç/ })).toHaveAttribute('href', '/activity/jobs/48213')
    const gate = screen.getByRole('region', { name: 'App Store' })
    expect(within(gate).getByText(/Son iş başarısız · hız sınırı/)).toBeInTheDocument()
  })

  it('rides a store event into storeasy as a pulse on its line', async () => {
    testState.jobs = []
    const { push } = setup()
    await screen.findByText('Hat şu an sessiz')

    push(
      activityEvent({ Type: InboundEventType.WebhookReceived, Store: StoreKind.Google, Message: 'SUBSCRIPTION_RENEWED' }),
      activityEvent({ Type: InboundEventType.WebhookReceived, Store: StoreKind.Google, Message: 'SUBSCRIPTION_RENEWED' }),
    )

    expect(screen.getByLabelText('Google Play kaynaklı Mağaza bildirimi: SUBSCRIPTION_RENEWED')).toBeInTheDocument()
    expect(screen.getByText('×2')).toBeInTheDocument()
    expect(screen.getAllByText('webhook.received')).toHaveLength(2)
  })

  it('fills the quiet line with what waits: reviews, expiries, deadlines and the gates, by app name', async () => {
    testState.jobs = []
    testState.apps = [app(), app({ Id: 103, Name: 'Mola' })]
    const now = Date.now()
    testState.board = {
      GeneratedDate: new Date(now).toISOString(),
      HoursStartDate: new Date(now - 23 * 3_600_000).toISOString(),
      Versions: [
        {
          AppId: 103,
          StoreAppId: 220,
          Store: StoreKind.Apple,
          VersionString: '1.4.0',
          BuildNumber: '400',
          State: 'IN_REVIEW',
          ChangedDate: new Date(now - 26 * 3_600_000 - 60_000).toISOString(),
        },
      ],
      Builds: [
        {
          AppId: 101,
          StoreAppId: 201,
          Store: StoreKind.Apple,
          Version: '2.2.4',
          BuildNumber: '398',
          ProcessingState: BuildProcessingState.Valid,
          ExpiryDate: new Date(now + 2.5 * 86_400_000).toISOString(),
        },
      ],
      Hours: [{ Store: StoreKind.Apple, Hour: new Date(now - 2 * 3_600_000).toISOString(), Succeeded: 4, Failed: 1, Inbound: 2 }],
    }
    setup()

    const gate = await screen.findByRole('region', { name: 'App Store' })
    expect(await within(gate).findByText('Mola')).toBeInTheDocument()
    expect(within(gate).getByText(/İncelemede · 1 g 2 sa oldu/)).toBeInTheDocument()
    const upcoming = screen.getByRole('region', { name: 'Sırada' })
    expect(within(upcoming).getByText('1 g 2 sa bekliyor')).toBeInTheDocument()
    expect(within(upcoming).getByText('2 gün')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /^Mola, 1\.4\.0 \(400\), App Review, 1 g 2 sa bekliyor/ })).toHaveAttribute(
      'href',
      '/apps/103/release/versions',
    )
    expect(screen.getByRole('img', { name: 'App Store son 24 saat: 4 iş başarılı, 1 başarısız, 2 mağaza bildirimi' })).toBeInTheDocument()
    const attention = screen.getByRole('region', { name: 'Dikkat isteyenler' })
    expect(within(attention).getByText("Pusula 2.2.4 (398) 2 gün içinde TestFlight'tan düşüyor")).toBeInTheDocument()
  })

  it('leaves a muted app out of what needs attention and says how many apps are muted', async () => {
    testState.jobs = []
    testState.connections = []
    testState.apps = [app(), app({ Id: 103, Name: 'Expenny', AlertsMutedDate: '2026-11-19T08:00:00Z' })]
    const now = Date.now()
    testState.board = {
      GeneratedDate: new Date(now).toISOString(),
      HoursStartDate: new Date(now - 23 * 3_600_000).toISOString(),
      Versions: [],
      Builds: [],
      Hours: [],
      MutedAppIds: [103],
    }
    const opened = { Store: StoreKind.Apple, Kind: RejectionKind.Rejected, State: RejectionState.Open, OpenedVia: RejectionSource.Webhook, OpenedDate: new Date(now - 3_600_000).toISOString(), ChecklistDone: 0, ChecklistTotal: 0 }
    openRejections = [
      { ...opened, Id: 5, AppId: 103, StoreAppId: 220, VersionString: '1.2' },
      { ...opened, Id: 6, AppId: 101, StoreAppId: 201, VersionString: '2.4' },
    ]
    setup()

    const attention = await screen.findByRole('region', { name: 'Dikkat isteyenler' })
    expect(await within(attention).findByText('Pusula 2.4 reddedildi')).toBeInTheDocument()
    expect(within(attention).getByRole('link')).toHaveAttribute('href', '/apps/101/rejections/6')
    expect(within(attention).queryByText(/Expenny/)).not.toBeInTheDocument()
    expect(within(attention).getByText('Susturulan uygulama dışarıda: 1')).toHaveAttribute('title', 'Expenny')
  })

  it('says nothing needs the user when all is well', async () => {
    testState.jobs = []
    testState.connections = []
    setup()

    expect(await screen.findByText('Şu an senden bekleyen bir şey yok.')).toBeInTheDocument()
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
