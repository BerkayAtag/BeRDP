namespace BeRdp.Backend.Dtos.Responses;

public class AdminAccessGroupResponse
{
    public Guid Id { get; init; }
    public required string GroupName { get; init; }
    public string? DomainName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
