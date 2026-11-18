import { test as base, expect, type Page } from '@playwright/test'
import { apiCall, login, uniqueIdentity, type Identity } from './api'
import { linkIn, waitForMail } from './mailpit'

export { expect }

export interface TestUser extends Identity {
  token: string
}

export interface TestOrganization {
  id: number
  name: string
  slug: string
}

/** Registers an account over the API and verifies its e-mail with the link from the inbox. */
export async function registerVerifiedUser(prefix = 'e2e'): Promise<TestUser> {
  const identity = uniqueIdentity(prefix)
  await apiCall('POST', '/Account/register', {
    Email: identity.email,
    Name: identity.name,
    Surname: identity.surname,
    Password: identity.password,
  })
  const mail = await waitForMail(identity.email)
  const token = new URL(linkIn(mail, '/verify-email')).searchParams.get('token')
  await apiCall('POST', '/Account/email/verify', { Token: token })
  return { ...identity, token: await login(identity.email, identity.password) }
}

export async function createOrganization(user: TestUser, name = `Org ${user.id}`): Promise<TestOrganization> {
  const slug = `org-${user.id}`
  const id = await apiCall<number>('POST', '/Organizations/organization', { Name: name, Slug: slug }, { token: user.token })
  return { id, name, slug }
}

/** Signs in through the login page (Turkish or English UI) and waits until the app shell is up. */
export async function signIn(page: Page, user: Identity, lang: 'tr' | 'en' = 'tr') {
  const text =
    lang === 'tr'
      ? { email: 'E-posta', password: 'Şifre', submit: 'Giriş yap' }
      : { email: 'E-mail', password: 'Password', submit: 'Sign in' }
  await page.goto('/login')
  await page.getByLabel(text.email).fill(user.email)
  await page.getByLabel(text.password, { exact: true }).fill(user.password)
  await page.getByRole('button', { name: text.submit }).click()
  await expect(page).not.toHaveURL(/\/login/)
}

export const test = base.extend<{ user: TestUser; organization: TestOrganization }>({
  // eslint-disable-next-line no-empty-pattern
  user: async ({}, provide) => {
    await provide(await registerVerifiedUser())
  },
  organization: async ({ user }, provide) => {
    await provide(await createOrganization(user))
  },
})
