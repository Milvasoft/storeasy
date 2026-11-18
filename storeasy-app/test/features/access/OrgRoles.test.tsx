import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgPermission, OrgRole } from '@/lib/api/types'
import { canDefine, canGrant, choiceValue, parseChoice, seededAccess } from '@/lib/org/roles'
import { OrgRolesPage } from '@/features/access/OrgRolesPage'
import { accessEn, accessTr } from '@/features/access/i18n'
import { MembersPage } from '@/pages/org/MembersPage'
import { member, memberList, organization, orgRole, orgRoles, PERMISSIONS_OF, TEST_ORG_ID } from '../../support/factories'
import { endpoint, errorResponse } from '../../support/handlers'
import { renderApp, renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

function renderRoles(route = '/members/roles') {
  return renderWithProviders(<OrgRolesPage />, { route })
}

const editor = () => screen.getByRole('form', { name: /Yayın sorumlusu|Yeni rol/ })

describe('organization role rules', () => {
  it('lets a member manager grant only what lies within their own permissions', () => {
    const admin = seededAccess(OrgRole.Admin)
    const releaser = { role: OrgRole.Custom, permissions: [OrgPermission.View, OrgPermission.ManageMembers, OrgPermission.ReleaseTest] }

    expect(canGrant(seededAccess(OrgRole.Owner), seededAccess(OrgRole.Owner), seededAccess(OrgRole.Viewer))).toBe(true)
    expect(canGrant(admin, seededAccess(OrgRole.Editor), seededAccess(OrgRole.Owner))).toBe(false)
    expect(canGrant(releaser, seededAccess(OrgRole.Viewer), seededAccess(OrgRole.Editor))).toBe(false)
    expect(
      canGrant(releaser, seededAccess(OrgRole.Viewer), {
        role: OrgRole.Custom,
        permissions: [OrgPermission.View, OrgPermission.ReleaseTest],
      }),
    ).toBe(true)
    expect(canDefine(admin, [OrgPermission.ReleaseProduction])).toBe(true)
    expect(canDefine(seededAccess(OrgRole.Editor), [OrgPermission.View])).toBe(false)
  })

  it('round trips a role choice through its select value', () => {
    expect(parseChoice(choiceValue(OrgRole.Custom, 14))).toEqual({ role: OrgRole.Custom, customRoleId: 14 })
    expect(parseChoice(choiceValue(OrgRole.Admin))).toEqual({ role: OrgRole.Admin, customRoleId: null })
  })

  it('has the same keys in both languages', () => {
    const keys = (tree: object): string[] =>
      Object.entries(tree).flatMap(([key, value]) =>
        typeof value === 'string' ? [key] : keys(value as object).map((child) => `${key}.${child}`),
      )
    expect(keys(accessTr).sort()).toEqual(keys(accessEn).sort())
  })
})

describe('OrgRolesPage', () => {
  it('lists the locked system roles, then the custom ones, with their counts', async () => {
    renderRoles()

    const seeded = await screen.findByRole('region', { name: 'Sistem rolleri' })
    expect(within(seeded).getByRole('button', { name: /Sahip.*10 \/ 10 izin · 1 üye/ })).toBeInTheDocument()
    expect(within(seeded).getByRole('button', { name: /Editör.*1 bekleyen davet/ })).toBeInTheDocument()
    const custom = screen.getByRole('region', { name: 'Özel roller' })
    expect(within(custom).getByRole('button', { name: /Yayın sorumlusu.*2 \/ 10 izin/ })).toBeInTheDocument()
    expect(screen.getByText('Sistem rolü · kilitli')).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: /Organizasyonu yönet/ })).toBeDisabled()
    expect(requestsTo('/Organizations/roles')[0].query).toEqual({ OrganizationId: String(TEST_ORG_ID) })
  })

  it('creates a role with the picked permissions; ManageOrganization can never be picked', async () => {
    const { user } = renderRoles()

    await user.click(await screen.findByRole('button', { name: 'Yeni rol' }))
    await user.type(screen.getByLabelText(/Rol adı/), 'Destek')
    expect(within(editor()).getByRole('checkbox', { name: /Organizasyonu yönet/ })).toBeDisabled()
    expect(within(editor()).getByRole('checkbox', { name: /Görüntüle/ })).toBeDisabled()
    await user.click(within(editor()).getByRole('checkbox', { name: /Yorum yanıtla/ }))
    await user.click(within(editor()).getByRole('button', { name: 'Rolü oluştur' }))

    await waitFor(() =>
      expect(lastRequestTo('/Organizations/role')?.body).toEqual({
        OrganizationId: TEST_ORG_ID,
        Name: 'Destek',
        Description: null,
        Permissions: [OrgPermission.View, OrgPermission.ReplyReviews],
      }),
    )
    expect(requestsTo('/Organizations/role')[0].headers['idempotency-key']).toBeTruthy()
    expect(await screen.findByText('Destek oluşturuldu.')).toBeInTheDocument()
  })

  it('does not offer a permission the author does not hold', async () => {
    testState.organizations = [
      organization({ Role: OrgRole.Custom, Permissions: [OrgPermission.View, OrgPermission.ManageMembers, OrgPermission.ReleaseTest] }),
    ]
    const { user } = renderRoles()

    await user.click(await screen.findByRole('button', { name: 'Yeni rol' }))

    expect(within(editor()).getByRole('checkbox', { name: /Production'a yayın/ })).toBeDisabled()
    expect(within(editor()).getByRole('checkbox', { name: /Test track'lerine yayın/ })).toBeEnabled()
    expect(within(editor()).getAllByText('Bu izin sende yok, bu yüzden veremezsin.').length).toBeGreaterThan(0)
  })

  it('asks for a name before sending', async () => {
    const { user } = renderRoles()

    await user.click(await screen.findByRole('button', { name: 'Yeni rol' }))
    await user.click(within(editor()).getByRole('button', { name: 'Rolü oluştur' }))

    expect(screen.getByText('Role bir ad ver.')).toBeInTheDocument()
    expect(requestsTo('/Organizations/role')).toHaveLength(0)
  })

  it('renames a custom role and replaces its permissions', async () => {
    const { user } = renderRoles(`/members/roles?role=${choiceValue(OrgRole.Custom, 14)}`)

    const name = await screen.findByLabelText(/Rol adı/)
    await user.clear(name)
    await user.type(name, 'Yayın ekibi')
    await user.click(within(editor()).getByRole('checkbox', { name: /Production'a yayın/ }))
    expect(screen.getByText('Kaydedilmedi')).toBeInTheDocument()
    await user.click(within(editor()).getByRole('button', { name: 'Kaydet' }))

    await waitFor(() =>
      expect(lastRequestTo('/Organizations/role')?.body).toEqual({
        OrganizationId: TEST_ORG_ID,
        RoleId: 14,
        Name: 'Yayın ekibi',
        Description: null,
        Permissions: [OrgPermission.View, OrgPermission.ReleaseTest, OrgPermission.ReleaseProduction],
      }),
    )
  })

  it('puts the draft back with cancel', async () => {
    const { user } = renderRoles(`/members/roles?role=${choiceValue(OrgRole.Custom, 14)}`)

    const name = await screen.findByLabelText(/Rol adı/)
    await user.type(name, ' 2')
    await user.click(within(editor()).getByRole('button', { name: 'Vazgeç' }))

    expect(name).toHaveValue('Yayın sorumlusu')
    expect(within(editor()).getByRole('button', { name: 'Kaydet' })).toBeDisabled()
  })

  it('says a role in use cannot be deleted and sends nothing', async () => {
    testState.orgRoles = [...orgRoles().slice(0, 4), orgRole({ MemberCount: 2, InvitationCount: 1 })]
    const { user } = renderRoles(`/members/roles?role=${choiceValue(OrgRole.Custom, 14)}`)

    await user.click(await screen.findByRole('button', { name: 'Sil' }))

    const dialog = await screen.findByRole('alertdialog', { name: 'Yayın sorumlusu henüz silinemez' })
    expect(dialog).toHaveTextContent('2 üye ve 1 davet')
    expect(requestsTo('/Organizations/role')).toHaveLength(0)
  })

  it('deletes an unused role after the confirmation', async () => {
    const { user } = renderRoles(`/members/roles?role=${choiceValue(OrgRole.Custom, 14)}`)

    await user.click(await screen.findByRole('button', { name: 'Sil' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Yayın sorumlusu silinsin mi?' })
    await user.click(within(dialog).getByRole('button', { name: 'Sil' }))

    await waitFor(() => expect(lastRequestTo('/Organizations/role')?.body).toEqual({ OrganizationId: TEST_ORG_ID, RoleId: 14 }))
    expect(await screen.findByText('Yayın sorumlusu silindi.')).toBeInTheDocument()
  })

  it('shows the API refusal when a role became used meanwhile (409)', async () => {
    server.use(endpoint('delete', '/Organizations/role', () => errorResponse('OrganizationRoleInUse', 'Bu rolü taşıyan üyeler var.', 409)))
    const { user } = renderRoles(`/members/roles?role=${choiceValue(OrgRole.Custom, 14)}`)

    await user.click(await screen.findByRole('button', { name: 'Sil' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Sil' }))

    expect(await screen.findByText('Bu rolü taşıyan üyeler var.')).toBeInTheDocument()
  })

  it('shows a name conflict above the form', async () => {
    server.use(endpoint('put', '/Organizations/role', () => errorResponse('OrganizationRoleNameExists', 'Bu adla bir rol var.', 409)))
    const { user } = renderRoles(`/members/roles?role=${choiceValue(OrgRole.Custom, 14)}`)

    await user.type(await screen.findByLabelText(/Rol adı/), ' 2')
    await user.click(within(editor()).getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Bu adla bir rol var.')).toBeInTheDocument()
  })

  it('is read only without the manage members permission', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]
    renderRoles(`/members/roles?role=${choiceValue(OrgRole.Custom, 14)}`)

    expect(await screen.findByText('Rol tanımlamak için Üyeleri yönet izni gerekir.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yeni rol' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/Rol adı/)).not.toBeInTheDocument()
  })

  it('is reached from the members tab row', async () => {
    await renderApp({ route: '/members/roles' })

    expect(await screen.findByRole('heading', { name: 'Roller', level: 1 })).toBeInTheDocument()
    const tabs = screen.getByRole('navigation', { name: 'Üyeler ve roller' })
    expect(within(tabs).getByRole('link', { name: 'Roller' })).toHaveAttribute('aria-current', 'page')
  })
})

describe('custom roles on the members screen', () => {
  it('gives a member a custom role with its id', async () => {
    const { user } = renderWithProviders(<MembersPage />)

    await user.click(await screen.findByRole('button', { name: 'Mert Kaya rolü' }))
    await user.click(await screen.findByRole('option', { name: /Yayın sorumlusu/ }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Mert Kaya kişisinin rolü değişsin mi?' })
    await user.click(within(dialog).getByRole('button', { name: 'Rolü değiştir' }))

    await waitFor(() =>
      expect(lastRequestTo('/Organizations/member/role')?.body).toEqual({
        OrganizationId: TEST_ORG_ID,
        MemberId: 3,
        Role: OrgRole.Custom,
        CustomRoleId: 14,
      }),
    )
    expect(await screen.findByText('Mert Kaya artık Yayın sorumlusu')).toBeInTheDocument()
  })

  it('shows the custom role name of members and invitations', async () => {
    testState.members = memberList({
      Members: [
        member(),
        member({
          Id: 4,
          UserId: 12,
          Name: 'Ali',
          Surname: 'Er',
          Role: OrgRole.Custom,
          CustomRoleId: 14,
          CustomRoleName: 'Yayın sorumlusu',
        }),
      ],
      Invitations: [
        {
          Id: 40,
          Email: 'yeni@kuzeylab.dev',
          Role: OrgRole.Custom,
          CustomRoleId: 14,
          CustomRoleName: 'Yayın sorumlusu',
          ExpiryDate: '2099-01-01T00:00:00Z',
        },
      ],
    })
    testState.organizations = [organization({ Role: OrgRole.Viewer, Permissions: PERMISSIONS_OF[OrgRole.Viewer] })]
    renderWithProviders(<MembersPage />)

    expect(await screen.findAllByText('Yayın sorumlusu')).toHaveLength(2)
  })

  it('invites with a custom role', async () => {
    const { user } = renderWithProviders(<MembersPage />)

    await user.click(await screen.findByRole('button', { name: 'Üye davet et' }))
    await user.type(screen.getByLabelText('E-posta adresleri'), 'a@kuzeylab.dev')
    await user.click(await screen.findByRole('radio', { name: /Yayın sorumlusu/ }))
    await user.click(screen.getByRole('button', { name: 'Davet oluştur' }))

    await waitFor(() =>
      expect(lastRequestTo('/Organizations/invitation')?.body).toEqual({
        OrganizationId: TEST_ORG_ID,
        Email: 'a@kuzeylab.dev',
        Role: OrgRole.Custom,
        CustomRoleId: 14,
      }),
    )
  })
})
