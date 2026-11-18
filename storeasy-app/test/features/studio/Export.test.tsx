import { act, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { StudioRoutes } from '@/features/studio/StudioRoutes'
import { ActivityEventType, createManualSource } from '@/lib/activity/events'
import { OrgRole, StepDirection, StoreJobStatus, StoreJobStepStatus, StoreKind, type StoreJobKind } from '@/lib/api/types'
import { setupContentApi } from '../../support/content'
import { activityEvent, app, jobDetail, jobStep, organization, storeRecord } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'
import { manifest, setupStudioApi, studioState } from '../../support/studio'

setupStudioApi()
setupContentApi()

const EXPORT_KIND = 60 as StoreJobKind

function render(route: string, stores = [storeRecord(), storeRecord({ StoreAppId: 202, Store: StoreKind.Google })]) {
  const source = createManualSource()
  testState.apps = [app({ Stores: stores })]
  const view = renderWithProviders(<StudioRoutes />, { route, path: '/studio/*', activity: source })
  return { ...view, source }
}

function exportJob(status: StoreJobStatus) {
  return jobDetail({
    Id: 8812,
    Kind: EXPORT_KIND,
    Status: status,
    AppId: 101,
    Steps: [
      jobStep({ Order: 1, Name: 'assets.load', Direction: StepDirection.Internal, Message: '6 screenshots read.' }),
      jobStep({
        Order: 2,
        Name: 'render.size',
        Direction: StepDirection.Internal,
        Status: StoreJobStepStatus.Running,
        Progress: 2,
        Total: 6,
        Message: 'iPhone DI medium · 1206x2622',
      }),
    ],
  })
}

describe('template editor: export plan', () => {
  it('lists the classes by store with their requirement, and explains the refused sizes', async () => {
    render('/studio/12/export')

    expect(await screen.findByText('12 görsel')).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'iPhone DI orta' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'iPhone Duo iç ekran' })).not.toBeChecked()
    expect(screen.getByText(/2027 itibarıyla zorunlu./)).toBeInTheDocument()
    expect(screen.getByText('The 6.1"/6.3" slot accepts only 1206x2622 and 1179x2556.')).toBeInTheDocument()
    expect(screen.getByText('1 / 2 seçili')).toBeInTheDocument()
    expect(lastRequestTo('/Mockups/export/plan')?.body).toEqual({
      OrganizationId: 7,
      TemplateId: 12,
      Locales: ['tr', 'en-US'],
      ClassKeys: null,
    })
  })

  it('plans again with the chosen classes and languages', async () => {
    const { user } = render('/studio/12/export')
    await screen.findByText('12 görsel')

    await user.click(screen.getByRole('checkbox', { name: 'iPhone Duo iç ekran' }))
    await waitFor(() =>
      expect(lastRequestTo('/Mockups/export/plan')?.body).toMatchObject({
        ClassKeys: ['apple.screenshots.iphone-dynamic-island-medium', 'google.screenshots.phone', 'apple.screenshots.iphone-duo-inner'],
      }),
    )
    await user.click(screen.getByRole('button', { name: 'en-US' }))

    await waitFor(() => expect(lastRequestTo('/Mockups/export/plan')?.body).toMatchObject({ Locales: ['tr'] }))
  })

  it('starts the export with the planned selection and follows the job', async () => {
    testState.jobDetail = exportJob(StoreJobStatus.Running)
    const { user } = render('/studio/12/export')
    await screen.findByText('12 görsel')

    await user.click(screen.getByRole('button', { name: 'Görselleri üret' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/studio/12/export/8812'))
    expect(lastRequestTo('/Mockups/export')?.body).toEqual({ OrganizationId: 7, TemplateId: 12, Locales: ['tr', 'en-US'], ClassKeys: null })
    expect(lastRequestTo('/Mockups/export')?.headers['idempotency-key']).toBeTruthy()
    expect(await screen.findByRole('heading', { name: 'Görseller üretiliyor' })).toBeInTheDocument()
  })

  it('asks to save first while the template has unsaved changes', async () => {
    const { user } = render('/studio/12/captions')
    await user.type(await screen.findByLabelText('Slot 3 başlığı, tr'), 'Yeni')

    await user.click(screen.getByRole('link', { name: 'Dışa aktar' }))

    expect(await screen.findByText('Önce şablonu kaydet: dışa aktarım kaydedilmiş sürümü çizer.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Görselleri üret' })).toBeDisabled()
  })

  it('shows the plan but no export action to a viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    render('/studio/12/export')

    expect(await screen.findByText('12 görsel')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Görselleri üret' })).not.toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'iPhone DI orta' })).toBeDisabled()
  })
})

