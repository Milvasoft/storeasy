import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// Test runner config, separate from vite.config.ts so the dev/build pipeline stays untouched.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: [{ find: '@', replacement: fileURLToPath(new URL('./src', import.meta.url)) }],
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./test/support/setup.ts'],
    include: ['test/**/*.test.ts', 'test/**/*.test.tsx'],
    restoreMocks: true,
    clearMocks: true,
    testTimeout: 15_000,
    // expect.poll waits as long as findBy* (test/support/setup.ts) before it gives up.
    expect: { poll: { timeout: 5_000 } },
    // The suite must not depend on a developer's .env file: every value the app reads from the environment is pinned here.
    env: {
      VITE_API_URL: '',
      VITE_APP_VERSION: '9.9.9-test',
      VITE_SOURCE_URL: 'https://example.test/storeasy',
      VITE_MOCK_API: '',
    },
    // CI reads the junit file; locally the default reporter is the readable one.
    reporters: process.env.CI ? ['default', ['junit', { outputFile: './test-results/junit.xml' }]] : ['default'],
    coverage: {
      provider: 'v8',
      reporter: ['text-summary', 'lcov', 'cobertura', 'html'],
      reportsDirectory: './coverage',
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/main.tsx',
        'src/vite-env.d.ts',
        'src/lib/i18n/{tr,en}.ts',
        'src/lib/api/types.ts',
        'src/components/layout/Icons.tsx',
        'src/mocks/**',
      ],
      // Set just under what the suite covers, so a regression fails the build but an ordinary refactor does not.
      // Raise them when coverage rises; never lower them to make a build pass.
      thresholds: { lines: 85, functions: 78, statements: 84, branches: 75 },
    },
  },
})
