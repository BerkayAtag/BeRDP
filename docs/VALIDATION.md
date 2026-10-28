# Doğrulama kaydı

28 Ekim 2026, Windows geliştirme ortamı.

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

Gerçek AD/Kerberos/SSO, TLS reverse proxy, hedef VM'de RDP/NLA, çoklu ekran donanımı, kurulumun yükseltilmiş servis yaşam döngüsü, standart kullanıcı ACL denemesi, EDR/WDAC, kaldırma ve yedekten dönüş bu ortamda tamamlanmış değildir. Otomatik testlerin geçmesi bu senaryoların geçtiği anlamına gelmez.

Temiz self-contained win-x64 derlemesi ve iki Inno Setup derlemesi geçti. Server/Client EXE CompanyName alanı `Berkay Atağ`, ProductVersion `1.6.0` olarak okundu. Paketler Authenticode imzalı değildir.

Tanıtım sayfası Edge ile 1440 ve 390 piksel genişlikte kontrol edildi: görseller yüklendi, yatay taşma ve JavaScript hatası görülmedi. GitHub Actions tanımı yerelde hazırlandı; uzak workflow çalışması henüz yapılmadı.

Docker Desktop Linux container içinde 6 HTTPS kontrolü geçti: sertifika ve hostname doğrulaması, uzak cleartext HTTP reddi, anonim yönetim reddi, bootstrap giriş, boş makine listesi ve yalnız açıkça oluşturulan yönetici. VMware laboratuvar hazırlığı tamamlanamadı; kullanıcı tercihiyle VM adımı atlandı. Çalışan test VM yoktur.

`appwiz.cpl` Publisher kaydı her iki Inno tanımında `AppPublisher=Berkay Atağ` olarak belirlenir. EXE CompanyName alanı ayrıca doğrulandı; Windows üzerinde gerçek kurulum sonrası uninstall registry kaydı bu ortamda test edilmedi.
