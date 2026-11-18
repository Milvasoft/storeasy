// SPDX-License-Identifier: AGPL-3.0-only
import type { Env } from './config.js'
import { ArtifactSource } from './enums.js'
import type { Logger } from './log.js'
import { registerSecret } from './redact.js'

export type CiKind = 'github' | 'azure' | 'gitlab'

export interface SourceRef {
  provider?: string
  runId?: string
  runAttempt?: string
  runUrl?: string
  repository?: string
  branch?: string
  commit?: string
  actor?: string
}

export function detectCi(env: Env): CiKind | undefined {
  if (env.GITHUB_ACTIONS === 'true') {
    return 'github'
  }
  if ((env.TF_BUILD ?? '').toLowerCase() === 'true') {
    return 'azure'
  }
  if (env.GITLAB_CI === 'true') {
    return 'gitlab'
  }
  return undefined
}

const clean = (o: SourceRef): SourceRef | undefined => {
  const entries = Object.entries(o).filter(([, v]) => v !== undefined && v !== '')
  return entries.length ? (Object.fromEntries(entries) as SourceRef) : undefined
}

/** `source` and `sourceRef` of the session open request (CONTRACT 3.2), from the CI's own variables. */
export function detectSource(env: Env): { source: ArtifactSource; sourceRef?: SourceRef } {
  switch (detectCi(env)) {
    case 'github': {
      const server = env.GITHUB_SERVER_URL ?? 'https://github.com'
      return {
        source: ArtifactSource.GitHubActions,
        sourceRef: clean({
          provider: 'github',
          runId: env.GITHUB_RUN_ID,
          runAttempt: env.GITHUB_RUN_ATTEMPT,
          runUrl: env.GITHUB_REPOSITORY && env.GITHUB_RUN_ID ? `${server}/${env.GITHUB_REPOSITORY}/actions/runs/${env.GITHUB_RUN_ID}` : undefined,
          repository: env.GITHUB_REPOSITORY,
          branch: env.GITHUB_HEAD_REF || env.GITHUB_REF_NAME,
          commit: env.GITHUB_SHA,
          actor: env.GITHUB_ACTOR,
        }),
      }
    }
    case 'azure': {
      const collection = (env.SYSTEM_COLLECTIONURI ?? '').replace(/\/+$/, '')
      const project = env.SYSTEM_TEAMPROJECT
      return {
        source: ArtifactSource.AzureDevOps,
        sourceRef: clean({
          provider: 'azuredevops',
          runId: env.BUILD_BUILDNUMBER ?? env.BUILD_BUILDID,
          runAttempt: env.SYSTEM_JOBATTEMPT,
          runUrl:
            collection && project && env.BUILD_BUILDID
              ? `${collection}/${encodeURIComponent(project)}/_build/results?buildId=${env.BUILD_BUILDID}`
              : undefined,
          repository: env.BUILD_REPOSITORY_NAME,
          branch: env.BUILD_SOURCEBRANCHNAME ?? env.BUILD_SOURCEBRANCH,
          commit: env.BUILD_SOURCEVERSION,
          actor: env.BUILD_REQUESTEDFOR,
        }),
      }
    }
    case 'gitlab':
      return {
        source: ArtifactSource.GitLab,
        sourceRef: clean({
          provider: 'gitlab',
          runId: env.CI_PIPELINE_ID,
          runUrl: env.CI_PIPELINE_URL,
          repository: env.CI_PROJECT_PATH,
          branch: env.CI_COMMIT_REF_NAME,
          commit: env.CI_COMMIT_SHA,
          actor: env.GITLAB_USER_LOGIN,
        }),
      }
    default:
      return { source: ArtifactSource.Cli }
  }
}

/**
 * Registers a secret with the redaction and, on a CI runner, with the runner's own log masking, so a value that
 * reaches the log through another tool is hidden too.
 */
export function maskSecret(value: string, env: Env, log?: Logger): void {
  registerSecret(value)
  if (!log) {
    return
  }
  const ci = detectCi(env)
  if (ci === 'github') {
    log.command(`::add-mask::${value}`)
  } else if (ci === 'azure') {
    log.command(`##vso[task.setsecret]${value}`)
  }
}
