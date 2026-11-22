import { ALL_STORES, byStore } from '@/lib/stores/channels'
import { StoreKind, type AppListDto, type AppStoreRecordDto, type IsoDateTime } from '@/lib/api/types'

// Small pure helpers of the apps screens (design 6.1-6.7), unit tested.

// Colors of the initial icon when the store gave no icon; white text on each is above 4.5:1.
export const APP_COLORS = ['#4636ff', '#0f766e', '#a1430b', '#be123c', '#6d28d9', '#1d4ed8', '#15803d', '#86198f'] as const

export function appColor(appId: number): string {
  return APP_COLORS[Math.abs(appId) % APP_COLORS.length]
}

// Play package names, the API's rule (AddGoogleAppCommandValidator).
export const PACKAGE_NAME_PATTERN = /^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$/

export function isValidPackageName(value: string): boolean {
  return PACKAGE_NAME_PATTERN.test(value.trim())
}

export const STORE_ORDER: readonly StoreKind[] = ALL_STORES

/**
 * The order of the apps list when no sort is asked (GetAppListQueryHandler): the apps a member placed by their place,
 * then the apps never placed by name.
 */
export function compareDisplayOrder(a: AppListDto, b: AppListDto): number {
  const placedA = a.DisplayOrder ?? null
  const placedB = b.DisplayOrder ?? null
  if (placedA !== null && placedB !== null && placedA !== placedB) {
    return placedA - placedB
  }
  if ((placedA === null) !== (placedB === null)) {
    return placedA === null ? 1 : -1
  }
  return a.Name.localeCompare(b.Name, 'tr', { sensitivity: 'base' }) || a.Id - b.Id
}

/** The ids with one id moved by offset places (-1 earlier, 1 later), kept inside the list. */
export function moveBy(ids: readonly number[], id: number, offset: number): number[] {
  const from = ids.indexOf(id)
  const to = Math.min(ids.length - 1, Math.max(0, from + offset))
  return from < 0 || from === to ? [...ids] : moveTo(ids, id, ids[to])
}

/** The ids with one id moved to the place of another one (the target and the ones between shift by one). */
export function moveTo(ids: readonly number[], id: number, targetId: number): number[] {
  const from = ids.indexOf(id)
  const to = ids.indexOf(targetId)
  if (from < 0 || to < 0 || from === to) {
    return [...ids]
  }
  const next = ids.filter((value) => value !== id)
  next.splice(to, 0, id)
  return next
}

export function recordsOf(app: AppListDto): AppStoreRecordDto[] {
  return [...(app.Stores ?? [])].sort((a, b) => a.Store - b.Store)
}

/** Stores whose records are added by id and never listed by a sync: only an app made of these can be deleted. */
const DELETABLE_STORES: readonly StoreKind[] = [StoreKind.Winget, StoreKind.Chrome, StoreKind.Firefox]

/** True when every store record of the app was added by id (Chrome, Firefox, winget), so the app can be deleted. */
export function isDeletable(app: AppListDto): boolean {
  return (app.Stores ?? []).every((record) => DELETABLE_STORES.includes(record.Store))
}

export function recordFor(app: AppListDto, store: StoreKind): AppStoreRecordDto | null {
  return app.Stores?.find((record) => record.Store === store) ?? null
}

/** The identifier shown under the app's name: the first bundle id or package name, else the store id. */
export function primaryIdentifier(app: AppListDto): string | null {
  const records = recordsOf(app)
  return records.find((record) => record.BundleId)?.BundleId ?? records[0]?.ExternalId ?? null
}

/** The newest sync among the apps' records; null when nothing was synced yet. */
export function lastSyncOf(apps: AppListDto[]): IsoDateTime | null {
  let latest: IsoDateTime | null = null
  for (const app of apps) {
    for (const record of app.Stores ?? []) {
      if (record.SyncedDate && (!latest || Date.parse(record.SyncedDate) > Date.parse(latest))) {
        latest = record.SyncedDate
      }
    }
  }
  return latest
}

/** How many of the apps have a record in each store (the store chips' counters). */
export function storeCounts(apps: AppListDto[]): Record<StoreKind, number> {
  const counts = byStore(() => 0)
  for (const app of apps) {
    for (const store of new Set((app.Stores ?? []).map((record) => record.Store))) {
      counts[store] += 1
    }
  }
  return counts
}

/** The connections the app's records are read with, once each: what "Sync" on the app queues. */
export function connectionIdsOf(app: AppListDto): number[] {
  return [...new Set((app.Stores ?? []).map((record) => record.StoreConnectionId).filter((id): id is number => typeof id === 'number'))]
}

/**
 * Records of other apps that this app can take: one record per store, so only stores this app does not have yet
 * (design 6.3: "a store record belongs to one app at a time").
 */
export function linkCandidates(app: AppListDto, others: AppListDto[]): Array<{ app: AppListDto; record: AppStoreRecordDto }> {
  const taken = new Set((app.Stores ?? []).map((record) => record.Store))
  return others
    .filter((other) => other.Id !== app.Id)
    .flatMap((other) => recordsOf(other).map((record) => ({ app: other, record })))
    .filter(({ record }) => !taken.has(record.Store))
}
