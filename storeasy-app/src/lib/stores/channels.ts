import { StoreKind } from '@/lib/api/types'

// Every store and distribution channel in one table, the web mirror of the API's StoreChannels
// (storeasy-api/src/Storeasy.Domain/Stores/StoreChannels.cs). Shared screens ask this table instead of listing stores, so
// a new channel is one row here plus its own feature folder (docs/adding-a-store.md). channels.test.ts checks that every
// StoreKind has a row and that the keys match the API's.

export type StoreKey = 'apple' | 'google' | 'microsoft' | 'winget' | 'firefox'

export type CredentialExpiry = 'none' | 'optional' | 'required'

export interface StoreChannel {
  store: StoreKind
  // The ?store= value of the app screens and the rules catalog key.
  key: StoreKey
  // storeasy's own text code, shown when a connection has no icon.
  code: string
  // Key of the store's logo in StoreIcons.
  logo: string
  credentialExpiry: CredentialExpiry
  // Shared features the channel has; screens for the others say why they are missing.
  hasReviews: boolean
  hasMetrics: boolean
  hasListingScreens: boolean
  // Products the store API lists by itself; the others are added by id.
  listsProducts: boolean
}

export const STORE_CHANNELS: readonly StoreChannel[] = [
  {
    store: StoreKind.Apple,
    key: 'apple',
    code: 'AS',
    logo: 'appstore',
    credentialExpiry: 'none',
    hasReviews: true,
    hasMetrics: true,
    hasListingScreens: true,
    listsProducts: true,
  },
  {
    store: StoreKind.Google,
    key: 'google',
    code: 'GP',
    logo: 'googleplay',
    credentialExpiry: 'none',
    hasReviews: true,
    hasMetrics: true,
    hasListingScreens: true,
    listsProducts: false,
  },
  {
    store: StoreKind.Microsoft,
    key: 'microsoft',
    code: 'MS',
    logo: 'msstore',
    credentialExpiry: 'required',
    hasReviews: false,
    hasMetrics: true,
    hasListingScreens: false,
    listsProducts: true,
  },
  {
    store: StoreKind.Winget,
    key: 'winget',
    code: 'WG',
    logo: 'winget',
    credentialExpiry: 'optional',
    hasReviews: false,
    hasMetrics: false,
    hasListingScreens: false,
    listsProducts: false,
  },
  {
    store: StoreKind.Firefox,
    key: 'firefox',
    code: 'FX',
    logo: 'firefoxaddons',
    credentialExpiry: 'none',
    hasReviews: true,
    hasMetrics: false,
    hasListingScreens: false,
    listsProducts: true,
  },
]

const BY_STORE = new Map(STORE_CHANNELS.map((channel) => [channel.store, channel]))
const BY_KEY = new Map(STORE_CHANNELS.map((channel) => [channel.key as string, channel]))

/** Every store, in StoreKind order. */
export const ALL_STORES: readonly StoreKind[] = STORE_CHANNELS.map((channel) => channel.store)

export function channelOf(store: StoreKind): StoreChannel {
  return BY_STORE.get(store) ?? STORE_CHANNELS[0]
}

export function channelOfKey(key: string | null | undefined): StoreChannel | null {
  return key ? (BY_KEY.get(key) ?? null) : null
}

/** Stores that have the shared feature. */
export function storesWith(feature: 'hasReviews' | 'hasMetrics' | 'hasListingScreens'): StoreKind[] {
  return STORE_CHANNELS.filter((channel) => channel[feature]).map((channel) => channel.store)
}

/** A record per store, built from the table (an exhaustive Record without listing the stores by hand). */
export function byStore<T>(value: (channel: StoreChannel) => T): Record<StoreKind, T> {
  return Object.fromEntries(STORE_CHANNELS.map((channel) => [channel.store, value(channel)])) as Record<StoreKind, T>
}
