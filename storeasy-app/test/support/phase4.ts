import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { ActionChannel, BuildProcessingState, StoreKind } from '@/lib/api/types'
import { type NotificationPreferenceDto, NotificationEntity, NotificationType, type AccountNotificationDto } from '@/features/notifications/types'
import {
  ChecklistItemStatus,
  RejectionKind,
  RejectionSource,
  RejectionState,
  type RejectionCaseDto,
  type RejectionCaseListDto,
  type RejectionHistoryDto,
} from '@/features/rejections/types'
import { ReviewReplyState, type ReviewListDto, type SavedReplyDto } from '@/features/reviews/types'
import {
  RuleProposalPreparer,
  RuleProposalStatus,
  RuleSourceStatus,
  type RuleProposalDetailDto,
  type RuleSourceDto,
} from '@/features/rules-watcher/types'
import {
  BetaInviteType,
  BetaReviewState,
  BetaTesterState,
  type BetaBuildDto,
  type BetaGroupDto,
  type BetaLocalizationDto,
  type BetaTesterDto,
} from '@/features/testflight/types'
import {
  WebhookDeliveryStatus,
  WebhookProcessResult,
  WebhookRegistrationState,
  WebhookSource,
  type OrgWebhookDto,
  type WebhookDeliveryDto,
  type WebhookEndpointDto,
  type WebhookEventDetailDto,
  type WebhookSecretDto,
} from '@/features/webhooks/types'
import { make, TEST_ORG_ID } from './factories'
import { endpoint, envelope, listEnvelope } from './handlers'
import { server } from './server'

// The phase 4 fake backend (reviews, rejections, TestFlight, webhooks, notifications, preferences, rule watcher): DTO
// builders typed with the screens' contract, the data the handlers answer with, and one handler per endpoint of the
// phase 4 api.ts files (phase4Contract.test.ts checks the pairing). A test file calls setupPhase4Api() once.

// ---- Builders ----------------------------------------------------------------------------------------------------------
export function review(overrides: Partial<ReviewListDto> = {}): ReviewListDto {
  return make<ReviewListDto>(
    {
      Id: 3001,
      AppId: 101,
      AppName: 'Pusula',
      Store: StoreKind.Google,
      StoreAppId: 202,
      ExternalReviewId: 'gp-1',
      Rating: 2,
      Title: 'Hatırlatıcılar',
      Body: 'Son güncellemeden sonra hatırlatıcılar gelmiyor.',
      AuthorName: 'Ayşe K.',
      Locale: 'tr',
      Territory: 'TR',
      AppVersion: '3.1.2',
      Device: 'Pixel 8',
      ReviewDate: '2026-11-18T10:48:00Z',
      IsRead: false,
      ReplyState: ReviewReplyState.None,
      ReplyMaxLength: 350,
    },
    overrides,
  )
}

export function savedReply(overrides: Partial<SavedReplyDto> = {}): SavedReplyDto {
  return make<SavedReplyDto>(
    {
      Id: 41,
      Name: 'Hata bildirimi için teşekkür',
      Variants: [
        { Locale: 'tr', Text: 'Merhaba {ad}, {uygulama} {sürüm} için teşekkürler.' },
        { Locale: 'en-US', Text: 'Hi {name}, thanks for using {app}.' },
      ],
      AppIds: [],
      UseCount: 41,
      UpdatedDate: '2026-11-12T09:00:00Z',
      UpdatedByUserName: 'ece@kuzeylab.dev',
    },
    overrides,
  )
}

