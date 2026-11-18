import { useI18n } from '@/lib/i18n/I18nContext'
import type { Lang } from '@/lib/i18n/lang'

// The rule watcher's dictionary (features namespace); Turkish is typed with the English shape.
export const ruleWatcherEn = {
  title: 'Catalog proposals',
  subtitle: 'When a source changes the watcher opens a proposal; Claude (MCP) may turn it into a catalog patch. No rule changes until you approve.',
  fetch: 'Scan now',
  fetched: '{checked} sources read · {changed} changed · {unreachable} unreachable · {opened} proposals opened.',
  note: 'Note',
  status: {
    pending: 'Pending',
    approved: 'Approved',
    rejected: 'Rejected',
  },
  sourceStatus: {
    new: 'Not read yet',
    unchanged: 'Unchanged',
    changed: 'Changed',
    unreachable: 'Unreachable',
  },
  preparer: {
    watcher: 'Watcher',
    mcp: 'Claude (MCP)',
    mcpPrepared: 'Prepared by Claude (MCP)',
  },
  proposals: {
    proposal: 'Proposal',
    store: 'Store',
    preparer: 'Prepared by',
    version: 'Catalog',
    status: 'Status',
    opened: 'Opened',
    pending: 'Pending',
    all: 'All',
    empty: 'No proposals yet',
    emptyPending: 'Nothing waits for a decision',
    emptyBody: 'Sources are read every day; a change opens a proposal here.',
  },
  sources: {
    title: 'Watched sources',
    source: 'Source',
    lastFetch: 'Last read',
    changedOn: 'changed {date}',
    hash: 'Content hash',
    inactive: 'Not watched',
  },
  detail: {
    source: 'Change in the source',
    openSource: 'Open the source',
    changedParagraph: 'Changed paragraph',
    noExcerpt: 'The watcher kept no excerpt of this change.',
    catalog: 'Catalog change',
    noPatch: 'No catalog patch yet. Prepare it here or let Claude (MCP) prepare it.',
    note: "Preparer's note",
  },
  impact: {
    title: 'Impact preview',
    none: 'No app is affected.',
    summary: '{apps} apps in {organizations} organizations.',
    organization: 'Organization #{id} · {count} apps',
    sample: 'Sample notification: {message}',
  },
  approve: {
    action: 'Approve',
    title: 'Approve proposal #{id}?',
    body: 'The catalog is published as {to} (from {from}). Undoing it needs a new proposal.',
    notify: 'Notify the owners and admins of the affected organizations ({organizations} organizations · {notifications} notifications)',
    confirm: 'Approve and publish',
    done: 'Catalog {version} published · {organizations} organizations, {users} people notified.',
  },
  reject: {
    action: 'Reject',
    title: 'Reject proposal #{id}?',
    body: 'The catalog does not change. The reason shows in the proposal history and the activity log.',
    reason: 'Reason',
    formattingOnly: 'Formatting change only',
    misread: 'The source was misread',
    sourceUnstable: 'The source is not settled yet',
    other: 'Other',
    confirm: 'Reject',
    done: 'The proposal is rejected.',
  },
  prepare: {
    action: 'Prepare',
    title: 'Prepare the catalog patch',
    body: 'Rule ids and the patch the approval publishes.',
    ruleIds: 'Rule ids',
    ruleIdsHint: 'Comma separated, e.g. apple.screenshots.duo',
    patch: 'Catalog patch (JSON)',
    done: 'The proposal is prepared.',
  },
}

export type RuleWatcherDictionary = typeof ruleWatcherEn

