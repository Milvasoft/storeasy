import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StudioPage } from '@/features/studio/StudioPage'
import { OrgRole } from '@/lib/api/types'
import { app, organization } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, testState } from '../../support/state'
import { setupStudioApi, studioState } from '../../support/studio'

setupStudioApi()

function render(route = '/studio') {
  testState.apps = [app(), app({ Id: 102, Name: 'Kumbara', Stores: [] }), app({ Id: 103, Name: 'Halka', Stores: [] })]
  return renderWithProviders(<StudioPage />, { route })
}

describe('StudioPage', () => {
  it('lists the templates with their app, languages and last export, and links each to its editor', async () => {
    render()

    const link = await screen.findByRole('link', { name: 'Mağaza seti 2026' })
    expect(link).toHaveAttribute('href', '/studio/12')
    expect(screen.getByText(/162 görsel/)).toBeInTheDocument()
    expect(screen.getByText('Henüz dışa aktarılmadı')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tümü · 2' })).toBeInTheDocument()
  })

  it('filters by app and offers the empty app its own template or a copy of another', async () => {
    const { user } = render()
    await screen.findByRole('link', { name: 'Mağaza seti 2026' })

    await user.click(screen.getByRole('button', { name: 'Halka · 0' }))

    expect(screen.getByRole('heading', { name: 'Halka için henüz şablon yok' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Halka için şablon oluştur' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Mağaza seti 2026 şablonunu kopyala' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yeni şablon' })).not.toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/studio?app=103')
  })

  it('says who can create a template to a viewer, with no create action', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    studioState.templates = []

    render()

    expect(await screen.findByText('Şablonu bir editör oluşturabilir.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /şablon/i })).not.toBeInTheDocument()
  })

  it('creates a template from a starter and opens its editor', async () => {
    const { user } = render()
    await screen.findByRole('link', { name: 'Mağaza seti 2026' })

    await user.click(screen.getByRole('button', { name: 'Yeni şablon' }))
    const dialog = await screen.findByRole('dialog', { name: 'Yeni şablon' })
    await user.type(within(dialog).getByLabelText('Şablon adı'), 'Plus tanıtımı')
    await user.click(await within(dialog).findByRole('radio', { name: /Klasik/ }))
    await user.clear(within(dialog).getByLabelText('Diller'))
    await user.type(within(dialog).getByLabelText('Diller'), 'tr, en-US')
    await user.click(within(dialog).getByRole('button', { name: 'Şablonu oluştur' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/studio/40'))
    expect(lastRequestTo('/Mockups/template')?.body).toEqual({
      OrganizationId: 7,
      AppId: 101,
      Name: 'Plus tanıtımı',
      Locales: ['tr', 'en-US'],
      PrimaryLocale: 'tr',
      SlotCount: null,
      StarterKey: 'classic',
      CopyFromTemplateId: null,
      CopyCaptions: false,
    })
    expect(lastRequestTo('/Mockups/template')?.headers['idempotency-key']).toBeTruthy()
  })

  it('marks the name field when the app already has a template with that name', async () => {
    server.use(endpoint('post', '/Mockups/template', () => errorResponse('MockupTemplateNameExists', 'Bu adla bir şablon var.', 409)))
    const { user } = render()
    await screen.findByRole('link', { name: 'Mağaza seti 2026' })

    await user.click(screen.getByRole('button', { name: 'Yeni şablon' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Şablon adı'), 'Mağaza seti 2026')
    await user.click(within(dialog).getByRole('button', { name: 'Şablonu oluştur' }))

    expect(await within(dialog).findByText('Bu uygulamada bu adla bir şablon var.')).toBeInTheDocument()
  })

  it('refuses a malformed locale tag before sending', async () => {
    const { user } = render()
    await screen.findByRole('link', { name: 'Mağaza seti 2026' })

    await user.click(screen.getByRole('button', { name: 'Yeni şablon' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Şablon adı'), 'X')
    await user.type(within(dialog).getByLabelText('Diller'), ', Deutsch')
    await user.click(within(dialog).getByRole('button', { name: 'Şablonu oluştur' }))

    expect(within(dialog).getByText('Deutsch bir dil etiketi değil.')).toBeInTheDocument()
    expect(lastRequestTo('/Mockups/template')).toBeUndefined()
  })

  it('copies a template with its captions from the card menu', async () => {
    const { user } = render()
    await screen.findByRole('link', { name: 'Mağaza seti 2026' })

    await user.click(screen.getByRole('button', { name: 'Mağaza seti 2026 eylemleri' }))
    await user.click(screen.getByRole('menuitem', { name: 'Kopyala' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByLabelText('Şablon adı')).toHaveValue('Mağaza seti 2026 kopyası')
    await user.click(within(dialog).getByRole('button', { name: 'Şablonu oluştur' }))

    await waitFor(() =>
      expect(lastRequestTo('/Mockups/template')?.body).toMatchObject({ CopyFromTemplateId: 12, CopyCaptions: true, StarterKey: null }),
    )
  })

  it('deletes a template after the confirmation', async () => {
    const { user } = render()
    await screen.findByRole('link', { name: 'Mağaza seti 2026' })

    await user.click(screen.getByRole('button', { name: 'Mağaza seti 2026 eylemleri' }))
    await user.click(screen.getByRole('menuitem', { name: 'Sil' }))
    const confirm = await screen.findByRole('alertdialog')
    await user.click(within(confirm).getByRole('button', { name: 'Sil' }))

    await waitFor(() => expect(lastRequestTo('/Mockups/template')?.method).toBe('DELETE'))
    expect(lastRequestTo('/Mockups/template')?.query).toEqual({ OrganizationId: '7', TemplateId: '12' })
  })
})
