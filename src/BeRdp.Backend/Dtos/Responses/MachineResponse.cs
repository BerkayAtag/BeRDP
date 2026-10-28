namespace BeRdp.Backend.Dtos.Responses;

public class MachineResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string IpAddress { get; init; }
    public string? RdpHostName { get; init; }
    public int RdpPort { get; init; }
    public bool IsOnline { get; init; }
    public int AssignedUserCount { get; init; }
    public int AssignedAdGroupCount { get; init; }
    public IReadOnlyList<string> AssignedUserDisplayNames { get; init; } = [];
    public IReadOnlyList<string> AssignedUsernames { get; init; } = [];
    public IReadOnlyList<string> AssignedAdGroups { get; init; } = [];
}
