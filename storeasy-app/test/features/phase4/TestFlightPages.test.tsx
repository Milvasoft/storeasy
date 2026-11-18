import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { TestFlightBuildPage, TestFlightGroupPage, TestFlightPage, buildLabel } from '@/features/testflight/TestFlightPages'
import { BetaReviewState } from '@/features/testflight/types'
import { organization } from '../../support/factories'
import { betaBuild, phase4State, setupPhase4Api } from '../../support/phase4'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupPhase4Api()

function renderOverview() {
  return renderWithProviders(<TestFlightPage />, { route: '/apps/101/release/testflight', path: '/apps/:appId/release/testflight' })
}

function renderGroup(id = 71) {
  return renderWithProviders(<TestFlightGroupPage />, {
    route: `/apps/101/release/testflight/groups/${id}`,
    path: '/apps/:appId/release/testflight/groups/:groupId',
  })
}

function renderBuild(id = 9101) {
  return renderWithProviders(<TestFlightBuildPage />, {
    route: `/apps/101/release/testflight/builds/${id}`,
    path: '/apps/:appId/release/testflight/builds/:buildId',
  })
}

describe('TestFlightPage', () => {
  it('warns about the build that expires soon and lists builds and groups', async () => {
    renderOverview()

    expect(await screen.findByText(/1\.0 \(37\) .* · 6 gün kaldı/)).toBeInTheDocument()
    expect(screen.getByText(/48 test kullanıcısı uygulamayı açamaz/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '1.0 (37)' })).toHaveAttribute('href', '/apps/101/release/testflight/builds/9101')
    expect(screen.getByText(/Süresi doldu ·/)).toBeInTheDocument()
    expect(screen.getByText('Kuzey içi')).toBeInTheDocument()
    expect(lastRequestTo('/TestFlight/builds')?.query).toMatchObject({ StoreAppId: '201' })
  })

  it('creates an external group with a limited public link', async () => {
    const { user } = renderOverview()

    await user.click(await screen.findByRole('button', { name: 'Grup oluştur' }))
    const dialog = await screen.findByRole('dialog', { name: 'Yeni TestFlight grubu' })
    await user.type(within(dialog).getByLabelText('Grup adı'), 'Beta')
    await user.click(within(dialog).getByRole('switch', { name: 'Herkese açık bağlantı' }))
    await user.click(within(dialog).getByRole('switch', { name: 'Test kullanıcısı sayısını sınırla' }))
    await user.type(within(dialog).getByLabelText('Katılım sınırı'), '50')
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText("App Store Connect'e gönderildi.")).toBeInTheDocument()
    expect(lastRequestTo('/TestFlight/group')?.body).toMatchObject({
      StoreAppId: 201,
      Name: 'Beta',
      IsInternal: false,
      PublicLinkEnabled: true,
      PublicLinkLimit: 50,
      PublicLinkLimitEnabled: true,
    })
  })

  it('adds a beta description and refreshes from App Store Connect', async () => {
    const { user } = renderOverview()

    await user.click(await screen.findByRole('button', { name: 'Dil ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Yeni beta dili' })
    await user.type(within(dialog).getByLabelText('Dil kodu'), 'en-US')
    await user.type(within(dialog).getByLabelText(/Beta açıklaması/), 'Rings for friends.')
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))
    await expect.poll(() => requestsTo('/TestFlight/localizations').filter((request) => request.method === 'PUT').length).toBe(1)
    await user.click(screen.getByRole('button', { name: "App Store Connect'ten yenile" }))

    expect(await screen.findByText('TestFlight verisi yeniden okunuyor.')).toBeInTheDocument()
    expect(lastRequestTo('/TestFlight/sync')?.body).toMatchObject({ StoreAppId: 201 })
  })

  it('offers no writes to a Viewer and says when there is no build', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    phase4State.builds = []
    phase4State.groups = []

    renderOverview()

    expect(await screen.findByRole('heading', { name: 'Henüz TestFlight build’i yok' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Grup oluştur' })).not.toBeInTheDocument()
  })
})

