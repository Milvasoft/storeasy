// SPDX-License-Identifier: AGPL-3.0-only
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  resolve: { alias: { storeasy: fileURLToPath(new URL('../storeasy-cli/src/index.ts', import.meta.url)) } },
  test: { include: ['test/**/*.test.ts'], testTimeout: 20000 },
})
