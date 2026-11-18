// SPDX-License-Identifier: AGPL-3.0-only
import type { Env } from './config.js'
import { maskSecret } from './ci.js'
import { OidcProvider } from './enums.js'
import { ExitCode, StoreasyError } from './errors.js'
import { type ApiClient, type FetchLike, readJson } from './http.js'
import type { Logger } from './log.js'
import { stripQuery } from './redact.js'

/** Audience of the OIDC token the CI is asked for (plan 11.8). */
export const OIDC_AUDIENCE = 'storeasy'

export interface OidcSource {
  provider: OidcProvider
  /** Fetches a fresh OIDC token from the CI. */
  getToken(): Promise<string>
}

export interface OidcOptions {
  env: Env
  fetch: FetchLike
  log?: Logger
  /** Azure DevOps service connection id (task input or STOREASY_SERVICE_CONNECTION_ID). */
  serviceConnectionId?: string
  /** Azure DevOps System.AccessToken; read from the environment when not given. */
  azureAccessToken?: string
}

/**
 * Finds an OIDC token source in the environment:
 *  - STOREASY_OIDC_TOKEN: a token the pipeline already holds (GitLab `id_tokens`, any other CI);
 *  - GitHub Actions with `permissions: id-token: write` (ACTIONS_ID_TOKEN_REQUEST_URL);
 *  - Azure Pipelines (SYSTEM_OIDCREQUESTURI) with a service connection id and System.AccessToken.
 */
export function detectOidc(opts: OidcOptions): OidcSource | undefined {
  const { env } = opts
  if (env.STOREASY_OIDC_TOKEN) {
    const token = env.STOREASY_OIDC_TOKEN
    maskSecret(token, env, opts.log)
    const provider = env.GITHUB_ACTIONS === 'true' ? OidcProvider.GitHub : env.TF_BUILD ? OidcProvider.AzureDevOps : OidcProvider.GitLab
    return { provider, getToken: async () => token }
  }
  if (env.ACTIONS_ID_TOKEN_REQUEST_URL && env.ACTIONS_ID_TOKEN_REQUEST_TOKEN) {
    return { provider: OidcProvider.GitHub, getToken: () => requestGithubToken(opts) }
  }
  const scId = opts.serviceConnectionId ?? env.STOREASY_SERVICE_CONNECTION_ID
  const access = opts.azureAccessToken ?? env.SYSTEM_ACCESSTOKEN ?? env.ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN
  if (env.SYSTEM_OIDCREQUESTURI && scId && access) {
    return {
      provider: OidcProvider.AzureDevOps,
      getToken: () => requestAzureToken({ ...opts, serviceConnectionId: scId, azureAccessToken: access }),
    }
  }
  return undefined
}

/** GitHub Actions: GET $ACTIONS_ID_TOKEN_REQUEST_URL&audience=storeasy with the request token. */
export async function requestGithubToken(opts: OidcOptions): Promise<string> {
  const { env } = opts
  const base = env.ACTIONS_ID_TOKEN_REQUEST_URL!
  const requestToken = env.ACTIONS_ID_TOKEN_REQUEST_TOKEN!
  maskSecret(requestToken, env)
  const url = `${base}${base.includes('?') ? '&' : '?'}audience=${encodeURIComponent(OIDC_AUDIENCE)}`
  const res = await opts.fetch(url, { headers: { Authorization: `Bearer ${requestToken}`, Accept: 'application/json' } })
  const body = (await readJson(res)) as { value?: string } | undefined
  if (!res.ok || !body?.value) {
    throw new StoreasyError(
      `Could not get an OIDC token from GitHub Actions (${res.status}). The job needs "permissions: id-token: write".`,
      { exitCode: ExitCode.Auth, status: res.status },
    )
  }
  maskSecret(body.value, env, opts.log)
  return body.value
}

/**
 * Azure Pipelines: POST $SYSTEM_OIDCREQUESTURI?api-version=7.1&serviceConnectionId=<id> with System.AccessToken.
 * The token's audience is fixed by Azure DevOps (api://AzureADTokenExchange); its subject names the service
 * connection (sc://org/project/name), which the trusted publisher rule matches (design 13.6).
 */
export async function requestAzureToken(opts: OidcOptions & { serviceConnectionId: string; azureAccessToken: string }): Promise<string> {
  const { env } = opts
  maskSecret(opts.azureAccessToken, env)
  const u = new URL(env.SYSTEM_OIDCREQUESTURI!)
  u.searchParams.set('api-version', '7.1')
  u.searchParams.set('serviceConnectionId', opts.serviceConnectionId)
  const res = await opts.fetch(u.toString(), {
    method: 'POST',
    headers: { Authorization: `Bearer ${opts.azureAccessToken}`, 'Content-Type': 'application/json', Accept: 'application/json' },
    body: '{}',
  })
  const body = (await readJson(res)) as { oidcToken?: string } | undefined
  if (!res.ok || !body?.oidcToken) {
    throw new StoreasyError(
      `Could not get an OIDC token from Azure Pipelines (${res.status}, ${stripQuery(u.toString())}). Check the service connection and that the job can use System.AccessToken.`,
      { exitCode: ExitCode.Auth, status: res.status },
    )
  }
  maskSecret(body.oidcToken, env, opts.log)
  return body.oidcToken
}

export interface UploadToken {
  uploadToken: string
  expiryDate: string
  appId?: number
}

/** POST /api/v1.0/oidc/exchange (CONTRACT 3.1): OIDC token to a 15 minute upload token for one app. */
export async function exchangeOidc(api: ApiClient, appId: number, token: string, provider: OidcProvider): Promise<UploadToken> {
  try {
    return await api.request<UploadToken>('POST', '/api/v1.0/oidc/exchange', {
      body: { appId, token, provider },
      auth: false,
      retry: true,
    })
  } catch (err) {
    if (err instanceof StoreasyError && err.hasKey('OidcClaimMismatch')) {
      throw new StoreasyError(
        "The OIDC token did not match this app's trusted publisher rules (403). Details: storeasy > Intake > Trusted publishers.",
        { exitCode: ExitCode.Auth, status: 403, keys: err.keys },
      )
    }
    if (err instanceof StoreasyError && (err.status === 401 || err.hasKey('OidcTokenInvalid'))) {
      throw new StoreasyError(`storeasy refused the OIDC token: ${err.message}`, { exitCode: ExitCode.Auth, status: err.status, keys: err.keys })
    }
    throw err
  }
}

/**
 * A token source for ApiClient that exchanges the CI's OIDC token and renews the upload token a minute before it
 * expires, so uploads and waits longer than 15 minutes keep working.
 */
export function oidcTokenSource(api: ApiClient, appId: number, source: OidcSource, env: Env, log?: Logger, now: () => number = Date.now) {
  let current: { token: string; expires: number } | undefined
  let pending: Promise<string> | undefined
  const renew = async () => {
    const oidc = await source.getToken()
    const up = await exchangeOidc(api, appId, oidc, source.provider)
    maskSecret(up.uploadToken, env, log)
    const exp = Date.parse(up.expiryDate)
    current = { token: up.uploadToken, expires: Number.isFinite(exp) ? exp : now() + 15 * 60_000 }
    return up.uploadToken
  }
  return async (): Promise<string> => {
    if (current && current.expires - now() > 60_000) {
      return current.token
    }
    pending ??= renew().finally(() => {
      pending = undefined
    })
    return pending
  }
}
