using System.Security.Cryptography;
using System.Text.Json;
using BeRdp.Backend.Models;

namespace BeRdp.Backend.Services;

public sealed class BrowserRdpGateway(IConfiguration configuration)
{
    public bool Enabled => configuration.GetValue<bool>("BrowserRdp:Enabled");

    public string CreateLaunchUrl(User user, Machine machine, DateTimeOffset now)
    {
        if (!Enabled) throw new InvalidOperationException("Tarayıcı RDP gateway'i etkinleştirilmemiş.");
        var baseUrl = configuration["BrowserRdp:BaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var gateway) ||
            gateway.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(gateway.UserInfo) ||
            !string.IsNullOrEmpty(gateway.Query) || !string.IsNullOrEmpty(gateway.Fragment))
        {
            throw new InvalidOperationException("BrowserRdp:BaseUrl geçerli, sorgusuz bir HTTPS Guacamole adresi olmalıdır.");
        }
        var secret = configuration["BrowserRdp:JsonSecretKey"];
        if (secret is null || secret.Length != 32 || !secret.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("BrowserRdp:JsonSecretKey gateway ile aynı 32 haneli hexadecimal anahtar olmalıdır.");
        }
        var key = Convert.FromHexString(secret);
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                // A unique identity prevents two machine launches merging gateway permissions.
                username = $"berdp-{user.Id:N}-{Guid.NewGuid():N}",
                expires = now.AddSeconds(60).ToUnixTimeMilliseconds(),
                connections = new Dictionary<string, object>
                {
                    [machine.Name] = new
                    {
                        protocol = "rdp",
                        parameters = new Dictionary<string, string>
                        {
                            ["hostname"] = machine.RdpHostName ?? machine.IpAddress,
                            ["port"] = machine.RdpPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["username"] = user.UserPrincipalName ?? user.Username,
                            ["security"] = "nla",
                            ["disable-auth"] = "false",
                            ["ignore-cert"] = "false",
                            ["enable-wallpaper"] = "false",
                            ["enable-font-smoothing"] = "true",
                            ["resize-method"] = "display-update"
                        }
                    }
                }
            });
            var signature = HMACSHA256.HashData(key, payload);
            var signed = new byte[signature.Length + payload.Length];
            signature.CopyTo(signed, 0);
            payload.CopyTo(signed, signature.Length);
            using var aes = Aes.Create();
            aes.Key = key;
            // Guacamole JSON-auth specifies a zero IV and an HMAC prefix.
            var encrypted = aes.EncryptCbc(signed, new byte[16], PaddingMode.PKCS7);
            return gateway.AbsoluteUri.TrimEnd('/') + "/?data=" +
                Uri.EscapeDataString(Convert.ToBase64String(encrypted));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
