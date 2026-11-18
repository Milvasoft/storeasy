// Webhooks contract (storeasy-api Storeasy.Application EventDtos WebhookEndpointDto, WebhookEventDto,
// WebhookEventDetailDto, WebhookSecretDto, OrgWebhookDto, WebhookDeliveryDto and Storeasy.Domain enums).
import type { IsoDateTime } from '@/lib/api/types'
import type { NotificationEntity } from '@/features/notifications/types'

export const WebhookSource = { AppStoreConnect: 1, Signed: 2 } as const
export type WebhookSource = (typeof WebhookSource)[keyof typeof WebhookSource]

export const WebhookRegistrationState = { Pending: 1, Api: 2, Manual: 3, Failed: 4 } as const
export type WebhookRegistrationState = (typeof WebhookRegistrationState)[keyof typeof WebhookRegistrationState]

export const WebhookProcessResult = { Pending: 1, Processed: 2, Ignored: 3, Failed: 4, InvalidSignature: 5, Stale: 6 } as const
export type WebhookProcessResult = (typeof WebhookProcessResult)[keyof typeof WebhookProcessResult]

export const WebhookDeliveryStatus = { Pending: 1, Succeeded: 2, Failed: 3, Retrying: 4 } as const
export type WebhookDeliveryStatus = (typeof WebhookDeliveryStatus)[keyof typeof WebhookDeliveryStatus]

// OrgWebhookEventTypes.All: what an outgoing subscription may name (empty list: every event).
export const ORG_WEBHOOK_EVENTS = [
  'rejection.opened',
  'rejection.closed',
  'version.state_changed',
  'review.received',
  'review.replied',
  'testflight.build.expiring',
  'compliance.deadline',
  'webhook.test',
] as const

export interface ExternalWebhookRegistration {
  AppExternalId?: string
  WebhookId?: string
}

export interface WebhookEndpointDto {
  Id: number
  StoreConnectionId?: number
  ConnectionName?: string
  Source: WebhookSource
  Name?: string
  PublicId?: string
  Url?: string
  Registration: WebhookRegistrationState
  RegistrationError?: string
  ExternalWebhookCount: number
  ExternalWebhooks?: ExternalWebhookRegistration[]
  Events?: string[]
  SecretSetDate: IsoDateTime
  PreviousSecretValidUntil?: IsoDateTime
  LastEventDate?: IsoDateTime
  CreationDate?: IsoDateTime
}

// The secret appears only in this answer (create and rotate).
export interface WebhookSecretDto {
  Id: number
  Url?: string
  Secret?: string
  Registration?: WebhookRegistrationState
  SecretSetDate: IsoDateTime
  PreviousSecretValidUntil?: IsoDateTime
  JobId?: number
}

export interface WebhookEventDto {
  Id: number
  WebhookEndpointId: number
  EventType?: string
  AppId?: number
  StoreAppId?: number
  OldValue?: string
  NewValue?: string
  SignatureValid: boolean
  ReceivedDate: IsoDateTime
  ProcessedDate?: IsoDateTime
  ProcessResult: WebhookProcessResult
  ProcessMessage?: string
  RelatedEntityType: NotificationEntity
  RelatedEntityId?: string
}

export interface WebhookEventDetailDto extends WebhookEventDto {
  Payload?: string
  SignatureHeaderMasked?: string
  ExternalEventId?: string
  SubjectExternalId?: string
}

export interface OrgWebhookDto {
  Id: number
  Name?: string
  Url?: string
  Events?: string[]
  IsActive: boolean
  LastDeliveryDate?: IsoDateTime
  LastDeliveryStatus?: WebhookDeliveryStatus
  CreationDate?: IsoDateTime
}

export interface WebhookDeliveryDto {
  Id: number
  OrganizationWebhookId: number
  EventType?: string
  EventId?: string
  Status: WebhookDeliveryStatus
  Attempt: number
  MaxAttempts: number
  NextAttemptDate?: IsoDateTime
  ResponseStatusCode?: number
  ResponseBody?: string
  Error?: string
  DurationMilliseconds?: number
  IsTest: boolean
  CreationDate: IsoDateTime
  LastAttemptDate?: IsoDateTime
}
