import { ConnectionStatus, StepDirection, StoreJobKind, StoreJobStatus, StoreJobStepStatus, StoreKind } from '@/lib/api/types'
import { ActivityEventType, createEmitter, InboundEventType, type ActivityEvent, type ActivitySource, type ConnectionInfo } from './events'

// A scripted demo of the hub's real events (ActivityEventDto), used by `npm run dev` with the mock API and in
// development when the hub cannot be reached. Never in production. One round of the busy script: a CI upload of Pusula
// that is read, checked and delivered to App Store Connect with byte progress; an App Store Connect sync; a Google Play
// sync whose first attempt fails (503), retries and then fails for good (403); a Microsoft Store submission; App Review
// moving Pusula into review (webhook and version state); a burst of Play purchase notifications; new reviews; a
// connection check. The `error` scenario plays a rate limited upload and a rejection; `quiet` sends nothing but now and
// then a store event, so the calm board can be seen.

const ORG = 7

export type DemoScenario = 'busy' | 'quiet' | 'error'

type Event = Omit<ActivityEvent, 'OrganizationId' | 'Date'>
type Tick = () => Event[]

const appleSync = { JobKind: StoreJobKind.SyncApps, Store: StoreKind.Apple, JobId: 48401, Attempt: 1 }
const googleSync = { JobKind: StoreJobKind.SyncApps, Store: StoreKind.Google, JobId: 48402 }
const upload = { JobKind: StoreJobKind.ProcessArtifact, Store: StoreKind.Apple, JobId: 48403, AppId: 101, Attempt: 1 }
const msSubmit = { JobKind: StoreJobKind.MsSubmit, Store: StoreKind.Microsoft, JobId: 48404, AppId: 104, Attempt: 1 }
const MB = 1024 * 1024
const IPA = 84 * MB
const MSIX = 46 * MB

function step(
  job: Partial<ActivityEvent>,
  order: number,
  name: string,
  direction: StepDirection,
  status: StoreJobStepStatus,
  extra: Partial<ActivityEvent> = {},
): Event {
  return {
    ...job,
    Type: ActivityEventType.JobStep,
    JobStatus: StoreJobStatus.Running,
    StepOrder: order,
    StepName: name,
    Direction: direction,
    StepStatus: status,
    ...extra,
  }
}

const running = (job: Partial<ActivityEvent>): Event => ({ ...job, Type: ActivityEventType.JobProgress, JobStatus: StoreJobStatus.Running })
const queued = (job: Partial<ActivityEvent>): Event => ({ ...job, Type: ActivityEventType.JobQueued, JobStatus: StoreJobStatus.Queued })
const intake = (session: number, status: string, extra: Partial<ActivityEvent> = {}): Event => ({
  Type: InboundEventType.ArtifactProgress,
  AppId: 101,
  Store: StoreKind.Apple,
  Message: `Session ${session}: ${status}`,
  ...extra,
})
const webhook = (store: StoreKind, message: string): Event => ({ Type: InboundEventType.WebhookReceived, Store: store, Message: message })

