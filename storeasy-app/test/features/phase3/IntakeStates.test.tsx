import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { IntakeRoutes } from '@/features/intake/IntakeRoutes'
import { intakeTr } from '@/features/intake/i18n'
import {
  SESSION_STATUSES,
  SOURCES,
  PLATFORMS,
  platformLabel,
  providerLabel,
  ruleText,
  sourceLabel,
  statusView,
} from '@/features/intake/labels'
import {
  ArtifactPlatform,
  ArtifactPullSourceKind,
  ArtifactSessionStatus,
  ArtifactSource,
  CheckStatus,
  IdentityKind,
  OidcExchangeResult,
  OidcProvider,
  PackageKind,
} from '@/features/intake/types'
import { organization } from '../../support/factories'
import { phase3State, publisher, pullSource, sessionDetail, sessionListItem, setupPhase3Api } from '../../support/phase3'
import { renderWithProviders } from '../../support/render'
import { requestsTo, testState } from '../../support/state'

setupPhase3Api()

// The other states of the intake screens: other roles, platforms, sources and session outcomes.

function renderIntake(route: string, connection?: 'connected' | 'disconnected') {
  return renderWithProviders(<IntakeRoutes />, {
    route,
    path: '/intake/*',
    activityConnection: connection ? { state: connection } : undefined,
  })
}

