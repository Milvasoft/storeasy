import type { BadgeTone } from '@/components/ui/Badge'
import { WingetPullRequestState, type WingetInstallerInputDto, type WingetInstallerSourceDto } from './types'

// The winget rules the screens check before asking the API (the API checks them again): the credential's shape, the
// package identifier, and what each review state means. Pure, unit tested.

// GitHub personal access tokens: classic (ghp_) or fine-grained (github_pat_), as WingetCredentialParser accepts.
export const TOKEN_PATTERN = /^(ghp_[A-Za-z0-9]{30,250}|github_pat_[A-Za-z0-9_]{30,250})$/

// owner/name of the fork: GitHub user or organization names and repository names.
export const FORK_PATTERN = /^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}\/[A-Za-z0-9._-]{1,100}$/

// PackageIdentifier of the manifest schema: 2 to 8 dot separated parts of up to 32 characters (no control characters,
// as the schema says).
export const PACKAGE_ID_PATTERN = /^[^.\s\\/:*?"<>|\p{Cc}]{1,32}(\.[^.\s\\/:*?"<>|\p{Cc}]{1,32}){1,7}$/u

export interface WingetCredentialFields {
  token: string
  fork: string
}

export const EMPTY_WINGET_CREDENTIALS: WingetCredentialFields = { token: '', fork: '' }

/** A fork given as a GitHub address reduced to owner/name. */
export function normalizeFork(value: string): string {
  let fork = value.trim().replace(/\/+$/, '')
  fork = fork.replace(/^(https?:\/\/)?github\.com\//i, '')
  return fork.replace(/\.git$/i, '')
}

export function credentialProblems(fields: WingetCredentialFields): Array<'token' | 'fork'> {
  const problems: Array<'token' | 'fork'> = []
  if (!TOKEN_PATTERN.test(fields.token.trim())) {
    problems.push('token')
  }
  if (!FORK_PATTERN.test(normalizeFork(fields.fork))) {
    problems.push('fork')
  }
  return problems
}

/** The secret JSON the API expects ({"token","fork"}). */
export function wingetSecret(fields: WingetCredentialFields): string {
  return JSON.stringify({ token: fields.token.trim(), fork: normalizeFork(fields.fork) })
}

export function tokenKind(token: string): 'classic' | 'fine-grained' {
  return token.trim().startsWith('github_pat_') ? 'fine-grained' : 'classic'
}

export function isPackageIdentifier(value: string): boolean {
  return value.trim().length <= 128 && PACKAGE_ID_PATTERN.test(value.trim())
}

/** Whether the review state waits for the author. */
export function needsAction(state: WingetPullRequestState): boolean {
  return state === WingetPullRequestState.ValidationFailed || state === WingetPullRequestState.NeedsAuthorFeedback
}

export function stateTone(state: WingetPullRequestState): BadgeTone {
  switch (state) {
    case WingetPullRequestState.Merged:
      return 'success'
    case WingetPullRequestState.Approved:
    case WingetPullRequestState.ValidationCompleted:
      return 'info'
    case WingetPullRequestState.ValidationFailed:
    case WingetPullRequestState.NeedsAuthorFeedback:
      return 'danger'
    case WingetPullRequestState.Validating:
      return 'accent'
    default:
      return 'neutral'
  }
}

const STATE_KEYS = {
  [WingetPullRequestState.Validating]: 'validating',
  [WingetPullRequestState.ValidationFailed]: 'validationFailed',
  [WingetPullRequestState.NeedsAuthorFeedback]: 'needsAuthorFeedback',
  [WingetPullRequestState.ValidationCompleted]: 'validationCompleted',
  [WingetPullRequestState.Approved]: 'approved',
  [WingetPullRequestState.Merged]: 'merged',
  [WingetPullRequestState.Closed]: 'closed',
  [WingetPullRequestState.Draft]: 'draft',
} as const satisfies Record<WingetPullRequestState, string>

export type WingetStateKey = (typeof STATE_KEYS)[WingetPullRequestState]

export function stateKey(state: WingetPullRequestState): WingetStateKey {
  return STATE_KEYS[state] ?? 'validating'
}

/** One installer per architecture of the app's Microsoft Store MSI/EXE packages, as the API builds them. */
export function installersFromSources(sources: WingetInstallerSourceDto[]): WingetInstallerInputDto[] {
  return sources.flatMap((source) =>
    (source.Architectures.length > 0 ? source.Architectures : ['x64']).map((architecture) => ({
      Url: source.Url,
      Architecture: architecture,
      Type: source.Type,
      SilentSwitches: source.SilentSwitches ?? null,
      Locale: source.Locales.length === 1 ? source.Locales[0] : null,
    })),
  )
}

export const ARCHITECTURES = ['x64', 'x86', 'arm64', 'arm', 'neutral'] as const
export const INSTALLER_TYPES = ['exe', 'msi', 'msix', 'inno', 'nullsoft', 'wix', 'burn', 'zip', 'portable', 'appx'] as const
export const SCOPES = ['', 'user', 'machine'] as const

// Fields of a locale manifest storeasy edits, with the schema's maximum length.
export const LISTING_FIELDS = [
  { name: 'PackageName', max: 256 },
  { name: 'Publisher', max: 256 },
  { name: 'ShortDescription', max: 256 },
  { name: 'Description', max: 10000, multiline: true },
  { name: 'License', max: 512 },
  { name: 'Author', max: 256 },
  { name: 'Copyright', max: 512 },
  { name: 'ReleaseNotes', max: 10000, multiline: true },
  { name: 'PublisherUrl', max: 2048 },
  { name: 'PackageUrl', max: 2048 },
  { name: 'LicenseUrl', max: 2048 },
  { name: 'PrivacyUrl', max: 2048 },
  { name: 'ReleaseNotesUrl', max: 2048 },
] as const
