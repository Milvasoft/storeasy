import type { BadgeTone } from '@/components/ui/Badge'
import type { RegistriesDictionary } from './i18n/en'
import { PackageAlertKind, PackageRegistry } from './types'

// The registries' name rules (the API's RegistryNames checks the same) and the labels of the registry enums.

const NAME_RULES: Record<PackageRegistry, { pattern: RegExp; max: number }> = {
  [PackageRegistry.Npm]: { pattern: /^(?:@[a-z0-9~-][a-z0-9._~-]*\/)?[a-z0-9~-][a-z0-9._~-]*$/, max: 214 },
  [PackageRegistry.NuGet]: { pattern: /^[A-Za-z0-9_]+(?:[.-][A-Za-z0-9_]+)*$/, max: 100 },
  [PackageRegistry.PyPI]: { pattern: /^(?:[A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9._-]*[A-Za-z0-9])$/, max: 214 },
  [PackageRegistry.Crates]: { pattern: /^[A-Za-z][A-Za-z0-9_-]*$/, max: 64 },
}

export const REGISTRIES: readonly PackageRegistry[] = [
  PackageRegistry.Npm,
  PackageRegistry.NuGet,
  PackageRegistry.PyPI,
  PackageRegistry.Crates,
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
    default:
      return t.alertKind.advisory
  }
}

export function alertTone(kind: PackageAlertKind, severity?: string | null): BadgeTone {
  if (kind === PackageAlertKind.Advisory) {
    return severity === 'LOW' || severity === 'MODERATE' ? 'warning' : 'danger'
  }
  return kind === PackageAlertKind.NoProvenance ? 'info' : 'warning'
}
