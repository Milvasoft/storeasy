// Light / dark / system. Applied by toggling `.dark` on <html> (Tailwind darkMode: 'class'); 'system' follows the OS
// and updates live. applyTheme() runs in main.tsx before the first render, so there is no flash.
import { appEvent } from '@/lib/utils/events'
import { readStorage, StorageKey, writeStorage } from '@/lib/utils/storage'

export type Theme = 'light' | 'dark' | 'system'

export const THEMES: readonly Theme[] = ['light', 'dark', 'system']

// The page background of each theme (--bg), for the browser chrome.
const THEME_COLORS = { light: '#FAFAF8', dark: '#0A0A0B' } as const

export const THEME_EVENT = appEvent('theme')

function prefersDark(): boolean {
  return (
    typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia('(prefers-color-scheme: dark)').matches
  )
}

export function getTheme(): Theme {
  const stored = readStorage(StorageKey.Theme)
  if (stored === 'light' || stored === 'dark' || stored === 'system') {
    return stored
  }
  return 'system'
}

export function resolvedTheme(theme: Theme = getTheme()): 'light' | 'dark' {
  if (theme === 'system') {
    return prefersDark() ? 'dark' : 'light'
  }
  return theme
}

export function applyTheme(theme: Theme = getTheme()): void {
  if (typeof document === 'undefined') {
    return
  }
  const resolved = resolvedTheme(theme)
  document.documentElement.classList.toggle('dark', resolved === 'dark')
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', THEME_COLORS[resolved])
}

export function setTheme(theme: Theme): void {
  writeStorage(StorageKey.Theme, theme)
  applyTheme(theme)
  window.dispatchEvent(new CustomEvent(THEME_EVENT))
}

// For useSyncExternalStore: a component that shows the choice re-renders when any other one changes it.
export function subscribeTheme(listener: () => void): () => void {
  window.addEventListener(THEME_EVENT, listener)
  return () => window.removeEventListener(THEME_EVENT, listener)
}

export function watchSystemTheme(): () => void {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return () => undefined
  }
  const media = window.matchMedia('(prefers-color-scheme: dark)')
  const onChange = () => {
    if (getTheme() === 'system') {
      applyTheme('system')
    }
  }
  media.addEventListener('change', onChange)
  return () => media.removeEventListener('change', onChange)
}
