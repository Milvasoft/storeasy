import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { ArtifactPlatform, ArtifactSessionStatus, ArtifactSource, CheckStatus, DeliveryTarget, OidcProvider } from '@/features/intake/types'
import { IntakeRoutes } from '@/features/intake/IntakeRoutes'
import { expectedClaims, decodeJwtPayload } from '@/features/intake/claims'
import { organization } from '../../support/factories'
import { PART_URL, phase3State, sessionDetail, setupPhase3Api } from '../../support/phase3'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupPhase3Api()

function renderIntake(route = '/intake') {
  return renderWithProviders(<IntakeRoutes />, { route, path: '/intake/*' })
}

function lastWrite(method: string, path: string) {
  return requestsTo(path)
    .filter((request) => request.method === method)
    .at(-1)
}

function jwt(payload: object): string {
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_')
  return `${encode({ alg: 'RS256' })}.${encode(payload)}.signature`
}

describe('intake overview', () => {
  it('shows the last package per app, its rule and the ways in', async () => {
    renderIntake()

    expect(await screen.findByRole('link', { name: 'pusula-1.0.0-42.aab' })).toHaveAttribute('href', '/intake/sessions/4127')
    expect(screen.getAllByText('Teslim ediliyor').length).toBeGreaterThan(0)
    expect(screen.getByText('Android → internal')).toBeInTheDocument()
    expect(screen.getAllByText('Uygulama başına son paket').length).toBeGreaterThan(0)
    expect(await screen.findByText('1 güven kuralı')).toBeInTheDocument()
  })

  it('leads the first open to the GitHub Action setup', async () => {
    phase3State.summary = [{ AppId: 101, AppName: 'Pusula', LastSession: null, Rules: [] }]
    renderIntake()

    expect(await screen.findByRole('heading', { name: 'Henüz paket gelmedi' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: "GitHub Action'ı kur" })).toHaveAttribute('href', '/intake/setup?path=github')
  })

  it('refreshes on a live artifact.progress event', async () => {
    const { activity } = renderIntake()
    await screen.findByRole('link', { name: 'pusula-1.0.0-42.aab' })
    const before = requestsTo('/Artifacts/summary').length

    act(() => {
      ;(activity as unknown as { push: (event: object) => void }).push({
        Type: 'artifact.progress',
        OrganizationId: 7,
        AppId: 101,
        Message: 'Session 4127: Delivered',
        Date: '2026-11-18T11:00:00Z',
      })
    })

    await waitFor(() => expect(requestsTo('/Artifacts/summary').length).toBeGreaterThan(before))
  })
})

