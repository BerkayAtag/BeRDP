# Ağ sınırları

| Kaynak → hedef | Port | Amaç |
|---|---|---|
| İstemci / tarayıcı → TLS proxy | TCP 443 | BeRDP API ve portal |
| Aynı sunucudaki proxy → API | Loopback TCP 5001 | Yerel HTTP aktarımı |
| Aynı sunucudaki proxy → portal | Loopback TCP 5173 | Yerel web dosyaları |
| Windows istemcisi → atanmış VM | TCP/UDP 3389 veya yönetilen port | Native RDP |
| Backend → AD | Kurum AD/Kerberos/DNS kuralları | Kimlik/grup çözümleme |
| Tarayıcı → Guacamole (opsiyonel) | HTTPS 443 | Tarayıcı RDP |
| Guacamole → VM (opsiyonel) | TCP 3389 | RDP aktarımı |

5001 ve 5173 dış ağa açılmaz. Proxy yalnız aynı sunucudan loopback erişir. TLS/WAF ve gerçek istemci IP hız sınırı proxy'de uygulanır. AD portları kurum topolojisi ve kullanılan kimlik yöntemine göre belirlenir; geniş `Any` kuralları oluşturmayın.

BeRDP envanter ataması Windows oturum açma hakkının yerine geçmez. RDP/NLA, Windows Remote Desktop Users üyeliği, GPO, RD Gateway CAP/RAP ve ağ segmentasyonu ayrıca yönetilmelidir. Açık RDP oturumu atama kaldırılınca otomatik kapanmaz.