describe('intake states', () => {
  it('shows an iOS session delivered with a CI token, its package and exit code 0', async () => {
    phase3State.session = sessionDetail({
      Platform: ArtifactPlatform.Ios,
      FileName: 'pusula-2.4-119.ipa',
      Source: ArtifactSource.Cli,
      SourceRef: {
        Actor: 'Mert Kaya',
        RunUrl: 'https://ci.example.test/run/1',
        Repository: 'kuzeylab/pusula',
        Branch: 'main',
        Commit: 'abcdef12345',
      },
      Status: ArtifactSessionStatus.Delivered,
      Identity: { Kind: IdentityKind.CiToken, CiTokenPrefix: 'sci_7f2' },
      Package: {
        Kind: PackageKind.Ipa,
        Identifier: 'dev.kuzeylab.pusula',
        VersionName: '2.4',
        VersionCode: '119',
        MinOs: '17.0',
        XcodeVersion: '26.1',
        SdkVersion: 'iphoneos26.1',
        Signature: 'Apple Distribution',
        UsesNonExemptEncryption: false,
      },
      Checks: [{ Key: 'w', Status: CheckStatus.Warning, Title: 'Age rating', Message: 'New questions' }],
      AppliedRule: null,
      CiResult: { ExitCode: 0, Summary: 'Delivered' },
      PackageDeleted: true,
    })
    renderIntake('/intake/sessions/4127')

    expect(await screen.findByText("CI token'ı sci_7f2…")).toBeInTheDocument()
    expect(screen.getByText('Çıkış kodu 0')).toBeInTheDocument()
    expect(screen.getByText('Saklanan paket silindi.')).toBeInTheDocument()
    expect(screen.getByText('26.1 · iphoneos26.1')).toBeInTheDocument()
    expect(screen.getByText('Hayır')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Çalışmayı aç' })).toHaveAttribute('href', 'https://ci.example.test/run/1')
    expect(screen.queryByRole('button', { name: 'Paketi şimdi sil' })).not.toBeInTheDocument()
  })

  it('shows a failed session from a member upload and the polling note when the stream is down', async () => {
    phase3State.session = sessionDetail({
      Status: ArtifactSessionStatus.Failed,
      StatusDescription: 'Google Play refused the bundle.',
      Identity: { Kind: IdentityKind.User, UserName: 'Deniz Arslan' },
      Source: ArtifactSource.Manual,
    })
    renderIntake('/intake/sessions/4127', 'disconnected')

    expect(await screen.findByText('Google Play refused the bundle.')).toBeInTheDocument()
    expect(screen.getByText('Üye · Deniz Arslan')).toBeInTheDocument()
  })

  it('polls a running session list while the live stream is down', async () => {
    renderIntake('/intake/sessions', 'disconnected')

    expect(await screen.findByText('Canlı akış bağlı değil; liste birkaç saniyede bir yenilenir.')).toBeInTheDocument()
  })

  it('shows the empty session list and its filters', async () => {
    phase3State.sessions = []
    const { user } = renderIntake('/intake/sessions?app=101&source=8')

    expect(await screen.findByRole('heading', { name: 'Oturum yok' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Filtreleri temizle' }))
    expect(screen.getByTestId('location')).toHaveTextContent(/^\/intake\/sessions$/)
  })

  it('counts held and rejected sessions on the overview', async () => {
    const today = new Date().toISOString()
    phase3State.sessions = [
      sessionListItem({ Id: 1, Status: ArtifactSessionStatus.Held, StartDate: today }),
      sessionListItem({ Id: 2, Status: ArtifactSessionStatus.Rejected, StartDate: today }),
      sessionListItem({ Id: 3, Status: ArtifactSessionStatus.Delivered, StartDate: today, Source: ArtifactSource.Eas }),
    ]
    phase3State.summary = [
      { AppId: 101, AppName: 'Pusula', LastSession: sessionListItem({ Status: ArtifactSessionStatus.Delivered }), Rules: [] },
      { AppId: 102, AppName: 'Mola', LastSession: null, Rules: [] },
    ]
    renderIntake('/intake')

    expect((await screen.findAllByText('Kural yok')).length).toBeGreaterThan(0)
    expect(screen.getByText('Henüz paket yok')).toBeInTheDocument()
    expect(screen.getByText('Onay bekleyen').nextSibling).toHaveTextContent('1')
  })

  it('tells a viewer to ask an admin on the first open', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    phase3State.summary = []
    renderIntake('/intake')

    expect(await screen.findByText('Kurulumu bir yöneticiden iste.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Paket yükle' })).not.toBeInTheDocument()
  })

  it('shows read only lists to an editor', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    phase3State.publishers = [
      publisher({
        Provider: OidcProvider.AzureDevOps,
        Organization: 'kuzeylab',
        Project: 'Pusula',
        ServiceConnection: 'sc',
        LastExchangeResult: OidcExchangeResult.Succeeded,
      }),
      publisher({ Id: 63, Provider: OidcProvider.GitLab, LastExchangeDate: null, LastExchangeResult: null }),
    ]
    renderIntake('/intake/trusted-publishers')

    expect(await screen.findByText('kuzeylab / Pusula · sc')).toBeInTheDocument()
    expect(screen.getByText("Token'da yok")).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kural ekle' })).not.toBeInTheDocument()
  })

  it('shows the empty trusted publishers, CI tokens and sources', async () => {
    phase3State.publishers = []
    phase3State.ciTokens = []
    phase3State.sources = []
    const first = renderIntake('/intake/trusted-publishers')
    expect(await screen.findByRole('heading', { name: 'Henüz güvenilir yayıncı yok' })).toBeInTheDocument()
    first.unmount()
    const second = renderIntake('/intake/tokens')
    expect(await screen.findByRole('heading', { name: "Henüz CI token'ı yok" })).toBeInTheDocument()
    second.unmount()
    renderIntake('/intake/sources')
    expect(await screen.findByRole('heading', { name: 'Henüz çekme kaynağı yok' })).toBeInTheDocument()
  })

  it('adds a generic webhook source with a sample request', async () => {
    const { user } = renderIntake('/intake/sources')

    await user.click(await screen.findByRole('button', { name: 'Kaynak ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Çekme kaynağı ekle' })
    await user.click(within(dialog).getByRole('radio', { name: /Genel webhook/ }))
    await user.click(within(dialog).getByRole('button', { name: /Uygulama/ }))
    await user.click(await screen.findByRole('option', { name: 'Pusula' }))
    await user.type(within(dialog).getByLabelText('İzinli indirme alan adları'), 'api.codemagic.io')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Windows' }))
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    const shown = await screen.findByRole('dialog', { name: 'Kaynak hazır' })
    expect(within(shown).getByLabelText('Örnek istek')).toHaveTextContent('storeasy.artifact-ready/v1')
  })

  it('edits a source without a new secret', async () => {
    phase3State.sources = [
      pullSource({ Kind: ArtifactPullSourceKind.Webhook, LastCallDate: '2026-11-18T10:00:00Z', LastCallResult: '202', IncludeApk: true }),
    ]
    const { user } = renderIntake('/intake/sources')

    await user.click(await screen.findByRole('button', { name: /düzenle$/ }))
    const dialog = await screen.findByRole('dialog', { name: 'Çekme kaynağını düzenle' })
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Kaynak kaydedildi.')).toBeInTheDocument()
  })

  it('deletes a source after a confirmation', async () => {
    const { user } = renderIntake('/intake/sources')

    await user.click(await screen.findByRole('button', { name: /sil$/ }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Sil' }))

    expect(await screen.findByText('Kaynak silindi.')).toBeInTheDocument()
    expect(requestsTo('/Artifacts/source').some((request) => request.method === 'DELETE')).toBe(true)
  })

  it('switches the setup snippets between the ways in', async () => {
    const { user } = renderIntake('/intake/setup?app=101')

    await user.click(await screen.findByRole('tab', { name: 'CLI' }))
    expect(screen.getByLabelText('CLI')).toHaveTextContent('storeasy upload app-release.aab --app 101 --wait')
    await user.click(screen.getByRole('tab', { name: 'REST' }))
    expect(screen.getByLabelText('REST')).toHaveTextContent('/api/v1.0/apps/101/artifacts')
    await user.click(screen.getByRole('tab', { name: 'EAS' }))
    expect(screen.getByText(/storeasy adresini ve sırrını bir kez gösterir/)).toBeInTheDocument()
  })

  it('keeps the settings read only for an editor and checks the range', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    renderIntake('/intake/settings')

    expect(await screen.findByLabelText('Paket saklama süresi (gün)')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Kaydet' })).not.toBeInTheDocument()
  })

  it('creates a GitLab rule with its issuer', async () => {
    const { user } = renderIntake('/intake/trusted-publishers')

    await user.click(await screen.findByRole('button', { name: 'Kural ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Güvenilir yayıncı ekle' })
    await user.click(within(dialog).getByRole('radio', { name: 'GitLab' }))
    await user.type(within(dialog).getByLabelText('Proje yolu'), 'kuzeylab/mola')
    await user.type(within(dialog).getByLabelText(/Issuer/), 'https://gitlab.kuzeylab.dev/')

    expect(within(dialog).getByLabelText("Beklenen claim'ler")).toHaveTextContent('iss https://gitlab.kuzeylab.dev')
    expect(within(dialog).getByLabelText("Beklenen claim'ler")).toHaveTextContent('ref_type branch')
  })

  it('names every status, source, platform and provider', () => {
    expect(new Set(SESSION_STATUSES.map((status) => statusView(status, intakeTr).label)).size).toBe(SESSION_STATUSES.length)
    expect(new Set(SOURCES.map((source) => sourceLabel(source, intakeTr))).size).toBe(SOURCES.length)
    expect(PLATFORMS.map((platform) => platformLabel(platform, intakeTr))).toEqual(['iOS', 'Android', 'Windows'])
    expect(
      [OidcProvider.GitHub, OidcProvider.AzureDevOps, OidcProvider.GitLab].map((provider) => providerLabel(provider, intakeTr)),
    ).toEqual(['GitHub', 'Azure DevOps', 'GitLab'])
    expect(
      ruleText(
        { Platform: ArtifactPlatform.Ios, Enabled: true, Target: 1, TargetId: 'Kuzey içi', OnWarning: 2, RolloutFraction: null },
        intakeTr,
      ),
    ).toBe('iOS → TestFlight Kuzey içi')
    expect(ruleText({ Platform: ArtifactPlatform.Windows, Enabled: false, Target: 3, TargetId: 'x', OnWarning: 2 }, intakeTr)).toBe(
      'Windows → Kural yok',
    )
  })
})
