import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'

// The AI pre-review section and its entry points. English is the source shape, Turkish is typed with it, so a missing or
// extra key fails the build.
export const preReviewEn = {
  title: 'Pre-review',
  subtitle:
    'Claude reviews this submission the way the store reviewer would: the listing, the declarations and the screenshots. It is an estimate; it never blocks or sends a submission.',
  run: 'Run pre-review',
  running: 'Reviewing...',
  runningHint: 'The pre-review runs in the background and usually takes under a minute. You can leave this page.',
  queued: 'Waiting to start...',
  model: 'Model: {model}',
  noKeyHint: 'Add an Anthropic API key in Settings to run a pre-review.',
  noKeyLink: 'AI settings',
  noPermission: 'Running a pre-review needs the Editor role or above.',
  inProgress: 'A pre-review of this app is already running for this store.',
  started: 'Pre-review started.',
  empty: 'No pre-review yet. Run one before you send the app to review.',
  disclaimer:
    'This is an AI estimate, not the store decision. Claude cannot run the app: it judges the listing, the declarations and the screenshots, and lists what a reviewer would test in the running app.',
  verdict: {
    likelyApproved: 'Likely approved',
    atRisk: 'At risk',
    likelyRejected: 'Likely rejected',
  },
  likelihood: '{percent}% likely to pass',
  likelihoodLabel: 'Approval likelihood',
  percent: '{percent}%',
  summary: 'Summary',
  findings: 'Findings',
  noFindings: 'No problems found.',
  severity: {
    blocker: 'Blockers',
    major: 'Major',
    minor: 'Minor',
  },
  area: {
    metadata: 'Metadata',
    screenshots: 'Screenshots',
    privacy: 'Privacy',
    ageRating: 'Age rating',
    payments: 'Payments',
    functionality: 'Functionality as described',
    legal: 'Legal',
    other: 'Other',
  },
  guideline: 'Guideline',
  evidence: 'Evidence',
  fix: 'Suggested fix',
  runtime: 'What a reviewer will test at runtime',
  runtimeHint: 'Check these in the build before you submit.',
  positive: 'What looks good',
  usage: '{input} input and {output} output tokens, about ${cost}',
  images: '{count} screenshots reviewed',
  failed: 'The pre-review failed.',
  history: 'History',
  historyEmpty: 'No earlier pre-reviews.',
  historyItem: '{date} by {user}',
  counts: '{blockers} blockers, {major} major, {minor} minor',
  selected: 'Shown',
  version: 'Version {version}',
  status: {
    queued: 'Queued',
    running: 'Running',
    failed: 'Failed',
  },
  submitEntry: 'Run a pre-review before submitting',
  submitLatest: 'Last pre-review: {verdict}, {percent}%',
}

export type PreReviewDictionary = typeof preReviewEn

export const preReviewTr: PreReviewDictionary = {
  title: 'Ön inceleme',
  subtitle:
    'Claude bu gönderimi mağaza incelemecisi gibi inceler: mağaza kaydı, beyanlar ve ekran görüntüleri. Bir tahmindir; gönderimi asla engellemez ya da göndermez.',
  run: 'Ön inceleme yap',
  running: 'İnceleniyor...',
  runningHint: 'Ön inceleme arka planda çalışır ve genelde bir dakikadan kısa sürer. Bu sayfadan ayrılabilirsin.',
  queued: 'Başlaması bekleniyor...',
  model: 'Model: {model}',
  noKeyHint: "Ön inceleme yapmak için Ayarlar'dan bir Anthropic API anahtarı ekle.",
  noKeyLink: 'Yapay zeka ayarları',
  noPermission: 'Ön inceleme için Editor ya da üstü rol gerekir.',
  inProgress: 'Bu uygulamanın bu mağaza için bir ön incelemesi zaten sürüyor.',
  started: 'Ön inceleme başladı.',
  empty: 'Henüz ön inceleme yok. Uygulamayı incelemeye göndermeden önce bir tane yap.',
  disclaimer:
    'Bu bir yapay zeka tahminidir, mağazanın kararı değildir. Claude uygulamayı çalıştıramaz: mağaza kaydını, beyanları ve ekran görüntülerini değerlendirir, bir incelemecinin çalışan uygulamada neyi test edeceğini listeler.',
  verdict: {
    likelyApproved: 'Büyük olasılıkla geçer',
    atRisk: 'Riskli',
    likelyRejected: 'Büyük olasılıkla reddedilir',
  },
  likelihood: '%{percent} geçme olasılığı',
  likelihoodLabel: 'Onay olasılığı',
  percent: '%{percent}',
  summary: 'Özet',
  findings: 'Bulgular',
  noFindings: 'Sorun bulunmadı.',
  severity: {
    blocker: 'Engeller',
    major: 'Önemli',
    minor: 'Küçük',
  },
  area: {
    metadata: 'Meta veriler',
    screenshots: 'Ekran görüntüleri',
    privacy: 'Gizlilik',
    ageRating: 'Yaş derecelendirmesi',
    payments: 'Ödemeler',
    functionality: 'Anlatıldığı gibi çalışma',
    legal: 'Hukuki',
    other: 'Diğer',
  },
  guideline: 'Kural',
  evidence: 'Kanıt',
  fix: 'Önerilen düzeltme',
  runtime: 'Bir incelemecinin çalışırken test edeceği şeyler',
  runtimeHint: 'Göndermeden önce bunları derlemede kontrol et.',
  positive: 'İyi görünenler',
  usage: '{input} girdi ve {output} çıktı tokenı, yaklaşık ${cost}',
  images: '{count} ekran görüntüsü incelendi',
  failed: 'Ön inceleme başarısız oldu.',
  history: 'Geçmiş',
  historyEmpty: 'Daha önce ön inceleme yapılmadı.',
  historyItem: '{date}, {user}',
  counts: '{blockers} engel, {major} önemli, {minor} küçük',
  selected: 'Gösterilen',
  version: 'Sürüm {version}',
  status: {
    queued: 'Sırada',
    running: 'Sürüyor',
    failed: 'Başarısız',
  },
  submitEntry: 'Göndermeden önce ön inceleme yap',
  submitLatest: 'Son ön inceleme: {verdict}, %{percent}',
}

const DICTIONARIES: Record<Lang, PreReviewDictionary> = { en: preReviewEn, tr: preReviewTr }

/** The pre-review texts in the UI language. */
export function usePreReviewText(): PreReviewDictionary {
  return DICTIONARIES[useI18n().lang]
}
