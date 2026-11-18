/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Backend origin; empty means same origin (dev proxy, or nginx in production). */
  readonly VITE_API_URL?: string
  /** Dev-only proxy target. */
  readonly VITE_API_PROXY?: string
  /** Filled by vite.config.ts from package.json when the pipeline does not set it. */
  readonly VITE_APP_VERSION?: string
  /** AGPL-3.0 section 13: where this deployment's source code is published. Empty means the upstream repository. */
  readonly VITE_SOURCE_URL?: string
  /** Dev only: 'true' answers every API call and the live hub from the in-browser mock (src/mocks). */
  readonly VITE_MOCK_API?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
