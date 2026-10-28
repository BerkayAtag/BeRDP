namespace BeRdp.Backend.Dtos.Requests;

public class AssignMachineRequest
{
    public Guid UserId { get; set; }
    public Guid MachineId { get; set; }
}
