using BeRdp.Backend.Data;
using BeRdp.Backend.Dtos.Requests;
using BeRdp.Backend.Dtos.Responses;
using BeRdp.Backend.Hubs;
using BeRdp.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeRdp.Backend.Controllers;

[ApiController]
[Route("")]
[Authorize(Policy = "AdminOnly")]
public class AssignmentsController(AppDbContext dbContext, IHubContext<AssignmentsHub> hubContext) : ControllerBase
{
    [HttpGet("assignments")]
    public async Task<ActionResult<IReadOnlyList<AssignmentResponse>>> GetAssignments(
        [FromQuery] Guid? userId,
        [FromQuery] string? search,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        var normalizedSkip = Math.Max(skip ?? 0, 0);
        var normalizedTake = Math.Clamp(take ?? 1000, 1, 5000);
        var query = BuildAssignmentsQuery(userId, search);

        var assignments = await query
            .AsNoTracking()
            .OrderBy(x => x.User.Username)
            .ThenBy(x => x.Machine.Name)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .Select(x => new AssignmentResponse
            {
                UserId = x.UserId,
                Username = x.User.Username,
                UserDisplayName = x.User.DisplayName,
                MachineId = x.MachineId,
                MachineName = x.Machine.Name,
                MachineIpAddress = x.Machine.IpAddress
            })
            .ToListAsync(cancellationToken);

        return Ok(assignments);
    }

    [HttpGet("admin/assignments")]
    public async Task<ActionResult<PagedResult<AssignmentResponse>>> GetAssignmentsPaged(
        [FromQuery] Guid? userId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Clamp(page, 1, 1_000_000);
        var normalizedPageSize = Math.Clamp(pageSize, 20, 500);
        var normalizedSkip = (normalizedPage - 1) * normalizedPageSize;

        var query = BuildAssignmentsQuery(userId, search);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.User.Username)
            .ThenBy(x => x.Machine.Name)
            .Skip(normalizedSkip)
            .Take(normalizedPageSize)
            .Select(x => new AssignmentResponse
            {
                UserId = x.UserId,
                Username = x.User.Username,
                UserDisplayName = x.User.DisplayName,
                MachineId = x.MachineId,
                MachineName = x.Machine.Name,
                MachineIpAddress = x.Machine.IpAddress
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<AssignmentResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        });
    }

    [HttpPost("assign-machine")]
    public async Task<IActionResult> AssignMachine([FromBody] AssignMachineRequest request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.MachineId == Guid.Empty)
        {
            return BadRequest(new { message = "UserId ve MachineId zorunludur." });
        }

        var userExists = await dbContext.Users.AnyAsync(x => x.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            return NotFound(new { message = "Kullanıcı bulunamadı." });
        }

        var machineExists = await dbContext.Machines.AnyAsync(x => x.Id == request.MachineId, cancellationToken);
        if (!machineExists)
        {
            return NotFound(new { message = "Makine bulunamadı." });
        }

        var alreadyAssigned = await dbContext.UserMachines.AnyAsync(
            x => x.UserId == request.UserId && x.MachineId == request.MachineId,
            cancellationToken);

        if (alreadyAssigned)
        {
            return Conflict(new { message = "Makine zaten kullanıcıya atanmış." });
        }

        dbContext.UserMachines.Add(new UserMachine
        {
            UserId = request.UserId,
            MachineId = request.MachineId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyUserAssignmentsChangedAsync(request.UserId, cancellationToken);

        return NoContent();
    }

    [HttpDelete("unassign-machine")]
    public async Task<IActionResult> UnassignMachine([FromBody] UnassignMachineRequest request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.MachineId == Guid.Empty)
        {
            return BadRequest(new { message = "UserId ve MachineId zorunludur." });
        }

        var assignment = await dbContext.UserMachines.SingleOrDefaultAsync(
            x => x.UserId == request.UserId && x.MachineId == request.MachineId,
            cancellationToken);

        if (assignment is null)
        {
            return NotFound(new { message = "Atama bulunamadı." });
        }

        dbContext.UserMachines.Remove(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyUserAssignmentsChangedAsync(request.UserId, cancellationToken);

        return NoContent();
    }

    [HttpPost("assign-machines")]
    public async Task<ActionResult<BulkAssignmentResponse>> AssignMachines(
        [FromBody] BulkMachineAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var machineIds = request.MachineIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (request.UserId == Guid.Empty || machineIds.Count == 0)
        {
            return BadRequest(new { message = "UserId ve en az bir MachineId zorunludur." });
        }

        var userExists = await dbContext.Users.AnyAsync(x => x.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            return NotFound(new { message = "Kullanıcı bulunamadı." });
        }

        var existingMachineIds = await dbContext.Machines
            .Where(x => machineIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (existingMachineIds.Count != machineIds.Count)
        {
            return NotFound(new { message = "Gönderilen makine listesinde geçersiz kayıtlar var." });
        }

        var existingAssignments = await dbContext.UserMachines
            .Where(x => x.UserId == request.UserId && machineIds.Contains(x.MachineId))
            .Select(x => x.MachineId)
            .ToListAsync(cancellationToken);

        var existingAssignmentSet = existingAssignments.ToHashSet();
        var toAssign = machineIds.Where(machineId => !existingAssignmentSet.Contains(machineId)).ToList();

        foreach (var machineId in toAssign)
        {
            dbContext.UserMachines.Add(new UserMachine
            {
                UserId = request.UserId,
                MachineId = machineId
            });
        }

        if (toAssign.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await NotifyUserAssignmentsChangedAsync(request.UserId, cancellationToken);
        }

        return Ok(new BulkAssignmentResponse
        {
            ProcessedCount = machineIds.Count,
            AssignedCount = toAssign.Count,
            UnassignedCount = 0,
            SkippedCount = machineIds.Count - toAssign.Count
        });
    }

    [HttpPost("unassign-machines")]
    public async Task<ActionResult<BulkAssignmentResponse>> UnassignMachines(
        [FromBody] BulkMachineAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var machineIds = request.MachineIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (request.UserId == Guid.Empty || machineIds.Count == 0)
        {
            return BadRequest(new { message = "UserId ve en az bir MachineId zorunludur." });
        }

        var assignments = await dbContext.UserMachines
            .Where(x => x.UserId == request.UserId && machineIds.Contains(x.MachineId))
            .ToListAsync(cancellationToken);

        if (assignments.Count > 0)
        {
            dbContext.UserMachines.RemoveRange(assignments);
            await dbContext.SaveChangesAsync(cancellationToken);
            await NotifyUserAssignmentsChangedAsync(request.UserId, cancellationToken);
        }

        return Ok(new BulkAssignmentResponse
        {
            ProcessedCount = machineIds.Count,
            AssignedCount = 0,
            UnassignedCount = assignments.Count,
            SkippedCount = machineIds.Count - assignments.Count
        });
    }

    private IQueryable<UserMachine> BuildAssignmentsQuery(Guid? userId, string? search)
    {
        IQueryable<UserMachine> query = dbContext.UserMachines.AsQueryable();
        query = query
            .Include(x => x.User)
            .Include(x => x.Machine);

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(x =>
                EF.Functions.Like(x.User.Username, pattern) ||
                EF.Functions.Like(x.User.DisplayName, pattern) ||
                EF.Functions.Like(x.Machine.Name, pattern) ||
                EF.Functions.Like(x.Machine.IpAddress, pattern));
        }

        return query;
    }

    private Task NotifyUserAssignmentsChangedAsync(Guid userId, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Group(userId.ToString()).SendAsync("AssignmentsChanged", cancellationToken);
    }
}