export function rejectionCase(overrides: Partial<RejectionCaseDto> = {}): RejectionCaseDto {
  return make<RejectionCaseDto>(
    {
      Id: 601,
      AppId: 101,
      AppName: 'Pusula',
      StoreAppId: 201,
      Store: StoreKind.Apple,
      VersionString: '1.4',
      BuildNumber: '52',
      Kind: RejectionKind.Rejected,
      State: RejectionState.Open,
      OpenedVia: RejectionSource.Webhook,
      OpenedDate: '2026-11-16T09:14:00Z',
      ResolutionCenterUrl: 'https://appstoreconnect.apple.com/apps/6740056789/distribution/reviewsubmissions',
      RawText: 'Guideline 2.1 - Performance - App Completeness\nWe were unable to sign in.',
      RawTextByUserName: 'ece@kuzeylab.dev',
      RawTextDate: '2026-11-16T10:02:00Z',
      Guidelines: [{ Code: '2.1', Title: 'App Completeness' }],
      RepeatedGuidelines: [],
      Checklist: [
        { Id: 'c1', GuidelineCode: '2.1', Text: 'Demo hesap şifresini yenile', Status: ChecklistItemStatus.Done, DoneDate: '2026-11-17T11:20:00Z' },
        { Id: 'c2', GuidelineCode: '2.1', Text: 'İnceleme notunu güncelle', Status: ChecklistItemStatus.Todo, FixRoute: 'release/review-info' },
      ],
      ReplyStepId: 5590,
      ReplyDraft: {
        Body: 'Hello App Review team,\nThe demo account works now.',
        Length: 49,
        MaxLength: 4000,
        OverLimit: false,
        UpdatedDate: '2026-11-17T15:42:00Z',
        ByUserName: 'deniz@kuzeylab.dev',
        Channel: ActionChannel.Web,
      },
      Timeline: [{ Date: '2026-11-16T09:14:00Z', Kind: 'opened', Text: 'Webhook geldi: REJECTED', Channel: ActionChannel.System }],
    },
    overrides,
  )
}

export function rejectionListItem(overrides: Partial<RejectionCaseListDto> = {}): RejectionCaseListDto {
  return make<RejectionCaseListDto>(
    {
      Id: 601,
      AppId: 101,
      Store: StoreKind.Apple,
      StoreAppId: 201,
      VersionString: '1.4',
      BuildNumber: '52',
      Kind: RejectionKind.Rejected,
      State: RejectionState.Open,
      Guidelines: [{ Code: '2.1' }, { Code: '5.1.1' }],
      OpenedVia: RejectionSource.Webhook,
      OpenedDate: '2026-11-16T09:14:00Z',
      ChecklistDone: 1,
      ChecklistTotal: 4,
    },
    overrides,
  )
}

export function betaBuild(overrides: Partial<BetaBuildDto> = {}): BetaBuildDto {
  return make<BetaBuildDto>(
    {
      BuildId: 9101,
      VersionString: '1.0',
      BuildNumber: '37',
      ProcessingState: BuildProcessingState.Valid,
      UploadedDate: '2026-08-26T10:00:00Z',
      ExpirationDate: '2026-11-24T10:00:00Z',
      Expired: false,
      DaysLeft: 6,
      BetaReviewState: BetaReviewState.NotSubmitted,
      GroupIds: [71],
      WhatToTest: [{ Locale: 'tr', Text: 'Davet akışını dene.' }],
    },
    overrides,
  )
}

export function betaGroup(overrides: Partial<BetaGroupDto> = {}): BetaGroupDto {
  return make<BetaGroupDto>(
    {
      Id: 71,
      Name: 'Erken erişim',
      IsInternal: false,
      TesterCount: 48,
      PublicLinkEnabled: true,
      PublicLink: 'https://testflight.apple.com/join/kZ4mQ2pL',
      PublicLinkLimit: 100,
      PublicLinkLimitEnabled: true,
      BuildIds: [9101],
      SyncedDate: '2026-11-18T06:00:00Z',
    },
    overrides,
  )
}

