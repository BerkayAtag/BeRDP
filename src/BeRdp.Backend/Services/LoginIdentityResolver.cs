using BeRdp.Backend.Data;
using BeRdp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace BeRdp.Backend.Services;

/// <summary>
/// Parses and resolves the different login identifier formats used by BeRDP:
/// plain SAM (user01), canonical DOMAIN\user and UPN user@domain.
/// </summary>
public static class LoginIdentityResolver
{
    public static async Task<List<User>> ResolveUsersAsync(
        AppDbContext dbContext,
        string? identifier,
        CancellationToken cancellationToken)
    {
        var parsed = LoginIdentifier.Parse(identifier);
        if (string.IsNullOrWhiteSpace(parsed.AccountName))
        {
            return [];
        }

        // Exact username/UPN match always wins. This keeps existing local and
        // imported AD accounts working without changing their stored identity.
        var exactMatches = await dbContext.Users
            .Where(x => x.Username.ToLower() == parsed.Normalized ||
                        (x.UserPrincipalName != null && x.UserPrincipalName.ToLower() == parsed.Normalized))
            .Take(2)
            .ToListAsync(cancellationToken);
        if (exactMatches.Count > 0)
        {
            return exactMatches;
        }

        if (parsed.IsUpn)
        {
            var adCandidates = await FindActiveDirectoryCandidatesAsync(
                dbContext, parsed.AccountName, cancellationToken);
            var domainMatches = adCandidates
                .Where(x => UserDomainMatches(x, parsed.Domain) ||
                            string.Equals(x.UserPrincipalName, parsed.Normalized, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();
            if (domainMatches.Count > 0)
            {
                return domainMatches;
            }

            // A local demo/service account may be entered with the corporate UPN
            // suffix even though it has no AD UPN of its own.
            return await FindLocalCandidatesAsync(dbContext, parsed.AccountName, cancellationToken);
        }

        if (parsed.Domain is not null)
        {
            var adCandidates = await FindActiveDirectoryCandidatesAsync(
                dbContext, parsed.AccountName, cancellationToken);
            var domainMatches = adCandidates
                .Where(x => UserDomainMatches(x, parsed.Domain))
                .Take(2)
                .ToList();
            if (domainMatches.Count > 0)
            {
                return domainMatches;
            }

            // Explicit domain did not identify an AD account; allow a local
            // account to be addressed as DOMAIN\user for compatibility.
            return await FindLocalCandidatesAsync(dbContext, parsed.AccountName, cancellationToken);
        }

        return (await FindActiveDirectoryCandidatesAsync(dbContext, parsed.AccountName, cancellationToken))
            .Take(2)
            .ToList();
    }

    private static async Task<List<User>> FindActiveDirectoryCandidatesAsync(
        AppDbContext dbContext,
        string accountName,
        CancellationToken cancellationToken)
    {
        var suffix = "\\" + accountName;
        var upnPrefix = accountName + "@";
        return await dbContext.Users
            .Where(x => x.IsActiveDirectoryUser &&
                        (x.Username.ToLower() == accountName ||
                         x.Username.ToLower().EndsWith(suffix) ||
                         x.Username.ToLower().StartsWith(upnPrefix) ||
                         (x.UserPrincipalName != null &&
                          x.UserPrincipalName.ToLower().StartsWith(upnPrefix))))
            .ToListAsync(cancellationToken);
    }

    private static bool UserDomainMatches(User user, string? inputDomain)
    {
        return ActiveDirectoryIdentity.DomainMatches(user.DomainName, inputDomain) ||
               ActiveDirectoryIdentity.DomainMatches(UpnDomain(user.UserPrincipalName), inputDomain) ||
               ActiveDirectoryIdentity.DomainMatches(UpnDomain(user.Username), inputDomain);
    }

    private static string? UpnDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var atIndex = value.LastIndexOf('@');
        return atIndex > 0 ? value[(atIndex + 1)..].Trim() : null;
    }

    private static async Task<List<User>> FindLocalCandidatesAsync(
        AppDbContext dbContext,
        string accountName,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Where(x => !x.IsActiveDirectoryUser && x.Username.ToLower() == accountName)
            .Take(2)
            .ToListAsync(cancellationToken);
    }
}

public sealed record LoginIdentifier(
    string Normalized,
    string AccountName,
    string? Domain,
    bool IsUpn)
{
    public static LoginIdentifier Parse(string? identifier)
    {
        var normalized = (identifier ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return new LoginIdentifier(string.Empty, string.Empty, null, false);
        }

        var atIndex = normalized.LastIndexOf('@');
        if (atIndex > 0)
        {
            return new LoginIdentifier(
                normalized,
                normalized[..atIndex].Trim(),
                normalized[(atIndex + 1)..].Trim(),
                true);
        }

        var separator = normalized.IndexOf('\\');
        if (separator > 0)
        {
            return new LoginIdentifier(
                normalized,
                normalized[(separator + 1)..].Trim(),
                normalized[..separator].Trim(),
                false);
        }

        return new LoginIdentifier(normalized, normalized, null, false);
    }
}
