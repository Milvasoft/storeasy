import { apiCall } from '../support/api'
import { expect, signIn, test } from '../support/fixtures'
import { fakeServiceAccount } from '../support/stores'

// No store is reachable from the e2e stack (deploy/e2e/docker-compose.e2e.yml): a connection can be saved, its check
// fails without breaking anything, and the key never comes back.
test.use({ locale: 'en-US' })

interface Connection {
  id: number
  name: string
  status: number | string
}

test('validates the store connection form, saves a Google key whose check fails gracefully and never returns it', async ({
  page,
  user,
  organization,
}) => {
  const account = fakeServiceAccount(user.id)
  await signIn(page, user, 'en')
  await page.goto('/connections')
  await page.getByRole('button', { name: 'Add a connection' }).first().click()
  const dialog = page.getByRole('dialog')
  await dialog.getByText('Google Play service account', { exact: true }).click()
  await dialog.getByRole('button', { name: 'Continue' }).click()

  // Client side checks first: nothing is sent while the form is incomplete or the file is not a service account.
  await dialog.getByRole('radio', { name: 'Paste' }).click()
  await dialog.getByRole('button', { name: 'Save and verify' }).click()
  await expect(dialog.getByText('Give the connection a name.')).toBeVisible()
  await expect(dialog.getByText('Choose or paste the service account JSON.')).toBeVisible()

  await dialog.getByLabel('Connection name').fill('Play e2e')
  const json = dialog.getByRole('textbox', { name: 'Service account JSON' })
  await json.fill('{ not json')
  await dialog.getByRole('button', { name: 'Save and verify' }).click()
  await expect(dialog.getByText('The file is not valid JSON.')).toBeVisible()

  await json.fill(JSON.stringify({ type: 'service_account', client_email: 'x@y.iam.gserviceaccount.com' }))
  await dialog.getByRole('button', { name: 'Save and verify' }).click()
  await expect(dialog.getByText('The file has no private_key: download a new key for the service account.')).toBeVisible()

  // A well formed key: saved, then the check cannot reach Google and says so.
  await json.fill(account.json)
  await dialog.getByRole('button', { name: 'Save and verify' }).click()
  await expect(dialog.getByText('Verification did not pass')).toBeVisible({ timeout: 60_000 })
  await dialog.getByRole('button', { name: 'Done' }).click()
  await expect(page.getByRole('heading', { name: 'Play e2e' })).toBeVisible()

  // The page keeps working, and no answer ever carries the key.
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Play e2e' })).toBeVisible()
  const list = await apiCall<Connection[]>('GET', '/StoreConnections', undefined, {
    token: user.token,
    query: { OrganizationId: organization.id },
  })
  expect(list).toHaveLength(1)
  const raw = JSON.stringify(list)
  expect(raw).not.toContain('PRIVATE KEY')
  expect(raw).not.toContain(account.privateKey.split('\n')[1])
})

interface Template {
  id: number
  name: string
}

test('creates a studio template from a starter, renders a preview and plans an export', async ({ page, user, organization }) => {
  // An app needs a store record; a Google record is added by package name on a connection (its sync job cannot reach
  // Google and fails, the app stays).
  const account = fakeServiceAccount(user.id)
  const connectionId = await apiCall<number>(
    'POST',
    '/StoreConnections/connection',
    { OrganizationId: organization.id, Store: 2, Name: 'Play e2e', Secret: account.json },
    { token: user.token },
  )
  const added = await apiCall<{ appId: number }>(
    'POST',
    '/Apps/google',
    { OrganizationId: organization.id, ConnectionId: connectionId, PackageName: `dev.storeasy.e2e${user.id}` },
    { token: user.token },
  )

  await signIn(page, user, 'en')
  await page.goto('/studio')
  await page.getByRole('button', { name: 'New template' }).first().click()
  const dialog = page.getByRole('dialog', { name: 'New template' })
  await dialog.getByLabel('Template name').fill('Launch set')
  await dialog.getByRole('button', { name: 'Create template' }).click()
  await expect(page.getByRole('heading', { name: 'Launch set' })).toBeVisible({ timeout: 20_000 })

  // The preview is drawn on the server.
  await expect(page.getByRole('img', { name: /Preview of slot 1/ })).toBeVisible({ timeout: 30_000 })

  // Export plan: what would be rendered, without rendering.
  await page
    .getByRole('link', { name: 'Export', exact: true })
    .or(page.getByRole('tab', { name: 'Export' }))
    .click()
  await expect(page.getByText('Will render')).toBeVisible({ timeout: 20_000 })
  await expect(page.getByText(/^\d+ images$/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Render images' })).toBeEnabled()

  const templates = await apiCall<Template[]>(
    'PATCH',
    '/Mockups/templates',
    { OrganizationId: organization.id, AppId: added.appId },
    {
      token: user.token,
    },
  )
  expect(JSON.stringify(templates)).toContain('Launch set')
})
