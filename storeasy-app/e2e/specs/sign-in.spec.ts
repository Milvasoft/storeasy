import { expect, test } from '../support/fixtures'

// Sign in with an existing account, land in the app, sign out. The account is a fresh verified account with an
// organization, made over the API (support/fixtures.ts).
test('signs in by e-mail and signs out again', async ({ page, user, organization }) => {
  expect(organization.id).toBeGreaterThan(0)
  await page.goto('/login')

  await page.getByLabel('E-posta').fill(user.email)
  await page.getByLabel('Şifre', { exact: true }).fill(user.password)
  await page.getByRole('button', { name: 'Giriş yap' }).click()

  await expect(page).not.toHaveURL(/\/login/)
  await page.getByRole('complementary', { name: 'Ana menü' }).getByRole('button', { name: 'Hesap menüsü' }).click()
  await page.getByRole('menuitem', { name: 'Çıkış yap' }).click()
  await expect(page.getByRole('heading', { name: 'Tekrar hoş geldin' })).toBeVisible()
})
