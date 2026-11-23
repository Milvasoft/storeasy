# storeasy ürün planı

Son güncelleme: 18 Kasım 2026. Durum: Faz 1-8 yapıldı (kod tamam, sahte mağazalara karşı doğrulandı); gerçek mağazalarla
canlı doğrulama ve public yayın Buğra onayı bekliyor.

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
- E-posta ile kayıt, giriş, e-posta doğrulama, şifre sıfırlama. Google ile giriş Faz 8'de.
- Organizasyon: kullanıcı birden çok organizasyona üye olabilir; kenar çubuğunda organizasyon değiştirici.
- Organizasyon rolleri: `Owner`, `Admin`, `Editor`, `Viewer` (seed, kilitli) ve Faz 8'de özel roller. Matris 6. bölümde.
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
- Şablon uygulama başınadır; başlıklar dil başına çevrilir (elle veya Faz 6'da (stüdyo) yapay zeka önerisiyle, kill switch'li).
- Dışa aktarım her gerekli boyuta tek tıkla: Apple ekran tipleri (iPhone 6.9", 6.5", 6.1"/6.3" slotu yalnız 1206x2622
  ve 1179x2556 kabul eder, 1320x2868 reddedilir; iPad 13" 2064x2752), Play telefon, 7" ve 10" tablet, öne çıkan görsel
  1024x500. Alfa kanalı otomatik düzleştirilir.
- Üretilen setler doğrudan 2.2'deki yükleme akışına girer. MCP üzerinden de üretilir ("Sousial için tr ve en setini
  üret ve yükle").

### 2.5 Webhook ve bildirimler
- Her zaman açık alıcı: App Store Connect `APP_STORE_VERSION_APP_VERSION_STATE_UPDATED` (tekil yazım, eski çoğul yazım
  da kabul), `x-apple-signature: hmacsha256=<hex>` kontrolü, bağlantı başına ayrı gizli anahtar. Uç bağlantının her
  App Store uygulamasına bir Apple webhook'u kurar; build işleme (`BUILD_UPLOAD_STATE_UPDATED`) ve beta inceleme
  (`BUILD_BETA_DETAIL_EXTERNAL_BUILD_STATE_UPDATED`) olayları da kaydedilir. "Ping" Apple'dan imzalı test olayı ister.
- Google Play gerçek zamanlı geliştirici bildirimleri: Pub/Sub push aboneliği `/api/webhooks/google/{id}` adresine
  gelir; Google'ın imzaladığı push jetonu (audience = adres, e-posta = push hizmet hesabı) doğrulanır, ortak sır yok.
  Abonelik, tek seferlik ürün, iade (voided), bekleyen iade incelemesi ve test bildirimleri satın alma jetonları
  maskelenerek kaydedilir. Microsoft Partner Center'da mağaza uygulamaları için durum webhook'u yok; MsSync yoklaması.
- Bildirim kanalları: uygulama içi (bildirim merkezi), e-posta, web push (Faz 8). Kullanıcı başına tercih.
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
- Lisans AGPL-3.0-only; web arayüzünde kaynak kod bağlantısı (13. madde). Herkese açık repo, `CONTRIBUTING.md`, `SECURITY.md`, self-host dokümanı (tek `docker compose up`).
- Repoda hiçbir gizli değer ve Milvasoft'a özel veri (uygulama kimlikleri, iç hostlar, değişken grubu adları) olmaz;
  CI'da gizli değer taraması (gitleaks) zorunlu.
- Milvasoft'un kendi deploy dosyaları (Azure DevOps, Dokploy) ayrı, özel bir ops reposunda tutulur.

## 3. Kapsam dışı (şimdilik)
- Ödeme ve plan/paket (06 modülü): barındırılan sürüm ücretli olacaksa ayrı karar.
- Mağaza analitiği ve gelir raporları.
- Mobil uygulama.
- App Store, Google Play ve Microsoft Store dışındaki mağazalar: winget ve Chrome Web Store yapıldı (22 Kasım 2026); diğerleri sırayla, kanal başına bir PR (bölüm 7.1).
- App Store, Google Play ve Microsoft Store dışındaki mağazalar: winget ve Firefox Add-ons yapıldı (22 Kasım 2026); diğerleri sırayla, kanal başına bir PR (bölüm 7.1).

## 4. Mimari

### 4.1 Bileşenler

| Bileşen | Teknoloji | Görev |
|---|---|---|
| `storeasy-api` | .NET 10, Milvonion şablonu, PostgreSQL 17, Redis 7 | REST API, kimlik, organizasyon ve roller, mağaza motoru, iş kuyruğu, SignalR, webhook alıcısı, uzaktan MCP, mockup render |
| `storeasy-app` | React 18, Vite, React Query, Tailwind 3 (02 standardı) | Web arayüzü, canlı aktivite animasyonları, stüdyo editörü |
| Nesne depolama | S3 uyumlu (self-host'ta SeaweedFS) | Ham görüntüler, mockup çıktıları, AAB'ler (geçici) |
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
| K13 | Varsayılan kurulum tek takım (`Storeasy:Mode` = `SingleTeam`): API veritabanını kendisi hazırlar, ilk kullanıcı log'daki tek seferlik kurulum koduyla yöneticiyi ve takımı oluşturur, tek organizasyon, kayıt yok, davetle katılım. Çok kiracılı (`MultiTenant`) barındırma için isteğe bağlı | Açık kaynak kullanıcısı aracı kurup kullanmak ister, SaaS kurmak değil; ilk kurulumda geliştirici adımı ve kök kullanıcı karmaşası kalkar | K10'u varsayılan kurulumda geçersiz kılar |

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

İlke (Buğra, 18 Kasım 2026): "Hiçbir şey eksik olmamalı." Claude oturumlarının bu haftalarda tarayıcıda elle yaptığı her
mağaza işi storeasy'de ve MCP'de olmalı. API'si olmayan iş de storeasy'nin içinde kalır: asistanlı adım olarak
(bölüm 11.4). Fazlar kapsam matrisinin (bölüm 11) satırlarını sırayla kapatır.

| Faz | İçerik | Bitti sayılır | Durum |
|---|---|---|---|
| 1. Temel | Repo, API, kayıt ve giriş, organizasyon, roller, davet, şifreli bağlantılar, denetim kaydı (yapıldı); `StoreJob` kuyruğu, SignalR olayları, S3 (SeaweedFS yerelde, presigned), mağazadan uygulama ve build senkronu, kural kataloğu v1 (bölüm 12); web: tasarım, kabuk, giriş, uygulamalar, bağlantılar, üyeler, denetim kaydı | Yerelde bağlantı ekleme, uygulamaların iki mağazadan gelmesi | Yapıldı: kod tamam, sahte mağazalara karşı doğrulandı; canlı doğrulama bekliyor |
| 2. Hazırlık ve içerik | Hazırlık kontrolü (11.1) ve uygulama başına kayıtlı cevaplar; metinler ve kategoriler, yaş derecelendirmesi (yeni sorular dahil), fiyat ve bölgeler, ekran görüntüsü ve önizleme setleri (kataloğa göre zorunlu, yedekle karşılanan, eksik), build izleme ve bağlama, ihracat uyumu, inceleme bilgileri, yayın türü ve aşamalı yayın, incelemeye gönderme (onaylı); Play: uygulama ayrıntıları, listeler, görseller, track ve sürüm, aşamalı yayın, ülkeler, test kullanıcıları, AAB, Data safety; canlı aktivite görünümü | Sousial ve Crewhall için "eksik ne var" tek çağrıda doğru cevap; Sousial iPad seti storeasy'den yüklenir | Yapıldı: kod tamam, sahte mağazalara karşı doğrulandı; canlı doğrulama bekliyor |
| 3. MCP ve paket alma | PAT, `/mcp`, CI'dan paket alma (bölüm 11.8: OIDC güvenilir yayıncı, CI token'ı, CLI, GitHub Action, Azure DevOps görevi, EAS webhook'u), matristeki her API satırı için araç (yazmalar dry run + onay), tüm uygulamaların özeti tek çağrı, `storeasy-mcp` yerel köprüsü (yerel dosya yolları ve klasörler) | Bir Claude oturumu store-ops yerine storeasy ile Sousial işini baştan sona yapar; bir GitHub Actions çalışması sır saklamadan AAB'yi internal track'e gönderir | Yapıldı: kod tamam, sahte mağazalara karşı doğrulandı; canlı doğrulama bekliyor |
| 4. Olaylar ve takip | Webhook alıcısı, bildirimler, red akışı (11.3), yorum kutuları (Apple ve Play, yanıt), TestFlight (gruplar, test kullanıcıları, beta metinleri, beta inceleme, build bitiş tarihleri), kural izleyici ve uygulama başına uyum zaman çizelgesi (bölüm 12) | Gerçek bir red bildirime ve red kaydına düşer; Expenny ve Perry'nin TestFlight bitiş tarihleri görünür | Yapıldı: kod tamam, sahte mağazalara karşı doğrulandı; canlı doğrulama bekliyor |
| 5. Microsoft Store | Üçüncü mağaza (bölüm 11.6): Entra bağlantısı, uygulama ve gönderim senkronu, listeler ve görseller, MSIX paketleri ve paket uçuşları, aşamalı yayın, add-on'lar; MSI/EXE uygulamalar için ayrı API; sertifikasyon durumu; kataloğa Microsoft kuralları | Bir Windows uygulamasının liste ve paket güncellemesi storeasy'den gönderilir | Yapıldı: kod tamam, sahte mağazalara karşı doğrulandı; canlı doğrulama bekliyor |
| 6. Stüdyo | Mockup şablonları, kendi çizdiğimiz çerçeveler (iPhone Duo dış ve iç ekran dahil), başlık lokalizasyonu, kataloğa göre tüm zorunlu ve önerilen boyutlara tek seferde dışa aktarım, yükleme akışına bağlama | Bir uygulamanın tr ve en setleri stüdyodan üretilip yüklenir | Yapıldı: kod tamam, sahte mağazalara karşı doğrulandı; canlı doğrulama bekliyor |
| 7. Para | Abonelik grupları, abonelikler, bölge fiyatları, tanıtım ve promosyon teklifleri, retention mesajları, IAP; Play abonelikleri, base plan'lar, teklifler, tek seferlik ürünler | Bir uygulamanın abonelikleri storeasy'den kurulur | Yapıldı: kod tamam, sahte mağazalara karşı doğrulandı; canlı doğrulama bekliyor |
| 8. Yayın | Asistanlı adımların tamamı (11.4), özel roller, web push, Google ile giriş, açık kaynak dosyaları, GitHub, CI, self-host dokümanı, barındırılan sürümün deploy'u | Buğra onayıyla public repo ve prod | Kod yapıldı (özel roller, web push, Google ile giriş, asistanlı adımlar, self-host dokümanı); public repo ve prod Buğra onayı bekliyor |

**MVP yok (Buğra, 18 Kasım 2026):** Ürün bütün özellikleriyle tek seferde yapılır. Fazlar artık yalnız yapım
sırasıdır; her faz bitince kod çalışır durumdadır ama hiçbir faz "ilk sürüm" diye ayrı yayına çıkmaz. Yayın (Faz 8)
bütün fazlar bitince, Buğra'nın onayıyla yapılır. store-ops, Faz 3 bittiğinde storeasy'nin MCP'si yerine geçince
emekli olur.

Microsoft Store Buğra'nın kararıyla (18 Kasım 2026) öne alındı: Faz 4'ün hemen arkasında. Hazırlık: `StoreKind` enum'u, `IStoreGateway` ve kural kataloğu baştan üç
mağazaya açık tasarlanır; Faz 1-3'te Apple ve Google'a özel varsayım koda gömülmez.

### 7.1 Planlanan mağazalar (şimdilik yapılmıyor)

Buğra'nın kararı (19 Kasım 2026): aşağıdaki dağıtım kanalları plana alındı ama şimdilik eklenmiyor. Sıra önerisi
yukarıdan aşağıya. Hazırlık ilkesi Microsoft Store'daki gibidir: `StoreKind`, `IStoreGateway` ve kural kataloğu yeni
kanallara açık kalır, mevcut üç mağazaya özel varsayım koda gömülmez.

| Kanal | Neden storeasy'ye uyar | Not |
|---|---|---|
| winget (`microsoft/winget-pkgs`) | MSI/EXE ürünleri zaten storeasy'de; aynı paketin winget manifesti | **Yapıldı (22 Kasım 2026).** GitHub token'ı ve fork bağlantısı, paket ve manifest okuma, yeni sürüm ve dil metni planı, onaylanınca fork'a dal ve winget-pkgs'e PR, PR'ın etiket ve denetimlerinden inceleme durumu, kurallar; ayrıntı [connecting-stores.md](connecting-stores.md#winget). Diğer kanallar [adding-a-store.md](adding-a-store.md)'deki genişleme noktalarını izler |
| Chrome Web Store | Mağaza sayfası, ekran görüntüleri, inceleme süreci, yorumlar, sık değişen politika son tarihleri | **Yapıldı (22 Kasım 2026).** Chrome Web Store API v2: hizmet hesabı veya OAuth istemcisi bağlantısı, uzantı kimliğiyle öğe ekleme, durum okuma, paket alımında ZIP (Manifest V3 ve sürüm denetimi), gönderim, bekletilen sürümü yayınlama, dağıtım yüzdesi, gönderim iptali ve geri alma planları, yorumlar ve metrikler, kurallar. Mağaza sayfası, gizlilik uygulamaları ve görünürlük için API yok, yardımlı adımdır; ayrıntı [connecting-stores.md](connecting-stores.md#chrome-web-store) |
| Microsoft Edge Add-ons | Chrome eklentisiyle aynı paket, ayrı mağaza | Partner Center Edge API |
| Firefox Add-ons (AMO) | Mağaza sayfası, inceleme, yorumlar | **Yapıldı (22 Kasım 2026).** AMO API v5 anahtarı (JWT issuer ve secret) bağlantısı, hesabın eklentilerini listeleme, dillere göre mağaza sayfası, önizleme ve simge planı, intake'ten gelen .xpi ile listelenen veya listelenmeyen sürüm, sürüm dosyasının inceleme durumu, değerlendirmeler ve geliştirici yanıtı, kurallar; istatistik API'si yok. Ayrıntı [connecting-stores.md](connecting-stores.md#firefox-add-ons) |
| Flathub | Linux uygulama mağazası, mağaza sayfası ve ekran görüntüleri | Gönderim GitHub deposu ve PR üzerinden |
| Snap Store | Linux uygulama mağazası, kanallar (stable, beta, edge) | snapcraft ve Snap Store API |
| Homebrew | macOS ve Linux paket dağıtımı | Cask ve formül PR'ı; mağaza sayfası yok, en düşük öncelik |

Kütüphane depoları (npm, NuGet, PyPI ve benzerleri) bu listede değildir: mağaza sayfası, ekran görüntüsü ve inceleme
süreçleri yoktur. Onlar ayrı bir modüldür (bölüm 7.2).

### 7.2 Paket depoları modülü (faz 1 ve 2 yapıldı)

Buğra'nın kararı (19 Kasım 2026): kütüphane depoları mağaza modelinin içine karışmadan, ayrı ve kapatılabilir bir modül
olarak eklenecek. Öncelik bölüm 7.1'deki winget ve tarayıcı eklenti mağazalarından sonradır.

İlke (Buğra'nın kararı, 23 Kasım 2026): modül yalnız izlemekle kalmaz; storeasy'den yayın, toplu sürüm yönetimi, paket
sayfası yönetimi ve indirme ile kullanım bilgisi de verir. Yayının kendisi mevcut CI'da kalır: storeasy CI'ı tetikler
(etiket gönderir veya iş akışını başlatır), CI OIDC ile yayımlar; yayın için depo token'ı tutulmaz. Depoda yazma işlemleri
(kullanımdan kaldırma, listeden çıkarma, dist-tag, yank) için storeasy depo API anahtarlarını mağaza bağlantı sırlarıyla
aynı zarf şifrelemesiyle saklayabilir; bu işlemler mağaza yazmaları gibi her zaman plan ve onayla yapılır.

| Kapsam | Ayrıntı |
|---|---|
| Depolar | npm, NuGet, PyPI, crates.io; sonra Maven Central, RubyGems, Packagist, pub.dev |
| Paket özeti | Her paketin son sürümü, son yayın tarihi, hangi depodan ve hangi CI çalışmasından geldiği, tek ekranda |
| Trusted publishing kurulumu | Her depo için adım adım sihirbaz (depo ayarı, CI dosyası, doğrulama); storeasy'nin kendi npm ve Marketplace kurulumunun tekrarlanabilir hâli |
| Uyarılar | Süresi dolan token'lar, provenance'ı olmayan yayınlar, kullanımdan kaldırılan (deprecated) veya geri çekilen (yanked) sürümler, güvenlik bildirimleri |
| Geçmiş | Yayın geçmişi, denetim kaydı ve bildirimlerle |
| MCP | Okuma araçları (paket özeti, uyarılar); yazma yalnız onaylı ve dry run ile |

**Faz 1 yapıldı (23 Kasım 2026).** npm, NuGet, PyPI ve crates.io paketleri yalnız herkese açık API'lerden okunur,
token saklanmaz. Paket özeti (son sürüm, yayın tarihi, provenance'tan CI çalışması), sürüm listesi, uyarılar
(kullanımdan kaldırılan ve geri çekilen sürüm, provenance'ı olmayan son sürüm, OSV.dev güvenlik bildirimi; NuGet'te
provenance olmadığı için uyarı yok), altı saatlik senkron ve "şimdi senkronla", denetim kaydı ve bildirimler, MCP
araçları (`registries_*`), web sayfası ve `Registries` özellik anahtarıyla kapatma. Ayrıntı
[package-registries.md](package-registries.md). Trusted publishing kurulum sihirbazı, süresi dolan token uyarıları
(depo kimlik bilgisi gerektirir) ve Maven Central, RubyGems, Packagist, pub.dev sonraya kaldı.

**Faz 2 yapıldı (23 Kasım 2026).** Paket başına trusted publishing sihirbazı (npm, PyPI, crates.io, NuGet, RubyGems,
pub.dev): depo ayarlarına girilecek değerler ve ayar sayfası bağlantısı, GitHub Actions (npm ve PyPI için GitLab CI de)
için kopyalanacak OIDC iş akışı, son yayının provenance'ını kayıtlı kurulumla karşılaştıran kontrol ve eşleşmezse
`PublisherMismatch` uyarısı. Maven Central ve Packagist'te trusted publishing yok, arayüz bunu söyler. Token hatırlatıcıları
token saklamadan: ad, depo, kullanıldığı yer ve bitiş tarihi; 14 ve 3 gün önce ve o gün `TokenExpiring` uyarısı, denetim
kaydı ve bildirim; token'a benzeyen değer reddedilir. Yeni depolar (yalnız okuma): Maven Central, RubyGems (sigstore
attestation ile provenance), Packagist (abandoned kullanımdan kaldırma sayılır), pub.dev (discontinued ve retracted).
MCP araçları `registries_setup_*` ve `registries_reminder*`. storeasy depoya veya repoya hiçbir şey yazmaz.

**Faz 3, ilk bölüm yapıldı (23 Kasım 2026).** İndirme ve kullanım: paket başına toplam, son dönem, sürüm başına ve
günlük seri (deponun günlük sayısı yoksa toplamın günlük kaydı tutulur; Maven Central indirme yayımlamaz), özet tabloda
sıralanabilir indirme sütunu. Paket grupları: üyelerin son sürümleri, grubun en yüksek sürümünün gerisinde kalanlar,
aynı repodan gelen paketlerden grup önerisi. Paket sayfası: depodaki açıklama, README (metin olarak), anahtar
kelimeler, lisans, bağlantılar ve hazırlık kontrolleri; bu bilgiler bir sonraki yayınla değişir (Packagist'te repodan).
Paket sayfasında sekmeler, MCP okuma araçları. Kalanlar: GitHub bağlantısı ve CI üzerinden yayın planı (etiket veya
workflow_dispatch, çalışmanın takibi), şifreli depo anahtarlarıyla yazma işlemleri, bağımlı paket sayısı (deps.dev v3'te
belgelenmiş uç yok).

Kapsam dışı: paket derleme, sürüm numarası belirleme ve yayını storeasy'nin kendisinin yapması.

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
| Lisans | Buğra | Karar verildi (18 Kasım 2026): AGPL-3.0-only. Web arayüzünün altbilgisinde kaynak kod bağlantısı olacak (lisansın 13. maddesi) |
| Domain | Buğra | Karar verildi: storeasy.milvasoft.com |
| Public GitHub reposu ve organizasyonu | Buğra | Repo yerel |
| Hosting, DB, Redis, S3, mail, Seq, CI | DevOpsçu | Cevaplandı, 10. bölüm |
| Barındırılan sürüm ücretli mi | Buğra | Kapsam dışı |
| MVP | Buğra | Kaldırıldı (18 Kasım 2026): bütün ürün tek seferde, bölüm 7 |
| Logo ve vurgu rengi | Buğra | Karar verildi (18 Kasım 2026): logo K3 "Dört kanat e" (rüzgârgülünün bir kanadı eksik hâli, boşluk "e"nin ağzı; `docs/storeasy uygulaması tasarımı/03-Logo-Onerileri-4.html`), vurgu Ultramarin #4636FF / #857AFF. Yazı logoda ortadaki "e" vurgu renginde |
| Microsoft Store | Buğra | Eklendi ve öne alındı (18 Kasım 2026): Faz 5, bölüm 11.6 |
| Yeni mağazalar ve kütüphane depoları | Buğra | Karar verildi (19 Kasım 2026): ikisi de plana alındı, şimdilik yapılmıyor; mağazalar bölüm 7.1, paket depoları ayrı modül bölüm 7.2 |

## 10. Altyapı varsayılanları (DevOpsçu, 18 Kasım 2026)

Kaynak: `serverstuff/docs/yeni-proje-azure-dokploy.md`. Para gerektiren veya paylaşılan prod'a dokunan her adım Buğra'nın
onayıyla.

| Konu | Varsayılan |
|---|---|
| Hosting | Dokploy, Oracle ARM (linux/arm64). Test hq-oci, prod ops-oci. Compose'ta yalnız `container-network` |
| Domain | Buğra kararı: `storeasy.milvasoft.com`, test `t-storeasy.milvasoft.com`. API aynı host'ta `/api`, SignalR `/hubs`, MCP `/mcp` (web konteynerindeki nginx API'ye yönlendirir). Gerekçe: Cloudflare'in ücretsiz sertifikası iki seviyeli alt alanı (`api.storeasy.milvasoft.com`) kapsamaz; tek origin CORS ve cookie sorununu da kaldırır. 07'deki `api.`/`app.` düzeninden sapma |
| PostgreSQL | Paylaşılan instance, kendi rolü ve `StoreasyDb` (prod'da `TEMPLATE template0`) |
| Redis | Paylaşılan instance, anahtar öneki `storeasy:` |
| Depolama | Onaylandı: `storeasy-s3-{test,prod}-bucket`, eu-central-1, herkese kapalı, CloudFront yok; okuma ve yazma presigned URL ile. Ayarlar `Storage__S3__Bucket`, `Region`, `AccessKey`, `SecretKey`, isteğe bağlı `ServiceUrl` (SeaweedFS, ForcePathStyle). Bucket'lar kuruldu (SSE-S3, CORS doğrulandı); erişim anahtarları deploy zamanı |
| Mail | Notifike (Milvasoft barındırması), yanında SMTP uygulaması (self-host) |
| CI/CD | GitHub Actions: test ve PR kontrolleri. Azure DevOps: yalnız imaj derleme ve Dokploy deploy'u. Dallar `master`, `test`, `dev` |
| Sırlar | Azure DevOps variable group (`storeasy-test`, `storeasy-prod`); ana anahtar sürümlü (K6 zaten öyle), yedeği ayrı saklanır |
| Gözlemlenebilirlik | Seq (test `seq:5341`); OTel toplayıcı yok, adres boşsa exporter tamamen kapalı. Health: `/health/live`, `/health/ready` |

## 11. Kapsam matrisi

Kaynak: Claude oturumlarının Kasım 2026'ya kadar Dump, Fenpo, Expenny, Perry, SaySay, Crewhall ve Sousial için
yaptığı işler. Sütunlar: **API** (resmi API ile otomatik), **Asistanlı** (API yok; storeasy kontrol listesi,
kayıtlı cevaplar ve tarayıcı devri ile yönetir, sonucu kaydeder), **Yok** (storeasy yapamaz, yalnız bilgi ve
bağlantı verir). Faz sütunu bölüm 7'deki fazdır. Kontrol tarihi: 18 Kasım 2026.

### 11.1 Ortak

| İş | Tür | Faz | Not |
|---|---|---|---|
| Hazırlık kontrolü: uygulama ve mağaza başına "gönderimden önce eksik ne" | API + katalog | 2 | Metinler, kataloğa göre zorunlu ekran setleri (iPad yalnız tablet destekliyse), build bağlı ve VALID, ihracat uyumu, yaş derecelendirmesi, gizlilik (asistanlı adım tamam mı), fiyat ve bölge, abonelik inceleme görüntüleri, inceleme iletişimi ve demo hesap, sözleşmeler (asistanlı) |
| Tüm uygulamaların özeti (iki mağaza, durum, eksikler) | API | 2-3 | MCP'de tek çağrı |
| Uygulama başına kayıtlı cevaplar | Veri | 2 | Gizlilik etiketleri, Data safety, içerik derecelendirmesi, hedef kitle, inceleme için giriş bilgileri, inceleme notları. Uygulamalar arası kopyalanabilir. Demo hesap şifresi şifreli saklanır, yalnız App Review alanına gider |
| Red akışı | Webhook + asistanlı | 4 | Bölüm 11.3 |
| Asistanlı adımlar | Asistanlı | 2-7 | Bölüm 11.4 |
| Birden çok geliştirici hesabı ve organizasyon | API | 1 | Organizasyon başına birden çok bağlantı (yapıldı) |
| EAS'ten gelen build'leri görme, işlenmeyi bekleme, sürüme bağlama | API | 2 | Build senkronu + `StoreJob` ile bekleme |

### 11.2 App Store Connect

| İş | Tür | Faz | Not |
|---|---|---|---|
| Uygulama oluşturma | Asistanlı | 8 | API uygulama oluşturmaya izin vermiyor |
| App info lokalizasyonları (ad, alt başlık, gizlilik URL'leri) | API | 2 | |
| Sürüm lokalizasyonları (açıklama, EULA ve otomatik yenileme metni dahil, anahtar kelimeler, tanıtım metni, destek ve pazarlama URL'si, yenilikler) | API | 2 | en-US app info oluşunca sürüm lokalizasyonu da oluşur: POST yerine PATCH (store-ops'ta çözüldü) |
| Kategoriler, telif, içerik hakları beyanı | API | 2 | |
| Yaş derecelendirmesi (yeni sorular dahil) | API + asistanlı | 2 | Shared Location ve Dating and Relationships soruları API'de yok (18 Kasım 2026: belge ve 7 uygulamada canlı GET); Apple ekleyene kadar asistanlı. Yeni descriptor'lar: Shared Location ve Dating and Relationships (18+), App Store Connect'te 16 Kasım 2026'dan beri, bütün uygulamalar için son tarih 12 Ocak 2027. Brezilya'da A16 ve A18. API alan adları canlıda doğrulanacak |
| Fiyat ve bölgeler | API | 2 | Fiyat takvimi, bölge seçimi (ör. Çin hariç) |
| App Privacy etiketleri | Asistanlı | 2 | Resmi API'de karşılığı bulunamadı (18 Kasım 2026); kayıtlı cevaplar + tarayıcı devri. API gelirse matris güncellenir |
| App Compliance sayfası (ICP, oyun lisansı, tıbbi cihaz, Çin AIGC ve Security Assessment) | Asistanlı | 4 | 16 Kasım 2026'da yeni sayfaya taşındı. Çin'de dağıtılan her uygulama 31 Ocak 2027'ye kadar beyanları tamamlamalı (yeni uygulamalar hemen). Account Holder veya Admin yapar |
| Ekran görüntüsü setleri, sıralama, silme | API | 2 | Katalog: zorunlu, yedekle karşılanan ve eksik slotlar uygulamanın cihaz ailesine ve SDK'sına göre |
| Otomatik boyutlandırma ve alfa temizleme | Uygulama içi | 2 | Yüklemeden önce |
| App Preview (video) | API | 2 | Boyutlar katalogda |
| iPhone Duo ekran görüntüleri (dış 1398x2034, iç 2007x2853) | API | 2 | Bölüm 12.2 |
| Build listesi, işlenme durumu, sürüme bağlama | API | 2 | |
| İhracat uyumu (usesNonExemptEncryption) | API | 2 | |
| İnceleme bilgileri (iletişim, demo hesap, notlar, ek) | API | 2 | |
| Yayın türü, aşamalı yayın, elle yayın | API | 2 | |
| İncelemeye gönderme (ilk abonelikler sürümle birlikte) | API | 2 | Onay adımı + `SubmitForReview` izni |
| TestFlight: gruplar, test kullanıcıları, beta lokalizasyonları, "neyi test et", beta inceleme | API | 4 | |
| TestFlight build bitiş tarihi (90 gün) | API | 4 | Expenny ve Perry yalnız TestFlight'ta; bitişten önce uyarı |
| Abonelik grupları ve lokalizasyonları, abonelik oluşturma | API | 7 | |
| Bölge fiyatları, tanıtım ve promosyon teklifleri, uygunluk, inceleme görüntüsü | API | 7 | İnceleme görüntüsü Faz 2'de (hazırlık kontrolü ister) |
| Retention mesajları, çok koltuklu satın alma | API (doğrulanacak) | 7 | Abonelik ekranlarında yeni alanlar |
| IAP | API | 7 | |
| Müşteri yorumları ve yanıt | API | 4 | |
| Sözleşme durumu (Paid Apps Agreement) | Asistanlı | 4 | API'de yok; hazırlık kontrolünde "son kontrol tarihi" ile |
| App Store Server Notifications URL, sandbox test kullanıcıları | Asistanlı / API | 7 | Sandbox test kullanıcıları API'de listelenir ve güncellenir |
| Hızlandırılmış inceleme talebi | Asistanlı | 4 | Yalnız web formu |

### 11.3 Red akışı

1. Webhook `APP_STORE_VERSION_APP_VERSION_STATE_UPDATED` (veya periyodik senkron) sürümü `REJECTED` / `METADATA_REJECTED` görür.
2. Red kaydı açılır, sahiplere bildirim gider.
3. Red metni API'de yok: kullanıcı yapıştırır ya da asistanlı tarayıcı adımı (Claude in Chrome) Resolution Center'dan okuyup
   storeasy'ye yazar.
4. Guideline numaraları ayrıştırılır, kontrol listesine dönüşür; Claude MCP ile yanıt taslağı hazırlar (Resolution Center
   sınırı 4000 karakter, taslakta sayaç).
5. Yanıt elle veya asistanlı adımla gönderilir; düzeltmeler storeasy üzerinden yapılır, yeniden gönderim onaylıdır.
6. Uygulama başına red geçmişi kalır (ör. Dump: 3.1.2, sonra 4.3 + 4.2.6; Fenpo: 4.3, sonra alt başlıkta "iPhone" için 5.2.5).

### 11.4 Asistanlı adım

API'si olmayan iş storeasy'de bir **asistanlı adım** kaydıdır:

- Kontrol listesi (adımlar, beklenen sonuç), ilgili mağaza sayfasının doğrudan bağlantısı ve uygulamanın kayıtlı cevapları.
- Tarayıcı devri: MCP aracı adımı ve cevapları yapılandırılmış olarak verir; Claude in Chrome kullanıcının kendi oturumunda
  uygular, sonucu (yapıldı, ekran görüntüsü, tarih) storeasy'ye kaydeder. Kullanıcı aynı listeyi kendisi de izleyebilir.
- storeasy ve Claude hiçbir zaman kullanıcı şifresi saklamaz ya da girmez; mağaza oturumu kullanıcınındır.
- Bilinen tuzaklar adıma not olarak yazılır (ör. Play iletişim kutusunda "Ekle"den sonra kaydetmeden sayfayı yenileme).

### 11.5 Google Play

| İş | Tür | Faz | Not |
|---|---|---|---|
| Uygulama oluşturma | Asistanlı | 8 | API yok |
| Uygulama ayrıntıları (iletişim e-postası, telefon, web sitesi, varsayılan dil) | API | 2 | `edits.details` |
| Uygulama kategorisi ve etiketler | Asistanlı | 8 | `edits.details`'te kategori alanı yok; Play Console'da |
| Dil başına mağaza listesi | API | 2 | |
| Grafikler: ikon, öne çıkan görsel, telefon ve 7"/10" tablet ekranları | API | 2 | |
| Track ve sürümler (internal, closed, open, production), sürüm notları, aşamalı yayın, ülke seçimi | API | 2 | Production yalnız `ReleaseProduction` izniyle |
| Test kullanıcı listeleri | API | 2 | `edits.testers` (Google grupları) |
| EAS çıktısından AAB yükleme | API | 2 | |
| Data safety | API | 2 | `applications.dataSafety` CSV alır; kayıtlı cevaplardan CSV üretilir |
| Abonelikler, base plan'lar, teklifler, tek seferlik ürünler | API | 7 | |
| Yorumlar ve yanıt | API | 4 | API yalnız yakın tarihli, metinli yorumları döndürür; storeasy saklar |
| Vitals (Reporting API) | API | 8 | İsteğe bağlı |
| İçerik derecelendirme anketi, hedef kitle, reklam, devlet, finans ve sağlık beyanları | Asistanlı | 8 | Kayıtlı cevaplar |
| Foreground service ve full-screen intent beyanları (demo videolu) | Asistanlı | 8 | |
| Politika durumu | Asistanlı | 4 | |
| Android geliştirici doğrulaması (paket adı kaydı) | Asistanlı | 4 | Play Console'da uygulama kaydı; kayıtsız uygulama Play'den kaldırılabilir. Gerekli hesap izni hazırlık kontrolünde |
| Hedef API seviyesi | Build verisi + katalog | 2 | Yeni uygulamalar ve güncellemeler 31 Ağustos 2026'dan beri API 36; mevcut uygulamalar yeni kullanıcılara görünmek için API 35; uzatma 1 Kasım 2026'ya kadardı |

### 11.6 Microsoft Store

Kaynak: Microsoft Learn, 18 Kasım 2026'da kontrol edildi. İki ayrı API var:

- **MSIX ve UWP uygulamaları:** `manage.devcenter.microsoft.com` (Microsoft Store submission API). Uygulama gönderimleri,
  add-on'lar ve paket uçuşları (flight).
- **MSI ve EXE uygulamaları:** `api.store.microsoft.com/submission/v1`. Liste, fiyat ve pazar, görseller, paketler (yükleyici
  URL'si; storeasy dosyayı barındırmaz, yükleyici geliştiricinin sunucusunda durur), hazır olma kontrolü, gönderim ve
  yayın durumu.

Bağlantı: Microsoft Entra uygulaması (Partner Center'da Manager rolü). Gizli değerler tenant ID, client ID, client secret ve
satıcı kimliği (`X-Seller-Account-Id`); diğer mağazalar gibi zarf şifrelemeyle saklanır. Client secret en çok 24 ay
geçerli, Microsoft 12 ayı öneriyor: bitiş tarihi bağlantıda tutulur, kural izleyici 30 gün önce uyarır.

| İş | Tür | Faz | Not |
|---|---|---|---|
| Bağlantı ekleme ve doğrulama (token alma, uygulama listesi) | API | 5 | |
| Uygulama oluşturma (isim ayırma) | Asistanlı | 5 | API uygulama oluşturamaz |
| İlk gönderim ve yaş derecelendirmesi anketi (IARC) | Asistanlı | 5 | API ancak Partner Center'daki ilk gönderimden sonra çalışır |
| Uygulama ve gönderim senkronu, durum | API | 5 | MSI/EXE'de `publishingStatus`: INPROGRESS, PUBLISHED, FAILED |
| Liste metinleri (açıklama, kısa açıklama, yenilikler, özellikler, arama terimleri) ve diller | API | 5 | Sınırlar: açıklama 10.000, kısa 1.000, yenilikler 1.500 karakter; en çok 20 özellik, 7 arama terimi |
| Ekran görüntüleri ve logolar | API | 5 | Dil başına en çok 10 ekran görüntüsü; stüdyo boyutları kataloğa eklenir |
| Fiyat, pazarlar, keşfedilebilirlik | API | 5 | "Pricing Version 2"deki uygulamalarda fiyat API'den okunamıyor; o modül asistanlı |
| MSIX paket yükleme, paket uçuşları, aşamalı yayın | API | 5 | |
| MSI/EXE paket güncelleme (URL, mimari, sessiz kurulum parametreleri) | API | 5 | Yükleyici Authenticode ile imzalı olmalı; hazırlık kontrolü bakar |
| Add-on'lar (uygulama içi ürünler) | API | 5 | Store-managed consumable ve zorunlu güncelleme kullanan ürünlerde API 409 döner; o durumda asistanlı |
| Sertifikasyon sonucu ve red | API + asistanlı | 5 | Durum API'den; ayrıntılı rapor Partner Center'da |
| Yorumlar ve yanıt | Asistanlı | 5 | Yanıt API'si Microsoft'un kendi sayfasında "çalışmıyor" olarak işaretli; yorumları okumak için analytics API denenecek |

Kural: API ile oluşturulan bir gönderim Partner Center'da elle değiştirilirse API onu bir daha değiştiremez. storeasy
bunu gönderim ekranında uyarı olarak gösterir. Aynı anda tek bir aktif gönderim olabilir.

### 11.7 MCP

- Matristeki her API ve asistanlı satır bir araçtır; yazmalar dry run ve onay kapılı.
- Tek çağrıda tüm uygulamaların özeti (iki mağaza, durum, eksikler).
- Araçlar yerel dosya yolu ve klasör kabul eder. Uzaktan MCP dosyayı göremez; yerel `storeasy-mcp` köprüsü dosyaları presigned
  URL ile S3'e yükleyip aracı varlık kimlikleriyle çağırır. Köprü Faz 3'te.

### 11.8 Paket alma (CI ve build servisleri)

İstek (Buğra, 18 Kasım 2026): build pipeline'ları paketi doğrudan storeasy'ye versin, storeasy mağazaya götürsün. Sektörde
yerleşik yol, Firebase App Distribution, Sentry ve Codemagic'teki gibi "CLI + CI eklentisi + REST" ve kimlik için OIDC ile
güvenilir yayıncıdır (PyPI ve npm trusted publishing): CI kendi kısa ömürlü OIDC token'ını gösterir, kalıcı sır saklanmaz.

Giriş yolları:

| Yol | Nasıl | Not |
|---|---|---|
| Push: REST | `POST /api/v1/apps/{appId}/artifacts` yükleme oturumu açar, dosya S3'e presigned multipart ile gider, `complete` ile kapanır | Kaldığı yerden devam eder; aynı SHA-256 ikinci kez işlenmez |
| Push: CLI | `storeasy upload <dosya> --app <id>` (npm paketi, `storeasy-mcp` ile aynı paket) | Yerel makine ve her CI'da çalışır |
| Push: GitHub Action | `storeasy/upload-action` | OIDC ile, `id-token: write` izniyle; sır gerekmez |
| Push: Azure DevOps görevi | `StoreasyUpload@1` | OIDC (workload identity) veya CI token'ı |
| Pull: EAS webhook'u | EAS build bitince `artifacts.buildUrl` ile storeasy'ye haber verir, storeasy indirir | `expo-signature` (HMAC-SHA1) doğrulanır; indirme yalnız izinli alan adlarından (SSRF koruması) |
| Pull: genel webhook | Codemagic, Bitrise ve diğerleri için imzalı "artifact hazır" çağrısı (HMAC-SHA256, storeasy'nin kendi şeması) | Aynı izinli alan adı listesi |
| Elle | Web arayüzünde sürükle bırak, MCP'de yerel dosya yolu | |
| Sonra (Faz 8) | fastlane eklentisi | |

Kimlik:

- **OIDC güvenilir yayıncı (önerilen):** Uygulama başına güven kuralı: sağlayıcı (GitHub, Azure DevOps, GitLab), repo veya
  proje, dal ve ortam. CI'ın token'ı (audience `storeasy`) doğrulanıp 15 dakikalık yükleme token'ına çevrilir. Azure DevOps
  token'ında dal bilgisi olmadığı için orada kısıt service connection ve ortam onaylarıyla kurulur.
- **CI token'ı:** Organizasyona bağlı, uygulama ve izin kapsamlı (`artifacts:write`), süreli, `sci_` önekli; yalnız SHA-256
  hash saklanır. PAT'tan ayrıdır, bir kişiye bağlı değildir.

İşleme:

1. Dosya S3'e iner (şifreli, saklama süresi organizasyon ayarı, varsayılan 30 gün).
2. Paket okunur: IPA'dan bundle id, sürüm ve build numarası, minimum iOS; AAB'den paket adı, versionCode, targetSdk;
   MSIX'ten kimlik ve sürüm. Uygulamayla eşleşmeyen paket reddedilir.
3. Hazırlık kontrolü kataloğa göre çalışır: hedef API seviyesi, versionCode'un öncekinden büyük olması, SDK ve Xcode
   alt sınırları, imza.
4. Uygulamanın teslim kuralı uygulanır: ör. "iOS → TestFlight iç grup", "Android → internal track", "Windows → paket
   uçuşu". Production ve incelemeye gönderme hiçbir zaman otomatik değildir; onay adımı ve izin ister.
5. Mağazaya teslim:
   - **Apple:** App Store Connect API'nin build upload kaynakları (`buildUploads`, `buildUploadFiles`, parçalı yükleme).
     Uçtan uca çalıştığı Faz 3'te gerçek bir build ile doğrulanacak. Çalışmazsa yedek yol: CI paketi Apple'a kendisi
     yükler (EAS Submit, Xcode Cloud), storeasy build'i senkronla görüp aynı teslim kuralını uygular.
   - **Google:** `edits.bundles.upload` ve track ataması.
   - **Microsoft:** MSIX paket yükleme; MSI ve EXE'de yükleyici URL'si güncellenir (Faz 5).
6. Her adım canlı aktivite görünümünde akar ve denetim kaydına düşer; sonuç CI'a çıkış kodu ve özet olarak döner.

## 12. Mağaza kuralları kataloğu ve izleyici

### 12.1 Kurallar veri olarak

Mağaza kuralları kodda değil, sürümlü bir katalogda durur (`rules/` altında JSON, repoda; ileride DB'ye yüklenir). Apple
veya Google bir kuralı değiştirince sürüm değil veri güncellemesi yapılır. Her kural kaydı: kimlik, mağaza, tür (ekran
sınıfı, önizleme sınıfı, son tarih, politika maddesi, build minimumu), değerler (boyutlar), yedek zinciri, "ne zaman
zorunlu" koşulu (cihaz ailesi, SDK sürümü, ülke, uygulama özelliği), yürürlük ve son tarih, kaynak URL ve kontrol tarihi.
Hazırlık kontrolü, stüdyo dışa aktarımı ve MCP araçları aynı katalogdan okur; Claude "eksik: Duo iç ekran (Nisan 2027'den
itibaren zorunlu)" gibi bir cevap görür.

### 12.2 Katalog v1 içeriği (18 Kasım 2026'da birincil kaynaklardan kontrol edildi)

Apple ekran görüntüleri ([Screenshot specifications](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/)):

| Sınıf | Boyutlar (dikey; yatayları da geçerli) | Zorunlu | Yedek |
|---|---|---|---|
| iPhone Dynamic Island (medium) | 1206x2622, 1179x2556 | Evet, iPhone'da çalışan her uygulama için en az bir görsel | Face ID (large) ölçeklenir |
| iPhone Dynamic Island (large) | 1320x2868, 1290x2796, 1260x2736 | Hayır | Face ID (large) |
| iPhone Face ID (large) | 1284x2778, 1242x2688 | Dynamic Island (large) verilmediyse | Dynamic Island (large) |
| iPhone Face ID (medium) | 1170x2532, 1125x2436, 1080x2340 | Hayır | Dynamic Island (medium) |
| iPhone Home Button (large, medium, 4", 3.5") | 1242x2208, 750x1334, 640x1136, 640x960 (durum çubuksuz varyantlar dahil) | Hayır | Bir üst sınıf |
| iPhone Duo dış ekran | 1398x2034 | Nisan 2027'den itibaren (aşağıdaki not) | Belirtilmemiş |
| iPhone Duo iç ekran | 2007x2853 | Nisan 2027'den itibaren | Belirtilmemiş |
| iPad 13" | 2064x2752, 2048x2732 | Evet, iPad'de çalışıyorsa | Yok (en üst sınıf) |
| iPad 12.9", 11", 10.5", 9.7" | Sayfadaki tüm boyutlar | Hayır | 13" veya bir üst sınıf |

Genel: en az bir, sınıf başına en fazla 10 görsel; jpeg veya png; alfa kanalı yok.

**Duo tutarsızlığı:** Ekran görüntüsü sayfası Duo'yu "Nisan 2027'den itibaren iOS 27.1 SDK veya sonrası ile derlenen
uygulamalar için zorunlu" diyor; App Store Connect sürüm notu (5 Ekim 2026) "Nisan 2027'den itibaren gönderimler Duo
ekran görüntüsü içermeli" diyor. Katalogda koşul SDK 27.1 olarak tutulur ve kaynak çelişkisi notlanır; izleyici
netleşmeyi takip eder. Duo görseli gönderilince ürün sayfasında "made for iPhone Duo" rozeti çıkar.

Apple diğer:
- Build minimumu: Xcode 26 ve iOS 26 SDK (28 Nisan 2026'dan beri); dağıtım hedefi iOS 13+ (9 Eylül 2026'dan beri).
- Yaş derecelendirmesi: Shared Location ve Dating and Relationships soruları, son tarih 12 Ocak 2027.
- Çin anakarası App Compliance beyanları (AIGC, Security Assessment): mevcut uygulamalar için 31 Ocak 2027.
- On-Demand Resources: 2027 başından itibaren yeni kullananlardan kabul edilmiyor (Background Assets'e geçiş).
- App Preview boyutları (Duo dahil) ayrı sayfadan katalog v1'e eklenecek; henüz kontrol edilmedi.

Google Play:
- Hedef API: yeni uygulamalar ve güncellemeler API 36 (31 Ağustos 2026); mevcut uygulamalar görünürlük için API 35.
- 15 Temmuz 2026 politika paketi: anonim ve rastgele sohbet uygulamalarına çocuk güvenliği kuralları; READ_CALL_LOG artık
  telefonla hesap doğrulaması için kullanılamaz; Android geliştirici doğrulaması için uygulamaların Play Console'da kayıtlı
  olması (kayıtsız uygulamalar kaldırılabilir); derecelendirmesiz uygulama yok. Maddelerin son tarihleri politika tablosundan
  katalog v1'e girecek.

### 12.3 Kural izleyici

- Periyodik iş (worker zamanlayıcısı) Apple Developer News ve RSS, Upcoming Requirements, App Store Connect sürüm notları,
  Play Console politika duyuruları ve hedef API sayfasını okur; içerik değişimini (hash) yakalar.
- Değişiklik "katalog önerisi" olarak platform yöneticisine düşer (otomatik yayın yok); Claude MCP ile öneriyi okuyup katalog
  güncellemesini hazırlayabilir, yönetici onaylar.
- Onaylanan değişiklik hangi uygulamaları etkiliyorsa sahiplerine bildirim gider (ör. "Fenpo, iOS 27.1 SDK ile derlenince Duo
  ekran görüntüsü isteyecek").

### 12.4 Uygulama başına uyum zaman çizelgesi

Her uygulamanın ekranında ona uyan bütün yaklaşan son tarihler: katalogdaki kural, uygulamanın verisiyle eşleşir (cihaz
aileleri, build SDK'sı, hedef API, dağıtılan ülkeler, abonelik var mı). Hazırlık kontrolü ve MCP özeti aynı listeyi kullanır.

Kaynaklar (18 Kasım 2026): [Apple screenshot specifications](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/),
[Apple upcoming requirements](https://developer.apple.com/news/upcoming-requirements/),
[App Store Connect release notes](https://developer.apple.com/help/app-store-connect/release-notes/),
[Yeni yaş derecelendirmesi descriptor'ları](https://developer.apple.com/news/?id=yxkh08us),
[China mainland compliance](https://developer.apple.com/help/app-store-connect/manage-compliance-information/view-china-mainland-compliance-information),
[Play target API](https://support.google.com/googleplay/android-developer/answer/11926878),
[Play politika duyurusu, 15 Temmuz 2026](https://support.google.com/googleplay/android-developer/answer/17134731),
[Play dataSafety API](https://developers.google.com/android-publisher/api-ref/rest/v3/applications/dataSafety).
