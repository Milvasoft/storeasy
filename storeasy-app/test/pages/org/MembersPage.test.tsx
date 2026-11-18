import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { MembersPage } from '@/pages/org/MembersPage'
import { member, memberList, organization, TEST_ORG_ID } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

describe('MembersPage', () => {
  it('lists members and pending invitations for a member manager', async () => {
    renderWithProviders(<MembersPage />)

    expect(await screen.findByText('Ece Yılmaz')).toBeInTheDocument()
    expect(screen.getByText('3 üye · 1 bekleyen davet')).toBeInTheDocument()
    expect(screen.getByText('can@kuzeylab.dev')).toBeInTheDocument()
    expect(lastRequestTo('/Organizations/members')?.query).toEqual({ OrganizationId: String(TEST_ORG_ID) })
  })

  it('locks the only owner out of stepping down', async () => {
    renderWithProviders(<MembersPage />)

    expect(await screen.findByText('Tek Sahip rolünü düşüremez.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Deniz Arslan rolü' })).not.toBeInTheDocument()
  })

  it('changes a role after the confirmation', async () => {
    const { user } = renderWithProviders(<MembersPage />)

    await user.click(await screen.findByRole('button', { name: 'Mert Kaya rolü' }))
    await user.click(screen.getByRole('option', { name: /İzleyici/ }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Mert Kaya kişisinin rolü değişsin mi?' })
    await user.click(within(dialog).getByRole('button', { name: 'Rolü değiştir' }))

    await waitFor(() =>
      expect(lastRequestTo('/Organizations/member/role')?.body).toEqual({
        OrganizationId: TEST_ORG_ID,
        MemberId: 3,
        Role: OrgRole.Viewer,
        CustomRoleId: null,
      }),
    )
    expect(await screen.findByText('Mert Kaya artık İzleyici')).toBeInTheDocument()
  })

  it('does not let an admin touch the owner or make an owner', async () => {
    testState.organizations = [organization({ Role: OrgRole.Admin })]
    testState.members = memberList({
      Members: [
        member({ UserId: 99, Email: 'owner@kuzeylab.dev', Name: 'Sahip', Surname: 'Kişi' }),
        member({ Id: 2, UserId: 7, Role: OrgRole.Admin }),
        member({ Id: 3, UserId: 9, Name: 'Mert', Surname: 'Kaya', Email: 'mert@kuzeylab.dev', Role: OrgRole.Editor }),
      ],
    })
    const { user } = renderWithProviders(<MembersPage />)

    expect(await screen.findByText('Yönetici, Sahip rolündeki üyeyi değiştiremez.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Mert Kaya rolü' }))
    expect(screen.getByRole('option', { name: /Sahip/ })).toHaveAttribute('aria-disabled', 'true')
  })

  it('shows a viewer a read-only list without invitations or invite button', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    testState.members = memberList({ Invitations: null })

    renderWithProviders(<MembersPage />)

    expect(await screen.findByText('Ece Yılmaz')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Üye davet et' })).not.toBeInTheDocument()
    expect(screen.queryByText('Bekleyen davetler')).not.toBeInTheDocument()
  })

  it('invites each address separately and shows each link once', async () => {
    const { user } = renderWithProviders(<MembersPage />)

    await user.click(await screen.findByRole('button', { name: 'Üye davet et' }))
    await user.type(screen.getByLabelText('E-posta adresleri'), 'a@kuzeylab.dev, b@kuzeylab.dev')
    await user.click(screen.getByRole('button', { name: 'Davet oluştur' }))

    expect(await screen.findByText('Bağlantılar yalnız şimdi gösteriliyor')).toBeInTheDocument()
    expect(requestsTo('/Organizations/invitation').map((request) => request.body)).toEqual([
      { OrganizationId: TEST_ORG_ID, Email: 'a@kuzeylab.dev', Role: OrgRole.Editor, CustomRoleId: null },
      { OrganizationId: TEST_ORG_ID, Email: 'b@kuzeylab.dev', Role: OrgRole.Editor, CustomRoleId: null },
    ])
    expect(screen.getByText(/\/invite\/token-for-a%40kuzeylab\.dev$/)).toBeInTheDocument()
    expect(requestsTo('/Organizations/invitation')[0].headers['idempotency-key']).toBeTruthy()
  })

  it('keeps an address that is already a member in the form (409)', async () => {
    server.use(endpoint('post', '/Organizations/invitation', () => errorResponse('OrganizationAlreadyMember', 'Bu adres zaten üye.', 409)))
    const { user } = renderWithProviders(<MembersPage />)

    await user.click(await screen.findByRole('button', { name: 'Üye davet et' }))
    await user.type(screen.getByLabelText('E-posta adresleri'), 'ece@kuzeylab.dev')
    await user.click(screen.getByRole('button', { name: 'Davet oluştur' }))

    expect(await screen.findByText('ece@kuzeylab.dev: Bu adres zaten üye.')).toBeInTheDocument()
    expect(screen.getByLabelText('E-posta adresleri')).toHaveValue('ece@kuzeylab.dev')
  })

  it('revokes a pending invitation after the confirmation', async () => {
    const { user } = renderWithProviders(<MembersPage />)

    await user.click(await screen.findByRole('button', { name: 'Geri al' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Geri al' }))

    await waitFor(() => expect(lastRequestTo('/Organizations/invitation')?.body).toEqual({ OrganizationId: TEST_ORG_ID, InvitationId: 31 }))
  })

  it('removes a member through the row menu', async () => {
    const { user } = renderWithProviders(<MembersPage />)

    await user.click(await screen.findByRole('button', { name: 'Mert Kaya için eylemler' }))
    await user.click(screen.getByRole('menuitem', { name: 'Organizasyondan çıkar' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Organizasyondan çıkar' }))

    await waitFor(() => expect(lastRequestTo('/Organizations/member')?.body).toEqual({ OrganizationId: TEST_ORG_ID, MemberId: 3 }))
  })

  it('tells the only owner to hand over ownership before leaving', async () => {
    const { user } = renderWithProviders(<MembersPage />)

    await user.click(await screen.findByRole('button', { name: 'Deniz Arslan için eylemler' }))
    await user.click(screen.getByRole('menuitem', { name: 'Organizasyondan ayrıl' }))

    expect(await screen.findByRole('alertdialog', { name: 'Tek Sahip sensin' })).toBeInTheDocument()
    expect(requestsTo('/Organizations/member')).toHaveLength(0)
  })
})
