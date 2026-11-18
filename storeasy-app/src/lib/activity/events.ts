import type {
  ConnectionStatus,
  IsoDateTime,
  StepDirection,
  StoreJobKind,
  StoreJobStatus,
  StoreJobStepStatus,
  StoreKind,
} from '@/lib/api/types'

// The live activity contract, as the API sends it (Storeasy.Api/Hubs/ActivityHub.cs, ActivityEventDto, ActivityEventTypes).
// The hub is /hubs/activity: after connecting, the client calls JoinOrganization(organizationId) and the server checks the
// membership (false joins nothing). Every event arrives on the one client method "activity" with its kind in Type. The
// hub uses SignalR's default JSON protocol: camelCase keys (pascalized here) and enums as numbers, like the REST API.

export const ACTIVITY_CLIENT_METHOD = 'activity'
export const JOIN_ORGANIZATION_METHOD = 'JoinOrganization'
export const LEAVE_ORGANIZATION_METHOD = 'LeaveOrganization'

export const ActivityEventType = {
  JobQueued: 'job.queued',
  // Changed state without finishing: started, or a retry was scheduled (JobStatus Queued, Message the reason).
  JobProgress: 'job.progress',
  JobStep: 'job.step',
  JobSucceeded: 'job.succeeded',
  JobFailed: 'job.failed',
  JobCancelled: 'job.cancelled',
  ConnectionVerified: 'connection.verified',
  AppSynced: 'app.synced',
  // Phase 4: records the screens refresh on (rejection cases, reviews) and the user's own notifications.
  RejectionOpened: 'rejection.opened',
  ReviewReceived: 'review.received',
  // Sent to the receiving user's own group, whatever organization is open (OrganizationId 0 for none).
  NotificationCreated: 'notification.created',
} as const
export type ActivityEventType = (typeof ActivityEventType)[keyof typeof ActivityEventType]

export const JOB_FINISHED_TYPES: readonly string[] = [
  ActivityEventType.JobSucceeded,
  ActivityEventType.JobFailed,
  ActivityEventType.JobCancelled,
]

/** ActivityEventDto: ids, states and a short redacted message; never a secret. Which fields are filled depends on Type. */
export interface ActivityEvent {
  Type: ActivityEventType | string
  OrganizationId: number
  JobId?: number | null
  JobKind?: StoreJobKind | null
  JobStatus?: StoreJobStatus | null
  Attempt?: number | null
  Store?: StoreKind | null
  AppId?: number | null
  ConnectionId?: number | null
  StepOrder?: number | null
  StepName?: string | null
  StepStatus?: StoreJobStepStatus | null
  Direction?: StepDirection | null
  // Units done and in total for a step that reports progress (bytes for an upload).
  Progress?: number | null
  Total?: number | null
  ConnectionStatus?: ConnectionStatus | null
  Message?: string | null
  Date: IsoDateTime
  // The record the event is about (NotificationEntity) and its id; the notification of notification.created.
  RelatedEntityType?: number | null
  RelatedEntityId?: string | null
  NotificationId?: number | null
}

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
  // The hub answered JoinOrganization with false: the caller is not (or no longer) a member.
  refused?: boolean
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
