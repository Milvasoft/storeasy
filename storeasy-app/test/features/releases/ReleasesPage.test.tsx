import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole, StoreKind } from '@/lib/api/types'
import { ReleasePlanKind, RolloutAction } from '@/features/common/types'
import { ReleasesPage } from '@/features/releases/ReleasesPage'
import { TestersPage } from '@/features/releases/TestersPage'
import { contentState, setupContentApi } from '../../support/content'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupContentApi()

function render(store: 'apple' | 'google' = 'apple') {
  return renderWithProviders(<ReleasesPage />, {
    route: `/apps/101/release/versions?store=${store}`,
    path: '/apps/:appId/release/versions',
  })
}

function useGoogleTracks() {
  contentState.releases = {
    StoreAppId: 202,
    Store: StoreKind.Google,
    Tracks: [
      { Track: 'internal', Releases: [{ Name: '2.4.0', VersionCodes: ['231'], Status: 'completed' }] },
      {
        Track: 'production',
        Releases: [
          {
            Name: '2.3.0',
            VersionCodes: ['230'],
            Status: 'inProgress',
            UserFraction: 0.2,
            ReleaseNotes: [{ Language: 'tr-TR', Text: 'Hız iyileştirmesi.' }],
          },
        ],
      },
    ],
  }
}

