import { act, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ActivityEventType, createManualSource } from '@/lib/activity/events'
import { FilterType } from '@/lib/api/list'
import { ActionChannel, OrgRole, StepDirection, StoreJobKind, StoreJobStatus, StoreJobStepStatus, StoreKind } from '@/lib/api/types'
import { JobDetailPage, JobsPage, jobsRequest } from '@/pages/activity/JobsPages'
import { activityEvent, app, job, jobDetail, jobStep, organization } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

const running = job({
  Id: 48214,
  Status: StoreJobStatus.Running,
  Store: StoreKind.Google,
  AppId: 101,
  Channel: ActionChannel.Mcp,
  RequestedByUserName: null,
  Summary: null,
  EndDate: null,
})

function renderDetail(jobId = 48213) {
  const source = createManualSource()
  const view = renderWithProviders(<JobDetailPage />, { route: `/activity/jobs/${jobId}`, path: '/activity/jobs/:jobId', activity: source })
  return { ...view, source }
}

describe('jobsRequest', () => {
  it('builds the list request from the address, newest first', () => {
    expect(jobsRequest(new URLSearchParams('status=4&store=2&channel=5&page=2&size=50'))).toEqual({
      PageNumber: 2,
      RowCount: 50,
      Sorting: { SortBy: 'Id', Type: 1 },
      Filtering: {
        Criterias: [
          { FilterBy: 'Status', Value: 4, Type: FilterType.EqualTo },
          { FilterBy: 'Store', Value: 2, Type: FilterType.EqualTo },
          { FilterBy: 'Channel', Value: 5, Type: FilterType.EqualTo },
        ],
      },
    })
    expect(jobsRequest(new URLSearchParams()).Filtering).toBeUndefined()
  })
})

describe('JobsPage', () => {
  it('lists the jobs with their state, attempt, duration and requester', async () => {
    testState.jobs = [job(), running]
    testState.apps = [app()]

    renderWithProviders(<JobsPage />, { route: '/activity/jobs' })

    const table = await screen.findByRole('table', { name: 'İşler' })
    const rows = within(table).getAllByRole('row')
    expect(within(rows[1]).getByText('Başarılı')).toBeInTheDocument()
    expect(within(rows[1]).getByText('1 / 3')).toBeInTheDocument()
    expect(within(rows[1]).getByText('1 dk 06 sn')).toBeInTheDocument()
    expect(within(rows[1]).getByText('ece@kuzeylab.dev')).toBeInTheDocument()
    expect(await within(rows[2]).findByText('Pusula · Uygulama ve build senkronu')).toBeInTheDocument()
    expect(within(rows[2]).getByText('Sürüyor')).toBeInTheDocument()
    expect(within(rows[2]).getByText('Sistem')).toBeInTheDocument()
    expect(within(rows[2]).getByText('MCP')).toBeInTheDocument()
  })

  it('updates a running row from the live stream', async () => {
    testState.jobs = [running]
    const source = createManualSource()
    renderWithProviders(<JobsPage />, { route: '/activity/jobs', activity: source })
    await screen.findByText('Sürüyor')

    act(() =>
      source.push(
        activityEvent({
          Type: ActivityEventType.JobProgress,
          JobId: 48214,
          JobKind: StoreJobKind.SyncApps,
          JobStatus: StoreJobStatus.Queued,
          Message: '503',
          Date: '2026-11-18T10:00:00Z',
        }),
      ),
    )

    expect(screen.getByText('Tekrar bekliyor')).toBeInTheDocument()
  })

  it('sends the status filter and offers to clear it', async () => {
    testState.jobs = []
    const { user } = renderWithProviders(<JobsPage />, { route: '/activity/jobs?status=4' })

    expect(await screen.findByRole('heading', { name: 'Bu filtrelere uyan iş yok' })).toBeInTheDocument()
    expect(requestsTo('/StoreJobs').map((request) => request.body)).toContainEqual(
      expect.objectContaining({ Filtering: { Criterias: [{ FilterBy: 'Status', Value: 4, Type: FilterType.EqualTo }] } }),
    )
    await user.click(screen.getAllByRole('button', { name: 'Filtreleri temizle' })[0])

    expect(screen.getByTestId('location')).toHaveTextContent(/^\/activity\/jobs$/)
  })

  it('says there is no job yet', async () => {
    testState.jobs = []

    renderWithProviders(<JobsPage />, { route: '/activity/jobs' })

    expect(await screen.findByRole('heading', { name: 'Henüz iş yok' })).toBeInTheDocument()
  })

  it('cancels a running job after the confirmation', async () => {
    testState.jobs = [running]
    const { user } = renderWithProviders(<JobsPage />, { route: '/activity/jobs' })

    await user.click(await screen.findByRole('button', { name: 'İş işlemleri' }))
    await user.click(screen.getByRole('menuitem', { name: 'İşi iptal et' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Bu iş iptal edilsin mi?' })
    await user.click(within(dialog).getByRole('button', { name: 'İşi iptal et' }))

    await waitFor(() => expect(lastRequestTo('/StoreJobs/job/cancel')?.body).toEqual({ OrganizationId: 7, JobId: 48214 }))
    expect(await screen.findByText('İş iptal edildi.')).toBeInTheDocument()
  })

  it('says a running job stops at its next step', async () => {
    testState.jobs = [running]
    server.use(endpoint('post', '/StoreJobs/job/cancel', () => Response.json({ IsSuccess: true, StatusCode: 200, Data: 2 })))
    const { user } = renderWithProviders(<JobsPage />, { route: '/activity/jobs' })

    await user.click(await screen.findByRole('button', { name: 'İş işlemleri' }))
    await user.click(screen.getByRole('menuitem', { name: 'İşi iptal et' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'İşi iptal et' }))

    expect(await screen.findByText('Durdurma istendi: iş bir sonraki adımda duracak.')).toBeInTheDocument()
  })

  it('reports a job that finished meanwhile instead of an error', async () => {
    testState.jobs = [running]
    server.use(endpoint('post', '/StoreJobs/job/cancel', () => errorResponse('StoreJobAlreadyFinished', 'İş bitti.', 409)))
    const { user } = renderWithProviders(<JobsPage />, { route: '/activity/jobs' })

    await user.click(await screen.findByRole('button', { name: 'İş işlemleri' }))
    await user.click(screen.getByRole('menuitem', { name: 'İşi iptal et' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'İşi iptal et' }))

    expect(await screen.findByText('İş zaten bitmişti.')).toBeInTheDocument()
  })

  it('does not offer cancel for a finished job, nor to a viewer', async () => {
    testState.jobs = [job(), running]
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    const { user } = renderWithProviders(<JobsPage />, { route: '/activity/jobs' })

    const buttons = await screen.findAllByRole('button', { name: 'İş işlemleri' })
    await user.click(buttons[1])

    expect(screen.getByRole('menuitem', { name: 'Ayrıntıyı aç' })).toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: 'İşi iptal et' })).not.toBeInTheDocument()
  })

  it('opens a job from its row', async () => {
    const { user } = renderWithProviders(<JobsPage />, { route: '/activity/jobs' })

    await user.click(await screen.findByText('5 apps, 3 new builds.'))

    expect(screen.getByTestId('location')).toHaveTextContent('/activity/jobs/48213')
  })
})

