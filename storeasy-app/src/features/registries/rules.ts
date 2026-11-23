import type { BadgeTone } from '@/components/ui/Badge'
import type { RegistriesDictionary } from './i18n/en'
import { CiProvider, PackageAlertKind, PackageRegistry, PublisherCheckStatus } from './types'

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
