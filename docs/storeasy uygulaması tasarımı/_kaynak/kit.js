/* storeasy tasarım runtime'ı: tema, görünüm (masaüstü / telefon), hareket azaltma, çerçeve ölçekleme,
   uygulama kabuğu (kenar çubuğu + üst bar) ve bölüm içindekileri. build.mjs her dosyaya gömer. */
(function () {
  var root = document.documentElement, KEY = 'storeasy.design';
  function load() { try { return JSON.parse(localStorage.getItem(KEY) || '{}') || {}; } catch (e) { return {}; } }
  function save(k, v) { try { var s = load(); s[k] = v; localStorage.setItem(KEY, JSON.stringify(s)); } catch (e) {} }

  /* ---------- Uygulama kabuğu ---------- */
  var ICON = function (id, cls) { return '<svg class="ic' + (cls ? ' ' + cls : '') + '" aria-hidden="true"><use href="#' + id + '"/></svg>'; };
  var NAV = [
    { g: null, items: [
      ['apps', '/apps', 'i-grid', 'Uygulamalar', '6'],
      ['activity', '/activity', 'i-activity', 'Canlı akış', '2', 'live'],
      ['reviews', '/reviews', 'i-msg', 'Yorumlar', '14'],
      ['compliance', '/compliance', 'i-cal-clock', 'Uyum takvimi', '3', 'dng'],
      ['studio', '/studio', 'i-images', 'Stüdyo']
    ] },
    { g: 'Teslim', items: [
      ['intake', '/intake', 'i-cloud-up', 'Paket alma'],
      ['connections', '/connections', 'i-plug', 'Mağaza bağlantıları'],
      ['webhooks', '/webhooks', 'i-webhook', 'Webhook\'lar']
    ] },
    { g: 'Organizasyon', items: [
      ['members', '/members', 'i-users', 'Üyeler'],
      ['audit', '/audit', 'i-list', 'Denetim kaydı'],
      ['rules', '/rules', 'i-book', 'Mağaza kuralları'],
      ['settings', '/settings', 'i-settings', 'Ayarlar']
    ] },
    { g: 'Geliştirici', items: [
      ['mcp', '/developer', 'i-terminal', 'MCP ve token\'lar']
    ] }
  ];
  var ADMIN = { g: 'Platform', items: [
    ['a-rules', '/admin/rules', 'i-radar', 'Katalog önerileri', '2'],
    ['a-users', '/admin/users', 'i-user', 'Kullanıcılar'],
    ['a-roles', '/admin/roles', 'i-shield', 'Roller'],
    ['a-logs', '/admin/activity-logs', 'i-history', 'Aktivite logu'],
    ['a-langs', '/admin/languages', 'i-lang', 'Diller'],
    ['a-flags', '/admin/feature-flags', 'i-flag', 'Özellik anahtarları'],
    ['a-health', '/admin/system', 'i-server', 'Sistem durumu']
  ] };
  var ROLE_HIDE = { Viewer: ['connections', 'webhooks'], Editor: ['connections', 'webhooks'] };

  function buildShell(sh) {
    var d = sh.dataset, on = d.nav || '', role = d.role || 'Owner';
    var hide = (ROLE_HIDE[role] || []);
    if (d.hide) { hide = hide.concat(d.hide.split(',')); }
    var groups = NAV.slice(); if (d.admin !== undefined) { groups.push(ADMIN); }
    var nav = groups.map(function (g) {
      var items = g.items.filter(function (it) { return hide.indexOf(it[0]) < 0; }).map(function (it) {
        var c = it[4] ? '<span class="cnt' + (it[5] ? ' ' + it[5] : '') + '">' + (it[5] === 'live' ? '&#9679; ' : '') + it[4] + '</span>' : '';
        return '<a class="nav-i' + (it[0] === on ? ' on' : '') + '" href="#" title="' + it[1] + '">' + ICON(it[2]) + '<span>' + it[3] + '</span>' + c + '</a>';
      }).join('');
      return (g.g ? '<div class="nav-g">' + g.g + '</div>' : '') + items;
    }).join('');
    var act = d.act === undefined ? '<a class="btn pri blk act" href="#">' + ICON('i-plus') + 'Uygulama ekle</a>' :
      (d.act ? '<a class="btn pri blk act" href="#">' + ICON(d.actIcon || 'i-plus') + d.act + '</a>' : '');
    var org = d.org || 'Kuzey Lab', orgInit = d.orgInit || 'K';
    var roleTr = { Owner: 'Sahip', Admin: 'Yönetici', Editor: 'Editör', Viewer: 'İzleyici' }[role] || role;
    var aside = '<aside class="side" aria-label="Ana menü">' +
      '<a class="brand" href="#"><svg class="mk" aria-hidden="true"><use href="#logo"/></svg><span class="wm">stor<i>e</i>asy</span></a>' +
      '<button class="orgsw" type="button" aria-label="Organizasyon değiştir"><span class="oav">' + orgInit + '</span><span class="f1"><b>' + org + '</b><small>' + roleTr + ' · 6 uygulama</small></span>' + ICON('i-chevs', 's16 fg3') + '</button>' +
      (role === 'Viewer' && d.act === undefined ? '' : act) + '<nav>' + nav + '</nav>' +
      '<div class="side-f"><div class="me"><span class="av">DA</span><span class="f1"><b>Deniz Arslan</b><small>deniz@kuzeylab.dev</small></span>' + ICON('i-more', 's16 fg3') + '</div>' +
      '<div class="lic"><span>AGPL-3.0 · Kaynak kod</span><span>v1.0.0</span></div></div></aside>';
    var crumbs = (d.crumbs || 'Uygulamalar').split('|');
    var cr = crumbs.map(function (c, i) { return i === crumbs.length - 1 ? '<b>' + c + '</b>' : '<span class="pre">' + c + '</span><span class="sep pre">/</span>'; }).join('');
    var live = d.live === undefined ? '' : '<span class="livepill"><span class="dot"></span><span>' + (d.live || '2 iş sürüyor') + '</span></span>';
    var bar = '<header class="bar"><button class="ib ghost menu-btn" type="button" aria-label="Menüyü aç">' + ICON('i-menu') + '</button>' +
      '<div class="crumb"><span class="pre">' + org + '</span><span class="sep pre">/</span>' + cr + '</div>' +
      '<div class="search" role="search">' + ICON('i-search', 's16') + '<span>Uygulama, sürüm, paket ara</span><span class="kbd">Ctrl K</span></div>' + live +
      '<button class="ib" type="button" aria-label="Bildirimler">' + ICON('i-bell') + (d.bell === 'off' ? '' : '<span class="pip"></span>') + '</button>' +
      '<button class="ib ghost" type="button" aria-label="Yardım">' + ICON('i-help') + '</button></header>';
    var main = document.createElement('div'); main.className = 'main';
    main.innerHTML = bar;
    while (sh.firstChild) { main.appendChild(sh.firstChild); }
    sh.innerHTML = aside; sh.appendChild(main);
    if (!main.querySelector('.content')) { var ct = document.createElement('div'); ct.className = 'content'; while (main.children.length > 1) { ct.appendChild(main.children[1]); } main.appendChild(ct); }
  }
  document.querySelectorAll('.shell[data-nav]').forEach(buildShell);

  /* ---------- Uygulama başlığı + sekmeler (uygulama detayı ekranları) ----------
     <div class="apphead" data-app="pusula" data-store="apple" data-tab="kayit" data-sub="Metinler">[eylem düğmeleri]</div> */
  var STORES = { apple: ['AS', 'App Store'], google: ['GP', 'Google Play'], ms: ['MS', 'Microsoft Store'] };
  var APPS = {
    pusula: { n: 'Pusula', c: '#0F766E', s: { apple: ['2.3', 'İncelemede', 'warn'], google: ['2.3.0', 'Production %20', 'info'], ms: ['2.3.0.0', 'Sertifikasyonda', 'info'] } },
    defter: { n: 'Defter', c: '#B45309', s: { apple: ['1.4', 'Reddedildi', 'dng'], google: ['1.4.0', 'Closed', ''] } },
    halka: { n: 'Halka', c: '#DB2777', s: { apple: ['1.0 (37)', 'TestFlight', 'info'], google: ['1.0.0', 'Internal', ''] } },
    mola: { n: 'Mola', c: '#15803D', s: { apple: ['3.1.2', 'Yayında', 'ok'], google: ['3.1.2', 'Production', 'ok'] } },
    tarla: { n: 'Tarla', c: '#9A3412', s: { google: ['0.9.4', 'Internal', ''], ms: ['0.9.4', 'Yayında', 'ok'] } },
    kumbara: { n: 'Kumbara', c: '#475569', s: { apple: ['1.0', 'Hazırlanıyor', ''] } }
  };
  var TABS = [['ozet', 'Genel bakış'], ['hazirlik', 'Hazırlık'], ['kayit', 'Mağaza kaydı'], ['yayin', 'Yayın'], ['para', 'Para'], ['geri', 'Geri bildirim'], ['uyum', 'Uyum']];
  var SUBS = {
    kayit: [['Metinler'], ['Görseller'], ['Önizleme videoları', 'apple,google'], ['Kategori ve yaş'], ['Fiyat ve bölgeler'], ['Uygulama ayrıntıları']],
    yayin: [['Build\'ler'], ['Sürümler ve track\'ler'], ['TestFlight', 'apple'], ['Paket uçuşları', 'ms'], ['Aşamalı yayın'], ['İnceleme bilgileri']],
    para: [['Abonelikler', 'apple,google'], ['Uygulama içi ürünler', 'apple,google'], ['Add-on\'lar', 'ms']],
    geri: [['Yorumlar'], ['Red kayıtları']],
    uyum: [['Zaman çizelgesi'], ['Kayıtlı cevaplar'], ['Asistanlı adımlar']]
  };
  document.querySelectorAll('.apphead[data-app]').forEach(function (h) {
    var d = h.dataset, a = APPS[d.app] || APPS.pusula, cur = d.store || Object.keys(a.s)[0];
    var acts = h.innerHTML;
    var seg = '<div class="seg" role="tablist" aria-label="Mağaza">' + Object.keys(a.s).map(function (k) {
      return '<span class="' + (k === cur && d.store !== 'all' ? 'on' : '') + '"><span class="st sm"><b>' + STORES[k][0] + '</b></span>' + STORES[k][1] + '</span>';
    }).join('') + '</div>';
    var lanes = Object.keys(a.s).map(function (k) {
      var x = a.s[k]; return '<span class="bdg ' + x[2] + '"><span class="dot"></span>' + STORES[k][0] + ' ' + x[0] + ' · ' + x[1] + '</span>';
    }).join('');
    var tabs = '<div class="tabs" role="tablist">' + TABS.map(function (t) {
      var n = t[0] === 'hazirlik' && d.ready ? ' <span class="n' + (d.ready.indexOf('!') === 0 ? ' dng' : '') + '">' + d.ready.replace('!', '') + '</span>' : '';
      return '<a class="tab' + (t[0] === d.tab ? ' on' : '') + '" href="#">' + t[1] + n + '</a>';
    }).join('') + '</div>';
    var subs = '';
    if (SUBS[d.tab]) {
      subs = '<div class="r gp6 wrap mb16 subnav">' + SUBS[d.tab].filter(function (s) { return !s[1] || d.store === 'all' || s[1].split(',').indexOf(cur) >= 0; }).map(function (s) {
        return '<span class="chip' + (s[0] === d.sub ? ' on' : '') + '">' + s[0] + '</span>';
      }).join('') + '</div>';
    }
    h.innerHTML = '<div class="r gp16 wrap mb16"><span class="aic l" style="--c:' + a.c + '">' + a.n[0] + '</span>' +
      '<div class="f1"><div class="r gp10 wrap"><h1 class="pg-t">' + a.n + '</h1>' + lanes + '</div>' +
      '<div class="t-s fg3 mono mt4">' + (d.meta || 'com.kuzeylab.' + d.app) + '</div></div>' +
      (d.store === 'all' ? '' : seg) + (acts.trim() ? '<div class="pg-a apphead-a">' + acts + '</div>' : '') + '</div>' + tabs + subs;
  });

  /* ---------- Çerçeve ölçekleme ---------- */
  function sizeOf(fr) {
    var v = fr.getAttribute('data-view') || root.getAttribute('data-view') || 'desktop';
    var ph = v === 'phone';
    var h = ph ? (fr.getAttribute('data-ph') || 844) : (fr.getAttribute('data-h') || 820);
    return { w: ph ? 390 : 1280, h: +h };
  }
  function fit() {
    document.querySelectorAll('.fr').forEach(function (fr) {
      var vp = fr.querySelector(':scope > .vp'); if (!vp) { return; }
      var scr = vp.querySelector(':scope > .scr'); if (!scr) { return; }
      var s = sizeOf(fr), avail = vp.clientWidth || s.w, k = Math.min(1, avail / s.w);
      scr.style.width = s.w + 'px'; scr.style.height = s.h + 'px';
      scr.style.transform = k < 1 ? 'scale(' + k + ')' : 'none';
      vp.style.height = Math.round(s.h * k) + 'px';
    });
  }

  /* ---------- Tema, görünüm, hareket ---------- */
  function setAttr(k, v) {
    root.setAttribute('data-' + k, v);
    document.querySelectorAll('.dseg [data-set="' + k + '"]').forEach(function (b) { b.setAttribute('aria-pressed', String(b.getAttribute('data-v') === v)); });
    save(k, v);
    if (k === 'view') { requestAnimationFrame(fit); }
    if (k === 'motion') { document.dispatchEvent(new CustomEvent('motionchange')); }
  }
  var st = load();
  setAttr('theme', st.theme || root.getAttribute('data-theme') || 'dark');
  setAttr('view', st.view || 'desktop');
  var sysReduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  setAttr('motion', sysReduce ? 'reduce' : (st.motion || 'full'));
  document.addEventListener('click', function (e) {
    var b = e.target.closest('.dseg [data-set]'); if (!b) { return; }
    setAttr(b.getAttribute('data-set'), b.getAttribute('data-v'));
  });

  /* ---------- SVG SMIL animasyonları hareket azaltmada durur ---------- */
  function motion() {
    var reduce = root.getAttribute('data-motion') === 'reduce';
    document.querySelectorAll('svg.anim').forEach(function (s) {
      try { if (reduce) { s.pauseAnimations(); s.setCurrentTime(0); } else { s.unpauseAnimations(); } } catch (e) {}
    });
  }
  document.addEventListener('motionchange', motion);

  /* ---------- Bölüm içindekiler ---------- */
  var toc = document.querySelector('.doc-toc');
  if (toc) {
    toc.innerHTML = Array.prototype.map.call(document.querySelectorAll('.sec'), function (s, i) {
      if (!s.id) { s.id = 's' + (i + 1); }
      var h = s.querySelector('.sec-h h2'); return h ? '<a href="#' + s.id + '">' + h.textContent + '</a>' : '';
    }).join('');
  }

  window.addEventListener('resize', fit);
  if (document.fonts && document.fonts.ready) { document.fonts.ready.then(fit); }
  fit(); motion();
})();
