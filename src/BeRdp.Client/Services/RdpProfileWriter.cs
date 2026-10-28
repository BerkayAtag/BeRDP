using System.Net;
using System.Text;
using BeRdp.Client.Models;

namespace BeRdp.Client.Services;

public static class RdpProfileWriter
{
    public static string Endpoint(string ipAddress, int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Makinenin RDP adresi veya portu geçersiz.");
        }
        string host;
        if (IPAddress.TryParse(ipAddress, out var address))
        {
            host = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"[{address}]" : address.ToString();
        }
        else if (Uri.CheckHostName(ipAddress) == UriHostNameType.Dns && !ipAddress.Any(char.IsWhiteSpace))
        {
            host = ipAddress;
        }
        else
        {
            throw new InvalidOperationException("Makinenin RDP adresi geçersiz.");
        }
        return port == 3389 ? host : $"{host}:{port}";
    }

    public static string Build(
        string ipAddress, int port, string username,
        MachineRdpLocalResourceOptions options, IReadOnlyList<RdpMonitorViewModel> monitors,
        bool useWindowsSso = false, bool promptForCredentials = false)
    {
        if (username.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new InvalidOperationException("RDP kullanıcı adı geçersiz karakter içeriyor.");
        }
        var endpoint = Endpoint(ipAddress, port);
        if (useWindowsSso && (Uri.CheckHostName(ipAddress) != UriHostNameType.Dns ||
            !ipAddress.Contains('.') || string.Equals(ipAddress, "localhost", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Remote Credential Guard için hedefin tam domain DNS adı gerekir; IP kullanılamaz.");
        }
        var settings = new StringBuilder(RdpDisplaySettings.Build(options, monitors));
        foreach (var line in new[]
        {
            "compression:i:1", "keyboardhook:i:2", "displayconnectionbar:i:1", "enableworkspacereconnect:i:1",
            "session bpp:i:32",
            // Experience tab defaults, fixed for every session: Satellite speed
            // with automatic quality detection off; only font smoothing,
            // desktop composition and visual styles enabled, every other
            // performance option off. No Experience UI is shown to the user,
            // so these values are authoritative.
            "connection type:i:3", "networkautodetect:i:0", "bandwidthautodetect:i:0",
            "allow font smoothing:i:1", "allow desktop composition:i:1", "disable themes:i:0",
            "disable wallpaper:i:1", "disable full window drag:i:1", "disable menu anims:i:1",
            "bitmapcachepersistenable:i:0", "autoreconnection enabled:i:0",
            "disable cursor setting:i:0", "audiomode:i:0", "audiocapturemode:i:1", "videoplaybackmode:i:1",
            $"prompt for credentials:i:{(promptForCredentials ? 1 : 0)}", "promptcredentialonce:i:1", "authentication level:i:2",
            "negotiate security layer:i:1", "redirectposdevices:i:0", "redirectwebauthn:i:0"
        })
        {
            settings.AppendLine(line);
        }
        settings.AppendLine($"username:s:{username}");
        settings.AppendLine("enablecredsspsupport:i:1");
        settings.AppendLine($"full address:s:{endpoint}");
        settings.AppendLine($"alternate full address:s:{endpoint}");
        settings.AppendLine($"redirectclipboard:i:{Boolean(options.RedirectClipboard)}");
        settings.AppendLine($"redirectprinters:i:{Boolean(options.RedirectPrinters)}");
        settings.AppendLine($"redirectsmartcards:i:{Boolean(options.RedirectSmartCards)}");
        settings.AppendLine($"redirectcomports:i:{Boolean(options.RedirectComPorts)}");
        settings.AppendLine($"redirectdrives:i:{Boolean(options.RedirectDrives)}");
        settings.AppendLine($"drivestoredirect:s:{(options.RedirectDrives ? "*" : string.Empty)}");
        settings.AppendLine($"devicestoredirect:s:{(options.RedirectPnPDevices ? "*" : string.Empty)}");
        return settings.ToString();
    }

    private static int Boolean(bool value) => value ? 1 : 0;
}