describe('ReleasesPage on the App Store', () => {
  it('links a valid build and answers export compliance in the same plan', async () => {
    const { user } = render()
    const version = await screen.findByRole('article', { name: '2.4' })

    await user.click(within(version).getByRole('button', { name: 'Build bağla' }))
    const dialog = await screen.findByRole('dialog', { name: '2.4 sürümüne build bağla' })
    expect(within(dialog).getByRole('radio', { name: /2\.4 \(413\)/ })).toBeDisabled()
    await user.click(within(dialog).getByRole('radio', { name: /2\.4 \(412\)/ }))
    await user.click(within(dialog).getByRole('radio', { name: 'Hayır' }))
    await user.click(within(dialog).getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog', { name: 'de-DE anahtar kelimeler' })
    expect(lastRequestTo('/Builds/link')?.body).toMatchObject({
      StoreAppId: 201,
      VersionId: 'v-24',
      BuildId: 9001,
      UsesNonExemptEncryption: false,
      DryRun: true,
    })
  })

  it('plans release settings with a phased release', async () => {
    const { user } = render()
    const version = await screen.findByRole('article', { name: '2.4' })

    await user.click(within(version).getByRole('button', { name: 'Yayın ayarları' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('radio', { name: 'Onaydan sonra otomatik yayınla' }))
    await user.click(within(dialog).getByRole('checkbox', { name: '7 güne yayılan aşamalı yayın' }))
    await user.click(within(dialog).getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog', { name: 'de-DE anahtar kelimeler' })
    expect(lastRequestTo('/Releases/plan')?.body).toMatchObject({
      Kind: ReleasePlanKind.SetReleaseType,
      VersionId: 'v-24',
      ReleaseType: 2,
      PhasedRelease: true,
    })
  })

  it('pauses a phased release for an Owner', async () => {
    const { user } = render()
    const version = await screen.findByRole('article', { name: '2.3' })

    await user.click(within(version).getByRole('button', { name: 'Duraklat' }))

    await screen.findByRole('dialog', { name: 'de-DE anahtar kelimeler' })
    expect(lastRequestTo('/Releases/rollout')?.body).toMatchObject({ VersionId: 'v-23', Action: RolloutAction.Pause, DryRun: true })
  })

  it('keeps phased release and submission disabled for an Editor, with the reasons', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]

    render()

    const live = await screen.findByRole('article', { name: '2.3' })
    expect(within(live).getByRole('button', { name: 'Duraklat' })).toBeDisabled()
    expect(within(live).getByText(/Production yayını iznini ister/)).toBeInTheDocument()
    const draft = screen.getByRole('article', { name: '2.4' })
    expect(within(draft).getByRole('button', { name: 'İncelemeye gönder' })).toBeDisabled()
    expect(within(draft).getByRole('button', { name: 'Build bağla' })).toBeEnabled()
  })
})

describe('ReleasesPage on Google Play', () => {
  it('shows the tracks with the staged rollout and its notes', async () => {
    useGoogleTracks()

    render('google')

    const production = await screen.findByRole('article', { name: 'production' })
    expect(within(production).getByText('%20')).toBeInTheDocument()
    expect(within(production).getByText(/Hız iyileştirmesi/)).toBeInTheDocument()
  })

  it('assigns a bundle to a test track as a plan', async () => {
    useGoogleTracks()
    const { user } = render('google')

    await user.click(await screen.findByRole('button', { name: "Track'e ata" }))
    const dialog = await screen.findByRole('dialog', { name: "Track'e ata" })
    const codes = within(dialog).getByLabelText('Sürüm kodları')
    expect(codes).toHaveValue('42')
    await user.click(within(dialog).getByRole('radio', { name: 'Aşamalı yayın' }))
    await user.clear(within(dialog).getByLabelText('Yayın oranı'))
    await user.type(within(dialog).getByLabelText('Yayın oranı'), '25')
    await user.click(within(dialog).getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog', { name: 'de-DE anahtar kelimeler' })
    expect(lastRequestTo('/Releases/plan')?.body).toMatchObject({
      Kind: ReleasePlanKind.AssignTrack,
      Track: 'internal',
      VersionCodes: ['42'],
      Status: 'inProgress',
      UserFraction: 0.25,
    })
  })

  it('halts a production rollout with a reason', async () => {
    useGoogleTracks()
    const { user } = render('google')
    const production = await screen.findByRole('article', { name: 'production' })

    await user.click(within(production).getByRole('button', { name: 'Yayını durdur' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.type(within(dialog).getByLabelText(/Neden/), 'Çökme raporu')
    await user.click(within(dialog).getByRole('button', { name: 'Yayını durdur' }))

    await screen.findByRole('dialog', { name: 'de-DE anahtar kelimeler' })
    expect(lastRequestTo('/Releases/rollout')?.body).toMatchObject({
      Track: 'production',
      Action: RolloutAction.Halt,
      Reason: 'Çökme raporu',
    })
  })

  it('refuses a percentage that does not raise the rollout', async () => {
    useGoogleTracks()
    const { user } = render('google')
    const production = await screen.findByRole('article', { name: 'production' })

    await user.click(within(production).getByRole('button', { name: 'Oranı değiştir' }))
    const dialog = await screen.findByRole('dialog')
    await user.clear(within(dialog).getByLabelText('Yeni oran'))
    await user.type(within(dialog).getByLabelText('Yeni oran'), '10')
    await user.click(within(dialog).getByRole('button', { name: 'Planla' }))

    expect(within(dialog).getByText(/Şu anki orandan büyük/)).toBeInTheDocument()
    expect(lastRequestTo('/Releases/rollout')).toBeUndefined()
  })

  it('keeps production rollout buttons disabled for an Editor', async () => {
    useGoogleTracks()
    testState.organizations = [organization({ Role: OrgRole.Editor })]

    render('google')

    const production = await screen.findByRole('article', { name: 'production' })
    expect(within(production).getByRole('button', { name: 'Oranı değiştir' })).toBeDisabled()
    expect(screen.getByRole('button', { name: "Track'e ata" })).toBeEnabled()
  })
})

describe('TestersPage', () => {
  it('adds a Google Group and plans the change', async () => {
    const { user } = renderWithProviders(<TestersPage />, {
      route: '/apps/101/release/testers?store=google',
      path: '/apps/:appId/release/testers',
    })
    expect(await screen.findByText('beta@kuzeylab.dev')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Grup e-postası'), 'qa@kuzeylab.dev')
    await user.click(screen.getByRole('button', { name: 'Grup ekle' }))
    await user.click(screen.getByRole('button', { name: 'Test kullanıcısı değişikliklerini planla' }))

    await screen.findByRole('dialog')
    expect(lastRequestTo('/Releases/testers')?.body).toMatchObject({
      Track: 'internal',
      GoogleGroups: ['beta@kuzeylab.dev', 'qa@kuzeylab.dev'],
      DryRun: true,
    })
  })

  it('refuses something that is not an e-mail', async () => {
    const { user } = renderWithProviders(<TestersPage />, {
      route: '/apps/101/release/testers?store=google',
      path: '/apps/:appId/release/testers',
    })
    await screen.findByText('beta@kuzeylab.dev')

    await user.type(screen.getByLabelText('Grup e-postası'), 'qa')
    await user.click(screen.getByRole('button', { name: 'Grup ekle' }))

    expect(screen.getByText('Bir grup e-posta adresi gir.')).toBeInTheDocument()
  })
})
