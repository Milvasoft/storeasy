// storeasy tasarım dosyalarını üretir ve denetler.
// Kullanım (tasarım klasöründen):  node _kaynak/build.mjs            -> bütün sayfalar
//                                   node _kaynak/build.mjs 07 12      -> yalnız bu numaralı sayfalar
// Kaynak: _kaynak/sayfalar/NN-Ad.html (gövde parçası). Çıktı: NN-Ad.html (tek başına açılan dosya).
// Parçanın başındaki yorum: <!-- title: ... | desc: ... | kicker: ... --> (her alan ayrı satırda "ad: değer").
import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const SRC = dirname(fileURLToPath(import.meta.url));
const OUT = join(SRC, '..');
const PAGES = join(SRC, 'sayfalar');
const css = readFileSync(join(SRC, 'kit.css'), 'utf8');
const sprite = readFileSync(join(SRC, 'sprite.svg'), 'utf8');
const js = readFileSync(join(SRC, 'kit.js'), 'utf8');
const all = readdirSync(PAGES).filter((f) => /^\d\d-.+\.html$/.test(f)).sort();
const only = process.argv.slice(2);
const TOC = '04-Icindekiler.html';

const FONTS = '<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>' +
  '<link href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,500;12..96,700;12..96,800&amp;family=Onest:wght@400;500;600;700&amp;family=JetBrains+Mono:wght@400;500;600&amp;display=swap" rel="stylesheet">';
const PRE = "<script>(function(){var r=document.documentElement;try{var s=JSON.parse(localStorage.getItem('storeasy.design')||'{}');if(s.theme)r.setAttribute('data-theme',s.theme);if(s.view)r.setAttribute('data-view',s.view);}catch(e){}})();</script>";

function meta(src) {
  const m = /^\s*<!--([\s\S]*?)-->/.exec(src);
  const o = {};
  if (m) { for (const line of m[1].split('\n')) { const x = /^\s*(\w+)\s*:\s*(.+?)\s*$/.exec(line); if (x) { o[x[1]] = x[2]; } } }
  return { o, body: m ? src.slice(m[0].length) : src };
}
const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');

function topbar(file, title, i) {
  const prev = all[i - 1], next = all[i + 1];
  const seg = (k, opts) => '<div class="dseg" role="group">' + opts.map(([v, l]) => `<button type="button" data-set="${k}" data-v="${v}" aria-pressed="false">${l}</button>`).join('') + '</div>';
  return '<header class="doc-top">' +
    `<a class="home" href="${TOC}"><svg class="mk" aria-hidden="true"><use href="#logo"/></svg><span class="wm" style="font-size:17px">stor<i>e</i>asy</span></a>` +
    `<span class="ttl">${esc(title)}</span><span class="sp"></span>` +
    '<nav class="pn">' + (prev ? `<a href="${prev}" title="Önceki dosya">${prev.slice(0, 2)}</a>` : '') + `<a href="${TOC}">İçindekiler</a>` + (next ? `<a href="${next}" title="Sonraki dosya">${next.slice(0, 2)}</a>` : '') + '</nav>' +
    seg('view', [['desktop', 'Masaüstü'], ['phone', 'Telefon']]) +
    seg('theme', [['light', 'Açık'], ['dark', 'Koyu']]) +
    seg('motion', [['full', 'Hareket'], ['reduce', 'Azaltılmış']]) +
    '</header>';
}

