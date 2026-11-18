import { describe, expect, it } from 'vitest'
import { msEn } from '@/features/microsoft/i18n/en'
import { msTr } from '@/features/microsoft/i18n/tr'
import { blockText, percentText, statusLabel, statusTone } from '@/features/microsoft/labels'
import {
  canPlan,
  credentialProblems,
  daysUntil,
  expiryBounds,
  expiryInstant,
  expiryLevel,
  expiryValid,
  imageLimit,
  installerProblems,
  isStoreId,
  isSubmittableDraft,
  listingChange,
  listingDraftOf,
  listingProblems,
  lockState,
  msSecret,
  sameIdentity,
  statusStep,
} from '@/features/microsoft/rules'
import { MsInstallerType, MsProductKind, MsSubmissionStatus } from '@/features/microsoft/types'
import { msProduct, msSubmission } from '../../support/microsoft'

const TENANT = '8d6b2e5a-1111-2222-3333-444455553c07'
const CLIENT = 'f19c4e27-1111-2222-3333-44445555be12'

describe('microsoft listing rules', () => {
  it('sends only the changed fields of a language, features and terms as lines', () => {
    const original = listingDraftOf({ Locale: 'tr-tr', Title: 'Pusula', Description: 'A', Features: ['x'], SearchTerms: [] })

    const change = listingChange('tr-tr', { ...original, description: 'B', features: 'x\n\n y ' }, original, true)

    expect(change).toEqual({
      Locale: 'tr-tr',
      Title: null,
      Description: 'B',
      ShortDescription: null,
      WhatsNew: null,
      Features: ['x', 'y'],
      SearchTerms: null,
    })
    expect(listingChange('tr-tr', original, original, true)).toBeNull()
  })

  it('never sends the title of an MSI/EXE product', () => {
    const original = listingDraftOf(undefined)

    expect(listingChange('tr-tr', { ...original, title: 'Yeni' }, original, false)).toBeNull()
  })

  it('flags texts and lists over the Microsoft limits', () => {
    const draft = listingDraftOf(undefined)

    expect(
      listingProblems({
        ...draft,
        shortDescription: 'a'.repeat(1001),
        searchTerms: Array.from({ length: 8 }, (_, index) => `t${index}`).join('\n'),
        features: 'f'.repeat(201),
      }),
    ).toEqual(['shortDescription', 'features', 'searchTerms'])
    expect(listingProblems({ ...draft, description: 'a'.repeat(10_000) })).toEqual([])
  })

  it('allows 10 desktop screenshots, 8 of the other families and one logo', () => {
    expect(imageLimit('Screenshot')).toBe(10)
    expect(imageLimit('XboxScreenshot')).toBe(8)
    expect(imageLimit('StoreLogoSquare')).toBe(1)
  })
})

describe('microsoft installer rules', () => {
  it('asks for https, switches when not silent, a return codes page for EXE and a language', () => {
    expect(installerProblems({ PackageUrl: 'http://x', PackageType: MsInstallerType.Exe, Languages: [], IsSilentInstall: false })).toEqual([
      'url',
      'parameters',
      'docUrl',
      'languages',
    ])
    expect(
      installerProblems({
        PackageUrl: 'https://x.dev/a.msi',
        PackageType: MsInstallerType.Msi,
        Languages: ['tr-tr'],
        IsSilentInstall: true,
      }),
    ).toEqual([])
  })
})

