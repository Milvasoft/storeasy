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
    case StoreKind.Winget:
      // winget has no store page: the package folder in winget-pkgs and its pull requests stand in.
      return {
        publicUrl: id
          ? `https://github.com/microsoft/winget-pkgs/tree/master/manifests/${record.ExternalId![0].toLowerCase()}/${record.ExternalId!.split('.').map(encodeURIComponent).join('/')}`
          : null,
        consoleUrl: id
          ? `https://github.com/microsoft/winget-pkgs/pulls?q=${encodeURIComponent(`is:pr ${record.ExternalId}`)}`
          : 'https://github.com/microsoft/winget-pkgs/pulls',
      }
    case StoreKind.Chrome:
      return {
        publicUrl: id ? `https://chromewebstore.google.com/detail/${id}` : null,
        consoleUrl: 'https://chrome.google.com/webstore/devconsole',
      }
    case StoreKind.Firefox:
      // The record keeps the numeric AMO id; the pages take it as well as the slug.
      return {
        publicUrl: id ? `https://addons.mozilla.org/firefox/addon/${id}/` : null,
        consoleUrl: id ? `https://addons.mozilla.org/developers/addon/${id}/edit` : 'https://addons.mozilla.org/developers/addons',
      }
    default:
      return { publicUrl: null, consoleUrl: '' }
  }
}
