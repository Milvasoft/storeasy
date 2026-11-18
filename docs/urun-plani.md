# storeasy ürün planı

Son güncelleme: 18 Kasım 2026. Durum: taslak, Faz 1 başladı.

## 1. Ürün

storeasy, App Store Connect ve Google Play işlerini resmi API'ler üzerinden tek yerden yönetmek için açık kaynak bir web
ürünüdür. Hedef: mağaza metinleri, ekran görüntüleri, build, abonelik, inceleme ve yorum işlerini tarayıcıda tıklayarak
saatlerce uğraşmadan, kontrollü (diff, dry run, onay, rol) ve izlenebilir (denetim kaydı, canlı aktivite) şekilde yapmak.
Önünde MCP olduğu için bir Claude oturumu arayüzün yapabildiği her şeyi aynı yetki kontrolleriyle yapabilir.

Kullanıcılar: uygulama geliştiren ekipler ve tek kişilik geliştiriciler (son kullanıcı ürünü). Barındırılan sürüm
(storeasy'nin kendi sunucusu) ve kendi sunucunda çalıştırma (self-host, Docker) aynı koddan çıkar.

Motor: `store-ops` (TypeScript) 1. aşamada yazılan ve test edilen mağaza çekirdeğidir. storeasy'de bu çekirdek .NET'e
taşınır (bkz. 4.2); `store-ops` storeasy onun yerini alana kadar yerel CLI ve stdio MCP olarak çalışmaya devam eder.

## 2. Kapsam

### 2.1 Hesap ve erişim
- E-posta ile kayıt, giriş, e-posta doğrulama, şifre sıfırlama. Google ile giriş Faz 6'da.
- Organizasyon: kullanıcı birden çok organizasyona üye olabilir; kenar çubuğunda organizasyon değiştirici.
- Organizasyon rolleri: `Owner`, `Admin`, `Editor`, `Viewer` (seed, kilitli) ve Faz 6'da özel roller. Matris 6. bölümde.
- Davet: e-posta ile, rol seçerek, 7 gün geçerli, tek kullanımlık token.
- Platform yöneticisi (instance admin): şablondaki `Manager` kullanıcı tipi ve global `PermissionCatalog` ile; yalnız
  self-host kurulumunu yöneten kişi için (kullanıcı kilitleme, feature flag, sistem durumu).
- Mağaza bağlantıları: her organizasyon kendi App Store Connect API anahtarlarını (.p8 + Key ID + Issuer ID) ve Play
  servis hesabı JSON'larını bağlar. Şifreli saklanır, hiçbir uçtan geri dönmez; arayüz yalnız parmak izi gösterir
  (Key ID, Issuer ID'nin son 4 hanesi, servis hesabı e-postası, son doğrulama zamanı).
- Denetim kaydı: mağazaya giden her yazma işlemi (kim, ne zaman, hangi uygulama, ne değişti, hangi kanaldan: web, MCP,
  API) organizasyon bazında tutulur ve listelenir.

### 2.2 Uygulamalar ve mağaza kaydı
- İki mağazadaki bütün uygulamalar tek listede; bir storeasy "uygulaması" bir iOS ve bir Android kaydını eşleyebilir.
- Dil başına metinler (ad, alt başlık, açıklama, anahtar kelimeler, tanıtım metni, yenilikler, URL'ler; Play: başlık,
  kısa ve uzun açıklama, video).
- Ekran tipi başına ekran görüntüleri (boyut ve alfa kontrolü, sıralama, MD5 ile değişmeyeni atlama).
- Build'ler ve işlenme durumu, sürüme build bağlama; Play AAB yükleme ve track (varsayılan `internal`).
- Abonelikler ve uygulama içi satın almalar: lokalizasyonlar, inceleme görüntüsü.
- Yaş derecelendirmesi, kategoriler. Gizlilik etiketleri ve Play beyan formları API'de yok: kontrol listesi olarak
  gösterilir, ilgili mağaza sayfasına bağlantı verilir.
- Her yazmadan önce diff ve dry run; değişiklik planı ekranda onaylanır.
- İncelemeye gönderme yalnız `Review.Submit` iznine sahip rolle ve açık onay adımıyla (uygulama adı ve sürüm yazılarak).

### 2.3 Canlı aktivite görünümü
- Her mağaza işi bir `StoreJob` ve adımlarıdır (reserve, upload, commit, processing, reorder, edit commit...).
- Adımlar SignalR ile anlık yayınlanır. Arayüz "storeasy -> Apple/Google" ve geri yönü animasyonlu akış olarak çizer:
  giden paketler (yükleme parçaları ilerleme çubuğuyla), dönen yanıtlar, bekleyen işleme durumları, gelen webhook'lar ve
  yorumlar. Hatalı adım kırmızı, tekrar deneme sayısıyla.
- Aynı olaylar iş detayında zaman çizelgesi olarak kalır (yeniden oynatılabilir).
- `prefers-reduced-motion` açıkken animasyon yerine durum listesi.

### 2.4 Mockup ve ekran görüntüsü stüdyosu
- Ham ekran görüntüsünden mağazaya hazır görsel: başlık ve alt başlık, marka arka planı (düz, gradyan, görsel), cihaz
  çerçevesi, konum ve ölçek.
- Cihaz çerçeveleri storeasy'nin kendi çizimidir (vektör, parametrik: köşe yarıçapı, çerçeve kalınlığı, Dynamic Island,
  kamera deliği). Apple ve Google cihaz görselleri kullanılmaz.
- Şablon uygulama başınadır; başlıklar dil başına çevrilir (elle veya Faz 6'da yapay zeka önerisiyle, kill switch'li).
- Dışa aktarım her gerekli boyuta tek tıkla: Apple ekran tipleri (iPhone 6.9", 6.5", 6.1"/6.3" slotu yalnız 1206x2622
  ve 1179x2556 kabul eder, 1320x2868 reddedilir; iPad 13" 2064x2752), Play telefon, 7" ve 10" tablet, öne çıkan görsel
  1024x500. Alfa kanalı otomatik düzleştirilir.
- Üretilen setler doğrudan 2.2'deki yükleme akışına girer. MCP üzerinden de üretilir ("Sousial için tr ve en setini
  üret ve yükle").

### 2.5 Webhook ve bildirimler
- Her zaman açık alıcı: App Store Connect `APP_STORE_VERSION_APP_VERSION_STATE_UPDATED` (tekil yazım, eski çoğul yazım
  da kabul), `x-apple-signature: hmacsha256=<hex>` kontrolü, bağlantı başına ayrı gizli anahtar.
- Bildirim kanalları: uygulama içi (bildirim merkezi), e-posta, web push (Faz 6). Kullanıcı başına tercih.
- Red takibi: API Resolution Center metnini okuyamaz. Sürüm `REJECTED` veya `METADATA_REJECTED` olunca:
  1. bir "Red kaydı" açılır ve sahiplerine bildirim gider,
  2. kayıt ekranında App Store Connect'teki ilgili sayfaya doğrudan bağlantı ve "Red metnini buraya yapıştır" alanı olur,
  3. yapıştırılan metinden guideline numaraları ayrıştırılır, kayıt bir kontrol listesine dönüşür,
  4. Claude MCP ile metni okuyup düzeltme planı önerebilir; yanıt yine elle Resolution Center'a yazılır,
  5. yeniden gönderimde kayıt kapanır, geçmişte kalır.
- Play yorum kutusu: yeni yorumlar periyodik çekilir, yanıtlanır (350 karakter sınırı), hazır yanıtlar.

### 2.6 MCP
- Barındırılan ürünün önünde uzaktan MCP (Streamable HTTP, `/mcp`).
- Kimlik: kullanıcı başına kişisel erişim token'ı (PAT). Token bir organizasyona bağlıdır, rolü kullanıcının o
  organizasyondaki rolünü aşamaz, isteğe bağlı salt okunur. Sunucuda yalnız hash'i tutulur.
- Her araç aynı uygulama katmanı komutunu çağırır; yetki, diff, dry run, onay ve denetim kaydı arayüzle aynıdır.
  İncelemeye gönderme MCP'de de `confirm: true` ister.
- Yerel stdio modu: self-host için `storeasy-mcp` küçük bir köprü (stdio -> uzak `/mcp`); geçiş dönemi boyunca
  `store-ops` stdio sunucusu aynen çalışır.

### 2.7 Açık kaynak
- Herkese açık repo, lisans, `CONTRIBUTING.md`, `SECURITY.md`, self-host dokümanı (tek `docker compose up`).
- Repoda hiçbir gizli değer ve Milvasoft'a özel veri (uygulama kimlikleri, iç hostlar, değişken grubu adları) olmaz;
  CI'da gizli değer taraması (gitleaks) zorunlu.
- Milvasoft'un kendi deploy dosyaları (Azure DevOps, Dokploy) ayrı, özel bir ops reposunda tutulur.

## 3. Kapsam dışı (şimdilik)
- Ödeme ve plan/paket (06 modülü): barındırılan sürüm ücretli olacaksa ayrı karar.
- Mağaza analitiği ve gelir raporları.
- Mobil uygulama.
- Apple ve Google dışındaki mağazalar.

## 4. Mimari

### 4.1 Bileşenler

| Bileşen | Teknoloji | Görev |
|---|---|---|
| `storeasy-api` | .NET 10, Milvonion şablonu, PostgreSQL 17, Redis 7 | REST API, kimlik, organizasyon ve roller, mağaza motoru, iş kuyruğu, SignalR, webhook alıcısı, uzaktan MCP, mockup render |
| `storeasy-app` | React 18, Vite, React Query, Tailwind 3 (02 standardı) | Web arayüzü, canlı aktivite animasyonları, stüdyo editörü |
| Nesne depolama | S3 uyumlu (self-host'ta MinIO) | Ham görüntüler, mockup çıktıları, AAB'ler (geçici) |
| `storeasy-mcp` | Node (store-ops'tan türetilmiş küçük köprü) | Yerel stdio MCP -> uzak `/mcp` |

### 4.2 Kararlar ve gerekçeleri

| # | Karar | Gerekçe | Standarttan sapma |
|---|---|---|---|
| K1 | Mağaza çekirdeği C#'a taşınır (`Infrastructure/Stores/AppStore`, `.../GooglePlay`) | 01 ve 03 .NET dışı worker'ı kapsamıyor; şifreli anahtarlar API sürecinden çıkmaz; olaylar doğrudan SignalR'a gider; self-host tek imaj. store-ops testleri birebir C# testlerine çevrilir | Yok |
| K2 | Tek repo (`storeasy/`), içinde `storeasy-api/` ve `storeasy-app/` klasörleri standart düzende | Açık kaynakta tek repo, tek issue listesi, tek `docker compose` beklenir. Klasörler 07'deki repo düzenini aynen taşır, gerekirse ayrılır | 07 "servis başına repo" |
| K3 | Organizasyon rolleri şablonun global `PermissionCatalog`'unun yanında ayrı bir `OrgPermission` kataloğu ile; kontrol token'dan değil, istek başına üyelikten (Redis'te 60 sn önbellek) | Kullanıcı birden çok organizasyonda farklı rolde olabilir; token claim'i organizasyona göre değişemez. Rol değişikliği anında etkili olur | 01 "multitenancy yok" varsayımı; 09 org modeli tarif ediyor |
| K4 | Mağaza işleri DB'de `StoreJobs` tablosunda durum makinesi; API içindeki `StoreJobRunner` hosted servisi işler (`FOR UPDATE SKIP LOCKED`) | Self-host için ek servis yok. İş DB durumundan bulunur (03'ün sözleşmesiyle aynı), ileride Milvaion worker'a taşınabilir | 03 "hafif olmayan iş worker'da" |
| K5 | Gerçek zaman SignalR, Redis backplane (`CacheKeys` önekiyle), grup = organizasyon; hub yolu `/hubs/activity`; token `access_token` sorgu parametresiyle ve log maskesinde | Standartta gerçek zaman yok; ASP.NET Core'un yerleşiği, ek bağımlılık yok | Yeni middleware değil, `MapHub` |
| K6 | Kimlik bilgileri AES-256-GCM zarf şifreleme: kayıt başına veri anahtarı, ana anahtar ortam değişkeninde (`Storeasy__Encryption__MasterKey`), anahtar sürümü kolonda (rotasyon) | Veritabanı sızsa da anahtarlar açılmaz; Key Vault'a geçiş yalnız ana anahtar kaynağını değiştirir | Yok |
| K7 | Mail `IEmailSender`: self-host için SMTP, Milvasoft barındırması için Notifike API'si (DevOps önerisi) | Açık kaynak kullanıcısının Notifike'si yok | 03 "mail kuyruktan" yerine Notifike |
| K8 | Mockup render sunucuda SkiaSharp ile; şablon JSON'u hem önizleme hem dışa aktarımın tek kaynağı | MCP ile otomatik üretim sunucuda olmalı; tarayıcı ve sunucu çıktısı farklılaşmaz. Fontlar açık lisanslı (Inter, Noto Sans) ve imaja gömülü | Yok |
| K9 | Uzaktan MCP `ModelContextProtocol.AspNetCore` ile, araçlar MediatR komutlarını çağırır | Tek iş mantığı; yetki ve denetim tek yerde | Yok |
| K10 | Kayıt herkese açık (self-host'ta `Registration:Mode` = `Open`, `InviteOnly`, `Closed`) | Açık kaynak ürünün ilk kullanıcı akışı | 01 şablonda kayıt yok, eklenir |
| K11 | Giriş e-posta ile | 09 çok kiracılı ürün kuralı | Yok |
| K12 | Public repo CI'ı GitHub Actions (test + build + gitleaks); Milvasoft deploy'u Azure DevOps + Dokploy (özel ops reposu) | Dış katkıcılar PR'da CI görmeli; iç deploy düzeni bozulmaz | DevOps'a soruldu |

### 4.3 İş akışı örneği (ekran görüntüsü yükleme)
1. Arayüz veya MCP `PlanScreenshotUpload` çağırır: dosyalar S3'te, boyut ve alfa kontrolü, mevcut setle MD5 diff'i.
   Sonuç plan (eklenecek, silinecek, sıralama) olarak döner, hiçbir mağaza yazması yok.
2. Kullanıcı onaylar: `StoreJob` oluşur (`Queued`), denetim kaydı yazılır.
3. `StoreJobRunner` işi alır; her adım `StoreJobStep` satırı ve SignalR olayı: `set.create`, `asset.reserve`,
   `asset.upload` (parça ilerlemesi), `asset.commit`, `asset.processing`, `set.reorder`.
4. Hata: Apple 5xx'te iki deneme, sonra set kontrolü (store-ops davranışı); kalıcı hatada iş `Failed`, rezervasyon silinir.
5. Bitti: iş `Succeeded`, bildirim, denetim kaydı tamamlanır.

## 5. Veri modeli (Faz 1-5)

Tüm tablolar şablon tabanlarıyla (`FullAuditableEntity` vb.), zamanlar UTC. Organizasyona bağlı her tabloda
`OrganizationId` ve indeks.

| Tablo | Ana alanlar | Not |
|---|---|---|
| `Users` (şablon) | Email, Name, Surname, UserType, PreferredLanguage, EmailConfirmed | Giriş e-posta ile |
| `Organizations` | Name, Slug, CreatorUserId | Slug benzersiz |
| `OrganizationMembers` | OrganizationId, UserId, Role (`OrgRole`), JoinDate | (OrganizationId, UserId) benzersiz |
| `OrganizationInvitations` | OrganizationId, Email, Role, TokenHash, ExpiryDate, AcceptDate, InviterUserId | Token yalnız hash |
| `StoreConnections` | OrganizationId, Store (`Apple`, `Google`), Name, Fingerprint (jsonb), EncryptedSecret (bytea), EncryptedDataKey, KeyVersion, LastVerifiedDate, LastVerifyStatus | Secret hiçbir DTO'da yok |
| `Apps` | OrganizationId, Name, IconUrl | storeasy uygulaması |
| `StoreApps` | AppId, StoreConnectionId, Store, ExternalId (ASC app id / package name), BundleId, SyncedDate | Mağaza kaydı |
| `StoreJobs` | OrganizationId, StoreAppId, Kind, Status, Plan (jsonb), Result (jsonb), Channel (`Web`, `Mcp`, `Api`), RequestedByUserId, StartDate, EndDate, Attempt, LockedUntil | Kuyruk |
| `StoreJobSteps` | StoreJobId, Order, Kind, Direction (`Outbound`, `Inbound`), Status, Bytes, Total, Message, Date | Canlı akış ve yeniden oynatma |
| `StoreAuditLogs` | OrganizationId, UserId, Channel, Action, StoreAppId, TargetType, TargetId, Summary, Changes (jsonb), StoreJobId, Ip, Date | Silinmez; saklama süresi ayarlanabilir |
| `Assets` | OrganizationId, Kind, ObjectKey, Width, Height, HasAlpha, Md5, Sha256, Size | S3 nesnesi |
| `MockupTemplates` | OrganizationId, AppId, Name, Spec (jsonb) | Çerçeve, arka plan, yerleşim |
| `MockupCaptions` | MockupTemplateId, Locale, SlotOrder, Title, Subtitle | Dil başına |
| `WebhookEndpoints` | StoreConnectionId, ExternalWebhookId, EncryptedSecret, Events | Apple tarafındaki kayıt |
| `WebhookEvents` | WebhookEndpointId, EventType, Payload (jsonb), SignatureValid, ReceivedDate, ProcessedDate | Ham olay |
| `RejectionCases` | StoreAppId, VersionString, State, OpenDate, CloseDate, PastedText, Guidelines (jsonb), Checklist (jsonb) | Red takibi |
| `PlayReviews` | StoreAppId, ExternalReviewId, Author, Rating, Text, Language, Date, ReplyText, ReplyDate | Yorum kutusu |
| `Notifications` | UserId, OrganizationId, Kind, Payload, ReadDate | Şablondaki `InternalNotifications` genişletilir |
| `PersonalAccessTokens` | UserId, OrganizationId, Name, TokenHash, Prefix, ReadOnly, ExpiryDate, LastUseDate | MCP ve API |

## 6. Rol matrisi (organizasyon)

| İzin | Owner | Admin | Editor | Viewer |
|---|---|---|---|---|
| Uygulama, kayıt, iş ve denetim kaydını görme | Evet | Evet | Evet | Evet |
| Metin, ekran görüntüsü, mockup, build bağlama (diff, dry run, yazma) | Evet | Evet | Evet | Hayır |
| Play AAB yükleme (`internal`, test track'leri) | Evet | Evet | Evet | Hayır |
| Play `production` track'ine yayın | Evet | Evet | Hayır | Hayır |
| İncelemeye gönderme (`Review.Submit`) | Evet | Evet | Hayır | Hayır |
| Yorum yanıtlama | Evet | Evet | Evet | Hayır |
| Mağaza bağlantısı ekleme, silme, doğrulama | Evet | Evet | Hayır | Hayır |
| Webhook yönetimi | Evet | Evet | Hayır | Hayır |
| Üye davet etme, rol değiştirme (Owner hariç) | Evet | Evet | Hayır | Hayır |
| Owner atama, organizasyonu silme | Evet | Hayır | Hayır | Hayır |
| Kendi PAT'ini oluşturma (rolü aşamaz) | Evet | Evet | Evet | Evet (salt okunur) |

Kurallar: en az bir Owner kalır; Owner kendini düşüremez (önce başka Owner atanır); Admin, Owner'ı değiştiremez.

## 7. Fazlar

| Faz | İçerik | Bitti sayılır |
|---|---|---|
| 1. Temel | Repo, API şablondan, kayıt ve giriş (e-posta), organizasyon, üyelik, davet, roller, şifreli mağaza bağlantısı ve doğrulama, uygulama senkronu (liste), denetim kaydı, `StoreJob` kuyruğu ve SignalR olayları, docker-compose; web: tasarım (09), giriş, organizasyon değiştirici, uygulamalar, bağlantılar, üyeler, denetim kaydı | Testler yeşil, yerelde uçtan uca bağlantı ekleme ve uygulama listesi |
| 2. Mağaza kaydı | Metinler (diff, dry run, push), ekran görüntüsü yükleme akışı, build bağlama, Play edit'leri, AAB, canlı aktivite animasyonu | Sousial iPad seti storeasy'den yüklenir |
| 3. Stüdyo | Mockup şablonları, kendi çerçeveler, başlık lokalizasyonu, tüm boyutlara dışa aktarım, yükleme akışına bağlama | Bir uygulamanın tr ve en setleri stüdyodan üretilip yüklenir |
| 4. Olaylar | Webhook alıcısı, bildirimler (uygulama içi, e-posta), red takibi, Play yorum kutusu | Gerçek bir durum değişikliği bildirime düşer |
| 5. MCP | PAT, `/mcp`, bütün araçlar, `storeasy-mcp` köprüsü | Claude oturumu store-ops yerine storeasy ile Sousial işini yapar |
| 6. Yayın | Abonelik ve IAP ekranları, özel roller, web push, Google ile giriş, açık kaynak dosyaları, GitHub, CI, self-host dokümanı, barındırılan sürümün deploy'u | Buğra onayıyla public repo ve prod |

**MVP:** Faz 1 + Faz 2 + Faz 5. Bu üçüyle storeasy, store-ops'un yerini alır ve Milvasoft uygulamaları için günlük
kullanılır. Stüdyo (Faz 3) MVP'den hemen sonra, çünkü ürünü dışarıya anlatan özellik o.

## 8. Güvenlik

- Mağaza gizli değerleri: zarf şifreleme (K6), yalnız `StoreJobRunner` ve doğrulama komutu çözer, bellekte kısa süre.
- Log ve hata maskesi: bearer, JWT, PEM, `ya29.`, imzalı URL sorguları, `x-apple-signature`, `access_token` (store-ops
  `redact()` kuralları C#'a taşınır).
- PAT ve davet token'ları yalnız SHA-256 hash; görüntülemede yalnız önek.
- Webhook ucu anonim, HMAC zorunlu, `[RateLimit]`'li, gövde boyutu sınırlı.
- İncelemeye gönderme ve production yayını: ikinci onay adımı ve denetim kaydı.
- Organizasyon izolasyonu: her sorgu `OrganizationId` ile; entegrasyon testi "başka organizasyonun kaydı 404" kuralını
  her uç için doğrular.

## 9. Açık sorular

| Soru | Kime | Varsayılan (cevap gelene kadar) |
|---|---|---|
| Lisans (MIT, Apache-2.0, AGPL-3.0) | Buğra | Karar verilmeden `LICENSE` eklenmez. Öneri: AGPL-3.0 (barındırılan kopyaların değişiklikleri açık kalır); daha geniş benimseme istenirse Apache-2.0 |
| Domain | Buğra | Karar verildi: storeasy.milvasoft.com |
| Public GitHub reposu ve organizasyonu | Buğra | Repo yerel |
| Hosting, DB, Redis, S3, mail, Seq, CI | DevOpsçu | Cevaplandı, 10. bölüm |
| Barındırılan sürüm ücretli mi | Buğra | Kapsam dışı |

## 10. Altyapı varsayılanları (DevOpsçu, 18 Kasım 2026)

Kaynak: `serverstuff/docs/yeni-proje-azure-dokploy.md`. Para gerektiren veya paylaşılan prod'a dokunan her adım Buğra'nın
onayıyla.

| Konu | Varsayılan |
|---|---|
| Hosting | Dokploy, Oracle ARM (linux/arm64). Test hq-oci, prod ops-oci. Compose'ta yalnız `container-network` |
| Domain | Buğra kararı: `storeasy.milvasoft.com`, test `t-storeasy.milvasoft.com`. API aynı host'ta `/api`, SignalR `/hubs`, MCP `/mcp` (web konteynerindeki nginx API'ye yönlendirir). Gerekçe: Cloudflare'in ücretsiz sertifikası iki seviyeli alt alanı (`api.storeasy.milvasoft.com`) kapsamaz; tek origin CORS ve cookie sorununu da kaldırır. 07'deki `api.`/`app.` düzeninden sapma |
| PostgreSQL | Paylaşılan instance, kendi rolü ve `StoreasyDb` (prod'da `TEMPLATE template0`) |
| Redis | Paylaşılan instance, anahtar öneki `storeasy:` |
| Depolama | Onaylandı: `storeasy-s3-{test,prod}-bucket`, eu-central-1, herkese kapalı, CloudFront yok; okuma ve yazma presigned URL ile. Ayarlar `Storage__S3__Bucket`, `Region`, `AccessKey`, `SecretKey`, isteğe bağlı `ServiceUrl` (MinIO, ForcePathStyle). Kurulum betiği `serverstuff/docs/storeasy-s3-kurulum.sh`, henüz çalıştırılmadı |
| Mail | Notifike (Milvasoft barındırması), yanında SMTP uygulaması (self-host) |
| CI/CD | GitHub Actions: test ve PR kontrolleri. Azure DevOps: yalnız imaj derleme ve Dokploy deploy'u. Dallar `master`, `test`, `dev` |
| Sırlar | Azure DevOps variable group (`storeasy-test`, `storeasy-prod`); ana anahtar sürümlü (K6 zaten öyle), yedeği ayrı saklanır |
| Gözlemlenebilirlik | Seq (test `seq:5341`); OTel toplayıcı yok, adres boşsa exporter tamamen kapalı. Health: `/health/live`, `/health/ready` |
