# BeRDP 1.7.0 kurulum ve işletim

## Ön koşullar

Windows x64; desteklenen Windows 11 / Windows Server sürümünde kurum pilotu yapılmalıdır. Paketler self-contained .NET 10 içerir. Yönetici yetkisi, kurum DNS adı ve güvenilir TLS sertifikası gerekir. AD/SSO isteğe bağlıdır. Hedef VM'lerde RDP/NLA ve kullanıcı oturum açma hakları kurum tarafından yönetilir; BeRDP ajanı gerekmez.

## Sunucu

Etkileşimli kurulum için `BeRDP.Server.Setup.exe` dosyasını çift tıklayın. Sihirbaz yalnız ilk yönetici kullanıcı adını ve parolasını (en az 14 karakter) sorar; mevcut bir BeRDP veritabanı varsa bu sayfa atlanır. Parola komut satırına, kurulum günlüğüne veya dosyaya yazılmaz. Kurulum bitince yönetim konsolu `http://localhost:5173` adresinde açılır; Başlat menüsünde konsol, servisler ve kaldırma kısayolları bulunur.

Sessiz kurulum:

```powershell
.\BeRDP.Server.Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SSO=0 /LOG="$env:TEMP\BeRDP-Server.log"
```

Varsayılan uygulama dizini `C:\Program Files\BeRDP\Server`; veriler `C:\ProgramData\BeRDP`. Servisler `BeRdpBackend` ve `BeRdpAdminConsole`, yalnız `127.0.0.1:5001` ve `127.0.0.1:5173` dinler. Setup dış dünyaya HTTP portu açmaz; önceki BeRDP HTTP firewall kurallarını kaldırır. Eski CLIENTNETWORKS/PORTALNETWORKS parametreleri TLS yayınını sağlamaz.

Backend dizini, veritabanı ve DataProtectionKeys dizini SYSTEM, yerel Administrators ve mevcut backend servis hesabına sınırlandırılır. Özel veri yolu **yalnız BeRDP'ye ayrılmış** bir dizin olmalıdır; ortak klasör veya sürücü kökü kullanmayın. Mevcut özel servis hesabı korunur; hesap değişiminde ACL ve DPAPI anahtar erişimini ayrıca doğrulayın. Varsayılan servis kimliği LocalSystem'dır; kurumsal pilotta en az yetkili servis hesabına geçiş planlayın.

Sessiz kurulumda [ilk yöneticiyi komut satırından oluşturun](Admin-Credentials.md). Varsayılan hesap veya parola yoktur.

## HTTPS yayını

Kurum tarafından yönetilen TLS reverse proxy aynı sunucuda çalışmalıdır. Örnek yönlendirme:

| Dış yol | Yerel hedef |
|---|---|
| `https://berdp.example.test/api/*` | `http://127.0.0.1:5001/*` (`/api` öneki kaldırılır) |
| `https://berdp.example.test/*` | `http://127.0.0.1:5173/*` |

AdminConsole/appsettings.json içinde `AdminConsole:BackendBaseUrl` değerini `https://berdp.example.test/api` yapın. Backend/appsettings.json içinde `Cors:AllowedOrigins` listesine yalnız `https://berdp.example.test` ekleyin. Servisleri yeniden başlatın. SignalR için WebSocket aktarımını açın. Dış HTTP isteklerini TLS katmanında HTTPS'e yönlendirin, HSTS'yi kurum politikasına göre uygulayın. Kestrel portlarını internete açmayın.

Bu uygulama güvenilmeyen forwarded header'ları kabul etmez. Reverse proxy arkasında API hız sınırı/IP listesi proxy'nin loopback IP'sini görebilir; gerçek istemci IP'sine göre hız sınırı ve ağ kısıtını proxy üzerinde uygulayın. `Security:AllowedIpAddresses` tek başına proxy arkasındaki kullanıcı IP'sini ayıramaz.

Windows Negotiate, bağlantıya bağlı kimlik doğrulamasıdır. Her reverse proxy Kerberos/NTLM aktarımını doğru yapmaz. SSO için SPN, DNS, servis kimliği, kanal/bağlantı affinity ve tarayıcı intranet politikasını kurumun desteklenen IIS/Kerberos mimarisiyle doğrulayın. Parola girişli pilot ile başlayıp SSO'yu ayrı kabul edin. TLS sertifikası denetimini kapatmayın.

## İstemci

Etkileşimli kurulumda sihirbaz yalnız sunucu adresini sorar ve aynı sayfada doğrular. Toplu dağıtım:

```powershell
.\BeRDP.Client.Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="desktopicon" /BACKENDURL="https://berdp.example.test/api" /SSO=0 /LOG="$env:TEMP\BeRDP-Client.log"
```

`/SSO=1` yalnız etki alanı DNS adresi ve doğrulanmış Windows SSO ortamında kullanılmalıdır. Uzak HTTP adresleri reddedilir; HTTP sadece loopback geliştirme içindir. Yükseltmede mevcut URL korunur. Windows RDP kaynak izin uyarıları değiştirilmez; eski `/RDPWARNING=0` reddedilir.

Hedef VM'lere istemci kurmak gerekli değildir. İsteğe bağlı `rdphost` setup görevi hedef makine grafik/transport politikaları ve firewall kuralları değiştirir; merkezi GPO ile yönetilen VM'lerde bu görevi kullanmayın. Normal istemci kurulumu inbound RDP açmaz.

## Yükseltme / geri dönüş

1. Servisleri durdurun; DB, yapılandırma, DataProtectionKeys ve mevcut paket sürümünü birlikte yedekleyin. SQLite canlı dosyasını tutarsız biçimde kopyalamayın.
2. Yedeği kurumun erişim kontrollü deposunda tutun; JWT/AD anahtarları hassastır. DPAPI koruması servis/makine kimliğine bağlıdır; başka sunucuya taşımayı pilotta doğrulayın.
3. Yeni Server, ardından Client paketlerini kurun. Başarısız servis hazırlığı dosya değiştirmeden durur; her yükseltme için otomatik transaction rollback garantisi yoktur.
4. Eski demo hesap geçişini [kurtarma rehberine](Admin-Credentials.md) göre inceleyin.
5. Sağlık, giriş, yetki iptali, AD ve gerçek RDP bağlantısını doğrulayın. Sorunda servisleri durdurup uyumlu eski paket + aynı sürüme ait DB/yapılandırma yedeğini birlikte geri yükleyin.

Kaldırma `unins000.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART` ile yapılır. Veritabanı/yapılandırma yedekleri otomatik silinmez. Saklama ve imha politikasını ayrıca uygulayın.

## Pilot kabulü

Temiz kurulum, eski kurulumdan yükseltme, standart kullanıcı ile dosya/anahtar erişim reddi, servis hesabı değişimi, yeniden başlatma, TLS, AD grup kaldırma/hesap kapatma, NLA, çoklu ekran, Credential Manager geri yükleme, kurulum kaldırma ve yedekten dönüş testlerini gerçek hedef Windows/EDR/GPO kombinasyonunda tamamlayın. Intune/SCCM dağıtımını test etmek, sessiz parametre desteğinden ayrı bir kabul adımıdır.
