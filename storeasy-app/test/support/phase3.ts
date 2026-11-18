import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import {
  ArtifactPlatform,
  ArtifactPullSourceKind,
  ArtifactSessionStatus,
  ArtifactSource,
  ArtifactStepKind,
  ArtifactStepStatus,
  CheckStatus,
  DeliveryTarget,
  DeliveryWarningAction,
  IdentityKind,
  OidcExchangeResult,
  OidcProvider,
  PackageKind,
  type ArtifactAppSummaryDto,
  type ArtifactCountsDto,
  type ArtifactSessionDetailDto,
  type ArtifactSessionListDto,
  type ArtifactSourceDto,
  type ArtifactUploadDto,
  type CiTokenDto,
  type DeliveryRulesDto,
  type IntakeSettingsDto,
  type TrustedPublisherDto,
  type TrustedPublisherTestDto,
} from '@/features/intake/types'
import type { PersonalAccessTokenDto } from '@/features/tokens/types'
import type { BetaGroupDto } from '@/features/testflight/types'
import { make, TEST_ORG_ID } from './factories'
import { endpoint, envelope, errorResponse, listEnvelope } from './handlers'
import { server } from './server'

// The phase 3 fake backend (personal access tokens and package intake): DTO builders typed with the screens' contract,
// the data the handlers answer with, and one handler per endpoint of src/features/{tokens,intake}/api.ts
// (phase3Contract.test.ts checks the pairing). A test file calls setupPhase3Api() once.

export const PART_URL = 'https://storage.test/bucket/artifact-part'

// ---- Builders ----------------------------------------------------------------------------------------------------------
export function pat(overrides: Partial<PersonalAccessTokenDto> = {}): PersonalAccessTokenDto {
  return make<PersonalAccessTokenDto>(
    {
      Id: 51,
      Name: 'Claude Code · dizüstü',
      OrganizationId: TEST_ORG_ID,
      OrganizationName: 'Kuzey Lab',
      ReadOnly: false,
      Prefix: 'sea_9c1',
      ExpiryDate: '2027-02-16T00:00:00Z',
      LastUseDate: '2026-11-18T10:58:00Z',
      LastClientName: 'Claude Code',
      CreationDate: '2026-11-18T08:00:00Z',
      IsExpired: false,
    },
    overrides,
  )
}

export function sessionListItem(overrides: Partial<ArtifactSessionListDto> = {}): ArtifactSessionListDto {
  return make<ArtifactSessionListDto>(
    {
      Id: 4127,
      AppId: 101,
      AppName: 'Pusula',
      Platform: ArtifactPlatform.Android,
      FileName: 'pusula-1.0.0-42.aab',
      Size: 50_331_648,
      Sha256: '9e41c0d2aa11bb22cc33dd44ee55ff6677889900aabbccddeeff00112233b703',
      Source: ArtifactSource.GitHubActions,
      SourceRef: { Provider: 'github', RunId: '318', RunAttempt: '1', Repository: 'kuzeylab/pusula', Branch: 'main', Commit: 'a41f9c2e' },
      Status: ArtifactSessionStatus.Delivering,
      StatusDescription: 'Delivering to Google Play internal',
      StartDate: '2026-11-18T10:58:02Z',
      EndDate: null,
      StoreJobId: 48500,
      OriginalSessionId: null,
    },
    overrides,
  )
}

