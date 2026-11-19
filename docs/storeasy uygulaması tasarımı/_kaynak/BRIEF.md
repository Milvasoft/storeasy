# storeasy tasarım brief'i (bölüm yazarları ve geliştirme oturumları için)

Bu dosya tasarımın ortak kurallarıdır. Her bölüm dosyası bu kurallarla yazılır; geliştirme oturumları tasarımı koda
geçirirken de buradaki kararları esas alır. Standartlar: `C:\Users\bugra\source\repos\app-standarts\09-tasarim-uretme-promptu.md`
(tasarım ilkeleri bölüm 2), `08-yonetim-ekranlari-tasarim-rehberi.md` (liste, form, onay, durum kuralları), ürün planı
`docs/urun-plani.md` (kapsam 2, veri modeli 5, rol matrisi 6, fazlar 7, kapsam matrisi 11, kural kataloğu 12).

## 1. Dosya biçimi ve üretim

- Kaynak parça: `_kaynak/sayfalar/NN-Ad.html`. Başında yorum bloğu:
  ```
  <!--
  title: Sayfa başlığı
  kicker: NN · Web uygulaması
  desc: Bir iki cümle: bu dosyada neler var.
  -->
  ```
  Ardından isteğe bağlı tek `<style>` bloğu (yalnız bu dosyaya özel CSS; kit sınıflarını ezme), sonra bölümler.
