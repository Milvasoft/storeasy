import { OidcProvider, type CreateTrustedPublisherInput } from './types'

// The client copy of Storeasy.Domain.Intake TrustedPublisherMatcher.ExpectedClaims: what a matching CI token carries,
// shown while the rule is typed. The server computes the same list and is the one that decides.

export const GITHUB_ISSUER = 'https://token.actions.githubusercontent.com'
export const AZURE_ISSUER = 'https://vstoken.dev.azure.com/*'
export const GITLAB_ISSUER = 'https://gitlab.com'
export const AZURE_AUDIENCE = 'api://AzureADTokenExchange'
export const STOREASY_AUDIENCE = 'storeasy'

export function audienceOf(provider: OidcProvider): string {
  return provider === OidcProvider.AzureDevOps ? AZURE_AUDIENCE : STOREASY_AUDIENCE
}

export function expectedClaims(rule: CreateTrustedPublisherInput): Array<[string, string]> {
  const value = (text?: string | null) => (text ?? '').trim()
  const issuer =
    rule.Provider === OidcProvider.GitHub
      ? GITHUB_ISSUER
      : rule.Provider === OidcProvider.AzureDevOps
        ? AZURE_ISSUER
        : value(rule.Issuer).replace(/\/+$/, '') || GITLAB_ISSUER
  const claims: Array<[string, string]> = [
    ['iss', issuer],
    ['aud', audienceOf(rule.Provider)],
  ]
  if (rule.Provider === OidcProvider.GitHub) {
    claims.push(['repository', value(rule.Repository)])
    if (value(rule.Branch)) {
      claims.push(['ref', `refs/heads/${value(rule.Branch)}`])
    }
    if (value(rule.Environment)) {
      claims.push(['environment', value(rule.Environment)])
    }
    if (value(rule.Workflow)) {
      claims.push(['job_workflow_ref', `${value(rule.Repository)}/${value(rule.Workflow).replace(/^\/+/, '')}@*`])
    }
  } else if (rule.Provider === OidcProvider.AzureDevOps) {
    claims.push(['sub', `sc://${value(rule.Organization)}/${value(rule.Project)}/${value(rule.ServiceConnection)}`])
  } else {
    claims.push(['project_path', value(rule.Repository)])
    if (value(rule.Branch)) {
      claims.push(['ref', value(rule.Branch)])
      claims.push(['ref_type', 'branch'])
    }
    if (value(rule.Environment)) {
      claims.push(['environment', value(rule.Environment)])
    }
  }
  return claims
}

/**
 * The payload of a pasted JWT, decoded in the browser for a preview only (no signature check, nothing sent). Null when
 * the text is not three base64url parts with a JSON object in the middle.
 */
export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const parts = token.trim().split('.')
  if (parts.length !== 3 || !parts[1]) {
    return null
  }
  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4)
    const bytes = Uint8Array.from(atob(padded), (char) => char.charCodeAt(0))
    const payload: unknown = JSON.parse(new TextDecoder().decode(bytes))
    return payload && typeof payload === 'object' && !Array.isArray(payload) ? (payload as Record<string, unknown>) : null
  } catch {
    return null
  }
}

export function claimText(value: unknown): string {
  if (typeof value === 'string') {
    return value
  }
  return JSON.stringify(value)
}
