import '@testing-library/jest-dom/vitest'
import { cleanup, configure } from '@testing-library/react'
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest'
import { dismissToast, getToasts } from '@/lib/toast/toast'
import { server } from './server'
import { resetTestState } from './state'

// jsdom implements neither layout nor the observer APIs: stub them once so every test renders the real components.
class NoopObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
  takeRecords() {
    return []
  }
}

// findBy* and waitFor wait up to 10 s instead of 1 s, so a test waits for what the user sees instead of racing the load of
// a full parallel run. The route chunks are loaded before the app renders (renderApp, preloadLazyPages), so this budget
// covers the page's own work, not module imports. A passing test does not wait longer; only a failing one does.
configure({ asyncUtilTimeout: 10_000 })

export const reducedMotion = { value: false }

beforeAll(() => {
  vi.stubGlobal('IntersectionObserver', NoopObserver)
  vi.stubGlobal('ResizeObserver', NoopObserver)
  vi.stubGlobal('scrollTo', () => {})
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: query.includes('reduced-motion') ? reducedMotion.value : false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }),
  })
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { writable: true, value: () => {} })
  // An unhandled request means the test hit an endpoint nobody stubbed: fail loudly instead of hanging.
  server.listen({ onUnhandledRequest: 'error' })
})

beforeEach(() => {
  localStorage.clear()
  sessionStorage.clear()
  resetTestState()
  reducedMotion.value = false
  document.documentElement.classList.remove('dark')
})

afterEach(() => {
  cleanup()
  server.resetHandlers()
  // The toast store lives at module level: a test must not see the previous test's toasts.
  for (const toast of getToasts()) {
    dismissToast(toast.id)
  }
})

afterAll(() => {
  server.close()
})
