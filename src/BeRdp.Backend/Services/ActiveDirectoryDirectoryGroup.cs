namespace BeRdp.Backend.Services;

public class ActiveDirectoryDirectoryGroup
{
    public required string GroupName { get; init; }
    public required string DomainName { get; init; }
    public string? DistinguishedName { get; init; }
}
