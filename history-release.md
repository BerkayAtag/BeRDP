# BeRDP Sürüm ve Değişiklik Geçmişi (History Release)

Bu doküman, BeRDP projesinin sürüm geçmişini, yapılan mimari iyileştirmeleri, bug fix çalışmalarını, güvenlik yamalarını ve yeni özellikleri detaylandırmaktadır.

---

## [Sürüm 1.5.0] - 2026-10-26 Giriş ve Kimlik Çözümleme Düzeltmesi

### 1. `user01` Neden Giriş Yapamıyordu?
- **Kök neden:** `AuthController.Login` ve `LoginWithWindows`, `CanAccessAdminConsole = false` olan her kullanıcıya `401 Unauthorized` döndürüyordu. Bu kontrol ortak `/login` API'sine konulduğu için entitlement-only kullanıcılar yalnızca Admin Console'a değil, WPF masaüstü istemcisine de giriş yapamıyordu.
- **İkinci kök neden:** Kullanıcı adı çözümlemesi yalnızca kayıtlı tam kullanıcı adını veya kayıtlı UPN'i eşleştiriyordu. `DOMAIN\kullanici` biçimi sadece AD kullanıcılarında aranıyor, UPN soneki AD domainiyle eşleştirilmiyor ve 64 karakterden uzun kimlikler reddediliyordu.
- **Çözüm:**
  - Yeni `LoginIdentityResolver` eklendi. Sıra: tam kullanıcı adı/UPN → AD domain eşleşmesi → yerel hesap.
  - `AKNET` (NetBIOS) ve `aknet.akb` (DNS) aynı domain kabul edilir; iki farklı FQDN eşleştirilmez.
  - `DOMAIN\kullanici`, `kullanici@domain` ve düz SAM biçimleri yerel demo hesapları dahil tüm hesaplarda çalışır.
  - Aynı düz SAM birden fazla AD domaininde varsa `400` ve `domain\kullanici`/UPN yönlendirmesi döner.
  - `LoginRequest.Username` sınırı 64 → 256 karaktere çıkarıldı.
  - `/login` ve `/login/windows` artık tüm doğrulanmış kullanıcılara token döndürür. Konsol erişimi Admin Console arayüzü ve korumalı endpoint'lerde denetlenir.

### 2. Konsol Yetkisi ve Oturum Güvenliği
- `SessionTokenValidator`, `CanAccessAdminConsole` ve `ConsoleRole` claim'lerini her istekte veritabanından yeniler. Böylece konsol yetkisi verildiğinde/geri alındığında eski token bir sonraki istekte güncel yetkiyle değerlendirilir.
- Windows SSO (`/login/windows`) domain eşleşmesi de NetBIOS/DNS alias'larını kabul eder.
- Admin Console ve WPF giriş ekranları üç kimlik biçimini örnekleriyle gösterir; konsol dışı kullanıcıya masaüstü istemcisini kullanması söylenir.

### 3. Giriş Hız Sınırı
- Aynı IP için dakikalık giriş limiti 20 → 60'a çıkarıldı. Paylaşımlı NAT/VDI çıkışında çok sayıda meşru kullanıcının aynı anda giriş yapabilmesi sağlandı.
- Limit `appsettings.json` içindeki `Security:LoginRateLimitPerMinute` ile 10–600 arasında yapılandırılabilir; aşımda `429` döner.

### 4. Masaüstü İstemcisinde `HTTP 405` Hatası Giderildi
- **Belirti:** İstemci kurulumunda Backend API yerine Admin Console adresi (`http://localhost:5173`) verildiğinde `user01` girişi **"İstek başarısız: HTTP 405."** ile reddediliyordu.
- **Kök neden:** `5173` portu yalnızca statik dosya ve `/config.js` sunan Admin Console'dur; `POST /login` bu sunucuda tanımlı olmadığı için `405 Method Not Allowed` döner. İstemci, kurulumda girilen adresi doğrudan API adresi sanıyordu.
- **Çözüm:**
  - Yeni `BackendEndpointResolver` eklendi. Giriş öncesinde portalın `/config.js` dosyası okunarak gerçek API adresi (`backendBaseUrl`) bulunur.
  - Portal başka bir ana bilgisayardan açıldıysa ve `config.js` loopback adres bildiriyorsa, istemcinin kullandığı ana bilgisayar adı korunur.
  - Yerel geliştirme portları için güvenli eşleme: `localhost:5173` → `localhost:5001`, `localhost:7173` → `localhost:7001`. Uzak portal adresleri körlemesine başka porta yönlendirilmez.
  - Otomatik düzeltme yapıldığında giriş ekranında bilgi mesajı gösterilir; `405`/`404` hataları artık doğru API adresini ve kontrol edilecek noktaları açıklar.
  - Client Setup sihirbazı API veya Admin Console adresinin girilebileceğini belirtir.

