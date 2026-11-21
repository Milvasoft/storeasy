import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'
import type { TourStepId } from './steps'

interface StepText {
  title: string
  body: string
}

// The guided tour's dictionary (features namespace): the first visit invitation, the step card and every step's text.
export const tourEn = {
  label: 'Guided tour',
  invite: {
    eyebrow: 'Welcome to storeasy',
    title: 'Shall we get to know storeasy in 2 minutes?',
    body: 'A short tour shows what each section is for, so afterwards you find everything on the first try.',
    gains: ['Three stores, one screen', 'Nothing reaches a store without approval', 'Every job, live'],
    meta: 'About 2 minutes · {count} steps',
    start: 'Start the tour',
    later: 'Not now',
    laterHint: 'You can start it any time from Preferences on your Account page.',
  },
  card: {
    counter: '{current} / {total}',
    back: 'Back',
    next: 'Next',
    finish: 'Finish',
    exit: 'End the tour',
    keys: 'Use ← → to move, Esc to leave',
  },
  settings: {
    title: 'Guided tour',
    body: 'A short walk through the sections of storeasy, step by step.',
    start: 'Start the tour',
  },
  steps: {
    welcome: {
      title: 'A quick tour',
      body: 'storeasy runs your App Store, Google Play and Microsoft Store work from one place. Here are the sections, one by one.',
    },
    nav: {
      title: 'The menu',
      body: 'Every section lives here. Your role decides which ones you see.',
    },
    apps: {
      title: 'Apps',
      body: 'Each app is one card with its App Store, Google Play and Microsoft Store records under it. Add a store connection and your apps come in on their own.',
    },
    topbar: {
      title: 'Top bar',
      body: 'Running jobs, the theme and the bell. Rejections, finished jobs and deadlines land in the bell.',
    },
    app: {
      title: 'One app',
      body: 'The store records of the app. The same app in another store is linked from here, so it stays one app.',
    },
    appTabs: {
      title: 'App sections',
      body: 'Readiness, texts, media, releases, money and more. Each tab gathers one job, and an edit turns into a change plan.',
    },
    mute: {
      title: 'Mute alerts',
      body: 'Using an app only through TestFlight? Mute it. The app stays everywhere; only its alerts go quiet.',
    },
    plans: {
      title: 'Change plans and approval',
      body: 'Every change bound for a store becomes a plan first, shown as a diff. Nothing is written to a store without approval.',
    },
    live: {
      title: 'Live activity',
      body: 'What goes to the stores and what comes back, as it happens: jobs, version states, items that need you and upcoming dates.',
    },
    reviews: {
      title: 'Reviews',
      body: 'Reviews from every store in one list. AI drafts a reply; you read it, edit it and send it.',
    },
    rejections: {
      title: 'Rejections',
      body: 'When a store rejects a version, a case opens here with the reason, a checklist and a reply draft.',
    },
    compliance: {
      title: 'Compliance',
      body: "Store rule deadlines on one timeline. Each app's catalog profile decides which rules apply to it.",
    },
    intake: {
      title: 'Build intake',
      body: 'Builds from your CI or your own uploads arrive here. Delivery rules decide which build goes to which store, and when.',
    },
    members: {
      title: 'Members and roles',
      body: 'Invite your team and give everyone a role. The role decides who can see, edit and approve what.',
    },
    audit: {
      title: 'Audit log',
      body: 'Who changed what, and when. Every action of the organization is kept here.',
    },
    developer: {
      title: 'MCP and tokens',
      body: 'Create a personal access token and connect storeasy to an AI assistant over MCP. Changes the assistant proposes arrive as plans too.',
    },
    restart: {
      title: 'Take the tour again',
      body: 'This tour is always here. Start it again whenever you like.',
    },
    done: {
      title: 'You are all set',
      body: 'First step: connect a store under Connections and your apps come in. Enjoy!',
    },
  } satisfies Record<TourStepId, StepText>,
}

export type TourDictionary = typeof tourEn

