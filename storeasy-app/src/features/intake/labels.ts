import type { BadgeTone } from '@/components/ui/Badge'
import { StoreKind } from '@/lib/api/types'
import type { IntakeDictionary } from './i18n'
import {
  ArtifactPlatform,
  ArtifactSessionStatus,
  ArtifactSource,
  DeliveryTarget,
  OidcExchangeResult,
  OidcProvider,
  type ArtifactSessionListDto,
  type DeliveryRuleDto,
} from './types'

/** IntakeRules.IsFinal: nothing changes on the session any more. */
export function isFinal(status: ArtifactSessionStatus): boolean {
  return (
    status === ArtifactSessionStatus.Delivered ||
    status === ArtifactSessionStatus.Rejected ||
    status === ArtifactSessionStatus.Duplicate ||
    status === ArtifactSessionStatus.Held ||
    status === ArtifactSessionStatus.Failed
  )
}

export function statusView(status: ArtifactSessionStatus, t: IntakeDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case ArtifactSessionStatus.Uploading:
      return { label: t.status.uploading, tone: 'info' }
    case ArtifactSessionStatus.Parsing:
      return { label: t.status.parsing, tone: 'info' }
    case ArtifactSessionStatus.Checking:
      return { label: t.status.checking, tone: 'info' }
    case ArtifactSessionStatus.Delivering:
      return { label: t.status.delivering, tone: 'accent' }
    case ArtifactSessionStatus.Delivered:
      return { label: t.status.delivered, tone: 'success' }
    case ArtifactSessionStatus.Rejected:
      return { label: t.status.rejected, tone: 'danger' }
    case ArtifactSessionStatus.Duplicate:
      return { label: t.status.duplicate, tone: 'neutral' }
    case ArtifactSessionStatus.Held:
      return { label: t.status.held, tone: 'warning' }
    default:
      return { label: t.status.failed, tone: 'danger' }
  }
}

export const SESSION_STATUSES: ArtifactSessionStatus[] = Object.values(ArtifactSessionStatus)

export function sourceLabel(source: ArtifactSource, t: IntakeDictionary): string {
  switch (source) {
    case ArtifactSource.GitHubActions:
      return t.source.github
    case ArtifactSource.AzureDevOps:
      return t.source.azure
    case ArtifactSource.GitLab:
      return t.source.gitlab
    case ArtifactSource.Cli:
      return t.source.cli
    case ArtifactSource.Rest:
      return t.source.rest
    case ArtifactSource.Eas:
      return t.source.eas
    case ArtifactSource.Webhook:
      return t.source.webhook
    case ArtifactSource.Manual:
      return t.source.manual
    default:
      return t.source.sync
  }
}

export const SOURCES: ArtifactSource[] = Object.values(ArtifactSource)

/** "GitHub Actions #318 · attempt 2" or "Manual · Deniz Arslan". */
export function sourceText(session: ArtifactSessionListDto, t: IntakeDictionary): string {
  const ref = session.SourceRef
  const run = ref?.RunId ? `#${ref.RunId}` : null
  const attempt = ref?.RunAttempt && ref.RunAttempt !== '1' ? t.sessions.attempt.replace('{n}', ref.RunAttempt) : null
  const actor = !run && ref?.Actor ? ref.Actor : null
  return [[sourceLabel(session.Source, t), run].filter(Boolean).join(' '), attempt, actor].filter(Boolean).join(' · ')
}

export function platformLabel(platform: ArtifactPlatform, t: IntakeDictionary): string {
  return platform === ArtifactPlatform.Ios
    ? t.platform.ios
    : platform === ArtifactPlatform.Android
      ? t.platform.android
      : platform === ArtifactPlatform.Extension
        ? t.platform.extension
        : t.platform.windows
}

export const PLATFORMS: ArtifactPlatform[] = [
  ArtifactPlatform.Ios,
  ArtifactPlatform.Android,
  ArtifactPlatform.Windows,
  ArtifactPlatform.Extension,
]

/** IntakeRules.StoreOf. */
export function storeOf(platform: ArtifactPlatform): StoreKind {
  return platform === ArtifactPlatform.Ios
    ? StoreKind.Apple
    : platform === ArtifactPlatform.Android
      ? StoreKind.Google
      : platform === ArtifactPlatform.Extension
        ? StoreKind.Chrome
        : StoreKind.Microsoft
}

/** The target kind a platform's rule uses. */
export function targetOf(platform: ArtifactPlatform): DeliveryTarget {
  return platform === ArtifactPlatform.Ios
    ? DeliveryTarget.TestFlightGroup
    : platform === ArtifactPlatform.Android
      ? DeliveryTarget.PlayTrack
      : platform === ArtifactPlatform.Extension
        ? DeliveryTarget.None
        : DeliveryTarget.MsFlight
}

/** "iOS → TestFlight Kuzey içi", "Android → internal". */
export function ruleText(rule: DeliveryRuleDto, t: IntakeDictionary): string {
  if (!rule.Enabled || rule.Target === DeliveryTarget.None) {
    return `${platformLabel(rule.Platform, t)} → ${t.rules.none}`
  }
  const name = rule.TargetName || rule.TargetId || ''
  const prefix = rule.Target === DeliveryTarget.TestFlightGroup ? 'TestFlight ' : ''
  const rollout = rule.RolloutFraction ? ` · %${Math.round(rule.RolloutFraction * 100)}` : ''
  return `${platformLabel(rule.Platform, t)} → ${prefix}${name}${rollout}`
}

export function providerLabel(provider: OidcProvider, t: IntakeDictionary): string {
  return provider === OidcProvider.GitHub
    ? t.source.github.replace(' Actions', '')
    : provider === OidcProvider.AzureDevOps
      ? t.source.azure
      : t.source.gitlab
}

export function exchangeView(result: OidcExchangeResult, t: IntakeDictionary): { label: string; tone: BadgeTone } {
  switch (result) {
    case OidcExchangeResult.Succeeded:
      return { label: t.publishers.result.succeeded, tone: 'success' }
    case OidcExchangeResult.ClaimMismatch:
      return { label: t.publishers.result.claimMismatch, tone: 'danger' }
    default:
      return { label: t.publishers.result.invalidToken, tone: 'danger' }
  }
}

/** The package extensions IntakeRules.Classify accepts. */
export const PACKAGE_EXTENSIONS = ['.ipa', '.aab', '.apk', '.msix', '.msixbundle', '.appx', '.appxbundle', '.msixupload', '.appxupload']

export function isPackageFile(name: string): boolean {
  const lower = name.toLowerCase()
  return PACKAGE_EXTENSIONS.some((extension) => lower.endsWith(extension))
}
