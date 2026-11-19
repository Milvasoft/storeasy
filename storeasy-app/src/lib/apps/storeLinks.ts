import { StoreKind, type AppStoreRecordDto } from '@/lib/api/types'

export type StoreLinks = { publicUrl: string | null; consoleUrl: string }

// The record's public store page and its console page. The console addresses match the API's assisted steps.
// A public page exists only once the app is published; before that the store answers with its own "not found" page.
export function storeLinksOf(record: Pick<AppStoreRecordDto, 'Store' | 'ExternalId' | 'BundleId'>): StoreLinks {
  const id = record.ExternalId ? encodeURIComponent(record.ExternalId) : null
  switch (record.Store) {
    case StoreKind.Apple:
      return {
        publicUrl: id ? `https://apps.apple.com/app/id${id}` : null,
        consoleUrl: id ? `https://appstoreconnect.apple.com/apps/${id}/distribution` : 'https://appstoreconnect.apple.com/apps',
      }
    case StoreKind.Google: {
      const packageName = record.BundleId ?? record.ExternalId
      return {
        publicUrl: packageName ? `https://play.google.com/store/apps/details?id=${encodeURIComponent(packageName)}` : null,
        consoleUrl: 'https://play.google.com/console',
      }
    }
    case StoreKind.Microsoft:
      return {
        publicUrl: id ? `https://apps.microsoft.com/detail/${id}` : null,
        consoleUrl: id
          ? `https://partner.microsoft.com/dashboard/products/${id}/overview`
          : 'https://partner.microsoft.com/dashboard/apps-and-games/overview',
      }
    default:
      return { publicUrl: null, consoleUrl: '' }
  }
}