export function betaTester(overrides: Partial<BetaTesterDto> = {}): BetaTesterDto {
  return make<BetaTesterDto>(
    {
      Id: 8801,
      FirstName: 'Aylin',
      LastName: 'Çelik',
      Email: 'aylin.celik@ornek.net',
      InviteType: BetaInviteType.Email,
      State: BetaTesterState.Installed,
      GroupExternalIds: ['g-71'],
    },
    overrides,
  )
}

export function webhookEndpoint(overrides: Partial<WebhookEndpointDto> = {}): WebhookEndpointDto {
  return make<WebhookEndpointDto>(
    {
      Id: 31,
      StoreConnectionId: 11,
      ConnectionName: 'Kuzey Lab ASC',
      Source: WebhookSource.AppStoreConnect,
      PublicId: 'whe_3k9x2m7q',
      Url: 'https://storeasy.example.test/api/webhooks/apple/whe_3k9x2m7q',
      Registration: WebhookRegistrationState.Api,
      ExternalWebhookCount: 5,
      Events: ['APP_STORE_VERSION_APP_VERSION_STATE_UPDATED'],
      SecretSetDate: '2026-11-02T10:00:00Z',
      LastEventDate: '2026-11-18T09:31:00Z',
    },
    overrides,
  )
}

export function webhookEvent(overrides: Partial<WebhookEventDetailDto> = {}): WebhookEventDetailDto {
  return make<WebhookEventDetailDto>(
    {
      Id: 7001,
      WebhookEndpointId: 31,
      EventType: 'APP_STORE_VERSION_APP_VERSION_STATE_UPDATED',
      AppId: 101,
      StoreAppId: 201,
      OldValue: 'IN_REVIEW',
      NewValue: 'REJECTED',
      SignatureValid: true,
      ReceivedDate: '2026-11-16T08:05:12Z',
      ProcessedDate: '2026-11-16T08:05:13Z',
      ProcessResult: WebhookProcessResult.Processed,
      ProcessMessage: 'red kaydı açıldı',
      RelatedEntityType: NotificationEntity.RejectionCase,
      RelatedEntityId: '601',
      Payload: '{"data":{"type":"appStoreVersionAppVersionStateUpdated"}}',
      SignatureHeaderMasked: 'hmacsha256=4f9a••••c21e',
    },
    overrides,
  )
}

export function subscription(overrides: Partial<OrgWebhookDto> = {}): OrgWebhookDto {
  return make<OrgWebhookDto>(
    {
      Id: 51,
      Name: 'Slack köprüsü',
      Url: 'https://hooks.kuzeylab.dev/storeasy',
      Events: ['rejection.opened'],
      IsActive: true,
      LastDeliveryDate: '2026-11-18T09:00:00Z',
      LastDeliveryStatus: WebhookDeliveryStatus.Succeeded,
      CreationDate: '2026-11-01T09:00:00Z',
    },
    overrides,
  )
}

export function delivery(overrides: Partial<WebhookDeliveryDto> = {}): WebhookDeliveryDto {
  return make<WebhookDeliveryDto>(
    {
      Id: 9901,
      OrganizationWebhookId: 51,
      EventType: 'rejection.opened',
      EventId: 'evt-1',
      Status: WebhookDeliveryStatus.Succeeded,
      Attempt: 1,
      MaxAttempts: 6,
      ResponseStatusCode: 200,
      DurationMilliseconds: 120,
      IsTest: false,
      CreationDate: '2026-11-18T09:00:00Z',
    },
    overrides,
  )
}

export function secretAnswer(overrides: Partial<WebhookSecretDto> = {}): WebhookSecretDto {
  return make<WebhookSecretDto>(
    {
      Id: 31,
      Url: 'https://storeasy.example.test/api/webhooks/apple/whe_3k9x2m7q',
      Secret: 'whsec_asc_Vb7Kq2Xm9Lp4Rt8N',
      Registration: WebhookRegistrationState.Api,
      SecretSetDate: '2026-11-18T10:00:00Z',
      PreviousSecretValidUntil: '2026-11-19T10:00:00Z',
    },
    overrides,
  )
}

