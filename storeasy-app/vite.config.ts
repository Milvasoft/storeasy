import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const proxyTarget = env.VITE_API_PROXY || 'http://localhost:5000'

  // The version the app reports (footer, support): VITE_APP_VERSION when the pipeline sets one, else package.json.
  const appVersion =
    env.VITE_APP_VERSION || (JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf-8')) as { version: string }).version

  return {
    plugins: [react()],
    resolve: {
      alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
    },
    define: {
      'import.meta.env.VITE_APP_VERSION': JSON.stringify(appVersion),
    },
    server: {
      port: 5173,
      // Poll for file changes so edits written by external tools are picked up reliably on Windows.
      watch: { usePolling: true, interval: 300 },
      // Empty VITE_API_URL: the app calls relative /api, /hubs and /mcp and the dev server proxies them (one origin, no
      // CORS), the same layout nginx gives production (plan section 10).
      proxy: env.VITE_API_URL
        ? undefined
        : {
            '/api': { target: proxyTarget, changeOrigin: true, secure: false },
            '/hubs': { target: proxyTarget, changeOrigin: true, secure: false, ws: true },
            '/mcp': { target: proxyTarget, changeOrigin: true, secure: false },
          },
    },
  }
})
