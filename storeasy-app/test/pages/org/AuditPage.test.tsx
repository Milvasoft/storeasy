import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ActionChannel, AuditAction, OrgRole } from '@/lib/api/types'
import { AuditPage } from '@/pages/org/AuditPage'
import { OrgSettingsPage } from '@/pages/org/OrgSettingsPage'
import { auditLog, organization, TEST_ORG_ID } from '../../support/factories'
import { endpoint, errorResponse, listEnvelope } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, testState } from '../../support/state'

describe('AuditPage', () => {
  it('asks for the first page newest first and lists the rows', async () => {
    renderWithProviders(<AuditPage />, { route: '/audit' })

    expect(await screen.findByText('Üye davet edildi')).toBeInTheDocument()
    expect(lastRequestTo('/Organizations/audit-logs')?.body).toEqual({
      OrganizationId: TEST_ORG_ID,
      PageNumber: 1,
      RowCount: 20,
      Sorting: { SortBy: 'Date', Type: 1 },
    })
    expect(screen.getByText('Sayfa 1 / 1 · Toplam: 1')).toBeInTheDocument()
  })

  it('sends the channel filter from the address as EqualTo', async () => {
    renderWithProviders(<AuditPage />, { route: '/audit?channel=2' })

    await screen.findByText('Üye davet edildi')
    expect(lastRequestTo('/Organizations/audit-logs')?.body).toMatchObject({
      Filtering: { Criterias: [{ FilterBy: 'Channel', Value: ActionChannel.Mcp, Type: 5 }] },
    })
  })

  it('opens the detail drawer from a row and shows the details as fields', async () => {
    const { user } = renderWithProviders(<AuditPage />, { route: '/audit' })

    await user.click(await screen.findByText('Üye davet edildi'))

    const drawer = await screen.findByRole('dialog', { name: 'Üye davet edildi' })
    expect(drawer).toHaveTextContent('can@kuzeylab.dev invited.')
    expect(drawer).toHaveTextContent('role')
    expect(screen.getByTestId('location')).toHaveTextContent('id=90412')
  })

  it('pages forward with the page number in the request', async () => {
    server.use(
      endpoint('patch', '/Organizations/audit-logs', () =>
        Response.json(listEnvelope([auditLog({ Action: AuditAction.ConnectionAdded })], 1, 45, 3)),
      ),
    )
    const { user } = renderWithProviders(<AuditPage />, { route: '/audit' })

    await user.click(await screen.findByRole('button', { name: /Sonraki/ }))

    await waitFor(() => expect(lastRequestTo('/Organizations/audit-logs')?.body).toMatchObject({ PageNumber: 2 }))
  })

  it('offers to clear the filters when nothing matches', async () => {
    testState.auditLogs = []
    renderWithProviders(<AuditPage />, { route: '/audit?channel=3' })

    expect(await screen.findByRole('heading', { name: 'Kayıt bulunamadı' })).toBeInTheDocument()
  })

  it('shows the not-authorized state without a retry on 403', async () => {
    server.use(endpoint('patch', '/Organizations/audit-logs', () => errorResponse('OrganizationForbidden', 'Yetkiniz yok.', 403)))
    renderWithProviders(<AuditPage />, { route: '/audit' })

    expect(await screen.findByRole('heading', { name: 'Yetkin yok' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Tekrar dene' })).not.toBeInTheDocument()
  })

  it('offers a retry on a server error', async () => {
    server.use(endpoint('patch', '/Organizations/audit-logs', () => errorResponse('Error', 'Sunucu hatası.', 500)))
    renderWithProviders(<AuditPage />, { route: '/audit' })

    expect(await screen.findByRole('button', { name: 'Tekrar dene' })).toBeInTheDocument()
  })
})

describe('OrgSettingsPage', () => {
  it('renames the organization for the owner, sending only the name', async () => {
    const { user } = renderWithProviders(<OrgSettingsPage />)

    const name = await screen.findByLabelText('Ad')
    await user.clear(name)
    await user.type(name, 'Kuzey Labs')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() =>
      expect(lastRequestTo('/Organizations/organization')?.body).toEqual({ OrganizationId: TEST_ORG_ID, Name: 'Kuzey Labs' }),
    )
    expect(await screen.findByText('Ayarlar kaydedildi')).toBeInTheDocument()
  })

  it('is read only for an admin', async () => {
    testState.organizations = [organization({ Role: OrgRole.Admin })]

    renderWithProviders(<OrgSettingsPage />)

    expect(await screen.findByLabelText('Ad')).toHaveAttribute('readonly')
    expect(screen.queryByRole('button', { name: 'Kaydet' })).not.toBeInTheDocument()
  })

  it('is closed to a viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    renderWithProviders(<OrgSettingsPage />)

    expect(await screen.findByRole('heading', { name: 'Yetkin yok' })).toBeInTheDocument()
  })
})
