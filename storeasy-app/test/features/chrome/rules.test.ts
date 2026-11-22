import { describe, expect, it } from 'vitest'
import { ChromeItemState } from '@/features/chrome/types'
import {
  EMPTY_CHROME_CREDENTIALS,
  chromeSecret,
  compareVersions,
  credentialProblems,
  isItemId,
  parseVersion,
  stateKey,
  stateTone,
} from '@/features/chrome/rules'

describe('Chrome Web Store rules', () => {
  it('reads manifest versions like the store', () => {
    expect(parseVersion('1.2.3.4')).toEqual([1, 2, 3, 4])
    expect(parseVersion('01.2')).toBeNull()
    expect(parseVersion('1.65536')).toBeNull()
    expect(parseVersion('1.2.3.4.5')).toBeNull()
    expect(compareVersions('1.2', '1.2.0')).toBe(0)
    expect(compareVersions('1.10', '1.9')).toBeGreaterThan(0)
    expect(compareVersions('bad', '1.0')).toBeLessThan(0)
  })

  it('knows extension ids', () => {
    expect(isItemId('abcdefghijklmnopabcdefghijklmnop')).toBe(true)
    expect(isItemId('abcdefghijklmnopabcdefghijklmnoq')).toBe(false)
  })

  it('checks the credential fields and builds the secret the API takes', () => {
    expect(credentialProblems(EMPTY_CHROME_CREDENTIALS)).toEqual(['publisherId', 'serviceAccountKey'])
    const oauth = {
      ...EMPTY_CHROME_CREDENTIALS,
      auth: 'oauth' as const,
      publisherId: 'pub-1',
      clientId: 'id',
      clientSecret: 's',
      refreshToken: 'r',
      itemId: 'nope',
    }
    expect(credentialProblems(oauth)).toEqual(['itemId'])
    expect(JSON.parse(chromeSecret({ ...oauth, itemId: '' }))).toEqual({
      publisherId: 'pub-1',
      clientId: 'id',
      clientSecret: 's',
      refreshToken: 'r',
      itemId: null,
    })
    const key = JSON.stringify({ type: 'service_account', client_email: 'a@b.iam.gserviceaccount.com', private_key: 'k' })
    expect(credentialProblems({ ...EMPTY_CHROME_CREDENTIALS, publisherId: 'pub-1', serviceAccountKey: key })).toEqual([])
    expect(JSON.parse(chromeSecret({ ...EMPTY_CHROME_CREDENTIALS, publisherId: 'pub-1', serviceAccountKey: key }))).toEqual({
      publisherId: 'pub-1',
      serviceAccountKey: key,
      itemId: null,
    })
  })

  it('names and colors the review states', () => {
    expect(stateKey(ChromeItemState.PendingReview)).toBe('pendingReview')
    expect(stateTone(ChromeItemState.Rejected)).toBe('danger')
    expect(stateTone(ChromeItemState.Published)).toBe('success')
  })
})
