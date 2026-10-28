using System.Security.Claims;
using BeRdp.Backend.Models;

namespace BeRdp.Backend.Services;

public static class MachineAccess
{
    public static IQueryable<Machine> AssignedTo(IQueryable<Machine> query, ClaimsPrincipal principal, Guid userId)
    {
        var groups = principal.FindAll("ad_group").Select(x => x.Value.Trim().ToLowerInvariant())
            .Where(x => x.Length > 0).Distinct().ToList();
        return query.Where(x => x.UserMachines.Any(a => a.UserId == userId) ||
            x.AdGroupEntitlements.Any(e => e.DomainName == null
                ? groups.Contains(e.GroupName.ToLower())
                : groups.Contains((e.DomainName + "\\" + e.GroupName).ToLower())));
    }
}
