import type { ReactElement } from 'react'
import { configure, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { MsAddonsPage } from '@/features/microsoft/AddonsPage'
import { MsFlightsPage } from '@/features/microsoft/FlightsPage'
import { MsImagesPage } from '@/features/microsoft/ImagesPage'
import { MsInstallersPage } from '@/features/microsoft/InstallersPage'
import { MsListingsPage } from '@/features/microsoft/ListingsPage'
import { MsPackagesPage } from '@/features/microsoft/PackagesPage'
import { MsPricingPage } from '@/features/microsoft/PricingPage'
import { MsProductPage } from '@/features/microsoft/ProductPage'
import { MsSubmissionsPage } from '@/features/microsoft/SubmissionsPage'
import {
  MsAddonType,
  MsAssistedReason,
  MsChangeKind,
  MsProductKind,
  MsRolloutAction,
  MsSignatureStatus,
  MsSubmissionStatus,
  MsSubmissionTarget,
} from '@/features/microsoft/types'
import { organization } from '../../support/factories'
import {
  MS_STORE_APP_ID,
  msAddon,
  msChangePlan,
  msContent,
  msInstaller,
  msProduct,
  msState,
  msSubmission,
  setupMsApi,
} from '../../support/microsoft'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupMsApi()

// These screens chain a dry run, a plan read and a render; under a loaded coverage run that can pass the default 1 s.
configure({ asyncUtilTimeout: 5000 })

function render(page: ReactElement, path: string, lang: 'tr' | 'en' = 'tr') {
  return renderWithProviders(page, { route: `/apps/101/ms/${path}`, path: `/apps/:appId/ms/${path}`, lang })
}

function asRole(role: OrgRole) {
  testState.organizations = [organization({ Role: role })]
}

describe('Microsoft Store product', () => {
  it('shows the flags, the Partner Center rules and the assisted steps with their links', async () => {
    render(<MsProductPage />, 'product')

    expect(await screen.findByRole('heading', { name: 'Pusula', level: 1 })).toBeInTheDocument()
    expect(await screen.findByText('9P4K7Q2MZL1D')).toBeInTheDocument()
    expect(screen.getByText('Microsoft allows one active submission at a time.')).toBeInTheDocument()
    expect(screen.getByText('Bekleyen gönderim yok.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: "Partner Center'da aç" })).toHaveAttribute(
      'href',
      'https://partner.microsoft.com/dashboard/products/9P4K7Q2MZL1D/agerating',
    )
    expect(screen.getByRole('link', { name: 'Adımı aç' })).toHaveAttribute('href', '/apps/101/assisted/5601')
    expect(lastRequestTo('/ms/product')?.query).toMatchObject({ StoreAppId: String(MS_STORE_APP_ID) })
    const nav = screen.getByRole('navigation', { name: 'Microsoft Store' })
    expect(within(nav).getByRole('link', { name: 'Paket uçuşları' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Yükleyiciler' })).not.toBeInTheDocument()
  })

  it('warns that a submission is in certification', async () => {
    msState.product = msProduct({ ActiveSubmission: msSubmission() })

    render(<MsProductPage />, 'product')

    expect(await screen.findByText(/2\.3\.0\.0 şu an Sertifikasyonda/)).toBeInTheDocument()
    expect(screen.getByRole('article', { name: '2.3.0.0' })).toBeInTheDocument()
  })

  it('locks a draft changed in Partner Center and offers only discarding it, through a plan', async () => {
    msState.product = msProduct({
      ActiveSubmission: msSubmission({ Status: MsSubmissionStatus.PendingCommit, ModifiedOutsideApi: true }),
    })
    const { user } = render(<MsListingsPage />, 'listings')

    expect(await screen.findByText("Bu taslak Partner Center'da değiştirildi")).toBeInTheDocument()
    expect(await screen.findByLabelText('Açıklama')).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Taslağı sil' }))

    await screen.findByRole('dialog', { name: 'Microsoft Store Listings of 9P4K7Q2MZL1D' })
    expect(lastRequestTo('/ms/change')?.body).toMatchObject({
      StoreAppId: MS_STORE_APP_ID,
      DryRun: true,
      Change: { Kind: MsChangeKind.DiscardSubmission },
    })
  })

  it('sends a ready MSI/EXE draft with the Store id typed', async () => {
    msState.product = msProduct({ Kind: MsProductKind.MsiExe, IsReady: true, PackageFamilyName: null })
    const { user } = render(<MsProductPage />, 'product')

    await user.click(await screen.findByRole('button', { name: 'Sertifikasyona gönder' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Pusula sertifikasyona gönderilsin mi?' })
    const send = within(dialog).getByRole('button', { name: 'Gönder' })
    expect(send).toBeDisabled()
    await user.type(within(dialog).getByLabelText('9P4K7Q2MZL1D yaz'), '9p4k7q2mzl1d')
    await user.click(send)

    expect(await screen.findByText('Sertifikasyona gönderildi; iş durumu izliyor.')).toBeInTheDocument()
    expect(lastRequestTo('/ms/submission/submit')?.body).toMatchObject({
      StoreAppId: MS_STORE_APP_ID,
      Target: MsSubmissionTarget.Installer,
      Confirm: '9p4k7q2mzl1d',
    })
  })

  it('keeps the send button of an editor disabled without SubmitForReview', async () => {
    asRole(OrgRole.Editor)
    msState.product = msProduct({ Kind: MsProductKind.MsiExe, IsReady: true })

    render(<MsProductPage />, 'product')

    expect(await screen.findByRole('button', { name: 'Sertifikasyona gönder' })).toBeDisabled()
    expect(screen.getByText(/Göndermek .* iznini ister/)).toBeInTheDocument()
  })

  it('says a part is not there for the product type', async () => {
    msState.product = msProduct({ Kind: MsProductKind.MsiExe })

    render(<MsFlightsPage />, 'flights')

    expect(await screen.findByText('Bu bölüm MSI/EXE uygulaması için yok.')).toBeInTheDocument()
  })
})

describe('Microsoft Store listings', () => {
  it('counts the limits, plans the changed fields and approves the plan', async () => {
    const { user } = render(<MsListingsPage />, 'listings')
    const description = await screen.findByLabelText('Açıklama')

    await user.clear(description)
    await user.type(description, 'Yeni metin')
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    const dialog = await screen.findByRole('dialog', { name: 'Microsoft Store Listings of 9P4K7Q2MZL1D' })
    expect(lastRequestTo('/ms/change')?.body).toMatchObject({
      DryRun: true,
      Change: {
        Kind: MsChangeKind.Listings,
        Listings: [{ Locale: 'tr-tr', Description: 'Yeni metin', Title: null, Features: null }],
      },
    })
    expect(lastRequestTo('/plans/plan')?.query).toMatchObject({ PlanId: '7101' })
    expect(within(dialog).getByText('Microsoft Store değişikliği')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Onayla ve gönder' }))

    expect(await screen.findByText('Plan onaylandı; mağaza işi kuyruğa girdi.')).toBeInTheDocument()
    expect(lastRequestTo('/plans/approve')?.body).toMatchObject({ PlanId: 7101 })
  })

  it('blocks the plan while a list is over the limit', async () => {
    const { user } = render(<MsListingsPage />, 'listings')
    const terms = await screen.findByLabelText('Arama terimleri')

    await user.type(terms, '\na\nb\nc\nd\ne\nf')

    expect(screen.getByText('Plan için sınırı aşan alanları düzelt.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Planla' })).toBeDisabled()
  })

  it('switches languages and shows a blocked answer with the assisted steps link', async () => {
    msState.change = msChangePlan({
      Blocked: true,
      BlockKey: 'MsSubmissionInProgress',
      AssistedReason: MsAssistedReason.ManualEdit,
      PlanId: null,
    })
    const { user } = render(<MsListingsPage />, 'listings')

    await user.click(await screen.findByRole('button', { name: /en-us/ }))
    await user.type(screen.getByLabelText('Kısa açıklama'), 'Plan it')
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    expect(await screen.findByText('Başka bir gönderim sürüyor. Bitmesini bekle.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Asistanlı adımları aç' })).toHaveAttribute('href', '/apps/101/assisted?store=microsoft')
    expect(requestsTo('/plans/plan')).toHaveLength(0)
  })

  it('keeps the title of an MSI/EXE product read only, in English', async () => {
    msState.product = msProduct({ Kind: MsProductKind.MsiExe })

    render(<MsListingsPage />, 'listings', 'en')

    expect(await screen.findByLabelText('Title')).toBeDisabled()
    expect(screen.getByText('MSI/EXE titles are read only through the API.')).toBeInTheDocument()
  })

  it('shows the empty state when the submission has no listing', async () => {
    msState.content = msContent({ Listings: [] })

    render(<MsListingsPage />, 'listings')

    expect(await screen.findByText('Gönderimde henüz metin yok.')).toBeInTheDocument()
  })
})

describe('Microsoft Store images', () => {
  it('counts the images of a type and plans a removal', async () => {
    const { user } = render(<MsImagesPage />, 'images')

    const desktop = await screen.findByRole('region', { name: 'Screenshot' })
    expect(within(desktop).getByText('1 / 10')).toBeInTheDocument()
    await user.click(within(desktop).getByRole('button', { name: 'Kaldır: tr-1.png' }))
    expect(within(desktop).getByText('0 / 10')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog', { name: 'Microsoft Store Listings of 9P4K7Q2MZL1D' })
    expect(lastRequestTo('/ms/change')?.body).toMatchObject({
      Change: { Kind: MsChangeKind.Images, Images: { Locale: 'tr-tr', Add: [], RemoveFileNames: ['tr-1.png'] } },
    })
  })

  it('says a full logo slot is full', async () => {
    const { user } = render(<MsImagesPage />, 'images')

    await user.click(await screen.findByRole('button', { name: 'Görsel türü' }))
    await user.click(screen.getByRole('option', { name: 'StoreLogoSquare' }))

    expect(screen.getByText('Bu tür bu dilde dolu.')).toBeInTheDocument()
  })

  it('shows an empty language', async () => {
    const { user } = render(<MsImagesPage />, 'images')

    await user.click(await screen.findByRole('button', { name: 'en-us' }))

    expect(screen.getByText('Bu dilde henüz görsel yok.')).toBeInTheDocument()
  })
})

describe('Microsoft Store pricing', () => {
  it('plans a new visibility and keeps the rest as they are', async () => {
    const { user } = render(<MsPricingPage />, 'pricing')

    await user.click(await screen.findByRole('button', { name: 'Görünürlük' }))
    await user.click(screen.getByRole('option', { name: 'Hidden' }))
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog', { name: 'Microsoft Store Listings of 9P4K7Q2MZL1D' })
    expect(lastRequestTo('/ms/change')?.body).toMatchObject({
      Change: { Kind: MsChangeKind.Pricing, Pricing: { Visibility: 'Hidden', PriceId: null, TrialPeriod: null, Markets: null } },
    })
  })

  it('makes the price assisted on Pricing Version 2', async () => {
    msState.product = msProduct({ PricingV2: true })

    render(<MsPricingPage />, 'pricing')

    expect(await screen.findByText('Fiyat asistanlı')).toBeInTheDocument()
    expect(screen.queryByLabelText('Temel fiyat kademesi')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Planla' })).toBeDisabled()
  })

  it('plans the markets of an MSI/EXE product', async () => {
    msState.product = msProduct({ Kind: MsProductKind.MsiExe })
    msState.content = msContent({
      Pricing: { Markets: ['US'], Discoverability: 'DISCOVERABLE', PricingModel: 'FREE', FreeTrial: 'NO_FREE_TRIAL' },
    })
    const { user } = render(<MsPricingPage />, 'pricing')

    const markets = await screen.findByLabelText('Pazarlar')
    await user.clear(markets)
    await user.type(markets, 'us, tr')
    await user.click(screen.getByRole('button', { name: 'Fiyat modeli' }))
    await user.click(screen.getByRole('option', { name: 'PAID' }))
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await waitFor(() =>
      expect(lastRequestTo('/ms/change')?.body).toMatchObject({
        Change: { Pricing: { Markets: ['US', 'TR'], PricingModel: 'PAID', Discoverability: null } },
      }),
    )
  })
})

describe('Microsoft Store packages and rollout', () => {
  it('lists the packages and plans a removal with a rollout percentage', async () => {
    const { user } = render(<MsPackagesPage />, 'packages')

    await user.click(await screen.findByRole('button', { name: 'Kaldır: Pusula_2.2.0.0_x64.msix' }))
    await user.type(screen.getByLabelText('Yeni paketlerin aşamalı dağıtımı'), '150')
    expect(screen.getAllByText('0 ile 100 arasında bir yüzde gir.').length).toBeGreaterThan(0)
    await user.clear(screen.getByLabelText('Yeni paketlerin aşamalı dağıtımı'))
    await user.type(screen.getByLabelText('Yeni paketlerin aşamalı dağıtımı'), '10')
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog', { name: 'Microsoft Store Listings of 9P4K7Q2MZL1D' })
    expect(lastRequestTo('/ms/change')?.body).toMatchObject({
      Change: {
        Kind: MsChangeKind.Packages,
        FlightId: null,
        Packages: { AddAssetIds: [], RemoveFileNames: ['Pusula_2.2.0.0_x64.msix'], RolloutPercentage: 10 },
      },
    })
  })

  it('raises the rollout of the published submission', async () => {
    const { user } = render(<MsPackagesPage />, 'packages')

    const rollout = await screen.findByRole('region', { name: 'Aşamalı dağıtım' })
    expect(within(rollout).getByText('Dağıtılıyor')).toBeInTheDocument()
    await user.type(within(rollout).getByLabelText('Yeni yüzde'), '50')
    await user.click(within(rollout).getByRole('button', { name: '%50 yap' }))

    await screen.findByRole('dialog', { name: 'Microsoft Store Listings of 9P4K7Q2MZL1D' })
    expect(lastRequestTo('/ms/change')?.body).toMatchObject({
      Change: {
        Kind: MsChangeKind.Rollout,
        Rollout: { SubmissionId: '1152921505403', Action: MsRolloutAction.SetPercentage, Percentage: 50 },
      },
    })
  })

  it('halts a flight rollout and names the missing permission of an editor for production', async () => {
    const { user } = render(<MsPackagesPage />, 'packages')

    await user.click(await screen.findByRole('button', { name: 'Gönderim' }))
    await user.click(screen.getByRole('option', { name: 'Uçuş: Kuzey içi' }))
    await waitFor(() => expect(lastRequestTo('/ms/content')?.query).toMatchObject({ FlightId: 'flight-1' }))
    const rollout = await screen.findByRole('region', { name: 'Aşamalı dağıtım' })
    await user.click(within(rollout).getByRole('button', { name: 'Durdur' }))

    await waitFor(() =>
      expect(lastRequestTo('/ms/change')?.body).toMatchObject({
        Change: {
          Kind: MsChangeKind.Rollout,
          FlightId: 'flight-1',
          Rollout: { SubmissionId: '1152921505744', Action: MsRolloutAction.Halt },
        },
      }),
    )
  })

  it('needs ReleaseProduction for the app rollout', async () => {
    asRole(OrgRole.Editor)

    render(<MsPackagesPage />, 'packages')

    const rollout = await screen.findByRole('region', { name: 'Aşamalı dağıtım' })
    expect(within(rollout).getByRole('button', { name: 'Tamamla' })).toBeDisabled()
    expect(within(rollout).getByText(/Dağıtım değişikliği .* iznini ister/)).toBeInTheDocument()
  })
})

describe('Microsoft Store installers', () => {
  function msiExe() {
    msState.product = msProduct({ Kind: MsProductKind.MsiExe })
    msState.content = msContent({
      Kind: MsProductKind.MsiExe,
      InstallerPackages: [
        {
          PackageUrl: 'https://indir.kuzeylab.dev/tarla/0.9.4/TarlaSetup.exe',
          PackageType: 1,
          Architecture: 'X64',
          Languages: ['tr-tr', 'en-us'],
          IsSilentInstall: false,
          InstallerParameters: '/S',
          GenericDocUrl: 'https://kuzeylab.dev/tarla/codes',
        },
      ],
    })
  }

  it('shows the signature check and queues a new one', async () => {
    msiExe()
    msState.packages = [msInstaller({ SignatureStatus: MsSignatureStatus.Unsigned, SignatureNote: null })]
    const { user } = render(<MsInstallersPage />, 'installers')

    expect(await screen.findByText('İmzasız')).toBeInTheDocument()
    expect(screen.getByText(/İmzasız yükleyiciyi Store reddeder/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'İmzayı kontrol et' }))

    expect(await screen.findByText('İmza kontrolü kuyruğa girdi.')).toBeInTheDocument()
    expect(lastRequestTo('/ms/packages/verify')?.body).toMatchObject({
      StoreAppId: MS_STORE_APP_ID,
      PackageUrls: ['https://indir.kuzeylab.dev/tarla/0.9.4/TarlaSetup.exe'],
    })
  })

  it('checks a new row and plans the whole set', async () => {
    msiExe()
    const { user } = render(<MsInstallersPage />, 'installers')

    await user.click(await screen.findByRole('button', { name: 'Yükleyici ekle' }))
    const urls = screen.getAllByLabelText("Yükleyici URL'si")
    await user.type(urls[1], 'http://x.dev/a.msi')
    expect(screen.getByText('https adresi gir.')).toBeInTheDocument()
    expect(screen.getByText('Plan için işaretli satırları düzelt.')).toBeInTheDocument()
    await user.clear(urls[1])
    await user.type(urls[1], 'https://x.dev/a.msi')
    await user.click(screen.getAllByRole('button', { name: 'Tür' })[1])
    await user.click(screen.getByRole('option', { name: 'MSI' }))
    await user.type(screen.getAllByLabelText('Diller')[1], 'tr-TR')
    await user.click(screen.getAllByText('Parametresiz sessiz kurulur')[1])
    await user.click(screen.getByRole('button', { name: 'Planla' }))

    await waitFor(() =>
      expect(lastRequestTo('/ms/change')?.body).toMatchObject({
        Change: {
          Kind: MsChangeKind.InstallerPackages,
          InstallerPackages: [
            { PackageUrl: 'https://indir.kuzeylab.dev/tarla/0.9.4/TarlaSetup.exe' },
            { PackageUrl: 'https://x.dev/a.msi', PackageType: 2, Languages: ['tr-tr'], IsSilentInstall: true, InstallerParameters: null },
          ],
        },
      }),
    )
  })

  it('removes a row and checks every signature', async () => {
    msiExe()
    const { user } = render(<MsInstallersPage />, 'installers')

    await user.click(await screen.findByRole('button', { name: 'Kaldır: https://indir.kuzeylab.dev/tarla/0.9.4/TarlaSetup.exe' }))
    expect(screen.getByText('Henüz yükleyici yok.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Bütün imzaları kontrol et' }))

    await waitFor(() => expect(lastRequestTo('/ms/packages/verify')?.body).toMatchObject({ PackageUrls: [] }))
  })
})

describe('Microsoft Store flights', () => {
  it('lists the flights with group names and records a group name', async () => {
    const { user } = render(<MsFlightsPage />, 'flights')

    expect(await screen.findByText('Kuzey içi')).toBeInTheDocument()
    const name = screen.getByLabelText('Ad · g-1')
    await user.clear(name)
    await user.click(screen.getByRole('button', { name: 'Kaydet: g-1' }))
    expect(screen.getByText('Gruba bir ad ver.')).toBeInTheDocument()
    await user.type(name, 'Çalışanlar')
    await user.click(screen.getByRole('button', { name: 'Kaydet: g-1' }))

    expect(await screen.findByText('Grup adı kaydedildi.')).toBeInTheDocument()
    expect(lastRequestTo('/ms/flight-group')?.body).toMatchObject({ GroupId: 'g-1', Name: 'Çalışanlar', MemberCount: 12 })
  })

  it('creates a flight through a plan after checking the name and groups', async () => {
    const { user } = render(<MsFlightsPage />, 'flights')

    await user.click(await screen.findByRole('button', { name: 'Uçuş oluştur' }))
    const dialog = await screen.findByRole('dialog', { name: 'Paket uçuşu oluştur' })
    await user.type(within(dialog).getByLabelText('Uçuş adı'), 'KUZEY İÇİ')
    await user.click(within(dialog).getByRole('button', { name: 'Planla' }))
    expect(within(dialog).getByText('Bu adda bir uçuş zaten var.')).toBeInTheDocument()
    expect(within(dialog).getByText('En az bir grup seç.')).toBeInTheDocument()
    await user.clear(within(dialog).getByLabelText('Uçuş adı'))
    await user.type(within(dialog).getByLabelText('Uçuş adı'), 'Erken Windows')
    await user.type(within(dialog).getByLabelText('Grup kimliği'), 'g-9')
    await user.click(within(dialog).getByRole('button', { name: 'Grup kimliği ekle' }))
    await user.click(within(dialog).getByRole('button', { name: 'Sıra' }))
    await user.click(screen.getByRole('option', { name: '"Kuzey içi" uçuşunun üstünde' }))
    await user.click(within(dialog).getByRole('button', { name: 'Planla' }))

    await screen.findByRole('dialog', { name: 'Microsoft Store Listings of 9P4K7Q2MZL1D' })
    expect(lastRequestTo('/ms/change')?.body).toMatchObject({
      Change: { Kind: MsChangeKind.CreateFlight, Flight: { FriendlyName: 'Erken Windows', GroupIds: ['g-9'], RankHigherThan: 'flight-1' } },
    })
  })

  it('shows an empty list to a viewer without actions', async () => {
    asRole(OrgRole.Viewer)
    msState.flights = []

    render(<MsFlightsPage />, 'flights')

    expect(await screen.findByText('Henüz uçuş yok.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Uçuş oluştur' })).not.toBeInTheDocument()
  })
})

describe('Microsoft Store add-ons', () => {
  it('marks the add-ons Partner Center owns and creates a new one through a plan', async () => {
    msState.addons = [
      msAddon(),
      msAddon({
        StoreId: '9X',
        Title: 'Jeton',
        Type: MsAddonType.Consumable,
        ApiWritable: false,
        AssistedReason: MsAssistedReason.StoreManagedConsumable,
        PriceId: null,
      }),
    ]
    const { user } = render(<MsAddonsPage />, 'addons')

    expect(await screen.findByText(/1 add-on API ile değiştirilemiyor/)).toBeInTheDocument()
    expect(screen.getByText('Asistanlı · Mağaza yönetimli tüketilebilir')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Düzenle: Jeton' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Add-on oluştur' }))
    const dialog = await screen.findByRole('dialog', { name: 'Add-on oluştur' })
    await user.click(within(dialog).getByRole('button', { name: 'Planla' }))
    expect(within(dialog).getByText('Ürün kimliğini gir.')).toBeInTheDocument()
    await user.type(within(dialog).getByLabelText('Ürün kimliği'), 'pusula.coins')
    await user.click(within(dialog).getByRole('button', { name: 'Tür' }))
    await user.click(screen.getByRole('option', { name: 'Tüketilebilir' }))
    await user.click(within(dialog).getByRole('button', { name: 'Planla' }))

    await waitFor(() =>
      expect(lastRequestTo('/ms/change')?.body).toMatchObject({
        Change: { Kind: MsChangeKind.CreateAddon, Addon: { ProductId: 'pusula.coins', ProductType: MsAddonType.Consumable } },
      }),
    )
  })

  it('edits the listing and tier of an add-on', async () => {
    msState.content = msContent({
      AddonListings: [{ Locale: 'tr-tr', Title: 'Pro', Description: 'Her şey' }],
      AddonLifetime: 'Forever',
      Pricing: { PriceId: 'Tier5' },
    })
    const { user } = render(<MsAddonsPage />, 'addons')

    await user.click(await screen.findByRole('button', { name: 'Düzenle: Pusula Pro' }))
    const dialog = await screen.findByRole('dialog', { name: 'Pusula Pro düzenle' })
    const title = await within(dialog).findByLabelText('Başlık · tr-tr')
    await user.clear(title)
    await user.type(title, 'Pusula Pro')
    await user.type(within(dialog).getByLabelText('Dil kodu'), 'EN-US')
    await user.click(within(dialog).getByRole('button', { name: 'Dil ekle' }))
    await user.type(within(dialog).getByLabelText('Başlık · en-us'), 'Pro')
    await user.clear(within(dialog).getByLabelText('Fiyat kademesi'))
    await user.type(within(dialog).getByLabelText('Fiyat kademesi'), 'Tier7')
    await user.click(within(dialog).getByRole('button', { name: 'Planla' }))

    await waitFor(() =>
      expect(lastRequestTo('/ms/change')?.body).toMatchObject({
        Change: {
          Kind: MsChangeKind.AddonContent,
          AddonStoreId: '9NBLGGH4R315',
          AddonContent: {
            Listings: [
              { Locale: 'tr-tr', Title: 'Pusula Pro' },
              { Locale: 'en-us', Title: 'Pro' },
            ],
            PriceId: 'Tier7',
            Lifetime: null,
          },
        },
      }),
    )
    expect(lastRequestTo('/ms/content')?.query).toMatchObject({ AddonStoreId: '9NBLGGH4R315' })
  })

  it('sends a pending add-on draft to certification', async () => {
    msState.addons = [msAddon({ PendingSubmissionId: '1152921505900' })]
    const { user } = render(<MsAddonsPage />, 'addons')

    await user.click(await screen.findByRole('button', { name: 'Sertifikasyona gönder' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.type(within(dialog).getByLabelText('9P4K7Q2MZL1D yaz'), '9P4K7Q2MZL1D')
    await user.click(within(dialog).getByRole('button', { name: 'Gönder' }))

    await waitFor(() =>
      expect(lastRequestTo('/ms/submission/submit')?.body).toMatchObject({
        Target: MsSubmissionTarget.Addon,
        AddonStoreId: '9NBLGGH4R315',
      }),
    )
  })

  it('shows the empty state', async () => {
    msState.addons = []

    render(<MsAddonsPage />, 'addons')

    expect(await screen.findByText('Henüz add-on yok.')).toBeInTheDocument()
  })
})

describe('Microsoft Store submissions', () => {
  it('shows the status history, errors and certification reports', async () => {
    msState.submissions = [
      msSubmission({
        Status: MsSubmissionStatus.CertificationFailed,
        ModifiedOutsideApi: true,
        RolloutPercentage: 25,
        StatusDetails: {
          Errors: [{ Code: 'InvalidPackage', Details: 'Paket imzalı değil.' }],
          Warnings: [{ Code: 'SlowStart', Details: 'Açılış yavaş.' }],
          CertificationReports: [{ Date: '2026-11-18T08:00:00Z', ReportUrl: 'https://partner.microsoft.com/report/1' }],
        },
      }),
    ]

    render(<MsSubmissionsPage />, 'submissions')

    const card = await screen.findByRole('article', { name: '2.3.0.0' })
    expect(within(card).getByText('Sertifikasyon başarısız')).toBeInTheDocument()
    expect(within(card).getByText('Gönderildi')).toBeInTheDocument()
    expect(within(card).getByText('Paket imzalı değil.')).toBeInTheDocument()
    expect(within(card).getByText('Açılış yavaş.')).toBeInTheDocument()
    expect(within(card).getByText("Partner Center'da değişti")).toBeInTheDocument()
    expect(within(card).getByText('Dağıtım %25')).toBeInTheDocument()
    expect(within(card).getByRole('link', { name: /raporu/ })).toHaveAttribute('href', 'https://partner.microsoft.com/report/1')
  })

  it('sends a flight draft with the typed Store id', async () => {
    msState.submissions = [
      msSubmission({
        Target: MsSubmissionTarget.Flight,
        FlightId: 'flight-1',
        FriendlyName: 'Kuzey içi 2.4',
        Status: MsSubmissionStatus.PendingCommit,
      }),
    ]
    const { user } = render(<MsSubmissionsPage />, 'submissions')

    await user.click(await screen.findByRole('button', { name: 'Sertifikasyona gönder' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText('Paket uçuşu · Kuzey içi 2.4')).toBeInTheDocument()
    await user.type(within(dialog).getByLabelText('9P4K7Q2MZL1D yaz'), 'yanlis')
    expect(within(dialog).getByText('Metin Store kimliğiyle eşleşmiyor.')).toBeInTheDocument()
    await user.clear(within(dialog).getByLabelText('9P4K7Q2MZL1D yaz'))
    await user.type(within(dialog).getByLabelText('9P4K7Q2MZL1D yaz'), '9P4K7Q2MZL1D')
    await user.click(within(dialog).getByRole('button', { name: 'Gönder' }))

    await waitFor(() =>
      expect(lastRequestTo('/ms/submission/submit')?.body).toMatchObject({ Target: MsSubmissionTarget.Flight, FlightId: 'flight-1' }),
    )
  })

  it('shows the empty state', async () => {
    msState.submissions = []

    render(<MsSubmissionsPage />, 'submissions')

    expect(await screen.findByText('Henüz gönderim yok.')).toBeInTheDocument()
  })
})
