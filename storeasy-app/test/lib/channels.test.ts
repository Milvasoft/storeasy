import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { StoreKind } from '@/lib/api/types'
import { ALL_STORES, byStore, channelOf, channelOfKey, STORE_CHANNELS, storesWith } from '@/lib/stores/channels'
import { STORE_LOGO_ICON } from '@/components/ui/StoreIcons'
import { storeFromSlug, storeSlug } from '@/features/common/store'

// The API's channel table, read from its source: StoreKind, key and logo of every row.
function apiChannels(): Array<{ store: string; key: string }> {
  const source = readFileSync(resolve(process.cwd(), '../storeasy-api/src/Storeasy.Domain/Stores/StoreChannels.cs'), 'utf-8')
  return [...source.matchAll(/new\(StoreKind\.(\w+), "(\w+)"/g)].map((match) => ({ store: match[1], key: match[2] }))
}

describe('store channels', () => {
  it('has one row per store, with the keys of the API', () => {
    const stores = Object.values(StoreKind)

    expect(ALL_STORES).toEqual(stores)
    expect(STORE_CHANNELS.map((channel) => channel.key)).toEqual(apiChannels().map((channel) => channel.key))
    expect(apiChannels().map((channel) => StoreKind[channel.store as keyof typeof StoreKind])).toEqual(stores)
    expect(new Set(STORE_CHANNELS.map((channel) => channel.code)).size).toBe(stores.length)
  })

  it('drives slugs, logos and per store records', () => {
    for (const store of Object.values(StoreKind)) {
      expect(storeFromSlug(storeSlug(store))).toBe(store)
      expect(STORE_LOGO_ICON[store]).toBe(channelOf(store).logo)
    }
    expect(channelOfKey('winget')?.store).toBe(StoreKind.Winget)
    expect(storeFromSlug('ms')).toBe(StoreKind.Microsoft)
    expect(byStore(() => 0)).toEqual({ 1: 0, 2: 0, 3: 0, 4: 0 })
    expect(storesWith('hasMetrics')).not.toContain(StoreKind.Winget)
    expect(storesWith('hasReviews')).toEqual([StoreKind.Apple, StoreKind.Google])
  })
})
