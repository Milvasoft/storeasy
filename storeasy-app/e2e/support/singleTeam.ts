import type { Page } from '@playwright/test'
import { expect } from './fixtures'

// The sidebar of a single team administrator (Turkish UI), in order: address, link label, the page's heading. Each link
// must open its own page; a page that sends the user elsewhere (or nowhere) looks like a dead link.
export const SIDEBAR_TR: Array<{ path: string; label: string; heading: string }> = [
  { path: '/apps', label: 'Uygulamalar', heading: 'Uygulamalar' },
  { path: '/activity', label: 'Canlı akış', heading: 'Canlı akış' },
  { path: '/reviews', label: 'Yorumlar', heading: 'Yorumlar' },
  { path: '/compliance', label: 'Uyum takvimi', heading: 'Uyum takvimi' },
  { path: '/studio', label: 'Stüdyo', heading: 'Stüdyo' },
  { path: '/intake', label: 'Paket alma', heading: 'Paket alma' },
  { path: '/webhooks', label: "Webhook'lar", heading: "Webhook'lar" },
  { path: '/developer', label: "MCP ve token'lar", heading: "MCP ve token'lar" },
  { path: '/settings', label: 'Takım', heading: 'Takım ayarları' },
  { path: '/members', label: 'Üyeler', heading: 'Üyeler' },
  { path: '/connections', label: 'Mağaza bağlantıları', heading: 'Mağaza bağlantıları' },
  { path: '/audit', label: 'Denetim kaydı', heading: 'Denetim kaydı' },
  { path: '/rules', label: 'Mağaza kuralları', heading: 'Mağaza kuralları' },
  { path: '/admin/system', label: 'Sistem durumu', heading: 'Sistem durumu' },
  { path: '/admin/users', label: 'Kullanıcılar', heading: 'Kullanıcılar' },
  { path: '/admin/roles', label: 'Roller', heading: 'Roller' },
  { path: '/admin/languages', label: 'Diller', heading: 'Diller' },
  { path: '/admin/feature-flags', label: 'Özellik anahtarları', heading: 'Özellik anahtarları' },
  { path: '/admin/activity-logs', label: 'Aktivite logu', heading: 'Aktivite logu' },
  { path: '/admin/rules', label: 'Katalog önerileri', heading: 'Katalog önerileri' },
]

export function sidebar(page: Page, lang: 'tr' | 'en' = 'tr') {
  return page.getByRole('complementary', { name: lang === 'tr' ? 'Ana menü' : 'Main menu' })
}

const exactPath = (path: string) => new RegExp(`^[^?#]*${path}$`)

/**
 * Clicks every sidebar link of a single team administrator and checks, for each one, the address and the page's heading
 * right away and again once the page loaded its data (no redirect after the first paint), and the breadcrumb.
 */
export async function expectEverySidebarLinkToOpenItsPage(page: Page) {
  const nav = sidebar(page).getByRole('navigation', { name: 'Ana menü' })
  const hrefs = await nav.getByRole('link').evaluateAll((links) => links.map((link) => link.getAttribute('href')))
  expect(hrefs).toEqual(SIDEBAR_TR.map((item) => item.path))

  for (const item of SIDEBAR_TR) {
    await nav.locator(`a[href="${item.path}"]`).click()
    const heading = page.getByRole('heading', { level: 1, name: item.heading, exact: true })
    await expect(heading, item.path).toBeVisible()
    await expect(page, item.path).toHaveURL(exactPath(item.path))
    await page.waitForLoadState('networkidle')
    await expect(page, item.path).toHaveURL(exactPath(item.path))
    await expect(heading, item.path).toBeVisible()
    await expect(page.getByRole('navigation', { name: 'Sayfa yolu' }), item.path).toContainText(item.label)
  }
}
