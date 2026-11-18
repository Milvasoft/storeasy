import { StoreKind } from '@/lib/api/types'
import {
  ActivityEventName,
  createEmitter,
  JobChannel,
  JobStatus,
  StepDirection,
  StepStatus,
  type ActivityEvent,
  type ActivitySource,
  type ConnectionInfo,
} from './events'

// A scripted demo of the design's sample day (design brief 5, design 11.1): Pusula's iPad screenshots going to App Store
// Connect, Halka's AAB to Google Play, a Microsoft Store package commit waiting for its retry, a webhook and a review.
// Used by `npm run dev` with the mock API, and in development when the hub cannot be reached. Never in production.

type Step = (now: string) => ActivityEvent[]

const MB = 1024 * 1024

function screenshotsStep(part: number): Step {
  return (now) => [
    {
      type: ActivityEventName.JobStep,
      data: {
        JobId: 48213,
        Order: 3 + part,
        Kind: 'asset.upload',
        Direction: StepDirection.Outbound,
        Status: StepStatus.Running,
        Bytes: part * 1.1 * MB,
        Total: 3.3 * MB,
        Message: `0${3 + (part % 3)}-takvim.png · ${part}/3`,
        Date: now,
      },
    },
    {
      type: ActivityEventName.JobStep,
      data: {
        JobId: 48213,
        Order: 4 + part,
        Kind: 'asset.commit',
        Direction: StepDirection.Inbound,
        Status: StepStatus.Succeeded,
        Message: '200 · md5',
        Date: now,
      },
    },
  ]
}

function bundleStep(percent: number): Step {
  return (now) => [
    {
      type: ActivityEventName.JobStep,
      data: {
        JobId: 48214,
        Order: 2,
        Kind: 'edits.bundles.upload',
        Direction: StepDirection.Outbound,
        Status: StepStatus.Running,
        Bytes: (48 * MB * percent) / 100,
        Total: 48 * MB,
        Message: 'internal · versionCode 42',
        Date: now,
      },
    },
  ]
}

const SCRIPT: Step[] = [
  (now) => [
    {
      type: ActivityEventName.JobCreated,
      data: {
        JobId: 48213,
        Kind: 'screenshots.apply',
        Title: 'iPad 13" · tr',
        AppName: 'Pusula',
        Store: StoreKind.Apple,
        Channel: JobChannel.Web,
        RequestedByUserName: 'ece@kuzeylab.dev',
        StepCount: 7,
        Date: now,
      },
    },
    {
      type: ActivityEventName.JobCreated,
      data: {
        JobId: 48214,
        Kind: 'bundles.upload',
        Title: 'AAB internal',
        AppName: 'Halka',
        Store: StoreKind.Google,
        Channel: JobChannel.Ci,
        StepCount: 3,
        Date: now,
      },
    },
    {
      type: ActivityEventName.JobCreated,
      data: {
        JobId: 48215,
        Kind: 'ms.flight.submit',
        Title: 'Kuzey içi uçuşu 2.4.0.0',
        AppName: 'Pusula',
        Store: StoreKind.Microsoft,
        Channel: JobChannel.Web,
        StepCount: 3,
        Date: now,
      },
    },
  ],
  screenshotsStep(1),
  bundleStep(40),
  (now) => [
    {
      type: ActivityEventName.JobStep,
      data: {
        JobId: 48215,
        Order: 2,
        Kind: 'package.commit',
        Direction: StepDirection.Outbound,
        Status: StepStatus.Retrying,
        Message: '503 Service Unavailable',
        Attempt: 2,
        MaxAttempt: 3,
        Date: now,
      },
    },
  ],
  (now) => [
    {
      type: ActivityEventName.ArtifactProgress,
      data: {
        SessionId: 's-1187',
        AppName: 'Halka 1.0.0 (42)',
        Source: 'GitHub Actions',
        FileName: 'halka-1.0.0-42.ipa',
        Bytes: 29.8 * MB,
        Total: 52.1 * MB,
        Date: now,
      },
    },
  ],
  screenshotsStep(2),
  (now) => [
    {
      type: ActivityEventName.WebhookReceived,
      data: {
        Store: StoreKind.Apple,
        EventType: 'APP_STORE_VERSION_APP_VERSION_STATE_UPDATED',
        AppName: 'Pusula 2.3',
        OldValue: 'WAITING_FOR_REVIEW',
        NewValue: 'IN_REVIEW',
        SignatureValid: true,
        Date: now,
      },
    },
  ],
  bundleStep(65),
  (now) => [
    {
      type: ActivityEventName.ReviewReceived,
      data: { Store: StoreKind.Google, AppName: 'Mola', Rating: 5, Locale: 'tr', Title: 'Çevrimdışı da çalışıyor', Date: now },
    },
  ],
  screenshotsStep(3),
  bundleStep(100),
  (now) => [
    {
      type: ActivityEventName.JobFinished,
      data: {
        JobId: 48214,
        Status: JobStatus.Succeeded,
        Title: 'AAB internal',
        AppName: 'Halka',
        Store: StoreKind.Google,
        DurationMs: 182_000,
        Date: now,
      },
    },
    {
      type: ActivityEventName.JobFinished,
      data: {
        JobId: 48213,
        Status: JobStatus.Succeeded,
        Title: 'iPad 13" · tr',
        AppName: 'Pusula',
        Store: StoreKind.Apple,
        DurationMs: 102_000,
        RequestedByUserName: 'ece@kuzeylab.dev',
        Date: now,
      },
    },
  ],
  (now) => [
    {
      type: ActivityEventName.JobFinished,
      data: {
        JobId: 48215,
        Status: JobStatus.Failed,
        Title: 'Kuzey içi uçuşu 2.4.0.0',
        AppName: 'Pusula',
        Store: StoreKind.Microsoft,
        DurationMs: 6_000,
        Message: '409 Conflict',
        Date: now,
      },
    },
  ],
]

/** Plays the script in a loop, one step every `intervalMs`. */
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
        for (const event of SCRIPT[index % SCRIPT.length](now)) {
          events.emit(event)
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
