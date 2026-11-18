import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'

// The MCP setup page (design 16.15, docs/mcp.md); Turkish is typed with the English shape.
export const mcpEn = {
  server: {
    title: 'Server',
    address: 'Address',
    copyAddress: 'Copy the MCP address',
    transport: 'Transport',
    transportValue: 'Streamable HTTP',
    identity: 'Identity',
    identityValue: 'Authorization: Bearer sea_… · personal token',
    token: 'Token in the snippets',
    tokenNone: 'Environment variable $STOREASY_TOKEN',
    tokenOption: '{name} · {prefix}…',
    noToken: 'Create a personal access token first; the snippets below need one.',
    createToken: 'Create token',
    placeholder: 'The copied text carries the token prefix as a placeholder; paste the value yourself.',
  },
  claudeCode: {
    title: 'Claude Code command line',
    copy: 'Copy the Claude Code command',
  },
  json: {
    title: 'JSON configuration · .mcp.json and clients with HTTP support',
    copy: 'Copy the JSON configuration',
    hint: 'Do not write the token into the file; read it from the environment.',
  },
  desktop: {
    title: 'Claude Desktop',
    body: 'Claude Desktop starts local servers from claude_desktop_config.json (macOS: ~/Library/Application Support/Claude/, Windows: %APPDATA%\\Claude\\). Sign in once with the CLI so the file holds no token, then restart Claude Desktop.',
    login: 'Sign in once',
    copyLogin: 'Copy the sign in command',
    config: 'claude_desktop_config.json',
    copyConfig: 'Copy the Claude Desktop configuration',
  },
  bridge: {
    title: 'Local bridge (stdio) · npm storeasy',
    body: 'For tools that take a local file or folder (screenshot folder, AAB, IPA). The bridge uploads the file with a presigned URL and calls the tool with the asset id; it adds artifact_upload. On a self-hosted storeasy, STOREASY_URL is your own address. Needs Node 22 or later.',
    copy: 'Copy the bridge command',
  },
  rules: {
    title: 'Confirmation rules',
    body: 'Every tool calls the same command as the web app: permission, diff, dry run, confirmation and audit log are the same, and the audit log records the channel as MCP.',
    reads: 'Reads (apps, readiness, plans, jobs, rules, compliance) need any role.',
    plans:
      'Writes are plans first: the tool reads the store live and returns the diff, nothing is written. Applying is a separate call; if the store changed meanwhile the apply is refused (409). Plans expire after 48 hours.',
    submit:
      'Review submission and production releases need SubmitForReview or ReleaseProduction (Admin, Owner), confirm: true and the typed confirmation (app name and version), like the web app.',
    readOnly: 'Read only tokens call read tools only (403 otherwise).',
    assisted:
      'Assisted steps return a checklist and links. storeasy never stores or types a store password; the store session stays yours.',
    prompts: "Keep your MCP client's permission prompts on for storeasy's write tools: approving a plan is an action on your store.",
  },
  groups: {
    title: 'Tool groups',
    read: 'Read',
    readTools: 'apps_summary · readiness_check · listings_get · jobs_list · audit_list · compliance_list',
    readTag: 'Read only',
    content: 'Content',
    contentTools: 'listings_plan → listing_apply · screenshots_plan → screenshots_apply · mockup_export',
    release: 'Release',
    releaseTools: 'builds_link · releases_plan → release_apply · rollout_set · testflight_*',
    dryRun: 'Dry run',
    submit: 'Submit for review',
    submitTools: 'submit_for_review · confirm: true required',
    confirm: 'Confirmation',
    feedback: 'Feedback',
    feedbackTools: 'reviews_list · review_reply · rejection_case_get',
    assisted: 'Assisted steps',
    assistedTools: 'assisted_step_get · assisted_step_complete',
    record: 'Record',
    local: 'Local files',
    localTools: 'screenshots_upload_dir · artifact_upload (bridge only)',
  },
  troubleshooting: {
    title: 'Troubleshooting',
    unauthorized: '401: the token expired or was revoked, or the Authorization header is missing.',
    forbidden: '403 OrganizationForbidden: the token is read only or your role lacks the permission.',
    assetNotFound: 'AssetNotFound from a tool: you passed a path to the remote server; use the bridge or pass asset ids.',
    bridge: 'The bridge does not start: Node 22+ on PATH; run the bridge command in a terminal to see the error.',
  },
}

export type McpDictionary = typeof mcpEn

