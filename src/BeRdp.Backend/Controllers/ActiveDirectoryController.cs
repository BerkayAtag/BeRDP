using BeRdp.Backend.Dtos.Requests;
using BeRdp.Backend.Dtos.Responses;
using BeRdp.Backend.Services;
using BeRdp.Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeRdp.Backend.Controllers;

[ApiController]
[Route("ad")]
[Authorize(Policy = "AdminOnly")]
public class ActiveDirectoryController(
    IActiveDirectoryConfigurationService activeDirectoryConfigurationService,
    IActiveDirectoryService activeDirectoryService,
    AppDbContext dbContext) : ControllerBase
{
    [HttpGet("config")]
    public async Task<ActionResult<ActiveDirectoryConfigResponse>> GetConfiguration(CancellationToken cancellationToken)
    {
        var configuration = await activeDirectoryConfigurationService.GetAsync(cancellationToken);
        return Ok(ToResponse(configuration));
    }

    [HttpPut("config")]
    public async Task<ActionResult<ActiveDirectoryConfigResponse>> SaveConfiguration(
        [FromBody] UpsertActiveDirectoryConfigRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var current = await activeDirectoryConfigurationService.GetAsync(cancellationToken);
        var newConfiguration = BuildConfiguration(request, current.BindPassword);
        if (newConfiguration.Enabled && !newConfiguration.UseMachineCredentials &&
            string.IsNullOrEmpty(newConfiguration.BindPassword))
        {
            return BadRequest(new { message = "Bind parolası zorunludur." });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var saved = await activeDirectoryConfigurationService.SaveAsync(newConfiguration, cancellationToken);
        var stamp = Guid.NewGuid().ToString("N");
        await dbContext.Users.Where(user => user.IsActiveDirectoryUser)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.SecurityStamp, stamp), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(ToResponse(saved));
    }

    [HttpPost("test")]
    public async Task<ActionResult> TestConnection(
        [FromBody] UpsertActiveDirectoryConfigRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request, requireEnabled: true);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var current = await activeDirectoryConfigurationService.GetAsync(cancellationToken);
        var configuration = BuildConfiguration(request, current.BindPassword);
        configuration.Enabled = true;

        try
        {
            await activeDirectoryService.TestConnectionAsync(configuration, cancellationToken);
            return Ok(new { message = "Active Directory bağlantısı başarılı." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<ActiveDirectoryUserResponse>>> SearchUsers(
        [FromQuery] string query,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = (query ?? string.Empty).Trim();
        if (normalizedQuery.Length < 2)
        {
            return Ok(Array.Empty<ActiveDirectoryUserResponse>());
        }

        var configuration = await activeDirectoryConfigurationService.GetAsync(cancellationToken);
        try
        {
            var users = await activeDirectoryService.SearchUsersAsync(
                normalizedQuery,
                Math.Clamp(limit, 1, 200),
                configuration,
                cancellationToken);

            return Ok(users.Select(x => new ActiveDirectoryUserResponse
            {
                SamAccountName = x.SamAccountName,
                DisplayName = x.DisplayName,
                DomainName = x.DomainName,
                UserPrincipalName = x.UserPrincipalName,
                Sid = x.Sid,
                DistinguishedName = x.DistinguishedName
            }).ToList());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<ActiveDirectoryGroupResponse>>> SearchGroups(
        [FromQuery] string query,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = (query ?? string.Empty).Trim();
        if (normalizedQuery.Length < 2)
        {
            return Ok(Array.Empty<ActiveDirectoryGroupResponse>());
        }

        var configuration = await activeDirectoryConfigurationService.GetAsync(cancellationToken);
        try
        {
            var groups = await activeDirectoryService.SearchGroupsAsync(
                normalizedQuery,
                Math.Clamp(limit, 1, 200),
                configuration,
                cancellationToken);

            return Ok(groups.Select(x => new ActiveDirectoryGroupResponse
            {
                GroupName = x.GroupName,
                DomainName = x.DomainName,
                DistinguishedName = x.DistinguishedName
            }).ToList());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static ActiveDirectoryConfigResponse ToResponse(ActiveDirectoryConfiguration configuration)
    {
        return new ActiveDirectoryConfigResponse
        {
            Enabled = configuration.Enabled,
            DomainName = configuration.DomainName,
            LdapContainer = configuration.LdapContainer,
            UseMachineCredentials = configuration.UseMachineCredentials,
            BindUsername = configuration.BindUsername,
            HasBindPassword = !string.IsNullOrEmpty(configuration.BindPassword)
        };
    }

    private static ActiveDirectoryConfiguration BuildConfiguration(
        UpsertActiveDirectoryConfigRequest request,
        string? currentBindPassword)
    {
        var bindPassword = request.BindPassword is null
            ? currentBindPassword
            : request.BindPassword;

        return new ActiveDirectoryConfiguration
        {
            Enabled = request.Enabled,
            DomainName = (request.DomainName ?? string.Empty).Trim(),
            LdapContainer = NormalizeNullable(request.LdapContainer),
            UseMachineCredentials = request.UseMachineCredentials,
            BindUsername = NormalizeNullable(request.BindUsername),
            BindPassword = bindPassword ?? string.Empty
        };
    }

    private static string? ValidateRequest(
        UpsertActiveDirectoryConfigRequest request,
        bool requireEnabled = false)
    {
        if ((request.Enabled || requireEnabled) && string.IsNullOrWhiteSpace(request.DomainName))
        {
            return "AD aktifken domain adı zorunludur.";
        }

        var domainName = (request.DomainName ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(domainName))
        {
            var ldapError = ActiveDirectoryLdapPath.Validate(domainName, request.LdapContainer);
            if (ldapError is not null)
            {
                return ldapError;
            }
        }

        if ((request.Enabled || requireEnabled) &&
            !request.UseMachineCredentials && string.IsNullOrWhiteSpace(request.BindUsername))
        {
            return "Makine hesabı kullanılmıyorsa bind kullanıcı adı zorunludur.";
        }

        return null;
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