### 5. Windows "Caution: Unknown remote connection" Uyarısı Giderildi
- **Belirti:** İstemciden RDP başlatıldığında Windows, **"Caution: Unknown remote connection"** / **"Allow the remote computer to access the following resources on my computer"** uyarısını gösteriyor ve tüm yerel kaynak yönlendirmelerini kapatıyordu. BeRDP içinde seçilen pano, sürücü, yazıcı, akıllı kart, port ve PnP ayarları her bağlantıda sıfırlanıyordu.
- **Kök neden:** Nisan 2026 güvenlik güncellemesi ([KB5083769 / CVE-2026-26151](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2026-26151)), imzasız `.rdp` dosyaları açıldığında her seferinde yeni bir kaynak onay diyaloğu gösteriyor ve yönlendirmeleri varsayılan olarak kapatıyor. BeRDP her bağlantı için yeni bir geçici `.rdp` dosyası ürettiğinden uyarı sürekli tekrarlanıyordu.
- **Çözüm:**
  - Client Setup, Microsoft'un belgelediği geçici geri dönüş politikasını uygular: `HKLM\SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services\Client\RedirectionWarningDialogVersion = 1`.
  - İstemci, bağlantıdan önce kullanıcının seçtiği yönlendirmeleri hedef bilgisayar için `HKCU\Software\Microsoft\Terminal Server Client\LocalDevices` onay listesine yazar. Böylece eski Windows uyarısı da ilk bağlantıda bastırılır.
  - Onay maskesi yalnızca gerekenleri kapsar: pano `0x04`, kayıtlı kimlik bilgisi kullanımı `0x08`, mikrofon `0x80`, sürücüler `0x01`, seri/paralel portlar `0x02`, PnP `0x20`. Taban değer `0xCC`'dir.
  - **`0x08` (kayıtlı kimlik bilgisi) kritik:** Windows hedef için `TERMSRV` kimlik bilgisi bulduğunda (BeRDP otomatik oturum açma için bir tane yazar; kullanıcı da önceden kaydetmiş olabilir) "Don't ask me again" seçeneği **olmayan** "unknown publisher" uyarısına geçer ve bu uyarı hiçbir zaman hatırlanmaz. Bu bit olmadan uyarı her bağlantıda tekrar ediyordu; deneysel olarak asgari gerekli maske `0x8C` (`0x80|0x08|0x04`), taban `0xCC` bu durumu da kapsar.
  - `.rdp` profili değişmez; yalnızca BeRDP ayarlarında seçilen yönlendirmeler uygulanır.
  - Yeni Windows uyarısını bilinçli olarak korumak için `/RDPWARNING=1` parametresi eklendi (politika değeri `2`).
- **Not:** Politika makine genelindedir; Microsoft gelecekte kaldırabilir. GPO yeni uyarıyı zorluyorsa yerel ayar ezilebilir.

### 6. RDP Experience Varsayılanları Sabitlendi
- **İstek:** Bağlantı hızı **Satellite** seçili olmalı; yalnızca **Font Smoothing**, **Desktop Composition** ve **Visual Styles** açık olmalı; Experience sayfasındaki diğer tüm seçenekler kapalı olmalı. Ayar kullanıcıya gösterilmemeli, sabit varsayılan olarak uygulanmalı.
- **Çözüm:** `RdpProfileWriter.Build` içinde Experience değerleri sabitlendi; istemcide Experience ayar ekranı yoktur, kullanıcı bu değerleri değiştiremez.
  - `connection type:i:3` → **Satellite (2 Mbps - 16 Mbps with high latency)**. Doğrulama: `1` Modem, `2` Low-speed broadband, `3` Satellite, `4` High-speed broadband, `5` WAN, `6` LAN, `7` otomatik/özel.
  - `networkautodetect:i:0`, `bandwidthautodetect:i:0` → "Detect connection quality automatically" kapalı.
  - Açık: `allow font smoothing:i:1`, `allow desktop composition:i:1`, `disable themes:i:0` (Visual styles).
  - Kapalı: `disable wallpaper:i:1` (Desktop background), `disable full window drag:i:1` (Show window contents while dragging), `disable menu anims:i:1` (Menu and window animation), `bitmapcachepersistenable:i:0` (Persistent bitmap caching), `autoreconnection enabled:i:0` (Reconnect if the connection is dropped).
