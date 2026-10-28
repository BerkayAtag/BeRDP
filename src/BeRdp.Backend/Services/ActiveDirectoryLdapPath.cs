using System.Text;
using System.Text.RegularExpressions;

namespace BeRdp.Backend.Services;

public static class ActiveDirectoryLdapPath
{
    public static string DomainDistinguishedName(string domainName) =>
        string.Join(",", domainName.Trim().Split('.').Select(part => $"DC={part}"));

    public static (string Server, string DistinguishedName) Resolve(
        string domainName,
        string? container)
    {
        var server = domainName.Trim();
        var distinguishedName = container?.Trim() ?? string.Empty;
        if (distinguishedName.StartsWith("LDAP://", StringComparison.OrdinalIgnoreCase))
        {
            distinguishedName = distinguishedName["LDAP://".Length..];
            var separator = distinguishedName.IndexOf('/');
            var firstEquals = distinguishedName.IndexOf('=');
            if (separator >= 0 && (firstEquals < 0 || separator < firstEquals))
            {
                server = distinguishedName[..separator];
                distinguishedName = distinguishedName[(separator + 1)..];
            }
        }

        return (server, string.IsNullOrEmpty(distinguishedName)
            ? DomainDistinguishedName(domainName)
            : distinguishedName);
    }

    public static string? Validate(string domainName, string? container)
    {
        if (string.Equals(container?.Trim(), "LDAP://", StringComparison.OrdinalIgnoreCase))
        {
            return "LDAP adresinde sunucu veya ayırt edici ad eksik.";
        }
        if (Uri.CheckHostName(domainName.Trim()) != UriHostNameType.Dns)
        {
            return "Domain adı geçersiz (örnek: example.test).";
        }

        var (server, distinguishedName) = Resolve(domainName, container);
        if (Uri.CheckHostName(server) != UriHostNameType.Dns)
        {
            return "LDAP sunucu adı geçersiz. LDAP://sunucu/OU=...,DC=... biçimini kullanın.";
        }

        var components = new List<string>();
        var current = new StringBuilder();
        var escaped = false;
        foreach (var character in distinguishedName)
        {
            if (character == ',' && !escaped)
            {
                components.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(character);
            escaped = character == '\\' && !escaped;
        }

        if (escaped)
        {
            return "LDAP Container eksik bir kaçış karakteri içeriyor.";
        }

        components.Add(current.ToString().Trim());
        if (components.Any(component =>
                !Regex.IsMatch(component, @"^[A-Za-z][A-Za-z0-9.-]*=.+$", RegexOptions.CultureInvariant)))
        {
            return "LDAP Container geçersiz (örnek: OU=People,OU=Example,DC=example,DC=test).";
        }

        var expectedSuffix = DomainDistinguishedName(domainName).Split(',');
        if (components.Count < expectedSuffix.Length ||
            !components.TakeLast(expectedSuffix.Length).SequenceEqual(expectedSuffix, StringComparer.OrdinalIgnoreCase))
        {
            return $"LDAP Container domain ile bitmelidir: {string.Join(",", expectedSuffix)}.";
        }

        return null;
    }
}
