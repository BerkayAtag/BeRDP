using System.ComponentModel.DataAnnotations;

namespace BeRdp.Backend.Dtos.Requests;

public class BulkMachineAssignmentRequest
{
    public Guid UserId { get; set; }
    [Required]
    [MinLength(1)]
    [MaxLength(5000)]
    public IReadOnlyList<Guid> MachineIds { get; set; } = Array.Empty<Guid>();
}
