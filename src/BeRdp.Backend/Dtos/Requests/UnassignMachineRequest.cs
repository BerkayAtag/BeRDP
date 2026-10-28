namespace BeRdp.Backend.Dtos.Requests;

public class UnassignMachineRequest
{
    public Guid UserId { get; set; }
    public Guid MachineId { get; set; }
}
