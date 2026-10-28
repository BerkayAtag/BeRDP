# Güvenlik incelemesi ve düzeltme kaydı

İlk kaynak kod taraması altı bulgu (3 yüksek, 3 orta) belirledi. Bu belge sonradan yapılan kaynak düzeltmelerini kaydeder; mühürlenmiş ilk tarama raporunu değiştirmez ve bağımsız pentest sertifikası değildir.

| Bulgu | Kaynak düzeltmesi | Doğrulama / kalan sınır |
|---|---|---|
| Bilinen yönetici/demo hesapları | Üretim seeder kaldırıldı; bootstrap ve legacy quarantine | Boş DB, eski parola iptali ve kurtarma regresyonu; gerçek eski müşteri DB'si pilotu gerekli |
| Uzak HTTP ile kimlik aktarımı | Kurulum loopback; uzak client/API HTTP reddi | API ve URL testleri; kurum TLS proxy kabulü gerekli |
| Anahtar/DB dosyalarının miras ACL erişimi | Özel storage ACL: SYSTEM, Administrators, servis kimliği | Kaynak incelemesi ve yapılandırma testi; standart Windows kullanıcı erişim testi pilotta |
| Yabancı domain/SAM karışması | Yapılandırılmış domain doğrulaması; tam principal çözümleme | Login matris testleri; gerçek çok-domain AD kabulü gerekli |
| JWT içindeki eski AD grupları | Her doğrulanmış istekte güncel AD grup sorgusu, hata halinde erişim reddi | Mock directory ile grup yenileme/iptal ve hata halinde ret regresyonları; canlı AD grup iptali ve yük ölçümü pilotta |
| Portal keşfinde HTTPS düşürme | HTTPS ve aynı-host kısıtı, HTTP redirect kapalı | URL regresyonları |

Ek düzeltmeler: native bağlantı öncesi atama yenileme, son yönetici koruması, konsol yetki kontrolü, Windows RDP izin politikalarını koruma, tema event aboneliği temizliği, aynı Windows oturumunda tek istemci, CSP ve güvenlik başlıkları.

## Güven sınırları

- Yerel sunucu yöneticisi, DB/anahtarlar ve servis hesabı güvenilir işletim sınırıdır.
- Konsol Standard yetkisi kurum envanterini salt okunur görür; native bağlantı yalnız atanmış makineler içindir.
- AD grup sorgusu her istekte maliyet getirir. Şu an kısa TTL cache yoktur; performans için eklenirse iptal gecikmesi açıkça tanımlanmalıdır.
- Doğrudan mstsc erişimini BeRDP engelleyemez; Windows/GPO/ağ geçidi politikaları zorunludur.
- Mevcut RDP oturumları atama iptalinde sonlanmaz; bu sürüm oturum broker'ı değildir.
- SSO, servis ACL'leri, EDR/WDAC, VM/NLA ve proxy davranışları gerçek ortamda doğrulanmadan evrensel kurumsal uyumluluk iddiası yapılmaz.
