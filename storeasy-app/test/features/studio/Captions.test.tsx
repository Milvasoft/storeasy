import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StudioRoutes } from '@/features/studio/StudioRoutes'
import { OrgRole } from '@/lib/api/types'
import { organization } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, testState } from '../../support/state'
import { setupStudioApi, studioState } from '../../support/studio'

setupStudioApi()

function render(flags: { StudioAiCaptions?: boolean } = {}) {
  return renderWithProviders(<StudioRoutes />, { route: '/studio/12/captions', path: '/studio/*', flags })
}

async function grid() {
  return screen.findByRole('table', { name: 'Slot ve dile göre başlıklar' })
}

describe('template editor: captions', () => {
  it('shows a cell per slot and language with its soft counters', async () => {
    render()

    await grid()
    expect(screen.getByLabelText('Slot 1 başlığı, tr')).toHaveValue('Hedeflerin tek bakışta')
    expect(screen.getByLabelText('Slot 1 başlığı, en-US')).toHaveValue('Your goals at a glance')
    expect(screen.getByLabelText('Slot 2 başlığı, en-US')).toHaveAttribute('placeholder', "Boş · tr'den çizilir")
    expect(screen.getAllByText('22 / 30')).toHaveLength(2)
  })

  it('saves only the edited cells and shows the overflow the server measured', async () => {
    studioState.captionsSaved = {
      Saved: 1,
      MeasuredClassKey: 'google.screenshots.phone',
      Warnings: [
        {
          Code: 'CaptionOverflow',
          Locale: 'en-US',
          SlotOrder: 2,
          Field: 'title',
          Lines: 3,
          MaxLines: 2,
          ClassKey: 'google.screenshots.phone',
        },
      ],
    }
    const { user } = render()
    await grid()

    await user.type(screen.getByLabelText('Slot 2 başlığı, en-US'), 'Check off every single step today')
    expect(screen.getByText('Düzenlendi · kaydedilmedi')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(lastRequestTo('/Mockups/captions')).toBeDefined())
    expect(lastRequestTo('/Mockups/captions')?.body).toEqual({
      OrganizationId: 7,
      TemplateId: 12,
      Items: [{ Locale: 'en-US', SlotOrder: 2, Title: 'Check off every single step today', Subtitle: '' }],
    })
    expect(await screen.findByText('Başlık 3 satır tutuyor; stil 2 satıra izin veriyor.')).toBeInTheDocument()
    expect(screen.getByText('En dar dışa aktarım sınıfında ölçüldü: google.screenshots.phone')).toBeInTheDocument()
    expect(lastRequestTo('/Mockups/template')?.method).toBe('GET')
  })

  it('narrows the grid to the problem rows', async () => {
    studioState.captionsSaved = {
      Saved: 1,
      MeasuredClassKey: null,
      Warnings: [{ Code: 'CaptionOverflow', Locale: 'tr', SlotOrder: 2, Lines: 3, MaxLines: 2 }],
    }
    const { user } = render()
    await grid()
    await user.type(screen.getByLabelText('Slot 2 başlığı, tr'), '!')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    await screen.findByText(/stil 2 satıra izin veriyor/)

    await user.click(screen.getByRole('button', { name: 'Yalnız sorunlu · 1' }))

    expect(screen.queryByLabelText('Slot 1 başlığı, tr')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Slot 2 başlığı, tr')).toBeInTheDocument()
  })

  it('copies one language into the empty cells of another', async () => {
    const { user } = render()
    await grid()

    await user.click(screen.getByRole('button', { name: 'Dilden kopyala' }))

    expect(screen.getByLabelText('Slot 2 başlığı, en-US')).toHaveValue('Her adımı işaretle')
    expect(screen.getByLabelText('Slot 1 başlığı, en-US')).toHaveValue('Your goals at a glance')
  })

  it('hides the AI suggestion while the platform flag is off', async () => {
    render()
    await grid()

    expect(screen.queryByRole('button', { name: 'Yapay zeka önerisi' })).not.toBeInTheDocument()
  })

  it('puts accepted AI suggestions into the grid as unsaved cells', async () => {
    studioState.suggestion = {
      TargetLocale: 'en-US',
      Items: [
        { SlotOrder: 2, Field: 'title', Before: '', After: 'Check off every step', Reason: 'Close to the Turkish' },
        { SlotOrder: 3, Field: 'title', Before: '', After: 'See your progress', Reason: null },
      ],
    }
    const { user } = render({ StudioAiCaptions: true })
    await grid()

    await user.click(screen.getByRole('button', { name: 'Yapay zeka önerisi' }))
    const drawer = await screen.findByRole('dialog', { name: 'en-US için öneri' })
    await user.click(within(drawer).getByRole('button', { name: 'Öner' }))
    await user.click(await within(drawer).findByRole('checkbox', { name: 'Slot 3 · Başlık' }))
    await user.click(within(drawer).getByRole('button', { name: '1 öneriyi ızgaraya ekle' }))

    expect(lastRequestTo('/Mockups/captions/suggest')?.body).toEqual({
      OrganizationId: 7,
      TemplateId: 12,
      TargetLocale: 'en-US',
      SourceLocales: ['tr'],
      SlotOrders: [2, 3],
    })
    expect(screen.getByLabelText('Slot 2 başlığı, en-US')).toHaveValue('Check off every step')
    expect(screen.getByLabelText('Slot 3 başlığı, en-US')).toHaveValue('')
    expect(lastRequestTo('/Mockups/captions')).toBeUndefined()
  })

  it('turns the AI suggestion off for the session when the platform answers 503', async () => {
    server.use(
      endpoint('post', '/Mockups/captions/suggest', () =>
        errorResponse('FeatureDisabled', 'Platform yöneticisi bu özelliği kapattı.', 503),
      ),
    )
    const { user } = render({ StudioAiCaptions: true })
    await grid()

    await user.click(screen.getByRole('button', { name: 'Yapay zeka önerisi' }))
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Öner' }))

    expect(await screen.findByText('Yapay zeka önerisi şu an kapalı')).toBeInTheDocument()
    expect(screen.getByText('Platform yöneticisi bu özelliği kapattı.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Yapay zeka önerisi' })).toBeDisabled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('keeps the cells read only for a viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    render({ StudioAiCaptions: true })
    await grid()

    expect(screen.getByLabelText('Slot 1 başlığı, tr')).toHaveAttribute('readonly')
    expect(screen.queryByRole('button', { name: 'Dilden kopyala' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yapay zeka önerisi' })).not.toBeInTheDocument()
  })
})