- Üretim ve denetim: tasarım klasöründen `node _kaynak/build.mjs NN`. Çıktı `NN-Ad.html` (tek başına açılan dosya:
  kit CSS, ikon sprite'ı ve runtime gömülü). Denetim: etiket dengesi, uzun tire / en dash = 0, `{{ }}` yok, tanımsız
  `<use href="#...">` yok, script sözdizimi. "HATA" kalan dosya teslim edilmez.
- Kit dosyalarını (`kit.css`, `kit.js`, `sprite.svg`, `build.mjs`) DEĞİŞTİRME. Eksik bir şey varsa dosyaya özel CSS yaz;
  gereken yeni ikonu parçanın içinde gizli bir `<svg width="0" height="0" style="position:absolute" aria-hidden="true"><defs><symbol id="i-...">`
  bloğunda tanımla (aynı 24x24 çizgi stili) ve raporunda belirt.
- Referans bölüm: `_kaynak/sayfalar/06-Uygulamalar-ve-Hazirlik.html`. Önce bunu baştan sona oku; desenleri buradan al.
- Önizleme: `http://127.0.0.1:8917/NN-Ad.html` (yerel sunucu açık). Üst bardan Masaüstü / Telefon, Açık / Koyu,
  Hareket / Azaltılmış seçilir. Tarayıcı aracı kullanacaksan kendi sekmeni aç (`tabs_create`), işin bitince kapat.

## 2. Bölüm ve çerçeve iskeleti

```html
<section class="sec" id="kisa-ad">
<div class="sec-h"><span class="k">Bölüm 7.1-7.4</span><h2>Bölüm başlığı</h2><p>Bölümün amacı.</p></div>
<div class="frames">
<figure class="fr" id="f7-1" data-h="980">
  <figcaption class="fr-h"><b class="fr-n">7.1</b><span class="fr-t">Ekran adı</span><code class="fr-r">/rota</code></figcaption>
  <p class="fr-note"><b>API:</b> <code>GET /api/v1.0/...</code> → <code>Dto { Alan, ... }</code>. <b>İzin:</b> <code>EditContent</code>. Davranış, uç durumlar, telefonda ne değişir. <span class="gap">Backend boşluğu:</span> ...</p>
  <div class="vp"><div class="scr">
    <div class="shell" data-nav="apps" data-crumbs="Uygulamalar|Pusula|Metinler">
      <div class="content"> ... ekran ... </div>
    </div>
  </div></div>
</figure>
</div>
</section>
```

- Numara: dosya numarası + sıra (`7.1`, `7.2`...). `id="f7-1"`. Her ekranın notu: amaç/davranış, **API** (uç + DTO alanları),
  **İzin** (OrgPermission), telefon davranışı, varsa `Backend boşluğu`.
- Çerçeve 1280x820 masaüstü; aynı işaretleme Telefon görünümünde 390x844 olur (kapsayıcı sorgusu). Uzun ekranlarda
  `data-h="1000"` (masaüstü yüksekliği). İçerik `.content` içinde dikey kayar; yatay taşma olmamalı.
- Yalnız telefonda anlamlı çerçeve: `data-view="phone"` (ör. açık çekmece menü). Yalnız masaüstü: `data-view="desktop"`.
- Uygulama kabuğu `kit.js` tarafından üretilir: `<div class="shell" data-nav="KEY" data-crumbs="A|B|C" [data-live] [data-live="1 iş sürüyor"] [data-role="Editor|Viewer|Admin"] [data-act="Metin" data-act-icon="i-..."] [data-act=""] [data-admin] [data-bell="off"]>`.
  `data-nav` anahtarları: `apps, activity, reviews, compliance, studio, intake, connections, webhooks, members, audit, rules, settings, mcp`; `data-admin` ile Platform grubu: `a-rules, a-users, a-roles, a-logs, a-langs, a-flags, a-health`.
  `data-act` verilmezse kenar çubuğunda "Uygulama ekle" ana eylemi vardır; ekranın kendi ana eylemi varsa `data-act=""` ver (ekranda tek ana eylem kuralı).
- Uygulama detayı başlığı: `<div class="apphead" data-app="pusula|defter|halka|mola|tarla|kumbara" data-store="apple|google|ms|all" data-tab="ozet|hazirlik|kayit|yayin|para|geri|uyum" data-sub="Metinler" [data-ready="!5"] [data-meta="mono alt satır"]>[eylem düğmeleri]</div>`.
  Alt bölümler (`data-sub`): kayit: Metinler, Görseller, Önizleme videoları, Kategori ve yaş, Fiyat ve bölgeler, Uygulama ayrıntıları ·
  yayin: Build'ler, Sürümler ve track'ler, TestFlight (apple), Paket uçuşları (ms), Aşamalı yayın, İnceleme bilgileri ·
  para: Abonelikler, Uygulama içi ürünler, Add-on'lar (ms) · geri: Yorumlar, Red kayıtları · uyum: Zaman çizelgesi, Kayıtlı cevaplar, Asistanlı adımlar.
- Kabuksuz ekranlar (giriş vb.) doğrudan `.scr` içine yazılır (`.auth`, `.auth-f`, `.auth-c`, `.auth-art`).

## 3. Kit sınıfları (özet; tamamı `kit.css`)

- Düzen: `.r .rt .c .bt .ac .wrap .f1 .ml .gp4..gp32 .mt4..mt32 .g2 .g3 .g4 .g5 .split (sağ 340) .split-l (sol 300) .split-r .grid .hr .hr.d`
- Tipografi: `.pg-h .pg-t .pg-s .pg-a .h2 .h3 .eb .t-xs .t-s .t-m .t-l .fg2 .fg3 .b6 .mono .num .tnum .trunc .nowrap .ok-t .warn-t .dng-t .info-t .acc-t`
- Düğme: `.btn` (ikincil), `.btn.pri` (ana eylem, ekranda en fazla bir), `.btn.inv` (güçlü ikincil, ters renk), `.btn.ghost`, `.btn.dng` (tehlikeli onay), `.btn.dng-o`, `.btn.sm .lg .blk .dis .ld`, ikon düğmesi `.ib .ib.sm .ib.ghost`, `.pip`, `.lnk`, `.kbd`
- Form: `.fld > .lbl + .inp (.foc .err .ro .dis .ta .sel .mono) + .hint / .err-t`, `.cnt (.near .over)` karakter sayacı, `.chk(.on .mix .dis) .rad(.on) .sw(.on .dis) .seg > span.on .chip(.on)`, form üstü hata `.ferr`
- Rozet: `.bdg (.ok .warn .dng .info .acc .sol .sm .live) > .dot`; mağaza etiketi `<span class="st"><b>AS</b>App Store</span>` (`GP` Google Play, `MS` Microsoft Store; `.st.sm`); `.tag`, `.plat`
- Avatar ve ikon: `.av (.s .l .xl)`, uygulama ikonu `<span class="aic" style="--c:#0F766E">P</span>` (`.s .m .l`)
- Kart ve liste: `.card .card-h .card-b .card-f (.dash .warn .dng .sel)`, `.lst > .lst-i(.on)`, `dl.kv`, `.stat`, `.callout(.info .warn .dng .ok)`, `.banner(.warn .dng .info)`
- Sekme: `.tabs > a.tab.on` (`.n` sayaç)
- Tablo: `.tbl-w > table.tbl`; her `td`'ye `data-l="Kolon"`; başlık hücresi `td.tm`, eylem hücresi `td.ta`, telefonda gizlenecek `td.tx`. Telefonda satırlar karta döner. `.pager` ("Sayfa X / Y · Toplam: N", sayfa boyutu, önceki/sonraki), `.bulk`, `.toolbar`
- Boş ve yükleme: `.empty > .eic + h4 + p`, `.state > .sic + h3 + p`, iskelet `.sk (.t .b .ci)`, `.topline`, `.spinner`
- İlerleme: `.prog > i` (`.ok .warn .dng .neu .sm`), adımlar `.stp > span(.d .n .x)`, zaman çizelgesi `.tl > .tl-i > .tl-d(.ok .now .dng .warn)`, kontrol listesi `.ck > .ck-i > .ck-s(.ok .dng .warn .asst)`
- Diff ve kod: `.diff > .diff-h + .dl(.del .add)` (satır içi `<del>` / `<ins>`), `.code` (`.c1 .k1 .s1`), gizli değer kutusu `.secret`
- Yüzen: `.ovl > .mdl(.sm .lg .xl) > .mdl-h .mdl-b .mdl-f`, çekmece `.drw` (`.ovl` içinde), menü `.pop > .mi(.on .dng) .ms .mh` (konumu inline `style="top:..;right:.."`; ekran altında yukarı açılan örnek için `bottom:` kullan), `.toasts > .toast(.ok .dng .info .warn)`, `.tip`
- Telefon yardımcıları: `.hide-ph`, `.only-ph`, `.fab` (telefonda sağ altta yüzen ana eylem)
- İkon: `<svg class="ic s16"><use href="#i-..."/></svg>`. Mevcut ikon kimlikleri `sprite.svg`'de (i-grid, i-activity, i-image, i-images, i-msg, i-key, i-users, i-user, i-user-plus, i-user-check, i-list, i-list-check, i-search, i-bell, i-bell-off, i-plus, i-minus, i-chev-d/u/r/l, i-chevs, i-arrow-r/l/up/down, i-arrows-lr, i-sort, i-x, i-check, i-check-c, i-x-c, i-alert-t, i-alert-c, i-info, i-help, i-lock, i-unlock, i-eye, i-eye-off, i-mail, i-mail-check, i-upload, i-download, i-cloud-up, i-refresh, i-settings, i-sliders, i-logout, i-login, i-building, i-switch, i-copy, i-ext, i-link, i-unlink, i-trash, i-edit, i-more, i-more-v, i-filter, i-calendar, i-cal-clock, i-clock, i-hourglass, i-history, i-play, i-pause, i-stop, i-replay, i-rocket, i-send, i-shield, i-shield-check, i-shield-x, i-wifi-off, i-server, i-database, i-globe, i-lang, i-flag, i-star, i-reply, i-tag, i-layers, i-box, i-file, i-file-text, i-folder, i-code, i-terminal, i-branch, i-compare, i-webhook, i-zap, i-hash, i-phone, i-tablet, i-monitor, i-fold, i-frame, i-type, i-palette, i-move, i-crop, i-maximize, i-clipboard, i-clip-check, i-bookmark, i-spark, i-cpu, i-workflow, i-inbox, i-archive, i-sun, i-moon, i-menu, i-home, i-coin, i-repeat, i-percent, i-gauge, i-flask, i-plane, i-grip, i-fingerprint, i-plug, i-ban, i-bug, i-dot, i-circle, i-circle-dash, i-loader, i-pin, i-scale, i-book, i-radar, i-hand, i-wand, i-cursor, i-gift, i-cart, i-keyboard, i-qr, i-cookie, i-scroll).
  Logo: `<svg class="mk" width="28" height="28"><use href="#logo"/></svg>` + `<span class="wm">stor<i>e</i>asy</span>`; vurgu zemin üstünde `.mk.inv`.
- Token'lar (RGB üçlüsü, `rgb(var(--x))` veya `rgb(var(--x)/.5)`): `--bg --bg-2 --surface --border --border-strong --fg --fg-2 --fg-3 --accent --accent-fg --accent-text --success --warning --danger --info --overlay`. Ham hex yalnız uygulama ikon renklerinde ve stüdyodaki kullanıcı içeriğinde (mockup arka planları).

## 4. Görsel ve metin kuralları

- Vurgu (Ultramarin #4636FF açık / #857AFF koyu) yalnız: ana eylem (ekranda en fazla bir `.btn.pri`), seçili durum (sekme çizgisi, seçili çip, onay kutusu, radyo, anahtar, seçili menü), ilerleme. Bağlantılar nötr kalın metin (`.lnk`). Uyarı ve ikincil güçlü eylem ters renk (`.btn.inv`).
- Durum renkleri yalnız rozet, toast ikonu, tehlikeli eylem metni, hata kenarlığı; her zaman metinle birlikte.
- Kart ve listede gölge yok; gölge yalnız yüzen öğelerde. Alan odağı nötr çizgi (`.inp.foc`).
- Apple, Google, Microsoft logoları ve cihaz görselleri ÇİZİLMEZ. Mağazalar nötr metin + kendi `AS / GP / MS` etiketiyle. Cihaz çerçeveleri storeasy'nin kendi parametrik çizimi.
  - Not, 19 Kasım 2026: kullanıcı bu kuralı değiştirdi. Bir mağaza bağlantısına isteğe bağlı simge seçilebilir (mağazanın kendi logosu, yalnız mağazayı belirtmek için, ya da renkli genel bir simge); seçilmişse etiket kod kutusu yerine onu gösterir, seçilmemişse `AS / GP / MS` kalır.
- Emoji yok. Uzun tire (U+2014) ve en dash (U+2013) hiçbir yerde yok; boş değer `-`, aralık `1-10`, ayırıcı ` · `, ok gerekiyorsa `→`.
- Bütün metin doğal Türkçe (i18n'e hazır: sabit metin gibi değil, sözlük anahtarına dönecek cümleler). Teknik adlar (track, build, TestFlight, AAB, MSIX, webhook, token) olduğu gibi. Kimlik, sürüm, boyut, hash, uç adları `.mono`.
- Tarih biçimi: "18 Kas 2026", "18 Kasım 2026, 14:02"; bugün **18 Kasım 2026** (Çarşamba). Sayılar Türkçe ayraçla (4,7 puan, 31,4 MB, 12.480).
- Hareket yalnız gerçekten bir şey olurken (canlı iş, yükleme); `@media (prefers-reduced-motion)` ve üst bardaki "Azaltılmış" seçimi bütün animasyonu durdurur. SMIL kullanan SVG'ye `class="anim"` ver (runtime durdurur); azaltılmış modda gösterilecek alternatif `.rm-only`, gizlenecek `.rm-hide`.

## 5. Örnek veri (bütün dosyalarda aynı; uydurma, gerçek kimlik yok)

- Organizasyon **Kuzey Lab** (slug `kuzey-lab`, Id 7). Kullanıcının diğer organizasyonları: **Ada Stüdyo** (Editor), **Deniz'in projeleri** (Owner, kişisel).
- Giriş yapan: **Deniz Arslan**, `deniz@kuzeylab.dev`, Owner. Üyeler: **Ece Yılmaz** `ece@kuzeylab.dev` Admin; **Mert Kaya** `mert@kuzeylab.dev` Editor; **Selin Demir** `selin@kuzeylab.dev` Viewer. Bekleyen davet: `can@kuzeylab.dev` Editor, 25 Kas 2026'da bitiyor.
- Uygulamalar (renk, bundle / paket, mağaza durumları):
  - **Pusula** `#0F766E` `com.kuzeylab.pusula`, ASC app id `6740012345`; App Store 2.3 (118) İncelemede (17 Kas 16:40 gönderildi, aşamalı yayın açık); Play 2.3.0 (230) production %20 aşamalı yayın 3. gün, closed 2.4.0 (231); Microsoft Store MSIX 2.3.0.0 sertifikasyonda, Store ID `9P4K7Q2MZL1D`, uçuş "Kuzey içi" 2.4.0.0. Diller tr, en-US, de-DE.
  - **Defter** `#B45309` `com.kuzeylab.defter`, `6740023456`; App Store 1.4 (52) **Reddedildi** (Guideline 2.1 ve 5.1.1, red kaydı 16 Kas'ta açıldı), Play 1.4.0 closed. Geçmiş red: 1.2'de 4.3.
  - **Halka** `#DB2777` `com.kuzeylab.halka`, `6740034567`; App Store yalnız TestFlight 1.0 (37), build **24 Kas 2026'da bitiyor**; gruplar "Kuzey içi" (iç) ve "Erken erişim" (dış, 48 test kullanıcısı); Play 1.0.0 (42) internal, AAB yükleniyor 31,4 / 48,0 MB.
  - **Mola** `#15803D` `com.kuzeylab.mola`, `6740045678`; App Store 3.1.2 yayında 4,7 puan, Play 3.1.2 production 4,5 puan; Çin'de dağıtılıyor (App Compliance beyanı 31 Oca 2027); en çok yorum alan uygulama.
  - **Tarla** `#9A3412` `com.kuzeylab.tarla`; Play 0.9.4 internal; Microsoft Store EXE yükleyici 0.9.4 yayında (MSI/EXE API, yükleyici URL'si `https://indir.kuzeylab.dev/tarla/0.9.4/TarlaSetup.exe`).
  - **Kumbara** `#475569` `com.kuzeylab.kumbara`, `6740056789`; App Store 1.0 (12) ilk gönderim hazırlanıyor, hazırlık 7 / 12; abonelik grubu "Kumbara Plus" (Aylık 49,99 TL, Yıllık 399,99 TL).
- Mağaza bağlantıları: "Kuzey Lab ASC" (Apple, Key ID `7QX2M4K9PL`, Issuer son 4 `…a91f`, Doğrulandı 18 Kas 09:12); "Play servis hesabı" (Google, `storeasy@kuzeylab-play.iam.gserviceaccount.com`, proje `kuzeylab-play`); "Kuzey Lab Partner Center" (Microsoft, tenant son 4 `…3c07`, client id son 4 `…be12`, satıcı `40291187`, sır bitişi **10 Ara 2026**).
- Canlı işler: Pusula iPad 13" tr ekran görüntüleri (Apple) 4 / 7; Halka AAB internal (Google) 31,4 / 48,0 MB.
- Yorumlar: 14 okunmamış (Mola 8, Pusula 4, Defter 2). PAT önekleri `sea_`, CI token öneki `sci_`.
- Son tarihler: Halka TestFlight 24 Kas 2026; Entra sırrı 10 Ara 2026; yaş derecelendirmesi yeni soruları 12 Oca 2027; Çin App Compliance 31 Oca 2027 (Mola); iPhone Duo Nisan 2027 (iOS 27.1 SDK); Play hedef API 36 (31 Ağu 2026'dan beri).

## 6. Backend sözleşmesi

- Uç yolları `/api/v1.0/<controller>/<eylem>` (ör. `/api/v1.0/organizations/members`). Liste uçları `PATCH` + ListRequest
  (`PageNumber, RowCount, Filtering.Criterias[{FilterBy, Value, Type}], Sorting`), yanıt `Data[], CurrentPageNumber, TotalPageCount, TotalDataCount`.
  Güncelleme kısmi (`UpdateProperty`), yanıt zarfı `{ IsSuccess, StatusCode, Messages[{Key, Message, Type}], Data }`. Mesaj metni sunucudan gelir.
- Organizasyona bağlı her istek `OrganizationId` taşır; yetki her istekte üyelikten (`[OrgAuth]`). OrgRole: `Owner=1, Admin, Editor, Viewer`.
  OrgPermission: `View, EditContent, ReleaseTest, ReleaseProduction, SubmitForReview, ReplyReviews, ManageConnections, ManageWebhooks, ManageMembers, ManageOrganization`.
  Matris (plan bölüm 6): Viewer yalnız View (+ salt okunur PAT); Editor + EditContent, ReleaseTest, ReplyReviews; Admin + ReleaseProduction, SubmitForReview, ManageConnections, ManageWebhooks, ManageMembers (Owner hariç); Owner hepsi + ManageOrganization (owner atama, organizasyonu silme).
- **Var olan uçlar** (`storeasy-api/src/Storeasy.Api/Controllers`):
  - `account`: `POST login {UserName (e-posta), Password, DeviceId}`, `POST login/refresh`, `POST logout`, `PUT password/change`, `GET detail`, `PATCH notifications`, `PUT notifications/seen`, `DELETE notifications`.
  - `organizations`: `GET mine` → `MyOrganizationDto { Id, Name, Slug, Role, Permissions[] }`; `POST organization {Name, Slug}`; `PUT organization`; `GET members` → `MemberListDto { Members[OrganizationMemberDto { Id, UserId, Email, Name, Surname, Role, JoinDate }], Invitations[OrganizationInvitationDto { Id, Email, Role, ExpiryDate, CreationDate }] }`; `POST invitation {Email, Role}` → `InvitationCreatedDto { Id, Token, ExpiryDate }`; `DELETE invitation`; `POST invitation/accept {Token}`; `PUT member/role {MemberId, Role}`; `DELETE member` (kendi üyeliğiyse ayrılma); `PATCH audit-logs` → `StoreAuditLogDto { Id, Date, UserName, Channel (Web, Mcp, Api, System), Action, Store?, TargetId, Summary, Details }`.
  - `storeConnections`: `GET` → `StoreConnectionDto { Id, Store (Apple, Google), Name, Fingerprint { KeyId, IssuerIdSuffix, ServiceAccountEmail, ProjectId }, Status (NotVerified, Verified, Rejected, Unreachable), LastVerifiedDate, LastVerifyError, CreationDate }`; `POST connection {Store, Name, Secret}`; `POST connection/verify`; `DELETE connection`.
  - Platform (Manager tipi, 08 E-ekranları): `users` (PATCH, GET user, POST user, PUT user, DELETE user), `roles`, `permissions` (+ `PUT migrate`), `activitylogs`, `languages`, `featureFlags`, `exports/export`, `healthcheck` (`/health/live`, `/health/ready`).
  - AuditAction değerleri bugün: OrganizationCreated, OrganizationUpdated, MemberInvited, InvitationRevoked, MemberJoined, MemberRoleChanged, MemberRemoved, ConnectionAdded, ConnectionUpdated, ConnectionRemoved, ConnectionVerified. Mağaza yazmaları için yeni değerler backend boşluğudur.
- **Planlanan uçlar** (adlandırma bu listeyle tutarlı olsun; listede olmayan bir uca ihtiyaç duyarsan aynı desende adlandır ve raporla):
  - account: `POST register`, `POST email/verify`, `POST email/verify/resend`, `POST password/forgot`, `POST password/reset`, `GET sessions`, `DELETE session`, `DELETE sessions/others`, `PUT profile`, `POST email/change`, `GET preferences`, `PUT preferences` (dil, tema, bildirim tercihleri), `POST data-export`, `DELETE account`.
  - organizations: `DELETE organization`, `PUT owner` (sahiplik devri), `GET settings`, `PUT settings` (paket saklama günü, denetim kaydı saklama), `GET invitation?Token=` (davet önizleme, anonim).
  - apps: `PATCH apps`, `GET apps/app`, `POST apps/app`, `PUT apps/app`, `DELETE apps/app`, `GET apps/unlinked-store-apps`, `POST apps/sync`, `GET apps/summary`, `GET apps/readiness`, `GET apps/compliance`, `GET apps/answers`, `PUT apps/answers`, `POST apps/answers/copy`; org geneli `GET compliance`.
  - Mağaza kaydı: `GET listings?StoreAppId=&Locale=`, `POST listings/plan` (diff + dry run → `ChangePlanDto { PlanId, Items[{ Field, Locale, Before, After, Action }], Warnings[] }`), `POST listings/apply {PlanId}` → StoreJob; `GET screenshots/sets`, `POST screenshots/plan`, `POST screenshots/apply`; `POST assets/upload-url` (presigned), `GET categories`, `PUT categories`, `GET age-rating`, `PUT age-rating`, `GET pricing`, `PUT pricing`.
  - Yayın: `PATCH builds`, `POST builds/link`, `PUT builds/export-compliance`, `GET releases`, `POST releases/plan`, `POST releases/apply`, `PUT releases/rollout` (duraklat, sürdür, yüzde), `POST releases/submit-for-review {..., Confirm}`, `GET review-info`, `PUT review-info`; TestFlight `GET testflight/groups`, `POST testflight/group`, `PUT testflight/group/testers`, `GET testflight/builds`, `PUT testflight/build/what-to-test`, `POST testflight/build/beta-review`; Microsoft `GET ms/submissions`, `GET ms/flights`, `POST ms/flight`.
  - Geri bildirim: `PATCH reviews`, `POST reviews/reply`, `GET reviews/saved-replies`, `POST reviews/saved-reply`; `PATCH rejections`, `GET rejections/case`, `PUT rejections/case/text`, `PUT rejections/case/checklist`, `PUT rejections/case/reply-draft`, `PUT rejections/case/close`.
  - Para: `GET subscriptions/groups`, `GET subscriptions/subscription`, `POST subscriptions/plan`, `POST subscriptions/apply`, `PATCH iap`, `GET ms/addons`.
  - İşler: `PATCH jobs`, `GET jobs/job` (+ adımlar), `POST jobs/retry`, `POST jobs/cancel`, `PATCH plans` (onay bekleyen değişiklik planları), `POST plans/approve`, `POST plans/reject`; SignalR `/hubs/activity` olayları `job.created`, `job.step`, `job.finished`, `webhook.received`, `review.received`.
  - Stüdyo: `PATCH mockups/templates`, `GET mockups/template`, `POST mockups/template`, `PUT mockups/template`, `GET mockups/frames`, `PUT mockups/captions`, `POST mockups/render-preview`, `POST mockups/export/plan`, `POST mockups/export`.
  - Paket alma: `POST /api/v1.0/apps/{appId}/artifacts` (CI yükleme oturumu), `PUT artifacts/{sessionId}/complete`, `PATCH artifacts/sessions`, `GET artifacts/session`, `GET artifacts/trusted-publishers`, `POST artifacts/trusted-publisher`, `DELETE artifacts/trusted-publisher`, `GET artifacts/ci-tokens`, `POST artifacts/ci-token`, `DELETE artifacts/ci-token`, `GET artifacts/delivery-rules`, `PUT artifacts/delivery-rules`, `GET artifacts/sources`, `PUT artifacts/source` (EAS / genel webhook), `POST oidc/exchange` (CI token değişimi).
  - Webhook ve bildirim: `GET webhooks`, `POST webhooks/endpoint`, `POST webhooks/endpoint/rotate-secret`, `PATCH webhooks/events`, alıcı `POST /webhooks/apple/{endpointId}` (anonim, HMAC); bildirim tercihleri `account/preferences`.
  - PAT: `GET tokens`, `POST tokens/token` (değer yalnız bir kez döner), `DELETE tokens/token`.
  - Kurallar: `PATCH rules`, `GET rules/rule`, `PATCH rules/proposals`, `PUT rules/proposal/approve`, `PUT rules/proposal/reject`, `GET rules/sources`.
  - Asistanlı adım: `PATCH assisted-steps`, `GET assisted-steps/step`, `POST assisted-steps/step/handoff`, `PUT assisted-steps/step/complete`.
  - Platform: `GET system/status`, `GET system/settings`, `PUT system/settings` (kayıt modu `Open | InviteOnly | Closed`).
- Planda olmayan alan / uç / davranış çizersen notta `<span class="gap">Backend boşluğu:</span>` ile yaz ve raporla.

## 7. Her dosyada bulunacak durumlar

Dosyanın kendi ekranları için uygun olanlar: iskelet yükleme, boş, arama sonucu yok, hata + tekrar dene, 403 (izinsiz rol),
404 (kayıt yok), form hataları (`.ferr` + alan hatası), onay diyaloğu (tehlikeli eylemde kırmızı düğme, oturum kapatan eylemde
metinde söylenir), toast, 409/429/503 gibi uca özgü durumlar. Genel durum ekranları ayrıca 18-Durum-Ekranlari dosyasında.

## 8. Rapor (bölüm yazarı işini bitirince)

Kısa ve Türkçe: çerçeve listesi (numara, ad, rota), kullanılan uçlar (var olan / planlanan), eklenen yeni uç adları,
backend boşlukları (her biri için öneri: Eklenecek ya da Tasarımdan çıkarıldı), dosyaya özel tanımlanan ikonlar,
build denetiminin son çıktısı. Commit atma, kit dosyalarına ve başka bölüm dosyalarına dokunma.
