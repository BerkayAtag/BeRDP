namespace BeRdp.Backend.Dtos.Responses;

public class ActiveDirectoryUserResponse
{
    public required string SamAccountName { get; init; }
    public required string DisplayName { get; init; }
    public required string DomainName { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? Sid { get; init; }
    public string? DistinguishedName { get; init; }
}
