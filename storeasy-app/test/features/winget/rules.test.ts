import { describe, expect, it } from 'vitest'
import {
  credentialProblems,
  installersFromSources,
  isPackageIdentifier,
  needsAction,
  normalizeFork,
  stateKey,
  stateTone,
  tokenKind,
  wingetSecret,
} from '@/features/winget/rules'
import { WingetPullRequestState } from '@/features/winget/types'

const TOKEN = `ghp_${'a1B2'.repeat(9)}`

describe('winget rules', () => {
  it('checks the token and the fork the way the API does', () => {
    expect(credentialProblems({ token: TOKEN, fork: 'kuzeylab/winget-pkgs' })).toEqual([])
    expect(credentialProblems({ token: 'password', fork: 'winget-pkgs' })).toEqual(['token', 'fork'])
    expect(credentialProblems({ token: `github_pat_11AB${'x'.repeat(40)}`, fork: 'https://github.com/kuzeylab/winget-pkgs.git' })).toEqual(
      [],
    )
    expect(normalizeFork(' https://github.com/kuzeylab/winget-pkgs/ ')).toBe('kuzeylab/winget-pkgs')
    expect(JSON.parse(wingetSecret({ token: ` ${TOKEN} `, fork: 'github.com/kuzeylab/winget-pkgs' }))).toEqual({
      token: TOKEN,
      fork: 'kuzeylab/winget-pkgs',
    })
    expect(tokenKind(TOKEN)).toBe('classic')
    expect(tokenKind('github_pat_1')).toBe('fine-grained')
  })

  it('accepts package identifiers of the manifest schema only', () => {
    expect(isPackageIdentifier('Milvasoft.Kabuk')).toBe(true)
    expect(isPackageIdentifier('Microsoft.VisualStudio.2022.Community')).toBe(true)
    expect(isPackageIdentifier('Kabuk')).toBe(false)
    expect(isPackageIdentifier('Milva soft.Kabuk')).toBe(false)
    expect(isPackageIdentifier('A.B.C.D.E.F.G.H.I')).toBe(false)
  })

  it('names the review states and flags the ones waiting for the author', () => {
    expect(needsAction(WingetPullRequestState.NeedsAuthorFeedback)).toBe(true)
    expect(needsAction(WingetPullRequestState.ValidationFailed)).toBe(true)
    expect(needsAction(WingetPullRequestState.Approved)).toBe(false)
    expect(stateKey(WingetPullRequestState.Merged)).toBe('merged')
    expect(stateTone(WingetPullRequestState.Merged)).toBe('success')
    expect(stateTone(WingetPullRequestState.ValidationFailed)).toBe('danger')
  })

  it('builds one installer per architecture of the Microsoft Store packages', () => {
    expect(
      installersFromSources([
        {
          Url: 'https://x.dev/a.exe',
          Type: 'exe',
          Architectures: ['x64', 'arm64'],
          SilentSwitches: '/S',
          Locales: ['en-US'],
          SignatureStatus: 2,
        },
      ]),
    ).toEqual([
      { Url: 'https://x.dev/a.exe', Architecture: 'x64', Type: 'exe', SilentSwitches: '/S', Locale: 'en-US' },
      { Url: 'https://x.dev/a.exe', Architecture: 'arm64', Type: 'exe', SilentSwitches: '/S', Locale: 'en-US' },
    ])
  })
})