export function notification(overrides: Partial<AccountNotificationDto> = {}): AccountNotificationDto {
  return make<AccountNotificationDto>(
    {
      Id: 1201,
      RecipientUserName: 'deniz@kuzeylab.dev',
      Type: NotificationType.Rejection,
      Text: 'Defter 1.4 reddedildi: Guideline 2.1.',
      RelatedEntityType: NotificationEntity.RejectionCase,
      RelatedEntityId: '601',
      ActionLink: '/apps/101/rejections/601',
      IsSeen: false,
      CreationDate: '2026-11-16T09:14:00Z',
      OrganizationId: TEST_ORG_ID,
    },
    overrides,
  )
}

export function preference(overrides: Partial<NotificationPreferenceDto> = {}): NotificationPreferenceDto {
  return make<NotificationPreferenceDto>(
    { Kind: NotificationType.Rejection, InApp: true, Email: true, WebPush: false, Locked: false },
    overrides,
  )
}

export function ruleSource(overrides: Partial<RuleSourceDto> = {}): RuleSourceDto {
  return make<RuleSourceDto>(
    {
      Id: 3,
      Name: 'App Store Connect sürüm notları',
      Store: StoreKind.Apple,
      Url: 'https://developer.apple.com/help/app-store-connect/release-notes/',
      IsActive: true,
      Status: RuleSourceStatus.Changed,
      LastFetchDate: '2026-11-18T14:00:00Z',
      LastChangeDate: '2026-11-17T22:00:00Z',
      ContentHash: 'a91fbbbbbbbb3b7e',
      PreviousContentHash: '52c0aaaaaaaad118',
    },
    overrides,
  )
}

export function ruleProposal(overrides: Partial<RuleProposalDetailDto> = {}): RuleProposalDetailDto {
  return make<RuleProposalDetailDto>(
    {
      Id: 14,
      Title: 'Duo koşulu netleşti',
      Store: StoreKind.Apple,
      RuleIds: ['apple.screenshots.duo'],
      RuleSourceId: 3,
      Status: RuleProposalStatus.Pending,
      PreparedBy: RuleProposalPreparer.Mcp,
      PreparedByUserName: 'deniz@kuzeylab.dev',
      CatalogVersionFrom: '1.3',
      CatalogVersionTo: '1.4',
      CreationDate: '2026-11-18T09:40:00Z',
      SourceName: 'App Store Connect sürüm notları',
      SourceUrl: 'https://developer.apple.com/help/app-store-connect/release-notes/',
      SourceExcerptBefore: 'Beginning April 2027, submissions must include screenshots for iPhone Duo.',
      SourceExcerptAfter: 'Beginning April 2027, apps built with the iOS 27.1 SDK must include screenshots for iPhone Duo.',
      CatalogPatch: '{ "id": "apple.screenshots.duo" }',
      PreparerNote: 'Koşulu değiştirmedim.',
      NotifiedOrganizationCount: 0,
      Impact: [
        { OrganizationId: 7, AppId: 101, AppName: 'Pusula', RuleId: 'apple.screenshots.duo', Message: 'Pusula Duo ekran görüntüsü isteyecek.' },
        { OrganizationId: 9, AppId: 140, AppName: 'Ada', RuleId: 'apple.screenshots.duo', Message: 'Ada Duo ekran görüntüsü isteyecek.' },
      ],
    },
    overrides,
  )
}

// ---- Data --------------------------------------------------------------------------------------------------------------
export interface Phase4State {
  reviews: ReviewListDto[]
  savedReplies: SavedReplyDto[]
  rejections: RejectionCaseListDto[]
  rejection: RejectionCaseDto
  history: RejectionHistoryDto
  builds: BetaBuildDto[]
  groups: BetaGroupDto[]
  testers: BetaTesterDto[]
  localizations: BetaLocalizationDto[]
  endpoints: WebhookEndpointDto[]
  events: WebhookEventDetailDto[]
  subscriptions: OrgWebhookDto[]
  deliveries: WebhookDeliveryDto[]
  notifications: AccountNotificationDto[]
  preferences: NotificationPreferenceDto[]
  sources: RuleSourceDto[]
  proposal: RuleProposalDetailDto
}

