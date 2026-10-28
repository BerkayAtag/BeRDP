# İnceleme ve gelişim yol haritası

Araştırma tarihi: 28 Ekim 2026. İlgili açık kaynak projelerin mimarileri ve README'leri incelendi; GitHub'ın tüm depolarının tarandığı iddia edilmez. Aşağıdakiler uygulanmış özellikler değil, önceliklendirilmiş önerilerdir.

| Öncelik | Öneri | Gerekçe / kabul ölçütü |
|---|---|---|
| P0 | İmzalı release ve pilot VM matrisi | Publisher kimliğini Authenticode ile doğrulama; temiz kurulum/yükseltme/geri dönüş kanıtı |
| P0 | Kalıcı yönetim audit trail | Kim, neyi, ne zaman değiştirdi; parola/token kaydetmeden SIEM'e aktarım |
| P1 | OIDC / kurumsal MFA | Admin erişiminde güçlü kimlik; JWT/Negotiate akışlarıyla açık rol eşlemesi |
| P1 | RD Gateway bağlantı profili | Ajan eklemeden merkezi RDP ağ sınırı; CAP/RAP ve NLA kabul testleri |
| P1 | İmzalı güncelleme manifesti | SHA256 + imza + sürüm düşürme koruması; otomatik güncellemeden önce pilot halkası |
| P1 | Güvenli yedek/geri yükleme aracı | DB + korumalı anahtarlar + kimlik bağımlılığını birlikte doğrulama |
| P2 | Favoriler, etiketler, son bağlantılar | Büyük envanterde aramayı kolaylaştırma; kullanıcıya özel yerel tercihler |
| P2 | Tanılama paketi | Redakte edilmiş sağlık/versiyon/bağlantı bilgisi, açık kullanıcı kontrolü |
| P2 | Büyük ortam veri katmanı | SQLite eşzamanlılık/yazma yükü ölçümü; gerekirse SQL Server/PostgreSQL geçişi |

## Karşılaştırılan projeler

- [mRemoteNG](https://github.com/mRemoteNG/mRemoteNG): sekmeli çok protokollü bağlantı yönetimi. BeRDP için favoriler, gruplama ve sürüm kanalı deneyimi değerlidir; bütün protokolleri eklemek mevcut RDP odağını gereksiz büyütür.
- [Apache Guacamole](https://github.com/apache/guacamole-client): istemcisiz web geçidi. Mevcut opsiyonel entegrasyonu TLS, kısa ömürlü bilet ve gerçek NLA testleriyle olgunlaştırmak hedef VM ajanı eklemekten daha uygun.
- [MeshCentral](https://github.com/Ylianst/MeshCentral): uzaktan yönetim ve ajan temelli cihaz yönetimi. MFA/SSO ve operasyonel görünürlük ilhamı; ajan zorunluluğu BeRDP'nin tercih edilen çalışma biçimine uygun değil.
- [RustDesk](https://github.com/rustdesk/rustdesk): kendi sunucusunda barındırılabilen uzak erişim yaklaşımı. NAT/relay ürün ihtiyaçları için karşılaştırma; native Windows RDP'nin doğrudan yerine taşımak ayrı ürün kapsamıdır.

Kaynak veya görsel kopyalamadan önce her projenin lisansı ayrıca değerlendirilmelidir; BeRDP'nin MIT lisansı üçüncü taraf kodunu otomatik olarak MIT yapmaz.

.NET 8 desteği 10 Kasım 2026'da biteceği için çalışma .NET 10'a taşındı; destek ve güncelleme takvimi [Microsoft yaşam döngüsü](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) üzerinden izlenmelidir. Windows RDP uyarılarını bastırma yerine [Microsoft güvenlik uyarısı davranışı](https://learn.microsoft.com/en-us/windows-server/remote/remote-desktop-services/remotepc/understanding-security-warnings) korunur.