describe('microsoft connection rules', () => {
  it('checks the Entra ids and builds the secret the API seals', () => {
    const fields = { tenantId: TENANT.toUpperCase(), clientId: ` ${CLIENT} `, clientSecret: ' s3cret ', sellerId: '40291187' }

    expect(credentialProblems(fields)).toEqual([])
    expect(credentialProblems({ tenantId: `${TENANT}x`, clientId: 'nope', clientSecret: '', sellerId: '12a' })).toEqual([
      'tenantId',
      'clientId',
      'clientSecret',
      'sellerId',
    ])
    expect(JSON.parse(msSecret(fields))).toEqual({ tenantId: TENANT, clientId: CLIENT, clientSecret: 's3cret', sellerId: '40291187' })
  })

  it('keeps a renewed secret on the same tenant and client', () => {
    const fields = { tenantId: TENANT, clientId: CLIENT, clientSecret: 'x', sellerId: '1' }

    expect(sameIdentity(fields, '3C07', 'be12')).toBe(true)
    expect(sameIdentity(fields, '9999', 'be12')).toBe(false)
  })

  it('takes an expiry from tomorrow up to 24 months away', () => {
    const today = new Date(2026, 10, 18)

    expect(expiryBounds(today)).toEqual({ min: '2026-11-19', max: '2028-11-18' })
    expect(expiryValid('2028-11-18', today)).toBe(true)
    expect(expiryValid('2028-11-19', today)).toBe(false)
    expect(expiryValid('2026-11-18', today)).toBe(false)
    expect(expiryValid(null, today)).toBe(false)
    expect(expiryInstant('2027-11-18')).toBe('2027-11-18T00:00:00Z')
  })

  it('warns 30 days before the expiry', () => {
    const now = new Date('2026-11-18T12:00:00Z')

    expect(daysUntil('2026-12-10T12:00:00Z', now)).toBe(22)
    expect(expiryLevel(22)).toBe('soon')
    expect(expiryLevel(31)).toBe('ok')
    expect(expiryLevel(-1)).toBe('expired')
  })

  it('accepts a 12 character Store id', () => {
    expect(isStoreId('9P4K7Q2MZL1D')).toBe(true)
    expect(isStoreId('9P4K7Q2MZL1')).toBe(false)
  })
})

describe('microsoft submission rules', () => {
  it('locks a draft changed in Partner Center and sees a submission in progress', () => {
    const edited = lockState(
      msProduct({ ActiveSubmission: msSubmission({ Status: MsSubmissionStatus.PendingCommit, ModifiedOutsideApi: true }) }),
    )
    const busy = lockState(msProduct({ ActiveSubmission: msSubmission() }))
    const first = lockState(msProduct({ FirstSubmissionDone: false }))

    expect(edited.editedInPartnerCenter).toBe(true)
    expect(canPlan(edited)).toBe(false)
    expect(busy.inProgress?.Status).toBe(MsSubmissionStatus.Certification)
    expect(canPlan(busy)).toBe(true)
    expect(canPlan(first)).toBe(false)
    expect(lockState(msProduct({ Kind: MsProductKind.MsiExe, FirstSubmissionDone: false })).firstSubmissionMissing).toBe(false)
  })

  it('sends on only an unchanged draft storeasy created', () => {
    expect(isSubmittableDraft(msSubmission({ Status: MsSubmissionStatus.PendingCommit }))).toBe(true)
    expect(isSubmittableDraft(msSubmission({ Status: MsSubmissionStatus.PendingCommit, CreatedByStoreasy: false }))).toBe(false)
    expect(isSubmittableDraft(msSubmission())).toBe(false)
  })

  it('places statuses on the five step machine and colors failures', () => {
    expect(statusStep(MsSubmissionStatus.CertificationFailed)).toBe(3)
    expect(statusStep(MsSubmissionStatus.Canceled)).toBe(-1)
    expect(statusTone(MsSubmissionStatus.CertificationFailed)).toBe('danger')
    expect(statusTone(MsSubmissionStatus.Published)).toBe('success')
    expect(statusTone(MsSubmissionStatus.PendingCommit)).toBe('neutral')
    expect(statusTone(MsSubmissionStatus.Certification)).toBe('warning')
    expect(statusLabel(MsSubmissionStatus.PendingPublication, msEn)).toBe('Waiting for publication')
  })

  it('names block keys and falls back to the key itself', () => {
    expect(blockText('MsFlightNameTaken', msTr)).toBe('Bu adda bir uçuş zaten var.')
    expect(blockText('MsSomethingNew', msEn)).toBe('The store refused the plan (MsSomethingNew).')
    expect(percentText(25, msTr)).toBe('%25')
    expect(percentText(12.5, msEn)).toBe('12.5%')
    expect(percentText(null, msEn)).toBe('-')
  })
})

describe('microsoft dictionaries', () => {
  function leaves(tree: object, prefix = ''): string[] {
    return Object.entries(tree).flatMap(([key, value]) =>
      typeof value === 'string' ? [`${prefix}${key}`] : leaves(value as object, `${prefix}${key}.`),
    )
  }

  it('has the same keys in Turkish and English', () => {
    expect(leaves(msTr).sort()).toEqual(leaves(msEn).sort())
  })
})
