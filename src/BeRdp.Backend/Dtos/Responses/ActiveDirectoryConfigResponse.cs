namespace BeRdp.Backend.Dtos.Responses;

public class ActiveDirectoryConfigResponse
{
    public bool Enabled { get; init; }
    public required string DomainName { get; init; }
    public string? LdapContainer { get; init; }
    public bool UseMachineCredentials { get; init; }
    public string? BindUsername { get; init; }
    public bool HasBindPassword { get; init; }
}