function initialPhase4(): Phase4State {
  return {
    reviews: [
      review(),
      review({
        Id: 3002,
        Store: StoreKind.Apple,
        StoreAppId: 201,
        Rating: 5,
        Title: undefined,
        Body: 'Ruhig und klar.',
        AuthorName: 'jonas_b',
        Locale: 'de-DE',
        IsRead: true,
        ReplyState: ReviewReplyState.Published,
        ReplyBody: 'Danke!',
        ReplyByUserName: 'ece@kuzeylab.dev',
        ReplyDate: '2026-11-18T11:00:00Z',
        ReplyMaxLength: 5970,
      }),
    ],
    savedReplies: [savedReply()],
    rejections: [rejectionListItem()],
    rejection: rejectionCase(),
    history: {
      AppId: 101,
      SubmissionCount: 2,
      RejectionCount: 1,
      RepeatedGuidelines: [],
      Submissions: [
        { StoreAppId: 201, VersionString: '1.3', BuildNumber: '50', State: 'Kabul', SubmittedDate: '2026-10-01T10:00:00Z', Rejections: [] },
        { StoreAppId: 201, VersionString: '1.4', BuildNumber: '52', SubmittedDate: '2026-11-14T18:22:00Z', Rejections: [rejectionListItem()] },
      ],
    },
    builds: [
      betaBuild(),
      betaBuild({
        BuildId: 9100,
        BuildNumber: '35',
        Expired: true,
        DaysLeft: 0,
        ExpirationDate: '2026-11-09T10:00:00Z',
        UploadedDate: '2026-08-11T10:00:00Z',
        BetaReviewState: BetaReviewState.Approved,
        GroupIds: [],
        WhatToTest: [],
      }),
    ],
    groups: [betaGroup(), betaGroup({ Id: 70, Name: 'Kuzey içi', IsInternal: true, TesterCount: 4, PublicLinkEnabled: false, PublicLink: undefined, BuildIds: [] })],
    testers: [betaTester(), betaTester({ Id: 8802, FirstName: 'Derya', LastName: 'Aksoy', Email: 'derya.a@ornek.net', State: BetaTesterState.Invited })],
    localizations: [
      { Id: 1, Locale: 'tr', Description: 'Halka, yakın çevrenle küçük gruplar kurmanı sağlar.', FeedbackEmail: 'beta@kuzeylab.dev', SyncedDate: '2026-11-18T06:00:00Z' },
    ],
    endpoints: [webhookEndpoint()],
    events: [
      webhookEvent(),
      webhookEvent({
        Id: 7002,
        OldValue: undefined,
        NewValue: undefined,
        SignatureValid: false,
        ProcessResult: WebhookProcessResult.InvalidSignature,
        ProcessMessage: 'gövde okunmadı',
        RelatedEntityType: NotificationEntity.None,
        RelatedEntityId: undefined,
      }),
    ],
    subscriptions: [subscription()],
    deliveries: [delivery(), delivery({ Id: 9902, Status: WebhookDeliveryStatus.Failed, ResponseStatusCode: 500, Attempt: 2, IsTest: true, EventType: 'webhook.test' })],
    notifications: [
      notification(),
      notification({
        Id: 1202,
        Type: NotificationType.JobSucceeded,
        Text: 'Pusula 2.4 TestFlight grubuna teslim edildi.',
        IsSeen: true,
        ActionLink: undefined,
        RelatedEntityType: NotificationEntity.StoreJob,
        CreationDate: '2026-11-15T09:00:00Z',
      }),
    ],
    preferences: [
      preference(),
      preference({ Kind: NotificationType.NewReview, InApp: true, Email: false }),
      preference({ Kind: NotificationType.ConnectionAlert, Locked: true }),
    ],
    sources: [
      ruleSource(),
      ruleSource({ Id: 4, Name: 'Play hedef API sayfası', Store: StoreKind.Google, Status: RuleSourceStatus.Unreachable, LastError: 'HTTP 503' }),
    ],
    proposal: ruleProposal(),
  }
}

