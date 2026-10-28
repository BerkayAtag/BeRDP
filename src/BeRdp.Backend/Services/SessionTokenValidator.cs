using System.Security.Claims;
using BeRdp.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace BeRdp.Backend.Services;

public sealed class SessionTokenValidator(AppDbContext dbContext, UserAuthorizationService authorizationService,
    IActiveDirectoryConfigurationService? directoryConfiguration = null, IActiveDirectoryService? directoryService = null)
{
    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (principal.Identity is not ClaimsIdentity identity ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return false;
        }

        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null ||
            !string.Equals(principal.FindFirstValue("security_stamp"), user.SecurityStamp, StringComparison.Ordinal))
        {
            return false;
        }

        if (user.IsActiveDirectoryUser)
        {
            if (directoryConfiguration is null || directoryService is null) return false;
            try
            {
                var config = await directoryConfiguration.GetAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(user.DomainName) &&
                    !ActiveDirectoryIdentity.DomainMatches(user.DomainName, config.DomainName)) return false;
                var groups = await directoryService.GetUserGroupNamesAsync(user.UserPrincipalName ?? user.Username, config, cancellationToken);
                foreach (var claim in identity.FindAll("ad_group").ToList()) identity.RemoveClaim(claim);
                foreach (var group in groups.Distinct(StringComparer.OrdinalIgnoreCase))
                    identity.AddClaim(new Claim("ad_group", group.ToLowerInvariant()));
            }
            catch (InvalidOperationException) { return false; }
        }

        var isAdmin = await authorizationService.IsAdminAsync(
            user,
            principal.FindAll("ad_group").Select(claim => claim.Value),
            cancellationToken);
        foreach (var role in identity.FindAll(ClaimTypes.Role).ToList())
        {
            identity.RemoveClaim(role);
        }

        identity.AddClaim(new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User"));

        // Console access is re-read on every request so that granting or
        // revoking it takes effect immediately instead of at token expiry.
        foreach (var claim in identity.FindAll("CanAccessAdminConsole").ToList())
        {
            identity.RemoveClaim(claim);
        }
        foreach (var claim in identity.FindAll("ConsoleRole").ToList())
        {
            identity.RemoveClaim(claim);
        }
        identity.AddClaim(new Claim("CanAccessAdminConsole",
            user.CanAccessAdminConsole ? "true" : "false"));
        identity.AddClaim(new Claim("ConsoleRole", user.ConsoleRole.ToString()));
        return true;
    }
}
