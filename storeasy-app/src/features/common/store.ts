import { StoreKind } from '@/lib/api/types'
import { byStore, channelOfKey, type StoreKey } from '@/lib/stores/channels'

// The ?store= value of the app screens (design: /apps/:appId/readiness?store=apple).
export type StoreSlug = StoreKey

const SLUGS: Record<StoreKind, StoreSlug> = byStore((channel) => channel.key)

export function storeSlug(store: StoreKind): StoreSlug {
  return SLUGS[store]
}

export function storeFromSlug(slug: string | null): StoreKind | null {
  switch (slug) {
    case 'apple':
      return StoreKind.Apple
    case 'google':
      return StoreKind.Google
    case 'microsoft':
    case 'ms':
      return StoreKind.Microsoft
    default:
      return channelOfKey(slug)?.store ?? null
  }
}

/** A readiness FixRoute ("listing/texts?locale=tr") as an app screen address carrying the store. */
export function fixRouteHref(appId: number, store: StoreKind, fixRoute: string): string {
  const [path, query = ''] = fixRoute.split('?')
  // A build is linked from the release screen; the builds tab itself has no write action.
  const target = path === 'release/builds' ? 'release/versions' : path
  const params = new URLSearchParams(query)
  params.set('store', storeSlug(store))
  return `/apps/${appId}/${target}?${params.toString()}`
}
