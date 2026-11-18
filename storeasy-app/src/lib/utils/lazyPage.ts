import { lazy, type ComponentType, type LazyExoticComponent } from 'react'

// React.lazy for a module whose page is a named export: lazyPage(() => import('@/pages/org/AuditPage'), 'AuditPage').
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- React.lazy itself takes a component with any props
export function lazyPage<K extends string, M extends Record<K, ComponentType<any>>>(
  load: () => Promise<M>,
  name: K,
): LazyExoticComponent<M[K]> {
  return lazy(() => load().then((module) => ({ default: module[name] })))
}
