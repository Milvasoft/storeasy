import { readFileSync } from 'node:fs'
import { apiRaw, login } from '../support/api'
import { expect, signIn, test } from '../support/fixtures'
import { expectEverySidebarLinkToOpenItsPage, sidebar } from '../support/singleTeam'

// An install made before the single team mode (deploy/e2e/stack.mjs up --mode upgrade): initialized in multi tenant mode
// with developer/database/init, so it has only the root user and no organization, then started again with
// Storeasy:Mode=SingleTeam. There is no first run setup (a user exists); the root user creates the team in one step.
test.describe.configure({ mode: 'serial' })

const ROOT_PASSWORD_FILE = process.env.E2E_ROOT_PASSWORD_FILE
const root = () => ({
  email: 'rootuser',
  password: ROOT_PASSWORD_FILE ? readFileSync(ROOT_PASSWORD_FILE, 'utf8').trim() : (process.env.E2E_ROOT_PASSWORD ?? ''),
})
const TEAM = `Eski Kurulum ${Date.now().toString(36)}`

test.skip(!ROOT_PASSWORD_FILE && !process.env.E2E_ROOT_PASSWORD, 'E2E_ROOT_PASSWORD_FILE names the root password (deploy/e2e/stack.mjs)')

test('reports a single team that needs no setup', async () => {
  const settings = await (await apiRaw('GET', '/System/public-settings')).json()
  expect(settings.data).toMatchObject({ mode: 1, setupRequired: false, registrationMode: 2 })
})

test('takes the root user to the one-step create-team screen, then into the app', async ({ page }) => {
  await signIn(page, root() as never)

  await expect(page).toHaveURL(/\/setup$/)
  await expect(page.getByRole('heading', { name: 'Takımını oluştur' })).toBeVisible()
  // An app page leads back to the team step; the System pages stay open meanwhile.
  await page.goto('/members')
  await expect(page).toHaveURL(/\/setup$/)
  await page.goto('/admin/system')
  await expect(page.getByRole('heading', { level: 1, name: 'Sistem durumu' })).toBeVisible()

  await page.goto('/setup')
  await page.getByLabel('Ad', { exact: true }).fill(TEAM)
  await page.getByRole('button', { name: 'Oluştur ve devam et' }).click()
  // The next step is the store connection; it can wait.
  await page.getByRole('button', { name: 'Sonra yap' }).click()
  await expect(page).toHaveURL(/\/apps$/)
  await expect(sidebar(page).getByRole('group', { name: `Takım: ${TEAM}` })).toBeVisible()
})

test('opens the page of every sidebar link for the upgraded install', async ({ page }) => {
  await signIn(page, root() as never)
  await expect(page).toHaveURL(/\/apps$/)

  await expectEverySidebarLinkToOpenItsPage(page)
})

test('refuses a second team over the API', async () => {
  const token = await login(root().email, root().password)
  const response = await apiRaw('POST', '/Organizations/organization', { Name: 'İkinci takım', Slug: 'ikinci-takim' }, { token })
  expect(response.status).toBe(409)
  expect((await response.json()).messages.map((m: { key: string }) => m.key)).toContain('SingleTeamOrganizationExists')
})