export function sessionDetail(overrides: Partial<ArtifactSessionDetailDto> = {}): ArtifactSessionDetailDto {
  return make<ArtifactSessionDetailDto>(
    {
      ...sessionListItem(),
      Steps: [
        {
          Kind: ArtifactStepKind.Upload,
          Status: ArtifactStepStatus.Done,
          Message: '6 parts',
          ResumeCount: 1,
          StartDate: '2026-11-18T10:58:02Z',
          EndDate: '2026-11-18T10:58:41Z',
        },
        { Kind: ArtifactStepKind.Parse, Status: ArtifactStepStatus.Done, Message: 'dev.kuzeylab.pusula 1.0.0 (42)' },
        { Kind: ArtifactStepKind.Check, Status: ArtifactStepStatus.Done, Message: '3 / 3 passed' },
        { Kind: ArtifactStepKind.Rule, Status: ArtifactStepStatus.Done, Message: 'Google Play internal' },
        {
          Kind: ArtifactStepKind.Deliver,
          Status: ArtifactStepStatus.Running,
          Message: 'edits.bundles.upload',
          Bytes: 31_457_280,
          Total: 50_331_648,
        },
        { Kind: ArtifactStepKind.Report, Status: ArtifactStepStatus.Pending },
      ],
      Package: {
        Kind: PackageKind.Aab,
        Identifier: 'dev.kuzeylab.pusula',
        VersionName: '1.0.0',
        VersionCode: '42',
        TargetSdk: '36',
        MinOs: '26',
      },
      Checks: [{ Key: 'target-api', Status: CheckStatus.Ok, RuleId: 'google.build.target-api', Title: 'Target API 36' }],
      AppliedRule: {
        Platform: ArtifactPlatform.Android,
        Target: DeliveryTarget.PlayTrack,
        TargetId: 'internal',
        TargetName: 'internal',
        OnWarning: DeliveryWarningAction.Hold,
        Override: false,
        RuleUpdatedByUserName: 'Ece Yılmaz',
        RuleUpdatedDate: '2026-11-02T09:00:00Z',
      },
      Identity: { Kind: IdentityKind.Oidc, TrustedPublisherId: 61 },
      HoldReason: null,
      CiResult: null,
      PackageDeleted: false,
    },
    overrides,
  )
}

export function ciToken(overrides: Partial<CiTokenDto> = {}): CiTokenDto {
  return make<CiTokenDto>(
    {
      Id: 71,
      Name: 'codemagic-pusula',
      AppIds: [101],
      AppNames: ['Pusula'],
      AllApps: false,
      Scopes: ['artifacts:write'],
      Prefix: 'sci_7f2',
      ExpiryDate: '2027-05-18T00:00:00Z',
      IsExpired: false,
      LastUseDate: '2026-11-18T10:41:00Z',
      LastUseSource: 'Codemagic',
      CreatorUserName: 'Deniz Arslan',
      CreationDate: '2026-11-01T00:00:00Z',
    },
    overrides,
  )
}

export function publisher(overrides: Partial<TrustedPublisherDto> = {}): TrustedPublisherDto {
  return make<TrustedPublisherDto>(
    {
      Id: 61,
      AppId: 101,
      AppName: 'Pusula',
      Provider: OidcProvider.GitHub,
      Repository: 'kuzeylab/pusula',
      Branch: 'main',
      Environment: 'play-internal',
      Audience: 'storeasy',
      ExpectedClaims: {
        iss: 'https://token.actions.githubusercontent.com',
        aud: 'storeasy',
        repository: 'kuzeylab/pusula',
        ref: 'refs/heads/main',
        environment: 'play-internal',
      },
      LastExchangeDate: '2026-11-18T10:58:00Z',
      LastExchangeResult: OidcExchangeResult.ClaimMismatch,
      LastExchangeRunUrl: 'https://github.com/kuzeylab/pusula/actions/runs/318',
      LastClaims: null,
      CreatorUserName: 'Deniz Arslan',
      CreationDate: '2026-11-01T00:00:00Z',
    },
    overrides,
  )
}

export function pullSource(overrides: Partial<ArtifactSourceDto> = {}): ArtifactSourceDto {
  return make<ArtifactSourceDto>(
    {
      Id: 81,
      Kind: ArtifactPullSourceKind.Eas,
      AppId: 101,
      AppName: 'Pusula',
      Url: '/api/webhooks/eas/src_4kq9m2',
      AllowedDomains: ['expo.dev', '*.expo.dev'],
      Platforms: [ArtifactPlatform.Ios, ArtifactPlatform.Android],
      IncludeApk: false,
      SecretSetDate: '2026-11-02T00:00:00Z',
      LastCallDate: null,
      LastCallResult: null,
    },
    overrides,
  )
}

// ---- State -------------------------------------------------------------------------------------------------------------
export interface Phase3State {
  tokens: PersonalAccessTokenDto[]
  summary: ArtifactAppSummaryDto[]
  counts: ArtifactCountsDto
  sessions: ArtifactSessionListDto[]
  session: ArtifactSessionDetailDto
  ciTokens: CiTokenDto[]
  publishers: TrustedPublisherDto[]
  publisherTest: TrustedPublisherTestDto
  rules: DeliveryRulesDto
  sources: ArtifactSourceDto[]
  settings: IntakeSettingsDto
  upload: ArtifactUploadDto
  groups: BetaGroupDto[]
}

