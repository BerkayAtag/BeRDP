using System.Security.Claims;
using BeRdp.Backend.Data;
using BeRdp.Backend.Dtos.Requests;
using BeRdp.Backend.Dtos.Responses;
using BeRdp.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BeRdp.Backend.Controllers;

[ApiController]
[Route("")]
[Authorize(Policy = "AdminOnly")]
public class UsersController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? source,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        var query = BuildUserQuery(search, source);
        var normalizedSkip = Math.Max(skip ?? 0, 0);
        var normalizedTake = Math.Clamp(take ?? 500, 1, 2000);

        var users = await query
            .AsNoTracking()
            .OrderBy(x => x.Username)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .Select(x => new UserResponse
            {
                Id = x.Id,
                Username = x.Username,
                DisplayName = x.DisplayName,
                IsAdmin = x.IsAdmin,
                IsActiveDirectoryUser = x.IsActiveDirectoryUser,
                DomainName = x.DomainName,
                UserPrincipalName = x.UserPrincipalName,
                LastLoginAtUtc = x.LastLoginAtUtc,
                CanAccessAdminConsole = x.CanAccessAdminConsole,
                ConsoleRole = x.ConsoleRole
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("admin/users")]
    public async Task<ActionResult<PagedResult<UserResponse>>> GetUsersPaged(
        [FromQuery] string? search,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Clamp(page, 1, 1_000_000);
        var normalizedPageSize = Math.Clamp(pageSize, 10, 500);
        var normalizedSkip = (normalizedPage - 1) * normalizedPageSize;

        var query = BuildUserQuery(search, source);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Username)
            .Skip(normalizedSkip)
            .Take(normalizedPageSize)
            .Select(x => new UserResponse
            {
                Id = x.Id,
                Username = x.Username,
                DisplayName = x.DisplayName,
                IsAdmin = x.IsAdmin,
                IsActiveDirectoryUser = x.IsActiveDirectoryUser,
                DomainName = x.DomainName,
                UserPrincipalName = x.UserPrincipalName,
                LastLoginAtUtc = x.LastLoginAtUtc,
                CanAccessAdminConsole = x.CanAccessAdminConsole,
                ConsoleRole = x.ConsoleRole
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<UserResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        });
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserResponse>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(request.Password) > 72)
        {
            return BadRequest(new { message = "Parola UTF-8 olarak en fazla 72 byte olabilir." });
        }
        var username = request.Username.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(x => x.Username == username, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = "Bu kullanıcı adı zaten kullanılıyor." });
        }

        var user = new User
        {
            Username = username,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsAdmin = request.IsAdmin,
            CanAccessAdminConsole = request.IsAdmin,
            ConsoleRole = request.IsAdmin ? ConsoleRole.Admin : ConsoleRole.Standard,
            IsActiveDirectoryUser = false,
            DomainName = null,
            UserPrincipalName = null
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/users/{user.Id}", MapUser(user));
    }

    [HttpPost("users/ad")]
    public async Task<ActionResult<UserResponse>> UpsertActiveDirectoryUser(
        [FromBody] UpsertActiveDirectoryUserRequest request,
        CancellationToken cancellationToken)
    {
        var domainName = request.DomainName.Trim();
        var samAccountName = request.SamAccountName.Trim();
        if (string.IsNullOrWhiteSpace(samAccountName))
        {
            return BadRequest(new { message = "SamAccountName zorunludur." });
        }

        if (string.IsNullOrWhiteSpace(domainName) && string.IsNullOrWhiteSpace(request.UserPrincipalName))
        {
            return BadRequest(new { message = "DomainName veya UserPrincipalName zorunludur." });
        }

        var username = BuildNormalizedAdUsername(domainName, samAccountName, request.UserPrincipalName);
        var existingUser = await dbContext.Users.SingleOrDefaultAsync(x => x.Username == username, cancellationToken);
        if (existingUser is not null && !existingUser.IsActiveDirectoryUser)
        {
            return Conflict(new { message = "Aynı kullanıcı adıyla lokal kullanıcı bulundu." });
        }

        if (existingUser is null)
        {
            existingUser = new User
            {
                Username = username,
                DisplayName = request.DisplayName.Trim(),
                PasswordHash = string.Empty,
                IsAdmin = request.IsAdmin,
            CanAccessAdminConsole = request.IsAdmin,
            ConsoleRole = request.IsAdmin ? ConsoleRole.Admin : ConsoleRole.Standard,
                IsActiveDirectoryUser = true,
                DomainName = string.IsNullOrWhiteSpace(domainName) ? null : domainName,
                UserPrincipalName = NormalizeNullable(request.UserPrincipalName)?.ToLowerInvariant()
            };

            dbContext.Users.Add(existingUser);
        }
        else
        {
            var upn = NormalizeNullable(request.UserPrincipalName)?.ToLowerInvariant();
            if (!string.Equals(existingUser.UserPrincipalName, upn, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(domainName) &&
                 !string.Equals(existingUser.DomainName, domainName, StringComparison.OrdinalIgnoreCase)))
            {
                existingUser.SecurityStamp = Guid.NewGuid().ToString("N");
            }
            existingUser.DisplayName = request.DisplayName.Trim();
            existingUser.DomainName = string.IsNullOrWhiteSpace(domainName) ? existingUser.DomainName : domainName;
            existingUser.UserPrincipalName = upn;
            existingUser.IsActiveDirectoryUser = true;
            existingUser.PasswordHash = string.Empty;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapUser(existingUser));
    }

    [HttpPut("users/{id:guid}/role")]
    public async Task<ActionResult<UserResponse>> UpdateUserRole(
        Guid id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if ((!request.IsAdmin || request.CanAccessAdminConsole == false) && user.IsAdmin && !user.IsActiveDirectoryUser &&
            !await dbContext.Users.AnyAsync(
                x => x.Id != id && x.IsAdmin && !x.IsActiveDirectoryUser, cancellationToken))
        {
            return BadRequest(new { message = "Son lokal yöneticinin yetkisi kaldırılamaz." });
        }

        user.IsAdmin = request.IsAdmin;
        var grantConsole = request.CanAccessAdminConsole ?? (request.IsAdmin || user.CanAccessAdminConsole);
        user.CanAccessAdminConsole = grantConsole;
        user.ConsoleRole = request.IsAdmin ? ConsoleRole.Admin : ConsoleRole.Standard;
        if (!grantConsole)
        {
            user.ConsoleRole = ConsoleRole.Standard;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(MapUser(user));
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(currentUserIdClaim, out var currentUserId) && currentUserId == id)
        {
            return BadRequest(new { message = "Giriş yapan admin kendi hesabını silemez." });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsAdmin && !user.IsActiveDirectoryUser &&
            !await dbContext.Users.AnyAsync(
                x => x.Id != id && x.IsAdmin && !x.IsActiveDirectoryUser, cancellationToken))
        {
            return BadRequest(new { message = "Son lokal yönetici silinemez." });
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<User> BuildUserQuery(string? search, string? source)
    {
        var query = dbContext.Users.AsQueryable();
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var searchPattern = $"%{normalizedSearch}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Username, searchPattern) ||
                EF.Functions.Like(x.DisplayName, searchPattern) ||
                (x.UserPrincipalName != null && EF.Functions.Like(x.UserPrincipalName, searchPattern)) ||
                (x.DomainName != null && EF.Functions.Like(x.DomainName, searchPattern)));
        }

        var normalizedSource = source?.Trim().ToLowerInvariant();
        if (normalizedSource == "ad")
        {
            query = query.Where(x => x.IsActiveDirectoryUser);
        }
        else if (normalizedSource == "local")
        {
            query = query.Where(x => !x.IsActiveDirectoryUser);
        }

        return query;
    }

    private static UserResponse MapUser(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsAdmin = user.IsAdmin,
            IsActiveDirectoryUser = user.IsActiveDirectoryUser,
            DomainName = user.DomainName,
            UserPrincipalName = user.UserPrincipalName,
            LastLoginAtUtc = user.LastLoginAtUtc,
            CanAccessAdminConsole = user.CanAccessAdminConsole,
            ConsoleRole = user.ConsoleRole
        };
    }

    private static string BuildNormalizedAdUsername(string domainName, string samAccountName, string? userPrincipalName)
    {
        var normalizedSam = samAccountName.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(domainName))
        {
            return $"{domainName.Trim().ToLowerInvariant()}\\{normalizedSam}";
        }

        return userPrincipalName!.Trim().ToLowerInvariant();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
