# BeRDP API Dokümantasyonu

Production: `https://berdp.example.test/api`; service loopback: `http://127.0.0.1:5001`. Remote HTTP is rejected.

## Kimlik Doğrulama

- Endpoint: `POST /login`
- Korumalı endpointler için:

```http
Authorization: Bearer {token}
```

## Ana Endpointler

Korumalı çağrılarda başlık biçimi `Authorization: Bearer <token>` olmalıdır.
Parola/kayıtlı AD kimliği değişikliği security-stamp üzerinden oturumları iptal
edebilir; yalnız setup sürümünün değişmesi token iptali anlamına gelmez.

### `POST /login`

Lokal veya AD kaynaklı kullanıcı login. Kullanıcı adı üç biçimde kabul edilir:

| Biçim | Örnek |
|-------|-------|
| Düz kullanıcı adı (SAM) | `user01` |
| Domain öneki (NetBIOS veya DNS) | `EXAMPLE\user01`, `example.test\user01` |
| UPN (NetBIOS veya DNS soneki) | `user01@example.test`, `user01@EXAMPLE` |

Çözümleme sırası:

1. Tam kullanıcı adı veya kayıtlı UPN eşleşmesi.
2. Domain öneki/UPN soneki ile AD domain eşleşmesi (`EXAMPLE` ve `example.test` eşdeğerdir; farklı FQDN'ler eşleşmez).
3. Yerel hesap eşleşmesi (domain öneki/UPN soneki ile de adreslenebilir).

Kurallar:

- Parola her biçimde zorunludur ve doğrulanır.
- Aynı düz kullanıcı adı birden fazla AD domaininde varsa `400 Bad Request` döner; `domain\kullanıcı` veya UPN istenir.
- Hatalı kimlik/parola `401`, AD altyapısı kullanılamıyorsa `503` döner.
- Aynı IP'den dakikada en fazla **60** giriş denemesi yapılabilir; aşımda `429` döner. Limit `appsettings.json` içindeki `Security:LoginRateLimitPerMinute` ile 10–600 arasında değiştirilebilir (paylaşımlı NAT/VDI çıkışları için yüksek tutulmalıdır).
- Başarılı login, `CanAccessAdminConsole = false` olsa bile token döndürür. Konsol erişimi Admin Console arayüzü ve korumalı yönetim endpoint'lerinde kontrol edilir; masaüstü istemcisi bu token ile çalışır.
- `canAccessAdminConsole` ve `consoleRole` yanıtta döner. Konsol yetkisi her istekte veritabanından yeniden okunur; yetki kaldırıldığında eski token bir sonraki istekte yetkisiz sayılır.

Response:

```json
{
  "userId": "guid",
  "username": "corp\\j.doe",
  "displayName": "John Doe",
  "isAdmin": false,
  "isActiveDirectoryUser": true,
  "rdpUsername": "j.doe@corp.local",
  "token": "jwt-token",
  "lastLoginAtUtc": "2026-06-15T09:00:00Z",
  "canAccessAdminConsole": true,
  "consoleRole": "Standard"
}
```

### `GET /machines`

`isOnline`, backend'den hedefin `rdpPort` TCP portuna erişimdir; ICMP/ping değildir.
İstemci tüm sayfaları `skip`/`take` kullanarak çeker.

Konsol erişimi olmayan kullanıcı yalnız atanmış makineleri görür. Konsol yetkisi olanlar envanteri okuyabilir; `assignedOnly=true` herkes için atama filtresi uygular.
AD kullanıcıları için bireysel atamaya ek olarak AD grup entitlement’ları da dikkate alınır.
Filtre parametreleri:

- `search`
- `skip`
- `take`
- `assignedOnly=true`: admin için de yalnız kendi bireysel/grup atamalarını
  döndürür. Standart kullanıcı bu parametre olmadan da daima filtrelenir.

Response alanlarında `assignedUserCount` yanında `assignedAdGroupCount` da döner.
`rdpHostName`, Windows SSO için kullanılan tam DNS adıdır; `ipAddress` TCP
erişilebilirlik kontrolünde kullanılır.

### `POST /login/windows`

Server Setup `/SSO=1` ile etkinleştirilen Windows Negotiate endpoint'i.
Parola/Hello PIN payload'u almaz. Windows SID AD'de çözümlenir; kayıtlı domain
(NetBIOS veya DNS) ve tam UPN/canonical SAM hesabı tekil eşleşirse normal login
response'u döner. Konsol erişimi olmayan kullanıcılar da masaüstü istemcisi için
token alır; konsol yetkisi ayrıca denetlenir. Kapalı platform/özellik için `503`,
eşleşmeyen kullanıcı için `403`, Windows kimliği doğrulanmadığında Negotiate
challenge (`401`) döner. Chrome/Edge istekleri `credentials: "include"` kullanır;
uygun tarayıcı integrated-auth ilkeleri, DNS/SPN ve kesin CORS origin izinleri
gerekir.

### `POST /machines` (Admin)

Makine ekler.

Makine eklenirken entitlement aynı request içinde verilebilir:

```json
{
  "name": "VDI-1001",
  "ipAddress": "10.10.30.25",
  "rdpHostName": "vdi-1001.corp.local",
  "rdpPort": 3389,
  "entitledUserIds": ["guid-user-1", "guid-user-2"],
  "entitledAdGroups": [
    { "groupName": "vdi-finance", "domainName": "corp.local" },
    { "groupName": "vdi-reporting", "domainName": "corp.local" }
  ]
}
```

### `DELETE /machines/{id}` (Admin)

Makine siler.

### `GET /admin/machines/{id}` (Admin)

Makine adresi/DNS bilgileri ile `entitledUsers` (`userId`, `username`,
`displayName`) ve `entitledAdGroups` (`groupName`, `domainName`) listelerini döner.

### `PUT /machines/{id}` (Admin)

`POST /machines` ile aynı request biçimini kullanır. Mevcut makineyi ve erişim
listesini birlikte günceller; makine ID'si korunur. Kullanıcı ve grup listeleri
tam replacement'tır: boş liste önceki atamaları kaldırır. İşlem transaction
içinde kaydedilir, ardından `AssignmentsChanged` bildirimi gönderilir.

### `POST /machines/{id}/browser-session` (Domain kullanıcı)

Her çağrıda bireysel/grup ataması tekrar sorgulanır; admin rolü bu kontrolden
muaf değildir. Body almaz; istemci hedef IP/hostname veya parola seçemez.
`BrowserRdp` yapılandırması geçerliyse `{ "launchUrl": "https://gateway/...?data=...",
"expiresInSeconds": 60 }` döner. Yanıt `Cache-Control: no-store` taşır.
URL, Guacamole JSON-auth için imzalı/şifreli, tek makineye erişen bilettir;
BeRDP JWT'si veya Windows parolası içermez. URL'yi loglamayın/paylaşmayın.
Atama yoksa `404`, domain hesabı değilse `403`, gateway kapalı/geçersizse `503`.
60 saniyelik süre yeni gateway kimlik doğrulaması içindir, açık RDP oturumunun
süresi değildir. Mevcut gateway oturumlarını sonlandırma gateway yönetimindedir.

### `GET /users` (Admin)

Kullanıcı listesi (filter + segment desteği):

- `search`
- `source` (`all` / `ad` / `local`)
- `skip`
- `take`

### `POST /users` (Admin)

Lokal kullanıcı ekler.

### `POST /users/ad` (Admin)

AD kullanıcısını sisteme ekler veya günceller.

Mevcut kullanıcı yeniden eklendiğinde explicit admin rolü korunur; rol
değişiklikleri ayrı role endpointinden yapılmalıdır.

Request:

```json
{
  "samAccountName": "j.doe",
  "displayName": "John Doe",
  "domainName": "corp.local",
  "userPrincipalName": "j.doe@corp.local",
  "isAdmin": false
}
```

### `PUT /users/{id}/role` (Admin)

Admin / standard rol günceller.

Son lokal adminin yetkisini kaldırmak/silmek reddedilir. Rol değişikliği mevcut
JWT'nin bir sonraki API isteğine yansır; AD grup admin grant'leri de DB'den kontrol edilir.

Request:

```json
{
  "isAdmin": true
}
```

### `DELETE /users/{id}` (Admin)

Kullanıcı siler.

### `GET /assignments` (Admin)

Atamaları döner:

- `userId`
- `search`
- `skip`
- `take`

### `POST /assign-machine` (Admin)

Tekli makine atama.

### `DELETE /unassign-machine` (Admin)

Tekli makine atama kaldırma.

### `POST /assign-machines` (Admin)

Toplu makine atama.

Request:

```json
{
  "userId": "guid",
  "machineIds": ["guid1", "guid2", "guid3"]
}
```

### `POST /unassign-machines` (Admin)

Toplu makine atama kaldırma.

## Ölçeklenebilir Admin Endpointleri (5000+ Makine)

### `GET /admin/users` (Admin)

Sayfalı kullanıcı listesi:

- `search`
- `source` (`all` / `ad` / `local`)
- `page`
- `pageSize`

### `GET /admin/machines` (Admin)

Sayfalı makine listesi:

- `search`
- `page`
- `pageSize`

Response içinde makine bazında entitlement görünürlüğü için:

- `assignedUserDisplayNames`: bireysel entitlement kullanıcı adları
- `assignedAdGroups`: AD grup entitlement adları

### `GET /admin/assignments` (Admin)

Sayfalı atama listesi:

- `userId`
- `search`
- `page`
- `pageSize`

## Active Directory Endpointleri

Kullanıcı araması tanımlı OU ile sınırlandırılır. Gruplar kullanıcı OU'sunun
dışında bulunabileceği için domain kökünden aranır. AD config kaydı mevcut AD
oturumlarını iptal eder.

### `POST /ad/test` (Admin)

`PUT /ad/config` ile aynı gövdeyi alır, ayarları kaydetmeden gerçek LDAP bind/OU
okuma testi yapar. Formun yeni parolası kullanılabilir; `bindPassword: null`
kayıtlı parolayı korur. Parola trim edilmez.

### `GET /ad/config` (Admin)

AD konfigürasyonunu döner.

### `PUT /ad/config` (Admin)

AD konfigürasyonunu günceller.

Request:

```json
{
  "enabled": true,
  "domainName": "corp.local",
  "ldapContainer": "OU=VDI,DC=corp,DC=local",
  "useMachineCredentials": true,
  "bindUsername": "",
  "bindPassword": ""
}
```

### `GET /ad/users` (Admin)

AD user arama:

- `query` (min 2 karakter)
- `limit` (1-200)

### `GET /ad/groups` (Admin)

AD grup arama:

- `query` (min 2 karakter)
- `limit` (1-200)

## Yetkilendirme Endpointleri (Admin)

### `GET /admin/access-groups`

Admin yetkisi verilmiş AD grup listesini döner.

- `search` (opsiyonel)

### `POST /admin/access-groups`

Yeni AD grup admin yetkisi tanımlar.

```json
{
  "groupName": "vdi-admins",
  "domainName": "corp.local"
}
```

### `DELETE /admin/access-groups/{id}`

AD grup admin yetkisini kaldırır.

## SignalR

- Hub URL: `/hubs/assignments`
- Event: `AssignmentsChanged`

## Güvenlik Notları

- Parolalar bcrypt hash tutulur.
- AD kullanıcıları parola hash yerine AD doğrulaması kullanır.
- `Security:AllowedIpAddresses` listesi boş değilse backend sadece listedeki IP’lere cevap verir.
- Bind parolaları DB'de Data Protection ile saklanır; DB ile birlikte anahtarları
  da yedekleyin. Aktif oturumda dış AD grup üyeliği değişikliği yeniden login'e
  kadar JWT claim'lerinde kalabilir; kurumun iptal/oturum süresi politikasını uygulayın.

## Ek Kimlik / Sağlık Endpointleri

### `GET /health`

Backend/veritabanı erişimi başarılıysa `{"status":"ok"}` döner.

### `POST /account/password` (Giriş yapmış lokal kullanıcı)

```json
{
  "currentPassword": "<mevcut-parola>",
  "newPassword": "<yeni-parola>"
}
```

En az 14 karakter, UTF-8 olarak en fazla 72 byte. AD parolaları domain üzerinden
değiştirilir. Başarıda kullanıcının tüm eski tokenları iptal edilir.

AD/DC erişim ve grup sorgusu hataları login'de `503`, yanlış parola `401`, belirsiz
kısa kullanıcı adı `400` verir. Login aynı kaynak IP'den dakikada 20 istekle sınırlıdır.

## Swagger

- `http://localhost:5001/swagger`

### `GET /machines/{id}`

Native RDP başlatmadan önce güncel atamayı ve endpoint bilgisini döndürür. Atama yoksa 404; önceki bir listenin önbelleği bağlantı yetkisi olarak kabul edilmez. Yanıt `Cache-Control: no-store` içerir.