export const phase4State: Phase4State = initialPhase4()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)
const unseen = () => phase4State.notifications.filter((item) => !item.IsSeen)

// ---- Handlers ----------------------------------------------------------------------------------------------------------
export const phase4Handlers: HttpHandler[] = [
  endpoint('patch', '/Reviews', () => json(listEnvelope(phase4State.reviews))),
  endpoint('post', '/Reviews/reply', () => json(envelope(48600))),
  endpoint('put', '/Reviews/read', ({ body }) => json(envelope((body as { ReviewIds: number[] }).ReviewIds.length))),
  endpoint('put', '/Reviews/reply-draft', () => json(envelope(3001))),
  endpoint('post', '/Reviews/sync', () => json(envelope([48601, 48602]))),
  endpoint('get', '/Reviews/saved-replies', () => json(envelope(phase4State.savedReplies))),
  endpoint('post', '/Reviews/saved-reply', () => json(envelope(42))),
  endpoint('put', '/Reviews/saved-reply', () => json(envelope(41))),
  endpoint('delete', '/Reviews/saved-reply', () => json(envelope(41))),

  endpoint('patch', '/Rejections', () => json(listEnvelope(phase4State.rejections))),
  endpoint('get', '/Rejections/case', () => json(envelope(phase4State.rejection))),
  endpoint('post', '/Rejections/case', () => json(envelope(602))),
  endpoint('put', '/Rejections/case/text', ({ body }) => {
    const text = (body as { RawText: string }).RawText
    const guidelines = /Guideline\s+\d/.test(text) ? phase4State.rejection.Guidelines : []
    return json(envelope({ ...phase4State.rejection, RawText: text, Guidelines: guidelines }))
  }),
  endpoint('put', '/Rejections/case/checklist', () => json(envelope(phase4State.rejection))),
  endpoint('put', '/Rejections/case/reply-draft', () => json(envelope(phase4State.rejection.ReplyDraft))),
  endpoint('put', '/Rejections/case/reply-sent', () => json(envelope({ ...phase4State.rejection.ReplyDraft, SentDate: '2026-11-18T12:00:00Z' }))),
  endpoint('put', '/Rejections/case/close', () => json(envelope(601))),
  endpoint('get', '/Rejections/history', () => json(envelope(phase4State.history))),

  endpoint('get', '/TestFlight/builds', () => json(envelope(phase4State.builds))),
  endpoint('get', '/TestFlight/groups', () => json(envelope(phase4State.groups))),
  endpoint('patch', '/TestFlight/testers', () => json(listEnvelope(phase4State.testers))),
  endpoint('get', '/TestFlight/localizations', () => json(envelope(phase4State.localizations))),
  endpoint('post', '/TestFlight/sync', () => json(envelope(48700))),
  endpoint('post', '/TestFlight/group', () => json(envelope(48701))),
  endpoint('put', '/TestFlight/group', () => json(envelope(48702))),
  endpoint('put', '/TestFlight/group/testers', () => json(envelope(48703))),
  endpoint('put', '/TestFlight/group/builds', () => json(envelope(48704))),
  endpoint('put', '/TestFlight/build/what-to-test', () => json(envelope(48705))),
  endpoint('put', '/TestFlight/localizations', () => json(envelope(48706))),
  endpoint('post', '/TestFlight/build/beta-review', () => json(envelope(48707))),

  endpoint('get', '/Webhooks', () => json(envelope(phase4State.endpoints))),
  endpoint('post', '/Webhooks/endpoint', () => json(envelope(secretAnswer({ PreviousSecretValidUntil: undefined })))),
  endpoint('post', '/Webhooks/endpoint/rotate-secret', () => json(envelope(secretAnswer({ Url: undefined, Secret: 'whsec_rotated_123' })))),
  endpoint('delete', '/Webhooks/endpoint', () => json(envelope(31))),
  endpoint('patch', '/Webhooks/events', () => json(listEnvelope(phase4State.events))),
  endpoint('get', '/Webhooks/event', ({ url }) =>
    json(envelope(phase4State.events.find((item) => item.Id === Number(url.searchParams.get('EventId'))) ?? phase4State.events[0])),
  ),
  endpoint('post', '/Webhooks/event/reprocess', () => json(envelope(48800))),
  endpoint('get', '/Webhooks/subscriptions', () => json(envelope(phase4State.subscriptions))),
  endpoint('post', '/Webhooks/subscription', () => json(envelope(secretAnswer({ Id: 52, Url: undefined, Secret: 'whsec_sub_456', Registration: undefined, PreviousSecretValidUntil: undefined })))),
  endpoint('put', '/Webhooks/subscription', () => json(envelope(51))),
  endpoint('delete', '/Webhooks/subscription', () => json(envelope(51))),
  endpoint('post', '/Webhooks/subscription/rotate-secret', () => json(envelope(secretAnswer({ Id: 51, Url: undefined, Secret: 'whsec_sub_rotated' })))),
  endpoint('post', '/Webhooks/subscription/test', () => json(envelope(delivery({ Id: 9903, IsTest: true, EventType: 'webhook.test' })))),
  endpoint('patch', '/Webhooks/deliveries', () => json(listEnvelope(phase4State.deliveries))),

  endpoint('patch', '/Account/notifications', ({ body }) => {
    const filters = (body as { Filtering?: { Criterias: Array<{ FilterBy: string; Value: unknown }> } }).Filtering?.Criterias ?? []
    const unreadOnly = filters.some((item) => item.FilterBy === 'IsSeen')
    const type = filters.find((item) => item.FilterBy === 'Type')?.Value
    const rows = (unreadOnly ? unseen() : phase4State.notifications).filter((item) => type === undefined || item.Type === type)
    const rowCount = (body as { RowCount: number }).RowCount
    return json(listEnvelope(rows.slice(0, rowCount), 1, rows.length))
  }),
  endpoint('put', '/Account/notifications/seen', () => json(envelope(null))),
  endpoint('delete', '/Account/notifications', () => json(envelope(null))),
  endpoint('get', '/account/preferences', () => json(envelope(phase4State.preferences))),
  endpoint('put', '/account/preferences', ({ body }) => json(envelope({ ...(body as object), Locked: false }))),

  endpoint('get', '/rules/sources', () => json(envelope(phase4State.sources))),
  endpoint('post', '/rules/sources/fetch', () => json(envelope({ Checked: 6, Changed: 1, Unreachable: 0, ProposalsOpened: 1 }))),
  endpoint('patch', '/rules/proposals', () => json(listEnvelope([phase4State.proposal]))),
  endpoint('get', '/rules/proposals/proposal', () => json(envelope(phase4State.proposal))),
  endpoint('put', '/rules/proposal/prepare', () => json(envelope(14))),
  endpoint('put', '/rules/proposal/approve', () => json(envelope({ CatalogVersion: '1.4', NotifiedOrganizationCount: 2, NotifiedUserCount: 5 }))),
  endpoint('put', '/rules/proposal/reject', () => json(envelope(14))),
]

/** Resets the phase 4 data and answers its endpoints for every test of the calling file. */
export function setupPhase4Api(): void {
  beforeEach(() => {
    Object.assign(phase4State, initialPhase4())
    server.use(...phase4Handlers)
  })
}