export const mcpTr: McpDictionary = {
  server: {
    title: 'Sunucu',
    address: 'Adres',
    copyAddress: 'MCP adresini kopyala',
    transport: 'Taşıma',
    transportValue: 'Streamable HTTP',
    identity: 'Kimlik',
    identityValue: 'Authorization: Bearer sea_… · kişisel token',
    token: 'Parçacıklardaki token',
    tokenNone: 'Ortam değişkeni $STOREASY_TOKEN',
    tokenOption: '{name} · {prefix}…',
    noToken: "Önce kişisel erişim token'ı oluştur; aşağıdaki parçacıklar bir token ister.",
    createToken: 'Token oluştur',
    placeholder: 'Kopyalanan metinde token önekli yer tutucudur; değeri sen yapıştır.',
  },
  claudeCode: {
    title: 'Claude Code komut satırı',
    copy: 'Claude Code komutunu kopyala',
  },
  json: {
    title: 'JSON yapılandırma · .mcp.json ve HTTP destekli istemciler',
    copy: 'JSON yapılandırmayı kopyala',
    hint: "Token'ı dosyaya yazma; ortam değişkeninden oku.",
  },
  desktop: {
    title: 'Claude Desktop',
    body: "Claude Desktop yerel sunucuları claude_desktop_config.json dosyasından başlatır (macOS: ~/Library/Application Support/Claude/, Windows: %APPDATA%\\Claude\\). Dosyada token kalmasın diye CLI ile bir kez giriş yap, sonra Claude Desktop'ı yeniden başlat.",
    login: 'Bir kez giriş yap',
    copyLogin: 'Giriş komutunu kopyala',
    config: 'claude_desktop_config.json',
    copyConfig: 'Claude Desktop yapılandırmasını kopyala',
  },
  bridge: {
    title: 'Yerel köprü (stdio) · npm storeasy',
    body: "Yerel dosya yolu ve klasör kabul eden araçlar için (ekran görüntüsü klasörü, AAB, IPA). Köprü dosyayı presigned URL ile yükler, aracı varlık kimliğiyle çağırır; artifact_upload aracını ekler. Self-host'ta STOREASY_URL kendi adresin. Node 22 ya da üstü gerekir.",
    copy: 'Köprü komutunu kopyala',
  },
  rules: {
    title: 'Onay kuralları',
    body: "Her araç web'deki komutun aynısını çağırır: yetki, diff, dry run, onay ve denetim kaydı aynıdır; denetim kaydında kanal MCP olarak görünür.",
    reads: 'Okumalar (uygulamalar, hazırlık, planlar, işler, kurallar, uyum) her rolle çalışır.',
    plans:
      'Yazmalar önce plandır: araç mağazayı canlı okur ve diff döner, hiçbir şey yazılmaz. Uygulama ayrı çağrıdır; arada mağaza değiştiyse reddedilir (409). Planların ömrü 48 saattir.',
    submit:
      'İncelemeye gönderme ve production yayını SubmitForReview ya da ReleaseProduction izni (Yönetici, Sahip), confirm: true ve yazılı onay (uygulama adı ve sürüm) ister; web uygulamasındaki gibi.',
    readOnly: 'Salt okunur token yalnız okuma araçlarını çağırır (yoksa 403).',
    assisted:
      'Asistanlı adımlar kontrol listesi ve bağlantılar döner. storeasy mağaza şifresini hiçbir zaman saklamaz ya da yazmaz; mağaza oturumu senindir.',
    prompts: 'MCP istemcinde storeasy yazma araçları için izin sorularını açık tut: bir planı onaylamak mağazanda bir işlemdir.',
  },
  groups: {
    title: 'Araç grupları',
    read: 'Okuma',
    readTools: 'apps_summary · readiness_check · listings_get · jobs_list · audit_list · compliance_list',
    readTag: 'Salt okunur',
    content: 'İçerik',
    contentTools: 'listings_plan → listing_apply · screenshots_plan → screenshots_apply · mockup_export',
    release: 'Yayın',
    releaseTools: 'builds_link · releases_plan → release_apply · rollout_set · testflight_*',
    dryRun: 'Dry run',
    submit: 'İncelemeye gönder',
    submitTools: 'submit_for_review · confirm: true zorunlu',
    confirm: 'Onay',
    feedback: 'Geri bildirim',
    feedbackTools: 'reviews_list · review_reply · rejection_case_get',
    assisted: 'Asistanlı adımlar',
    assistedTools: 'assisted_step_get · assisted_step_complete',
    record: 'Kayıt',
    local: 'Yerel dosya',
    localTools: 'screenshots_upload_dir · artifact_upload (yalnız köprüde)',
  },
  troubleshooting: {
    title: 'Sorun giderme',
    unauthorized: "401: token'ın süresi doldu ya da iptal edildi, veya Authorization başlığı yok.",
    forbidden: '403 OrganizationForbidden: token salt okunur ya da rolünde bu izin yok.',
    assetNotFound: 'Araçtan AssetNotFound: uzak sunucuya dosya yolu verdin; köprüyü kullan ya da varlık kimliği ver.',
    bridge: "Köprü başlamıyor: PATH'te Node 22+ olmalı; hatayı görmek için köprü komutunu terminalde çalıştır.",
  },
}

const DICTIONARIES: Record<Lang, McpDictionary> = { en: mcpEn, tr: mcpTr }

export function useMcpText(): McpDictionary {
  return DICTIONARIES[useI18n().lang]
}
