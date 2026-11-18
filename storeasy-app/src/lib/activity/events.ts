import type { IsoDateTime, StoreKind } from '@/lib/api/types'

// The live activity contract (plan 2.3, K5; design 11.1). The hub is /hubs/activity, one group per organization, and
// these are its events. The hub is being built on the API: names and fields follow the design notes, enums are numeric
// like every other API enum, payload keys arrive camelCase and are pascalized. This file is the one place to adjust.

export const ActivityEventName = {
  JobCreated: 'job.created',
  JobStep: 'job.step',
  JobFinished: 'job.finished',
  WebhookReceived: 'webhook.received',
  ReviewReceived: 'review.received',
  // Planned (design 11.1 backend gap): a CI upload is an intake session, not a StoreJob.
  ArtifactProgress: 'artifact.progress',
} as const
export type ActivityEventName = (typeof ActivityEventName)[keyof typeof ActivityEventName]

export const JobStatus = { Queued: 1, Running: 2, Succeeded: 3, Failed: 4, Canceled: 5 } as const
export type JobStatus = (typeof JobStatus)[keyof typeof JobStatus]

export const StepDirection = { Outbound: 1, Inbound: 2 } as const
export type StepDirection = (typeof StepDirection)[keyof typeof StepDirection]

// Retrying: the step failed and waits for its next attempt (design 11.1 "Tekrar bekliyor").
export const StepStatus = { Running: 1, Succeeded: 2, Failed: 3, Retrying: 4 } as const
export type StepStatus = (typeof StepStatus)[keyof typeof StepStatus]

export const JobChannel = { Web: 1, Mcp: 2, Api: 3, Ci: 4 } as const
export type JobChannel = (typeof JobChannel)[keyof typeof JobChannel]

export interface JobCreatedEvent {
  JobId: number
  Kind: string
  Title: string
  AppName?: string | null
  Store: StoreKind
  Channel?: JobChannel | null
  RequestedByUserName?: string | null
  StepCount?: number | null
  Date: IsoDateTime
}

export interface JobStepEvent {
  JobId: number
  Order: number
  // "asset.upload", "edits.bundles.upload"...
  Kind: string
  Direction: StepDirection
  Status: StepStatus
  Bytes?: number | null
  Total?: number | null
  // File name and part ("04-takvim.png · parça 2/3"), or the store's answer.
  Message?: string | null
  Attempt?: number | null
  MaxAttempt?: number | null
  NextAttemptDate?: IsoDateTime | null
  Date: IsoDateTime
}

export interface JobFinishedEvent {
  JobId: number
  Status: JobStatus
  Title: string
  AppName?: string | null
  Store: StoreKind
  DurationMs?: number | null
  Message?: string | null
  RequestedByUserName?: string | null
  Date: IsoDateTime
}

export interface WebhookReceivedEvent {
  Store: StoreKind
  EventType: string
  AppName?: string | null
  OldValue?: string | null
  NewValue?: string | null
  SignatureValid: boolean
  Date: IsoDateTime
}

export interface ReviewReceivedEvent {
  Store: StoreKind
  AppName: string
  Rating: number
  Locale?: string | null
  Title?: string | null
  Date: IsoDateTime
}

export interface ArtifactProgressEvent {
  SessionId: string
  AppName: string
  // "GitHub Actions", "Azure DevOps", "EAS".
  Source: string
  FileName: string
  Bytes: number
  Total: number
  Date: IsoDateTime
}

export type ActivityEvent =
  | { type: typeof ActivityEventName.JobCreated; data: JobCreatedEvent }
  | { type: typeof ActivityEventName.JobStep; data: JobStepEvent }
  | { type: typeof ActivityEventName.JobFinished; data: JobFinishedEvent }
  | { type: typeof ActivityEventName.WebhookReceived; data: WebhookReceivedEvent }
  | { type: typeof ActivityEventName.ReviewReceived; data: ReviewReceivedEvent }
  | { type: typeof ActivityEventName.ArtifactProgress; data: ArtifactProgressEvent }

export const ACTIVITY_EVENT_NAMES: readonly ActivityEventName[] = Object.values(ActivityEventName)

// 'connecting' on start, 'reconnecting' between retries, 'disconnected' when the retries gave up (design 18.12).
export type ConnectionState = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export interface ConnectionInfo {
  state: ConnectionState
  // 1-based retry count while reconnecting.
  attempt?: number
  // When the next retry runs (ms since epoch), for the "next try in n s" text.
  nextRetryAt?: number | null
  // The stream is the in-browser demo, not the API (dev only).
  demo?: boolean
}

/**
 * Where live events come from. The app uses the SignalR source; tests push events through a manual source; dev with the
 * mock API plays a scripted demo. Every source has the same shape, so the reducer and the Hat never know which it is.
 */
export interface ActivitySource {
  start: () => Promise<void>
  stop: () => Promise<void>
  subscribe: (listener: (event: ActivityEvent) => void) => () => void
  onConnection: (listener: (info: ConnectionInfo) => void) => () => void
}

/** A small listener set, shared by every source. */
export function createEmitter<T>() {
  const listeners = new Set<(value: T) => void>()
  return {
    emit: (value: T) => {
      for (const listener of listeners) {
        listener(value)
      }
    },
    subscribe: (listener: (value: T) => void) => {
      listeners.add(listener)
      return () => {
        listeners.delete(listener)
      }
    },
  }
}

/** A source the caller drives by hand: tests, and anything that replays recorded events. */
export function createManualSource(
  initial: ConnectionInfo = { state: 'connected' },
): ActivitySource & { push: (event: ActivityEvent) => void; setConnection: (info: ConnectionInfo) => void } {
  const events = createEmitter<ActivityEvent>()
  const connection = createEmitter<ConnectionInfo>()
  return {
    start: async () => {
      connection.emit(initial)
    },
    stop: async () => undefined,
    subscribe: events.subscribe,
    onConnection: connection.subscribe,
    push: events.emit,
    setConnection: connection.emit,
  }
}
