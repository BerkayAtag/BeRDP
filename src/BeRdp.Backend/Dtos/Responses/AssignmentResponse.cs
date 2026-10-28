namespace BeRdp.Backend.Dtos.Responses;

public class AssignmentResponse
{
    public Guid UserId { get; init; }
    public required string Username { get; init; }
    public required string UserDisplayName { get; init; }
    public Guid MachineId { get; init; }
    public required string MachineName { get; init; }
    public required string MachineIpAddress { get; init; }
}
