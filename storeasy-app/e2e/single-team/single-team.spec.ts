import { randomUUID } from 'node:crypto'
import { apiRaw, BASE_URL, login, uniqueIdentity, type Identity } from '../support/api'
import { expect, signIn, test } from '../support/fixtures'
import { linkIn, waitForMail } from '../support/mailpit'
import { expectEverySidebarLinkToOpenItsPage, sidebar, SIDEBAR_TR } from '../support/singleTeam'
import { setupCodeFromLog } from '../support/stack'

// A single team instance as a real install starts (deploy/e2e/stack.mjs up --mode single, Storeasy:Mode=SingleTeam): the
// database has no user, the API logged a one time setup code. The suite runs the setup in the UI, then checks what a
// single team server is: one team, no sign-up, a merged Settings menu, members by invitation. The tests share the
// administrator the first one creates, so they run in order.
test.describe.configure({ mode: 'serial' })

const owner: Identity = uniqueIdentity('owner')
const TEAM = `Kuzey ${owner.id}`

test('runs the first setup with the code from the API log and lands in the app as the owner', async ({ page }) => {
  const settings = await (await apiRaw('GET', '/System/public-settings')).json()
  expect(settings.data).toMatchObject({ mode: 1, setupRequired: true, registrationMode: 2 })

  // A guest at / sees the setup instead of the sign-in.
  await page.goto('/')
  await expect(page.getByRole('heading', { name: "storeasy'yi kur" })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Giriş yap' })).toHaveCount(0)

  // A wrong code is refused and creates nothing.
  const form = page.getByRole('form', { name: 'Kurulum' })
  await form.getByLabel('Kurulum kodu').fill('AAAA-BBBB-CCCC')
  await form.getByLabel('Ad', { exact: true }).fill(owner.name)
  await form.getByLabel('Soyad').fill(owner.surname)
  await form.getByLabel('E-posta').fill(owner.email)
  await form.getByLabel('Şifre', { exact: true }).fill(owner.password)
  await form.getByLabel('Takım adı').fill(TEAM)
  await form.getByRole('button', { name: 'Kurulumu bitir' }).click()
  await expect(page.getByText('Kurulum kodu doğru değil.').first()).toBeVisible()

  // The code from the log, typed in lower case without dashes (as someone copying it by hand might).
  const code = setupCodeFromLog()
  expect(code).toMatch(/^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$/)
  await form.getByLabel('Kurulum kodu').fill(code.replace(/-/g, '').toLowerCase())
  await form.getByRole('button', { name: 'Kurulumu bitir' }).click()

  await expect(page).toHaveURL(/\/apps$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Uygulamalar' })).toBeVisible()
  await expect(sidebar(page).getByRole('group', { name: `Takım: ${TEAM}` })).toBeVisible()
  await expect(sidebar(page).getByText('Sahip', { exact: true })).toBeVisible()

  const after = await (await apiRaw('GET', '/System/public-settings')).json()
  expect(after.data.setupRequired).toBe(false)
})

test('has no sign-up: the sign-in has no link and /register goes back to the sign-in', async ({ page }) => {
  await page.goto('/login')
  await expect(page.getByRole('heading', { name: 'Tekrar hoş geldin' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Giriş yap' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Hesap oluştur' })).toHaveCount(0)
  await expect(page.locator('a[href="/register"]')).toHaveCount(0)

  await page.goto('/register')
  await expect(page).toHaveURL(/\/login$/)

  const register = await apiRaw('POST', '/Account/register', {
    Email: `stranger-${owner.id}@e2e.test`,
    Name: 'Yabancı',
    Surname: 'Kişi',
    Password: owner.password,
  })
  expect(register.ok).toBe(false)
})

test('shows one team without a switcher and one Settings area with the team and the System items', async ({ page }) => {
  await signIn(page, owner)
  const aside = sidebar(page)

  await expect(aside.getByRole('group', { name: `Takım: ${TEAM}` })).toBeVisible()
  // No organization switcher and no "new organization" anywhere in the menu.
  await expect(aside.getByRole('button', { name: /Organizasyon|Değiştir/ })).toHaveCount(0)
  await expect(aside.getByText('Yeni organizasyon')).toHaveCount(0)

  const nav = aside.getByRole('navigation', { name: 'Ana menü' })
  for (const group of ['Ayarlar', 'Sistem']) {
    await expect(nav.getByText(group, { exact: true })).toBeVisible()
  }
  for (const group of ['Organizasyon', 'Platform']) {
    await expect(nav.getByText(group, { exact: true })).toHaveCount(0)
  }
  // The Settings area: the team's own pages, then the System pages of the administrator.
  for (const label of ['Takım', 'Üyeler', 'Mağaza bağlantıları', 'Denetim kaydı', 'Mağaza kuralları']) {
    await expect(nav.getByRole('link', { name: label, exact: true })).toBeVisible()
  }
  for (const label of ['Sistem durumu', 'Kullanıcılar', 'Roller', 'Diller', 'Özellik anahtarları', 'Aktivite logu', 'Katalog önerileri']) {
    await expect(nav.getByRole('link', { name: label, exact: true })).toBeVisible()
  }
  // Exactly the expected links, in this order.
  const hrefs = await nav.getByRole('link').evaluateAll((links) => links.map((link) => link.getAttribute('href')))
  expect(hrefs).toEqual(SIDEBAR_TR.map((item) => item.path))
})

test('opens the page of every sidebar link', async ({ page }) => {
  await signIn(page, owner)

  await expectEverySidebarLinkToOpenItsPage(page)
})

test('refuses a second setup and a second organization over the API', async () => {
  const setup = await apiRaw('POST', '/System/setup', {
    SetupCode: 'AAAA-BBBB-CCCC',
    Name: 'İkinci',
    Email: `second-${owner.id}@e2e.test`,
    Password: owner.password,
    OrganizationName: 'İkinci takım',
    DeviceId: randomUUID(),
  })
  expect(setup.status).toBe(409)
  expect((await setup.json()).messages.map((m: { key: string }) => m.key)).toContain('SetupAlreadyCompleted')

  const token = await login(owner.email, owner.password)
  const organization = await apiRaw('POST', '/Organizations/organization', { Name: 'İkinci takım', Slug: `second-${owner.id}` }, { token })
  expect(organization.status).toBe(409)
  expect((await organization.json()).messages.map((m: { key: string }) => m.key)).toContain('SingleTeamOrganizationExists')
})

test('invites a member by e-mail who creates an account from the invitation and sees the team', async ({ page, browser }) => {
  const invitee = uniqueIdentity('member')
  await signIn(page, owner)

  await sidebar(page).getByRole('link', { name: 'Üyeler', exact: true }).click()
  await page.getByRole('button', { name: 'Üye davet et' }).first().click()
  const dialog = page.getByRole('dialog', { name: 'Üye davet et' })
  await dialog.getByLabel('E-posta adresleri').fill(invitee.email)
  await dialog.getByRole('button', { name: 'Davet oluştur' }).click()
  await expect(dialog.getByText(invitee.email)).toBeVisible()
  await dialog.getByRole('button', { name: 'Bitti' }).click()

  // The invitee has no account (a single team server has no sign-up): the e-mail's link creates one.
  const mail = await waitForMail(invitee.email, TEAM)
  const link = linkIn(mail, '/invitation')
  const other = await browser.newContext({ locale: 'tr-TR' })
  try {
    const memberPage = await other.newPage()
    await memberPage.goto(link)
    await expect(memberPage.getByRole('heading', { name: new RegExp(TEAM) })).toBeVisible()
    await memberPage.getByRole('link', { name: 'Hesap oluştur ve katıl' }).click()
    await expect(memberPage).toHaveURL(/\/register\?invite=/)
    await memberPage.getByLabel('Ad', { exact: true }).fill(invitee.name)
    await memberPage.getByLabel('Soyad').fill(invitee.surname)
    await expect(memberPage.getByLabel('E-posta')).toHaveValue(invitee.email)
    await memberPage.getByLabel('Şifre', { exact: true }).fill(invitee.password)
    await memberPage.getByRole('button', { name: 'Hesap oluştur' }).click()

    // The member lands in the team (verified by the invitation, nothing else to do).
    await expect(memberPage).toHaveURL(/\/apps/, { timeout: 30_000 })
    await expect(sidebar(memberPage).getByRole('group', { name: `Takım: ${TEAM}` })).toBeVisible()
    await expect(sidebar(memberPage).getByText('Sistem', { exact: true })).toHaveCount(0)
  } finally {
    await other.close()
  }

  // The owner sees the member.
  await page.reload()
  await expect(page.getByText(invitee.email).first()).toBeVisible()
})

test('shows the menu, the team settings and a healthy system status in the English UI', async ({ browser }) => {
  const context = await browser.newContext({ locale: 'en-US' })
  try {
    const page = await context.newPage()
    await signIn(page, owner, 'en')
    const nav = sidebar(page, 'en').getByRole('navigation', { name: 'Main menu' })
    await expect(nav.getByText('Settings', { exact: true })).toBeVisible()
    await expect(nav.getByText('System', { exact: true })).toBeVisible()
    await expect(nav.getByText('Organization', { exact: true })).toHaveCount(0)

    await nav.getByRole('link', { name: 'Team', exact: true }).click()
    await expect(page).toHaveURL(/\/settings$/)
    await expect(page.getByRole('heading', { level: 1, name: 'Team settings' })).toBeVisible()
    await expect(page.getByLabel('Name', { exact: true })).toHaveValue(TEAM)

    await nav.getByRole('link', { name: 'System status', exact: true }).click()
    await expect(page).toHaveURL(/\/admin\/system$/)
    await expect(page.getByRole('heading', { level: 1, name: 'System status' })).toBeVisible()
    const services = page
      .getByRole('region', { name: 'Services' })
      .or(page.locator('section', { hasText: 'Services' }))
      .first()
    for (const name of ['PostgreSQL', 'Redis', 'Object storage', 'E-mail']) {
      await expect(services.getByText(name).first()).toBeVisible()
    }
    await expect(page.getByText('Healthy').first()).toBeVisible()
    await expect(page.getByText('Unhealthy')).toHaveCount(0)
    await expect(page.getByText('Unreachable')).toHaveCount(0)
    // Registration is fixed on a single team server.
    await expect(page.getByText(/used by a single team/)).toBeVisible()
  } finally {
    await context.close()
  }
})

test('answers the health probes through the web origin and has no API documentation (MILVA_ENV=prod)', async () => {
  for (const path of ['/health/live', '/health/ready', '/api/v1.0/healthcheck', '/api/v1.0/healthcheck/ready']) {
    const response = await fetch(`${BASE_URL}${path}`)
    expect(response.status, path).toBe(200)
    expect(response.headers.get('content-type'), path).not.toContain('text/html')
  }
  const ready = (await (await fetch(`${BASE_URL}/health/ready`)).json()) as { status: string }
  expect(ready.status).toBe('Healthy')

  for (const path of ['/api/documentation', '/api/documentation/', '/api/docs/v1.0/docs.json']) {
    const response = await fetch(`${BASE_URL}${path}`)
    expect(response.status, path).toBe(404)
    expect(response.headers.get('content-type') ?? '', path).not.toContain('text/html')
  }
})
