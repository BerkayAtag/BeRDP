using System.IO;

namespace BeRdp.Client.Services;

public static class BackendAddressStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeRDP", "Client", "backend-url.txt");

    public static Uri Validate(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("Sunucu adresi geçerli bir HTTP/HTTPS adresi olmalıdır (örnek: https://berdp.example.test/api).");
        }

        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
            throw new InvalidOperationException("Uzak sunucu için HTTPS zorunludur. Kurumun sertifikalı API adresini kullanın.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public static string Load(string defaultUrl) =>
        Validate(File.Exists(FilePath) ? File.ReadAllText(FilePath) : defaultUrl).AbsoluteUri.TrimEnd('/');

    public static void Save(string value)
    {
        var normalized = Validate(value).AbsoluteUri.TrimEnd('/');
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, normalized);
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
