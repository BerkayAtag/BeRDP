# Public yayın hazırlığı

1. Hedef GitHub hesabı/organizasyonu altında `BeRDP` deposunu belirleyin. MIT LICENSE ve kaynakları gizli veri taramasından sonra yükleyin. Setup logları, DB, runtime anahtarları ve eski dist çıktıları kaynak commit'ine girmemelidir.
2. Private vulnerability reporting, branch protection ve bağımlılık bildirimlerini açın.
3. `installer/Build-Installers.ps1` ile temiz Release derlemesi yapın; `-SkipPublish` public release için kullanılmamalıdır.
4. Pilot kabul matrisini tamamlayın. Güvenilir Berkay Atağ kimliğine ait Authenticode sertifikasıyla exe/setup dosyalarını imzalayın. Sertifika veya parolasını repoya koymayın.
5. İmzalamadan sonra SHA256 manifestini yeniden üretin. Setup metadata'sındaki Publisher, Authenticode kimliği değildir.
6. GitHub Release'e Server/Client setup ve SHA256SUMS yükleyin; açık test sınırları ve yükseltme notlarını ekleyin. Büyük binary dosyalarını git geçmişine koymayın.
7. GitHub Pages için `docs/` dizinini seçin. Tanıtım sayfası indirme düğmeleri `releases/latest/download/` bağlantılarını kullanır; bu yüzden son kararlı sürüm "Latest" olarak işaretlenmelidir.

Yerel paketler `dist/installers/` altında üretilir. Yayın hedefi `BerkayAtag/BeRDP` olarak doğrulandı. Kod imzalama sertifikası dahil değildir. CI örneği otomatik public yayın yapmaz; çıktı review için artifact olarak tutulur.

`publish-candidate.yml` (Publish verified release) `run_id` ve `tag` (ör. `v1.7.0`) girdileriyle elle başlatılır. Yalnız başarılı `main` Windows test çalışmasının artifact'ini kabul eder; SHA256, iki paketin Publisher, sürüm ve appwiz simge sonucunu doğrular, `docs/releases/<tag>.md` notlarıyla release oluşturur veya günceller. Kontrol başarısızsa public release yapılmaz. Ekran görüntüleri `tests/Screenshots.Web.cjs` ile yeniden üretilir.
