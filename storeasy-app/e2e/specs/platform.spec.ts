import { readFileSync } from 'node:fs'
import { BASE_URL } from '../support/api'
import { expect, signIn, test } from '../support/fixtures'

// The root user of the instance (docs/self-host.md section 5): node deploy/e2e/stack.mjs writes its password here.
const ROOT_PASSWORD_FILE = process.env.E2E_ROOT_PASSWORD_FILE
const rootPassword = () => (ROOT_PASSWORD_FILE ? readFileSync(ROOT_PASSWORD_FILE, 'utf8').trim() : (process.env.E2E_ROOT_PASSWORD ?? ''))

test('answers the health probes through the web origin', async () => {
  for (const path of ['/health/live', '/health/ready', '/api/v1.0/healthcheck', '/api/v1.0/healthcheck/ready']) {
    const response = await fetch(`${BASE_URL}${path}`)
    expect(response.status, path).toBe(200)
    expect(response.headers.get('content-type'), path).not.toContain('text/html')
  }
  const ready = (await (await fetch(`${BASE_URL}/health/ready`)).json()) as { status: string }
  expect(ready.status).toBe('Healthy')
  // Webhook receivers are the API's too (not the app page): an unknown source is refused by the API.
  const webhook = await fetch(`${BASE_URL}/webhooks/artifacts/unknown`, { method: 'POST', body: '{}' })
  expect(webhook.headers.get('content-type') ?? '').toContain('application/json')
})

test.describe('platform admin', () => {
  test.use({ locale: 'en-US' })
  test.skip(!ROOT_PASSWORD_FILE && !process.env.E2E_ROOT_PASSWORD, 'E2E_ROOT_PASSWORD_FILE names the root password (deploy/e2e/stack.mjs)')

  test('signs in as the root user and sees every service healthy on the system page', async ({ page }) => {
    await signIn(page, { email: 'rootuser', password: rootPassword() } as never, 'en')
    await page.goto('/admin/system')
    await expect(page.getByRole('heading', { name: 'System status' })).toBeVisible()
    const services = page
      .getByRole('region', { name: 'Services' })
      .or(page.locator('section', { hasText: 'Services' }))
      .first()
    for (const name of ['PostgreSQL', 'Redis', 'Object storage', 'E-mail']) {
      await expect(services.getByText(name).first()).toBeVisible()
    }
    await expect(page.getByText('Unhealthy')).toHaveCount(0)
    await expect(page.getByText('Unreachable')).toHaveCount(0)
    await expect(page.getByText('Healthy').first()).toBeVisible()
  })
})

test('shows the rules catalog, the notification bell and a connected live activity stream', async ({ page, user, organization }) => {
  expect(organization.id).toBeGreaterThan(0)
  await signIn(page, user)

  // Store rules (the catalog shipped in the image).
  await page.goto('/rules')
  await expect(page.getByRole('heading', { name: 'Mağaza kuralları' })).toBeVisible()
  await expect(page.getByRole('row').nth(1).or(page.getByRole('listitem').first())).toBeVisible()

  // Notification bell: opens the center, nothing is there for a new account.
  await page.getByRole('button', { name: 'Bildirimler' }).first().click()
  await expect(page.getByText('Bildirim yok').first()).toBeVisible()
  await page.keyboard.press('Escape')

  // Live activity: the SignalR hub connects over the web origin (/hubs WebSocket).
  await page.goto('/activity')
  await expect(page.getByText('Canlı', { exact: true }).first()).toBeVisible({ timeout: 30_000 })
})
