import type { BadgeTone } from '@/components/ui/Badge'
import type { RegistriesDictionary } from './i18n/en'
import {
  CiProvider,
  PackageAlertKind,
  PackageRegistry,
  PublisherCheckStatus,
  RegistryConnectionKind,
  RegistryPlanKind,
  ReleaseFollowStatus,
  ReleaseTrigger,
  VersionFileFormat,
  type ReleaseSettings,
} from './types'

// The registries' name rules (the API's RegistryNames checks the same) and the labels of the registry enums.

const NAME_RULES: Record<PackageRegistry, { pattern: RegExp; max: number }> = {
  [PackageRegistry.Npm]: { pattern: /^(?:@[a-z0-9~-][a-z0-9._~-]*\/)?[a-z0-9~-][a-z0-9._~-]*$/, max: 214 },
  [PackageRegistry.NuGet]: { pattern: /^[A-Za-z0-9_]+(?:[.-][A-Za-z0-9_]+)*$/, max: 100 },
  [PackageRegistry.PyPI]: { pattern: /^(?:[A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9._-]*[A-Za-z0-9])$/, max: 214 },
  [PackageRegistry.Crates]: { pattern: /^[A-Za-z][A-Za-z0-9_-]*$/, max: 64 },
  [PackageRegistry.Maven]: { pattern: /^[A-Za-z0-9_.-]+:[A-Za-z0-9_.-]+$/, max: 214 },
  [PackageRegistry.RubyGems]: { pattern: /^[A-Za-z0-9][A-Za-z0-9._-]*$/, max: 128 },
  [PackageRegistry.Packagist]: { pattern: /^[a-z0-9]([_.-]?[a-z0-9]+)*\/[a-z0-9](([_.]|-{1,2})?[a-z0-9]+)*$/, max: 214 },
  [PackageRegistry.Pub]: { pattern: /^[a-z_][a-z0-9_]*$/, max: 64 },
}

export const REGISTRIES: readonly PackageRegistry[] = [
  PackageRegistry.Npm,
  PackageRegistry.NuGet,
  PackageRegistry.PyPI,
  PackageRegistry.Crates,
  PackageRegistry.Maven,
  PackageRegistry.RubyGems,
  PackageRegistry.Packagist,
  PackageRegistry.Pub,
]

/** Whether `name` is a valid package name on `registry`. */
export function isPackageName(registry: PackageRegistry, name: string): boolean {
  const value = name.trim()
  const rule = NAME_RULES[registry]
  return value.length > 0 && value.length <= rule.max && rule.pattern.test(value)
}

export function registryLabel(registry: PackageRegistry): string {
  switch (registry) {
    case PackageRegistry.Npm:
      return 'npm'
    case PackageRegistry.NuGet:
      return 'NuGet'
    case PackageRegistry.PyPI:
      return 'PyPI'
    case PackageRegistry.Maven:
      return 'Maven Central'
    case PackageRegistry.RubyGems:
      return 'RubyGems'
    case PackageRegistry.Packagist:
      return 'Packagist'
    case PackageRegistry.Pub:
      return 'pub.dev'
    default:
      return 'crates.io'
  }
}

export function alertKindLabel(kind: PackageAlertKind, t: RegistriesDictionary): string {
  switch (kind) {
    case PackageAlertKind.Deprecated:
      return t.alertKind.deprecated
    case PackageAlertKind.Yanked:
      return t.alertKind.yanked
    case PackageAlertKind.NoProvenance:
      return t.alertKind.noProvenance
    case PackageAlertKind.PublisherMismatch:
      return t.alertKind.publisherMismatch
    case PackageAlertKind.TokenExpiring:
      return t.alertKind.tokenExpiring
    default:
      return t.alertKind.advisory
  }
}

export function alertTone(kind: PackageAlertKind, severity?: string | null): BadgeTone {
  if (kind === PackageAlertKind.Advisory) {
    return severity === 'LOW' || severity === 'MODERATE' ? 'warning' : 'danger'
  }
  if (kind === PackageAlertKind.PublisherMismatch) {
    return 'danger'
  }
  return kind === PackageAlertKind.NoProvenance ? 'info' : 'warning'
}

export function providerLabel(provider: CiProvider): string {
  return provider === CiProvider.GitLabCi ? 'GitLab CI/CD' : 'GitHub Actions'
}

