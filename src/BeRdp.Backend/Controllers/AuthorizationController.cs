using BeRdp.Backend.Data;
using BeRdp.Backend.Dtos.Requests;
using BeRdp.Backend.Dtos.Responses;
using BeRdp.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeRdp.Backend.Controllers;

[ApiController]
[Route("admin/access-groups")]
[Authorize(Policy = "AdminOnly")]
public class AuthorizationController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminAccessGroupResponse>>> GetGroups(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AdminAccessGroups.AsNoTracking().AsQueryable();
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch.ToLowerInvariant()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.GroupName, pattern) ||
                (x.DomainName != null && EF.Functions.Like(x.DomainName, pattern)));
        }

        var groups = await query
            .OrderBy(x => x.DomainName)
            .ThenBy(x => x.GroupName)
            .Select(x => new AdminAccessGroupResponse
            {
                Id = x.Id,
                GroupName = x.GroupName,
                DomainName = x.DomainName,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(groups);
    }

    [HttpPost]
    public async Task<ActionResult<AdminAccessGroupResponse>> CreateGroup(
        [FromBody] UpsertAdminAccessGroupRequest request,
        CancellationToken cancellationToken)
    {
        var groupName = NormalizeGroupPart(request.GroupName);
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return BadRequest(new { message = "GroupName zorunludur." });
        }

        var domainName = NormalizeOptionalGroupPart(request.DomainName);
        var exists = await dbContext.AdminAccessGroups.AnyAsync(
            x => x.GroupName == groupName && x.DomainName == domainName,
            cancellationToken);

        if (exists)
        {
            return Conflict(new { message = "Bu AD grubu zaten admin yetkisine sahip." });
        }

        var group = new AdminAccessGroup
        {
            GroupName = groupName,
            DomainName = domainName
        };

        dbContext.AdminAccessGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/admin/access-groups/{group.Id}", new AdminAccessGroupResponse
        {
            Id = group.Id,
            GroupName = group.GroupName,
            DomainName = group.DomainName,
            CreatedAtUtc = group.CreatedAtUtc
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken cancellationToken)
    {
        var group = await dbContext.AdminAccessGroups.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        dbContext.AdminAccessGroups.Remove(group);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string NormalizeGroupPart(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalGroupPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }
}