const BUSY: Tick[] = [
  () => [intake(77, 'Uploading'), queued(appleSync)],
  () => [running(appleSync), step(appleSync, 1, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Running)],
  () => [
    intake(77, 'Parsing'),
    step(appleSync, 1, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Succeeded, { Message: '4 apps.' }),
    queued({ ...googleSync, Attempt: 1 }),
  ],
  () => [
    step(appleSync, 2, 'apps.save', StepDirection.Internal, StoreJobStepStatus.Succeeded, { Message: '4 records saved.' }),
    running({ ...googleSync, Attempt: 1 }),
    step({ ...googleSync, Attempt: 1 }, 1, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Running),
  ],
  () => [
    intake(77, 'Checking'),
    step(appleSync, 3, 'builds.list', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 1, Total: 4 }),
    queued(msSubmit),
  ],
  () => [
    step(appleSync, 3, 'builds.list', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 3, Total: 4 }),
    {
      ...googleSync,
      Attempt: 1,
      Type: ActivityEventType.JobProgress,
      JobStatus: StoreJobStatus.Queued,
      Message: '503 Service Unavailable',
    },
    running(msSubmit),
    step(msSubmit, 1, 'submission.create', StepDirection.Outbound, StoreJobStepStatus.Running),
  ],
  () => [
    intake(77, 'Delivering', { JobId: upload.JobId }),
    queued(upload),
    step(appleSync, 3, 'builds.list', StepDirection.Outbound, StoreJobStepStatus.Succeeded, {
      Progress: 4,
      Total: 4,
      Message: '11 builds.',
    }),
    step(msSubmit, 1, 'submission.create', StepDirection.Outbound, StoreJobStepStatus.Succeeded),
  ],
  () => [
    running(upload),
    step(upload, 1, 'build.upload', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 12 * MB, Total: IPA }),
    step(appleSync, 4, 'builds.save', StepDirection.Internal, StoreJobStepStatus.Succeeded, { Message: '2 new builds.' }),
    step(msSubmit, 2, 'package.upload', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 9 * MB, Total: MSIX }),
  ],
  () => [
    { ...appleSync, Type: ActivityEventType.JobSucceeded, JobStatus: StoreJobStatus.Succeeded, Message: '4 apps, 2 new builds.' },
    { Type: ActivityEventType.AppSynced, Store: StoreKind.Apple, AppId: 103 },
    step(upload, 1, 'build.upload', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 38 * MB, Total: IPA }),
    running({ ...googleSync, Attempt: 2 }),
    step({ ...googleSync, Attempt: 2 }, 2, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Running),
    step(msSubmit, 2, 'package.upload', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 27 * MB, Total: MSIX }),
  ],
  () => [
    webhook(StoreKind.Apple, 'WAITING_FOR_REVIEW -> IN_REVIEW'),
    { Type: InboundEventType.VersionState, AppId: 101, Message: 'Pusula 2.3.1: WAITING_FOR_REVIEW -> IN_REVIEW' },
    step(upload, 1, 'build.upload', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 61 * MB, Total: IPA }),
    step(msSubmit, 2, 'package.upload', StepDirection.Outbound, StoreJobStepStatus.Succeeded, { Progress: MSIX, Total: MSIX }),
  ],
  () => [
    step({ ...googleSync, Attempt: 2 }, 2, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Failed, {
      Message: '403 The caller does not have permission',
    }),
    step(upload, 1, 'build.upload', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 80 * MB, Total: IPA }),
    step(msSubmit, 3, 'submission.commit', StepDirection.Outbound, StoreJobStepStatus.Running),
  ],
  () => [
    {
      ...googleSync,
      Attempt: 2,
      Type: ActivityEventType.JobFailed,
      JobStatus: StoreJobStatus.Failed,
      Message: '403 The caller does not have permission',
    },
    step(upload, 1, 'build.upload', StepDirection.Outbound, StoreJobStepStatus.Succeeded, { Progress: IPA, Total: IPA }),
    webhook(StoreKind.Google, 'SUBSCRIPTION_RENEWED'),
    webhook(StoreKind.Google, 'SUBSCRIPTION_RENEWED'),
    webhook(StoreKind.Google, 'SUBSCRIPTION_PURCHASED'),
  ],
  () => [
    step(upload, 2, 'build.processing', StepDirection.Inbound, StoreJobStepStatus.Running),
    step(msSubmit, 3, 'submission.commit', StepDirection.Outbound, StoreJobStepStatus.Succeeded),
    { Type: InboundEventType.ReviewReceived, AppId: 102, Store: StoreKind.Google, Message: 'Halka: 2 new reviews.' },
  ],
  () => [
    step(msSubmit, 4, 'submission.status', StepDirection.Inbound, StoreJobStepStatus.Running, { Progress: 1, Total: 3 }),
    webhook(StoreKind.Apple, 'PROCESSING -> COMPLETE'),
  ],
  () => [
    step(upload, 2, 'build.processing', StepDirection.Inbound, StoreJobStepStatus.Succeeded, { Message: 'Build 2.3.2 (412) is valid.' }),
    step(msSubmit, 4, 'submission.status', StepDirection.Inbound, StoreJobStepStatus.Running, { Progress: 2, Total: 3 }),
  ],
  () => [
    step(upload, 3, 'build.save', StepDirection.Internal, StoreJobStepStatus.Succeeded),
    { ...upload, Type: ActivityEventType.JobSucceeded, JobStatus: StoreJobStatus.Succeeded, Message: 'Pusula 2.3.2 (412) delivered.' },
    intake(77, 'Delivered', { JobId: upload.JobId }),
    step(msSubmit, 4, 'submission.status', StepDirection.Inbound, StoreJobStepStatus.Succeeded, { Progress: 3, Total: 3 }),
  ],
  () => [
    { ...msSubmit, Type: ActivityEventType.JobSucceeded, JobStatus: StoreJobStatus.Succeeded, Message: 'Submission in certification.' },
    { Type: ActivityEventType.ConnectionVerified, Store: StoreKind.Google, ConnectionId: 12, ConnectionStatus: ConnectionStatus.Verified },
  ],
  () => [],
  () => [],
]

