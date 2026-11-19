import { lazy, type ComponentType, type LazyExoticComponent } from 'react'

// Every loader handed to lazyPage, so preloadLazyPages can fetch the chunks ahead of a navigation.
const loaders = new Set<() => Promise<unknown>>()

// React.lazy for a module whose page is a named export: lazyPage(() => import('@/pages/org/AuditPage'), 'AuditPage').
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- React.lazy itself takes a component with any props
export function lazyPage<K extends string, M extends Record<K, ComponentType<any>>>(
  load: () => Promise<M>,
  name: K,
): LazyExoticComponent<M[K]> {
  loaders.add(load)
  return lazy(() => load().then((module) => ({ default: module[name] })))
}

/**
 * Loads every lazy page module now, including the lazy pages those modules declare in turn. The module cache keeps them,
 * so a later navigation renders without waiting for a chunk. The tests call it before rendering the whole app: under a
 * loaded parallel run, importing (and transforming) a route chunk took longer than a findBy waits.
 */
export async function preloadLazyPages(): Promise<void> {
  const done = new Set<() => Promise<unknown>>()
  while (done.size < loaders.size) {
    const pending = [...loaders].filter((load) => !done.has(load))
    pending.forEach((load) => done.add(load))
    await Promise.all(pending.map((load) => load()))
  }
}
