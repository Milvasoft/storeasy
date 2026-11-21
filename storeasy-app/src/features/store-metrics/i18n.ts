import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'

// The store performance (ASO conversion analytics) screens. English is the source shape, Turkish is typed with it, so a
// missing or extra key fails the build.
export const storeMetricsEn = {
  title: 'Store performance',
  subtitle: 'How people find your store page and how many of them download: impressions, page views, downloads and conversion, per day.',
  orgTitle: 'Store performance',
  orgSubtitle: 'Every app over the same period, with the change against the period before.',
  fetchNow: 'Fetch now',
  fetching: 'Fetching...',
  fetchStarted: 'Reading the store reports. The numbers update when the jobs finish.',
  fetchNoRecord: 'No store record with a connection to read.',
  fetchNoPermission: 'Fetching needs the Editor role or above.',
  delayNote: 'Stores publish these numbers a day or two late; storeasy reads them every morning (05:30 UTC).',
  range: {
    label: 'Period',
    days: '{days} days',
    custom: 'Custom',
    customLabel: 'Custom period',
    showing: '{from} to {to}, compared with {previousFrom} to {previousTo}',
  },
  filters: {
    store: 'Store',
    allStores: 'All stores',
    country: 'Country',
    allCountries: 'All countries',
    source: 'Source',
    allSources: 'All sources',
  },
  metric: {
    impressions: 'Impressions',
    pageViews: 'Page views',
    downloads: 'Downloads',
    redownloads: 'Redownloads',
    conversionRate: 'Conversion rate',
  },
  metricHint: {
    impressions: 'App Store only: times the app was shown in a list.',
    pageViews: 'Store page views (Play: store listing visitors; Microsoft: users).',
    downloads: 'First time downloads (Play: store listing acquisitions).',
    conversionRate: 'Downloads divided by page views, over the stores that report both.',
  },
  notReported: 'Not reported',
  change: {
    up: '{value} more than the previous period',
    down: '{value} less than the previous period',
    flat: 'Same as the previous period',
    none: 'No earlier data to compare',
  },
  chart: {
    title: 'Over time',
    pick: 'Metric on the chart',
    label: '{metric} per day, {from} to {to}',
    empty: 'No data for this period.',
  },
  breakdown: {
    sources: 'By source',
    countries: 'By country',
    stores: 'By store',
    name: 'Name',
    empty: 'No breakdown for this selection.',
    playSourceNote:
      'Google Play reports countries and sources separately, so a country filter shows sources only for the App Store and Microsoft Store.',
  },
  source: {
    all: 'All sources',
    search: 'Search',
    browse: 'Browse',
    appReferrer: 'App referrer',
    webReferrer: 'Web referrer',
    referral: 'Third-party referral',
    appClip: 'App Clip',
    notification: 'Notification',
    storeClient: 'Store app',
    storeWeb: 'Store web',
    other: 'Other',
    unavailable: 'Unknown',
  },
  unknownCountry: 'Unknown country',
  feeds: {
    title: 'Data sources',
    noConnection: 'This record has no store connection.',
    lastFetched: 'Last read {time}',
    neverFetched: 'Not read yet',
    dataUntil: 'Data until {date}',
    since: 'Since {date}',
    enabledBy: 'Enabled by {user} on {date}',
    status: {
      notEnabled: 'Not enabled',
      waiting: 'Waiting for data',
      active: 'Active',
      needsSetup: 'Needs setup',
      accessDenied: 'No access',
      failed: 'Read failed',
    },
  },
  apple: {
    enable: 'Enable analytics reports',
    enabling: 'Enabling...',
    notEnabled:
      'App Store Connect sends these numbers only after the analytics reports are requested once for the app. The request is the only thing storeasy writes to Apple for this page.',
    confirmTitle: 'Enable the App Store analytics reports?',
    confirmBody:
      'storeasy asks App Store Connect to generate the ongoing analytics reports of this app (an analytics report request). It changes nothing in your listing and cannot be undone from storeasy, but Apple stops it on its own when nobody reads the reports for a long time. The API key needs the Admin role.',
    confirmLabel: 'Enable',
    started: 'Requested. Apple delivers the first data in one or two days.',
    waiting:
      'Requested. Apple generates the first reports one or two days after the request, then every day with a delay of one to two days.',
    noPermission: 'Enabling needs the Manage connections permission (Owner or Admin).',
    accessDenied:
      'App Store Connect refused the key. Creating the request needs the Admin role; reading the reports needs Admin or Sales and Reports.',
  },
  google: {
    bucket: 'Reports bucket',
    bucketHint:
      'Play Console, Download reports, Statistics: copy the Cloud Storage URI (gs://pubsite_prod_rev_...). The service account must be a Play Console user with "View app information and download bulk reports" for the whole account.',
    bucketPlaceholder: 'gs://pubsite_prod_rev_01234567890987654321',
    save: 'Save',
    saved: 'Bucket saved. The Play reports are being read.',
    cleared: 'Bucket cleared.',
    needsSetup:
      'Enter the Cloud Storage bucket Play Console exports your statistics to. storeasy reads the last 6 months of monthly files, then the new days every morning.',
    accessDenied:
      'Google refused to read the bucket. Check that the service account is invited in Play Console with "View app information and download bulk reports" and that the bucket is the one shown under Download reports.',
    noPermission: 'Only members who manage connections can change the bucket.',
  },
  microsoft: {
    waiting: 'storeasy reads the last 90 days of acquisitions and page views from Partner Center with your existing connection.',
    accessDenied: 'Partner Center refused the analytics read. The Entra app needs a role that can read analytics (Manager or Developer).',
  },
  failedHint: 'The last read failed; storeasy tries again with the next read.',
  empty: {
    title: 'No store performance data yet',
    body: 'Data arrives once a source below is set up. Each store reports on its own schedule.',
    filtered: 'No data for this selection. Try another period, store, country or source.',
  },
  summary: {
    app: 'App',
    total: 'All apps',
    empty: 'No app has store performance data for this period yet. Open an app and set up its data sources on the Store performance tab.',
    open: 'Open {app}',
  },
}