describe('intake sessions', () => {
  it('lists sessions and filters them by status', async () => {
    const { user } = renderIntake('/intake/sessions')

    expect(await screen.findByText('pusula-dev-1.0.0-41.aab')).toBeInTheDocument()
    expect(screen.getAllByText('9e41c0d2')).toHaveLength(2)
    expect(screen.getByText('GitHub Actions #318')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Durum' }))
    await user.click(await screen.findByRole('option', { name: 'Reddedildi' }))

    await waitFor(() =>
      expect(lastRequestTo('/Artifacts/sessions')?.body).toMatchObject({ Status: ArtifactSessionStatus.Rejected, PageNumber: 1 }),
    )
  })

  it('shows a running session with its steps, package and identity', async () => {
    renderIntake('/intake/sessions/4127')

    expect(await screen.findByRole('heading', { name: 'pusula-1.0.0-42.aab' })).toBeInTheDocument()
    expect(screen.getByText('Mağazaya teslim')).toBeInTheDocument()
    expect(screen.getByText('1 parça yeniden gönderildi')).toBeInTheDocument()
    expect(screen.getByText('dev.kuzeylab.pusula')).toBeInTheDocument()
    expect(screen.getByText('OIDC · güven kuralı #61')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Canlı işi aç' })).toHaveAttribute('href', '/activity/jobs/48500')
    expect(screen.queryByRole('button', { name: 'Teslim et' })).not.toBeInTheDocument()
  })

  it('delivers a held session after a confirmation', async () => {
    phase3State.session = sessionDetail({
      Status: ArtifactSessionStatus.Held,
      HoldReason: 'The checks passed with warnings: a member must approve the delivery.',
    })
    const { user } = renderIntake('/intake/sessions/4127')

    expect(await screen.findByText('The checks passed with warnings: a member must approve the delivery.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Teslim et' }))
    const confirm = await screen.findByRole('alertdialog')
    expect(within(confirm).getByText(/internal hedefine gider/)).toBeInTheDocument()
    await user.click(within(confirm).getByRole('button', { name: 'Teslim et' }))

    expect(await screen.findByText('Teslim başladı.')).toBeInTheDocument()
    expect(lastRequestTo('/Artifacts/session/deliver')?.body).toEqual({ OrganizationId: 7, SessionId: 4127 })
  })

  it('deletes the package of a rejected session and shows the CI result', async () => {
    phase3State.session = sessionDetail({
      Status: ArtifactSessionStatus.Rejected,
      StatusDescription: 'Rejected: the package name does not match the app.',
      CiResult: { ExitCode: 2, Summary: 'Package rejected: dev.kuzeylab.pusula.dev' },
      Checks: [{ Key: 'id', Status: CheckStatus.Missing, Title: 'Package name' }],
    })
    const { user } = renderIntake('/intake/sessions/4127')

    expect(await screen.findByText('Rejected: the package name does not match the app.')).toBeInTheDocument()
    expect(screen.getByText('Çıkış kodu 2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Paketi şimdi sil' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Paketi şimdi sil' }))

    expect(await screen.findByText('Paket silindi.')).toBeInTheDocument()
    expect(lastWrite('DELETE', '/Artifacts/session')?.body).toEqual({ OrganizationId: 7, SessionId: 4127 })
  })

  it('points a duplicate to its original session', async () => {
    phase3State.session = sessionDetail({ Status: ArtifactSessionStatus.Duplicate, OriginalSessionId: 4100, StoreJobId: null })
    renderIntake('/intake/sessions/4127')

    expect(await screen.findByText('Bu paket zaten alındı')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Asıl oturumu aç' })).toHaveAttribute('href', '/intake/sessions/4100')
    expect(screen.queryByRole('button', { name: 'Paketi şimdi sil' })).not.toBeInTheDocument()
  })
})

describe('manual upload', () => {
  it('hashes the file, uploads the part and completes the session', async () => {
    server.use(
      http.put(
        PART_URL,
        () => new HttpResponse(null, { status: 200, headers: { ETag: '"etag-1"', 'Access-Control-Expose-Headers': 'ETag' } }),
      ),
    )
    const { user } = renderIntake('/intake/sessions')

    await user.click(await screen.findByRole('button', { name: 'Paket yükle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Paket yükle' })
    await user.click(within(dialog).getByRole('button', { name: /Uygulama/ }))
    await user.click(await screen.findByRole('option', { name: 'Pusula' }))
    const file = new File(['package bytes'], 'pusula-1.0.0-43.aab', { type: 'application/octet-stream' })
    await user.upload(dialog.querySelector('input[type="file"]') as HTMLInputElement, file)

    await waitFor(() =>
      expect(lastRequestTo('/Artifacts/4200/complete')?.body).toEqual({ OrganizationId: 7, Parts: [{ PartNumber: 1, ETag: '"etag-1"' }] }),
    )
    const open = lastRequestTo('/apps/101/artifacts')?.body as Record<string, unknown>
    expect(open).toMatchObject({ FileName: 'pusula-1.0.0-43.aab', Size: 13, Source: ArtifactSource.Manual })
    expect(open.Sha256).toMatch(/^[0-9a-f]{64}$/)
    expect(await screen.findByText('Yüklendi; storeasy şimdi okuyup kontrol ediyor.')).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/intake/sessions/4200')
  })

  it('reports a duplicate without uploading', async () => {
    phase3State.upload = { SessionId: 4201, Duplicate: true, Resumed: false, OriginalSessionId: 4127 }
    const { user } = renderIntake('/intake/sessions')

    await user.click(await screen.findByRole('button', { name: 'Paket yükle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Paket yükle' })
    await user.click(within(dialog).getByRole('button', { name: /Uygulama/ }))
    await user.click(await screen.findByRole('option', { name: 'Pusula' }))
    await user.upload(dialog.querySelector('input[type="file"]') as HTMLInputElement, new File(['x'], 'a.ipa'))

    expect(await screen.findByText('Bu paket zaten alındı (oturum 4127); hiçbir şey yeniden yüklenmedi.')).toBeInTheDocument()
    expect(requestsTo('/Artifacts/4201/complete')).toHaveLength(0)
  })

  it('refuses a file that is not a package', async () => {
    renderIntake('/intake/sessions')
    const user = userEvent.setup({ applyAccept: false })

    await user.click(await screen.findByRole('button', { name: 'Paket yükle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Paket yükle' })
    await user.click(within(dialog).getByRole('button', { name: /Uygulama/ }))
    await user.click(await screen.findByRole('option', { name: 'Pusula' }))
    await user.upload(dialog.querySelector('input[type="file"]') as HTMLInputElement, new File(['x'], 'notes.txt'))

    expect(await within(dialog).findByText(/storeasy .ipa, .aab/)).toBeInTheDocument()
  })
})

describe('CI tokens', () => {
  it('creates a token for chosen apps and shows it once', async () => {
    const { user } = renderIntake('/intake/tokens')

    expect(await screen.findByText('codemagic-pusula')).toBeInTheDocument()
    expect(screen.getByText(/Süresi doldu/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Token oluştur' }))
    const dialog = await screen.findByRole('dialog', { name: "CI token'ı oluştur" })
    await user.type(within(dialog).getByLabelText('Ad'), 'bitrise-pusula')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Pusula' }))
    await user.click(within(dialog).getByRole('radio', { name: '1 yıl' }))
    await user.click(within(dialog).getByRole('button', { name: 'Token oluştur' }))

    const created = await screen.findByRole('dialog', { name: 'bitrise-pusula oluşturuldu' })
    expect(within(created).getByText('sci_7f2a4QmV9xT2kLr8NwZp3Hc6YbJd1EuS5gAo')).toBeInTheDocument()
    expect(lastRequestTo('/Artifacts/ci-token')?.body).toEqual({
      OrganizationId: 7,
      Name: 'bitrise-pusula',
      AppIds: [101],
      AllApps: false,
      Scopes: ['artifacts:write'],
      ExpiryDays: 365,
    })
  })

  it('revokes a token after a confirmation', async () => {
    const { user } = renderIntake('/intake/tokens')

    await user.click(await screen.findByRole('button', { name: 'codemagic-pusula iptal et' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'İptal et' }))

    expect(await screen.findByText("CI token'ı iptal edildi.")).toBeInTheDocument()
    expect(lastRequestTo('/Artifacts/ci-token')?.body).toEqual({ OrganizationId: 7, Id: 71 })
  })

  it('keeps an editor to the list', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    renderIntake('/intake/tokens')

    expect(await screen.findByText(/yalnız Sahip ve Yönetici rolünde/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Token oluştur' })).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'codemagic-pusula iptal et' })).not.toBeInTheDocument()
  })
})

describe('trusted publishers', () => {
  it('adds a GitHub rule and previews the expected claims', async () => {
    const { user } = renderIntake('/intake/trusted-publishers')

    await user.click(await screen.findByRole('button', { name: 'Kural ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Güvenilir yayıncı ekle' })
    await user.click(within(dialog).getByRole('button', { name: /Uygulama/ }))
    await user.click(await screen.findByRole('option', { name: 'Pusula' }))
    await user.type(within(dialog).getByLabelText('Repo'), 'kuzeylab/pusula')
    await user.type(within(dialog).getByLabelText(/Ortam/), 'play-internal')
    expect(within(dialog).getByLabelText("Beklenen claim'ler")).toHaveTextContent('environment play-internal')
    await user.click(within(dialog).getByRole('button', { name: 'Kuralı kaydet' }))

    expect(await screen.findByText('Kural kaydedildi.')).toBeInTheDocument()
    expect(lastRequestTo('/Artifacts/trusted-publisher')?.body).toMatchObject({
      AppId: 101,
      Provider: OidcProvider.GitHub,
      Repository: 'kuzeylab/pusula',
      Branch: 'main',
      Environment: 'play-internal',
      Organization: null,
    })
  })

  it('switches the form to Azure DevOps fields', async () => {
    const { user } = renderIntake('/intake/trusted-publishers')

    await user.click(await screen.findByRole('button', { name: 'Kural ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Güvenilir yayıncı ekle' })
    await user.click(within(dialog).getByRole('radio', { name: 'Azure DevOps' }))
    await user.type(within(dialog).getByLabelText('Azure DevOps organizasyonu'), 'kuzeylab')
    await user.type(within(dialog).getByLabelText('Proje'), 'Pusula')
    await user.type(within(dialog).getByLabelText('Service connection'), 'storeasy-pusula')

    expect(within(dialog).getByLabelText("Beklenen claim'ler")).toHaveTextContent('sub sc://kuzeylab/Pusula/storeasy-pusula')
    expect(within(dialog).getByText("Azure DevOps token'ında dal bilgisi yok.")).toBeInTheDocument()
  })

  it('diagnoses a rule with a dry run of a pasted token', async () => {
    const { user } = renderIntake('/intake/trusted-publishers')

    await user.click(await screen.findByRole('button', { name: 'Pusula kuralının tanısı' }))
    const drawer = await screen.findByRole('dialog', { name: 'Pusula · GitHub' })
    expect(await within(drawer).findByText('refs/heads/main')).toBeInTheDocument()
    await user.click(within(drawer).getByLabelText('OIDC token'))
    await user.paste(jwt({ repository: 'kuzeylab/pusula', ref: 'refs/heads/feature/x', exp: 1_900_000_000 }))
    expect(within(drawer).getByLabelText("Yapıştırılan token'daki claim'ler")).toHaveTextContent('ref refs/heads/feature/x')
    await user.click(within(drawer).getByRole('button', { name: 'Dene' }))

    expect(await within(drawer).findByText("Bazı claim'ler eşleşmiyor")).toBeInTheDocument()
    expect((lastRequestTo('/Artifacts/trusted-publisher/test')?.body as { Id: number }).Id).toBe(61)
  })

  it('deletes a rule after a confirmation', async () => {
    const { user } = renderIntake('/intake/trusted-publishers')

    await user.click(await screen.findByRole('button', { name: 'Pusula kuralını sil' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Sil' }))

    expect(await screen.findByText('Kural silindi.')).toBeInTheDocument()
  })
})

describe('delivery rules', () => {
  it('edits the Android track and the iOS group and saves test channels only', async () => {
    const { user } = renderIntake('/intake/rules?app=101')

    const group = await screen.findByRole('button', { name: 'TestFlight iç grubu' })
    await user.click(group)
    await user.click(await screen.findByRole('option', { name: 'Kuzey içi' }))
    await user.click(screen.getByRole('switch', { name: 'iOS paketlerini otomatik teslim et' }))
    await user.type(screen.getByLabelText(/Kademeli dağıtım/), '20')
    await user.click(screen.getByRole('button', { name: 'Kuralları kaydet' }))

    expect(await screen.findByText('Teslim kuralları kaydedildi.')).toBeInTheDocument()
    const body = lastWrite('PUT', '/Artifacts/delivery-rules')?.body as { AppId: number; Rules: Array<Record<string, unknown>> }
    expect(body.AppId).toBe(101)
    expect(body.Rules).toEqual([
      expect.objectContaining({
        Platform: ArtifactPlatform.Ios,
        Enabled: true,
        Target: DeliveryTarget.TestFlightGroup,
        TargetId: 'Kuzey içi',
      }),
      expect.objectContaining({
        Platform: ArtifactPlatform.Android,
        Target: DeliveryTarget.PlayTrack,
        TargetId: 'internal',
        RolloutFraction: 0.2,
      }),
    ])
    expect(screen.getByText('Production ve incelemeye gönderme hiçbir zaman otomatik değildir')).toBeInTheDocument()
  })

  it('refuses a production track', async () => {
    const { user } = renderIntake('/intake/rules?app=101')

    await user.click(await screen.findByRole('button', { name: "Play test track'i" }))
    await user.click(await screen.findByRole('option', { name: 'Başka bir kapalı track' }))
    await user.type(screen.getByLabelText('Kapalı track adı'), 'production')

    expect(screen.getByText('Production track hedef olamaz.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Kuralları kaydet' })).toBeDisabled()
  })
})

describe('pull sources', () => {
  it('adds an EAS source and shows its URL, secret and the EAS command once', async () => {
    const { user } = renderIntake('/intake/sources')

    expect(await screen.findByText('http://localhost:3000/api/webhooks/eas/src_4kq9m2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Kaynak ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Çekme kaynağı ekle' })
    await user.click(within(dialog).getByRole('button', { name: /Uygulama/ }))
    await user.click(await screen.findByRole('option', { name: 'Pusula' }))
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    const shown = await screen.findByRole('dialog', { name: 'Kaynak hazır' })
    expect(within(shown).getByText('whsec_src_Q4n8Lr2VxT9mKp6Z')).toBeInTheDocument()
    expect(within(shown).getByLabelText("EAS'ta kaydet")).toHaveTextContent('eas webhook:create --event BUILD')
    expect(lastRequestTo('/Artifacts/source')?.body).toMatchObject({
      Kind: 1,
      AppId: 101,
      AllowedDomains: ['expo.dev', '*.expo.dev', 'storage.googleapis.com'],
      RotateSecret: false,
    })
  })

  it('rejects a bad domain in the form', async () => {
    const { user } = renderIntake('/intake/sources')

    await user.click(await screen.findByRole('button', { name: 'Kaynak ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Çekme kaynağı ekle' })
    await user.type(within(dialog).getByLabelText('İzinli indirme alan adları'), '\nlocalhost')

    expect(within(dialog).getByText('Alan adı değil: localhost')).toBeInTheDocument()
  })

  it('rotates a secret after a confirmation', async () => {
    const { user } = renderIntake('/intake/sources')

    await user.click(await screen.findByRole('button', { name: 'Yenile' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Yenile' }))

    expect(await screen.findByRole('dialog', { name: 'Yeni sır hazır' })).toBeInTheDocument()
    expect(lastRequestTo('/Artifacts/source')?.body).toMatchObject({ Id: 81, RotateSecret: true })
  })
})

describe('setup and settings', () => {
  it('fills the GitHub workflow with the app id and says the trust rule is ready', async () => {
    renderIntake('/intake/setup?path=github&app=101')

    expect(await screen.findByLabelText('.github/workflows/release.yml')).toHaveTextContent('app: 101')
    expect(screen.getByLabelText('.github/workflows/release.yml')).toHaveTextContent('environment: play-internal')
    expect(await screen.findByText(/Güven kuralı hazır/)).toBeInTheDocument()
  })

  it('warns when the Azure path has no rule', async () => {
    renderIntake('/intake/setup?path=azure&app=101')

    expect(await screen.findByText(/henüz güven kuralı yok/)).toBeInTheDocument()
    expect(screen.getByLabelText('azure-pipelines.yml')).toHaveTextContent('StoreasyUpload@1')
  })

  it('saves the retention settings', async () => {
    const { user } = renderIntake('/intake/settings')

    const days = await screen.findByLabelText('Paket saklama süresi (gün)')
    await user.clear(days)
    await user.type(days, '14')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Paket alma ayarları kaydedildi.')).toBeInTheDocument()
    expect(lastWrite('PUT', '/Artifacts/settings')?.body).toEqual({
      OrganizationId: 7,
      ArtifactRetentionDays: 14,
      MaxArtifactBytes: 4 * 1024 * 1024 * 1024,
    })
  })
})

describe('claims helpers', () => {
  it('mirrors the server expected claims for GitLab', () => {
    expect(expectedClaims({ AppId: 1, Provider: OidcProvider.GitLab, Repository: 'kuzeylab/mola', Branch: 'release/*' })).toEqual([
      ['iss', 'https://gitlab.com'],
      ['aud', 'storeasy'],
      ['project_path', 'kuzeylab/mola'],
      ['ref', 'release/*'],
      ['ref_type', 'branch'],
    ])
  })

  it('decodes only a JWT payload', () => {
    expect(decodeJwtPayload(jwt({ sub: 'x' }))).toEqual({ sub: 'x' })
    expect(decodeJwtPayload('not a token')).toBeNull()
  })
})
