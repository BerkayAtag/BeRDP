namespace BeRdp.Backend.Dtos.Responses;

public class ActiveDirectoryGroupResponse
{
    public required string GroupName { get; init; }
    public required string DomainName { get; init; }
    public string? DistinguishedName { get; init; }
}
