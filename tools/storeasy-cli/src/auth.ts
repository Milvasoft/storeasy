// SPDX-License-Identifier: AGPL-3.0-only
import { type Env, readConfig } from './config.js'
import { maskSecret } from './ci.js'
import { ApiClient, type FetchLike, type RetryPolicy } from './http.js'
import type { Logger } from './log.js'
import { detectOidc, oidcTokenSource } from './oidc.js'
import { tokenHint } from './redact.js'

export type AuthKind = 'input' | 'env' | 'oidc' | 'saved' | 'none'

export interface ResolveAuthOptions {
  env: Env
  baseUrl: string
  fetch?: FetchLike
  log?: Logger
  client?: string
  retry?: Partial<RetryPolicy>
  sleep?: (ms: number) => Promise<void>
  /** A token passed in directly (action or task input). Highest precedence. */
  token?: string
  /** App id; OIDC exchange is per app, so OIDC is used only when it is known. */
  appId?: number
  /** Allow OIDC (default true). */
  oidc?: boolean
  serviceConnectionId?: string
  azureAccessToken?: string
}

export interface ResolvedAuth {
  api: ApiClient
  kind: AuthKind
  /** Safe description for messages, e.g. "STOREASY_TOKEN (sci_7f2...)". */
  describe: string
}

/**
 * Auth precedence (tools/README.md):
 *   1. a token given to the action or task as input,
 *   2. STOREASY_TOKEN (a CI token `sci_` or a PAT `sea_`),
 *   3. OIDC in CI (GitHub Actions, Azure Pipelines, STOREASY_OIDC_TOKEN) exchanged for a 15 minute upload token,
 *   4. the PAT saved by `storeasy login`.
 */
export function resolveAuth(opts: ResolveAuthOptions): ResolvedAuth {
  const { env } = opts
  const api = new ApiClient({ baseUrl: opts.baseUrl, fetch: opts.fetch, client: opts.client, retry: opts.retry, sleep: opts.sleep })
  if (opts.token) {
    maskSecret(opts.token, env, opts.log)
    api.setToken(opts.token)
    return { api, kind: 'input', describe: `token input (${tokenHint(opts.token)})` }
  }
  if (env.STOREASY_TOKEN) {
    maskSecret(env.STOREASY_TOKEN, env)
    api.setToken(env.STOREASY_TOKEN)
    return { api, kind: 'env', describe: `STOREASY_TOKEN (${tokenHint(env.STOREASY_TOKEN)})` }
  }
  if (opts.oidc !== false && opts.appId !== undefined) {
    const source = detectOidc({
      env,
      fetch: api.fetch,
      log: opts.log,
      serviceConnectionId: opts.serviceConnectionId,
      azureAccessToken: opts.azureAccessToken,
    })
    if (source) {
      // The exchange itself is anonymous; a separate client keeps it from sending a stale token.
      const anon = new ApiClient({ baseUrl: opts.baseUrl, fetch: api.fetch, client: opts.client, retry: opts.retry, sleep: opts.sleep })
      api.setToken(oidcTokenSource(anon, opts.appId, source, env, opts.log))
      return { api, kind: 'oidc', describe: 'OIDC trusted publisher' }
    }
  }
  const saved = readConfig(env).token
  if (saved) {
    api.setToken(saved)
    return { api, kind: 'saved', describe: `saved login (${tokenHint(saved)})` }
  }
  return { api, kind: 'none', describe: 'no credentials' }
}