export const ruleWatcherTr: RuleWatcherDictionary = {
  title: 'Katalog önerileri',
  subtitle: 'Kaynak değişince izleyici öneri açar; Claude MCP ile kataloğa çevirebilir. Sen onaylamadan hiçbir kural değişmez.',
  fetch: 'Şimdi tara',
  fetched: '{checked} kaynak okundu · {changed} değişti · {unreachable} okunamadı · {opened} öneri açıldı.',
  note: 'Not',
  status: {
    pending: 'Bekliyor',
    approved: 'Onaylandı',
    rejected: 'Reddedildi',
  },
  sourceStatus: {
    new: 'Henüz okunmadı',
    unchanged: 'Aynı',
    changed: 'Değişti',
    unreachable: 'Okunamadı',
  },
  preparer: {
    watcher: 'İzleyici',
    mcp: 'Claude (MCP)',
    mcpPrepared: 'Claude (MCP) hazırladı',
  },
  proposals: {
    proposal: 'Öneri',
    store: 'Mağaza',
    preparer: 'Hazırlayan',
    version: 'Katalog',
    status: 'Durum',
    opened: 'Açıldı',
    pending: 'Bekleyen',
    all: 'Tümü',
    empty: 'Henüz öneri yok',
    emptyPending: 'Karar bekleyen öneri yok',
    emptyBody: 'Kaynaklar her gün okunur; bir değişiklik burada öneri açar.',
  },
  sources: {
    title: 'İzlenen kaynaklar',
    source: 'Kaynak',
    lastFetch: 'Son okuma',
    changedOn: 'değişim {date}',
    hash: "İçerik hash'i",
    inactive: 'İzlenmiyor',
  },
  detail: {
    source: 'Kaynaktaki değişiklik',
    openSource: 'Kaynağı aç',
    changedParagraph: 'Değişen paragraf',
    noExcerpt: 'İzleyici bu değişikliğin alıntısını saklamadı.',
    catalog: 'Katalog değişikliği',
    noPatch: 'Henüz katalog yaması yok. Burada hazırla ya da Claude (MCP) hazırlasın.',
    note: 'Hazırlayanın notu',
  },
  impact: {
    title: 'Etki önizlemesi',
    none: 'Etkilenen uygulama yok.',
    summary: '{organizations} organizasyonda {apps} uygulama.',
    organization: 'Organizasyon #{id} · {count} uygulama',
    sample: 'Örnek bildirim: {message}',
  },
  approve: {
    action: 'Onayla',
    title: 'Öneri #{id} onaylansın mı?',
    body: 'Katalog {from} sürümünden {to} olarak yayınlanır. Geri almak için yeni bir öneri gerekir.',
    notify: 'Etkilenen organizasyonların sahip ve yöneticilerine bildirim gönder ({organizations} organizasyon · {notifications} bildirim)',
    confirm: 'Onayla ve yayınla',
    done: 'Katalog {version} yayınlandı · {organizations} organizasyonda {users} kişiye bildirim gitti.',
  },
  reject: {
    action: 'Reddet',
    title: 'Öneri #{id} reddedilsin mi?',
    body: 'Katalog değişmez. Gerekçe öneri geçmişinde ve aktivite logunda görünür.',
    reason: 'Gerekçe',
    formattingOnly: 'Yalnız biçim değişikliği',
    misread: 'Kaynak yanlış yorumlanmış',
    sourceUnstable: 'Kaynak henüz kararsız',
    other: 'Diğer',
    confirm: 'Reddet',
    done: 'Öneri reddedildi.',
  },
  prepare: {
    action: 'Hazırla',
    title: 'Katalog yamasını hazırla',
    body: "Onayın yayınlayacağı kural kimlikleri ve yama.",
    ruleIds: 'Kural kimlikleri',
    ruleIdsHint: 'Virgülle ayır, ör. apple.screenshots.duo',
    patch: 'Katalog yaması (JSON)',
    done: 'Öneri hazırlandı.',
  },
}

const DICTIONARIES: Record<Lang, RuleWatcherDictionary> = { en: ruleWatcherEn, tr: ruleWatcherTr }

/** The rule watcher's dictionary in the UI language. */
export function useRuleWatcherText(): RuleWatcherDictionary {
  return DICTIONARIES[useI18n().lang]
}
