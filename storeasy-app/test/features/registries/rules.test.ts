import { describe, expect, it } from 'vitest'
import { registriesEn } from '@/features/registries/i18n/en'
import { alertKindLabel, alertTone, isPackageName, registryLabel } from '@/features/registries/rules'
import { PackageAlertKind, PackageRegistry } from '@/features/registries/types'

describe('package registries rules', () => {
  it.each([
    [PackageRegistry.Npm, 'left-pad', true],
    [PackageRegistry.Npm, '@sigstore/bundle', true],
    [PackageRegistry.Npm, 'Left-Pad', false],
    [PackageRegistry.NuGet, 'Newtonsoft.Json', true],
    [PackageRegistry.NuGet, 'bad..id', false],
    [PackageRegistry.PyPI, 'zope.interface', true],
    [PackageRegistry.PyPI, '-bad', false],
    [PackageRegistry.Crates, 'serde_json', true],
    [PackageRegistry.Crates, '1crate', false],
    [PackageRegistry.Crates, '   ', false],
  ])('checks %s name %s', (registry, name, valid) => {
    expect(isPackageName(registry, name)).toBe(valid)
  })

  it('labels the registries and alerts', () => {
    expect(registryLabel(PackageRegistry.Crates)).toBe('crates.io')
    expect(alertKindLabel(PackageAlertKind.NoProvenance, registriesEn)).toBe('No provenance')
    expect(alertTone(PackageAlertKind.Advisory, 'CRITICAL')).toBe('danger')
    expect(alertTone(PackageAlertKind.Advisory, 'MODERATE')).toBe('warning')
  })
})
