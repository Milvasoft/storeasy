import { describe, expect, it } from 'vitest'
import {
  addonStatusKey,
  compareVersions,
  credentialProblems,
  fileStatusKey,
  firefoxSecret,
  isAddonReference,
  isGuid,
  isVersion,
  parseIds,
} from '@/features/firefox/rules'
import { FirefoxAddonStatus, FirefoxFileStatus } from '@/features/firefox/types'

describe('Firefox Add-ons rules', () => {
  it('checks the AMO API key fields and writes the secret the API parses', () => {
    expect(credentialProblems({ issuer: 'user:12345:67', secret: 'a'.repeat(64) })).toEqual([])
    expect(credentialProblems({ issuer: '12345', secret: 'short' })).toEqual(['issuer', 'secret'])
    expect(credentialProblems({ issuer: ' user:1:2 ', secret: `${'a'.repeat(40)} b` })).toEqual(['secret'])
    expect(JSON.parse(firefoxSecret({ issuer: ' user:1:2 ', secret: ` ${'c'.repeat(64)} ` }))).toEqual({ issuer: 'user:1:2', secret: 'c'.repeat(64) })
  })

  it('tells add-on ids, guids and slugs', () => {
    expect(isGuid('pusula@kuzeylab.com')).toBe(true)
    expect(isGuid('{8e6c8f1a-1234-4bcd-9abc-0123456789ab}')).toBe(true)
    expect(isGuid('pusula')).toBe(false)
    expect(isAddonReference('640123')).toBe(true)
    expect(isAddonReference('pusula-tabs')).toBe(true)
    expect(isAddonReference('not a slug!')).toBe(false)
    expect(isAddonReference('')).toBe(false)
  })

  it('orders versions like the API', () => {
    expect(isVersion('1.2.3.4')).toBe(true)
    expect(isVersion('01.2')).toBe(false)
    expect(isVersion('1.2.3.4.5')).toBe(false)
    expect(compareVersions('1.10', '1.9')).toBeGreaterThan(0)
    expect(compareVersions('1.0', '1.0.0')).toBe(0)
    expect(compareVersions('1.0a1', '1.0')).toBeLessThan(0)
  })

  it('names every status and reads asset id lists', () => {
    expect(Object.values(FirefoxAddonStatus).map(addonStatusKey)).toEqual(['none', 'public', 'nominated', 'incomplete', 'disabled', 'rejected', 'deleted'])
    expect(Object.values(FirefoxFileStatus).map(fileStatusKey)).toEqual(['none', 'unreviewed', 'public', 'disabled'])
    expect(parseIds('12, 13 x 0 14')).toEqual([12, 13, 14])
  })
})
