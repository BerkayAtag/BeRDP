namespace BeRdp.Backend.Services;

public class ActiveDirectoryDirectoryUser
{
    public required string SamAccountName { get; init; }
    public required string DisplayName { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? DistinguishedName { get; init; }
    public string? Sid { get; init; }
    public required string DomainName { get; init; }
}