describe('TestFlightGroupPage', () => {
  it('shows the testers, the public link and the builds of the group', async () => {
    renderGroup()

    expect(await screen.findByRole('heading', { name: 'Erken erişim' })).toBeInTheDocument()
    expect(await screen.findByText('aylin.celik@ornek.net')).toBeInTheDocument()
    expect(screen.getByText('Davet edildi')).toBeInTheDocument()
    expect(screen.getByText('https://testflight.apple.com/join/kZ4mQ2pL')).toBeInTheDocument()
    expect(lastRequestTo('/TestFlight/testers')?.body).toMatchObject({ GroupId: 71, StoreAppId: 201 })
  })

  it('invites a tester by e-mail', async () => {
    const { user } = renderGroup()

    await user.click(await screen.findByRole('button', { name: 'Test kullanıcısı ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Test kullanıcısı davet et' })
    await user.type(within(dialog).getByLabelText('E-posta'), 'emre')
    expect(within(dialog).getByText('Geçerli bir e-posta adresi gir.')).toBeInTheDocument()
    await user.type(within(dialog).getByLabelText('E-posta'), '@ornek.net')
    await user.type(within(dialog).getByLabelText(/^Ad/), 'Emre')
    await user.click(within(dialog).getByRole('button', { name: 'Davet et' }))

    expect(await screen.findByText('Davet yola çıktı.')).toBeInTheDocument()
    expect(lastRequestTo('/TestFlight/group/testers')?.body).toMatchObject({
      GroupId: 71,
      Add: [{ Email: 'emre@ornek.net', FirstName: 'Emre', LastName: null }],
      Remove: [],
    })
  })

  it('removes a tester after a confirmation and a build from the group', async () => {
    const { user } = renderGroup()

    await user.click(await screen.findByRole('button', { name: 'derya.a@ornek.net kullanıcısını çıkar' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Çıkar' }))
    await expect.poll(() => (lastRequestTo('/TestFlight/group/testers')?.body as { Remove: number[] } | undefined)?.Remove).toEqual([8802])
    await user.click(screen.getByRole('button', { name: "1.0 (37) build'ini gruptan çıkar" }))

    await expect.poll(() => lastRequestTo('/TestFlight/group/builds')?.body).toMatchObject({ RemoveBuildIds: [9101], AddBuildIds: [] })
  })

  it('renames the group and turns its link off', async () => {
    const { user } = renderGroup()

    await user.click(await screen.findByRole('button', { name: 'Düzenle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Grubu düzenle' })
    await user.click(within(dialog).getByRole('switch', { name: 'Herkese açık bağlantı' }))
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    await expect.poll(() => lastRequestTo('/TestFlight/group')?.body).toMatchObject({ GroupId: 71, PublicLinkEnabled: false })
  })

  it('says when the group does not exist', async () => {
    renderGroup(999)

    expect(await screen.findByRole('alert')).toHaveTextContent('Kayıt bulunamadı')
  })
})

describe('TestFlightBuildPage', () => {
  it('names a build by version and build number', () => {
    expect(buildLabel(betaBuild())).toBe('1.0 (37)')
    expect(buildLabel({})).toBe('- (-)')
  })

  it('saves what to test for a language', async () => {
    const { user } = renderBuild()
    const box = await screen.findByLabelText('Neyi test etmeli (tr)')

    await user.type(box, ' Sessiz saatler.')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText("Metin TestFlight'a yazılıyor.")).toBeInTheDocument()
    expect(lastRequestTo('/TestFlight/build/what-to-test')?.body).toMatchObject({
      BuildId: 9101,
      Locale: 'tr',
      WhatsNew: 'Davet akışını dene. Sessiz saatler.',
    })
  })

  it('submits for beta review only after the version and build are typed', async () => {
    const { user } = renderBuild()

    await user.click(await screen.findByRole('button', { name: 'Beta incelemeye gönder' }))
    const dialog = await screen.findByRole('alertdialog')
    const confirm = within(dialog).getByRole('button', { name: 'Beta incelemeye gönder' })
    expect(confirm).toBeDisabled()
    await user.type(within(dialog).getByRole('textbox'), '1.0 (37)')
    await user.click(confirm)

    expect(await screen.findByText('Beta incelemeye gönderildi.')).toBeInTheDocument()
    expect(lastRequestTo('/TestFlight/build/beta-review')?.body).toMatchObject({ BuildId: 9101, Confirm: true, ConfirmVersion: '1.0 (37)' })
  })

  it('adds the build to another group', async () => {
    const { user } = renderBuild()

    await user.click(await screen.findByRole('button', { name: 'Bir gruba ekle' }))
    await user.click(await screen.findByRole('option', { name: 'Kuzey içi' }))

    await expect.poll(() => lastRequestTo('/TestFlight/group/builds')?.body).toMatchObject({ GroupId: 70, AddBuildIds: [9101] })
  })

  it('keeps an expired build read only', async () => {
    renderBuild(9100)

    expect(await screen.findByText('Bu build artık açılmıyor')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Beta incelemeye gönder' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kaydet' })).not.toBeInTheDocument()
  })

  it('shows an approved build without a submit action', async () => {
    phase4State.builds = [betaBuild({ BetaReviewState: BetaReviewState.Approved })]

    renderBuild()

    expect(await screen.findByText(/Bu build onaylı/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Beta incelemeye gönder' })).not.toBeInTheDocument()
  })
})
