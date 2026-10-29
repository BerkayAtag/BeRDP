# Doğrulama kaydı

## 1.7.0 — 29 Ekim 2026

| Kontrol | Sonuç |
|---|---|
| .NET regresyon çalıştırıcısı | 139 kontrol geçti (yeni: atama adı gizliliği, AD grup konsol yetkisi, yerel ad doğrulaması, setup parola aktarımı) |
| Native service setup self-test | Geçti |
| Gerçek HTTP + tarayıcı smoke | 12 kontrol geçti (Edge headless) |
| Tarayıcı davranış regresyonu | 38 kontrol geçti |
| NuGet bağımlılık taraması (4 üretim projesi, transitive dahil) | Bilinen açık raporlanmadı |
| Inno Setup derlemesi | İki paket derlendi; setup ve uygulama exe'lerinde CompanyName `Berkay Atağ`, ProductVersion `1.7.0`, kırmızı BeRDP simgesi |
| Tanıtım sayfası | Edge 1440 ve 390 piksel, açık/koyu tema: görseller yüklendi, yatay taşma ve JavaScript hatası yok, tema tercihi kalıcı |

Temiz Windows kurulum/yeniden kurulum/kaldırma, appwiz Publisher/sürüm/simge kaydı ve setup sihirbazının ilk yönetici oluşturma yolu `main` üzerindeki Validate BeRDP çalışmasında (`tests/Smoke.Installers.ps1`) doğrulanır; release yalnız bu çalışmanın artifact'inden yayımlanır. Sihirbaz ekranları bu geliştirme bilgisayarında etkileşimli kurulumla denenmemiştir, çünkü kurulum servis ve sistem ayarı değiştirir.

## 1.6.0 — 28 Ekim 2026, Windows geliştirme ortamı

| Kontrol | Sonuç |
|---|---|
| .NET regresyon çalıştırıcısı | 131 kontrol geçti |
| Native service setup self-test | Yapılandırma koruma, yedek, SSO parametresi, kilitli dosyada güvenli başarısızlık geçti |
| Gerçek HTTP + tarayıcı smoke | 12 kontrol geçti |
| Tarayıcı davranış regresyonu | 38 kontrol geçti (fetch/gateway mock) |
| Dört üretim projesinin NuGet bağımlılık taraması | O anki NuGet advisory kaynağında bilinen açık raporlanmadı |
| Portal | Edge headless, 1440×1000 ve 390×844 |

HTTP testi yalnız geçici SQLite DB kullanır: anonim erişim reddi, varsayılan hesabın yokluğu, bootstrap giriş, kullanıcı/makine oluşturma, ID tabanlı atama kontrolü, atama iptali ve son yönetici koruması. Portal normal CSP ile gerçek login ve makine ekranını çalıştırır; mock regresyon sayfasında test kodu enjeksiyonu için CSP bypass yalnız test context'inde kullanılır. Mobil login yatay taşma kontrolü geçti. JavaScript sayfa hatası görülmedi.

Test adresleri `http://127.0.0.1:5501` ve `http://127.0.0.1:5517`; bunlar yalnız yerel QA adresleridir. Ekran görüntüleri `docs/ui/login.png`, `machines.png`, `login-mobile.png` içinde. İsimler/IP'ler sentetiktir.

Gerçek AD/Kerberos/SSO, TLS reverse proxy, hedef VM'de RDP/NLA, çoklu ekran donanımı, standart kullanıcı ACL denemesi, EDR/WDAC, eski sürümden yükseltme ve yedekten dönüş bu ortamda tamamlanmış değildir. Otomatik testlerin geçmesi bu senaryoların geçtiği anlamına gelmez.

Temiz self-contained win-x64 derlemesi ve iki Inno Setup derlemesi geçti. Server/Client EXE CompanyName alanı `Berkay Atağ`, ProductVersion `1.6.0` olarak okundu. Paketler Authenticode imzalı değildir.

Tanıtım sayfası Edge ile 1440 ve 390 piksel genişlikte kontrol edildi: görseller yüklendi, yatay taşma ve JavaScript hatası görülmedi. GitHub Actions üzerinde temiz Windows çalıştırıcısı kullanıldı.

Docker Desktop Linux container içinde 6 HTTPS kontrolü geçti: sertifika ve hostname doğrulaması, uzak cleartext HTTP reddi, anonim yönetim reddi, bootstrap giriş, boş makine listesi ve yalnız açıkça oluşturulan yönetici. VMware laboratuvar hazırlığı tamamlanamadı; kullanıcı tercihiyle VM adımı atlandı. Çalışan test VM yoktur.

`appwiz.cpl` Publisher kaydı her iki Inno tanımında `AppPublisher=Berkay Atağ` olarak belirlenir. EXE CompanyName alanı ayrıca doğrulandı; Gerçek kurulum sonrası uninstall registry kaydı GitHub-hosted Windows ortamında test edildi ve her iki paket için tam olarak `Berkay Atağ` bulundu.

### Gerçek Windows paket kabulü (1.6.0)

[Başarılı Windows CI çalışması](https://github.com/BerkayAtag/BeRDP/actions/runs/40738303817) kaynak commit `5858914a5551863979932c3f2c2ed5fb0b67d79a` üzerinden 4m33s içinde tamamlandı:

- Server ve Client sessiz kurulumları başarılı.
- appwiz.cpl'nin okuduğu uninstall kayıtlarında Publisher **Berkay Atağ**, DisplayVersion **1.6.0**.
- Kurulan iki servis sağlıklı ve yalnız loopback dinliyor.
- Kurulan API tarihsel varsayılan yönetici parolasını reddediyor.
- Aynı sürümün yeniden kurulması JWT anahtarını koruyor ve servisleri yeniden başlatıyor.
- Her iki kaldırma işlemi uninstall kayıtlarını ve servisleri kaldırıyor.

Bu kanıt belirli bir kurumun AD, EDR/GPO veya eski veri tabanıyla yükseltme kabulünü kapsamaz. Release paketleri bu başarılı çalışmanın artifact'inden yayımlanır; `installation-verification.json` sonucu ve SHA256 manifesti birlikte sunulur.