function initialPhase3(): Phase3State {
  return {
    tokens: [
      pat(),
      pat({ Id: 52, Name: 'Eski deneme', Prefix: 'sea_b52', IsExpired: true, ExpiryDate: '2026-11-01T00:00:00Z', ReadOnly: true }),
    ],
    counts: {
      From: '2026-10-19T00:00:00Z',
      To: '2026-11-18T12:00:00Z',
      Total: 5,
      ByStatus: [
        { Status: ArtifactSessionStatus.Delivered, Count: 14 },
        { Status: ArtifactSessionStatus.Rejected, Count: 1 },
        { Status: ArtifactSessionStatus.Held, Count: 2 },
      ],
      BySource: [
        { Source: ArtifactSource.GitHubActions, Count: 2 },
        { Source: ArtifactSource.AzureDevOps, Count: 2 },
        { Source: ArtifactSource.Eas, Count: 1 },
      ],
    },
    summary: [
      {
        AppId: 101,
        AppName: 'Pusula',
        LastSession: sessionListItem(),
        Rules: [
          {
            Platform: ArtifactPlatform.Android,
            Enabled: true,
            Target: DeliveryTarget.PlayTrack,
            TargetId: 'internal',
            TargetName: 'internal',
            OnWarning: DeliveryWarningAction.Hold,
          },
        ],
      },
    ],
    sessions: [
      sessionListItem(),
      sessionListItem({
        Id: 4102,
        FileName: 'pusula-dev-1.0.0-41.aab',
        Status: ArtifactSessionStatus.Rejected,
        StartDate: '2026-11-17T17:02:00Z',
        SourceRef: { RunId: '127' },
      }),
    ],
    session: sessionDetail(),
    ciTokens: [
      ciToken(),
      ciToken({
        Id: 72,
        Name: 'eski-jenkins',
        AllApps: true,
        AppIds: [],
        AppNames: [],
        Prefix: 'sci_2b1',
        IsExpired: true,
        ExpiryDate: '2026-11-01T00:00:00Z',
      }),
    ],
    publishers: [publisher()],
    publisherTest: {
      Valid: true,
      Matched: false,
      Claims: [
        { Claim: 'repository', Expected: 'kuzeylab/pusula', Actual: 'kuzeylab/pusula', Matches: true },
        { Claim: 'ref', Expected: 'refs/heads/main', Actual: 'refs/heads/feature/x', Matches: false },
      ],
    },
    rules: {
      AppId: 101,
      Rules: [
        {
          Platform: ArtifactPlatform.Android,
          Enabled: true,
          Target: DeliveryTarget.PlayTrack,
          TargetId: 'internal',
          TargetName: 'internal',
          OnWarning: DeliveryWarningAction.Hold,
        },
      ],
      UpdatedByUserName: 'Ece Yılmaz',
      UpdatedDate: '2026-11-02T09:00:00Z',
    },
    sources: [pullSource()],
    settings: { ArtifactRetentionDays: 30, MaxArtifactBytes: 4 * 1024 * 1024 * 1024 },
    upload: {
      SessionId: 4200,
      Duplicate: false,
      Resumed: false,
      PartSize: 8 * 1024 * 1024,
      Parts: [{ PartNumber: 1, Url: PART_URL }],
      UploadedParts: [],
      UrlExpiryDate: '2026-11-18T12:00:00Z',
    },
    groups: [
      {
        Id: 501,
        Name: 'Kuzey içi',
        IsInternal: true,
        TesterCount: 4,
        PublicLinkEnabled: false,
        PublicLinkLimitEnabled: false,
        SyncedDate: '2026-11-18T09:00:00Z',
      },
    ],
  }
}

export const phase3State: Phase3State = initialPhase3()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

