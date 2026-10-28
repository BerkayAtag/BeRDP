using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BeRdp.Backend.Models;
using Microsoft.IdentityModel.Tokens;

namespace BeRdp.Backend.Services;

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string GenerateToken(User user, bool isAdmin, IReadOnlyList<string>? activeDirectoryGroupNames = null)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var signingKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = jwtSection["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        var expiresMinutes = int.TryParse(jwtSection["ExpiresMinutes"], out var value) ? value : 480;

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User"),
            new Claim("username", user.Username),
            new Claim("security_stamp", user.SecurityStamp),
            // Admin Console Access Control
            new Claim("CanAccessAdminConsole", user.CanAccessAdminConsole.ToString().ToLowerInvariant()),
            new Claim("ConsoleRole", user.ConsoleRole.ToString())
        };

        if (activeDirectoryGroupNames is not null)
        {
            foreach (var groupName in activeDirectoryGroupNames
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new Claim("ad_group", groupName.ToLowerInvariant()));
            }
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
