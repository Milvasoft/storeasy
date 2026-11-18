import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests: the real browser against the real API (no mocks anywhere). The API behind E2E_API_URL has to be up
 * first (see e2e/README.md). The app is served by Vite's preview server from a production build, so what the test
 * drives is what ships.
 */
const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5000'
const PORT = Number(process.env.E2E_PORT ?? 4173)

export default defineConfig({
  testDir: './e2e/specs',
  outputDir: './e2e/.artifacts',
  retries: process.env.CI ? 1 : 0,
  // One worker: rate-limited endpoints (sign-in) are shared by every test.
  workers: 1,
  timeout: 150_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['list'], ['junit', { outputFile: 'e2e/results/junit.xml' }]] : [['list']],
  use: {
    baseURL: `http://localhost:${PORT}`,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    locale: 'tr-TR',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `npm run build && npm run preview -- --port ${PORT} --strictPort`,
    port: PORT,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    env: { VITE_API_URL: API_URL },
  },
})
