# İlk yönetici ve kurtarma

1.6.0 hiçbir kullanıcı, parola veya demo makine üretmez. Setup yalnızca servisleri ve sunucuya özgü rastgele JWT anahtarını yapılandırır.

## İlk kurulum

Sunucuda yetkili yönetici PowerShell oturumu açın. Kurulu yapılandırmanın okunması için doğru dizinde çalıştırın:

```powershell
Set-Location 'C:\Program Files\BeRDP\Server\Backend'
.\BeRdp.Backend.exe --bootstrap-admin kurum-admin
```

Parola ekranda gösterilmeden istenir. En az 14 karakter, UTF-8 olarak en fazla 72 byte olmalıdır. Parolayı komut satırına, setup parametresine veya versiyon kontrolüne koymayın. Komut yalnızca boş kullanıcı veritabanında çalışır ve web sunucusu başlatmadan çıkar.

Yerel geliştirmede aynı işlem `dotnet run --project src/BeRdp.Backend -- --bootstrap-admin kurum-admin` ile yapılabilir; önce güvenli `Jwt__Key` ve ayrı `Storage__DataDirectory` ayarlayın. Üretim anahtarını kullanmayın.

## Yerel yönetici kurtarma

```powershell
Set-Location 'C:\Program Files\BeRDP\Server\Backend'
.\BeRdp.Backend.exe --reset-admin kurum-admin
```

Bu komut yalnız mevcut bir yerel yönetici için parola yeniler, konsol erişimini açar ve güvenlik damgasını değiştirerek önceki token'ları geçersiz kılar. Sunucu dosyalarına erişimi olan yerel yöneticiler zaten güven sınırının içindedir; bu komut uzaktan API olarak sunulmaz.

## Eski demo kurulumlarından yükseltme

`DemoSeedVersion` işaretli eski veritabanlarında bilinen, değiştirilmemiş demo parolaları rastgele erişilemez hash'lerle değiştirilir, konsol erişimi ve oturumları iptal edilir. Özel değiştirilmiş parolalar korunur. Otomatik kullanıcı/makine eklenmez veya veri silinmez. Eski hesapları ve makine atamalarını yönetici inceleyip gereksiz kayıtları kaldırmalıdır. Bilinen parola eşleştirmeleri yalnız bu geçiş için kaynak kodda kalır; geçerli giriş bilgileri değildir.

Yükseltmeden önce veritabanını ve anahtarları yedekleyin. Gerekirse yukarıdaki kurtarma komutunu kullanın. Test fixture'ları yalnız `tests/` altında derlenir ve setup'a dahil edilmez.
