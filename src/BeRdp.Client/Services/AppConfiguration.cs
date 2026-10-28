using Microsoft.Extensions.Configuration;
using Microsoft.Win32;

namespace BeRdp.Client.Services;

public class AppConfiguration
{
    public string BackendBaseUrl { get; init; } = "http://localhost:5001";
    public int PollingSeconds { get; init; } = 30;
    public bool UseWindowsSso { get; init; }

    public static AppConfiguration Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var section = configuration.GetSection("Client");
        var pollingSeconds = int.TryParse(section["PollingSeconds"], out var value) ? Math.Clamp(value, 5, 300) : 30;

        using var deployment = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\BeRDP\Client");
        var managedValue = deployment?.GetValue("BackendBaseUrl");
        if (managedValue is not null && managedValue is not string)
        {
            throw new InvalidOperationException("Kurulumun sunucu adresi geçersiz; Client Setup ile yeniden yapılandırın.");
        }
        var backendUrl = managedValue is string managedUrl
            ? managedUrl
            : section["BackendBaseUrl"] ?? "http://localhost:5001";
        var ssoValue = deployment?.GetValue("UseWindowsSso");
        if (ssoValue is not null && (ssoValue is not int flag || flag is < 0 or > 1))
        {
            throw new InvalidOperationException("Kurulumun SSO ayarı geçersiz; Client Setup ile yeniden yapılandırın.");
        }
        var useWindowsSso = false;
        if (ssoValue is int enabled)
        {
            useWindowsSso = enabled == 1;
        }
        else if (section["UseWindowsSso"] is string configuredSso &&
                 !bool.TryParse(configuredSso, out useWindowsSso))
        {
            throw new InvalidOperationException("UseWindowsSso ayarı true veya false olmalıdır.");
        }
        var address = BackendAddressStore.Validate(backendUrl);
        if (useWindowsSso && (address.HostNameType != UriHostNameType.Dns || address.IsLoopback || !address.Host.Contains('.')))
        {
            throw new InvalidOperationException("SSO için sunucunun domain DNS adını kullanın; IP/localhost kullanılamaz.");
        }

        return new AppConfiguration
        {
            BackendBaseUrl = address.AbsoluteUri.TrimEnd('/'),
            PollingSeconds = pollingSeconds,
            UseWindowsSso = useWindowsSso
        };
    }
}