- **Doğrulama yöntemi:** Gerçek istemciyle üretilen `.rdp` dosyası `mstsc.exe /edit` ile açıldı; Win32 `SysTabControl32` ile Experience sekmesine geçilip `BM_GETCHECK` / `CB_GETCURSEL` ile durumlar okundu. Aynı kontrol canlı istemci bağlantısından yakalanan profil üzerinde de tekrarlandı.
- **Sonuç:** Satellite seçili; Font smoothing=1, Desktop composition=1, Visual styles=1; Desktop background=0, Show window contents while dragging=0, Menu and window animation=0, Persistent bitmap caching=0, Reconnect if the connection is dropped=0.

### 7. Doğrulama ve Teslim
- **134/134** .NET regresyon testi başarılı. Yeni kapsam: düz SAM, `DOMAIN\kullanici`, NetBIOS/DNS öneki, UPN, AD'siz UPN, yalnız UPN ile saklanan AD hesabı, farklı FQDN reddi, belirsiz SAM (400), yanlış parola, token claim'leri, `LastLoginAtUtc` kalıcılığı, konsol yetkisinin anında geri alınması, portal adresinden API adresi çözümleme, RDP kaynak onay maskesi (kayıtlı kimlik bilgisi biti dahil) ve Experience varsayılanları.
- Kurulu sunucuda **42 canlı kontrol** başarılı:
  - 12 hesabın tamamı düz kullanıcı adıyla; admin/console-user/user01 ayrıca `AKNET\`, `aknet.akb\`, `@aknet.akb` ve `@AKNET` biçimleriyle giriş yaptı.
  - `user01` → 200, `canAccessAdminConsole=false`; `/admin/machines` ve `/users` → 403, `/machines` → yalnızca 2 yetkili makine.
  - `console-user` → 200, 10 makinenin tamamını salt okunur gördü; admin uç noktaları → 403.
  - Yanlış parola/kullanıcı → 401; boş istek → 400.
- Admin Console tarayıcı E2E: `AKNET\user01` konsoldan doğru mesajla reddedildi; admin tüm menüleri ve 10 makineyi gördü; standart kullanıcı yalnızca Masaüstlerim ekranını, 10 makineyi ve düzenleme butonu olmadan gördü; Ağ Erişim Kuralları ve Sessiz Kurulum sayfaları açıldı.
- Gerçek WPF istemcisi E2E (405 senaryosu): Client Setup bilerek `/BACKENDURL=http://localhost:5173` ile kuruldu. İstemci `/config.js` üzerinden `http://localhost:5001` adresini buldu, `user01` ile giriş yaptı ve atanmış 2 makineyi (VM-001, VM-006) listeledi.
- Gerçek WPF istemcisi RDP E2E (Nisan 2026 uyarısı): Client Setup `/BACKENDURL=http://localhost:5173` ile kuruldu, `user01` ile giriş yapıldı ve VM-006 için "RDP ile Bağlan" tetiklendi. Üç senaryoda da `WARNING_DIALOG=False`:
  - ilk kullanım (kayıt yok, kimlik bilgisi yok),
  - önceden kaydedilmiş `TERMSRV/vm-006.berdp.local` kimlik bilgisi + eski `0xC4` onayı,
  - yalnızca istemcinin kendi yazdığı kimlik bilgisi.
  İstemci `vm-006.berdp.local=0xCC` değerini yazdı, `mstsc.exe` ve `wksprt.exe` başladı; Windows uyarı penceresi hiç açılmadı. (Uyarı engellenmediğinde mstsc uyarıyı gösterip bekliyor; engellendiğinde oturumu `wksprt.exe`'ye devredip kapanıyor.)
- Kurulum paketleri yeniden derlendi ve hem sunucu hem istemci yeniden kuruldu:
  - `dist/installers/BeRDP.Server.Setup.exe` (142.199.377 bayt)
  - `dist/installers/BeRDP.Client.Setup.exe` (51.490.727 bayt)
  - `/download/client-setup` güncel istemci paketini 51.490.727 bayt olarak sundu.
- RDP Experience E2E: Final istemci paketiyle `user01` girişi yapıldı, VM-006 bağlantısı tetiklendi ve canlı `.rdp` profili yakalandı. Profilde `connection type:i:3`, `networkautodetect:i:0`, `bandwidthautodetect:i:0`, `allow font smoothing:i:1`, `allow desktop composition:i:1`, `disable themes:i:0`, `disable wallpaper:i:1`, `disable full window drag:i:1`, `disable menu anims:i:1`, `bitmapcachepersistenable:i:0`, `autoreconnection enabled:i:0` değerleri doğrulandı; Experience sekmesinde Satellite ve istenen üç seçenek açık, diğerleri kapalı görüldü.

---

## [Sürüm 1.5.0] - 2026-10-25 Bakım Güncellemesi (Hotfix)

### 1. Admin Konsolu Giriş Hatası Giderildi
- **Kök neden:** Kurulu veritabanındaki `admin` kullanıcısının BCrypt hash'i, belgelenen `Berdp@Admin2026!Secure#Password` yerine eski `Admin123!` parolasına ait idi.
- **İkinci kök neden:** `DbInitializer.Seed` yalnızca `Users` tablosu boşsa çalışıyordu. Bu nedenle mevcut kurulumlara yeni demo hesapları, makineler ve entitlement'lar hiç eklenmiyordu.
- **Çözüm:**
  - Seed işlemi `SystemSettings["DemoSeedVersion"] = 2` ile sürümlendi ve idempotent hale getirildi.
  - Mevcut admin hesabı korunarak hash yalnızca legacy `Admin123!` ile eşleşiyorsa bir kez onarılır; kullanıcı tarafından değiştirilmiş özel parolalar ezilmez.
  - Eksik `console-user` ve `user01`–`user10` hesapları, 10 makine ve 22 atama mevcut kuruluma otomatik eklenir.
  - AD hesaplarıyla aynı ada sahip yerel demo kullanıcıları asla ezilmez.

### 2. Admin / Standart / Yetkisiz Konsol Erişim Modeli
- Konsol girişi `CanAccessAdminConsole` alanına bağlandı; entitlement-only kullanıcılar konsola giremez.
- `ConsoleRole.Admin` tam yetki, `ConsoleRole.Standard` salt okunur erişim sağlar.
- Konsol erişimi olan Standart kullanıcılar da tüm makine envanterini ve atama bilgilerini görür; düzenleme yapamaz.
- Yetkilendirme API'si ve ekranı; **Admin Yetkisi Ver**, **Standart Konsol (Salt Okunur)** ve **Konsol Erişimini Kaldır** işlemlerini ayrı ayrı destekler.
- Login ve kullanıcı yanıtlarında `ConsoleRole` artık `"Admin"` / `"Standard"` olarak serileştirilir; istemci eski sayısal değerleri de kabul eder.

### 3. Masaüstlerim Ekranı
- Admin ve Standart konsol kullanıcıları 10 sanal makinenin tamamını görür.
- Kartlarda makine adı, domain, IP adresi, RDP portu, atanmış kullanıcı adları ve AD grup entitlement'ları gösterilir.
- Enter tuşuyla makine filtresi doğrulandı.

### 4. Doğrulama ve Teslim
- **68/68** .NET regresyon testi başarılı; legacy veritabanı yükseltme senaryosu, admin hash onarımı ve rol kontrolleri kapsama eklendi.
- Kurulum paketleri yeniden derlendi:
  - `dist/installers/BeRDP.Server.Setup.exe` (142.173.615 bayt)
  - `dist/installers/BeRDP.Client.Setup.exe` (51.475.877 bayt)
- Kurulu sunucuda canlı doğrulama:
  - `admin` → 200, `consoleRole=Admin`, 10 makine.
  - `console-user` → 200, `consoleRole=Standard`, 10 makine; `/admin/machines` → 403.
  - `user01` → 401, konsola giriş reddedildi.
  - `/download/client-setup` → 51.475.877 baytlık güncel istemci kurulumu.
- Admin Console tarayıcı E2E: dört zorunlu menü öğesi, Client Setup indirme, Enter ile filtreleme, standart kullanıcı salt okunur görünümü ve yetkisiz kullanıcı reddi doğrulandı.

---

## [Sürüm 1.5.0] - 2026-10-24

### 1. Modern Kurumsal UI Tasarımı (Akbank Kırmızısı ve Beyazı)
- **Akbank Kurumsal Renk Paleti:**
  - Ana Marka Rengi: Akbank Kırmızısı (`#E20613` - Pantone 186 C standardı).
  - Hover / Aktif Durumları: `#C90510` (Hover), `#A6040D` (Pressed), `#FFF0F1` (Subtle tint).
  - Kontrast: Net beyaz (`#FFFFFF`) ve modern tipografi hiyerarşisi.
- **Karanlık (Dark) ve Açık (Light) Tema Desteği:**
  - **Client (WPF Masaüstü Uygulaması):**
    - Dinamik kaynak yönetimi (`ThemeManager`) ile tek tıkla canlı tema değişimi.
    - Kullanıcı tercihi (`%LOCALAPPDATA%\BeRDP\Client\theme.txt`) üzerinde kalıcı olarak saklanır.
    - Koyu Tema (`#111215` derin antrasit arka plan, `#181A20` kartlar, canlı kırmızı vurgular).
    - Açık Tema (`#F8F9FA` temiz beyaz kurumsal zemin, `#FFFFFF` kartlar, keskin kontrastlı metinler).
    - Canlı durum rozetleri: Çevrimiçi için zümrüt yeşili rozet (`#DEF7EC` / `#03543F`), Çevrimdışı için yumuşak gri rozet (`#F3F4F6` / `#6B7280`).
    - Makine Arama ve Filtreleme çubuğu: Kullanıcıların atanan makineler arasında isim, IP veya hostname ile anlık arama yapabilmesi sağlandı.
  - **Admin Console & Kullanıcı Web Portalı:**
    - `site.css` tamamen Akbank Kırmızısı ve beyazı kurumsal kimliğine uyarlandı.
    - Koyu ve Açık tema desteği, `localStorage` üzerinde kaydedilerek oturumlar arası korunur.
    - Modern kart gölgeleri, kenarlık yumuşatmaları, istatistik kartlarında kurumsal kırmızı gösterge çizgileri eklendi.
    - Masaüstlerim grid görünümünde modern hover efektleri ve canlı TCP erişilebilirlik rozetleri entegre edildi.

---

### 2. Windows Hello ile SSO ve RDP Kimlik Doğrulama Çözümü
- **İstemcide Windows Hello ile SSO Girişi:**
  - Kullanıcı dizüstü bilgisayarında Windows Hello (PIN, Parmak İzi veya Yüz Tanıma) ile Windows oturumu açtığında, BeRDP istemcisine kurumsal Windows kimliği (Negotiate/Kerberos) ile tek tıkla şifresiz giriş yapabilir.
  - İstemci giriş ekranında **⊞ Windows Oturumuyla Giriş (SSO)** butonu doğrudan kullanılabilir hale getirildi.
  - Otomatik SSO (`/SSO=1`) etkinleştirildiğinde uygulama açılışında doğrudan giriş denenir; sunucuya ulaşılamazsa veya SSO başarısız olursa kullanıcıyı kilitlemeden parola formunu hazır tutar.
- **Hedef RDP Makinesinde Windows Hello Olmaması Durumunun Çözümü:**
  - **Sorun:** Windows Hello dizüstü bilgisayarda açık olduğunda, RDP bağlantısında mstsc Windows Hello'nun sanal akıllı kart (Virtual Smart Card) veya WebAuthn sağlayıcısını tetiklemekte, kullanıcıdan PIN veya parmak izi istemektedir. Ancak hedef sanal makinede (VM) Windows Hello yapılandırması bulunmadığından bağlantı reddedilmekteydi. Ayrıca `/remoteGuard` bayrağı hedefte Remote Credential Guard yoksa bağlantıyı tamamen engellemekteydi.
  - **Çözüm Adımları:**
    1. **Varsayılan Domain Kullanıcısı Formatı:** Backend ve Client, kullanıcının tam yetkili domain adını (`DOMAIN\kullanici` veya UPN `kullanici@domain.com`) çözümleyerek RDP profilindeki `username:s:...` parametresine otomatik yazar.
    2. **Windows Hello Müdahalesini Engelleme:** `.rdp` profiline `redirectsmartcards:i:0` ve `redirectwebauthn:i:0` parametreleri yerleştirildi. Böylece Windows'un local Hello sanal akıllı kart sağlayıcısını uzak NLA oturumuna zorlaması engellendi.
    3. **Hedefte Remote Guard Zorlamasının Kaldırılması:** Standart domain makinelerinde bağlantıyı bozan `/remoteGuard` bayrağı varsayılan bağlantıdan çıkartıldı.
    4. **Entegre AD Parolası İletişim Kutusu (`RdpCredentialPromptWindow`):**
       - Kullanıcı BeRDP'ye Windows Hello SSO ile girdiğinde (istemcinin belleğinde henüz parola yokken) makineye tıklandığında modern kurumsal bir diyalog açılır.
       - Domain Kullanıcı Adı varsayılan olarak kilitli ve seçili gelir (`DOMAIN\kullanici`).
       - Kullanıcı Active Directory parolasını girer.
       - **"Bu oturum boyunca parolamı hatırla"** seçeneği işaretlendiğinde, parola oturum boyunca bellekte korunur; kullanıcı diğer makinelerine bağlandığında tekrar parola sormadan doğrudan bağlanır.
       - **"Doğrudan Bağlan"** seçildiğinde: Parola güvenli Windows Credential Store (`TERMSRV/{target}`) içine geçici olarak yazılır ve mstsc hiçbir şifre veya Hello PIN ekranı çıkarmadan doğrudan uzak masaüstünü açar.
       - **"RDP'de Sor (mstsc)"** seçildiğinde: `.rdp` profili `prompt for credentials:i:1` ile hazırlanır; mstsc doğrudan standart Windows parola giriş ekranını domain kullanıcısı seçili olarak açar.

---

### 3. Klavyeden Enter ile Otomatik İşlem ve Filtreleme Desteği
- **Web Admin Konsolu ve Kullanıcı Portalı:**
  - Tüm metin ve filtre kutularında (`usersFilterInput`, `authUsersFilterInput`, `machinesFilterInput`, vb.) klavyeden `Enter` tuşuna basıldığında ilgili "Uygula", "Ara" veya kaydetme butonu otomatik tetiklenir.
  - Form içindeki ve form dışındaki arama alanları için akıllı `keydown` dinleyicileri entegre edildi.
- **Client (WPF Masaüstü Uygulaması):**
  - **Ana Ekran Filtre Kutusu (`SearchBox`):** Filtre kutusunda metin yazıp `Enter` tuşuna basıldığında, liste filtrelenir ve tek makine eşleştiğinde doğrudan RDP bağlantısı başlatılır.
  - **Parola / Kimlik Doğrulama Diyaloğu (`RdpCredentialPromptWindow`):** AD parolası yazıldıktan sonra `Enter` basıldığında doğrudan "Doğrudan Bağlan" seçeneği çalıştırılır.
  - **Ekran ve Kaynak Ayarları Penceresi (`RdpLocalResourcesWindow`):** Özel çözünürlük veya aygıt ayarları yapıldıktan sonra `Enter` basıldığında "Ayarları Kaydet" otomatik tetiklenir.
  - **Giriş Ekranı (`LoginWindow`):** Kullanıcı adı veya parola alanlarında `Enter` basıldığında "Parola ile Giriş Yap" otomatik tetiklenir.

---

### 4. Admin Arayüzünde İstemci (Client) Setup İndirme Entegrasyonu
- Web Admin konsolu üst menüsüne kurumsal `⬇ Client Setup İndir` butonu eklendi.
- Backend API ve Admin Console üzerinde `/download/client-setup` uç noktası yapılandırıldı.
- Adminler ve kullanıcılar tek tıkla sistemde derlenmiş en güncel `BeRDP.Client.Setup.exe` dosyasını indirebilir.

---

### 5. Admin Arayüzünde İnteraktif Ağ Erişim Kuralları (Firewall Matrisi) Sayfası
- Admin konsolu sol menüsüne **"Ağ Erişim Kuralları"** sekmesi eklendi.
- Sayfa içerisinde:
  1. İstemci ◄► Sunucu ve RDP Hedefleri Port Matrisi (TCP 5001 API/SignalR, TCP 5173 Web, TCP/UDP 3389 RDP).
  2. BeRDP Sunucusu ◄► Active Directory Port Matrisi (DNS 53, Kerberos 88, LDAP 389, Global Catalog 3268, RPC 135, Dinamik RPC).
  3. PowerShell hızlı doğrulama ve `Test-NetConnection` komutları yer almaktadır.

---
### 6. Admin Arayüzünde Sessiz Kurulum & Kaldırma Komutları Sayfası
- Sol menüde **"Sessiz Kurulum & Kaldırma"** sekmesi eklendi.
- Bu sayfa, kurumsal dağıtım (SCCM / Intune / GPO) senaryoları için gerekli olan tüm sessiz kurulum ve kaldırma komutlarını interaktif ve temiz bir şekilde gösterir:
  - İstemci (Client) Sessiz Kurulum Komutları (Normal Kullanıcı ve RDP Host senaryoları).
  - İstemci (Client) Sessiz Kaldırma Komutu.
  - Sunucu (Server) Sessiz Kurulum Komutu.
  - Sunucu (Server) Sessiz Kaldırma Komutu.
  - In-Place Yükseltme (Güncelleme) Prosedürü.

---


### 7. Kapsamlı Bug Fix ve Güvenlik İyileştirmeleri
1. **Yüksek Düzey Güvenlik Açığı (CVE) Yaması:**
   - `Microsoft.AspNetCore.Authentication.Negotiate` paketi `8.0.6` sürümünden `8.0.32` sürümüne yükseltildi.
   - Bu güncelleme ile GHSA-2p3q-h3hg-jcqq, GHSA-8prm-248r-h957 ve GHSA-vrrp-6v32-2p8g güvenlik zafiyetleri tamamen giderildi.
   - `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.EntityFrameworkCore` ve `Microsoft.AspNetCore.SignalR.Client` paketleri 8.0.32'ye çekilerek tüm zafiyetler ve uyarılar sıfırlandı (`0 vulnerable packages`).
2. **ActiveDirectoryService Null Dereference Giderimi:**
   - `ActiveDirectoryService.cs` içerisindeki `user.Sid.Value` çağrısı öncesinde eksik null denetimi giderilerek derleme uyarısı (CS8602) ve olası çalışma zamanı `NullReferenceException` riski ortadan kaldırıldı.
3. **AppConfiguration Yerel Test Esnekliği:**
   - `AppConfiguration.cs` içerisindeki IP veya tek etiketli hostname kullanımında uygulamanın başlamasını engelleyen kontrol düzeltildi; yerel test ve intranet ortamlarında çökmeler engellendi.
4. **WPF Client Modern İkon Oluşturucu:**
   - Projenin derleme anında dinamik ürettiği `BeRdpClientBrand.ico` ikonunun piksel rengi resmi Akbank Kırmızısı RGB(226, 6, 19) olarak güncellendi.
5. **Regresyon Testleri ve Doğrulama:**
   - Bu sürümün ilk doğrulamasında RDP profil güvenlik ve Windows Hello izolasyon testleri dahil **60/60** C# regresyon testi geçti.
   - 25 Ekim 2026 bakım güncellemesiyle legacy yükseltme ve rol testleri eklendi; güncel sonuç **68/68**.
   - Admin Console arayüzü ayrıca canlı kurulumda tarayıcı E2E senaryolarıyla doğrulandı (admin, standart konsol ve yetkisiz kullanıcı).

---

## [Sürüm 1.4.0]
- PowerShell kapalı kurumsal ortamlar için yerel `BeRdp.ServiceSetup.exe` yardımcı modülü eklendi.
- Kurulum ve yükseltmelerde atomik `appsettings.json` konfigürasyon yedekleme mekanizması getirildi.
- Guacamole tabanlı tarayıcı RDP gateway biletleme entegrasyonu sağlandı.
- Monitor seçim matrisi ve çoklu monitör bitişiklik doğrulama motoru eklendi.

---

## [Sürüm 1.3.0]
- Active Directory grup bazlı admin yetkilendirme sayfası ayrıldı.
- 5000+ makine için sayfalama ve sunucu taraflı arama filtreleri eklendi.
- SQLite veritabanı şifreleme ve Windows Data Protection entegrasyonu sağlandı.
