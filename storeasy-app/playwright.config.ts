import { defineConfig, devices } from '@playwright/test'
import { clientAddress } from './e2e/support/api'

/**
 * End-to-end tests: the real browser against the real API (no mocks anywhere).
 *
 * - E2E_BASE_URL set (the full stack of deploy/e2e, `npm run e2e:up`): the tests drive the web container itself, so nginx,
 *   the single origin proxy (/api, /hubs, /mcp, /webhooks, /health) and the production images are what is tested.
 * - Otherwise the app is built and served by Vite's preview server against the API behind E2E_API_URL.
 */
const BASE_URL = process.env.E2E_BASE_URL
const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5000'
const PORT = Number(process.env.E2E_PORT ?? 4173)
// The deployment mode the stack runs (deploy/e2e/stack.mjs up --mode): each has its own suite, since a single team
// instance has no sign-up and one organization, and the upgrade suite needs a database in a given state.
const MODE = process.env.E2E_MODE ?? 'multi'
const TEST_DIRS: Record<string, string> = { multi: './e2e/specs', single: './e2e/single-team', upgrade: './e2e/upgrade' }
if (!TEST_DIRS[MODE]) {
  throw new Error(`E2E_MODE=${MODE}: multi, single or upgrade`)
}

export default defineConfig({
  testDir: TEST_DIRS[MODE],
  outputDir: './e2e/.artifacts',
  retries: process.env.CI ? 1 : 0,
  // One worker: rate-limited endpoints (sign-in) are shared by every test.
  workers: 1,
  timeout: 150_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['list'], ['junit', { outputFile: 'e2e/results/junit.xml' }]] : [['list']],
  use: {
    baseURL: BASE_URL ?? `http://localhost:${PORT}`,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    locale: 'tr-TR',
    // The browser's own client address for the API's rate limits (see clientAddress).
    extraHTTPHeaders: { 'X-Forwarded-For': clientAddress() },
    // A step that cannot happen fails in seconds, not at the end of the test timeout.
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: BASE_URL
    ? undefined
    : {
        command: `npm run build && npm run preview -- --port ${PORT} --strictPort`,
        port: PORT,
        reuseExistingServer: !process.env.CI,
        timeout: 180_000,
        env: { VITE_API_URL: API_URL },
      },
})