describe('export job', () => {
  it('shows the render progress live and the manifest when the job succeeds', async () => {
    testState.jobDetail = exportJob(StoreJobStatus.Running)
    const { source } = render('/studio/12/export/8812')

    expect(await screen.findByText('2 / 6 görsel')).toBeInTheDocument()
    testState.jobDetail = exportJob(StoreJobStatus.Running)
    testState.jobDetail.Steps![1] = { ...testState.jobDetail.Steps![1], Progress: 5 }
    act(() =>
      source.push(
        activityEvent({
          Type: ActivityEventType.JobStep,
          JobId: 8812,
          JobKind: EXPORT_KIND,
          JobStatus: StoreJobStatus.Running,
          StepOrder: 2,
          StepName: 'render.size',
          StepStatus: StoreJobStepStatus.Running,
          Progress: 5,
          Total: 6,
        }),
      ),
    )
    expect(await screen.findByText('5 / 6 görsel')).toBeInTheDocument()

    act(() =>
      source.push(
        activityEvent({ Type: ActivityEventType.JobSucceeded, JobId: 8812, JobKind: EXPORT_KIND, JobStatus: StoreJobStatus.Succeeded }),
      ),
    )

    expect(await screen.findByRole('heading', { name: '9 görsel hazır' })).toBeInTheDocument()
    expect(lastRequestTo('/Mockups/export/manifest')?.query).toEqual({ OrganizationId: '7', JobId: '8812' })
    expect(screen.getByText('1 görsel üretilemedi')).toBeInTheDocument()
  })

  it('opens the zip through its signed URL', async () => {
    testState.jobDetail = exportJob(StoreJobStatus.Succeeded)
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    const { user } = render('/studio/12/export/8812')

    await user.click(await screen.findByRole('button', { name: 'Zip indir' }))

    await waitFor(() => expect(open).toHaveBeenCalledWith('https://storage.test/bucket/mockups-8812.zip', '_blank', 'noopener'))
  })

  it('plans each set that has a store key, in display order, and links the created plans', async () => {
    testState.jobDetail = exportJob(StoreJobStatus.Succeeded)
    const { user } = render('/studio/12/export/8812')
    const table = await screen.findByRole('table', { name: 'Dışa aktarılan setler' })
    expect(within(table).getByText("Henüz yüklenemez: mağaza API'sinde bu sınıfın anahtarı yok.")).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Yükleme planlarına gönder · 2' }))

    await waitFor(() => expect(requestsTo('/Screenshots/plan')).toHaveLength(2))
    expect(requestsTo('/Screenshots/plan').map((request) => request.body)).toEqual([
      {
        OrganizationId: 7,
        StoreAppId: 201,
        Locale: 'tr',
        ClassKey: 'APP_IPHONE_61',
        Order: [],
        Deletes: [],
        AssetIds: [901, 902, 903],
        DryRun: true,
      },
      {
        OrganizationId: 7,
        StoreAppId: 202,
        Locale: 'tr',
        ClassKey: 'phoneScreenshots',
        Order: [],
        Deletes: [],
        AssetIds: [911, 912, 913],
        DryRun: true,
      },
    ])
    expect(await screen.findByText('2 yükleme planından 2 tanesi oluştu')).toBeInTheDocument()
    expect(within(table).getAllByRole('link', { name: 'Plan #7001' })[0]).toHaveAttribute('href', '/apps/101/listing/plans/7001')
  })

  it('says when the app has no record in a store of the manifest', async () => {
    testState.jobDetail = exportJob(StoreJobStatus.Succeeded)
    studioState.manifest = manifest({ Sets: [manifest().Sets![1]] })
    const { user } = render('/studio/12/export/8812', [storeRecord()])
    await screen.findByRole('table', { name: 'Dışa aktarılan setler' })

    await user.click(screen.getByRole('button', { name: 'Yükleme planlarına gönder · 1' }))

    expect(await screen.findByText('Uygulamanın bu mağazada kaydı yok.')).toBeInTheDocument()
  })

  it('shows a failed job with its error', async () => {
    testState.jobDetail = { ...exportJob(StoreJobStatus.Failed), Error: 'Storage is not configured.' }
    render('/studio/12/export/8812')

    expect(await screen.findByText('Storage is not configured.')).toBeInTheDocument()
    expect(screen.getByText('Başarısız')).toBeInTheDocument()
  })
})
