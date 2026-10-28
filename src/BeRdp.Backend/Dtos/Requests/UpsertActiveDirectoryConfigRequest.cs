using System.ComponentModel.DataAnnotations;

namespace BeRdp.Backend.Dtos.Requests;

public class UpsertActiveDirectoryConfigRequest
{
    public bool Enabled { get; set; }

    [MaxLength(128)]
    public string DomainName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? LdapContainer { get; set; }

    public bool UseMachineCredentials { get; set; } = true;

    [MaxLength(256)]
    public string? BindUsername { get; set; }

    [MaxLength(256)]
    public string? BindPassword { get; set; }
}
