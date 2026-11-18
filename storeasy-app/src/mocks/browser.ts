import { getResponse } from 'msw'
import { setupWorker } from 'msw/browser'
import { mockHandlers } from './handlers'

// Starts the service worker that answers /api in the browser (dev only, VITE_MOCK_API=true). Anything else, the fonts
// and the Vite client included, passes through untouched. Where a service worker cannot register (some embedded
// browsers, private modes), the same handlers answer through a fetch wrapper instead.
export async function startMockApi(): Promise<void> {
  try {
    const worker = setupWorker(...mockHandlers)
    await worker.start({ onUnhandledRequest: 'bypass', quiet: true })
  } catch {
    const realFetch = window.fetch.bind(window)
    window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
      const request = new Request(input, init)
      const mocked = new URL(request.url).pathname.startsWith('/api/') ? await getResponse(mockHandlers, request) : undefined
      return mocked ?? realFetch(input, init)
    }
  }
}