export function checkTone(status: PublisherCheckStatus): BadgeTone {
  switch (status) {
    case PublisherCheckStatus.Match:
      return 'success'
    case PublisherCheckStatus.Mismatch:
      return 'danger'
    case PublisherCheckStatus.NoProvenance:
      return 'warning'
    default:
      return 'neutral'
  }
}

export function checkLabel(status: PublisherCheckStatus, t: RegistriesDictionary): string {
  switch (status) {
    case PublisherCheckStatus.Match:
      return t.check.match
    case PublisherCheckStatus.Mismatch:
      return t.check.mismatch
    case PublisherCheckStatus.NoProvenance:
      return t.check.noProvenance
    case PublisherCheckStatus.NotVerifiable:
      return t.check.notVerifiable
    default:
      return t.check.notConfigured
  }
}

// The API's TrustedPublishing.IsRepository and IsWorkflow.
export function isSetupRepository(provider: CiProvider, value: string): boolean {
  const pattern = provider === CiProvider.GitLabCi ? /^[A-Za-z0-9_.-]+(\/[A-Za-z0-9_.-]+)+$/ : /^[A-Za-z0-9-]+\/[A-Za-z0-9._-]+$/
  return value.trim().length <= 200 && pattern.test(value.trim())
}

export function isSetupWorkflow(provider: CiProvider, value: string): boolean {
  const pattern = provider === CiProvider.GitLabCi ? /^[A-Za-z0-9._-]+(\/[A-Za-z0-9._-]+)*\.ya?ml$/ : /^[A-Za-z0-9._-]+\.ya?ml$/
  const trimmed = value.trim()
  return trimmed.length <= 200 && !trimmed.includes('..') && pattern.test(trimmed)
}

// The API's TokenReminderRules.LooksLikeToken: storeasy never takes a registry token.
const KNOWN_TOKEN =
  /(^|[\s:=])(npm_[A-Za-z0-9]{16,}|pypi-[A-Za-z0-9_-]{16,}|oy2[a-z0-9]{20,}|cio[A-Za-z0-9]{20,}|rubygems_[a-f0-9]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|glpat-[A-Za-z0-9_-]{16,})/i

export function looksLikeToken(text: string): boolean {
  if (!text.trim()) {
    return false
  }
  if (KNOWN_TOKEN.test(text)) {
    return true
  }
  return (text.match(/[A-Za-z0-9+=_-]{32,}/g) ?? []).some((run) => /[0-9]/.test(run) && /[A-Za-z]/.test(run))
}

// Releases and registry operations.

const camel = (name: string) => name.charAt(0).toLowerCase() + name.slice(1)

function nameOf<T extends Record<string, number>>(values: T, value: number): string {
  return camel(Object.keys(values).find((key) => values[key] === value) ?? '')
}

export function triggerLabel(trigger: ReleaseTrigger, t: RegistriesDictionary): string {
  return t.release.triggerKind[nameOf(ReleaseTrigger, trigger) as keyof RegistriesDictionary['release']['triggerKind']]
}

export function triggerHint(trigger: ReleaseTrigger, t: RegistriesDictionary): string {
  return t.release.triggerHint[nameOf(ReleaseTrigger, trigger) as keyof RegistriesDictionary['release']['triggerHint']]
}

export function formatLabel(format: VersionFileFormat, t: RegistriesDictionary): string {
  return t.release.formatKind[nameOf(VersionFileFormat, format) as keyof RegistriesDictionary['release']['formatKind']]
}

export function planKindLabel(kind: RegistryPlanKind, t: RegistriesDictionary): string {
  return t.operations.kind[nameOf(RegistryPlanKind, kind) as keyof RegistriesDictionary['operations']['kind']]
}

export function followLabel(status: ReleaseFollowStatus, t: RegistriesDictionary): string {
  return t.plan.follow[nameOf(ReleaseFollowStatus, status) as keyof RegistriesDictionary['plan']['follow']]
}

export function followTone(status: ReleaseFollowStatus): BadgeTone {
  if (status === ReleaseFollowStatus.Published) {
    return 'success'
  }
  return status === ReleaseFollowStatus.Following ? 'info' : 'danger'
}

export function connectionKindLabel(kind: RegistryConnectionKind, t: RegistriesDictionary): string {
  return t.credentials.kindLabel[nameOf(RegistryConnectionKind, kind) as keyof RegistriesDictionary['credentials']['kindLabel']]
}

