import { expect, test } from '@playwright/test'
import { uniqueIdentity } from '../support/api'
import { linkIn, waitForMail } from '../support/mailpit'

// The first journey of a new user, in Turkish: sign up, read the verification e-mail, verify, sign in, create the
// organization in the setup flow and sign out.
test('registers, verifies the e-mail from the inbox, signs in and creates an organization', async ({ page }) => {
  const user = uniqueIdentity('reg')

  await page.goto('/register')
  await page.getByLabel('Ad', { exact: true }).fill(user.name)
  await page.getByLabel('Soyad').fill(user.surname)
  await page.getByLabel('E-posta').fill(user.email)
  await page.getByLabel('Şifre', { exact: true }).fill(user.password)
  await page.getByRole('button', { name: 'Hesap oluştur' }).click()
  // The new account is signed in at once; the setup waits for the verification (banner with a resend button).
  await expect(page).toHaveURL(/\/setup/)
  await expect(page.getByText('E-posta adresin henüz doğrulanmadı.')).toBeVisible()

  const mail = await waitForMail(user.email)
  const link = linkIn(mail, '/verify-email')
  await page.goto(link)
  await expect(page.getByRole('heading', { name: 'E-postan doğrulandı' })).toBeVisible()

  // Still signed in from the sign-up: the setup now lets the organization be created.
  await page.goto('/setup')
  await page.getByLabel('Ad', { exact: true }).fill(`Org ${user.id}`)
  await page.getByLabel('Kısa ad').fill(`org-${user.id}`)
  await page.getByRole('button', { name: 'Oluştur ve devam et' }).click()
  await page.getByText('Sonra yap', { exact: true }).click()
  await expect(page).toHaveURL(/\/apps/)

  await page.getByRole('complementary', { name: 'Ana menü' }).getByRole('button', { name: 'Hesap menüsü' }).click()
  await page.getByRole('menuitem', { name: 'Çıkış yap' }).click()
  await expect(page.getByRole('heading', { name: 'Tekrar hoş geldin' })).toBeVisible()

  // Sign in again: straight into the organization.
  await page.getByLabel('E-posta').fill(user.email)
  await page.getByLabel('Şifre', { exact: true }).fill(user.password)
  await page.getByRole('button', { name: 'Giriş yap' }).click()
  await expect(page).toHaveURL(/\/apps/)
  await expect(page.getByRole('button', { name: new RegExp(`Org ${user.id}`) }).first()).toBeVisible()
})
