namespace BeRdp.Backend.Dtos.Responses;

public class BulkAssignmentResponse
{
    public int ProcessedCount { get; init; }
    public int AssignedCount { get; init; }
    public int UnassignedCount { get; init; }
    public int SkippedCount { get; init; }
}
