import { ConnectionStatus, StepDirection, StoreJobKind, StoreJobStatus, StoreJobStepStatus, StoreKind } from '@/lib/api/types'
import { ActivityEventType, createEmitter, type ActivityEvent, type ActivitySource, type ConnectionInfo } from './events'

// A scripted demo of the hub's real events (ActivityEventDto): an App Store Connect sync that runs through its four
// steps, a Google Play sync whose first attempt fails and waits for a retry, a verified connection. Used by `npm run dev`
// with the mock API, and in development when the hub cannot be reached. Never in production.

const ORG = 7

type Step = (now: string) => Array<Omit<ActivityEvent, 'OrganizationId' | 'Date'>>

const apple = { JobKind: StoreJobKind.SyncApps, Store: StoreKind.Apple, JobId: 48213, Attempt: 1 }
const google = { JobKind: StoreJobKind.SyncApps, Store: StoreKind.Google, JobId: 48214 }

function step(
  job: Partial<ActivityEvent>,
  order: number,
  name: string,
  direction: StepDirection,
  status: StoreJobStepStatus,
  extra: Partial<ActivityEvent> = {},
): Omit<ActivityEvent, 'OrganizationId' | 'Date'> {
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

const SCRIPT: Step[] = [
  () => [
    { ...apple, Type: ActivityEventType.JobQueued, JobStatus: StoreJobStatus.Queued },
    { ...google, Attempt: 1, Type: ActivityEventType.JobQueued, JobStatus: StoreJobStatus.Queued },
  ],
  () => [
    { ...apple, Type: ActivityEventType.JobProgress, JobStatus: StoreJobStatus.Running },
    step(apple, 1, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Running, { Message: "Apple connection 'Kuzey Lab ASC'" }),
  ],
  () => [
    step(apple, 1, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Succeeded, { Message: '5 apps.' }),
    { ...google, Attempt: 1, Type: ActivityEventType.JobProgress, JobStatus: StoreJobStatus.Running },
    step({ ...google, Attempt: 1 }, 1, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Running),
  ],
  () => [
    step(apple, 2, 'apps.save', StepDirection.Internal, StoreJobStepStatus.Succeeded, { Message: '5 records saved.' }),
    {
      ...google,
      Attempt: 1,
      Type: ActivityEventType.JobProgress,
      JobStatus: StoreJobStatus.Queued,
      Message: '503 Service Unavailable',
    },
  ],
  () => [step(apple, 3, 'builds.list', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 2, Total: 5 })],
  () => [step(apple, 3, 'builds.list', StepDirection.Outbound, StoreJobStepStatus.Running, { Progress: 4, Total: 5 })],
  () => [
    step(apple, 3, 'builds.list', StepDirection.Outbound, StoreJobStepStatus.Succeeded, { Progress: 5, Total: 5, Message: '42 builds.' }),
    { ...google, Attempt: 2, Type: ActivityEventType.JobProgress, JobStatus: StoreJobStatus.Running },
    step({ ...google, Attempt: 2 }, 3, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Running),
  ],
  () => [
    step(apple, 4, 'builds.save', StepDirection.Internal, StoreJobStepStatus.Succeeded, { Message: '3 new builds.' }),
    { ...apple, Type: ActivityEventType.JobSucceeded, JobStatus: StoreJobStatus.Succeeded, Message: '5 apps, 3 new builds.' },
    { Type: ActivityEventType.AppSynced, Store: StoreKind.Apple, AppId: 101 },
  ],
  () => [
    step({ ...google, Attempt: 2 }, 3, 'apps.list', StepDirection.Outbound, StoreJobStepStatus.Failed, {
      Message: '403 The caller does not have permission',
    }),
    {
      ...google,
      Attempt: 2,
      Type: ActivityEventType.JobFailed,
      JobStatus: StoreJobStatus.Failed,
      Message: '403 The caller does not have permission',
    },
  ],
  () => [
    { Type: ActivityEventType.ConnectionVerified, Store: StoreKind.Google, ConnectionId: 12, ConnectionStatus: ConnectionStatus.Verified },
  ],
]

/** Plays the script in a loop, one step every `intervalMs`; job ids move on every round so each loop is new work. */
export function createMockActivitySource({
  intervalMs = 2_500,
  demo = true,
}: { intervalMs?: number; demo?: boolean } = {}): ActivitySource {
  const events = createEmitter<ActivityEvent>()
  const connection = createEmitter<ConnectionInfo>()
  let timer: number | null = null
  let index = 0

  return {
    start: async () => {
      connection.emit({ state: 'connected', demo })
      timer = window.setInterval(() => {
        const now = new Date().toISOString()
        const round = Math.floor(index / SCRIPT.length)
        for (const event of SCRIPT[index % SCRIPT.length](now)) {
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
