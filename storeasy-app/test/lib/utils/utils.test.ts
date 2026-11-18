import { describe, expect, it } from 'vitest'
import { checkP8, keyIdFromFileName, readServiceAccount } from '@/lib/connections/secrets'
import { en } from '@/lib/i18n/en'
import { auditRequest } from '@/pages/org/AuditPage'
import { addDays, parseDateOnly, toDateOnly } from '@/lib/utils/date'
import { isValidSlug, toSlug, toSlugDraft } from '@/lib/utils/slug'
import { emailError, passwordRules } from '@/lib/validation'
import { SAMPLE_P8, SAMPLE_SERVICE_ACCOUNT } from '../../support/factories'

describe('slug', () => {
  it('folds Turkish letters and joins words with a hyphen', () => {
    expect(toSlug('Mavi Atölye')).toBe('mavi-atolye')
    expect(toSlug("Deniz'in İşleri")).toBe('deniz-in-isleri')
    expect(isValidSlug('ab')).toBe(false)
    expect(isValidSlug('kuzey-lab')).toBe(true)
  })

  it('keeps a trailing hyphen while typing', () => {
    expect(toSlugDraft('mavi ')).toBe('mavi-')
  })
})

describe('date', () => {
  it('reads a calendar day in local time and refuses days that do not exist', () => {
    expect(parseDateOnly('2026-11-24')?.getDate()).toBe(24)
    expect(parseDateOnly('2027-02-31')).toBeNull()
    expect(toDateOnly(new Date(2026, 10, 18))).toBe('2026-11-18')
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01')
  })
})

describe('store secrets', () => {
  it('takes the key id from an App Store Connect file name', () => {
    expect(keyIdFromFileName('AuthKey_7qx2m4k9pl.p8')).toBe('7QX2M4K9PL')
    expect(keyIdFromFileName('key.p8')).toBeNull()
  })

  it('accepts a PKCS#8 PEM and refuses anything else', () => {
    expect(checkP8(SAMPLE_P8)).toBeNull()
    expect(checkP8('not a key')).toBe('notPem')
    expect(checkP8('-----BEGIN EC PRIVATE KEY-----\nabc\n-----END EC PRIVATE KEY-----')).toBe('notPrivateKey')
  })

  it('reads a service account and still shows what it can of a wrong file', () => {
    expect(readServiceAccount(SAMPLE_SERVICE_ACCOUNT)).toEqual({
      info: { clientEmail: 'storeasy@kuzeylab-play.iam.gserviceaccount.com', projectId: 'kuzeylab-play' },
      problem: null,
    })
    expect(readServiceAccount(JSON.stringify({ type: 'service_account', client_email: 'a@b.c', project_id: 'p' }))).toMatchObject({
      info: { projectId: 'p' },
      problem: 'noPrivateKey',
    })
    expect(readServiceAccount('{').problem).toBe('notJson')
  })
})

describe('validation', () => {
  it('checks e-mail addresses', () => {
    expect(emailError('', en)).toBe(en.validation.emailRequired)
    expect(emailError('deniz@kuzeylab', en)).toBe(en.validation.emailInvalid)
    expect(emailError(' deniz@kuzeylab.dev ', en)).toBeNull()
  })

  it('tells which password rules are met', () => {
    expect(passwordRules('Abcdef1!')).toEqual({ length: true, case: true, digit: true, symbol: true })
    expect(passwordRules('abc')).toEqual({ length: false, case: false, digit: false, symbol: false })
  })
})

describe('audit request', () => {
  it('builds enum filters as EqualTo, the user as Contains and the day range as UTC Between', () => {
    const request = auditRequest(new URLSearchParams('channel=2&user=ece&from=2026-11-12&to=2026-11-18&page=3&size=50'))

    expect(request).toMatchObject({ PageNumber: 3, RowCount: 50, Sorting: { SortBy: 'Date', Type: 1 } })
    const criterias = request.Filtering?.Criterias ?? []
    expect(criterias).toContainEqual({ FilterBy: 'Channel', Value: 2, Type: 5 })
    expect(criterias).toContainEqual({ FilterBy: 'UserName', Value: 'ece', Type: 1 })
    const between = criterias.find((criteria) => criteria.FilterBy === 'Date')
    expect(between?.Type).toBe(0)
    expect(new Date(String(between?.Value)).getDate()).toBe(12)
  })

  it('sends no filtering when nothing is filtered', () => {
    expect(auditRequest(new URLSearchParams()).Filtering).toBeUndefined()
  })
})
