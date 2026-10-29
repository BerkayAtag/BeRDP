using BeRdp.Backend.Data;
using BeRdp.Backend.Dtos.Auth;
using BeRdp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using BeRdp.Backend.Dtos.Requests;
using BeRdp.Backend.Models;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using System.Security.Principal;

namespace BeRdp.Backend.Controllers;

[ApiController]
[Route("")]
public class AuthController(
    AppDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IActiveDirectoryConfigurationService activeDirectoryConfigurationService,
    IActiveDirectoryService activeDirectoryService,
    UserAuthorizationService authorizationService,
    ILogger<AuthController> logger,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var username = (request.Username ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { message = "Kullanıcı adı veya parola hatalı." });
        }

        var matches = await LoginIdentityResolver.ResolveUsersAsync(dbContext, username, cancellationToken);
        if (matches.Count > 1)
        {
            return BadRequest(new { message = "Bu kullanıcı adı birden fazla domaine ait. Lütfen domain\\kullanıcı veya kullanıcı@domain biçiminde giriş yapın." });
        }
        if (matches.Count == 0)
        {
            return Unauthorized(new { message = "Kullanıcı adı veya parola hatalı." });
        }
        var user = matches[0];

        var isAuthenticated = false;
        var adGroupNames = Array.Empty<string>();
        if (user.IsActiveDirectoryUser)
        {
            var activeDirectoryConfiguration = await activeDirectoryConfigurationService.GetAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(user.DomainName) &&
                !ActiveDirectoryIdentity.DomainMatches(user.DomainName, activeDirectoryConfiguration.DomainName))
                return Unauthorized(new { message = "Hesabın domain bilgisi yapılandırılan dizinle eşleşmiyor." });
            try
            {
                isAuthenticated = await activeDirectoryService.ValidateCredentialsAsync(
                    user.Username,
                    request.Password,
                    activeDirectoryConfiguration,
                    cancellationToken);
                if (isAuthenticated)
                {
                    adGroupNames = (await activeDirectoryService.GetUserGroupNamesAsync(
                        user.Username, activeDirectoryConfiguration, cancellationToken)).ToArray();
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "AD login could not be completed.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
            }
        }
        else if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            isAuthenticated = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }

        if (!isAuthenticated)
        {
            return Unauthorized(new { message = "Kullanıcı adı veya parola hatalı." });
        }

        // Console access is enforced by the Admin Console UI and the protected
        // admin endpoints; entitlement-only users still need a client token.
        return Ok(await CreateLoginResponseAsync(user, adGroupNames, cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login/windows")]
    public async Task<ActionResult<LoginResponse>> LoginWithWindows(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("WindowsAuthentication:Enabled") || !OperatingSystem.IsWindows())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Sunucuda Windows SSO etkin değil. Server Setup /SSO=1 ile yapılandırılmalıdır." });
        }
        var result = await HttpContext.AuthenticateAsync(NegotiateDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal?.Identity is not WindowsIdentity identity ||
            string.IsNullOrEmpty(identity.Name) || identity.User is null)
        {
            return Challenge(NegotiateDefaults.AuthenticationScheme);
        }
        try
        {
            var adConfiguration = await activeDirectoryConfigurationService.GetAsync(cancellationToken);
            var directoryUser = await activeDirectoryService.GetUserBySidAsync(
                identity.User.Value, adConfiguration, cancellationToken);
            var sam = directoryUser.SamAccountName.ToLowerInvariant();
            var domain = directoryUser.DomainName.ToLowerInvariant();
            var upn = directoryUser.UserPrincipalName?.ToLowerInvariant();
            var canonicalUsername = $"{domain}\\{sam}";
            var candidates = await dbContext.Users.Where(x => x.IsActiveDirectoryUser)
                .Where(x => x.Username.ToLower() == sam || x.Username.ToLower() == canonicalUsername ||
                    (upn != null && (x.Username.ToLower() == upn ||
                        (x.UserPrincipalName != null && x.UserPrincipalName.ToLower() == upn))))
                .ToListAsync(cancellationToken);
            var matches = candidates.Where(x => ActiveDirectoryIdentity.Matches(x, directoryUser)).Take(2).ToList();
            if (matches.Count != 1)
            {
                logger.LogWarning("Windows SSO identity did not resolve to one imported AD user.");
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Windows hesabınız BeRDP AD kullanıcı havuzunda tekil olarak bulunamadı. Yöneticiniz hesabınızı eklemelidir." });
            }
            var user = matches[0];

            var directoryIdentity = directoryUser.UserPrincipalName ??
                $"{directoryUser.DomainName}\\{directoryUser.SamAccountName}";
            var groups = await activeDirectoryService.GetUserGroupNamesAsync(directoryIdentity, adConfiguration, cancellationToken);
            return Ok(await CreateLoginResponseAsync(user, groups, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Windows SSO AD identity resolution failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    private async Task<LoginResponse> CreateLoginResponseAsync(
        User user, IReadOnlyList<string> adGroupNames, CancellationToken cancellationToken)
    {
        var effectiveIsAdmin = await authorizationService.IsAdminAsync(user, adGroupNames, cancellationToken);

        var previousLastLogin = user.LastLoginAtUtc;
        user.LastLoginAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var token = jwtTokenService.GenerateToken(user, effectiveIsAdmin, adGroupNames);
        var console = UserAuthorizationService.EffectiveConsoleAccess(user, effectiveIsAdmin);
        return new LoginResponse
        {
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsAdmin = effectiveIsAdmin,
            IsActiveDirectoryUser = user.IsActiveDirectoryUser,
            RdpUsername = ResolveRdpUsername(user),
            Token = token,
            LastLoginAtUtc = previousLastLogin,
            CanAccessAdminConsole = console.CanAccess,
            ConsoleRole = console.Role
        };
    }

    [Authorize]
    [HttpPost("account/password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await dbContext.Users.SingleAsync(x => x.Id == userId, cancellationToken);
        if (user.IsActiveDirectoryUser)
        {
            return BadRequest(new { message = "AD parolası domain üzerinden değiştirilmelidir." });
        }
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "Mevcut parola hatalı." });
        }
        if (Encoding.UTF8.GetByteCount(request.NewPassword) > 72)
        {
            return BadRequest(new { message = "Yeni parola UTF-8 olarak en fazla 72 byte olabilir." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Parola değiştirildi. Tüm oturumlar iptal edildi; yeniden giriş yapın." });
    }

    private static string ResolveRdpUsername(User user)
    {
        if (!user.IsActiveDirectoryUser)
        {
            return user.Username;
        }
        if (!string.IsNullOrWhiteSpace(user.UserPrincipalName))
        {
            return user.UserPrincipalName;
        }
        if (user.Username.Contains('\\') || user.Username.Contains('@'))
        {
            return user.Username;
        }
        if (!string.IsNullOrWhiteSpace(user.DomainName))
        {
            return $"{user.DomainName}\\{user.Username}";
        }
        return user.Username;
    }
}