export function connectionKindHint(kind: RegistryConnectionKind, t: RegistriesDictionary): string {
  return t.credentials.kindHint[nameOf(RegistryConnectionKind, kind) as keyof RegistriesDictionary['credentials']['kindHint']]
}

export const CONNECTION_KINDS: readonly RegistryConnectionKind[] = [
  RegistryConnectionKind.GitHub,
  RegistryConnectionKind.NuGet,
  RegistryConnectionKind.Npm,
  RegistryConnectionKind.Crates,
  RegistryConnectionKind.RubyGems,
]

// The API's RegistrySecrets.Supports: the registry operations with an official API.
const OPERATIONS: Partial<Record<PackageRegistry, RegistryPlanKind[]>> = {
  [PackageRegistry.NuGet]: [RegistryPlanKind.Unlist, RegistryPlanKind.Relist],
  [PackageRegistry.Npm]: [RegistryPlanKind.Deprecate, RegistryPlanKind.Undeprecate, RegistryPlanKind.DistTag],
  [PackageRegistry.Crates]: [RegistryPlanKind.Yank, RegistryPlanKind.Unyank],
  [PackageRegistry.RubyGems]: [RegistryPlanKind.Yank],
}

export function operationsOf(registry: PackageRegistry): RegistryPlanKind[] {
  return OPERATIONS[registry] ?? []
}

// The key a registry's operations need (RegistrySecrets.KindOf).
export function keyKindOf(registry: PackageRegistry): RegistryConnectionKind | null {
  const kinds: Partial<Record<PackageRegistry, RegistryConnectionKind>> = {
    [PackageRegistry.NuGet]: RegistryConnectionKind.NuGet,
    [PackageRegistry.Npm]: RegistryConnectionKind.Npm,
    [PackageRegistry.Crates]: RegistryConnectionKind.Crates,
    [PackageRegistry.RubyGems]: RegistryConnectionKind.RubyGems,
  }
  return kinds[registry] ?? null
}

export function needsArgument(kind: RegistryPlanKind): boolean {
  return kind === RegistryPlanKind.Deprecate || kind === RegistryPlanKind.DistTag
}

// The API's ChangePlanRules.ConfirmationMatches: the text typed exactly, ignoring surrounding spaces.
export function confirmationMatches(expected: string, typed: string): boolean {
  return typed.trim() === expected.trim()
}

export const EMPTY_RELEASE: ReleaseSettings = {
  Repository: '',
  Branch: 'master',
  Trigger: ReleaseTrigger.Tag,
  VersionFiles: [],
  TagPattern: 'v{version}',
  Workflow: '',
  DispatchVersionInput: '',
  DispatchPrereleaseInput: '',
  DispatchTakesTag: false,
}

// The settings as the API takes them: blanks become null, unused fields are dropped.
export function cleanRelease(settings: ReleaseSettings): ReleaseSettings {
  const bumps = settings.Trigger === ReleaseTrigger.VersionBump || settings.Trigger === ReleaseTrigger.VersionBumpAndTag
  const tags = settings.Trigger === ReleaseTrigger.Tag || settings.Trigger === ReleaseTrigger.VersionBumpAndTag
  const dispatch = settings.Trigger === ReleaseTrigger.Dispatch
  const text = (value?: string | null) => (value?.trim() ? value.trim() : null)
  return {
    Repository: settings.Repository.trim(),
    Branch: settings.Branch.trim() || 'master',
    Trigger: settings.Trigger,
    VersionFiles: bumps
      ? settings.VersionFiles.filter((file) => file.Path.trim()).map((file) => ({ ...file, Path: file.Path.trim() }))
      : [],
    TagPattern: tags ? text(settings.TagPattern) : null,
    Workflow: text(settings.Workflow),
    DispatchVersionInput: dispatch ? text(settings.DispatchVersionInput) : null,
    DispatchPrereleaseInput: dispatch ? text(settings.DispatchPrereleaseInput) : null,
    DispatchTakesTag: dispatch && settings.DispatchTakesTag,
  }
}

// The PyPI page where a release is yanked by hand (PyPI has no yank API).
export function pypiManageUrl(name: string): string {
  return `https://pypi.org/manage/project/${encodeURIComponent(name)}/releases/`
}