// ---------- denetimler ----------
const VOID = new Set(['area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'source', 'track', 'wbr']);
function balance(html) {
  const errs = [];
  const s = html.replace(/<!--[\s\S]*?-->/g, '').replace(/<(script|style)\b[^>]*>[\s\S]*?<\/\1>/gi, '<$1></$1>');
  const re = /<\/?([a-zA-Z][\w:-]*)\b[^>]*?(\/?)>/g;
  const stack = [];
  let m;
  while ((m = re.exec(s))) {
    const tag = m[1].toLowerCase(), close = m[0][1] === '/', self = m[2] === '/' || m[0].endsWith('/>');
    if (VOID.has(tag)) { continue; }
    if (close) {
      if (!stack.length) { errs.push(`fazla kapanış </${tag}>`); continue; }
      const top = stack[stack.length - 1];
      if (top.tag === tag) { stack.pop(); continue; }
      const idx = stack.map((x) => x.tag).lastIndexOf(tag);
      if (idx >= 0) { errs.push(`kapanmamış <${stack[stack.length - 1].tag}> (satır ~${lineAt(s, stack[stack.length - 1].pos)}), </${tag}> ile kapandı`); stack.length = idx; } else { errs.push(`eşsiz </${tag}> (satır ~${lineAt(s, m.index)})`); }
    } else if (!self) { stack.push({ tag, pos: m.index }); }
  }
  for (const x of stack) { errs.push(`kapanmamış <${x.tag}> (satır ~${lineAt(s, x.pos)})`); }
  return errs;
}
function lineAt(s, pos) { return s.slice(0, pos).split('\n').length; }

let failed = 0;
const report = [];
all.forEach((file, i) => {
  if (only.length && !only.some((n) => file.startsWith(n))) { return; }
  const raw = readFileSync(join(PAGES, file), 'utf8');
  const { o, body } = meta(raw);
  const title = o.title || file.replace(/^\d\d-|\.html$/g, '').replace(/-/g, ' ');
  // dosyaya özel <style> blokları head'e taşınır
  const styles = [];
  const bodyNoStyle = body.replace(/<style\b[^>]*>([\s\S]*?)<\/style>/gi, (_, c) => { styles.push(c); return ''; });
  const hero = o.nohero ? '' : `<section class="doc-hero"><span class="k">${esc(o.kicker || file.slice(0, 2))}</span><h1>${esc(title)}</h1>${o.desc ? `<p>${esc(o.desc)}</p>` : ''}<nav class="doc-toc" aria-label="Bölümler"></nav></section>`;
  const html = '<!doctype html>\n<html lang="tr" data-theme="dark" data-view="desktop">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1">\n' +
    `<title>storeasy · ${esc(title)}</title>\n` + (o.desc ? `<meta name="description" content="${esc(o.desc)}">\n` : '') + PRE + '\n' + FONTS +
    '\n<style>\n' + css + '\n</style>\n' + (styles.length ? '<style>\n' + styles.join('\n') + '\n</style>\n' : '') +
    '</head>\n<body>\n' + sprite + '\n' + topbar(file, title, i) + '\n<main class="doc">\n' + hero + '\n' + bodyNoStyle.trim() + '\n</main>\n<script>\n' + js + '\n</script>\n</body>\n</html>\n';

  const errs = balance(html);
  const dashes = (html.match(/[–—]/g) || []).length;
  if (dashes) { errs.push(`uzun tire / en dash: ${dashes}`); }
  if (/\{\{|\}\}/.test(bodyNoStyle)) { errs.push('{{ }} bulundu'); }
  const ids = new Set([...html.matchAll(/\sid="([^"]+)"/g)].map((x) => x[1]));
  const noScript = html.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, '');
  const refs = new Set([...noScript.matchAll(/<use\b[^>]*href="#([^"]+)"/g)].map((x) => x[1]));
  const missing = [...refs].filter((r) => !ids.has(r));
  if (missing.length) { errs.push('tanımsız sembol: ' + missing.join(', ')); }
  for (const m of html.matchAll(/<script>([\s\S]*?)<\/script>/g)) { try { new vm.Script(m[1]); } catch (e) { errs.push('script hatası: ' + e.message); } }
  const frames = (html.match(/class="fr[" ]/g) || []).length;
  writeFileSync(join(OUT, file), html);
  report.push(`${errs.length ? 'HATA' : 'tamam'}  ${file}  (${frames} çerçeve, ${Math.round(html.length / 1024)} KB)` + (errs.length ? '\n   - ' + errs.slice(0, 25).join('\n   - ') : ''));
  if (errs.length) { failed++; }
});
console.log(report.join('\n'));
process.exit(failed ? 1 : 0);
