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

    public bool CanAccessAdminConsole(User user)
    {
        return user.CanAccessAdminConsole;
    }

    public bool IsConsoleAdmin(User user)
    {
        return user.ConsoleRole == ConsoleRole.Admin;
    }
}
