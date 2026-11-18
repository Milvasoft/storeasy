import { useSyncExternalStore } from 'react'

const QUERY = '(prefers-reduced-motion: reduce)'

function media(): MediaQueryList | null {
  return typeof window !== 'undefined' && typeof window.matchMedia === 'function' ? window.matchMedia(QUERY) : null
}

function subscribe(listener: () => void): () => void {
  const list = media()
  list?.addEventListener('change', listener)
  return () => list?.removeEventListener('change', listener)
}

function snapshot(): boolean {
  return media()?.matches ?? false
}

/**
 * True when the OS asks for reduced motion. JS driven and SMIL animations read this (CSS ones are stopped globally in
 * index.css): the live Hat scene is then replaced by its status list (design 11.2).
 */
export function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(subscribe, snapshot, () => false)
}