describe('JobDetailPage', () => {
  it('shows the steps as a timeline with direction, state and progress, and the job details', async () => {
    renderDetail()

    expect(await screen.findByRole('heading', { name: 'Uygulama ve build senkronu', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('3 adım')).toBeInTheDocument()
    expect(screen.getAllByText('GİDEN')).toHaveLength(2)
    expect(screen.getByText('İÇ')).toBeInTheDocument()
    expect(screen.getByText('5 / 5 · 42 builds.')).toBeInTheDocument()
    expect(screen.getByRole('progressbar', { name: 'builds.list' })).toHaveAttribute('aria-valuenow', '5')
    expect(screen.getByText('1 dk 06 sn · iş 48213')).toBeInTheDocument()
    expect(screen.getByText(/"ConnectionId": 11/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'İşi iptal et' })).not.toBeInTheDocument()
  })

  it('marks the attempt of each step and shows the last error of a failed job', async () => {
    testState.jobDetail = jobDetail({
      Status: StoreJobStatus.Failed,
      Attempt: 2,
      Error: '403 The caller does not have permission',
      Steps: [
        jobStep({ Status: StoreJobStepStatus.Failed, Message: '503' }),
        jobStep({ Attempt: 2, Order: 2, Status: StoreJobStepStatus.Failed, Message: '403', Direction: StepDirection.Inbound }),
      ],
    })

    renderDetail()

    expect(await screen.findByText('403 The caller does not have permission')).toBeInTheDocument()
    expect(screen.getAllByText('HATA')).toHaveLength(2)
    expect(screen.getByText(/deneme 2/)).toBeInTheDocument()
    expect(screen.getByText('2 / 3')).toBeInTheDocument()
  })

  it('cancels a running job and reads it again when the stream reports a change', async () => {
    testState.jobDetail = jobDetail({ Status: StoreJobStatus.Running, EndDate: null })
    const { user, source } = renderDetail()

    await user.click(await screen.findByRole('button', { name: 'İşi iptal et' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'İşi iptal et' }))
    await waitFor(() => expect(requestsTo('/StoreJobs/job/cancel')).toHaveLength(1))
    const reads = requestsTo('/StoreJobs/job').length
    act(() =>
      source.push(
        activityEvent({
          Type: ActivityEventType.JobCancelled,
          JobId: 48213,
          JobStatus: StoreJobStatus.Cancelled,
          Date: '2026-11-18T09:05:00Z',
        }),
      ),
    )

    await waitFor(() => expect(requestsTo('/StoreJobs/job').length).toBeGreaterThan(reads))
    expect(screen.getByText('İptal edildi')).toBeInTheDocument()
  })

  it('says the job is not found', async () => {
    renderDetail(1)

    expect(await screen.findByRole('heading', { name: 'Kayıt bulunamadı' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'İşler' })).toHaveAttribute('href', '/activity/jobs')
  })

  it('links the app the job worked on', async () => {
    testState.jobDetail = jobDetail({ AppId: 101 })
    testState.apps = [app()]

    renderDetail()

    expect(await screen.findByRole('link', { name: 'Pusula' })).toHaveAttribute('href', '/apps/101')
  })

  it('shows the error state when the job cannot be read', async () => {
    server.use(endpoint('get', '/StoreJobs/job', () => errorResponse('UnhandledException', 'Sunucu hatası.', 500)))

    renderDetail()

    expect(await screen.findByText('Sunucu hatası.')).toBeInTheDocument()
  })
})
