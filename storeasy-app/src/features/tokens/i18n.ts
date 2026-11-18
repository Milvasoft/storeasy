import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'

// The developer section's token screens (design 16.11-16.14) and the shared "shown once" dialog; Turkish is typed with
// the English shape.
export const tokensEn = {
  title: 'MCP and tokens',
  subtitle:
    'Claude and other MCP clients work on your behalf with these tokens; what they do shows in the audit log with your name and the token prefix.',
  create: 'Create token',
  tabs: {
    label: 'Developer sections',
    tokens: 'Tokens',
    setup: 'Setup',
  },
  list: {
    caption: 'Personal access tokens',
    name: 'Name',
    organization: 'Organization',
    access: 'Access',
    readOnly: 'Read only',
    readWrite: 'Read and write',
    prefix: 'Prefix',
    expiry: 'Expires',
    expired: 'Expired · {date}',
    lastUse: 'Last used',
    never: 'Never',
    created: 'Created',
    actions: 'Actions',
    revoke: 'Revoke',
    revokeOf: 'Revoke {name}',
    renew: 'Renew',
    renewOf: 'Create a new token like {name}',
    footer: 'Token values are not stored; if you lost one, revoke it and create a new one.',
    emptyTitle: 'No token yet',
    emptyBody:
      'Create a personal token bound to an organization to connect Claude to storeasy. Starting read only is a good idea: it reads summaries and readiness reports and changes nothing.',
  },
  form: {
    title: 'New token',
    body: 'Name it after where you will use it.',
    name: 'Name',
    organization: 'Organization',
    organizationOption: '{name} · your role {role}',
    readOnly: 'Read only',
    readOnlyHint: 'Off, the token carries every write permission of your role: content, releases, review submission.',
    viewerLocked: 'With the Viewer role a token can only read. Ask an admin to make you an Editor to write.',
    expiry: 'Valid for',
    days: '{count} days',
    endsOn: 'Ends on {date}. You get a notification 7 days before.',
    limit:
      'The token never exceeds your role in this organization. If your role drops, the token drops at once; if your membership ends, the token stops working. Every write through MCP still asks for a dry run and a confirmation.',
    submit: 'Create token',
    submitReadOnly: 'Create read only token',
    failed: 'The token could not be created',
  },
  created: {
    title: '"{name}" is ready',
    meta: '{organization} · {access} · until {date}',
    warning:
      'You will not see this value again. Copy it now into your password manager or the client configuration. Do not share it or commit it.',
    value: 'Token',
    claudeCode: 'Add to Claude Code',
    copyCommand: 'Copy the command',
    otherClients: 'Setup for other clients',
    ack: 'I copied the token to a safe place',
  },
  revoke: {
    title: 'Revoke "{name}"?',
    body: 'The token with prefix {prefix} stops working at once; a Claude session that uses it fails on its next tool call. This cannot be undone.',
    lastUse: 'Last used {when}.',
    confirm: 'Revoke',
    done: 'The token is revoked.',
  },
  once: {
    copied: 'Copied',
  },
}

export type TokensDictionary = typeof tokensEn

export const tokensTr: TokensDictionary = {
  title: "MCP ve token'lar",
  subtitle:
    "Claude ve başka MCP istemcileri bu token'larla senin adına çalışır; yaptıkları denetim kaydında senin adınla ve token önekiyle görünür.",
  create: 'Token oluştur',
  tabs: {
    label: 'Geliştirici bölümleri',
    tokens: "Token'lar",
    setup: 'Kurulum',
  },
  list: {
    caption: "Kişisel erişim token'ları",
    name: 'Ad',
    organization: 'Organizasyon',
    access: 'Erişim',
    readOnly: 'Salt okunur',
    readWrite: 'Okuma ve yazma',
    prefix: 'Önek',
    expiry: 'Bitiş',
    expired: 'Süresi doldu · {date}',
    lastUse: 'Son kullanım',
    never: 'Hiç',
    created: 'Oluşturuldu',
    actions: 'İşlemler',
    revoke: 'İptal et',
    revokeOf: '{name} iptal et',
    renew: 'Yenile',
    renewOf: '{name} gibi yeni token oluştur',
    footer: 'Token değerleri saklanmaz; kaybettiysen iptal edip yenisini oluştur.',
    emptyTitle: 'Henüz token yok',
    emptyBody:
      "Claude'u storeasy'ye bağlamak için bir organizasyona bağlı kişisel token oluştur. Salt okunur başlamak iyi bir fikir: özetleri ve hazırlık raporlarını okur, hiçbir şeyi değiştirmez.",
  },
  form: {
    title: 'Yeni token',
    body: "Token'ı nerede kullanacağını adından anlaşılır yaz.",
    name: 'Ad',
    organization: 'Organizasyon',
    organizationOption: '{name} · rolün {role}',
    readOnly: 'Salt okunur',
    readOnlyHint: 'Kapalıyken token rolünün bütün yazma izinlerini taşır: içerik, yayın, incelemeye gönderme.',
    viewerLocked: 'İzleyici rolünde token yalnız okuyabilir. Yazma için bir Yöneticiden rolünü Editör yapmasını iste.',
    expiry: 'Geçerlilik',
    days: '{count} gün',
    endsOn: '{date} tarihinde biter. Bitişten 7 gün önce bildirim gelir.',
    limit:
      "Token'ın yetkisi bu organizasyondaki rolünü hiçbir zaman aşamaz. Rolün düşerse token da hemen düşer; üyeliğin biterse token çalışmaz. MCP'de her yazma yine dry run ve onay ister.",
    submit: 'Token oluştur',
    submitReadOnly: 'Salt okunur token oluştur',
    failed: 'Token oluşturulamadı',
  },
  created: {
    title: '"{name}" hazır',
    meta: '{organization} · {access} · {date} tarihine kadar',
    warning:
      'Bu değeri bir daha göremezsin. Şimdi kopyala ve şifre yöneticine ya da istemcinin yapılandırmasına koy. Kimseyle paylaşma, repoya koyma.',
    value: 'Token',
    claudeCode: "Claude Code'a ekle",
    copyCommand: 'Komutu kopyala',
    otherClients: 'Diğer istemciler için kurulum',
    ack: "Token'ı güvenli bir yere kopyaladım",
  },
  revoke: {
    title: '"{name}" iptal edilsin mi?',
    body: "{prefix} önekli token hemen çalışmaz; bu token'la bağlı Claude oturumu bir sonraki araç çağrısında hata alır. Geri alınamaz.",
    lastUse: 'Son kullanım {when}.',
    confirm: 'İptal et',
    done: 'Token iptal edildi.',
  },
  once: {
    copied: 'Kopyalandı',
  },
}

const DICTIONARIES: Record<Lang, TokensDictionary> = { en: tokensEn, tr: tokensTr }

/** The token screens' dictionary in the UI language. */
export function useTokensText(): TokensDictionary {
  return DICTIONARIES[useI18n().lang]
}
