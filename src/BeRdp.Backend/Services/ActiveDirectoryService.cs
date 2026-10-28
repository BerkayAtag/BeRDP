using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace BeRdp.Backend.Services;

public class ActiveDirectoryService(ILogger<ActiveDirectoryService> logger) : IActiveDirectoryService
{
    public Task<ActiveDirectoryDirectoryUser> GetUserBySidAsync(
        string sid,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Active Directory Windows gerektirir.");
        EnsureAdSearchAvailable(configuration, "AD SID doğrulaması Windows gerektirir.");
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(GetUserBySidWindows(sid, configuration));
        }
        catch (Exception ex) when (ex is PrincipalException or COMException)
        {
            throw ReportDirectoryError("SID doğrulaması", ex);
        }
    }

    [SupportedOSPlatform("windows")]
    private static ActiveDirectoryDirectoryUser GetUserBySidWindows(string sid, ActiveDirectoryConfiguration configuration)
    {
        using var context = CreatePrincipalContext(configuration);
        using var user = UserPrincipal.FindByIdentity(context, IdentityType.Sid, sid);
        if (user is null || user.Enabled != true || string.IsNullOrWhiteSpace(user.SamAccountName) ||
            user.Sid is null || !string.Equals(user.Sid.Value, sid, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Windows hesabı yapılandırılan AD kapsamında doğrulanamadı.");
        }
        return new ActiveDirectoryDirectoryUser
        {
            SamAccountName = user.SamAccountName,
            UserPrincipalName = user.UserPrincipalName,
            DisplayName = user.DisplayName ?? user.SamAccountName,
            DistinguishedName = user.DistinguishedName,
            Sid = user.Sid.Value,
            DomainName = configuration.DomainName
        };
    }

    public Task TestConnectionAsync(
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Active Directory Windows gerektirir.");
        EnsureAdSearchAvailable(configuration, "Active Directory bağlantı testi sadece Windows üzerinde desteklenir.");
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            TestConnectionWindows(configuration);
            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is PrincipalException or COMException)
        {
            throw ReportDirectoryError("bağlantı testi", ex);
        }
    }

    public Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Active Directory Windows gerektirir.");
        EnsureAdSearchAvailable(configuration, "Active Directory doğrulaması sadece Windows üzerinde desteklenir.");

        var normalizedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(ValidateCredentialsWindows(normalizedUsername, password, configuration));
        }
        catch (Exception ex) when (ex is PrincipalException or COMException)
        {
            throw ReportDirectoryError("kimlik doğrulama", ex);
        }
    }

    public Task<IReadOnlyList<ActiveDirectoryDirectoryUser>> SearchUsersAsync(
        string query,
        int limit,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Active Directory Windows gerektirir.");
        EnsureAdSearchAvailable(configuration, "Active Directory kullanıcı araması sadece Windows üzerinde desteklenir.");

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<ActiveDirectoryDirectoryUser>>(Array.Empty<ActiveDirectoryDirectoryUser>());
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var users = SearchUsersWindows(normalizedQuery, Math.Clamp(limit, 1, 200), configuration);
            return Task.FromResult<IReadOnlyList<ActiveDirectoryDirectoryUser>>(users);
        }
        catch (Exception ex) when (ex is PrincipalException or COMException)
        {
            throw ReportDirectoryError("kullanıcı araması", ex);
        }
    }

    public Task<IReadOnlyList<ActiveDirectoryDirectoryGroup>> SearchGroupsAsync(
        string query,
        int limit,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Active Directory Windows gerektirir.");
        EnsureAdSearchAvailable(configuration, "Active Directory grup araması sadece Windows üzerinde desteklenir.");

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<ActiveDirectoryDirectoryGroup>>(Array.Empty<ActiveDirectoryDirectoryGroup>());
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var groups = SearchGroupsWindows(normalizedQuery, Math.Clamp(limit, 1, 200), configuration);
            return Task.FromResult<IReadOnlyList<ActiveDirectoryDirectoryGroup>>(groups);
        }
        catch (Exception ex) when (ex is PrincipalException or COMException)
        {
            throw ReportDirectoryError("grup araması", ex);
        }
    }

    public Task<IReadOnlyList<string>> GetUserGroupNamesAsync(
        string username,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Active Directory Windows gerektirir.");
        EnsureAdSearchAvailable(configuration, "Active Directory grup üyeliği sadece Windows üzerinde desteklenir.");

        var normalizedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var groupNames = GetUserGroupNamesWindows(normalizedUsername, configuration);
            return Task.FromResult(groupNames);
        }
        catch (Exception ex) when (ex is PrincipalException or COMException)
        {
            throw ReportDirectoryError("grup üyeliği sorgusu", ex);
        }
    }

    private static void EnsureAdSearchAvailable(ActiveDirectoryConfiguration configuration, string platformMessage)
    {
        if (!configuration.Enabled)
        {
            throw new InvalidOperationException("Active Directory entegrasyonu kapalı.");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException(platformMessage);
        }

        if (string.IsNullOrWhiteSpace(configuration.DomainName))
        {
            throw new InvalidOperationException("Active Directory domain bilgisi tanımlı değil.");
        }
        var ldapError = ActiveDirectoryLdapPath.Validate(configuration.DomainName, configuration.LdapContainer);
        if (ldapError is not null)
        {
            throw new InvalidOperationException(ldapError);
        }

        if (!configuration.UseMachineCredentials &&
            (string.IsNullOrWhiteSpace(configuration.BindUsername) || string.IsNullOrEmpty(configuration.BindPassword)))
        {
            throw new InvalidOperationException("Bind kullanıcı adı ve parola zorunludur.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void TestConnectionWindows(ActiveDirectoryConfiguration configuration)
    {
        using var rootEntry = CreateDirectoryEntry(configuration);
        using var searcher = new DirectorySearcher(rootEntry)
        {
            Filter = "(objectClass=*)",
            SearchScope = SearchScope.Base,
            ClientTimeout = TimeSpan.FromSeconds(10),
            ServerTimeLimit = TimeSpan.FromSeconds(10)
        };
        if (searcher.FindOne() is null)
        {
            throw new InvalidOperationException("LDAP Container okunamadı. OU adı ve okuma yetkisini kontrol edin.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool ValidateCredentialsWindows(
        string normalizedUsername,
        string password,
        ActiveDirectoryConfiguration configuration)
    {
        using var principalContext = CreatePrincipalContext(configuration);
        var identifier = LoginIdentifier.Parse(normalizedUsername);
        if (!identifier.IsUpn && identifier.Domain is not null &&
            !ActiveDirectoryIdentity.DomainMatches(identifier.Domain, configuration.DomainName))
            return false;
        using var directoryUser = ResolveUserPrincipal(principalContext, normalizedUsername);
        if (directoryUser is null || directoryUser.Enabled != true || string.IsNullOrEmpty(directoryUser.SamAccountName))
            return false;
        return principalContext.ValidateCredentials(directoryUser.SamAccountName, password, ContextOptions.Negotiate);

    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<ActiveDirectoryDirectoryUser> SearchUsersWindows(
        string query,
        int limit,
        ActiveDirectoryConfiguration configuration)
    {
        using var rootEntry = CreateDirectoryEntry(configuration);
        using var directorySearcher = new DirectorySearcher(rootEntry)
        {
            Filter = BuildUserSearchFilter(query),
            SearchScope = SearchScope.Subtree,
            ClientTimeout = TimeSpan.FromSeconds(10),
            ServerTimeLimit = TimeSpan.FromSeconds(10),
            PageSize = Math.Min(limit, 100),
            SizeLimit = limit
        };

        directorySearcher.PropertiesToLoad.Add("sAMAccountName");
        directorySearcher.PropertiesToLoad.Add("displayName");
        directorySearcher.PropertiesToLoad.Add("userPrincipalName");
        directorySearcher.PropertiesToLoad.Add("distinguishedName");
        directorySearcher.PropertiesToLoad.Add("objectSid");

        var users = new List<ActiveDirectoryDirectoryUser>(limit);
        using var results = directorySearcher.FindAll();
        foreach (SearchResult result in results)
        {
            var samAccountName = ReadPropertyValue(result, "sAMAccountName");
            if (string.IsNullOrWhiteSpace(samAccountName))
            {
                continue;
            }

            var displayName = ReadPropertyValue(result, "displayName");
            var userPrincipalName = ReadPropertyValue(result, "userPrincipalName");
            var distinguishedName = ReadPropertyValue(result, "distinguishedName");

            users.Add(new ActiveDirectoryDirectoryUser
            {
                SamAccountName = samAccountName,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? samAccountName : displayName,
                UserPrincipalName = NormalizeNullable(userPrincipalName),
                DistinguishedName = NormalizeNullable(distinguishedName),
                Sid = ReadSecurityIdentifier(result, "objectSid"),
                DomainName = configuration.DomainName
            });

            if (users.Count >= limit)
            {
                break;
            }
        }

        return users;
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<ActiveDirectoryDirectoryGroup> SearchGroupsWindows(
        string query,
        int limit,
        ActiveDirectoryConfiguration configuration)
    {
        using var rootEntry = CreateDirectoryEntry(configuration, useUserContainer: false);
        using var directorySearcher = new DirectorySearcher(rootEntry)
        {
            Filter = BuildGroupSearchFilter(query),
            SearchScope = SearchScope.Subtree,
            ClientTimeout = TimeSpan.FromSeconds(10),
            ServerTimeLimit = TimeSpan.FromSeconds(10),
            PageSize = Math.Min(limit, 100),
            SizeLimit = limit
        };

        directorySearcher.PropertiesToLoad.Add("sAMAccountName");
        directorySearcher.PropertiesToLoad.Add("cn");
        directorySearcher.PropertiesToLoad.Add("distinguishedName");

        var groups = new List<ActiveDirectoryDirectoryGroup>(limit);
        using var results = directorySearcher.FindAll();
        foreach (SearchResult result in results)
        {
            var samAccountName = ReadPropertyValue(result, "sAMAccountName");
            var commonName = ReadPropertyValue(result, "cn");
            var groupName = string.IsNullOrWhiteSpace(samAccountName) ? commonName : samAccountName;
            if (string.IsNullOrWhiteSpace(groupName))
            {
                continue;
            }

            groups.Add(new ActiveDirectoryDirectoryGroup
            {
                GroupName = groupName,
                DomainName = configuration.DomainName,
                DistinguishedName = NormalizeNullable(ReadPropertyValue(result, "distinguishedName"))
            });

            if (groups.Count >= limit)
            {
                break;
            }
        }

        return groups;
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> GetUserGroupNamesWindows(
        string username,
        ActiveDirectoryConfiguration configuration)
    {
        using var principalContext = CreatePrincipalContext(configuration);
        using var user = ResolveUserPrincipal(principalContext, username);
        if (user is null || user.Enabled != true)
        {
            throw new InvalidOperationException("AD kullanıcısı etkin değil veya yapılandırılan OU içinde bulunamadı.");
        }

        var groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var groups = user.GetAuthorizationGroups();
        foreach (var group in groups)
        {
            using var disposableGroup = group;
            AddGroupCandidate(groupNames, disposableGroup.SamAccountName);
            AddGroupCandidate(groupNames, disposableGroup.Name);
            if (!string.IsNullOrWhiteSpace(disposableGroup.SamAccountName))
            {
                var groupDomain = string.Join(".", (disposableGroup.DistinguishedName ?? string.Empty)
                    .Split(',')
                    .Where(part => part.TrimStart().StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
                    .Select(part => part.Trim()[3..]));
                if (!string.IsNullOrEmpty(groupDomain))
                {
                    AddGroupCandidate(groupNames, $"{groupDomain}\\{disposableGroup.SamAccountName}");
                }
            }
        }

        return groupNames
            .Select(x => x.ToLowerInvariant())
            .ToList();
    }

    [SupportedOSPlatform("windows")]
    private static PrincipalContext CreatePrincipalContext(ActiveDirectoryConfiguration configuration)
    {
        var (server, container) = ActiveDirectoryLdapPath.Resolve(configuration.DomainName, configuration.LdapContainer);
        if (configuration.UseMachineCredentials)
        {
            return new PrincipalContext(ContextType.Domain, server, container);
        }

        var bindPassword = configuration.BindPassword ?? string.Empty;
        return new PrincipalContext(ContextType.Domain, server, container, configuration.BindUsername, bindPassword);
    }

    [SupportedOSPlatform("windows")]
    private static DirectoryEntry CreateDirectoryEntry(
        ActiveDirectoryConfiguration configuration,
        bool useUserContainer = true)
    {
        var (server, container) = ActiveDirectoryLdapPath.Resolve(configuration.DomainName, configuration.LdapContainer);
        var ldapPath = $"LDAP://{server}/{(useUserContainer ? container : ActiveDirectoryLdapPath.DomainDistinguishedName(configuration.DomainName))}";
        var authentication = AuthenticationTypes.Secure | AuthenticationTypes.Signing | AuthenticationTypes.Sealing;
        if (configuration.UseMachineCredentials)
        {
            return new DirectoryEntry(ldapPath, null, null, authentication);
        }

        return new DirectoryEntry(ldapPath, configuration.BindUsername, configuration.BindPassword, authentication);
    }

    private InvalidOperationException ReportDirectoryError(string operation, Exception exception)
    {
        logger.LogWarning(exception, "Active Directory {Operation} failed.", operation);
        var hint = exception switch
        {
            PrincipalServerDownException => "Domain DNS çözümlemesini ve DC/firewall erişimini kontrol edin.",
            COMException { HResult: unchecked((int)0x8007052E) } => "Bind hesabının kullanıcı adı veya parolası hatalı.",
            COMException { HResult: unchecked((int)0x80072030) } => "Yazılan LDAP Container / OU bulunamadı.",
            COMException { HResult: unchecked((int)0x8007203A) } => "LDAP sunucusuna erişilemiyor. DNS ve TCP 389 erişimini kontrol edin.",
            COMException { HResult: unchecked((int)0x80070005) } => "Hesabın AD okuma yetkisi yok.",
            _ => "Domain, LDAP Container ve bind hesabını kontrol edin; ayrıntı sunucu logundadır."
        };
        return new InvalidOperationException($"Active Directory {operation} başarısız. {hint}", exception);
    }

    private static string BuildUserSearchFilter(string query)
    {
        var escaped = EscapeLdapFilterValue(query);
        return $"(&(objectCategory=person)(objectClass=user)(|(sAMAccountName=*{escaped}*)(displayName=*{escaped}*)(userPrincipalName=*{escaped}*)))";
    }

    private static string BuildGroupSearchFilter(string query)
    {
        var escaped = EscapeLdapFilterValue(query);
        return $"(&(objectCategory=group)(|(sAMAccountName=*{escaped}*)(cn=*{escaped}*)))";
    }

    [SupportedOSPlatform("windows")]
    private static UserPrincipal? ResolveUserPrincipal(
        PrincipalContext principalContext,
        string username)
    {
        var identity = username.Trim();
        if (identity.Contains('\\'))
        {
            identity = identity[(identity.LastIndexOf('\\') + 1)..];
        }
        return UserPrincipal.FindByIdentity(principalContext,
            identity.Contains('@') ? IdentityType.UserPrincipalName : IdentityType.SamAccountName, identity);
    }

    [SupportedOSPlatform("windows")]
    private static string ReadPropertyValue(SearchResult result, string propertyName)
    {
        if (!result.Properties.Contains(propertyName) || result.Properties[propertyName].Count == 0)
        {
            return string.Empty;
        }

        return result.Properties[propertyName][0]?.ToString() ?? string.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadSecurityIdentifier(SearchResult result, string propertyName)
    {
        if (!result.Properties.Contains(propertyName) || result.Properties[propertyName].Count == 0)
        {
            return null;
        }

        if (result.Properties[propertyName][0] is byte[] sidBytes && sidBytes.Length > 0)
        {
            return new SecurityIdentifier(sidBytes, 0).Value;
        }

        return null;
    }

    private static void AddGroupCandidate(ISet<string> groupSet, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        groupSet.Add(value.Trim());
    }

    private static string EscapeLdapFilterValue(string value)
    {
        return value
            .Replace("\\", "\\5c", StringComparison.Ordinal)
            .Replace("*", "\\2a", StringComparison.Ordinal)
            .Replace("(", "\\28", StringComparison.Ordinal)
            .Replace(")", "\\29", StringComparison.Ordinal)
            .Replace("\0", "\\00", StringComparison.Ordinal);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
