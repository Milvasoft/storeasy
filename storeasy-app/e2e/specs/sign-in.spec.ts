import { expect, test } from '@playwright/test'

// The first critical journey: sign in with an existing account, land in the app, sign out. The account comes from the
// environment (a seeded user of the API under test); the test is skipped when none is given.
const EMAIL = process.env.E2E_USER_EMAIL
const PASSWORD = process.env.E2E_USER_PASSWORD

test.skip(!EMAIL || !PASSWORD, 'E2E_USER_EMAIL and E2E_USER_PASSWORD name a seeded account of the API under test')

test('signs in by e-mail and signs out again', async ({ page }) => {
  await page.goto('/login')

  await page.getByLabel('E-posta').fill(EMAIL!)
  await page.getByLabel('Şifre').fill(PASSWORD!)
  await page.getByRole('button', { name: 'Giriş yap' }).click()

  await expect(page).not.toHaveURL(/\/login/)
  await page.getByRole('complementary', { name: 'Ana menü' }).getByRole('button', { name: 'Hesap menüsü' }).click()
  await page.getByRole('menuitem', { name: 'Çıkış yap' }).click()
  await expect(page.getByRole('heading', { name: 'Tekrar hoş geldin' })).toBeVisible()
})
