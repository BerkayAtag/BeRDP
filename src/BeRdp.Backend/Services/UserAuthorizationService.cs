using BeRdp.Backend.Data;
using BeRdp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace BeRdp.Backend.Services;

public sealed class UserAuthorizationService(AppDbContext dbContext)
{
    public async Task<bool> IsAdminAsync(
        User user,
        IEnumerable<string> groupNames,
        CancellationToken cancellationToken)
    {
        if (user.IsAdmin)
        {
            return true;
        }

        if (!user.IsActiveDirectoryUser)
        {
            return false;
        }

        var groups = groupNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (groups.Count == 0)
        {
            return false;
        }

        var grants = await dbContext.AdminAccessGroups.AsNoTracking().ToListAsync(cancellationToken);
        return grants.Any(grant => groups.Contains(string.IsNullOrWhiteSpace(grant.DomainName)
            ? grant.GroupName
            : $"{grant.DomainName}\\{grant.GroupName}"));
    }

    /// <summary>
    /// An AD group admin grant also opens the console with full rights; explicit
    /// per-user console settings are kept for everyone else.
    /// </summary>
    public static (bool CanAccess, ConsoleRole Role) EffectiveConsoleAccess(User user, bool effectiveIsAdmin)
    {
        var groupGranted = effectiveIsAdmin && !user.IsAdmin;
        return (user.CanAccessAdminConsole || groupGranted, groupGranted ? ConsoleRole.Admin : user.ConsoleRole);
    }

    public bool CanAccessAdminConsole(User user)
    {
        return user.CanAccessAdminConsole;
    }

    public bool IsConsoleAdmin(User user)
    {
        return user.ConsoleRole == ConsoleRole.Admin;
    }
}