export const phase3Handlers: HttpHandler[] = [
  // ---- tokens ----------------------------------------------------------------------------------------------------------
  endpoint('get', '/Tokens', () => json(envelope(phase3State.tokens))),
  endpoint('post', '/Tokens/token', ({ body }) => {
    const input = body as { ReadOnly: boolean; Name: string }
    if (input.Name === 'conflict') {
      return errorResponse('OrganizationForbidden', 'Bu organizasyonda yalnız salt okunur token oluşturabilirsin.', 403)
    }
    return json(
      envelope({ Id: 60, Token: 'sea_9c1f4b7e2a6d0c83f15a9b2e7d4c61aa0f3e8b52', Prefix: 'sea_9c1', ExpiryDate: '2027-02-16T00:00:00Z' }),
    )
  }),
  endpoint('delete', '/Tokens/token', () => json(envelope(1))),

  // ---- artifacts -------------------------------------------------------------------------------------------------------
  endpoint('get', '/Artifacts/summary', () => json(envelope(phase3State.summary))),
  endpoint('get', '/Artifacts/counts', () => json(envelope(phase3State.counts))),
  endpoint('patch', '/Artifacts/sessions', () => json(listEnvelope(phase3State.sessions))),
  endpoint('get', '/Artifacts/session', () => json(envelope(phase3State.session))),
  endpoint('post', '/Artifacts/session/deliver', () =>
    json(envelope({ ...phase3State.session, Status: ArtifactSessionStatus.Delivering, HoldReason: null })),
  ),
  endpoint('delete', '/Artifacts/session', () => json(envelope(phase3State.session.Id))),
  endpoint('post', '/apps/:appId/artifacts', () => json(envelope(phase3State.upload))),
  endpoint('put', '/Artifacts/:sessionId/complete', () =>
    json(envelope(sessionListItem({ Id: phase3State.upload.SessionId, Status: ArtifactSessionStatus.Parsing }))),
  ),
  endpoint('get', '/Artifacts/ci-tokens', () => json(envelope(phase3State.ciTokens))),
  endpoint('post', '/Artifacts/ci-token', () =>
    json(envelope({ Id: 73, Token: 'sci_7f2a4QmV9xT2kLr8NwZp3Hc6YbJd1EuS5gAo', Prefix: 'sci_7f2', ExpiryDate: '2027-02-16T00:00:00Z' })),
  ),
  endpoint('delete', '/Artifacts/ci-token', () => json(envelope(1))),
  endpoint('get', '/Artifacts/trusted-publishers', () => json(envelope(phase3State.publishers))),
  endpoint('get', '/Artifacts/trusted-publisher', ({ url }) => {
    const found = phase3State.publishers.find((item) => item.Id === Number(url.searchParams.get('Id')))
    return found ? json(envelope(found)) : errorResponse('TrustedPublisherNotFound', 'Kural bulunamadı.', 404)
  }),
  endpoint('post', '/Artifacts/trusted-publisher', () => json(envelope(62))),
  endpoint('delete', '/Artifacts/trusted-publisher', () => json(envelope(1))),
  endpoint('post', '/Artifacts/trusted-publisher/test', () => json(envelope(phase3State.publisherTest))),
  endpoint('get', '/Artifacts/delivery-rules', () => json(envelope(phase3State.rules))),
  endpoint('put', '/Artifacts/delivery-rules', () => json(envelope(1))),
  endpoint('get', '/Artifacts/sources', () => json(envelope(phase3State.sources))),
  endpoint('put', '/Artifacts/source', ({ body }) => {
    const input = body as { Id?: number | null; RotateSecret: boolean; Kind: ArtifactPullSourceKind }
    const created = !input.Id
    return json(
      envelope({
        Id: input.Id ?? 82,
        Url: input.Kind === ArtifactPullSourceKind.Eas ? '/api/webhooks/eas/src_new' : '/api/webhooks/artifacts/src_new',
        Secret: created || input.RotateSecret ? 'whsec_src_Q4n8Lr2VxT9mKp6Z' : null,
      }),
    )
  }),
  endpoint('delete', '/Artifacts/source', () => json(envelope(1))),
  endpoint('get', '/Artifacts/settings', () => json(envelope(phase3State.settings))),
  endpoint('put', '/Artifacts/settings', () => json(envelope(1))),
]

/** Answers the target pickers of the rule editor (other features' endpoints); kept out of the contract pairing. */
export const phase3SupportHandlers: HttpHandler[] = [endpoint('get', '/TestFlight/groups', () => json(envelope(phase3State.groups)))]

export function setupPhase3Api(): void {
  beforeEach(() => {
    Object.assign(phase3State, initialPhase3())
    server.use(...phase3Handlers, ...phase3SupportHandlers)
  })
}
