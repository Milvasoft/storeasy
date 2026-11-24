import { HttpResponse, type HttpHandler } from 'msw'
import { beforeEach } from 'vitest'
import { ConnectionStatus } from '@/lib/api/types'
import {
  EasAfterBuild,
  EasBuildStatus,
  EasPlatform,
  EasSubmissionStatus,
  type EasAppDto,
  type EasSubmissionDto,
  type EasBuildDto,
  type EasProfilesDto,
  type EasProjectDto,
  type ExpoConnectionDto,
} from '@/features/eas/types'
import { make } from './factories'
import { endpoint, envelope } from './handlers'
import { server } from './server'

// The EAS fake backend (EasController): DTO builders typed with the screens' own contract, the data the handlers answer
// with, and one handler per endpoint of src/features/eas/api.ts (easContract.test.ts checks the pairing).

export const EAS_APP_ID = 101

export function easBuild(overrides: Partial<EasBuildDto> = {}): EasBuildDto {
  return make<EasBuildDto>(
    {
      Id: 701,
      AppId: EAS_APP_ID,
      BuildId: '6c1f2a90-1b2c-4d5e-8f90-123456789abc',
      Platform: EasPlatform.Android,
      Status: EasBuildStatus.Finished,
      Profile: 'production',
      GitRef: 'main',
      GitCommitHash: 'abc123def456',
      AppVersion: '2.4.0',
      AppBuildVersion: '57',
      Distribution: 'STORE',
      StartedBy: 'deniz',
      StartedInStoreasy: true,
      PageUrl: 'https://expo.dev/accounts/kuzeylab/projects/pusula/builds/6c1f2a90-1b2c-4d5e-8f90-123456789abc',
      LogsUrl: 'https://logs.expo.dev/build.txt',
      ArtifactUrl: 'https://expo.dev/artifacts/eas/pusula.aab',
      StartedDate: '2026-11-24T08:00:00Z',
      FinishedDate: '2026-11-24T08:20:00Z',
      ErrorCode: null,
      ErrorMessage: null,
      ArtifactSessionId: null,
      AfterBuild: EasAfterBuild.None,
      PrepareReviewPlan: false,
      SubmitTrack: null,
      ReviewPlanIds: [],
      ChainError: null,
    },
    overrides,
  )
}

export function easSubmission(overrides: Partial<EasSubmissionDto> = {}): EasSubmissionDto {
  return make<EasSubmissionDto>(
    {
      Id: 801,
      AppId: EAS_APP_ID,
      EasBuildId: 701,
      SubmissionId: 'sub-1',
      Platform: EasPlatform.Android,
      Status: EasSubmissionStatus.Errored,
      Track: 'internal',
      ReleaseStatus: 'DRAFT',
      StartedBy: 'deniz',
      FromChain: true,
      StartedDate: '2026-11-24T08:25:00Z',
      FinishedDate: '2026-11-24T08:27:00Z',
      LogsUrl: 'https://logs.expo.dev/submission.txt',
      ErrorCode: 'SUBMISSION_FAILED',
      ErrorMessage: 'Package not found on Play',
    },
    overrides,
  )
}

export function easApp(overrides: Partial<EasAppDto> = {}): EasAppDto {
  return make<EasAppDto>(
    {
      AppId: EAS_APP_ID,
      AppName: 'Pusula',
      Connected: true,
      Link: {
        ProjectId: 'f1e2d3c4-0000-4000-8000-000000000001',
        AccountName: 'kuzeylab',
        Slug: 'pusula',
        GitHubRepository: 'kuzeylab/pusula',
        DefaultBranch: 'main',
        ProjectUrl: 'https://expo.dev/accounts/kuzeylab/projects/pusula',
        GitHubSettingsUrl: 'https://expo.dev/accounts/kuzeylab/projects/pusula/github',
        SyncedDate: '2026-11-24T08:30:00Z',
        SyncError: null,
        Chain: { AfterBuild: EasAfterBuild.Intake, PrepareReviewPlan: true, SubmitTrack: null, SubmitReleaseStatus: null },
      },
      Builds: [
        easBuild(),
        easBuild({
          Id: 702,
          Platform: EasPlatform.Ios,
          Status: EasBuildStatus.InProgress,
          ArtifactUrl: null,
          FinishedDate: null,
          StartedInStoreasy: false,
          StartedBy: 'cli-user',
        }),
      ],
      Submissions: [easSubmission()],
    },
    overrides,
  )
}

export interface EasState {
  connection: ExpoConnectionDto | null
  app: EasAppDto
  projects: EasProjectDto[]
  profiles: EasProfilesDto
}

function initialEas(): EasState {
  return {
    connection: {
      Id: 9,
      Fingerprint: 'kuzeylab-ci (robot)',
      Status: ConnectionStatus.Verified,
      LastVerifiedDate: '2026-11-24T07:00:00Z',
      LastVerifyError: null,
    },
    app: easApp(),
    projects: [
      {
        Id: 'p-1',
        AccountName: 'kuzeylab',
        Slug: 'pusula',
        Name: 'Pusula',
        GitHubRepository: 'kuzeylab/pusula',
        AppIdentifier: 'com.kuzeylab.pusula',
        Match: 3,
        LinkedAppId: null,
      },
      {
        Id: 'p-2',
        AccountName: 'kuzeylab',
        Slug: 'mola',
        Name: 'Mola',
        GitHubRepository: null,
        AppIdentifier: null,
        Match: 0,
        LinkedAppId: 102,
      },
    ],
    profiles: { Profiles: ['development', 'preview', 'production'], FromRepository: true, Note: null },
  }
}

export const easState: EasState = initialEas()

const json = (data: unknown) => HttpResponse.json(data as Record<string, unknown>)

export const easHandlers: HttpHandler[] = [
  endpoint('get', '/eas/connection', () => json(envelope(easState.connection))),
  endpoint('post', '/eas/connection', () => json(envelope(easState.connection))),
  endpoint('delete', '/eas/connection', () => json(envelope(1))),
  endpoint('get', '/eas/projects', () => json(envelope(easState.projects))),
  endpoint('put', '/eas/project', () => json(envelope(easState.app))),
  endpoint('get', '/eas/app', () => json(envelope(easState.app))),
  endpoint('get', '/eas/profiles', () => json(envelope(easState.profiles))),
  endpoint('post', '/eas/build/start', () => json(envelope([easBuild({ Id: 703, Status: EasBuildStatus.Requested, BuildId: null })]))),
  endpoint('get', '/eas/build', () => json(envelope(easBuild()))),
  endpoint('post', '/eas/build/cancel', () => json(envelope(easBuild({ Id: 702, Status: EasBuildStatus.Canceled })))),
  endpoint('post', '/eas/build/intake', () => json(envelope(easBuild({ ArtifactSessionId: 990 })))),
  endpoint('post', '/eas/sync', () => json(envelope(52300))),
  endpoint('put', '/eas/chain', () => json(envelope(easState.app))),
  endpoint('post', '/eas/submission/start', () =>
    json(envelope(easSubmission({ Id: 802, Status: EasSubmissionStatus.InQueue, FromChain: false, ErrorMessage: null }))),
  ),
  endpoint('get', '/eas/submissions', () => json(envelope(easState.app.Submissions))),
  endpoint('get', '/eas/submission', () => json(envelope(easSubmission()))),
]

/** Resets the EAS data and answers its endpoints for every test of the calling file. */
export function setupEasApi(): void {
  beforeEach(() => {
    Object.assign(easState, initialEas())
    server.use(...easHandlers)
  })
}
