// Notifications contract (storeasy-api AccountDtos AccountNotificationDto, NotificationDtos NotificationPreferenceDto and
// Storeasy.Domain NotificationType, NotificationEntity). PascalCase keys, numeric enums, null properties left out.
import type { IsoDateTime } from '@/lib/api/types'

export const NotificationType = {
  OrderApproved: 0,
  Rejection: 40,
  CertificationFailed: 41,
  JobSucceeded: 42,
  JobFailed: 43,
  PlanPending: 44,
  Deadline: 45,
  NewReview: 46,
  ReviewReplyFailed: 47,
  Invitation: 48,
  ConnectionAlert: 49,
  ArtifactRejected: 50,
  RuleChanged: 51,
  RuleProposal: 52,
  VersionState: 53,
  PackageRegistry: 54,
  EasBuild: 55,
} as const
export type NotificationType = (typeof NotificationType)[keyof typeof NotificationType]

export const NotificationEntity = {
  None: 0,
  App: 40,
  StoreJob: 41,
  RejectionCase: 42,
  Review: 43,
  Invitation: 44,
  StoreConnection: 45,
  ArtifactSession: 46,
  Build: 47,
  RuleProposal: 48,
  WebhookEvent: 49,
  Rule: 50,
  AppVersionState: 51,
  TrackedPackage: 52,
  EasBuild: 53,
} as const
export type NotificationEntity = (typeof NotificationEntity)[keyof typeof NotificationEntity]

export interface AccountNotificationDto {
  Id: number
  RecipientUserName?: string
  Type: NotificationType
  TypeDescription?: string
  SeenDate?: IsoDateTime
  Text?: string
  // JSON text: { key, args, data }.
  Data?: string
  RelatedEntityType: NotificationEntity
  RelatedEntityId?: string
  ActionLink?: string
  IsSeen: boolean
  IsSeenDescription?: string
  CreationDate?: IsoDateTime
  OrganizationId?: number
}

export interface NotificationPreferenceDto {
  Kind: NotificationType
  InApp: boolean
  Email: boolean
  WebPush: boolean
  // Security and connection alerts: in-app and e-mail cannot be turned off.
  Locked: boolean
}