export const tourTr: TourDictionary = {
  label: 'Tanıtım turu',
  invite: {
    eyebrow: "storeasy'ye hoş geldin",
    title: "storeasy'yi 2 dakikada tanıyalım mı?",
    body: 'Kısa bir turda her bölümün ne işe yaradığını görürsün; sonra aradığını ilk seferde bulursun.',
    gains: ['Üç mağaza, tek ekran', 'Onaysız hiçbir şey mağazaya gitmez', 'Her iş canlı önünde'],
    meta: 'Yaklaşık 2 dakika · {count} adım',
    start: 'Turu başlat',
    later: 'Şimdi değil',
    laterHint: 'Turu istediğin zaman Hesap sayfasındaki Tercihler bölümünden başlatabilirsin.',
  },
  card: {
    counter: '{current} / {total}',
    back: 'Geri',
    next: 'İleri',
    finish: 'Bitir',
    exit: 'Turu bitir',
    keys: '← → ile gez, Esc ile çık',
  },
  settings: {
    title: 'Tanıtım turu',
    body: "storeasy'nin bölümlerini adım adım gösteren kısa bir tur.",
    start: 'Turu başlat',
  },
  steps: {
    welcome: {
      title: 'Kısa bir tur',
      body: 'storeasy ile App Store, Google Play ve Microsoft Store işlerini tek yerden yürütürsün. Bölümleri sırayla gösteriyorum.',
    },
    nav: {
      title: 'Menü',
      body: 'Bütün bölümler burada. Hangilerini göreceğini rolün belirler.',
    },
    apps: {
      title: 'Uygulamalar',
      body: 'Her uygulama tek kart; App Store, Google Play ve Microsoft Store kayıtları altında toplanır. Mağaza bağlantısı ekleyince uygulamaların kendiliğinden gelir.',
    },
    topbar: {
      title: 'Üst çubuk',
      body: 'Çalışan işler, tema ve bildirim zili burada. Retler, biten işler ve yaklaşan tarihler zile düşer.',
    },
    app: {
      title: 'Bir uygulama',
      body: 'Uygulamanın mağaza kayıtları burada. Aynı uygulamanın başka mağazadaki kaydını da buradan bağlarsın; uygulama tek kalır.',
    },
    appTabs: {
      title: 'Uygulamanın bölümleri',
      body: 'Hazırlık, metinler, görseller, sürümler, gelirler ve fazlası. Her sekme bir işi toplar; yaptığın düzenleme bir değişiklik planına dönüşür.',
    },
    mute: {
      title: 'Uyarıları sessize al',
      body: 'Bir uygulamayı yalnız TestFlight için mi kullanıyorsun? Sessize al. Uygulama her yerde kalır, yalnız uyarıları susar.',
    },
    plans: {
      title: 'Değişiklik planları ve onay',
      body: 'Mağazaya gidecek her değişiklik önce plan olur ve farkıyla gösterilir. Onay verilmeden mağazaya hiçbir şey yazılmaz.',
    },
    live: {
      title: 'Canlı akış',
      body: 'Mağazalara giden ve oradan gelen her şey anında akar: işler, sürüm durumları, seni bekleyenler ve yaklaşan tarihler.',
    },
    reviews: {
      title: 'Yorumlar',
      body: 'Bütün mağazaların yorumları tek listede. Yapay zekâ bir cevap taslağı yazar; sen okur, düzenler ve gönderirsin.',
    },
    rejections: {
      title: 'Retler',
      body: 'Bir mağaza sürümü reddederse burada bir kayıt açılır: gerekçe, yapılacaklar listesi ve cevap taslağı bir arada.',
    },
    compliance: {
      title: 'Uyum',
      body: 'Mağaza kurallarının son tarihleri tek bir zaman çizelgesinde. Hangi kuralın uygulamanı ilgilendirdiğini katalog profili belirler.',
    },
    intake: {
      title: 'Paket alma',
      body: "CI'dan gelen ya da elle yüklediğin derlemeler buraya düşer. Hangisinin hangi mağazaya ne zaman gideceğini teslim kuralları belirler.",
    },
    members: {
      title: 'Üyeler ve roller',
      body: 'Ekibini davet et, herkese bir rol ver. Kimin neyi görüp düzenleyebileceğini ve onaylayabileceğini rol belirler.',
    },
    audit: {
      title: 'Denetim kaydı',
      body: 'Kim neyi ne zaman değiştirdi; organizasyondaki her işlem burada kayıtlı.',
    },
    developer: {
      title: 'MCP ve tokenlar',
      body: "Kişisel erişim tokenı oluştur, storeasy'yi MCP ile bir yapay zekâ asistanına bağla. Asistanın önerdiği değişiklikler de plan olarak gelir.",
    },
    restart: {
      title: 'Turu yeniden başlat',
      body: 'Bu tur hep burada. İstediğin zaman yeniden başlatabilirsin.',
    },
    done: {
      title: 'Hazırsın',
      body: "İlk adım: Bağlantılar'dan bir mağaza bağla, uygulamaların gelsin. Kolay gelsin!",
    },
  },
}

const DICTIONARIES: Record<Lang, TourDictionary> = { en: tourEn, tr: tourTr }

/** The guided tour's dictionary in the UI language. */
export function useTourText(): TourDictionary {
  return DICTIONARIES[useI18n().lang]
}
