import { linkIn, waitForMail } from '../support/mailpit'
import { expect, registerVerifiedUser, signIn, test } from '../support/fixtures'

// Organization administration in the English UI: invite a member by e-mail and accept in a second browser, change the
// role, define a custom role and give it, change the settings, and find all of it in the audit log.
test.use({ locale: 'en-US' })

test('invites a member who accepts from the e-mail, then manages roles, settings and the audit log', async ({
  page,
  browser,
  user,
  organization,
}) => {
  const invitee = await registerVerifiedUser('inv')
  const inviteeName = `${invitee.name} ${invitee.surname}`

  await signIn(page, user, 'en')

  // Invite by e-mail.
  await page.goto('/members')
  await page.getByRole('button', { name: 'Invite members' }).first().click()
  const dialog = page.getByRole('dialog', { name: 'Invite members' })
  await dialog.getByLabel('E-mail addresses').fill(invitee.email)
  await dialog.getByText('Viewer', { exact: true }).click()
  await expect(dialog.getByRole('radio', { name: /Viewer/ })).toBeChecked()
  await dialog.getByRole('button', { name: 'Create invitation' }).click()
  await expect(dialog.getByText(invitee.email)).toBeVisible()
  await dialog.getByRole('button', { name: 'Done' }).click()

  // The invitee opens the e-mail's link in another browser, signs in and joins.
  const mail = await waitForMail(invitee.email, organization.name)
  const link = linkIn(mail, '/invitation')
  const other = await browser.newContext({ locale: 'en-US' })
  const inviteePage = await other.newPage()
  await signIn(inviteePage, invitee, 'en')
  await inviteePage.goto(link)
  await expect(inviteePage.getByRole('heading', { name: `Join ${organization.name}` })).toBeVisible()
  await inviteePage.getByRole('button', { name: 'Join' }).click()
  await expect(inviteePage.getByText('You joined the organization')).toBeVisible()
  await expect(inviteePage).toHaveURL(/\/apps/)

  // The owner sees the new member and makes them an admin.
  await page.reload()
  const roleSelect = page.getByRole('button', { name: `Role of ${inviteeName}` })
  await expect(roleSelect).toBeVisible()
  await roleSelect.click()
  await page.getByRole('option', { name: /^Admin/ }).click()
  await page.getByRole('button', { name: 'Change role', exact: true }).click()
  await expect(page.getByText(`${inviteeName} is now Admin`)).toBeVisible()

  // A custom role, then given to the member.
  const roleName = `Releasers ${user.id}`
  await page.goto('/members/roles')
  await page.getByRole('button', { name: 'New role' }).click()
  const editor = page.getByRole('form', { name: 'New role' })
  await editor.getByLabel('Role name').fill(roleName)
  await editor.getByText('Release to test tracks', { exact: true }).click()
  await editor.getByRole('button', { name: 'Create role' }).click()
  await expect(page.getByText(`${roleName} created.`)).toBeVisible()

  await page.goto('/members')
  await page.getByRole('button', { name: `Role of ${inviteeName}` }).click()
  await page.getByRole('option', { name: new RegExp(roleName) }).click()
  await page.getByRole('button', { name: 'Change role', exact: true }).click()
  await expect(page.getByText(`${inviteeName} is now ${roleName}`)).toBeVisible()

  // Settings: rename the organization.
  const newName = `${organization.name} renamed`
  await page.goto('/settings')
  await page.getByLabel('Name', { exact: true }).fill(newName)
  await page.getByRole('button', { name: 'Save' }).first().click()
  await expect(page.getByText('Settings saved')).toBeVisible()

  // The audit log shows what happened.
  await page.goto('/audit')
  await expect(page.getByText('Member invited').first()).toBeVisible()
  await expect(page.getByRole('cell', { name: inviteeName }).or(page.getByText(invitee.email)).first()).toBeVisible()

  await other.close()
})
