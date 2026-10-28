# Public yayın hazırlığı

1. Hedef GitHub hesabı/organizasyonu altında `BeRDP` deposunu belirleyin. MIT LICENSE ve kaynakları gizli veri taramasından sonra yükleyin. Setup logları, DB, runtime anahtarları ve eski dist çıktıları kaynak commit'ine girmemelidir.
2. Private vulnerability reporting, branch protection ve bağımlılık bildirimlerini açın.
3. `installer/Build-Installers.ps1` ile temiz Release derlemesi yapın; `-SkipPublish` public release için kullanılmamalıdır.
4. Pilot kabul matrisini tamamlayın. Güvenilir Berkay Atağ kimliğine ait Authenticode sertifikasıyla exe/setup dosyalarını imzalayın. Sertifika veya parolasını repoya koymayın.
5. İmzalamadan sonra SHA256 manifestini yeniden üretin. Setup metadata'sındaki Publisher, Authenticode kimliği değildir.
6. GitHub Release'e Server/Client setup ve SHA256SUMS yükleyin; açık test sınırları ve yükseltme notlarını ekleyin. Büyük binary dosyalarını git geçmişine koymayın.
7. GitHub Pages için `docs/` dizinini seçin. `docs/site-config.js` içindeki repositoryUrl alanına doğrulanmış depo URL'sini girin; tanıtım sayfası release indirme bağlantılarını buradan üretir.

Yerel paketler `dist/installers/` altında üretilir. Yayın hesabı ve kod imzalama sertifikası bu çalışma kapsamında varsayılmaz. CI örneği otomatik public yayın yapmaz; çıktı review için artifact olarak tutulur.
