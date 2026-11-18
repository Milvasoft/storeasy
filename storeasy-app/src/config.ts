// Central runtime configuration derived from Vite env. Values are baked in at build time.
const API_VERSION = 'v1.0'
const rawApiUrl = (import.meta.env.VITE_API_URL || '').replace(/\/+$/, '')

// When VITE_API_URL is empty the app calls relative /api (the Vite dev proxy, or the same origin behind nginx).
export const API_BASE = `${rawApiUrl}/api/${API_VERSION}`

// The live activity hub (SignalR). Same origin as the API (plan section 10: /api, /hubs and /mcp behind one host).
export const ACTIVITY_HUB_URL = `${rawApiUrl}/hubs/activity`

export const APP_VERSION = import.meta.env.VITE_APP_VERSION || 'web'

// The upstream repository. A deployment that changes the code points VITE_SOURCE_URL at its own fork (AGPL-3.0 section 13).
export const UPSTREAM_SOURCE_URL = 'https://github.com/Milvasoft/storeasy'
export const SOURCE_URL = import.meta.env.VITE_SOURCE_URL || UPSTREAM_SOURCE_URL

// Dev only: the in-browser mock answers the API and the hub (npm run dev with VITE_MOCK_API=true).
export const MOCK_API = import.meta.env.DEV && import.meta.env.VITE_MOCK_API === 'true'

// Prefix of every localStorage key (`storeasy.lang`) and window event (`storeasy:auth-lost`) the app owns.
export const APP_KEY = 'storeasy'
