using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BeRdp.Client.Services;

/// <summary>
/// Resolves the API base address when the installer or the administrator
/// entered the Admin Console (portal) address instead of the backend address.
/// The portal publishes its backend address in /config.js, so the client can
/// recover automatically instead of failing with HTTP 405.
/// </summary>
public sealed record BackendEndpoint(Uri Address, Uri ConfiguredAddress, string? Notice)
{
    public bool WasCorrected => Address != ConfiguredAddress;
}

public static partial class BackendEndpointResolver
{
    private static readonly HttpClient DefaultProbeClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    public static Task<BackendEndpoint> ResolveAsync(
        string configuredUrl,
        CancellationToken cancellationToken = default)
        => ResolveAsync(configuredUrl, null, cancellationToken);

    public static async Task<BackendEndpoint> ResolveAsync(
        string configuredUrl,
        Func<Uri, CancellationToken, Task<Uri?>>? discover,
        CancellationToken cancellationToken)
    {
        var configured = BackendAddressStore.Validate(configuredUrl);
        discover ??= DiscoverFromConfigScriptAsync;

        Uri? discovered = null;
        try
        {
            discovered = await discover(configured, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or
                                   InvalidOperationException or FormatException or UriFormatException)
        {
            // A portal that cannot be probed must not block login. The original
            // address is kept and the login error explains what to check.
        }

        discovered ??= MapLocalPortalPort(configured);
        if (discovered is null || discovered == configured)
        {
            return new BackendEndpoint(configured, configured, null);
        }

        return new BackendEndpoint(
            discovered,
            configured,
            $"Girilen adres yönetim konsoluna ait. API adresi otomatik olarak {discovered.AbsoluteUri.TrimEnd('/')} kullanılıyor.");
    }

    private static async Task<Uri?> DiscoverFromConfigScriptAsync(Uri configured, CancellationToken cancellationToken)
    {
        using var response = await DefaultProbeClient.GetAsync(
            new Uri(configured, "config.js"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseConfigScript(configured, payload);
    }

    /// <summary>
    /// Reads backendBaseUrl from the portal's config.js payload.
    /// </summary>
    public static Uri? ParseConfigScript(Uri configured, string script)
    {
        var match = BackendBaseUrlRegex().Match(script);
        if (!match.Success)
        {
            return null;
        }

        var raw = JsonSerializer.Deserialize<string>($"\"{match.Groups[1].Value}\"");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var backend = BackendAddressStore.Validate(raw);

        // Older consoles always report localhost. When the client reached the
        // portal through a real host name or IP, keep that host for the API.
        if (!IsLoopbackHost(configured.Host) && IsLoopbackHost(backend.Host))
        {
            backend = BackendAddressStore.Validate(
                new UriBuilder(backend) { Host = configured.Host }.Uri.ToString());
        }

        if (configured.Scheme == Uri.UriSchemeHttps && backend.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("HTTPS adresi HTTP bağlantısına düşürülemez.");
        if (!string.Equals(configured.Host, backend.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Otomatik keşif başka bir sunucuya kimlik bilgisi gönderemez. API adresini doğrudan yapılandırın.");
        return backend;
    }

    /// <summary>
    /// Maps the well-known local development portal ports to the local API
    /// ports. Only loopback addresses are mapped, so a remote reverse proxy is
    /// never redirected to an unexpected port.
    /// </summary>
    public static Uri? MapLocalPortalPort(Uri configured)
    {
        if (!IsLoopbackHost(configured.Host))
        {
            return null;
        }

        var mappedPort = configured.Port switch
        {
            5173 => 5001,
            7173 => 7001,
            _ => 0
        };

        return mappedPort == 0
            ? null
            : BackendAddressStore.Validate(new UriBuilder(configured) { Port = mappedPort }.Uri.ToString());
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));

    [GeneratedRegex("backendBaseUrl\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex BackendBaseUrlRegex();
}
