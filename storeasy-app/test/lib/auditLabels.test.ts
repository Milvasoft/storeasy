import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { AuditAction } from '@/lib/api/types'
import { en } from '@/lib/i18n/en'
import { tr } from '@/lib/i18n/tr'
import { auditActionLabel } from '@/lib/org/auditLabels'

// The API's AuditAction enum, read from its source: every action it writes must have a label, or the audit log shows
// the action's number.
function apiActions(): Map<string, number> {
  const source = readFileSync(resolve(process.cwd(), '../storeasy-api/src/Storeasy.Domain/Enums/AuditAction.cs'), 'utf-8')
  return new Map([...source.matchAll(/^\s+([A-Z]\w+) = (\d+),/gm)].map((match) => [match[1], Number(match[2])]))
}

describe('audit action labels', () => {
  it('knows every action the API writes, with the same number', () => {
    const api = apiActions()

    expect(api.size).toBeGreaterThan(0)
    expect(Object.fromEntries(Object.entries(AuditAction))).toEqual(Object.fromEntries(api))
  })

  it('labels every action in both languages, never with its number', () => {
    for (const action of Object.values(AuditAction)) {
      expect(auditActionLabel(action, tr)).not.toBe(String(action))
      expect(auditActionLabel(action, en)).not.toBe(String(action))
    }
    expect(auditActionLabel(AuditAction.MsSubmitted, tr)).toBe('Microsoft Store gönderimi yapıldı')
    expect(auditActionLabel(AuditAction.PlanApproved, en)).toBe('Change plan approved')
  })
})
