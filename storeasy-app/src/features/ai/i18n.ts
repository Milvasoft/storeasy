import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'

// The AI settings card and the review page's "write with AI" button. English is the source shape, Turkish is typed with
// it, so a missing or extra key fails the build.
export const aiEn = {
  settings: {
    title: 'AI',
    intro:
      'Reply drafts for store reviews are written with Claude using your own Anthropic API key. A draft is only a suggestion: nothing is sent to a store until someone sends it.',
    model: 'Model: {model}',
    keyLabel: 'Anthropic API key',
    keyHint: 'Create a key in the Anthropic Console. It is stored encrypted and never shown again; only its last 4 characters are.',
    keyPlaceholder: 'sk-ant-...',
    keyInvalid: 'An Anthropic API key starts with sk-ant- and has no spaces.',
    keySet: 'Key ending in {lastFour}',
    keySetBy: 'Saved {date} by {user}',
    keySetDate: 'Saved {date}',
    noKey: 'No key yet. Without one, "Write with AI" stays off on the reviews page.',
    save: 'Save key',
    replace: 'Replace key',
    cancel: 'Cancel',
    remove: 'Remove key',
    removeTitle: 'Remove the Anthropic API key?',
    removeBody: 'Writing reply drafts with AI stops until a key is saved again. Drafts already written stay.',
    saved: 'Key saved and checked with Anthropic.',
    savedUnverified: 'Key saved, but Anthropic could not be reached to check it. It is checked again on first use.',
    removed: 'Key removed.',
    readOnly: 'Only members who manage store connections can change the key.',
    cost: 'Each draft is one short call billed to your Anthropic account.',
  },
  models: {
    title: 'Models',
    intro:
      'Choose the Claude model of each feature. Prices are approximate, in US dollars per million tokens, billed to your Anthropic account.',
    replyDrafts: 'Review reply drafts',
    preReview: 'Pre-review',
    price: '${input} / ${output} per million tokens (input / output)',
    save: 'Save models',
    saved: 'Models saved.',
    needsKey: 'Save a key first to choose the models.',
    readOnly: 'Reply drafts: {replyDrafts}. Pre-review: {preReview}.',
  },
  reply: {
    generate: 'Write with AI',
    generating: 'Writing...',
    generated: 'Draft written with AI. Read and edit it before you send.',
    shortened: 'The draft was shortened to fit the store limit.',
    replaceTitle: 'Replace the current text?',
    replaceBody: 'The text in the reply box is replaced by the AI draft.',
    replaceConfirm: 'Replace',
    noKeyHint: 'Add an Anthropic API key in Settings to write drafts with AI.',
    noKeyLink: 'AI settings',
  },
}

export type AiDictionary = typeof aiEn

export const aiTr: AiDictionary = {
  settings: {
    title: 'Yapay zeka',
    intro:
      'Mağaza yorumlarına yanıt taslakları, kendi Anthropic API anahtarınla Claude tarafından yazılır. Taslak yalnızca bir öneridir: biri göndermeden hiçbir şey mağazaya gitmez.',
    model: 'Model: {model}',
    keyLabel: 'Anthropic API anahtarı',
    keyHint: "Anahtarı Anthropic Console'da oluştur. Şifreli saklanır ve bir daha gösterilmez; yalnızca son 4 karakteri görünür.",
    keyPlaceholder: 'sk-ant-...',
    keyInvalid: 'Anthropic API anahtarı sk-ant- ile başlar ve boşluk içermez.',
    keySet: 'Sonu {lastFour} ile biten anahtar',
    keySetBy: '{date} tarihinde {user} kaydetti',
    keySetDate: '{date} tarihinde kaydedildi',
    noKey: 'Henüz anahtar yok. Anahtar olmadan yorumlar sayfasındaki "Yapay zeka ile yaz" kapalı kalır.',
    save: 'Anahtarı kaydet',
    replace: 'Anahtarı değiştir',
    cancel: 'Vazgeç',
    remove: 'Anahtarı kaldır',
    removeTitle: 'Anthropic API anahtarı kaldırılsın mı?',
    removeBody: 'Yeni bir anahtar kaydedilene kadar yapay zeka ile yanıt taslağı yazılamaz. Yazılmış taslaklar kalır.',
    saved: 'Anahtar kaydedildi ve Anthropic ile doğrulandı.',
    savedUnverified: "Anahtar kaydedildi ama doğrulamak için Anthropic'e ulaşılamadı. İlk kullanımda yeniden denenir.",
    removed: 'Anahtar kaldırıldı.',
    readOnly: 'Anahtarı yalnızca mağaza bağlantılarını yöneten üyeler değiştirebilir.',
    cost: 'Her taslak, Anthropic hesabına faturalanan kısa bir çağrıdır.',
  },
  models: {
    title: 'Modeller',
    intro: 'Her özelliğin Claude modelini seç. Fiyatlar yaklaşıktır, milyon token başına ABD doları olarak Anthropic hesabına faturalanır.',
    replyDrafts: 'Yorum yanıtı taslakları',
    preReview: 'Ön inceleme',
    price: 'Milyon token başına ${input} / ${output} (girdi / çıktı)',
    save: 'Modelleri kaydet',
    saved: 'Modeller kaydedildi.',
    needsKey: 'Modelleri seçmek için önce bir anahtar kaydet.',
    readOnly: 'Yanıt taslakları: {replyDrafts}. Ön inceleme: {preReview}.',
  },
  reply: {
    generate: 'Yapay zeka ile yaz',
    generating: 'Yazılıyor...',
    generated: 'Taslak yapay zeka ile yazıldı. Göndermeden önce oku ve düzenle.',
    shortened: 'Taslak mağaza sınırına sığması için kısaltıldı.',
    replaceTitle: 'Mevcut metin değiştirilsin mi?',
    replaceBody: 'Yanıt kutusundaki metnin yerine yapay zeka taslağı gelir.',
    replaceConfirm: 'Değiştir',
    noKeyHint: "Yapay zeka ile taslak yazmak için Ayarlar'dan bir Anthropic API anahtarı ekle.",
    noKeyLink: 'Yapay zeka ayarları',
  },
}

const DICTIONARIES: Record<Lang, AiDictionary> = { en: aiEn, tr: aiTr }

/** The AI texts in the UI language. */
export function useAiText(): AiDictionary {
  return DICTIONARIES[useI18n().lang]
}
