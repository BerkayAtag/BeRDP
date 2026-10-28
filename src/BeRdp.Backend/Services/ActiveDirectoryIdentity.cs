using BeRdp.Backend.Models;

namespace BeRdp.Backend.Services;

public static class ActiveDirectoryIdentity
{
    /// <summary>
    /// Compares stored and directory domain names. NetBIOS aliases (EXAMPLE) match
    /// their DNS domain (example.test), but two different FQDNs never match.
    /// </summary>
    public static bool DomainMatches(string? storedDomain, string? directoryDomain)
    {
        if (string.IsNullOrWhiteSpace(storedDomain) || string.IsNullOrWhiteSpace(directoryDomain))
        {
            return false;
        }

        var stored = storedDomain.Trim().TrimEnd('.');
        var directory = directoryDomain.Trim().TrimEnd('.');
        if (string.Equals(stored, directory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!stored.Contains('.') &&
            string.Equals(stored, directory.Split('.')[0], StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !directory.Contains('.') &&
               string.Equals(directory, stored.Split('.')[0], StringComparison.OrdinalIgnoreCase);
    }

    public static bool Matches(User imported, ActiveDirectoryDirectoryUser directory)
    {
        if (!imported.IsActiveDirectoryUser ||
            !DomainMatches(imported.DomainName, directory.DomainName))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(imported.UserPrincipalName))
        {
            return string.Equals(imported.UserPrincipalName, directory.UserPrincipalName, StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(imported.Username, directory.SamAccountName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(imported.Username, $"{directory.DomainName}\\{directory.SamAccountName}", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(directory.UserPrincipalName) &&
             string.Equals(imported.Username, directory.UserPrincipalName, StringComparison.OrdinalIgnoreCase));
    }
}
