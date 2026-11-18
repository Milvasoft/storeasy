import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { StudioRoutes } from '@/features/studio/StudioRoutes'
import type { MockupSpec } from '@/features/studio/types'
import { OrgRole } from '@/lib/api/types'
import { organization } from '../../support/factories'
import { endpoint, errorResponse, STORAGE_URL } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'
import { setupStudioApi, studioState, templateDetail } from '../../support/studio'

setupStudioApi()

function render(route = '/studio/12') {
  return renderWithProviders(<StudioRoutes />, { route, path: '/studio/*' })
}

async function opened() {
  return screen.findByRole('heading', { name: 'Mağaza seti 2026', level: 1 })
}

function lastPreviewSpec(): MockupSpec {
  return (lastRequestTo('/Mockups/render-preview')?.body as { Spec: MockupSpec }).Spec
}

describe('template editor: design', () => {
  it('draws the first page of slots in the primary language and design class', async () => {
    render()
    await opened()

    expect(await screen.findByRole('img', { name: 'Slot 1 önizlemesi' })).toHaveAttribute('src', 'data:image/png;base64,slot1')
    expect(lastRequestTo('/Mockups/render-preview')?.body).toMatchObject({
      OrganizationId: 7,
      TemplateId: 12,
      Locale: 'tr',
      ClassKey: 'apple.screenshots.iphone-dynamic-island-medium',
      SlotOrders: [1, 2, 3],
      MaxWidth: 360,
    })
  })

  it('previews an unsaved change once the typing settles, and counts it as unsaved', async () => {
    render()
    await opened()
    await screen.findByRole('img', { name: 'Slot 1 önizlemesi' })
    const before = requestsTo('/Mockups/render-preview').length

    fireEvent.focus(screen.getByLabelText('Renk'))
    fireEvent.change(screen.getByLabelText('Renk'), { target: { value: '#14B8A6' } })

    await waitFor(() => expect(lastPreviewSpec().Background?.Colors?.[0]).toBe('#14B8A6'))
    expect(requestsTo('/Mockups/render-preview').length).toBe(before + 1)
    expect(screen.getByText('1 kaydedilmemiş değişiklik')).toBeInTheDocument()
  })

  it('saves with the version it started from and takes the answered version for the next save', async () => {
    const { user } = render()
    await opened()

    await user.click(screen.getByRole('button', { name: 'Cihaz' }))
    fireEvent.focus(screen.getByLabelText('Ölçek (%)'))
    fireEvent.change(screen.getByLabelText('Ölçek (%)'), { target: { value: '74' } })
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(lastRequestTo('/Mockups/template')?.method).toBe('PUT'))
    const first = lastRequestTo('/Mockups/template')?.body as { Version: number; Spec: MockupSpec; Slots: unknown[]; SlotCount: number }
    expect(first.Version).toBe(4)
    expect(first.Spec.Frame?.Scale).toBe(0.74)
    expect(first.SlotCount).toBe(3)
    expect(await screen.findByText('Şablon kaydedildi.')).toBeInTheDocument()
    expect(screen.queryByText(/kaydedilmemiş değişiklik/)).not.toBeInTheDocument()

    fireEvent.focus(screen.getByLabelText('Ölçek (%)'))
    fireEvent.change(screen.getByLabelText('Ölçek (%)'), { target: { value: '70' } })
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(requestsTo('/Mockups/template').filter((request) => request.method === 'PUT')).toHaveLength(2))
    expect((lastRequestTo('/Mockups/template')?.body as { Version: number }).Version).toBe(5)
    expect(requestsTo('/Mockups/template').filter((request) => request.method === 'GET')).toHaveLength(1)
  })

  it('offers to open the other version or keep mine as a new template when the version is stale (409)', async () => {
    server.use(endpoint('put', '/Mockups/template', () => errorResponse('MockupTemplateChanged', 'Şablon değişti.', 409)))
    const { user } = render()
    await opened()

    await user.click(screen.getByRole('button', { name: 'Şablon' }))
    await user.clear(screen.getByLabelText('Şablon adı'))
    await user.type(screen.getByLabelText('Şablon adı'), 'Benim set')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Bu şablonu arada başka biri kaydetti' })
    studioState.template = templateDetail({ Name: 'Ece seti', Version: 6 })
    await user.click(within(dialog).getByRole('button', { name: 'Onun sürümünü aç' }))

    expect(await screen.findByRole('heading', { name: 'Ece seti', level: 1 })).toBeInTheDocument()
    expect(screen.queryByText(/kaydedilmemiş değişiklik/)).not.toBeInTheDocument()
  })

  it('keeps my version as a copy of the template when I choose so', async () => {
    let puts = 0
    server.use(
      endpoint('put', '/Mockups/template', () => {
        puts += 1
        return puts === 1
          ? errorResponse('MockupTemplateChanged', 'Şablon değişti.', 409)
          : HttpResponse.json({ IsSuccess: true, StatusCode: 200, Data: 2 })
      }),
    )
    const { user } = render()
    await opened()

    await user.click(screen.getByRole('button', { name: 'Şablon' }))
    await user.type(screen.getByLabelText('Şablon adı'), ' B')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Benimkini yeni şablon olarak kaydet' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/studio/40'))
    expect(requestsTo('/Mockups/template').find((request) => request.method === 'POST')?.body).toMatchObject({
      CopyFromTemplateId: 12,
      CopyCaptions: true,
      Name: 'Mağaza seti 2026 B (benim)',
    })
    const saves = requestsTo('/Mockups/template').filter((request) => request.method === 'PUT')
    expect(saves.at(-1)?.body).toMatchObject({ TemplateId: 40, Version: 4, Name: 'Mağaza seti 2026' })
  })

  it('uploads a tablet screenshot for one slot and saves it in the slots', async () => {
    server.use(
      http.put(
        `${STORAGE_URL}/*`,
        () => new HttpResponse(null, { status: 200, headers: { 'Access-Control-Allow-Origin': '*', ETag: '"e"' } }),
      ),
    )
    const { user } = render()
    await opened()

    const tablet = screen.getByRole('group', { name: 'Tablet' })
    expect(within(tablet).getByText('Yok; telefon görüntüsü kullanılır')).toBeInTheDocument()
    await user.click(within(tablet).getByRole('button', { name: 'Tablet yükle' }))
    const input = tablet.querySelector('input[type="file"]') as HTMLInputElement
    await user.upload(input, new File([new Uint8Array(4)], 'ipad-01.png', { type: 'image/png' }))

    expect(await within(screen.getByRole('group', { name: 'Tablet' })).findByText('01-home.png')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    await waitFor(() => expect(lastRequestTo('/Mockups/template')?.method).toBe('PUT'))
    expect((lastRequestTo('/Mockups/template')?.body as { Slots: unknown[] }).Slots).toEqual([
      { Order: 1, Family: 1, Locale: null, AssetId: 901 },
      { Order: 1, Family: 2, Locale: null, AssetId: 501 },
    ])
  })

  it('adds a badge layer with a text per language', async () => {
    const { user } = render()
    await opened()

    await user.click(screen.getByRole('button', { name: 'Rozet ekle' }))
    await user.type(screen.getByLabelText('Rozet metni, tr'), 'YENİ')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(lastRequestTo('/Mockups/template')?.method).toBe('PUT'))
    const spec = (lastRequestTo('/Mockups/template')?.body as { Spec: MockupSpec }).Spec
    expect(spec.Layers).toEqual([expect.objectContaining({ Type: 'badge', Texts: { '*': 'NEW', tr: 'YENİ' } })])
  })

  it('adds and removes slots down to one', async () => {
    const { user } = render()
    await opened()

    await user.click(screen.getByRole('button', { name: 'Slot ekle' }))
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))
    await waitFor(() => expect(lastRequestTo('/Mockups/template')?.method).toBe('PUT'))
    expect(lastRequestTo('/Mockups/template')?.body).toMatchObject({ SlotCount: 4 })
    for (let index = 0; index < 3; index += 1) {
      await user.click(screen.getByRole('button', { name: 'Son slotu kaldır' }))
    }

    expect(screen.getByRole('button', { name: 'Son slotu kaldır' })).toBeDisabled()
  })

  it('shows the preview warnings with a way to the captions', async () => {
    server.use(
      endpoint('post', '/Mockups/render-preview', () =>
        HttpResponse.json({
          IsSuccess: true,
          StatusCode: 200,
          Data: {
            DurationMs: 120,
            Images: [
              {
                SlotOrder: 1,
                Width: 180,
                Height: 390,
                Url: null,
                Warnings: [{ Code: 'CaptionOverflow', Locale: 'tr', SlotOrder: 1, Field: 'title', Lines: 3, MaxLines: 2 }],
              },
            ],
          },
        }),
      ),
    )
    render()
    await opened()

    expect(await screen.findByText(/Başlık 3 satır tutuyor; stil 2 satıra izin veriyor./)).toBeInTheDocument()
    expect(screen.getByText('Bu slot çizilemedi.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Başlıklara git' })).toHaveAttribute('href', '/studio/12/captions')
  })

  it('lets a viewer preview but not change or save', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    render()
    await opened()

    expect(screen.getByText(/İzleyici rolündesin/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kaydet' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Renk')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Tablet yükle' })).not.toBeInTheDocument()
    expect(await screen.findByRole('img', { name: 'Slot 1 önizlemesi' })).toBeInTheDocument()
  })

  it('shows the not found state for a missing template', async () => {
    server.use(endpoint('get', '/Mockups/template', () => errorResponse('MockupTemplateNotFound', 'Şablon bulunamadı.', 404)))
    render()

    expect(await screen.findByRole('link', { name: 'Stüdyoya dön' })).toHaveAttribute('href', '/studio')
  })
})