export type StoreMetricsDictionary = typeof storeMetricsEn

export const storeMetricsTr: StoreMetricsDictionary = {
  title: 'Mağaza performansı',
  subtitle: 'İnsanlar mağaza sayfanı nasıl buluyor ve kaçı indiriyor: gösterim, sayfa görüntüleme, indirme ve dönüşüm, gün gün.',
  orgTitle: 'Mağaza performansı',
  orgSubtitle: 'Tüm uygulamalar aynı dönemde, bir önceki döneme göre değişimiyle.',
  fetchNow: 'Şimdi çek',
  fetching: 'Çekiliyor...',
  fetchStarted: 'Mağaza raporları okunuyor. İşler bitince rakamlar güncellenir.',
  fetchNoRecord: 'Okunacak, bağlantısı olan bir mağaza kaydı yok.',
  fetchNoPermission: 'Çekmek için Editör veya üstü rol gerekir.',
  delayNote: 'Mağazalar bu rakamları bir iki gün gecikmeyle yayınlar; storeasy her sabah (05.30 UTC) okur.',
  range: {
    label: 'Dönem',
    days: '{days} gün',
    custom: 'Özel',
    customLabel: 'Özel dönem',
    showing: '{from} ile {to} arası, {previousFrom} ile {previousTo} arasıyla karşılaştırılıyor',
  },
  filters: {
    store: 'Mağaza',
    allStores: 'Tüm mağazalar',
    country: 'Ülke',
    allCountries: 'Tüm ülkeler',
    source: 'Kaynak',
    allSources: 'Tüm kaynaklar',
  },
  metric: {
    impressions: 'Gösterim',
    pageViews: 'Sayfa görüntüleme',
    downloads: 'İndirme',
    redownloads: 'Yeniden indirme',
    conversionRate: 'Dönüşüm oranı',
  },
  metricHint: {
    impressions: 'Yalnızca App Store: uygulamanın bir listede gösterilme sayısı.',
    pageViews: 'Mağaza sayfası görüntülemeleri (Play: mağaza girişi ziyaretçileri; Microsoft: kullanıcı).',
    downloads: 'İlk indirmeler (Play: mağaza girişinden edinmeler).',
    conversionRate: 'İndirmelerin sayfa görüntülemelerine oranı, ikisini de bildiren mağazalarda.',
  },
  notReported: 'Bildirilmiyor',
  change: {
    up: 'Önceki döneme göre {value} fazla',
    down: 'Önceki döneme göre {value} az',
    flat: 'Önceki dönemle aynı',
    none: 'Karşılaştıracak önceki veri yok',
  },
  chart: {
    title: 'Zaman içinde',
    pick: 'Grafikteki metrik',
    label: 'Günlük {metric}, {from} ile {to} arası',
    empty: 'Bu dönemde veri yok.',
  },
  breakdown: {
    sources: 'Kaynağa göre',
    countries: 'Ülkeye göre',
    stores: 'Mağazaya göre',
    name: 'Ad',
    empty: 'Bu seçim için dağılım yok.',
    playSourceNote:
      'Google Play ülkeleri ve kaynakları ayrı bildirir; ülke seçiliyken kaynaklar yalnızca App Store ve Microsoft Store için görünür.',
  },
  source: {
    all: 'Tüm kaynaklar',
    search: 'Arama',
    browse: 'Göz atma',
    appReferrer: 'Uygulama yönlendirmesi',
    webReferrer: 'Web yönlendirmesi',
    referral: 'Üçüncü taraf yönlendirmesi',
    appClip: 'App Clip',
    notification: 'Bildirim',
    storeClient: 'Mağaza uygulaması',
    storeWeb: 'Mağaza web sitesi',
    other: 'Diğer',
    unavailable: 'Bilinmiyor',
  },
  unknownCountry: 'Bilinmeyen ülke',
  feeds: {
    title: 'Veri kaynakları',
    noConnection: 'Bu kaydın mağaza bağlantısı yok.',
    lastFetched: 'Son okuma {time}',
    neverFetched: 'Henüz okunmadı',
    dataUntil: '{date} tarihine kadar veri',
    since: '{date} tarihinden beri',
    enabledBy: '{user} tarafından {date} tarihinde etkinleştirildi',
    status: {
      notEnabled: 'Etkin değil',
      waiting: 'Veri bekleniyor',
      active: 'Etkin',
      needsSetup: 'Kurulum gerekli',
      accessDenied: 'Erişim yok',
      failed: 'Okuma başarısız',
    },
  },
  apple: {
    enable: 'Analitik raporlarını etkinleştir',
    enabling: 'Etkinleştiriliyor...',
    notEnabled:
      "App Store Connect bu rakamları, uygulama için analitik raporları bir kez istendikten sonra gönderir. Bu istek, storeasy'nin bu sayfa için Apple'a yazdığı tek şeydir.",
    confirmTitle: 'App Store analitik raporları etkinleştirilsin mi?',
    confirmBody:
      "storeasy, App Store Connect'ten bu uygulamanın sürekli analitik raporlarını üretmesini ister (analytics report request). Mağaza sayfanda hiçbir şeyi değiştirmez ve storeasy'den geri alınamaz; raporlar uzun süre okunmazsa Apple isteği kendisi durdurur. API anahtarının Admin rolü olmalı.",
    confirmLabel: 'Etkinleştir',
    started: 'İstendi. Apple ilk veriyi bir iki gün içinde gönderir.',
    waiting: 'İstendi. Apple ilk raporları istekten bir iki gün sonra, ardından her gün bir iki gün gecikmeyle üretir.',
    noPermission: 'Etkinleştirmek için bağlantı yönetme izni gerekir (Sahip veya Yönetici).',
    accessDenied:
      'App Store Connect anahtarı reddetti. İsteği oluşturmak için Admin, raporları okumak için Admin veya Sales and Reports rolü gerekir.',
  },
  google: {
    bucket: 'Rapor paketi (bucket)',
    bucketHint: `Play Console, Raporları indir, İstatistikler: Cloud Storage URI'sini kopyala (gs://pubsite_prod_rev_...). Hizmet hesabı, tüm hesap için "Uygulama bilgilerini görüntüleme ve toplu raporları indirme" izniyle Play Console kullanıcısı olmalı.`,
    bucketPlaceholder: 'gs://pubsite_prod_rev_01234567890987654321',
    save: 'Kaydet',
    saved: 'Paket kaydedildi. Play raporları okunuyor.',
    cleared: 'Paket kaldırıldı.',
    needsSetup:
      "Play Console'un istatistiklerini aktardığı Cloud Storage paketini gir. storeasy son 6 ayın aylık dosyalarını, sonra her sabah yeni günleri okur.",
    accessDenied: `Google paketi okumaya izin vermedi. Hizmet hesabının Play Console'a "Uygulama bilgilerini görüntüleme ve toplu raporları indirme" izniyle davet edildiğini ve paketin Raporları indir altında görünen paket olduğunu kontrol et.`,
    noPermission: 'Paketi yalnızca bağlantıları yöneten üyeler değiştirebilir.',
  },
  microsoft: {
    waiting: "storeasy, mevcut bağlantınla Partner Center'dan son 90 günün edinmelerini ve sayfa görüntülemelerini okur.",
    accessDenied:
      'Partner Center analitik okumayı reddetti. Entra uygulamasının analitik okuyabilen bir rolü olmalı (Manager veya Developer).',
  },
  failedHint: 'Son okuma başarısız oldu; storeasy bir sonraki okumada yeniden dener.',
  empty: {
    title: 'Henüz mağaza performansı verisi yok',
    body: 'Aşağıdaki kaynaklardan biri kurulunca veri gelmeye başlar. Her mağaza kendi takviminde bildirir.',
    filtered: 'Bu seçim için veri yok. Başka bir dönem, mağaza, ülke veya kaynak dene.',
  },
  summary: {
    app: 'Uygulama',
    total: 'Tüm uygulamalar',
    empty:
      'Bu dönemde hiçbir uygulamanın mağaza performansı verisi yok. Bir uygulamayı açıp Mağaza performansı sekmesinden veri kaynaklarını kur.',
    open: '{app} uygulamasını aç',
  },
}

const DICTIONARIES: Record<Lang, StoreMetricsDictionary> = { en: storeMetricsEn, tr: storeMetricsTr }

/** The store performance texts in the UI language. */
export function useStoreMetricsText(): StoreMetricsDictionary {
  return DICTIONARIES[useI18n().lang]
}
