using System.Net;
using System.Security.Claims;
using BeRdp.Backend.Data;
using BeRdp.Backend.Dtos.Requests;
using BeRdp.Backend.Dtos.Responses;
using BeRdp.Backend.Hubs;
using BeRdp.Backend.Models;
using BeRdp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeRdp.Backend.Controllers;

[ApiController]
[Route("")]
[Authorize]
public class MachinesController(
    AppDbContext dbContext,
    IHubContext<AssignmentsHub> hubContext,
    IRdpAvailabilityService rdpAvailabilityService) : ControllerBase
{
    [HttpGet("machines/{id:guid}")]
    public async Task<ActionResult<MachineResponse>> GetAssignedMachine(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        var machine = await MachineAccess.AssignedTo(dbContext.Machines.AsNoTracking(), User, userId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (machine is null) return NotFound();
        return Ok(new MachineResponse { Id = machine.Id, Name = machine.Name, IpAddress = machine.IpAddress,
            RdpHostName = machine.RdpHostName, RdpPort = machine.RdpPort });
    }

    [HttpGet("machines")]
    public async Task<ActionResult<IReadOnlyList<MachineResponse>>> GetMachines(
        [FromQuery] string? search,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        [FromQuery] bool? assignedOnly,
        CancellationToken cancellationToken)
    {
        var normalizedSkip = Math.Max(skip ?? 0, 0);
        var normalizedTake = Math.Clamp(take ?? 500, 1, 2000);
        var normalizedSearch = search?.Trim();

        IQueryable<Machine> query = dbContext.Machines
            .AsNoTracking()
            .Include(x => x.UserMachines)
            .ThenInclude(x => x.User)
            .Include(x => x.AdGroupEntitlements);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Name, pattern) ||
                (x.RdpHostName != null && EF.Functions.Like(x.RdpHostName, pattern)) ||
                EF.Functions.Like(x.IpAddress, pattern));
        }

        // Anyone granted console access (Admin or Standard read-only) sees the full
        // machine inventory. Entitlement-only end users are filtered below. The role
        // fallback keeps already-issued admin tokens working across an upgrade.
        var hasConsoleAccess = User.IsInRole("Admin") ||
            User.FindFirst("CanAccessAdminConsole")?.Value == "true";

        if (!hasConsoleAccess || assignedOnly == true)
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(claim, out var userId))
            {
                return Forbid();
            }

            query = MachineAccess.AssignedTo(query, User, userId);
        }

        var machines = await query
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .ToListAsync(cancellationToken);

        var response = await Task.WhenAll(machines.Select(async machine => new MachineResponse
        {
            Id = machine.Id,
            Name = machine.Name,
            IpAddress = machine.IpAddress,
            RdpHostName = machine.RdpHostName,
            RdpPort = machine.RdpPort,
            IsOnline = await rdpAvailabilityService.IsAvailableAsync(machine.IpAddress, machine.RdpPort, cancellationToken),
            AssignedUserCount = machine.UserMachines.Count,
            AssignedAdGroupCount = machine.AdGroupEntitlements.Count,
            AssignedUserDisplayNames = machine.UserMachines
                .Select(x => x.User.DisplayName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList(),
            AssignedUsernames = machine.UserMachines
                .Select(x => x.User.Username)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList(),
            AssignedAdGroups = machine.AdGroupEntitlements
                .Select(x => FormatAdGroupName(x.DomainName, x.GroupName))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList()
        }));

        return Ok(response);
    }

    [HttpPost("machines/{id:guid}/browser-session")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<IActionResult> CreateBrowserSession(
        Guid id, [FromServices] BrowserRdpGateway gateway, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.IsActiveDirectoryUser) return Forbid();
        // Administrators also require an assignment to launch.
        var machine = await MachineAccess.AssignedTo(dbContext.Machines.AsNoTracking(), User, userId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (machine is null) return NotFound(new { message = "Makine bulunamadı veya erişim atamanız kaldırılmış." });
        if (!gateway.Enabled)
            return StatusCode(503, new { message = "Tarayıcı bağlantısı için Guacamole gateway yapılandırılmalıdır." });
        try
        {
            return Ok(new { launchUrl = gateway.CreateLaunchUrl(user, machine, DateTimeOffset.UtcNow), expiresInSeconds = 60 });
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<MachinesController>>()
                .LogError(ex, "Browser RDP gateway configuration is invalid.");
            return StatusCode(503, new { message = "Tarayıcı gateway yapılandırması geçersiz; sunucu yöneticisi logları kontrol etmelidir." });
        }
    }

    [HttpGet("admin/machines")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<PagedResult<MachineResponse>>> GetMachinesPaged(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Clamp(page, 1, 1_000_000);
        var normalizedPageSize = Math.Clamp(pageSize, 20, 500);
        var normalizedSkip = (normalizedPage - 1) * normalizedPageSize;
        var normalizedSearch = search?.Trim();

        var query = dbContext.Machines.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Name, pattern) ||
                (x.RdpHostName != null && EF.Functions.Like(x.RdpHostName, pattern)) ||
                EF.Functions.Like(x.IpAddress, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var machineRows = await query
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip(normalizedSkip)
            .Take(normalizedPageSize)
            .Include(x => x.UserMachines)
            .ThenInclude(x => x.User)
            .Include(x => x.AdGroupEntitlements)
            .ToListAsync(cancellationToken);

        var items = await Task.WhenAll(machineRows.Select(async machine => new MachineResponse
        {
            Id = machine.Id,
            Name = machine.Name,
            IpAddress = machine.IpAddress,
            RdpHostName = machine.RdpHostName,
            RdpPort = machine.RdpPort,
            IsOnline = await rdpAvailabilityService.IsAvailableAsync(machine.IpAddress, machine.RdpPort, cancellationToken),
            AssignedUserCount = machine.UserMachines.Count,
            AssignedAdGroupCount = machine.AdGroupEntitlements.Count,
            AssignedUserDisplayNames = machine.UserMachines
                .Select(x => x.User.DisplayName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList(),
            AssignedUsernames = machine.UserMachines
                .Select(x => x.User.Username)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList(),
            AssignedAdGroups = machine.AdGroupEntitlements
                .Select(x => FormatAdGroupName(x.DomainName, x.GroupName))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList()
        }));

        return Ok(new PagedResult<MachineResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        });
    }

    [HttpPost("machines")]
    [Authorize(Policy = "AdminOnly")]
    public Task<ActionResult<MachineResponse>> CreateMachine([FromBody] CreateMachineRequest request, CancellationToken cancellationToken)
        => SaveMachine(request, null, cancellationToken);

    [HttpPut("machines/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public Task<ActionResult<MachineResponse>> UpdateMachine(Guid id, [FromBody] CreateMachineRequest request, CancellationToken cancellationToken)
        => SaveMachine(request, id, cancellationToken);

    [HttpGet("admin/machines/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetMachineAccess(Guid id, CancellationToken cancellationToken)
    {
        var machine = await dbContext.Machines.AsNoTracking()
            .Include(x => x.UserMachines).ThenInclude(x => x.User)
            .Include(x => x.AdGroupEntitlements)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (machine is null) { return NotFound(new { message = "Makine bulunamadı." }); }
        return Ok(new
        {
            machine.Id, machine.Name, machine.IpAddress, machine.RdpHostName, machine.RdpPort,
            entitledUsers = machine.UserMachines.Select(x => new
            {
                x.UserId, x.User.Username, x.User.DisplayName
            }),
            entitledAdGroups = machine.AdGroupEntitlements.Select(x => new { x.GroupName, x.DomainName })
        });
    }

    private async Task<ActionResult<MachineResponse>> SaveMachine(
        CreateMachineRequest request, Guid? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Makine adı zorunludur." });
        }

        var ipAddress = (request.IpAddress ?? string.Empty).Trim();
        if (!IPAddress.TryParse(ipAddress, out var parsedAddress))
        {
            return BadRequest(new { message = "IP adresi geçersiz." });
        }

        ipAddress = parsedAddress.ToString();
        var rdpHostName = request.RdpHostName?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(rdpHostName) &&
            (Uri.CheckHostName(rdpHostName) != UriHostNameType.Dns || !rdpHostName.Contains('.') ||
             rdpHostName.Any(char.IsWhiteSpace)))
        {
            return BadRequest(new { message = "RDP DNS adı tam domain adı olmalıdır (örnek: vm01.example.test)." });
        }
        var exists = await dbContext.Machines.AnyAsync(x => x.IpAddress == ipAddress && x.Id != id, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = "Bu IP adresi zaten kayıtlı." });
        }

        var entitledUserIds = (request.EntitledUserIds ?? [])
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (entitledUserIds.Count > 0)
        {
            var validUserCount = await dbContext.Users
                .CountAsync(x => entitledUserIds.Contains(x.Id), cancellationToken);
            if (validUserCount != entitledUserIds.Count)
            {
                return BadRequest(new { message = "Entitlement listesinde geçersiz kullanıcılar var." });
            }
        }

        if ((request.EntitledAdGroups ?? []).Any(group => group is null || string.IsNullOrWhiteSpace(group.GroupName)))
        {
            return BadRequest(new { message = "Entitlement listesinde boş AD grubu olamaz." });
        }

        var entitledAdGroups = (request.EntitledAdGroups ?? [])
            .Select(x => new
            {
                GroupName = NormalizeGroupName(x.GroupName),
                DomainName = NormalizeDomainName(x.DomainName)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.GroupName))
            .GroupBy(x => new { x.GroupName, x.DomainName })
            .Select(x => x.Key)
            .ToList();

        var machine = id.HasValue
            ? await dbContext.Machines.Include(x => x.UserMachines).Include(x => x.AdGroupEntitlements)
                .SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
            : new Machine();
        if (machine is null) { return NotFound(new { message = "Makine bulunamadı." }); }
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            if (!id.HasValue) { dbContext.Machines.Add(machine); }
            await SaveMachineValuesAsync(machine, request, rdpHostName, ipAddress, entitledUserIds,
                entitledAdGroups.Select(x => (x.GroupName, x.DomainName)).ToList(), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await hubContext.Clients.All.SendAsync("AssignmentsChanged", cancellationToken);
        var response = new MachineResponse
        {
            Id = machine.Id,
            Name = machine.Name,
            IpAddress = machine.IpAddress,
            RdpHostName = machine.RdpHostName,
            RdpPort = machine.RdpPort,
            IsOnline = await rdpAvailabilityService.IsAvailableAsync(machine.IpAddress, machine.RdpPort, cancellationToken),
            AssignedUserCount = entitledUserIds.Count,
            AssignedAdGroupCount = entitledAdGroups.Count,
            AssignedUserDisplayNames = [],
            AssignedUsernames = [],
            AssignedAdGroups = []
        };
        if (id.HasValue) { return Ok(response); }
        return Created($"/machines/{machine.Id}", response);
    }

    private async Task SaveMachineValuesAsync(
        Machine machine, CreateMachineRequest request, string? rdpHostName, string ipAddress,
        IReadOnlyList<Guid> entitledUserIds, IReadOnlyList<(string GroupName, string? DomainName)> entitledAdGroups,
        CancellationToken cancellationToken)
    {
        machine.Name = request.Name.Trim();
        machine.IpAddress = ipAddress;
        machine.RdpHostName = string.IsNullOrEmpty(rdpHostName) ? null : rdpHostName;
        machine.RdpPort = request.RdpPort;

        var existingUserIds = machine.UserMachines.Select(x => x.UserId).ToHashSet();
        dbContext.UserMachines.RemoveRange(machine.UserMachines
            .Where(x => !entitledUserIds.Contains(x.UserId)).ToList());
        foreach (var userId in entitledUserIds.Where(x => !existingUserIds.Contains(x)))
        {
            dbContext.UserMachines.Add(new UserMachine
            {
                UserId = userId,
                Machine = machine
            });
        }

        var existingGroups = machine.AdGroupEntitlements.ToList();
        dbContext.MachineAdGroupEntitlements.RemoveRange(existingGroups.Where(existing =>
            !entitledAdGroups.Any(group =>
                string.Equals(existing.GroupName, group.GroupName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.DomainName, group.DomainName, StringComparison.OrdinalIgnoreCase))).ToList());
        foreach (var group in entitledAdGroups.Where(group => !existingGroups.Any(existing =>
            string.Equals(existing.GroupName, group.GroupName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.DomainName, group.DomainName, StringComparison.OrdinalIgnoreCase))))
        {
            dbContext.MachineAdGroupEntitlements.Add(new MachineAdGroupEntitlement
            {
                Machine = machine,
                GroupName = group.GroupName,
                DomainName = group.DomainName
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    [HttpDelete("machines/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteMachine(Guid id, CancellationToken cancellationToken)
    {
        var machine = await dbContext.Machines
            .Include(x => x.UserMachines)
            .Include(x => x.AdGroupEntitlements)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (machine is null)
        {
            return NotFound();
        }

        var affectedUserIds = machine.UserMachines
            .Select(x => x.UserId.ToString())
            .Distinct()
            .ToList();

        dbContext.Machines.Remove(machine);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var userId in affectedUserIds)
        {
            await hubContext.Clients.Group(userId).SendAsync("AssignmentsChanged", cancellationToken);
        }

        if (machine.AdGroupEntitlements.Count > 0)
        {
            await hubContext.Clients.All.SendAsync("AssignmentsChanged", cancellationToken);
        }

        return NoContent();
    }

    private static string NormalizeGroupName(string? groupName)
    {
        return string.IsNullOrWhiteSpace(groupName) ? string.Empty : groupName.Trim().ToLowerInvariant();
    }

    private static string? NormalizeDomainName(string? domainName)
    {
        return string.IsNullOrWhiteSpace(domainName) ? null : domainName.Trim().ToLowerInvariant();
    }

    private static string FormatAdGroupName(string? domainName, string groupName)
    {
        return string.IsNullOrWhiteSpace(domainName)
            ? groupName
            : $"{domainName}\\{groupName}";
    }

}