const RATE_LIMITED = '429 Too Many Requests: App Store Connect rate limit, retry later'

const ERROR: Tick[] = [
  () => [intake(91, 'Uploading', { AppId: 103 }), queued({ ...upload, AppId: 103 })],
  () => [
    running({ ...upload, AppId: 103 }),
    step({ ...upload, AppId: 103 }, 1, 'build.upload', StepDirection.Outbound, StoreJobStepStatus.Running, {
      Progress: 20 * MB,
      Total: IPA,
    }),
  ],
  () => [
    step({ ...upload, AppId: 103 }, 1, 'build.upload', StepDirection.Outbound, StoreJobStepStatus.Failed, {
      Progress: 41 * MB,
      Total: IPA,
      Message: RATE_LIMITED,
    }),
  ],
  () => [
    { ...upload, AppId: 103, Type: ActivityEventType.JobFailed, JobStatus: StoreJobStatus.Failed, Message: RATE_LIMITED },
    intake(91, 'Failed', { AppId: 103 }),
  ],
  () => [
    webhook(StoreKind.Apple, 'IN_REVIEW -> REJECTED'),
    { Type: InboundEventType.VersionState, AppId: 103, Message: 'Mola 1.4.0: IN_REVIEW -> REJECTED' },
    { Type: InboundEventType.RejectionOpened, AppId: 103, Message: 'Mola 1.4.0: REJECTED' },
  ],
  () => [],
  () => [],
  () => [],
  () => [],
  () => [],
]

const QUIET: Tick[] = [() => [], () => [], () => [], () => [webhook(StoreKind.Google, 'SUBSCRIPTION_RENEWED')], () => [], () => []]

const SCRIPTS: Record<DemoScenario, Tick[]> = { busy: BUSY, quiet: QUIET, error: ERROR }

/** The demo picked with ?demo=quiet|error (kept for the tab), busy by default. */
export function demoScenario(): DemoScenario {
  try {
    const asked = new URLSearchParams(window.location.search).get('demo')
    if (asked === 'busy' || asked === 'quiet' || asked === 'error') {
      window.sessionStorage.setItem('storeasy.demo', asked)
      return asked
    }
    const kept = window.sessionStorage.getItem('storeasy.demo')
    return kept === 'quiet' || kept === 'error' ? kept : 'busy'
  } catch {
    return 'busy'
  }
}

/** Plays the script in a loop, one tick every `intervalMs`; job ids move on every round so each loop is new work. */
export function createMockActivitySource({
  intervalMs = 1_600,
  demo = true,
  scenario = 'busy',
}: { intervalMs?: number; demo?: boolean; scenario?: DemoScenario } = {}): ActivitySource {
  const events = createEmitter<ActivityEvent>()
  const connection = createEmitter<ConnectionInfo>()
  const script = SCRIPTS[scenario]
  let timer: number | null = null
  let index = 0

  return {
    start: async () => {
      connection.emit({ state: 'connected', demo })
      timer = window.setInterval(() => {
        const now = new Date().toISOString()
        const round = Math.floor(index / script.length)
        for (const event of script[index % script.length]()) {
          events.emit({ ...event, JobId: event.JobId ? event.JobId + round * 10 : event.JobId, OrganizationId: ORG, Date: now })
        }
        index += 1
      }, intervalMs)
    },
    stop: async () => {
      if (timer !== null) {
        window.clearInterval(timer)
        timer = null
      }
    },
    subscribe: events.subscribe,
    onConnection: connection.subscribe,
  }
}
